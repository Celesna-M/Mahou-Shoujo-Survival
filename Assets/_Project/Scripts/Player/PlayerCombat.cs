using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Phase 2: จัดการระบบโจมตีระยะประชิด (Melee Brawler Combo) ของผู้เล่น
/// - เปิด/ปิด Hitbox ตามเฟรมแอนิเมชัน (hit / end)
/// - Hit-stop: เมื่อตีโดนศัตรู สั่ง Time.timeScale = 0.1f นาน 0.05 วินาที แล้วคืนค่าเป็น 1
/// - Screen Shake: เรียกใช้ Cinemachine Impulse สั่นกล้องเมื่อโจมตีจบคอมโบหนัก (Surprise Uppercut / หมัดที่ 3)
/// - Animation Cancel: อนุญาตให้ปุ่ม Dodge ตัดท่าโจมตีได้ทันทีเสมอ
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Hitbox Setup (Array สำหรับหมัดซ้าย/ขวา/อาวุธ)")]
    [Tooltip("ลาก Collider ของจุดชก/ฟันมาใส่ที่นี่ หากเว้นว่างไว้สคริปต์จะค้นหาอัตโนมัติ")]
    [SerializeField] private Collider[] hitboxes;

    [Header("Mouse & Camera Filter")]
    [Tooltip("เปิด/ปิดการใช้คลิกซ้ายโจมตี")]
    [SerializeField] private bool allowLeftClickAttack = true;
    [Tooltip("ระยะลากเมาส์ขั้นต่ำ (Pixels) ที่จะถือว่าเป็นการลากหันกล้อง ไม่ใช่การคลิกโจมตี")]
    [SerializeField] private float dragThreshold = 14f;
    [Tooltip("ล็อกเมาส์กลางจออัตโนมัติในฉากต่อสู้")]
    [SerializeField] private bool autoLockCursor = false;

    private Vector2 mouseDownPosition;
    private float mouseDownTime;
    private bool isMousePotentialAttack;

    [Header("Combat Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float heavyFinisherDamageMultiplier = 1.4f;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField] private float combatExitDelay = 4.0f;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Game Feel: Hit-stop & Screen Shake (Phase 2)")]
    [Tooltip("เวลาหน่วง Hit-stop เมื่อตีโดนศัตรู (วินาที)")]
    [SerializeField] private float hitStopDuration = 0.05f;
    [Tooltip("ความเร็วเวลาขณะเกิด Hit-stop (เช่น 0.1f)")]
    [SerializeField] private float hitStopTimeScale = 0.1f;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("แรงสั่นกล้องเมื่อโจมตีจบคอมโบหนัก (หมัดสุดท้าย / Surprise Uppercut)")]
    [SerializeField] private float heavyComboShakeForce = 0.45f;
    [Tooltip("แรงสั่นกล้องเบาๆ ในหมัดปกติ (ใส่ 0 หากต้องการให้สั่นเฉพาะจบคอมโบหนักเท่านั้น)")]
    [SerializeField] private float normalHitShakeForce = 0.08f;

    [Header("Fallback Hitbox Sphere")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.8f;

    private PlayerMovement playerMovement;
    private PlayerExecution playerExecution;
    private CharacterController controller;
    private Animator animator;

    private float lastAttackTime = -999f;
    private bool isAttacking;
    private bool inCombat;
    private float combatTimer;
    private bool isHitboxActive;
    private int currentHitIndex;
    private int comboStepCounter;

    private readonly List<GameObject> hitEnemies = new List<GameObject>();
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private readonly Collider[] hitBuffer = new Collider[16];

    private static readonly int ParamAttack = Animator.StringToHash("Attack");
    private static readonly int ParamInCombat = Animator.StringToHash("InCombat");

    public bool InCombat => inCombat;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>();
        playerExecution = GetComponent<PlayerExecution>() ?? GetComponentInParent<PlayerExecution>() ?? GetComponentInChildren<PlayerExecution>();
        controller = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator == null && playerMovement != null)
            animator = playerMovement.Animator;
        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (attackPoint == null)
        {
            Transform found = transform.Find("AttackPoint") ?? transform.Find("Hitbox");
            if (found != null) attackPoint = found;
        }

        SetupHitboxColliders();

        if (enemyLayers.value == 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
                enemyLayers = 1 << enemyLayer;
            else
                enemyLayers = ~LayerMask.GetMask("Player", "Ignore Raycast");
        }

        SetupAnimationEventReceiver();
    }

    private void Start()
    {
        if (autoLockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetupHitboxColliders()
    {
        if (hitboxes == null || hitboxes.Length == 0)
        {
            var foundList = new List<Collider>();

            if (attackPoint != null && attackPoint.TryGetComponent<Collider>(out var col))
            {
                foundList.Add(col);
            }

            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (c != controller && (c.isTrigger || c.name.ToLower().Contains("hitbox") || c.name.ToLower().Contains("attack") || c.name.ToLower().Contains("weapon")))
                {
                    if (!foundList.Contains(c)) foundList.Add(c);
                }
            }

            if (foundList.Count > 0)
            {
                hitboxes = foundList.ToArray();
            }
        }

        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null)
                {
                    hitboxes[i].isTrigger = true;
                    hitboxes[i].enabled = false;

                    if (hitboxes[i].gameObject != gameObject)
                    {
                        var forwarder = hitboxes[i].GetComponent<HitboxTriggerForwarder>();
                        if (forwarder == null)
                            forwarder = hitboxes[i].gameObject.AddComponent<HitboxTriggerForwarder>();
                        forwarder.Initialize(this);
                    }
                }
            }
        }
    }

    private void SetupAnimationEventReceiver()
    {
        var animEventReceiver = GetComponent<PlayerAnimationEvents>()
                             ?? GetComponentInChildren<PlayerAnimationEvents>(true)
                             ?? GetComponentInParent<PlayerAnimationEvents>();

        if (animEventReceiver == null)
        {
            var targetGo = (animator != null) ? animator.gameObject : gameObject;
            targetGo.AddComponent<PlayerAnimationEvents>();
        }
    }

    private void OnEnable()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Attack.performed += OnAttackPerformed;
            playerMovement.InputActions.Player.Dodge.performed += OnDodgePerformed;
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Attack.performed -= OnAttackPerformed;
            playerMovement.InputActions.Player.Dodge.performed -= OnDodgePerformed;
        }
    }

    private void Update()
    {
        if (playerExecution != null && playerExecution.IsExecuting)
            return;

        HandleCombatStanceTimer();
        CheckDirectAttackInput();
        UpdateAttackWatchdog();

        if (isHitboxActive)
        {
            ScanAndDealDamage();
        }
    }

    private void CheckDirectAttackInput()
    {
        if (autoLockCursor && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked) ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = (Cursor.lockState != CursorLockMode.Locked);
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            isMousePotentialAttack = false;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            isMousePotentialAttack = false;
            return;
        }

        if (allowLeftClickAttack && Mouse.current != null)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    PerformAttack();
                    return;
                }
            }
            else
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    mouseDownPosition = Mouse.current.position.ReadValue();
                    mouseDownTime = Time.time;
                    isMousePotentialAttack = true;
                }
                else if (Mouse.current.leftButton.isPressed && isMousePotentialAttack)
                {
                    Vector2 currentPos = Mouse.current.position.ReadValue();
                    if (Vector2.Distance(currentPos, mouseDownPosition) > dragThreshold)
                    {
                        isMousePotentialAttack = false;
                    }
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && isMousePotentialAttack)
                {
                    if (Time.time - mouseDownTime <= 0.3f)
                    {
                        PerformAttack();
                        isMousePotentialAttack = false;
                        return;
                    }
                    isMousePotentialAttack = false;
                }
            }
        }

        if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.rightTrigger.wasPressedThisFrame))
        {
            PerformAttack();
            return;
        }

        // ปุ่ม J สำหรับโจมตีคีย์บอร์ด (ส่วนปุ่ม F สงวนไว้ให้ระบบ Execution ตามข้อตกลง)
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            PerformAttack();
            return;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
        {
            isMousePotentialAttack = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            PerformAttack();
            return;
        }
#endif
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (context.control != null && (context.control.device is Mouse || context.control.device is Touchscreen || context.control.device is Pointer))
        {
            return;
        }

        // หาก InputAction Attack ผูกปุ่ม F ไว้ด้วย ให้ข้ามถ้ากด F เพื่อไม่ให้ชนกับปุ่ม Execution [F]
        if (context.control != null && context.control.name == "f")
        {
            return;
        }

        PerformAttack();
    }

    private void OnDodgePerformed(InputAction.CallbackContext context)
    {
        if (isAttacking) CancelCombat();
    }

    public void PerformAttack()
    {
        if (playerExecution != null && playerExecution.IsExecuting) return;
        if (playerMovement != null && playerMovement.IsDodging) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        // รีเซ็ตตัวนับคอมโบหากเว้นช่วงนานเกินไป
        if (Time.time - lastAttackTime > 1.1f)
        {
            comboStepCounter = 0;
        }

        lastAttackTime = Time.time;
        comboStepCounter = (comboStepCounter % 3) + 1;
        isAttacking = true;
        isHitboxActive = false;
        hitEnemies.Clear();
        damagedTargets.Clear();

        EnterCombatState();

        if (playerMovement != null)
            playerMovement.CanMove = false;

        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool isAlreadyInAttack = state.IsName("Cross Punch") || state.IsName("Hook Punch") || state.IsName("Surprise Uppercut");

            if (!isAlreadyInAttack)
            {
                comboStepCounter = 1;
                animator.ResetTrigger(ParamAttack);
                animator.CrossFadeInFixedTime("Cross Punch", 0.06f, 0);
            }
            else
            {
                animator.SetTrigger(ParamAttack);
            }
        }

        CancelInvoke(nameof(ResetAttack));
        Invoke(nameof(ResetAttack), 0.65f);
    }

    /// <summary>
    /// ตรวจสอบว่าการโจมตีจังหวะปัจจุบันเป็นท่าปิดคอมโบหนัก (Surprise Uppercut / หมัดที่ 3) หรือไม่
    /// </summary>
    private bool IsHeavyComboFinisher()
    {
        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Surprise Uppercut")) return true;
        }
        return comboStepCounter >= 3 || currentHitIndex >= 3;
    }

    public void EnterCombatState()
    {
        inCombat = true;
        combatTimer = combatExitDelay;

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == ParamInCombat)
                {
                    animator.SetBool(ParamInCombat, true);
                    break;
                }
            }
        }
    }

    public void ExitCombatState()
    {
        inCombat = false;
        combatTimer = 0f;

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == ParamInCombat)
                {
                    animator.SetBool(ParamInCombat, false);
                    break;
                }
            }
        }
    }

    private void HandleCombatStanceTimer()
    {
        if (inCombat)
        {
            bool isMoving = (playerMovement != null && playerMovement.IsMoving);
            if (!isMoving && animator != null)
            {
                isMoving = animator.GetFloat(PlayerAnimID.Speed) > 0.1f;
            }

            if (!isAttacking && isMoving)
            {
                ExitCombatState();
                return;
            }

            combatTimer -= Time.deltaTime;
            if (combatTimer <= 0f)
            {
                ExitCombatState();
            }
        }
    }

    private void UpdateAttackWatchdog()
    {
        if (isAttacking && Time.time > lastAttackTime + 0.4f && animator != null)
        {
            if (animator.IsInTransition(0)) return;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Cross Punch") && !state.IsName("Hook Punch") && !state.IsName("Surprise Uppercut"))
            {
                ResetAttack();
            }
        }

        if (!isAttacking && (playerExecution == null || !playerExecution.IsExecuting) && playerMovement != null && !playerMovement.CanMove)
        {
            playerMovement.CanMove = true;
        }
    }

    // =========================================================================
    // HITBOX EVENT FUNCTIONS: hit(int) & end()
    // =========================================================================

    public void hit(int hitIndex = 0)
    {
        currentHitIndex = hitIndex;
        isHitboxActive = true;
        hitEnemies.Clear();
        damagedTargets.Clear();

        if (hitboxes != null && hitboxes.Length > 0)
        {
            if (hitIndex > 0 && hitIndex <= hitboxes.Length)
            {
                int targetIdx = hitIndex - 1;
                for (int i = 0; i < hitboxes.Length; i++)
                {
                    if (hitboxes[i] != null)
                        hitboxes[i].enabled = (i == targetIdx);
                }
            }
            else
            {
                for (int i = 0; i < hitboxes.Length; i++)
                {
                    if (hitboxes[i] != null)
                        hitboxes[i].enabled = true;
                }
            }
        }

        ScanAndDealDamage();
    }

    public void end()
    {
        isHitboxActive = false;

        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null)
                    hitboxes[i].enabled = false;
            }
        }

        if (playerMovement != null && (playerExecution == null || !playerExecution.IsExecuting))
            playerMovement.CanMove = true;

        isAttacking = false;
    }

    public void OnHitboxEnd() => end();
    public void EnableHitbox() => hit(0);
    public void DisableHitbox() => end();

    public void ResetAttack()
    {
        end();
    }

    public void ProcessHit(Collider other)
    {
        if (!isHitboxActive) return;
        OnTriggerEnter(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isHitboxActive || other == null) return;
        if (other.transform.root == transform.root) return;

        var damageable = other.GetComponentInParent<IDamageable>()
                      ?? other.GetComponent<IDamageable>()
                      ?? other.GetComponentInChildren<IDamageable>();

        if (damageable != null && !damageable.IsDead && !damagedTargets.Contains(damageable))
        {
            damagedTargets.Add(damageable);

            bool isHeavyFinisher = IsHeavyComboFinisher();
            float effectiveBaseDamage = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.HasSelectedCharacter) ? GameProgressionManager.Instance.TotalEffectiveAtk : (damage + (GameProgressionManager.Instance != null ? GameProgressionManager.Instance.BonusAttackDamage : 0f));
            float finalDamage = isHeavyFinisher ? effectiveBaseDamage * heavyFinisherDamageMultiplier : effectiveBaseDamage;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(finalDamage, hitPoint);

            // Phase 2 Game Feel (Hit-stop): เมื่อ OnTriggerEnter โดนศัตรู -> สั่ง Time.timeScale = 0.1f หน่วงไว้ 0.05 วินาที แล้วคืนค่าเป็น 1
            HitStopManager.Trigger(hitStopDuration, hitStopTimeScale);

            // Phase 2 Game Feel (Screen Shake): เรียกใช้ Cinemachine Impulse สั่นกล้องสั้นๆ เมื่อโจมตีจบคอมโบหนัก
            if (impulseSource != null)
            {
                if (isHeavyFinisher)
                {
                    impulseSource.GenerateImpulse(heavyComboShakeForce);
                }
                else if (normalHitShakeForce > 0f)
                {
                    impulseSource.GenerateImpulse(normalHitShakeForce);
                }
            }
        }
    }

    private void ScanAndDealDamage()
    {
        Vector3 origin = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.1f + Vector3.up * 1.0f;
        float radius = attackRadius;

        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].enabled)
                {
                    origin = hitboxes[i].bounds.center;
                    radius = Mathf.Max(hitboxes[i].bounds.extents.x, hitboxes[i].bounds.extents.z, 0.8f);
                    break;
                }
            }
        }

        int count = Physics.OverlapSphereNonAlloc(origin, radius, hitBuffer, enemyLayers);
        for (int i = 0; i < count; i++)
        {
            OnTriggerEnter(hitBuffer[i]);
        }
    }

    /// <summary>
    /// Phase 2 Animation Cancel: ยกเลิกท่าโจมตีทันทีเมื่อผู้เล่นกด Dodge หรือเข้าสู่ท่า Execution
    /// </summary>
    public void CancelCombat()
    {
        CancelInvoke(nameof(ResetAttack));
        ResetAttack();

        if (animator != null)
        {
            animator.ResetTrigger(ParamAttack);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isHitboxActive ? Color.red : new Color(1f, 0f, 0f, 0.3f);

        if (hitboxes != null && hitboxes.Length > 0)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null)
                {
                    Gizmos.DrawWireSphere(hitboxes[i].bounds.center, Mathf.Max(hitboxes[i].bounds.extents.x, 0.5f));
                }
            }
        }
        else
        {
            Vector3 origin = attackPoint != null
                ? attackPoint.position
                : transform.position + transform.forward * 1.1f + Vector3.up * 1.0f;
            Gizmos.DrawWireSphere(origin, attackRadius);
        }
    }
}

public class HitboxTriggerForwarder : MonoBehaviour
{
    private PlayerCombat playerCombat;
    private EnemyController enemyController;

    public void Initialize(PlayerCombat combat) => playerCombat = combat;
    public void Initialize(EnemyController enemy) => enemyController = enemy;

    private void OnTriggerEnter(Collider other)
    {
        if (playerCombat != null) playerCombat.ProcessHit(other);
        if (enemyController != null) enemyController.ProcessHit(other);
    }
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ PlayerCombat.cs]
1. Awake() / Start()
   - ค้นหาและเตรียมอ้างอิงคอมโพเนนต์ทั้งหมด ตั้งค่า Hitbox Colliders, LayerMask ของศัตรู และติดตั้งตัวรับ Animation Event (PlayerAnimationEvents)

2. SetupHitboxColliders() / SetupAnimationEventReceiver()
   - ตั้งค่าให้ Collider ของหมัด/อาวุธทุกชิ้นเป็น Trigger และปิดไว้จนกว่าจะถึงเฟรมโจมตี พร้อมผูก HitboxTriggerForwarder ส่งต่อการชนกลับมาประมวลผล

3. Update() / CheckDirectAttackInput()
   - ตรวจจับการกดโจมตี (คลิกซ้าย / ปุ่ม J / จอยเกม), กรองการลากเมาส์หันกล้องออกจากคลิกโจมตี และสแกนทำดาเมจเมื่อ Hitbox เปิดทำงาน

4. PerformAttack()
   - สั่งเริ่มท่าโจมตีและนับจังหวะคอมโบ 3 หมัด (Cross Punch -> Hook Punch -> Surprise Uppercut)

5. IsHeavyComboFinisher()
   - ตรวจสอบว่าเป็นหมัดปิดคอมโบหนัก (หมัดที่ 3 / Surprise Uppercut) หรือไม่ เพื่อคูณดาเมจและสั่นหน้าจอแรงขึ้น

6. EnterCombatState() / ExitCombatState() / HandleCombatStanceTimer()
   - จัดการสถานะตั้งการ์ดต่อสู้ (InCombat) และนับถอยหลังกลับสู่ท่ายืนปกติเมื่อไม่ได้โจมตีหรือเมื่อเริ่มวิ่ง

7. hit(int hitIndex = 0) / end()
   - ฟังก์ชันหลักที่รับคำสั่งจาก Animation Event (รายการชื่ออีเวนท์ใน PlayerAnimationEvents: hit / end) เพื่อเปิดและปิด Hitbox ตามเฟรมแอนิเมชัน

8. ProcessHit(Collider other) / OnTriggerEnter(Collider other) / ScanAndDealDamage()
   - คำนวณดาเมจรวมกับโบนัสอัปเกรดจาก Hub (Raw Power) ส่งดาเมจให้ศัตรู พร้อมสั่ง Hit-stop และสั่นหน้าจอ (Cinemachine Impulse)

9. CancelCombat()
   - ยกเลิกท่าโจมตีทันทีเมื่อผู้เล่นกดกลิ้งหลบ (Animation Cancel) หรือกดใช้ท่าสังหาร (Execution)
===============================================================================
*/
