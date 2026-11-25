using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum LostSightBehavior
{
    StandStill,
    Patrol
}

public enum RewardDropType
{
    StarDust,
    ShatteredCore
}

/// <summary>
/// EnemyController
/// - Prevents pushing the player through the floor (forces child bone colliders to isTrigger = true & keeps safe separation distance).
/// - Uses customizable Inspector Lists for Animation Events (Default: Open/Damage = ["hite"], Close = ["end"]).
///   You can freely add or remove event names in the Inspector List!
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("1. Animation Event Name Lists (เพิ่ม/ลดชื่อ Event ได้ตามใจ)")]
    [Tooltip("List of Animation Event names that open the hitbox & deal damage (Default: hite)")]
    [SerializeField] private List<string> openHitboxEventNames = new List<string> { "hite" };
    [Tooltip("List of Animation Event names that close the hitbox (Default: end)")]
    [SerializeField] private List<string> closeHitboxEventNames = new List<string> { "end" };

    [Header("Phase 3: Enemy Dead Settings")]
    [SerializeField] private float destroyDelayOnDeath = 0f;

    [Header("Phase 3: Elite / Boss & Stagger System")]
    [SerializeField] private bool isEliteOrBoss = false;
    [SerializeField] private string enemyDisplayName = "ELITE ENTITY";
    [SerializeField] private float maxStagger = 100f;
    [SerializeField] private float staggerDamageMultiplier = 1.15f;
    [SerializeField] private float stunDuration = 5.0f;

    [Header("Phase 3 & 5: Drops & RNG Rare Drop")]
    [SerializeField] private int starDustReward = 15;
    [Range(0f, 100f)]
    [SerializeField] private float shatteredCoreDropChance = 20f;
    [SerializeField] private int shatteredCoreDropAmount = 1;
    [SerializeField] private GameObject dropItemPrefab;

    [Header("Stage Rank Scaling & Attack Telegraph (Core Loop Balance)")]
    [Tooltip("Automatically scales Max HP, Attack Damage, Stagger, and Star Dust reward by Stage Rank (Rank E -> S)")]
    [SerializeField] private bool enableStageRankScaling = true;
    [SerializeField] private float hpMultiplierPerRank = 0.28f;
    [SerializeField] private float damageMultiplierPerRank = 0.18f;
    [SerializeField] private float rewardMultiplierPerRank = 0.25f;
    [Tooltip("Flashes warning color briefly before swinging so the player can time Dodge I-Frames")]
    [SerializeField] private bool enableAttackTelegraph = true;
    [SerializeField] private float attackTelegraphDuration = 0.8f;
    [SerializeField] private Color attackTelegraphColor = new Color(1.0f, 0.55f, 0.1f, 1.0f);

    [Header("Optional Audio Hooks (ลากเสียงมาใส่ได้ถ้ามี ถ้าเว้นว่างไว้จะไม่ Error)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackWindupSfx;
    [SerializeField] private AudioClip attackHitPlayerSfx;
    [SerializeField] private AudioClip hurtSfx;
    [SerializeField] private AudioClip stunBreakSfx;

            [Header("GDD Combat Feedback: Knockback, Ranged Aim Line & Boss 3 Patterns")]
    [SerializeField] private bool enableKnockbackOnHit = true;
    [SerializeField] private float knockbackDistance = 0.45f;
    [Tooltip("เส้นเล็งเตือนสีแดงล่วงหน้า 1.0 วินาที สำหรับมอนสเตอร์ตีไกล (GDD Section 7.2)")]
    [SerializeField] private float rangedAimTelegraphDuration = 1.0f;
    [SerializeField] private float rangedPreferredDistance = 8.5f;
    [Tooltip("เปิดใช้งาน 3 รูปแบบการโจมตีสำหรับบอสใหญ่ (1.ฟันต่อเนื่อง 2.พุ่งกระแทก 3.ระเบิด AoE วงแดงที่พื้น)")]
    [SerializeField] private bool enableBossThreeAttackPatterns = true;
    [SerializeField] private float bossDashSpeed = 14f;
    [SerializeField] private float bossAoERadius = 4.2f;
    [SerializeField] private float bossAoEWarningDuration = 1.0f;
    private LineRenderer rangedAimLine;
    private static Material sharedAimLineMat;
    private static Material sharedBossWarningRingMat;

    private static Material GetSharedAimLineMaterial()
    {
        if (sharedAimLineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) sharedAimLineMat = new Material(sh) { name = "Shared_EnemyAimLine_Mat" };
        }
        return sharedAimLineMat;
    }

    private static Material GetSharedBossWarningRingMaterial()
    {
        if (sharedBossWarningRingMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                sharedBossWarningRingMat = new Material(sh) { name = "Shared_BossAoEWarningRing_Mat" };
                sharedBossWarningRingMat.color = new Color(0.95f, 0.1f, 0.18f, 0.45f);
            }
        }
        return sharedBossWarningRingMat;
    }

    private int bossPatternCycleIndex = 0;

    [Header("GDD Boss Phase 2 (<50% HP) & Ranged Aim Line")]
    [Tooltip("เมื่อบอสเลือดต่ำกว่า 50% จะเพิ่มความเร็วเคลื่อนที่และโจมตีขึ้น 20% ตาม GDD Section 7.2")]
    [SerializeField] private bool enableBossPhase2Enrage = true;
    [SerializeField] private float bossPhase2SpeedMultiplier = 1.2f;
    private bool hasEnteredBossPhase2 = false;

    [Header("Ranged Enemy Settings & VFX (ตั้งค่ามอนสเตอร์ตัวตีไกล + เรียก VFX)")]
    [Tooltip("ติ๊กเปิดเพื่อให้มอนสเตอร์ตัวนี้เป็น 'มอนสเตอร์ตีไกล' (ยิงกระสุนเวท + เรียก VFX ใส่ผู้เล่นในจังหวะ Event 'hite')")]
    [SerializeField] private bool isRangedEnemy = false;
    [Tooltip("จุดปล่อยกระสุนของมอนสเตอร์ (ถ้าเว้นว่างจะยิงออกจากด้านหน้าอกมอนสเตอร์)")]
    [SerializeField] private Transform enemyFirePoint;
    [Tooltip("Prefab กระสุนของมอนสเตอร์ (ถ้าเว้นว่างจะสร้างลูกแก้วเวทสีม่วงแดงพร้อม VFX ให้อัตโนมัติ)")]
    [SerializeField] private MagicProjectile enemyProjectilePrefab;
    [SerializeField] private CombatSkillPattern_SO enemyRangedPattern;
    [Tooltip("VFX ตอนมอนสเตอร์ยิงกระสุนออก (Enemy Muzzle Cast VFX Prefab)")]
    [SerializeField] private GameObject enemyMuzzleVFXPrefab;
    [Tooltip("VFX ตอนกระสุนมอนสเตอร์ระเบิดโดนผู้เล่น (Enemy Impact VFX Prefab)")]
    [SerializeField] private GameObject enemyImpactVFXPrefab;
    [SerializeField] private float enemyProjectileSpeed = 16f;
    [SerializeField] private Color enemyProjectileVfxColor = new Color(1.0f, 0.22f, 0.45f, 1.0f);

    [Header("Detection & Combat Settings")]
    [SerializeField] private bool alwaysChasePlayer = true;
    [SerializeField] private float visionRange = 25f;
    [SerializeField] private float attackRange = 2.0f;
    [Tooltip("Minimum horizontal distance kept from the player so the enemy collider never overlaps or pushes the player through the floor")]
    [SerializeField] private float minBodySeparationDistance = 1.15f;
    [SerializeField] private float attackHitReachBonus = 0.85f;
    [SerializeField] private float activeHitWindowDuration = 0.25f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float attackDamage = 12f;

    [Header("Taunt Settings")]
    [SerializeField] private bool enableRandomTaunt = false;
    [SerializeField] private float minTauntInterval = 7.0f;
    [SerializeField] private float maxTauntInterval = 12.0f;
    [Range(0f, 100f)]
    [SerializeField] private float tauntChance = 20.0f;
    [SerializeField] private float tauntDuration = 2.0f;

    [Header("Lost Sight Behavior")]
    [SerializeField] private LostSightBehavior lostSightBehavior = LostSightBehavior.StandStill;
    [SerializeField] private Transform[] patrolPoints;

    [Header("Visual & Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer enemyRenderer;

    [Header("Hitbox Detection")]
    [SerializeField] private Collider[] hitboxes;

    private readonly List<Collider> hitPlayers = new List<Collider>();
    private readonly Collider[] overlapBuffer = new Collider[16];
    private bool isHitboxActive = false;
    private bool hasDealtDamageThisSwing = false;
    private float hitWindowTimer = 0f;

    // Cached Animator & Shader Property Hashes (Zero-GC Hot Paths)
    public static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
    public static readonly int AnimInCombatHash = Animator.StringToHash("InCombat");
    public static readonly int AnimAttackHash = Animator.StringToHash("Attack");
    public static readonly int AnimHurtHash = Animator.StringToHash("Hurt");
    public static readonly int AnimTauntHash = Animator.StringToHash("Taunt");

    private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropId = Shader.PropertyToID("_Color");
    private static readonly WaitForSeconds HurtFlashWait = new WaitForSeconds(0.18f);
    private static readonly Color StunTintColor = new Color(1f, 0.85f, 0.2f, 1f);

    // Pre-allocated FSM States (Zero-GC State Transitions)
    public IdleState IdleStateInstance { get; private set; }
    public ChaseState ChaseStateInstance { get; private set; }
    public AttackState AttackStateInstance { get; private set; }
    public ReturnState ReturnStateInstance { get; private set; }
    public PatrolState PatrolStateInstance { get; private set; }

    // Runtime Fields
    private NavMeshAgent agent;
    private AnimVFXController animVfxController;
    private Collider[] bodyColliders;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private CharacterController playerCharacterController;
    private EnemyState currentState;
    private MaterialPropertyBlock propBlock;
    private Coroutine hurtFlashCoroutine;
    private GameObject bossAoEWarningRingObj;
    private bool hasBaseColorProp;
    private bool hasColorProp;
    private Vector3 spawnPosition;
    private float currentHealth;
    private float currentStagger;
    private bool isDead = false;
    private bool isAttacking = false;
    private bool isTaunting = false;
    private bool isHurt = false;
    private bool isStunned = false;
    private float stunTimer = 0f;
    private float nextTauntCheckTime = 0f;
    private Color defaultColor = Color.white;

    public static bool GlobalFreezeAI { get; set; } = false;
    public static float NextAttackStaggerBonusMultiplier { get; set; } = 1.0f;

    public NavMeshAgent Agent => agent;
    public Animator Animator => animator;
    public Transform PlayerTransform => playerTransform;
    public PlayerHealth PlayerHealth => playerHealth;
    public EnemyState CurrentState => currentState;
    public Vector3 SpawnPosition => spawnPosition;
    public float VisionRange => visionRange;
    public float AttackRange => attackRange;
    public float AttackCooldown => attackCooldown;
    public float AttackDamage => attackDamage;
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public bool AlwaysChasePlayer => alwaysChasePlayer;
    public Transform[] PatrolPoints => patrolPoints;
    public LostSightBehavior LostSightBehaviorSetting => lostSightBehavior;

    public bool IsAttacking => isAttacking;
    public bool IsTaunting => isTaunting;
    public bool IsEliteOrBoss => isEliteOrBoss;
    public float CurrentStagger => currentStagger;
    public float MaxStagger => maxStagger;
    public bool IsStunned => isStunned;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;


    private void Reset()
    {
        EnsureDefaultEventLists();
    }

    private void Awake()
    {
        EnsureDefaultEventLists();

        IdleStateInstance = new IdleState(this);
        ChaseStateInstance = new ChaseState(this);
        AttackStateInstance = new AttackState(this);
        ReturnStateInstance = new ReturnState(this);
        PatrolStateInstance = new PatrolState(this);

        propBlock = new MaterialPropertyBlock();
        agent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        bodyColliders = GetComponentsInChildren<Collider>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        CacheRendererColorProperties();

        string lowerName = gameObject.name.ToLower();
        if (!isEliteOrBoss && (lowerName.Contains("elite") || lowerName.Contains("boss") || maxHealth >= 200f))
        {
            isEliteOrBoss = true;
        }

        currentHealth = maxHealth;
        currentStagger = isEliteOrBoss ? maxStagger : 0f;
        spawnPosition = transform.position;

        VFXAnchorRig vfxRig = GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>();
        vfxRig.RefreshBoneAnchors(animator);
        animVfxController = GetComponent<AnimVFXController>() ?? gameObject.AddComponent<AnimVFXController>();
        animVfxController.RefreshAnchorRig(vfxRig);

        SetupAnimationEventForwarder();
        SetupWeaponHitboxAndPreventSolidBonePush();
        BindCustomAnimationClipEvents();
    }

    private void CacheRendererColorProperties()
    {
        hasBaseColorProp = false;
        hasColorProp = false;
        defaultColor = Color.white;

        if (enemyRenderer == null) return;
        Material shared = enemyRenderer.sharedMaterial;
        if (shared == null) return;

        hasBaseColorProp = shared.HasProperty(BaseColorPropId);
        hasColorProp = shared.HasProperty(ColorPropId);

        if (hasBaseColorProp)
            defaultColor = shared.GetColor(BaseColorPropId);
        else if (hasColorProp)
            defaultColor = shared.GetColor(ColorPropId);
    }

    private void ApplyTintColor(Color tint)
    {
        if (enemyRenderer == null || (!hasBaseColorProp && !hasColorProp)) return;
        enemyRenderer.GetPropertyBlock(propBlock);
        if (hasBaseColorProp) propBlock.SetColor(BaseColorPropId, tint);
        if (hasColorProp) propBlock.SetColor(ColorPropId, tint);
        enemyRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyNavMeshSafeDisplacement(Vector3 delta)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.00001f) return;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Move(delta);
            return;
        }

        Vector3 targetPos = transform.position + delta;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 1.25f, NavMesh.AllAreas))
        {
            transform.position = new Vector3(navHit.position.x, transform.position.y, navHit.position.z);
        }
    }

    private void ShowBossAoEWarningRing(Vector3 worldPos, float radius)
    {
        if (bossAoEWarningRingObj == null)
        {
            bossAoEWarningRingObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bossAoEWarningRingObj.name = "BossAoEWarningRing";
            if (bossAoEWarningRingObj.TryGetComponent<Collider>(out var col)) Destroy(col);
            if (bossAoEWarningRingObj.TryGetComponent<Renderer>(out var rend))
            {
                rend.sharedMaterial = GetSharedBossWarningRingMaterial();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        bossAoEWarningRingObj.transform.position = worldPos;
        bossAoEWarningRingObj.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        bossAoEWarningRingObj.SetActive(true);
    }

    public void HideBossAoEWarningRing()
    {
        if (bossAoEWarningRingObj != null)
        {
            bossAoEWarningRingObj.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (bossAoEWarningRingObj != null)
        {
            Destroy(bossAoEWarningRingObj);
            bossAoEWarningRingObj = null;
        }
    }

    private void EnsureDefaultEventLists()
    {
        if (openHitboxEventNames == null || openHitboxEventNames.Count == 0)
        {
            openHitboxEventNames = new List<string> { "hite" };
        }
        if (closeHitboxEventNames == null || closeHitboxEventNames.Count == 0)
        {
            closeHitboxEventNames = new List<string> { "end" };
        }
    }

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 16f;
            agent.stoppingDistance = Mathf.Max(minBodySeparationDistance, attackRange * 0.75f);
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        if (GameProgressionManager.Instance != null)
        {
            ApplyStageRankScaling(GameProgressionManager.Instance.SelectedStageIndex);
        }
        EnsureAgentOnNavMesh();
        FindPlayerTarget();
        IgnorePhysicalPushAgainstPlayer();

        if (isEliteOrBoss && BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowBossBar(enemyDisplayName, currentHealth, maxHealth, currentStagger, maxStagger);
        }

        if (isEliteOrBoss)
        {
            AudioManager.EnterBossMusic();
        }

        nextTauntCheckTime = Time.time + Random.Range(minTauntInterval, maxTauntInterval);
        TransitionTo(CreateInitialState());
    }

    /// <summary>
    /// Prevents any moving collider on the Enemy from physically pushing the Player's CharacterController downward through the floor!
    /// </summary>
    private void IgnorePhysicalPushAgainstPlayer()
    {
        if (playerCharacterController == null && playerTransform != null)
        {
            playerCharacterController = playerTransform.GetComponent<CharacterController>();
        }

        if (playerCharacterController == null || bodyColliders == null) return;

        foreach (var col in bodyColliders)
        {
            // Keep trigger hitboxes able to detect hits, but prevent solid colliders on child bones from physically ramming the player downward
            if (col != null && col.gameObject != gameObject && !col.isTrigger)
            {
                Physics.IgnoreCollision(playerCharacterController, col, true);
            }
        }
    }

    
    public bool EnableAttackTelegraph => enableAttackTelegraph;
    public float AttackTelegraphDuration => attackTelegraphDuration;

    public void PlayOptionalSfx(AudioClip clip, float volumeScale = 0.85f, string fallbackLibraryKey = null)
    {
        if (clip != null)
        {
            if (audioSource != null)
            {
                float sfxGain = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.SfxVolume : 1f;
                audioSource.PlayOneShot(clip, volumeScale * sfxGain);
            }
            else
            {
                AudioManager.PlayClip(clip, transform.position, volumeScale);
            }
            return;
        }

        if (!string.IsNullOrEmpty(fallbackLibraryKey))
        {
            AudioManager.PlaySFX(fallbackLibraryKey, transform.position, volumeScale);
        }
    }

    public void ApplyStageRankScaling(int stageIndex)
    {
        if (!enableStageRankScaling || stageIndex <= 0) return;

        float hpScale = 1f + (stageIndex * Mathf.Max(0f, hpMultiplierPerRank));
        float atkScale = 1f + (stageIndex * Mathf.Max(0f, damageMultiplierPerRank));
        float rewardScale = 1f + (stageIndex * Mathf.Max(0f, rewardMultiplierPerRank));

        maxHealth = Mathf.Round(maxHealth * hpScale);
        currentHealth = maxHealth;
        attackDamage = Mathf.Round(attackDamage * atkScale);
        starDustReward = Mathf.RoundToInt(starDustReward * rewardScale);

        if (isEliteOrBoss)
        {
            maxStagger = Mathf.Round(maxStagger * (1f + stageIndex * 0.20f));
            currentStagger = maxStagger;
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.ShowBossBar(enemyDisplayName, currentHealth, maxHealth, currentStagger, maxStagger);
            }

            AudioManager.EnterBossMusic();
        }
    }

    public void FlashTelegraphColor(bool active)
    {
        if (isDead || isStunned) return;
        ApplyTintColor(active ? attackTelegraphColor : defaultColor);
    }


    public bool IsRangedEnemy => isRangedEnemy;
    public float RangedAimTelegraphDuration => rangedAimTelegraphDuration;
    public float RangedPreferredDistance => rangedPreferredDistance;
    public bool EnableBossThreeAttackPatterns => isEliteOrBoss && enableBossThreeAttackPatterns;

    public void ConfigureAsRangedMinion(float range = 10.5f)
    {
        isRangedEnemy = true;
        attackRange = Mathf.Max(attackRange, range);
        rangedPreferredDistance = Mathf.Max(5.5f, range * 0.78f);
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.stoppingDistance = rangedPreferredDistance;
        }
    }

    public void SetRangedAimLineVisible(bool visible)
    {
        if (!visible)
        {
            if (rangedAimLine != null) rangedAimLine.enabled = false;
            return;
        }

        if (rangedAimLine == null)
        {
            rangedAimLine = gameObject.GetComponent<LineRenderer>();
            if (rangedAimLine == null)
                rangedAimLine = gameObject.AddComponent<LineRenderer>();

            rangedAimLine.positionCount = 2;
            rangedAimLine.startWidth = 0.08f;
            rangedAimLine.endWidth = 0.04f;
            rangedAimLine.sharedMaterial = GetSharedAimLineMaterial();
            rangedAimLine.startColor = new Color(1f, 0.15f, 0.25f, 0.9f);
            rangedAimLine.endColor = new Color(1f, 0.15f, 0.25f, 0.25f);
        }

        Vector3 startPos = (enemyFirePoint != null) ? enemyFirePoint.position : transform.position + Vector3.up * 1.15f;
        Vector3 endPos = (playerTransform != null)
            ? playerTransform.position + Vector3.up * 1.0f
            : startPos + transform.forward * attackRange;

        rangedAimLine.enabled = true;
        rangedAimLine.SetPosition(0, startPos);
        rangedAimLine.SetPosition(1, endPos);
    }

    public IEnumerator ExecuteNextBossSpecialPatternRoutine()
    {
        int pattern = bossPatternCycleIndex % 3;
        bossPatternCycleIndex++;

        if (pattern == 1)
        {
            // Pattern 2: Long-Range Dash Slam (พุ่งกระแทกเข้าหาผู้เล่น)
            FlashTelegraphColor(true);
            yield return new WaitForSeconds(0.45f);
            FlashTelegraphColor(false);

            float dashTime = 0.42f;
            float elapsed = 0f;
            Vector3 dashDir = (playerTransform != null) ? (playerTransform.position - transform.position) : transform.forward;
            dashDir.y = 0f;
            dashDir = dashDir.sqrMagnitude > 0.01f ? dashDir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(dashDir);

            ExecuteOpenHitbox(0);
            while (elapsed < dashTime && !isDead && !isStunned)
            {
                elapsed += Time.deltaTime;
                Vector3 step = dashDir * (bossDashSpeed * Time.deltaTime);
                Vector3 nextPos = transform.position + step;
                if (playerTransform == null || Vector3.Distance(nextPos, playerTransform.position) > minBodySeparationDistance)
                {
                    ApplyNavMeshSafeDisplacement(step);
                }
                yield return null;
            }
            ExecuteCloseHitbox();
        }
        else if (pattern == 2)
        {
            // Pattern 3: Ground Red Ring Warning + Radial AoE Explosion (reusable tracked ring, auto-cleaned on stun/death)
            Vector3 ringPos = VFXAnchorRig.ResolvePosition(transform, VFXSpawnAnchor.GroundFeet);
            ShowBossAoEWarningRing(ringPos, bossAoERadius);

            FlashTelegraphColor(true);
            float warnTime = Mathf.Max(0.5f, bossAoEWarningDuration / (hasEnteredBossPhase2 ? bossPhase2SpeedMultiplier : 1f));
            yield return new WaitForSeconds(warnTime);
            FlashTelegraphColor(false);
            HideBossAoEWarningRing();

            if (!isDead && !isStunned)
            {
                UniversalVFXManager.SpawnOnOwner(VFXEventType.GroundShockwave, transform, VFXSpawnAnchor.GroundFeet, null, new Color(1f, 0.15f, 0.25f, 1f), 1.6f);
                if (playerTransform != null && playerHealth != null && !playerHealth.IsDead && GetHorizontalDistanceToPlayer() <= bossAoERadius)
                {
                    hasDealtDamageThisSwing = false;
                    DealDamageToPlayerOnce(playerHealth, playerTransform.position + Vector3.up * 1.0f);
                }
            }
        }
        else
        {
            // Pattern 1: Standard 3-Hit Combo Telegraph + Attack Animation
            if (enableAttackTelegraph && attackTelegraphDuration > 0.02f)
            {
                FlashTelegraphColor(true);
                yield return new WaitForSeconds(attackTelegraphDuration);
                FlashTelegraphColor(false);
            }
            if (!isDead && !isStunned && animator != null)
            {
                animator.ResetTrigger(AnimHurtHash);
                animator.SetTrigger(AnimAttackHash);
            }
            yield return new WaitForSeconds(Mathf.Max(0.75f, attackCooldown * 0.80f));
        }
    }

    public void ConfigureAsEliteOrBoss(bool eliteStatus, string customName = "")
    {
        isEliteOrBoss = eliteStatus;
        if (!string.IsNullOrEmpty(customName))
            enemyDisplayName = customName;

        if (isEliteOrBoss)
        {
            if (maxStagger <= 0f) maxStagger = 100f;
            currentStagger = maxStagger;
            if (shatteredCoreDropChance < 50f) shatteredCoreDropChance = 60f;

            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.ShowBossBar(enemyDisplayName, currentHealth, maxHealth, currentStagger, maxStagger);
            }

            AudioManager.EnterBossMusic();
        }
    }

    private void EnsureAgentOnNavMesh()
    {
        if (agent == null) return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    private void Update()
    {
        if (isDead) return;
        if (Time.timeScale <= 0.01f) return;

        if (animator != null && animator.applyRootMotion)
            animator.applyRootMotion = false;

        if (GlobalFreezeAI || (VNManager.Instance != null && VNManager.Instance.IsPlayingVN))
        {
            StopMovement();
            if (animator != null) animator.SetFloat(AnimSpeedHash, 0f);
            return;
        }

        if (isStunned)
        {
            StopMovement();
            if (animator != null) animator.SetFloat(AnimSpeedHash, 0f);

            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                RecoverFromStun();
            }
            return;
        }

        if (playerTransform == null)
        {
            FindPlayerTarget();
            IgnorePhysicalPushAgainstPlayer();
            if (playerTransform == null)
            {
                if (animator != null) animator.SetFloat(AnimSpeedHash, 0f);
                return;
            }
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            ExecuteCloseHitbox();
            HideBossAoEWarningRing();
            StopMovement();
            SetInCombat(false);
            if (animator != null) animator.SetFloat(AnimSpeedHash, 0f);
            return;
        }

        if (isHitboxActive)
        {
            ScanActiveHitboxesAndAttackArc();

            hitWindowTimer -= Time.deltaTime;
            if (hitWindowTimer <= 0f)
            {
                ExecuteCloseHitbox();
            }
        }

        UpdateAnimatorSpeed();
        HandleRandomTaunt();

        if (!isTaunting && !isHurt)
        {
            currentState?.Tick();
        }
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null) return;

        float currentSpeed = 0f;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            currentSpeed = agent.velocity.magnitude;

            if (currentSpeed < 0.1f && !agent.isStopped && agent.hasPath)
            {
                currentSpeed = moveSpeed * 0.5f;
            }
        }

        animator.SetFloat(AnimSpeedHash, currentSpeed);
    }

    private void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null)
        {
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) playerObj = pm.gameObject;
        }

        if (playerObj == null)
        {
            var ph = FindAnyObjectByType<PlayerHealth>();
            if (ph != null) playerObj = ph.gameObject;
        }

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerCharacterController = playerObj.GetComponent<CharacterController>();
            playerHealth = playerObj.GetComponent<PlayerHealth>()
                        ?? playerObj.GetComponentInChildren<PlayerHealth>()
                        ?? playerObj.GetComponentInParent<PlayerHealth>();
        }
    }

    public void TransitionTo(EnemyState nextState)
    {
        if (nextState == null || isDead) return;

        currentState?.Exit();
        currentState = nextState;

        bool inCombat = (currentState is ChaseState || currentState is AttackState);
        SetInCombat(inCombat);

        currentState.Enter();
    }

    public void SetInCombat(bool inCombat)
    {
        if (animator != null)
        {
            animator.SetBool(AnimInCombatHash, inCombat);
        }
    }

    private EnemyState CreateInitialState()
    {
        if (alwaysChasePlayer)
        {
            return ChaseStateInstance;
        }

        if (lostSightBehavior == LostSightBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            return PatrolStateInstance;
        }

        return IdleStateInstance;
    }

    public EnemyState CreateLostSightState()
    {
        if (alwaysChasePlayer)
        {
            return ChaseStateInstance;
        }

        if (lostSightBehavior == LostSightBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            return PatrolStateInstance;
        }
        return ReturnStateInstance;
    }

    public void MoveTo(Vector3 destination)
    {
        if (isDead || isStunned) return;

        // Never walk closer than minBodySeparationDistance to the player so enemy colliders never push the player through the floor
        if (playerTransform != null && GetHorizontalDistanceToPlayer() <= minBodySeparationDistance)
        {
            StopMovement();
            RotateTowards(playerTransform.position);
            return;
        }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
        else
        {
            Vector3 targetFlat = new Vector3(destination.x, transform.position.y, destination.z);
            Vector3 dir = targetFlat - transform.position;
            if (dir.sqrMagnitude > 0.02f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetFlat, moveSpeed * Time.deltaTime);
                RotateTowards(destination);
                if (animator != null) animator.SetFloat(AnimSpeedHash, moveSpeed);
            }
        }
    }

    public void StopMovement()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    public bool HasReachedDestination(float tolerance = 0.3f)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        if (!agent.pathPending && agent.hasPath)
        {
            return agent.remainingDistance <= agent.stoppingDistance + tolerance;
        }

        return false;
    }

    public float GetHorizontalDistanceToPlayer()
    {
        if (playerTransform == null) return Mathf.Infinity;
        Vector2 enemyXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
        return Vector2.Distance(enemyXZ, playerXZ);
    }

    public bool IsPlayerInVision()
    {
        if (alwaysChasePlayer) return true;
        if (playerTransform == null) return false;
        return GetHorizontalDistanceToPlayer() <= visionRange;
    }

    public bool IsPlayerInAttackRange()
    {
        if (playerTransform == null) return false;
        return GetHorizontalDistanceToPlayer() <= attackRange;
    }

    public void RotateTowards(Vector3 targetPosition)
    {
        if (isStunned) return;

        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleRandomTaunt()
    {
        if (!enableRandomTaunt || isDead || isAttacking || isHurt || isTaunting || isStunned) return;

        if (Time.time >= nextTauntCheckTime)
        {
            nextTauntCheckTime = Time.time + Random.Range(minTauntInterval, maxTauntInterval);

            bool isStandingStill = (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude < 0.05f)
                                   || currentState is IdleState;

            if (isStandingStill && Random.Range(0f, 100f) <= tauntChance)
            {
                StartCoroutine(TauntRoutine());
            }
        }
    }

    private IEnumerator TauntRoutine()
    {
        isTaunting = true;
        StopMovement();

        if (animator != null)
        {
            animator.SetTrigger(AnimTauntHash);
        }

        yield return new WaitForSeconds(tauntDuration);
        isTaunting = false;
    }

    /// <summary>
    /// Forces all child bone/weapon colliders to be Triggers (isTrigger = true) so swinging arms/weapons
    /// NEVER physically push the Player's CharacterController downward through the floor!
    /// </summary>
    private void SetupWeaponHitboxAndPreventSolidBonePush()
    {
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null && col.gameObject != gameObject)
            {
                if (col is MeshCollider meshCol && !meshCol.convex)
                {
                    meshCol.convex = true;
                }
                col.isTrigger = true;
            }
        }

        if (hitboxes == null || hitboxes.Length == 0)
        {
            var found = new List<Collider>();
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                if (col != null && col.gameObject != gameObject)
                {
                    if (!found.Contains(col)) found.Add(col);
                }
            }
            if (found.Count > 0) hitboxes = found.ToArray();
        }

        if (hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].gameObject != gameObject)
                {
                    if (hitboxes[i] is MeshCollider mc && !mc.convex)
                        mc.convex = true;

                    hitboxes[i].isTrigger = true;
                    hitboxes[i].enabled = false;

                    var forwarder = hitboxes[i].GetComponent<HitboxTriggerForwarder>();
                    if (forwarder == null)
                        forwarder = hitboxes[i].gameObject.AddComponent<HitboxTriggerForwarder>();
                    forwarder.Initialize(this);
                }
            }
        }
    }

    private void SetupAnimationEventForwarder()
    {
        Animator[] allAnimators = GetComponentsInChildren<Animator>(true);
        foreach (var anim in allAnimators)
        {
            if (anim != null && anim.gameObject != gameObject)
            {
                var forwarder = anim.GetComponent<EnemyAnimationEvents>();
                if (forwarder == null)
                {
                    forwarder = anim.gameObject.AddComponent<EnemyAnimationEvents>();
                }
                forwarder.Initialize(this);
            }
        }
    }

    /// <summary>
    /// Automatically binds any custom event name added to openHitboxEventNames or closeHitboxEventNames in the Inspector!
    /// </summary>
    private void BindCustomAnimationClipEvents()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null) return;

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            AnimationEvent[] events = clip.events;
            if (events == null || events.Length == 0) continue;

            bool modified = false;
            for (int i = 0; i < events.Length; i++)
            {
                string fn = events[i].functionName;
                if (string.IsNullOrEmpty(fn)) continue;

                // If it's a custom event name in our Lists (other than the built-in hite / end), route it through OnCustomEnemyAnimEvent
                if (fn != "hite" && fn != "end" && (IsOpenEventName(fn) || IsCloseEventName(fn)))
                {
                    events[i].stringParameter = fn;
                    events[i].functionName = nameof(OnCustomEnemyAnimEvent);
                    modified = true;
                }
            }

            if (modified)
            {
                clip.events = events;
            }
        }
    }

    // =========================================================================
    // LIST-DRIVEN ANIMATION EVENT SYSTEM (DEFAULT: hite / end)
    // =========================================================================

    public bool IsOpenEventName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || openHitboxEventNames == null) return false;
        for (int i = 0; i < openHitboxEventNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(openHitboxEventNames[i]) &&
                string.Equals(openHitboxEventNames[i].Trim(), eventName.Trim(), System.StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsCloseEventName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || closeHitboxEventNames == null) return false;
        for (int i = 0; i < closeHitboxEventNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(closeHitboxEventNames[i]) &&
                string.Equals(closeHitboxEventNames[i].Trim(), eventName.Trim(), System.StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public void OnCustomEnemyAnimEvent(string eventName)
    {
        if (IsOpenEventName(eventName))
        {
            ExecuteOpenHitbox(0);
        }
        else if (IsCloseEventName(eventName))
        {
            ExecuteCloseHitbox();
        }
    }

    /// <summary>
    /// Default Enemy Open Hitbox & Deal Damage Event ("hite")
    /// Only executes if "hite" is present in openHitboxEventNames List!
    /// </summary>
    public void hite(int hitIndex = 0)
    {
        if (!IsOpenEventName("hite")) return;
        ExecuteOpenHitbox(hitIndex);
    }

    /// <summary>
    /// Default Enemy Close Hitbox Event ("end")
    /// Only executes if "end" is present in closeHitboxEventNames List!
    /// </summary>
    public void end()
    {
        if (!IsCloseEventName("end")) return;
        ExecuteCloseHitbox();
    }

    // Root-Animator VFX Animation Event Forwarders (for enemies whose Animator sits on the same GameObject as EnemyController)
    public void PlayVFX(string key) => animVfxController?.PlayByKey(key);
    public void PlayVFXPrefab(UnityEngine.Object prefabObj)
    {
        if (prefabObj is GameObject prefabGo)
            animVfxController?.PlayDirectPrefab(prefabGo);
    }
    public void PlayVFXEvent(AnimationEvent evt) => animVfxController?.PlayFromAnimationEvent(evt);
    public void StopVFX(string key) => animVfxController?.StopByKey(key);
    public void StopAllVFX() => animVfxController?.CancelInterruptibleVFX();
    public void PlaySFX(string key) => AudioManager.PlaySFX(key, transform.position);
    public void PlayFootstep() => AudioManager.PlaySFX(AudioKeys.Combat_Footstep, transform.position);

    
    /// <summary>
    /// สำหรับมอนสเตอร์สายตีไกล (isRangedEnemy = true): ยิงกระสุนเวทพร้อมเรียก Muzzle VFX และ Impact VFX ใส่ผู้เล่น
    /// </summary>
    public void FireEnemyRangedProjectileWithVFX()
    {
        if (isDead || isStunned) return;

        if (playerTransform == null) FindPlayerTarget();

        Vector3 spawnPos = (enemyFirePoint != null)
            ? enemyFirePoint.position
            : VFXAnchorRig.ResolvePosition(transform, VFXSpawnAnchor.Muzzle);

        Vector3 aimDir = (playerTransform != null)
            ? ((playerTransform.position + Vector3.up * 1.0f) - spawnPos).normalized
            : transform.forward;
        if (aimDir.sqrMagnitude < 0.001f) aimDir = transform.forward;

        Quaternion shotRot = Quaternion.LookRotation(aimDir);

        if (enemyRangedPattern != null && enemyRangedPattern.IsAllowedFor(CharacterCombatArchetype.RangedCaster, true))
        {
            UniversalVFXManager.ExecuteSkillPattern(enemyRangedPattern, spawnPos, shotRot, gameObject, playerTransform, attackDamage, true, 0);
            return;
        }

        // Pooled Zero-Leak Spawner via UniversalVFXManager (fixes GPU Material leak from rend.material.color)
        UniversalVFXManager.SpawnProjectile(
            spawnPos,
            shotRot,
            gameObject,
            playerTransform,
            attackDamage,
            0f,
            0f,
            enemyProjectileSpeed,
            65f,
            enemyProjectilePrefab,
            enemyMuzzleVFXPrefab,
            enemyImpactVFXPrefab,
            null,
            enemyProjectileVfxColor,
            false,
            true);
    }

    public void ExecuteOpenHitbox(int hitIndex = 0)
    {
        if (isDead || isStunned) return;

        if (isRangedEnemy)
        {
            hasDealtDamageThisSwing = true;
            PlayOptionalSfx(attackWindupSfx, 0.9f, AudioKeys.Combat_EnemyAttack);
            FireEnemyRangedProjectileWithVFX();
            return;
        }

        PlayOptionalSfx(attackWindupSfx, 0.75f, AudioKeys.Combat_EnemyAttack);

        hitPlayers.Clear();
        hasDealtDamageThisSwing = false;
        isHitboxActive = true;
        hitWindowTimer = Mathf.Max(0.15f, activeHitWindowDuration);

        EnableHitboxByIndex(hitIndex);
        ScanActiveHitboxesAndAttackArc();
    }

    public void ExecuteCloseHitbox()
    {
        DisableAllHitboxes();
        isHitboxActive = false;
        hitWindowTimer = 0f;
    }

    private void EnableHitboxByIndex(int index)
    {
        if (hitboxes == null || hitboxes.Length == 0) return;

        if (index <= 0 || index > hitboxes.Length)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].gameObject != gameObject)
                {
                    hitboxes[i].isTrigger = true;
                    hitboxes[i].enabled = true;
                }
            }
            return;
        }

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null && hitboxes[i].gameObject != gameObject)
            {
                hitboxes[i].isTrigger = true;
                hitboxes[i].enabled = (i == index - 1);
            }
        }
    }

    private void DisableAllHitboxes()
    {
        if (hitboxes == null) return;
        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null && hitboxes[i].gameObject != gameObject)
                hitboxes[i].enabled = false;
        }
    }

    private void ScanActiveHitboxesAndAttackArc()
    {
        if (!isHitboxActive || hasDealtDamageThisSwing || isDead || isStunned) return;

        if (hitboxes != null && hitboxes.Length > 0)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                Collider hb = hitboxes[i];
                if (hb != null && hb.enabled && hb.gameObject != gameObject)
                {
                    float radius = Mathf.Max(hb.bounds.extents.x, hb.bounds.extents.y, hb.bounds.extents.z, 0.45f);
                    int count = Physics.OverlapSphereNonAlloc(hb.bounds.center, radius, overlapBuffer);
                    for (int c = 0; c < count; c++)
                    {
                        if (TryApplyDamageToCollider(overlapBuffer[c]))
                            return;
                    }
                }
            }
        }

        if (playerTransform == null || playerHealth == null)
        {
            FindPlayerTarget();
        }

        if (playerTransform != null && playerHealth != null && !playerHealth.IsDead)
        {
            float horizDist = GetHorizontalDistanceToPlayer();
            if (horizDist <= attackRange + attackHitReachBonus)
            {
                Vector3 flatToPlayer = playerTransform.position - transform.position;
                flatToPlayer.y = 0f;
                float angle = flatToPlayer.sqrMagnitude > 0.001f
                    ? Vector3.Angle(transform.forward, flatToPlayer.normalized)
                    : 0f;

                if (angle <= 120f)
                {
                    DealDamageToPlayerOnce(playerHealth, playerTransform.position + Vector3.up * 1.0f);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    public void ProcessHit(Collider other)
    {
        if (!isHitboxActive || hasDealtDamageThisSwing || isStunned || isDead || other == null) return;
        TryApplyDamageToCollider(other);
    }

    private bool TryApplyDamageToCollider(Collider other)
    {
        if (other == null || other.transform.root == transform.root) return false;

        PlayerHealth hp = other.GetComponentInParent<PlayerHealth>()
                       ?? other.GetComponent<PlayerHealth>()
                       ?? (other.CompareTag("Player") ? playerHealth : null);

        if (hp != null && !hp.IsDead)
        {
            DealDamageToPlayerOnce(hp, other.ClosestPoint(transform.position));
            return true;
        }

        return false;
    }

    private void DealDamageToPlayerOnce(PlayerHealth targetPlayerHealth, Vector3 hitPoint)
    {
        if (hasDealtDamageThisSwing || targetPlayerHealth == null || targetPlayerHealth.IsDead) return;

        hasDealtDamageThisSwing = true;
        PlayOptionalSfx(attackHitPlayerSfx, 0.9f);
        targetPlayerHealth.TakeDamage(attackDamage, hitPoint);
    }

    public void SetAttackingState(bool attacking)
    {
        isAttacking = attacking;
        if (!attacking)
        {
            ExecuteCloseHitbox();
        }
    }

    // =========================================================================
    // PHASE 3: DAMAGE, STAGGER (ELITE/BOSS), STUN & EXECUTION
    // =========================================================================

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        float legacyStaggerBonus = NextAttackStaggerBonusMultiplier;
        NextAttackStaggerBonusMultiplier = 1.0f;
        TakeDamage(amount, hitPoint, legacyStaggerBonus);
    }

    public void TakeDamage(float amount, Vector3 hitPoint, float staggerBonusMultiplier)
    {
        if (isDead || amount <= 0f) return;

        currentHealth -= amount;

        if (BattleHUD.Instance != null)
        {
            BattleHUD.Instance.SpawnFloatingDamageNumber(amount, transform.position + Vector3.up * 1.7f, amount >= 40f);
        }

        if (enableKnockbackOnHit && !isEliteOrBoss && playerTransform != null && currentHealth > 0f)
        {
            Vector3 pushDir = (transform.position - playerTransform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f)
            {
                ApplyNavMeshSafeDisplacement(pushDir.normalized * knockbackDistance);
            }
        }

        if (isEliteOrBoss && enableBossPhase2Enrage && !hasEnteredBossPhase2 && currentHealth > 0f && currentHealth <= maxHealth * 0.5f)
        {
            EnterBossPhase2Enrage();
        }

        if (currentHealth <= 0f)
        {
            if (isEliteOrBoss && BattleHUD.Instance != null)
            {
                BattleHUD.Instance.ShowBossBar(enemyDisplayName, 0f, maxHealth, currentStagger, maxStagger);
            }
            Die();
            return;
        }

        if (isEliteOrBoss)
        {
            if (!isStunned)
            {
                float staggerReduction = amount * staggerDamageMultiplier * Mathf.Max(1.0f, staggerBonusMultiplier);
                currentStagger = Mathf.Max(0f, currentStagger - staggerReduction);

                if (currentStagger <= 0f)
                {
                    TriggerStun();
                }
            }

            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.ShowBossBar(enemyDisplayName, Mathf.Max(0f, currentHealth), maxHealth, currentStagger, maxStagger);
            }
        }

        if (isStunned)
        {
            return;
        }

        isTaunting = false;

        if (!isAttacking && !(currentState is AttackState))
        {
            if (animator != null)
            {
                animator.SetTrigger(AnimHurtHash);
            }
            if (currentState != ChaseStateInstance)
            {
                TransitionTo(ChaseStateInstance);
            }
        }

        PlayOptionalSfx(hurtSfx, 0.75f, AudioKeys.Combat_EnemyHurt);
        if (hurtFlashCoroutine != null)
            StopCoroutine(hurtFlashCoroutine);
        hurtFlashCoroutine = StartCoroutine(HurtFlashOnlyRoutine());
    }

    
    private void EnterBossPhase2Enrage()
    {
        hasEnteredBossPhase2 = true;
        moveSpeed *= bossPhase2SpeedMultiplier;
        attackCooldown = Mathf.Max(0.75f, attackCooldown / bossPhase2SpeedMultiplier);
        if (agent != null && agent.isActiveAndEnabled)
            agent.speed = moveSpeed;
        if (animator != null)
            animator.speed = bossPhase2SpeedMultiplier;

        UniversalVFXManager.SpawnOnOwner(VFXEventType.StatusBurst, transform, VFXSpawnAnchor.CenterBody, null, new Color(1f, 0.15f, 0.25f, 1f), 1.4f);
        AudioManager.PlayUI(AudioKeys.Combat_BossEnrage);

        if (BattleHUD.Instance != null)
        {
            BattleHUD.Instance.SpawnFloatingFeedback("PHASE 2", "BOSS ENRAGE", 20, new Color(1f, 0.25f, 0.25f), transform.position + Vector3.up * 2.2f, "% SPD!");
        }
    }

    public void TriggerStun()
    {
        if (isDead) return;

        isEliteOrBoss = true;
        currentStagger = 0f;
        isStunned = true;
        PlayOptionalSfx(stunBreakSfx, 1.0f, AudioKeys.Combat_BossStun);
        isAttacking = false;
        isTaunting = false;
        stunTimer = stunDuration;

        SetRangedAimLineVisible(false);
        HideBossAoEWarningRing();
        ExecuteCloseHitbox();
        animVfxController?.CancelInterruptibleVFX();
        StopMovement();

        if (animator != null)
        {
            animator.SetFloat(AnimSpeedHash, 0f);
            animator.SetTrigger(AnimHurtHash);
        }

        ApplyTintColor(StunTintColor);

        if (BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowBossBar(enemyDisplayName, Mathf.Max(1f, currentHealth), maxHealth, 0f, maxStagger);
        }
    }

    private void RecoverFromStun()
    {
        if (isDead) return;

        isStunned = false;
        currentStagger = maxStagger;

        ApplyTintColor(defaultColor);

        if (BattleHUD.Instance != null && isEliteOrBoss)
        {
            BattleHUD.Instance.UpdateBossStagger(currentStagger, maxStagger);
        }

        TransitionTo(ChaseStateInstance);
    }

    private IEnumerator HurtFlashOnlyRoutine()
    {
        ApplyTintColor(Color.red);

        yield return HurtFlashWait;

        if (!isDead)
        {
            ApplyTintColor(isStunned ? StunTintColor : defaultColor);
        }
        hurtFlashCoroutine = null;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        isStunned = false;
        isAttacking = false;
        isTaunting = false;
        isHurt = false;

        StopAllCoroutines();
        hurtFlashCoroutine = null;
        currentState?.Exit();
        currentState = null;
        SetRangedAimLineVisible(false);
        HideBossAoEWarningRing();
        ExecuteCloseHitbox();
        animVfxController?.StopAllActiveVFX();

        if (GameProgressionManager.Instance != null)
        {
            GameProgressionManager.Instance.RegisterEnemyKill(isEliteOrBoss);
        }

        if (isEliteOrBoss && BattleHUD.Instance != null)
        {
            BattleHUD.Instance.HideBossBar();
        }

        if (animator != null)
        {
            animator.speed = 0f;
            animator.enabled = false;
        }

        if (agent != null)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;
        }

        if (bodyColliders != null)
        {
            for (int i = 0; i < bodyColliders.Length; i++)
            {
                if (bodyColliders[i] != null) bodyColliders[i].enabled = false;
            }
        }

        ApplyTintColor(defaultColor);

        Vector3 dropOrigin = transform.position + Vector3.up * 0.9f;
        if (starDustReward > 0)
        {
            RewardDropParticle.Spawn(dropOrigin, RewardDropType.StarDust, starDustReward, dropItemPrefab);
        }

        if (shatteredCoreDropAmount > 0 && Random.Range(0f, 100f) <= shatteredCoreDropChance)
        {
            RewardDropParticle.Spawn(dropOrigin + new Vector3(0.35f, 0.2f, 0f), RewardDropType.ShatteredCore, shatteredCoreDropAmount, dropItemPrefab);
        }

        if (destroyDelayOnDeath <= 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, destroyDelayOnDeath);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน EnemyController.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake() & EnsureDefaultEventLists()
 *    - เตรียมค่าเริ่มต้นของมอนสเตอร์ ดึงคอมโพเนนต์ NavMeshAgent, Animator, Renderer
 *    - ปิด applyRootMotion ของ Animator เพื่อไม่ให้แอนิเมชันดึงตัวมอนสเตอร์จมพื้น
 *    - ตั้งค่ารายการชื่อ Animation Event เริ่มต้นใน List:
 *      • openHitboxEventNames = ["hite"] (สำหรับเปิด Hitbox และทำดาเมจ)
 *      • closeHitboxEventNames = ["end"] (สำหรับปิด Hitbox)
 *
 * 2. Start() & IgnorePhysicalPushAgainstPlayer()
 *    - ตั้งค่าระยะหยุดของ NavMeshAgent (stoppingDistance) ไม่ให้เดินชนซ้อนทับตัวผู้เล่น
 *    - สั่ง Physics.IgnoreCollision ระหว่าง CharacterController ของผู้เล่น กับ Collider ลูกบนกระดูกแขน/อาวุธของมอนสเตอร์
 *      เพื่อป้องกันไม่ให้แขนของมอนสเตอร์ตบผู้เล่นจมทะลุพื้นแมพ (แก้ปัญหาตกแมพ)
 *
 * 3. SetupWeaponHitboxAndPreventSolidBonePush()
 *    - บังคับให้ Collider ลูกทุกชิ้นบนตัวมอนสเตอร์ (เช่น มือ, แขน, อาวุธ) เป็น Trigger (isTrigger = true)
 *    - ผูก HitboxTriggerForwarder เข้ากับก้อน Hitbox เพื่อส่งข้อมูลการชนกลับมาหา EnemyController
 *
 * 4. BindCustomAnimationClipEvents() & OnCustomEnemyAnimEvent(string eventName)
 *    - สแกนคลิปแอนิเมชันทั้งหมดใน Animator อัตโนมัติ
 *    - หากคุณกด + เพิ่มชื่อ Event ใหม่เข้าไปใน List บน Inspector (นอกเหนือจาก hite / end)
 *      ระบบจะเชื่อมชื่อ Event นั้นเข้ากับฟังก์ชันเปิด/ปิด Hitbox ให้อัตโนมัติโดยไม่ต้องเขียนโค้ดเพิ่ม
 *
 * 5. hite(int hitIndex) & ExecuteOpenHitbox(int hitIndex)
 *    - ถูกเรียกเมื่อแอนิเมชันโจมตีของมอนสเตอร์เล่นถึงเฟรมที่ปักหมุด Event "hite"
 *    - เช็กว่าชื่อ "hite" อยู่ใน openHitboxEventNames List หรือไม่ ถ้าอยู่จะเปิดใช้งานก้อน Hitbox
 *      ตั้งเวลาหน้าต่างดาเมจ (activeHitWindowDuration = 0.25 วินาที) และสแกนทำดาเมจทันที
 *
 * 6. end() & ExecuteCloseHitbox()
 *    - ถูกเรียกเมื่อแอนิเมชันเล่นถึงหมุด Event "end" (หรือเมื่อหมดเวลาหน้าต่างดาเมจ 0.25 วินาที)
 *    - ปิดก้อน Hitbox ทั้งหมดทันทีเพื่อจบการโจมตีในรอบนั้น
 *
 * 7. ScanActiveHitboxesAndAttackArc() & DealDamageToPlayerOnce()
 *    - ทำงานทุกเฟรมในช่วงที่ Hitbox เปิดอยู่
 *    - ตรวจจับการโดนตัวผู้เล่นทั้งจากก้อน Hitbox (OverlapSphere) และระยะแนวราบ XZ ด้านหน้ามอนสเตอร์
 *    - ล็อกตัวแปร hasDealtDamageThisSwing = true เมื่อทำดาเมจสำเร็จ เพื่อการันตีว่าต่อย 1 หมัด เลือดลดแค่ 1 ครั้ง
 *
 * 8. MoveTo(Vector3 destination) & GetHorizontalDistanceToPlayer()
 *    - คำนวณระยะห่างแนวราบ (แกน X, Z ไม่รวมความสูง Y) เพื่อให้เช็กระยะโจมตีแม่นยำ
 *    - หยุดเดินทันทีเมื่อเข้าใกล้ผู้เล่นถึงระยะ minBodySeparationDistance (1.15m) ไม่เดินเบียดทับตัวผู้เล่น
 *
 * 9. TakeDamage(float amount, Vector3 hitPoint) & TriggerStun()
 *    - ลดเลือดมอนสเตอร์เมื่อถูกผู้เล่นโจมตี และลดเกจ Stagger สำหรับบอส/Elite
 *    - หากมอนสเตอร์กำลังออกท่าโจมตีอยู่ (isAttacking == true) จะกะพริบตัวสีแดงแต่ไม่สั่งเล่นท่า Hurt แทรก
 *      เพื่อไม่ให้แอนิเมชันโจมตีของมอนสเตอร์ถูกตัดทิ้งก่อนถึงเฟรมทำดาเมจ
 *    - หากเกจ Stagger ของบอสลดเหลือ 0 จะเข้าสู่สถานะ Stun (ตัวสีเหลืองทอง) เปิดจังหวะให้ผู้เล่นรัวคอมโบและกดสกิล [E]/[F] โจมตีซ้ำ
 *
 * 10. Die() & RewardDropParticle
 *    - เมื่อเลือดหมด จะหยุดแอนิเมชันทันที บันทึกจำนวนการสังหารเข้า GameProgressionManager
 *    - เสกลูกแก้วพลังงาน Star Dust และสุ่มดรอป Shattered Core ให้ลอยพุ่งเข้าหาตัวผู้เล่นก่อนลบออบเจกต์ทิ้ง
 *
 * 11. ApplyStageRankScaling(int stageIndex) & FlashTelegraphColor(bool active) & PlayOptionalSfx()
 *    - ปรับสเกลค่า Max HP (+28%/Rank), ดาเมจ (+18%/Rank), เกราะ Stagger (+20%/Rank) และแร่ Star Dust (+25%/Rank)
 *      ตามระดับด่านอัตโนมัติเมื่อเริ่มด่าน
 *    - กะพริบสีเตือนล่วงหน้า (Attack Telegraph สีส้มทอง 0.22 วินาที) ก่อนสั่งเล่นแอนิเมชันโจมตี เพื่อให้ผู้เล่นอ่านจังหวะกดกลิ้งหลบ (Dodge I-Frame) ได้ทัน
 *    - รองรับการเล่นเสียงเอฟเฟกต์ (AudioSource/AudioClip) ตอนง้างตี ตอนฟันโดนผู้เล่น ตอนโดนตี และตอนเกราะแตกติด Stun
 *
 * 12. isRangedEnemy & FireEnemyRangedProjectileWithVFX() (ระบบมอนสเตอร์ตีไกล + เรียก VFX)
 *    - หากติ๊กเปิด isRangedEnemy = true บน Inspector ของมอนสเตอร์ (และตั้ง attackRange เช่น 8-12 เมตร)
 *      เมื่อแอนิเมชันโจมตีเล่นถึงหมุด Event "hite" มอนสเตอร์จะยิงกระสุนเวทพร้อมเสก Muzzle VFX และ Impact VFX ใส่ผู้เล่นทันที
 *
 * 13. EnterBossPhase2Enrage() (GDD Boss Phase 2 < 50% HP)
 *    - เมื่อบอสเลือดลดลงต่ำกว่า 50% จะเข้าสู่ Phase 2 อัตโนมัติ เพิ่มความเร็วเคลื่อนที่และโจมตีขึ้น 20%
 *      พร้อมเอฟเฟกต์ระเบิดออร่าสีแดงและข้อความแจ้งเตือนบนหน้าจอ
 *
 * 14. GDD Section 4 & 7.2 (Knockback, Floating Damage, Ranged Aim Line 1.0s, Boss 3 Patterns)
 *    - แสดงตัวเลขดาเมจลอยเหนือหัวศัตรู (SpawnFloatingDamageNumber) และดันศัตรูถอยหลังเมื่อโดนโจมตี
 *    - มอนสเตอร์ตีไกลแสดงเส้นเล็งเตือนสีแดง 1.0 วินาที (SetRangedAimLineVisible) ก่อนยิงกระสุน
 *    - บอสใหญ่สลับใช้ 3 ท่าโจมตี (ExecuteNextBossSpecialPatternRoutine): 1) ฟันต่อเนื่อง 2) พุ่งกระแทก 3) ระเบิด AoE วงสีแดงเตือนที่พื้น
 * ============================================================================ */