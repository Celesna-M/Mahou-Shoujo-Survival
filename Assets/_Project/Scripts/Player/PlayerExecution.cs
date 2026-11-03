using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Phase 3: ระบบท่าสังหาร (Execution Logic) สำหรับศัตรูระดับ Boss / Elite ที่เกราะแตกติด Stun
/// - กดปุ่ม [F] เมื่ออยู่ในระยะของศัตรูที่ติด Stun (CanBeExecuted == true)
/// - ล็อกตำแหน่งผู้เล่นหน้าเป้าหมาย (ไม่ตัดคัตซีนมุมกล้อง) และเล่นแอนิเมชันเร็วๆ
/// - ผู้เล่นเป็นอมตะ (I-Frame) ตลอดการออกท่า และฟื้นฟู HP เมื่อปลิดชีพสำเร็จ
/// </summary>
public class PlayerExecution : MonoBehaviour
{
    [Header("Execution Settings (Phase 3 - ปุ่ม F)")]
    [Tooltip("ระยะตรวจจับเป้าหมายที่ติด Stun เพื่อกดปุ่ม [F] ทำ Execution")]
    [SerializeField] private float executionRange = 3.5f;
    [Tooltip("ระยะห่างที่ล็อกผู้เล่นไว้หน้าศัตรูตอนออกท่า Execution")]
    [SerializeField] private float standOffDistance = 1.15f;
    [Tooltip("ความเร็วแอนิเมชันตอนทำท่า Execution (เล่นแอนิเมชันเร็วๆ ดุดัน ไม่ตัดคัตซีน)")]
    [SerializeField] private float executionAnimSpeed = 1.45f;
    [Tooltip("เวลาทั้งหมดของท่า Execution (วินาที)")]
    [SerializeField] private float executionDuration = 0.65f;
    [Tooltip("จังหวะเวลาที่หมัด/อาวุธปะทะเป้าหมายเพื่อปลิดชีพและฟื้นเลือด (วินาที)")]
    [SerializeField] private float impactTime = 0.30f;

    [Header("Reward: HP Recovery")]
    [Tooltip("เปอร์เซ็นต์เลือดที่ฟื้นฟูหลังทำ Execution สำเร็จ (0 - 100%)")]
    [SerializeField] private float healPercentageOnExecute = 35f;

    [Header("Game Feel")]
    [SerializeField] private float executeHitStopDuration = 0.08f;
    [SerializeField] private float executeHitStopTimeScale = 0.05f;
    [SerializeField] private float executeShakeForce = 0.65f;

    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private PlayerHealth playerHealth;
    private Animator animator;
    private CinemachineImpulseSource impulseSource;

    private bool isExecuting;
    private bool hasTriggeredImpact;
    private IExecutable currentTarget;
    private readonly Collider[] scanBuffer = new Collider[24];

    public bool IsExecuting => isExecuting;
    public float ExecutionRange => executionRange;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>() ?? GetComponentInParent<PlayerCombat>();
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();
        impulseSource = GetComponent<CinemachineImpulseSource>() ?? GetComponentInParent<CinemachineImpulseSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true) ?? GetComponentInParent<Animator>();
    }

    private void Update()
    {
        if (isExecuting) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            fPressed = true;
        else if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
            fPressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F))
            fPressed = true;
#endif

        if (fPressed)
        {
            TryExecuteNearbyTarget();
        }
    }

    /// <summary>
    /// ค้นหาศัตรูระดับ Elite / Boss ที่เกราะแตกติด Stun ในระยะ แล้วเริ่มทำท่า Execution ทันที
    /// </summary>
    public bool TryExecuteNearbyTarget()
    {
        if (isExecuting) return false;

        IExecutable bestTarget = FindNearestExecutableTarget();
        if (bestTarget == null) return false;

        StartCoroutine(ExecutionRoutine(bestTarget));
        return true;
    }

    public IExecutable FindNearestExecutableTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, executionRange, scanBuffer);
        IExecutable nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = scanBuffer[i];
            if (col == null) continue;

            IExecutable exec = col.GetComponentInParent<IExecutable>() ?? col.GetComponent<IExecutable>();
            if (exec != null && exec.CanBeExecuted && exec.Transform != null)
            {
                float dist = Vector3.Distance(transform.position, exec.Transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = exec;
                }
            }
        }

        return nearest;
    }

    private IEnumerator ExecutionRoutine(IExecutable target)
    {
        isExecuting = true;
        hasTriggeredImpact = false;
        currentTarget = target;

        // 1. แจ้งศัตรูให้ล็อกสถานะ Stun ไม่ให้หลุดหรือขยับระหว่างโดนท่าสังหาร
        target.OnExecutionStart();

        // 2. ยกเลิกท่าโจมตีปกติและเปิดสถานะอมตะ (I-Frame) ให้ผู้เล่นตลอดการทำท่า
        if (playerCombat != null)
        {
            playerCombat.CancelCombat();
            playerCombat.EnterCombatState();
        }

        if (playerMovement != null)
        {
            playerMovement.CanMove = false;
            playerMovement.SetExternalInvincible(true);
        }

        // 3. ล็อกตำแหน่งผู้เล่นหน้าเป้าหมาย (ไม่ตัดคัตซีนมุมกล้อง!)
        if (target.Transform != null)
        {
            Vector3 targetPos = target.Transform.position;
            Vector3 dirFromTargetToPlayer = (transform.position - targetPos);
            dirFromTargetToPlayer.y = 0f;

            if (dirFromTargetToPlayer.sqrMagnitude < 0.001f)
            {
                dirFromTargetToPlayer = -target.Transform.forward;
                dirFromTargetToPlayer.y = 0f;
            }
            dirFromTargetToPlayer.Normalize();

            Vector3 lockedPlayerPos = targetPos + (dirFromTargetToPlayer * standOffDistance);
            lockedPlayerPos.y = transform.position.y;

            if (playerMovement != null)
            {
                playerMovement.WarpToPositionAndLookAt(lockedPlayerPos, targetPos);
            }
            else
            {
                transform.position = lockedPlayerPos;
                Vector3 lookDir = targetPos - lockedPlayerPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // หมุนศัตรูให้หันหน้ามารับท่าสังหารของผู้เล่น
            Vector3 enemyLookDir = lockedPlayerPos - targetPos;
            enemyLookDir.y = 0f;
            if (enemyLookDir.sqrMagnitude > 0.001f)
            {
                target.Transform.rotation = Quaternion.LookRotation(enemyLookDir.normalized);
            }
        }

        // 4. เล่นแอนิเมชันท่าสังหารแบบรวดเร็ว ดุดัน (ไม่เปิดแถบดำคัตซีน ไม่สลับกล้อง)
        float originalAnimSpeed = 1f;
        if (animator != null)
        {
            originalAnimSpeed = animator.speed;
            animator.speed = executionAnimSpeed;
            animator.ResetTrigger(PlayerAnimID.Attack);
            animator.SetTrigger(PlayerAnimID.Execute);
        }

        // 5. รอจังหวะปะทะ (Impact)
        float clampedImpact = Mathf.Clamp(impactTime, 0.1f, executionDuration - 0.05f);
        yield return new WaitForSeconds(clampedImpact);

        TriggerExecutionImpact();

        // 6. รอจนจบท่าแล้วคืนค่าการควบคุมและปิดอมตะ
        float remainingTime = Mathf.Max(0.05f, executionDuration - clampedImpact);
        yield return new WaitForSeconds(remainingTime);

        if (animator != null)
        {
            animator.speed = originalAnimSpeed;
        }

        if (playerMovement != null)
        {
            playerMovement.SetExternalInvincible(false);
            playerMovement.CanMove = true;
        }

        currentTarget = null;
        isExecuting = false;
    }

    public void TriggerExecutionImpactFromAnim()
    {
        if (isExecuting && !hasTriggeredImpact)
        {
            TriggerExecutionImpact();
        }
    }

    private void TriggerExecutionImpact()
    {
        if (hasTriggeredImpact) return;
        hasTriggeredImpact = true;

        // ปลิดชีพเป้าหมายที่ติด Stun
        if (currentTarget != null)
        {
            currentTarget.ExecuteKill();
        }

        // ฟื้นฟูเลือดผู้เล่นตามเปอร์เซ็นต์ที่กำหนด
        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.HealPercentage(healPercentageOnExecute);

            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.SpawnFloatingFeedback("HP", "HP", Mathf.RoundToInt(healPercentageOnExecute), new Color(0.25f, 1f, 0.45f), transform.position + Vector3.up * 1.6f, "%");
            }
        }

        // Hit-stop และ Screen Shake เน้นความสะใจ
        HitStopManager.Trigger(executeHitStopDuration, executeHitStopTimeScale);

        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(executeShakeForce);
        }
    }
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ PlayerExecution.cs]
1. Awake() / Update()
   - ตรวจจับการกดปุ่ม [F] (หรือปุ่มบนจอย) เพื่อสั่งใช้งานท่าสังหาร (Execution) เมื่อไม่ติดสถานะตายหรือกำลังออกท่าอยู่

2. TryExecuteNearbyTarget() / FindNearestExecutableTarget()
   - สแกนหาศัตรูระดับ Elite / Boss ในระยะที่เกราะแตกและติด Stun (CanBeExecuted == true) ที่อยู่ใกล้ผู้เล่นที่สุด

3. ExecutionRoutine(IExecutable target)
   - Coroutine ควบคุมลำดับท่าสังหาร: ล็อกเป้าหมายให้อยู่กับที่ เปิดอมตะ (I-Frame) ให้ผู้เล่น วาร์ปผู้เล่นไปยืนด้านหน้าเป้าหมาย เร่งความเร็วแอนิเมชันสังหาร และคืนการควบคุมเมื่อจบท่า

4. TriggerExecutionImpactFromAnim() / TriggerExecutionImpact()
   - สั่งปลิดชีพเป้าหมายทันทีในจังหวะปะทะ ฟื้นฟูเลือด (HP) ให้ผู้เล่นตามเปอร์เซ็นต์ที่กำหนด พร้อมทำ Hit-stop และสั่นหน้าจอ
===============================================================================
*/
