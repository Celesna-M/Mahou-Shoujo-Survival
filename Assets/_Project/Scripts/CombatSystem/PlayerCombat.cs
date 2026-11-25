using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// CombatSystem: ระบบต่อสู้ผู้เล่น (Refactored Clean Headers: Base / Melee / Ranged / Skill / Patterns / Input / Game Feel)
// - แยก Melee และ Ranged ออกจากกัน 100% พร้อมรองรับ CombatSkillPattern_SO ทั้ง 22 รูปแบบผ่าน UniversalVFXManager
// - ปิด GPU Material Leak (ลบ PrimitiveType.Sphere + rend.material.color) และ Cache PlayerLockOn ใน Awake()
// - คงชื่อฟิลด์หลักทั้งหมดให้เข้ากับ PlayerCharacterLoader.cs Reflection 100%
public class PlayerCombat : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private bool forceRangedCasterMode = false;
    [Tooltip("Base ATK before Hub Raw Power upgrades (auto-populated from CharacterData_SO).")]
    [SerializeField] private float damage = 24f;
    [Tooltip("Melee 3rd-hit finisher damage multiplier (auto-populated from CharacterData_SO).")]
    [SerializeField] private float heavyFinisherDamageMultiplier = 1.4f;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField] private float combatExitDelay = 4.0f;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Melee")]
    [SerializeField] private Collider[] hitboxes;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.9f;
    [SerializeField] private float meleeStrikerAnimSpeed = 1.22f;
    [SerializeField] private float meleeStrikerLungeDistance = 0.55f;
    [SerializeField] private float heavyBreakerAnimSpeed = 0.92f;
    [SerializeField] private float heavyBreakerRadiusMultiplier = 1.65f;
    [Tooltip("Stagger damage multiplier for HeavyBreaker / character stagger bonus (auto-populated from CharacterData_SO).")]
    [SerializeField] private float heavyBreakerStaggerMultiplier = 1.6f;

    [Header("Ranged")]
    [SerializeField] private Transform rangedFirePoint;
    [SerializeField] private MagicProjectile magicProjectilePrefab;
    [SerializeField] private float rangedCasterAnimSpeed = 1.12f;
    [SerializeField] private float rangedProjectileSpeed = 30f;
    [Tooltip("Number of projectiles fired on Ranged 3rd-hit finisher.")]
    [SerializeField] private int rangedFinisherProjectileCount = 3;
    [Tooltip("TOTAL damage multiplier of Ranged 3rd-hit finisher (split evenly across finisher projectiles).")]
    [SerializeField] private float rangedFinisherTotalDamageMultiplier = 1.65f;
    [SerializeField] private float rangedSpreadAngle = 16f;
    [SerializeField] private float rangedShotCooldown = 0.16f;
    [SerializeField] private bool useWhiteBlueLightningVFX = false;
    [SerializeField] private GameObject rangedMuzzleCastVFXPrefab;
    [SerializeField] private GameObject rangedImpactVFXPrefab;
    [SerializeField] private GameObject rangedSplashVFXPrefab;

    [Header("Skill")]
    [SerializeField] private bool enableActiveSkill = true;
    [Tooltip("Active Skill cooldown in seconds (auto-populated from CharacterData_SO).")]
    [SerializeField] private float activeSkillBaseCooldown = 5.5f;
    [Tooltip("TOTAL Active Skill burst damage multiplier (e.g. 2.2x Melee AoE / 2.4x Ranged Multi-Tick Beam).")]
    [SerializeField] private float activeSkillDamageMultiplier = 2.4f;
    [Tooltip("Fallback delay before skill impact fires if no Animation Event 'skill'/'OnSkillImpact' triggers earlier.")]
    [SerializeField] private float activeSkillImpactDelay = 0.18f;
    [Tooltip("Active Skill animation lockout duration in seconds.")]
    [SerializeField] private float activeSkillDuration = 0.9f;
    [SerializeField] private float meleeSkillAoERadius = 4.6f;
    [Tooltip("Stagger multiplier for Melee Active Skill AoE.")]
    [SerializeField] private float meleeSkillStaggerMultiplier = 1.8f;
    [Tooltip("Ranged Beam Active Skill active duration (seconds) when no CombatSkillPattern_SO is assigned.")]
    [SerializeField] private float rangedSkillBeamDuration = 1.0f;
    [Tooltip("Ranged Beam Active Skill tick interval (seconds). Total burst stays equal to activeSkillDamageMultiplier.")]
    [SerializeField] private float rangedSkillBeamTickInterval = 0.2f;
    [Tooltip("Ranged Beam Active Skill reach (meters).")]
    [SerializeField] private float rangedSkillBeamRange = 18f;
    [Tooltip("Ranged Beam Active Skill width (meters).")]
    [SerializeField] private float rangedSkillBeamWidth = 0.85f;
    [SerializeField] private GameObject activeSkillVFXPrefab;

    [Header("Patterns")]
    [Tooltip("แพตเทิร์นคอมโบแต่ละฮิต (Index 0 = ฮิต 1, 1 = ฮิต 2, 2 = ฮิต 3 — เว้นว่างได้จะใช้จาก CharacterData_SO หรือโหมดปกติ)")]
    [SerializeField] private CombatSkillPattern_SO[] comboPatterns;
    [Tooltip("แพตเทิร์นสำหรับ Active Skill (รองรับทั้ง 22 รูปแบบ: Projectile / Beam / Area & Dash)")]
    [SerializeField] private CombatSkillPattern_SO activeSkillPattern;

    [Header("Input")]
    [SerializeField] private bool allowLeftClickAttack = true;
    [SerializeField] private bool allowRightClickSkill = false;
    [SerializeField] private float dragThreshold = 14f;
    [SerializeField] private bool autoLockCursor = false;

    [Header("Game Feel")]
    [SerializeField] private float hitStopDuration = 0.045f;
    [SerializeField] private float hitStopTimeScale = 0.1f;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float heavyComboShakeForce = 0.45f;
    [SerializeField] private float normalHitShakeForce = 0.08f;
    [SerializeField] private AudioSource combatAudioSource;
    [SerializeField] private AudioClip swingSfx;
    [SerializeField] private AudioClip hitEnemySfx;
    [SerializeField] private AudioClip heavyFinisherSfx;

    // Cached Runtime Components (Zero GetComponent in Hot Paths)
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private PlayerStateMachine stateMachine;
    private PlayerLockOn cachedLockOn;
    private AnimVFXController animVfxController;
    private CharacterController controller;
    private Animator animator;

    // Runtime State
    private Vector2 mouseDownPosition;
    private float mouseDownTime;
    private bool isMousePotentialAttack;

    private float lastAttackTime = -999f;
    private float lastRangedShotTime = -999f;
    private float nextSkillReadyTime = 0f;

    private bool isAttacking = false;
    private bool isUsingSkill = false;
    private bool hasTriggeredSkillImpact = false;
    private bool inCombat = false;
    private float combatTimer = 0f;
    private bool isMeleeHitboxActive = false;
    private int currentHitIndex = 0;
    private int comboStepCounter = 0;

    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private readonly Collider[] hitBuffer = new Collider[24];
    private Coroutine activeSkillCoroutine;
    private CombatSkillPattern_SO runtimeRangedBeamPattern;

    /// <summary>
    /// Applies all Inspector/ScriptableObject stats from CharacterData_SO cleanly with zero hardcoded overrides.
    /// </summary>
    public void ApplyCharacterData(CharacterData_SO data)
    {
        if (data == null) return;

        damage = Mathf.Max(1f, data.baseATK);
        attackCooldown = Mathf.Max(0.05f, data.attackCooldown);
        heavyFinisherDamageMultiplier = Mathf.Max(0.5f, data.heavyFinisherDamageMultiplier);
        rangedFinisherProjectileCount = Mathf.Max(1, data.rangedFinisherProjectileCount);
        rangedFinisherTotalDamageMultiplier = Mathf.Max(0.5f, data.rangedFinisherTotalDamageMultiplier);
        heavyBreakerStaggerMultiplier = Mathf.Max(0.5f, data.staggerBonusMultiplier);

        activeSkillBaseCooldown = Mathf.Max(0.5f, data.skillCooldown);
        activeSkillDamageMultiplier = Mathf.Max(0.5f, data.skillDamageMultiplier);
        activeSkillDuration = Mathf.Max(0.2f, data.skillDuration);
        meleeSkillAoERadius = Mathf.Max(0.5f, data.meleeSkillAoERadius);
        rangedSkillBeamDuration = Mathf.Max(0.2f, data.rangedBeamDuration);
        rangedSkillBeamTickInterval = Mathf.Max(0.05f, data.rangedBeamTickInterval);
        rangedSkillBeamRange = Mathf.Max(2f, data.rangedBeamRange);
        rangedSkillBeamWidth = Mathf.Max(0.2f, data.rangedBeamWidth);

        useWhiteBlueLightningVFX = data.useWhiteBlueLightningVFX;
        if (data.comboSkillPatterns != null && data.comboSkillPatterns.Length > 0)
            comboPatterns = data.comboSkillPatterns;
        if (data.activeSkillPattern != null)
            activeSkillPattern = data.activeSkillPattern;

        if (data.customMuzzleVFXPrefab != null) rangedMuzzleCastVFXPrefab = data.customMuzzleVFXPrefab;
        if (data.customImpactVFXPrefab != null) rangedImpactVFXPrefab = data.customImpactVFXPrefab;
        if (data.customActiveSkillVFXPrefab != null) activeSkillVFXPrefab = data.customActiveSkillVFXPrefab;

        SyncBaseAnimatorArchetype();
    }

    private void OnDestroy()
    {
        if (runtimeRangedBeamPattern != null)
        {
            Destroy(runtimeRangedBeamPattern);
            runtimeRangedBeamPattern = null;
        }
    }

    // Public Properties
    public bool InCombat => inCombat;
    public bool IsAttacking => isAttacking;
    public bool IsUsingSkill => isUsingSkill;
    public float ActiveSkillCooldownRemaining => Mathf.Max(0f, nextSkillReadyTime - Time.time);
    public bool IsSkillReady => enableActiveSkill && Time.time >= nextSkillReadyTime && !isUsingSkill;

    public float ActiveSkillEffectiveCooldown
    {
        get
        {
            float baseCd = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null)
                ? GameProgressionManager.Instance.ActiveCharacterData.skillCooldown
                : activeSkillBaseCooldown;
            float reduction = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.BonusSkillCooldownReduction : 0f;
            return Mathf.Max(1.0f, baseCd - reduction);
        }
    }

    public CharacterCombatArchetype GetActiveArchetype()
    {
        if (forceRangedCasterMode) return CharacterCombatArchetype.RangedCaster;
        if (GameProgressionManager.Instance == null) return CharacterCombatArchetype.MeleeStriker;
        return GameProgressionManager.Instance.SelectedCharacterArchetype;
    }

    public bool IsRangedArchetype => GetActiveArchetype() == CharacterCombatArchetype.RangedCaster;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();
        stateMachine = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();
        cachedLockOn = GetComponent<PlayerLockOn>() ?? GetComponentInParent<PlayerLockOn>();
        animVfxController = GetComponent<AnimVFXController>() ?? gameObject.AddComponent<AnimVFXController>();
        controller = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();

        if (combatAudioSource == null) combatAudioSource = GetComponent<AudioSource>();
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true) ?? (playerMovement != null ? playerMovement.Animator : null) ?? GetComponentInParent<Animator>();

        if (attackPoint == null)
            attackPoint = transform.Find("AttackPoint") ?? transform.Find("Hitbox");
        if (rangedFirePoint == null)
            rangedFirePoint = transform.Find("RangedFirePoint") ?? transform.Find("FirePoint") ?? transform.Find("MuzzlePoint") ?? attackPoint;

        SetupMeleeHitboxColliders();

        if (enemyLayers.value == 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            enemyLayers = (enemyLayer != -1) ? (1 << enemyLayer) : ~LayerMask.GetMask("Player", "Ignore Raycast");
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
        SyncBaseAnimatorArchetype();
    }

    private void SyncBaseAnimatorArchetype()
    {
        if (animator == null) return;
        PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ArchetypeID, (int)GetActiveArchetype());
    }

    private void SetupMeleeHitboxColliders()
    {
        if (hitboxes == null || hitboxes.Length == 0)
        {
            var foundList = new List<Collider>();
            if (attackPoint != null && attackPoint.TryGetComponent<Collider>(out var col))
                foundList.Add(col);

            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (c != controller && (c.isTrigger || c.name.ToLower().Contains("hitbox") || c.name.ToLower().Contains("attack") || c.name.ToLower().Contains("weapon")))
                {
                    if (!foundList.Contains(c)) foundList.Add(c);
                }
            }
            if (foundList.Count > 0) hitboxes = foundList.ToArray();
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
                        var forwarder = hitboxes[i].GetComponent<HitboxTriggerForwarder>() ?? hitboxes[i].gameObject.AddComponent<HitboxTriggerForwarder>();
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
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Attack.performed -= OnAttackPerformed;
        }

        if (isAttacking || isUsingSkill)
        {
            CancelCombat();
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        HandleCombatStanceTimer();
        CheckDirectCombatInputs();
        UpdateAttackWatchdog();

        if (!IsRangedArchetype && isMeleeHitboxActive)
        {
            ScanMeleeHitboxAndDealDamage();
        }
    }

    private void CheckDirectCombatInputs()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            isMousePotentialAttack = false;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (enableActiveSkill)
        {
            bool skillPressed = (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame))
                             || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
                             || (allowRightClickSkill && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

            if (skillPressed)
            {
                TryPerformActiveSkill();
                return;
            }
        }

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
                    PerformBasicAttack();
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
                    if (Vector2.Distance(Mouse.current.position.ReadValue(), mouseDownPosition) > dragThreshold)
                        isMousePotentialAttack = false;
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && isMousePotentialAttack)
                {
                    if (Time.time - mouseDownTime <= 0.3f)
                    {
                        PerformBasicAttack();
                        isMousePotentialAttack = false;
                        return;
                    }
                    isMousePotentialAttack = false;
                }
            }
        }

        if ((Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.rightTrigger.wasPressedThisFrame))
            || (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame))
        {
            PerformBasicAttack();
            return;
        }
#endif
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        if (context.control != null && (context.control.device is Mouse || context.control.device is Touchscreen || context.control.device is Pointer))
            return;
        if (context.control != null && (context.control.name == "f" || context.control.name == "e"))
            return;
        PerformBasicAttack();
    }

    // =========================================================================
    // BASIC ATTACK DISPATCHER (MELEE vs RANGED — STRICT NON-OVERLAP)
    // =========================================================================

    public void PerformAttack() => PerformBasicAttack();

    public void PerformBasicAttack()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (isUsingSkill) return;
        if (playerMovement != null && playerMovement.IsDodging) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (stateMachine != null && !stateMachine.TryTransitionTo(PlayerStateType.Attack)) return;

        if (Time.time - lastAttackTime > 1.15f)
            comboStepCounter = 0;

        lastAttackTime = Time.time;
        comboStepCounter = (comboStepCounter % 3) + 1;
        isAttacking = true;
        isMeleeHitboxActive = false;
        damagedTargets.Clear();

        EnterCombatState();
        if (playerMovement != null) playerMovement.CanMove = false;

        if (IsRangedArchetype)
            BeginRangedAttack();
        else
            BeginMeleeAttack();

        CancelInvoke(nameof(ResetAttack));
        Invoke(nameof(ResetAttack), 0.65f);
    }

    // =========================================================================
    // MELEE
    // =========================================================================

    private void BeginMeleeAttack()
    {
        CharacterCombatArchetype arch = GetActiveArchetype();

        if (animator != null)
        {
            animator.speed = (arch == CharacterCombatArchetype.HeavyBreaker) ? heavyBreakerAnimSpeed : meleeStrikerAnimSpeed;
            PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ComboStep, comboStepCounter);
            PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ArchetypeID, (int)arch);
            PlayerAnimID.SafeResetTrigger(animator, PlayerAnimID.Attack);
            PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Attack);
        }

        if (arch == CharacterCombatArchetype.MeleeStriker && controller != null && controller.enabled && meleeStrikerLungeDistance > 0.05f)
        {
            Vector3 lungeDir = transform.forward;
            if (cachedLockOn != null && cachedLockOn.CurrentTarget != null)
            {
                Vector3 toTarget = cachedLockOn.CurrentTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > 1.25f)
                {
                    lungeDir = toTarget.normalized;
                    transform.rotation = Quaternion.LookRotation(lungeDir);
                }
                else lungeDir = Vector3.zero;
            }
            if (lungeDir.sqrMagnitude > 0.01f)
                controller.Move(lungeDir * meleeStrikerLungeDistance);
        }
    }

    private void OpenMeleeHitbox(int hitIndex)
    {
        currentHitIndex = hitIndex;
        isMeleeHitboxActive = true;
        damagedTargets.Clear();
        PlayCombatSfx(swingSfx, 0.75f);

        if (hitboxes != null && hitboxes.Length > 0)
        {
            if (hitIndex > 0 && hitIndex <= hitboxes.Length)
            {
                int targetIdx = hitIndex - 1;
                for (int i = 0; i < hitboxes.Length; i++)
                    if (hitboxes[i] != null) hitboxes[i].enabled = (i == targetIdx);
            }
            else
            {
                for (int i = 0; i < hitboxes.Length; i++)
                    if (hitboxes[i] != null) hitboxes[i].enabled = true;
            }
        }

        ScanMeleeHitboxAndDealDamage();

        // Optional Melee-Hybrid Pattern (e.g. SlashWaveCrescent on Finisher) — strictly guarded by ArchetypeMask!
        CombatSkillPattern_SO stepPattern = ResolveComboPattern(comboStepCounter - 1);
        if (stepPattern != null && stepPattern.IsAllowedFor(GetActiveArchetype(), false))
        {
            ExecuteResolvedPattern(stepPattern, GetEffectiveBaseAttack());
        }
    }

    private void ScanMeleeHitboxAndDealDamage()
    {
        Vector3 origin = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.1f + Vector3.up * 1.0f;

        float radius = (GetActiveArchetype() == CharacterCombatArchetype.HeavyBreaker)
            ? attackRadius * heavyBreakerRadiusMultiplier
            : attackRadius;

        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].enabled)
                {
                    origin = hitboxes[i].bounds.center;
                    radius = Mathf.Max(hitboxes[i].bounds.extents.x, hitboxes[i].bounds.extents.z, radius);
                    break;
                }
            }
        }

        int count = Physics.OverlapSphereNonAlloc(origin, radius, hitBuffer, enemyLayers);
        for (int i = 0; i < count; i++)
            ApplyMeleeDamageToCollider(hitBuffer[i]);
    }

    public void ProcessHit(Collider other)
    {
        if (IsRangedArchetype || !isMeleeHitboxActive) return;
        ApplyMeleeDamageToCollider(other);
    }

    private void ApplyMeleeDamageToCollider(Collider other)
    {
        if (!isMeleeHitboxActive || other == null || other.transform.root == transform.root) return;

        var damageable = other.GetComponentInParent<IDamageable>()
                      ?? other.GetComponent<IDamageable>()
                      ?? other.GetComponentInChildren<IDamageable>();

        if (damageable != null && !damageable.IsDead && damagedTargets.Add(damageable))
        {
            bool isHeavyFinisher = IsHeavyComboFinisher();
            float baseAtk = GetEffectiveBaseAttack();
            float finalDamage = isHeavyFinisher ? baseAtk * heavyFinisherDamageMultiplier : baseAtk;
            float staggerMult = (GetActiveArchetype() == CharacterCombatArchetype.HeavyBreaker)
                ? heavyBreakerStaggerMultiplier
                : 1.0f;

            Vector3 hitPoint = VFXAnchorRig.ResolvePosition(other.transform.root, VFXSpawnAnchor.CenterBody);
            if (damageable is EnemyController enemyCtrl)
                enemyCtrl.TakeDamage(finalDamage, hitPoint, staggerMult);
            else
                damageable.TakeDamage(finalDamage, hitPoint);
            Color meleeColor = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null)
                ? GameProgressionManager.Instance.ActiveCharacterData.themeColor
                : new Color(0.95f, 0.35f, 0.80f, 1f);
            UniversalVFXManager.SpawnAtPoint(VFXEventType.MeleeHit, hitPoint, transform.rotation, null, meleeColor, isHeavyFinisher ? 1.35f : 0.95f);
            PlayCombatSfx(isHeavyFinisher && heavyFinisherSfx != null ? heavyFinisherSfx : hitEnemySfx, isHeavyFinisher ? 1.0f : 0.85f);

            HitStopManager.Trigger(hitStopDuration, hitStopTimeScale);
            if (impulseSource != null)
                impulseSource.GenerateImpulse(isHeavyFinisher ? heavyComboShakeForce : normalHitShakeForce);
        }
    }

    // =========================================================================
    // RANGED
    // =========================================================================

    private void BeginRangedAttack()
    {
        if (animator != null)
        {
            animator.speed = rangedCasterAnimSpeed;
            PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ComboStep, comboStepCounter);
            PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ArchetypeID, (int)CharacterCombatArchetype.RangedCaster);
            PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Attack);
        }

        Transform target = ResolveRangedTarget();
        if (target != null)
        {
            Vector3 flat = target.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(flat.normalized);
        }
    }

    private void FireRangedAttack(int customCount, float damageMultiplier, float splashRad)
    {
        if (Time.time < lastRangedShotTime + rangedShotCooldown) return;
        lastRangedShotTime = Time.time;

        // 1. Check Data-Driven Combo Pattern first
        CombatSkillPattern_SO stepPattern = ResolveComboPattern(comboStepCounter - 1);
        if (stepPattern != null && stepPattern.IsAllowedFor(GetActiveArchetype(), false))
        {
            ExecuteResolvedPattern(stepPattern, GetEffectiveBaseAttack());
            return;
        }

        // 2. Fallback to Pooled UniversalVFXManager Projectile (Zero Material Leak!)
        PlayCombatSfx(swingSfx, 0.8f);

        Vector3 spawnPos = GetFireOrigin();
        Transform lockTarget = ResolveRangedTarget();
        Quaternion baseRot = GetAimRotation(spawnPos, lockTarget);

        float baseAtk = GetEffectiveBaseAttack();
        int count = Mathf.Max(1, customCount);
        // Split total damageMultiplier cleanly across multi-shot volleys so multi-bolt finishers never multiply total DPS unfairly
        float perShotMult = (count > 1) ? (damageMultiplier / count) : damageMultiplier;
        float directDmg = baseAtk * perShotMult;
        float splashDmg = directDmg * 0.5f;

        bool useLightning = useWhiteBlueLightningVFX
            || (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null && GameProgressionManager.Instance.ActiveCharacterData.useWhiteBlueLightningVFX);

        Color themeColor = useLightning
            ? new Color(0.35f, 0.85f, 1.0f, 1.0f)
            : new Color(0.55f, 0.78f, 1.0f, 1.0f);

        for (int i = 0; i < count; i++)
        {
            float yawOffset = 0f;
            if (count > 1)
            {
                float norm = (float)i / (count - 1) - 0.5f;
                yawOffset = norm * rangedSpreadAngle * 2f;
            }
            Quaternion shotRot = baseRot * Quaternion.Euler(0f, yawOffset, 0f);

            UniversalVFXManager.SpawnProjectile(
                spawnPos,
                shotRot,
                gameObject,
                lockTarget,
                directDmg,
                splashDmg,
                splashRad,
                rangedProjectileSpeed,
                195f,
                magicProjectilePrefab,
                rangedMuzzleCastVFXPrefab,
                rangedImpactVFXPrefab,
                rangedSplashVFXPrefab,
                themeColor,
                useLightning,
                false);
        }
    }

    // =========================================================================
    // SKILL (DATA-DRIVEN 22 PATTERNS OR LEGACY FALLBACK)
    // =========================================================================

    public bool TryPerformActiveSkill()
    {
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (!enableActiveSkill || isUsingSkill) return false;
        if (playerMovement != null && playerMovement.IsDodging) return false;
        if (Time.time < nextSkillReadyTime) return false;
        if (stateMachine != null && !stateMachine.TryTransitionTo(PlayerStateType.Skill)) return false;

        CancelAttackOnly();

        nextSkillReadyTime = Time.time + ActiveSkillEffectiveCooldown;
        isUsingSkill = true;
        hasTriggeredSkillImpact = false;
        EnterCombatState();

        if (playerMovement != null) playerMovement.CanMove = false;

        CombatSkillPattern_SO skillPat = ResolveActiveSkillPattern();
        if (animator != null)
        {
            animator.speed = skillPat != null ? Mathf.Max(0.4f, skillPat.animatorSpeedMultiplier) : 1.15f;
            PlayerAnimID.SafeResetTrigger(animator, PlayerAnimID.Attack);
            if (skillPat != null && !string.IsNullOrEmpty(skillPat.animatorTriggerName))
                animator.SetTrigger(skillPat.animatorTriggerName);
            else
                PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Skill);
        }

        if (activeSkillCoroutine != null) StopCoroutine(activeSkillCoroutine);
        activeSkillCoroutine = StartCoroutine(ActiveSkillRoutine());
        return true;
    }

    private IEnumerator ActiveSkillRoutine()
    {
        float impactWait = Mathf.Max(0.05f, activeSkillImpactDelay);
        yield return new WaitForSeconds(impactWait);
        if (!hasTriggeredSkillImpact) ExecuteActiveSkillImpact();

        CombatSkillPattern_SO skillPat = ResolveActiveSkillPattern();
        float totalLockout = activeSkillDuration;
        if (skillPat != null && skillPat.ResolveEffectiveCategory() == SkillDeliveryCategory.Beam)
        {
            totalLockout = Mathf.Max(activeSkillDuration, skillPat.windupOrDelayDuration + (skillPat.activeDuration * 0.75f));
        }

        float remaining = Mathf.Max(0.1f, totalLockout - impactWait);
        yield return new WaitForSeconds(remaining);

        EndActiveSkillState();
    }

    public void TriggerSkillImpactFromAnim()
    {
        if (isUsingSkill && !hasTriggeredSkillImpact)
            ExecuteActiveSkillImpact();
    }

    private void ExecuteActiveSkillImpact()
    {
        if (hasTriggeredSkillImpact) return;
        hasTriggeredSkillImpact = true;

        float baseAtk = GetEffectiveBaseAttack();

        // 1. Unified 22-Pattern Active Skill (works for Melee, Ranged, Beam, Area/Dash without overlap!)
        CombatSkillPattern_SO skillPat = ResolveActiveSkillPattern();
        if (skillPat != null && skillPat.IsAllowedFor(GetActiveArchetype(), false))
        {
            ExecuteResolvedPattern(skillPat, baseAtk);
            if (impulseSource != null && skillPat.shakeIntensity > 0.05f)
                impulseSource.GenerateImpulse(skillPat.shakeIntensity);
            return;
        }

        // 2. Legacy Fallback
        float skillMult = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null)
            ? GameProgressionManager.Instance.ActiveCharacterData.skillDamageMultiplier
            : activeSkillDamageMultiplier;

        PlayCombatSfx(heavyFinisherSfx != null ? heavyFinisherSfx : swingSfx, 1.0f);

        if (IsRangedArchetype)
        {
            // Ranged Active Skill: Multi-Tick Pooled Beam Attack keeping total burst equal to skillMult (~2.4x total)
            LaunchFallbackRangedBeamSkill(baseAtk, skillMult);
            if (impulseSource != null) impulseSource.GenerateImpulse(heavyComboShakeForce);
        }
        else
        {
            Vector3 burstCenter = VFXAnchorRig.ResolvePosition(transform, VFXSpawnAnchor.MeleeSwing);
            float finalSkillDmg = baseAtk * skillMult;

            bool useLightning = useWhiteBlueLightningVFX
                || (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null && GameProgressionManager.Instance.ActiveCharacterData.useWhiteBlueLightningVFX);
            Color skillCol = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null)
                ? GameProgressionManager.Instance.ActiveCharacterData.themeColor
                : new Color(1.0f, 0.25f, 0.65f, 1f);

            UniversalVFXManager.SpawnAtPoint(VFXEventType.SkillAoE, burstCenter, transform.rotation, activeSkillVFXPrefab, skillCol, 1.35f, useLightning, false);

            int count = Physics.OverlapSphereNonAlloc(burstCenter, meleeSkillAoERadius, hitBuffer, enemyLayers);
            damagedTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || col.transform.root == transform.root) continue;
                var dmgTarget = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
                if (dmgTarget != null && !dmgTarget.IsDead && damagedTargets.Add(dmgTarget))
                {
                    float skillStaggerMult = Mathf.Max(1.0f, meleeSkillStaggerMultiplier);
                    Vector3 hitPt = col.ClosestPoint(burstCenter);
                    if (dmgTarget is EnemyController enemyCtrl)
                        enemyCtrl.TakeDamage(finalSkillDmg, hitPt, skillStaggerMult);
                    else
                        dmgTarget.TakeDamage(finalSkillDmg, hitPt);
                }
            }

            HitStopManager.Trigger(hitStopDuration * 1.4f, hitStopTimeScale);
            if (impulseSource != null) impulseSource.GenerateImpulse(heavyComboShakeForce * 1.3f);
        }
    }

    private void LaunchFallbackRangedBeamSkill(float baseAtk, float totalSkillMult)
    {
        if (runtimeRangedBeamPattern == null)
        {
            runtimeRangedBeamPattern = ScriptableObject.CreateInstance<CombatSkillPattern_SO>();
            runtimeRangedBeamPattern.name = "Runtime_RangedBeamSkillFallback";
        }

        bool useLightning = useWhiteBlueLightningVFX
            || (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null && GameProgressionManager.Instance.ActiveCharacterData.useWhiteBlueLightningVFX);

        Color themeCol = (GameProgressionManager.Instance != null && GameProgressionManager.Instance.ActiveCharacterData != null)
            ? GameProgressionManager.Instance.ActiveCharacterData.themeColor
            : new Color(0.35f, 0.85f, 1.0f, 1.0f);

        runtimeRangedBeamPattern.deliveryCategory = SkillDeliveryCategory.Beam;
        runtimeRangedBeamPattern.patternPreset = CombatPatternPreset.ContinuousChannelBeam;
        runtimeRangedBeamPattern.allowedArchetypes = SkillArchetypeMask.RangedCaster;
        runtimeRangedBeamPattern.windupOrDelayDuration = 0f;
        runtimeRangedBeamPattern.activeDuration = Mathf.Max(0.2f, rangedSkillBeamDuration);
        runtimeRangedBeamPattern.damageTickInterval = Mathf.Max(0.05f, rangedSkillBeamTickInterval);
        runtimeRangedBeamPattern.beamRange = Mathf.Max(2f, rangedSkillBeamRange);
        runtimeRangedBeamPattern.beamWidth = Mathf.Max(0.2f, rangedSkillBeamWidth);
        runtimeRangedBeamPattern.directDamageMultiplier = Mathf.Max(0.5f, totalSkillMult);
        runtimeRangedBeamPattern.splitDamageAcrossDuration = true;
        runtimeRangedBeamPattern.muzzleCastVFXPrefab = rangedMuzzleCastVFXPrefab;
        runtimeRangedBeamPattern.mainBodyVFXPrefab = activeSkillVFXPrefab;
        runtimeRangedBeamPattern.impactVFXPrefab = rangedImpactVFXPrefab;
        runtimeRangedBeamPattern.useWhiteBlueLightningVFX = useLightning;
        runtimeRangedBeamPattern.themeColor = themeCol;
        runtimeRangedBeamPattern.visualScale = 1.0f;

        ExecuteResolvedPattern(runtimeRangedBeamPattern, baseAtk);
    }

    private void EndActiveSkillState()
    {
        isUsingSkill = false;
        stateMachine?.ReturnToLocomotionIfState(PlayerStateType.Skill);
        activeSkillCoroutine = null;
        if (animator != null) animator.speed = 1.0f;
        if (playerMovement != null) playerMovement.CanMove = true;
    }

    // =========================================================================
    // PATTERN RESOLUTION HELPERS
    // =========================================================================

    private CombatSkillPattern_SO ResolveComboPattern(int index)
    {
        if (index < 0) return null;
        if (comboPatterns != null && index < comboPatterns.Length && comboPatterns[index] != null)
            return comboPatterns[index];

        var charData = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.ActiveCharacterData : null;
        if (charData != null && charData.comboSkillPatterns != null && index < charData.comboSkillPatterns.Length)
            return charData.comboSkillPatterns[index];

        return null;
    }

    private CombatSkillPattern_SO ResolveActiveSkillPattern()
    {
        if (activeSkillPattern != null) return activeSkillPattern;
        var charData = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.ActiveCharacterData : null;
        return charData != null ? charData.activeSkillPattern : null;
    }

    private void ExecuteResolvedPattern(CombatSkillPattern_SO pattern, float baseAtk)
    {
        PlayCombatSfx(pattern.castSfx != null ? pattern.castSfx : swingSfx, 0.9f);
        Vector3 origin = GetFireOrigin();
        Transform target = ResolveRangedTarget();
        Quaternion aimRot = GetAimRotation(origin, target);

        UniversalVFXManager.ExecuteSkillPattern(pattern, origin, aimRot, gameObject, target, baseAtk, false, 0);
    }

    private Vector3 GetFireOrigin()
    {
        return rangedFirePoint != null
            ? rangedFirePoint.position
            : VFXAnchorRig.ResolvePosition(transform, VFXSpawnAnchor.Muzzle);
    }

    private Quaternion GetAimRotation(Vector3 spawnPos, Transform target)
    {
        if (target != null)
        {
            Vector3 aimDir = (target.position + Vector3.up * 1.0f) - spawnPos;
            if (aimDir.sqrMagnitude > 0.01f)
                return Quaternion.LookRotation(aimDir.normalized);
        }
        return transform.rotation;
    }

    private Transform ResolveRangedTarget()
    {
        if (cachedLockOn != null && cachedLockOn.CurrentTarget != null)
            return cachedLockOn.CurrentTarget;

        int count = Physics.OverlapSphereNonAlloc(transform.position, 18f, hitBuffer, enemyLayers);
        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i];
            if (c == null || c.transform.root == transform.root) continue;
            var dmg = c.GetComponentInParent<IDamageable>() ?? c.GetComponent<IDamageable>();
            if (dmg == null || dmg.IsDead) continue;

            Transform candidate = (dmg is MonoBehaviour mb) ? mb.transform : c.transform;
            Vector3 toEnemy = candidate.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist > 0.2f && Vector3.Angle(transform.forward, toEnemy.normalized) <= 65f && dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }
        return best;
    }

    // =========================================================================
    // ANIMATION EVENTS (hit / shoot / end)
    // =========================================================================

    public void hit(int hitIndex = 0)
    {
        if (IsRangedArchetype)
        {
            bool isFinisher = IsHeavyComboFinisher();
            int shots = isFinisher ? rangedFinisherProjectileCount : 1;
            float totalMult = isFinisher ? rangedFinisherTotalDamageMultiplier : 1.0f;
            FireRangedAttack(shots, totalMult, 3.0f);
        }
        else
        {
            OpenMeleeHitbox(hitIndex);
        }
    }

    public void shoot(int volleyMode = 0)
    {
        bool isFinisher = (volleyMode >= 3 || IsHeavyComboFinisher());
        int shots = isFinisher ? rangedFinisherProjectileCount : 1;
        float totalMult = isFinisher ? rangedFinisherTotalDamageMultiplier : 1.0f;
        FireRangedAttack(shots, totalMult, 3.0f);
    }

    public void end()
    {
        isMeleeHitboxActive = false;
        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
                if (hitboxes[i] != null) hitboxes[i].enabled = false;
        }

        isAttacking = false;
        if (!isUsingSkill)
        {
            stateMachine?.ReturnToLocomotionIfState(PlayerStateType.Attack);
            if (playerMovement != null) playerMovement.CanMove = true;
            if (animator != null) animator.speed = 1.0f;
        }
    }

    public void ResetAttack() => end();

    private void CancelAttackOnly()
    {
        CancelInvoke(nameof(ResetAttack));
        end();
    }

    public void CancelCombat()
    {
        animVfxController?.CancelInterruptibleVFX();
        CancelInvoke(nameof(ResetAttack));
        if (activeSkillCoroutine != null)
        {
            StopCoroutine(activeSkillCoroutine);
            activeSkillCoroutine = null;
        }
        isUsingSkill = false;
        end();
        if (animator != null)
        {
            PlayerAnimID.SafeResetTrigger(animator, PlayerAnimID.Attack);
            PlayerAnimID.SafeResetTrigger(animator, PlayerAnimID.Skill);
            PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ComboStep, 0);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private float GetEffectiveBaseAttack()
    {
        if (GameProgressionManager.Instance != null && GameProgressionManager.Instance.HasSelectedCharacter)
            return GameProgressionManager.Instance.TotalEffectiveAtk;
        return damage + (GameProgressionManager.Instance != null ? GameProgressionManager.Instance.BonusAttackDamage : 0f);
    }

    private bool IsHeavyComboFinisher() => comboStepCounter >= 3 || currentHitIndex >= 3;

    public void EnterCombatState()
    {
        inCombat = true;
        combatTimer = combatExitDelay;
        PlayerAnimID.SafeSetBool(animator, PlayerAnimID.InCombat, true);
    }

    public void ExitCombatState()
    {
        inCombat = false;
        combatTimer = 0f;
        PlayerAnimID.SafeSetBool(animator, PlayerAnimID.InCombat, false);
        PlayerAnimID.SafeSetInt(animator, PlayerAnimID.ComboStep, 0);
    }

    private void HandleCombatStanceTimer()
    {
        if (!inCombat) return;
        combatTimer -= Time.deltaTime;
        if (combatTimer <= 0f && !isAttacking && !isUsingSkill)
            ExitCombatState();
    }

    private void UpdateAttackWatchdog()
    {
        if (playerMovement == null) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerMovement.IsDodging) return;
        if (stateMachine != null && !stateMachine.IsInLocomotion) return;

        if (!isAttacking && !isUsingSkill && !playerMovement.CanMove)
            playerMovement.CanMove = true;
    }

    private void PlayCombatSfx(AudioClip clip, float volume = 0.85f)
    {
        if (clip == null) return;
        if (combatAudioSource != null)
            combatAudioSource.PlayOneShot(clip, volume);
        else
            AudioSource.PlayClipAtPoint(clip, transform.position, volume * (GameProgressionManager.Instance != null ? GameProgressionManager.Instance.MasterVolume : 0.8f));
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ PlayerCombat.cs (Clean Headers + 22-Pattern Ready)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. จัดระเบียบ Inspector ใหม่ให้เรียบง่าย (Base / Melee / Ranged / Skill / Patterns / Input / Game Feel):
 *    - คงชื่อฟิลด์หลักทั้งหมดไว้เพื่อให้ PlayerCharacterLoader.cs ผูกค่าผ่าน Reflection ได้ 100%
 *
 * 2. ปิดจุดรั่วและเชื่อมต่อระบบ 22 แพตเทิร์น (#debug + #code):
 *    - ลบจุดสร้าง PrimitiveType.Sphere + rend.material.color ที่ทำให้ GPU Material รั่ว เปลี่ยนเป็นเรียกผ่าน
 *      UniversalVFXManager.SpawnProjectile(...) และ ExecuteSkillPattern(...) ที่ใช้ ObjectPool ทั้งหมด
 *    - Cache PlayerLockOn ใน Awake() ลดการเรียก GetComponent ซ้ำระหว่างต่อสู้
 *    - ตรวจสิทธิ์ pattern.IsAllowedFor(...) ก่อนยิงทุกครั้ง เพื่อไม่ให้สกิลสายใกล้และสายไกลทำงานทับกัน
 * ============================================================================ */
