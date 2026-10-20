using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum LostSightBehavior
{
    StandStill,  // เดินกลับไปจุดเกิดเดิมแล้วยืนรอ
    Patrol       // สลับไปเดินลาดตระเวนตามจุด Patrol Points
}

public enum RewardDropType
{
    StarDust,
    ShatteredCore
}

/// <summary>
/// Phase 3 & Phase 5: ระบบศัตรู AI ด้วย State Pattern, ระบบเกจเกราะ (Stagger) เฉพาะ Boss/Elite,
/// การติดสถานะ Stun รอรับปุ่ม [F] ทำ Execution, ระบบสุ่มดรอปของแรร์ (Shattered Core) แบบแยกอิสระ,
/// และระบบเสก Particle ลอยเข้าหาผู้เล่น (MoveTowards) เมื่อศัตรูตาย
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable, IExecutable
{
    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 9f;

    [Header("Phase 3: Enemy Dead Settings")]
    [Tooltip("เวลาหน่วงก่อน Destroy เมื่อศัตรูตาย (0 = Destroy ทันที, หรือตั้งเวลาหน่วงโดยหยุดแอนิเมชันทันทีไม่ให้เล่นต่อ)")]
    [SerializeField] private float destroyDelayOnDeath = 0f;

    [Header("Phase 3: Elite / Boss & Stagger System")]
    [Tooltip("เปิดใช้งานสำหรับศัตรูระดับสูง (Boss / Elite) เพื่อให้มีเกจ Stagger และกดปุ่ม [F] ทำ Execution ได้")]
    [SerializeField] private bool isEliteOrBoss = false;
    [SerializeField] private string enemyDisplayName = "ELITE ENTITY";
    [Tooltip("ค่าเกจเกราะสูงสุดของศัตรูระดับ Elite / Boss")]
    [SerializeField] private float maxStagger = 100f;
    [Tooltip("ตัวคูณการลดเกจ Stagger เมื่อถูกโจมตี")]
    [SerializeField] private float staggerDamageMultiplier = 1.15f;
    [Tooltip("ระยะเวลาที่ศัตรูติด Stun รอให้ผู้เล่นกดปุ่ม [F] ทำ Execution (วินาที)")]
    [SerializeField] private float stunDuration = 5.0f;

    [Header("Phase 3 & 5: Drops & RNG Rare Drop (No Inventory)")]
    [SerializeField] private int starDustReward = 15;
    [Tooltip("โอกาสสุ่มดรอปของแรร์ Shattered Core (0 - 100%) แยกลอจิกอิสระจากการทำ Execution")]
    [Range(0f, 100f)]
    [SerializeField] private float shatteredCoreDropChance = 20f;
    [SerializeField] private int shatteredCoreDropAmount = 1;
    [SerializeField] private GameObject dropItemPrefab;

    [Header("Detection & Vision (State Pattern)")]
    [Tooltip("เริ่มเกมให้เดินไล่ล่าผู้เล่นทันที")]
    [SerializeField] private bool alwaysChasePlayer = true;
    [Tooltip("ระยะการมองเห็นผู้เล่น (Vision Range)")]
    [SerializeField] private float visionRange = 25f;
    [Tooltip("ระยะเข้าโจมตี (Attack Range)")]
    [SerializeField] private float attackRange = 2.0f;
    [Tooltip("ระยะเวลาคูลดาวน์หลังจบคอมโบ (วินาที)")]
    [SerializeField] private float attackCooldown = 1.8f;
    [Tooltip("ความเสียหายต่อฮิต")]
    [SerializeField] private float attackDamage = 12f;

    [Header("Taunt Settings (สุ่มเยาะเย้ย)")]
    [SerializeField] private bool enableRandomTaunt = true;
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

    [Header("Hitbox Detection (กำหนดจำนวนได้ตามใจ)")]
    [Tooltip("กำหนดจำนวนและใส่ Collider ของ Hitbox ศัตรูได้ไม่จำกัด (เปิด IsTrigger = true)")]
    [SerializeField] private Collider[] hitboxes;
    private readonly List<Collider> hitPlayers = new List<Collider>();
    private bool isHitboxActive = false;

    // Runtime Fields
    private NavMeshAgent agent;
    private Collider[] bodyColliders;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private EnemyState currentState;
    private Vector3 spawnPosition;
    private float currentHealth;
    private float currentStagger;
    private bool isDead = false;
    private bool isAttacking = false;
    private bool isTaunting = false;
    private bool isHurt = false;
    private bool isStunned = false;
    private bool isBeingExecuted = false;
    private float stunTimer = 0f;
    private float nextTauntCheckTime = 0f;
    private Color defaultColor = Color.white;

    public static bool GlobalExecutionFreeze { get; set; } = false;

    // Properties สำหรับ State ต่างๆ ใช้งาน
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

    // --- IDamageable Implementation ---
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // --- IExecutable Implementation (Phase 3) ---
    public bool CanBeExecuted => isEliteOrBoss && isStunned && !isDead && !isBeingExecuted;
    public Transform Transform => transform;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        bodyColliders = GetComponentsInChildren<Collider>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            defaultColor = enemyRenderer.material.color;
        }

        // ตรวจสอบชื่อ Prefab อัตโนมัติหากเป็น Elite หรือ Boss
        string lowerName = gameObject.name.ToLower();
        if (!isEliteOrBoss && (lowerName.Contains("elite") || lowerName.Contains("boss") || maxHealth >= 200f))
        {
            isEliteOrBoss = true;
        }

        currentHealth = maxHealth;
        currentStagger = isEliteOrBoss ? maxStagger : 0f;
        spawnPosition = transform.position;

        SetupAnimationEventForwarder();
        SetupWeaponHitbox();
    }

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 14f;
            agent.stoppingDistance = attackRange * 0.85f;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        EnsureAgentOnNavMesh();
        FindPlayerTarget();

        if (isEliteOrBoss && BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowBossBar(enemyDisplayName, currentHealth, maxHealth, currentStagger, maxStagger);
        }

        nextTauntCheckTime = Time.time + Random.Range(minTauntInterval, maxTauntInterval);
        TransitionTo(CreateInitialState());
    }

    /// <summary>
    /// ตั้งค่าให้ศัตรูตัวนี้เป็นระดับ Elite / Boss จาก Spawner โดยตรง
    /// </summary>
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

        if (GlobalExecutionFreeze || isBeingExecuted)
        {
            StopMovement();
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }

        if (isStunned)
        {
            StopMovement();
            if (animator != null) animator.SetFloat("Speed", 0f);

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
            if (playerTransform == null)
            {
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            StopMovement();
            SetInCombat(false);
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
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

        animator.SetFloat("Speed", currentSpeed);
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
            animator.SetBool("InCombat", inCombat);
        }
    }

    private EnemyState CreateInitialState()
    {
        if (alwaysChasePlayer)
        {
            return new ChaseState(this);
        }

        if (lostSightBehavior == LostSightBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            return new PatrolState(this);
        }

        return new IdleState(this);
    }

    public EnemyState CreateLostSightState()
    {
        if (alwaysChasePlayer)
        {
            return new ChaseState(this);
        }

        if (lostSightBehavior == LostSightBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            return new PatrolState(this);
        }
        return new ReturnState(this);
    }

    public void MoveTo(Vector3 destination)
    {
        if (isDead || isStunned || isBeingExecuted) return;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
        else
        {
            Vector3 dir = (destination - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
                RotateTowards(destination);
                if (animator != null) animator.SetFloat("Speed", moveSpeed);
            }
        }
    }

    public void StopMovement()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
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

    public bool IsPlayerInVision()
    {
        if (alwaysChasePlayer) return true;
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= visionRange;
    }

    public bool IsPlayerInAttackRange()
    {
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= attackRange;
    }

    public void RotateTowards(Vector3 targetPosition)
    {
        if (isStunned || isBeingExecuted) return;

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

            if (isStandingStill)
            {
                if (Random.Range(0f, 100f) <= tauntChance)
                {
                    StartCoroutine(TauntRoutine());
                }
            }
        }
    }

    private IEnumerator TauntRoutine()
    {
        isTaunting = true;
        StopMovement();

        if (animator != null)
        {
            animator.SetTrigger("Taunt");
        }

        yield return new WaitForSeconds(tauntDuration);
        isTaunting = false;
    }

    private void SetupWeaponHitbox()
    {
        if (hitboxes == null || hitboxes.Length == 0)
        {
            var found = new List<Collider>();
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                if (col != null && (col.isTrigger || col.name.ToLower().Contains("hitbox") || col.name.ToLower().Contains("attack") || col.name.ToLower().Contains("weapon")))
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

    // =========================================================================
    // ENEMY HITBOX EVENT FUNCTIONS: hite(int) & end()
    // =========================================================================

    public void hite(int hitIndex = 0)
    {
        if (isDead || isStunned || isBeingExecuted) return;

        hitPlayers.Clear();
        isHitboxActive = true;

        EnableHitboxByIndex(hitIndex);
        OnEnemyAttackHit();
    }

    public void end()
    {
        DisableAllHitboxes();
        isHitboxActive = false;
    }

    private void EnableHitboxByIndex(int index)
    {
        if (hitboxes == null || hitboxes.Length == 0) return;

        if (index <= 0 || index > hitboxes.Length)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null) hitboxes[i].enabled = true;
            }
            return;
        }

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null)
            {
                hitboxes[i].enabled = (i == index - 1);
            }
        }
    }

    private void DisableAllHitboxes()
    {
        if (hitboxes == null) return;
        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null) hitboxes[i].enabled = false;
        }
    }

    public void OnHitboxEnd() => end();
    public void EnableHitbox() => hite(0);
    public void DisableHitbox() => end();

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    public void ProcessHit(Collider other)
    {
        if (!isHitboxActive || isStunned || isDead || other == null) return;

        bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null;

        if (isPlayer && !hitPlayers.Contains(other))
        {
            hitPlayers.Add(other);

            PlayerHealth hp = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(attackDamage, other.ClosestPoint(transform.position));
            }
        }
    }

    public void OnEnemyAttackHit()
    {
        if (isDead || isStunned || isBeingExecuted) return;

        if (playerTransform != null && playerHealth != null && !playerHealth.IsDead)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange + 0.9f)
            {
                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle <= 110f)
                {
                    playerHealth.TakeDamage(attackDamage, playerTransform.position + Vector3.up * 1.0f);
                }
            }
        }
    }

    public void OnAttackHit() => OnEnemyAttackHit();
    public void DealDamage() => OnEnemyAttackHit();

    public void SetAttackingState(bool attacking)
    {
        isAttacking = attacking;
    }

    private void SetupAnimationEventForwarder()
    {
        if (animator != null && animator.gameObject != gameObject)
        {
            var forwarder = animator.GetComponent<EnemyAnimationEvents>();
            if (forwarder == null)
            {
                forwarder = animator.gameObject.AddComponent<EnemyAnimationEvents>();
            }
            forwarder.Initialize(this);
        }
    }

    // =========================================================================
    // PHASE 3: DAMAGE, STAGGER (ELITE/BOSS), STUN & EXECUTION
    // =========================================================================

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead || isBeingExecuted) return;

        currentHealth -= amount;

        // คำนวณลดเกจ Stagger เฉพาะศัตรูระดับสูง (Elite / Boss)
        if (isEliteOrBoss)
        {
            if (!isStunned)
            {
                float staggerReduction = amount * staggerDamageMultiplier;
                currentStagger = Mathf.Max(0f, currentStagger - staggerReduction);

                // เมื่อเกราะแตก (currentStagger <= 0) ให้ติด Stun ทันทีเพื่อเปิดโอกาสให้ผู้เล่นกด [F] ทำ Execution ฟื้นเลือด
                if (currentStagger <= 0f)
                {
                    if (currentHealth <= 0f)
                    {
                        // เลี้ยงเลือดไว้ 1 HP ตอนเกราะแตกพอดี เพื่อให้ผู้เล่นกด [F] ทำ Execution เอาเลือดได้
                        currentHealth = 1f;
                    }
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
            if (currentHealth <= 0f)
            {
                Die();
            }
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        isTaunting = false;

        if (!(currentState is AttackState))
        {
            TransitionTo(new ChaseState(this));
        }

        StartCoroutine(HurtFlinchRoutine());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// สั่งให้ศัตรูเกราะแตกและติดสถานะ Stun รอรับปุ่ม [F] ทำ Execution
    /// (รองรับการกดปุ่มเทส [F3] Instant Stun จาก CombatDebugTester ด้วย)
    /// </summary>
    public void TriggerStun()
    {
        if (isDead) return;

        // หากกดเรียกจาก Debug Tester บนมอนสเตอร์ทั่วไป ให้เปิดสิทธิ์ทำ Execution ชั่วคราวเพื่อทดสอบได้ทันที
        isEliteOrBoss = true;
        currentStagger = 0f;
        isStunned = true;
        isAttacking = false;
        isTaunting = false;
        stunTimer = stunDuration;

        end();
        StopMovement();

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetTrigger("Hurt");
        }

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = new Color(1f, 0.85f, 0.2f); // สีเหลืองทองแสดงสถานะ Stun เกราะแตก
        }

        if (BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowBossBar(enemyDisplayName, Mathf.Max(1f, currentHealth), maxHealth, 0f, maxStagger);
        }
    }

    private void RecoverFromStun()
    {
        if (isDead || isBeingExecuted) return;

        isStunned = false;
        currentStagger = maxStagger;

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = defaultColor;
        }

        if (BattleHUD.Instance != null && isEliteOrBoss)
        {
            BattleHUD.Instance.UpdateBossStagger(currentStagger, maxStagger);
        }

        TransitionTo(new ChaseState(this));
    }

    public void OnExecutionStart()
    {
        isBeingExecuted = true;
        isStunned = true;
        stunTimer = 99f;
        end();
        StopMovement();
    }

    public void ExecuteKill()
    {
        if (isDead) return;
        currentHealth = 0f;
        Die();
    }

    private IEnumerator HurtFlinchRoutine()
    {
        isHurt = true;
        StopMovement();

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = Color.red;
        }

        yield return new WaitForSeconds(0.25f);

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = isStunned ? new Color(1f, 0.85f, 0.2f) : defaultColor;
        }

        isHurt = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        isStunned = false;
        isBeingExecuted = false;
        isAttacking = false;
        isTaunting = false;
        isHurt = false;

        // หยุด Coroutine และ Hitbox ทั้งหมดทันที
        StopAllCoroutines();
        currentState?.Exit();
        currentState = null;
        end();

        if (isEliteOrBoss && BattleHUD.Instance != null)
        {
            BattleHUD.Instance.HideBossBar();
        }

        // Phase 3 Enemy Dead: หยุดแอนิเมชันทันที ไม่ให้มีการเล่นแอนิเมชันใดๆ ต่อระหว่างรอหรือก่อน Destroy
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
            foreach (var col in bodyColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = defaultColor;
        }

        // Phase 5: เสก Particle ออบเจกต์ให้ลอย MoveTowards พุ่งเข้าหาผู้เล่น
        Vector3 dropOrigin = transform.position + Vector3.up * 0.9f;
        if (starDustReward > 0)
        {
            RewardDropParticle.Spawn(dropOrigin, RewardDropType.StarDust, starDustReward, dropItemPrefab);
        }

        // Phase 3 RNG Rare Drop: สุ่มโอกาสดรอปของแรร์ (Shattered Core) แยกลอจิกอิสระจากการทำ Execution
        if (shatteredCoreDropAmount > 0 && Random.Range(0f, 100f) <= shatteredCoreDropChance)
        {
            RewardDropParticle.Spawn(dropOrigin + new Vector3(0.35f, 0.2f, 0f), RewardDropType.ShatteredCore, shatteredCoreDropAmount, dropItemPrefab);
        }

        // เรียก Destroy ทันที (หาก destroyDelayOnDeath <= 0) หรือหน่วงเวลาตามที่กำหนดโดยไม่มีแอนิเมชันเล่นต่อ
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

// =========================================================================
// PHASE 5: HOMING PARTICLE DROP (MoveTowards Player + Floating UI Trigger)
// =========================================================================

/// <summary>
/// Phase 5: Particle ออบเจกต์ที่เสกออกมาเมื่อศัตรูตาย ลอย MoveTowards พุ่งเข้าหาตำแหน่งผู้เล่น
/// เมื่อชนผู้เล่นจะบวกแต้มเข้าตัวแปรและเด้งข้อความ Floating UI สไตล์ "+ [ไอคอน] จำนวน"
/// </summary>
public class RewardDropParticle : MonoBehaviour
{
    private RewardDropType dropType;
    private int amount;
    private Transform targetPlayer;
    private PlayerHealth playerHealth;

    private Vector3 scatterVelocity;
    private float scatterTimer = 0.32f;
    private float currentHomingSpeed = 7f;
    private float maxHomingSpeed = 26f;
    private float acceleration = 32f;
    private bool isCollected = false;

    public static void Spawn(Vector3 position, RewardDropType type, int amount, GameObject optionalPrefab = null)
    {
        GameObject dropObj;

        if (optionalPrefab != null)
        {
            dropObj = Instantiate(optionalPrefab, position, Quaternion.identity);
        }
        else
        {
            dropObj = new GameObject($"RewardParticle_{type}");
            dropObj.transform.position = position;
            CreateProceduralParticleVisual(dropObj, type);
        }

        RewardDropParticle drop = dropObj.GetComponent<RewardDropParticle>();
        if (drop == null)
        {
            drop = dropObj.AddComponent<RewardDropParticle>();
        }

        drop.Initialize(type, amount);
    }

    private static void CreateProceduralParticleVisual(GameObject host, RewardDropType type)
    {
        Color mainColor = (type == RewardDropType.ShatteredCore)
            ? new Color(0.95f, 0.25f, 1.0f)   // สีม่วงชมพูสำหรับแรร์ไอเทม Shattered Core
            : new Color(1.0f, 0.88f, 0.25f);  // สีทองสว่างสำหรับ Star Dust

        // สร้างลูกแก้วแสงขนาดเล็ก
        GameObject visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualSphere.transform.SetParent(host.transform, false);
        visualSphere.transform.localScale = (type == RewardDropType.ShatteredCore) ? Vector3.one * 0.34f : Vector3.one * 0.24f;

        if (visualSphere.TryGetComponent<Collider>(out var col))
        {
            Destroy(col);
        }

        if (visualSphere.TryGetComponent<Renderer>(out var rend))
        {
            rend.material.color = mainColor;
        }

        // สร้าง Particle System ประกายแสงวิ่งตามลูกแก้ว
        ParticleSystem ps = host.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = mainColor;
        main.startSize = (type == RewardDropType.ShatteredCore) ? 0.22f : 0.15f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
    }

    public void Initialize(RewardDropType type, int rewardAmount)
    {
        dropType = type;
        amount = rewardAmount;

        // ปิด Collider ที่ไม่ใช่ Trigger บน Prefab เพื่อไม่ให้สะดุดขาผู้เล่น
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.isTrigger = true;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
        }
        trigger.isTrigger = true;
        trigger.radius = 0.5f;

        // สุ่มแรงดีดตัวขึ้นสั้นๆ ตอนดรอปออกจากตัวศัตรู
        Vector2 randCircle = Random.insideUnitCircle * 2.2f;
        scatterVelocity = new Vector3(randCircle.x, Random.Range(2.2f, 3.6f), randCircle.y);

        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            var ph = FindAnyObjectByType<PlayerHealth>();
            if (ph != null) playerObj = ph.gameObject;
        }

        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>() ?? playerObj.GetComponentInChildren<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (targetPlayer == null)
        {
            FindPlayer();
            if (targetPlayer == null) return;
        }

        // จังหวะที่ 1: ลอยกระจายตัวขึ้นสั้นๆ ให้เห็นชัดเจนว่าดรอปจากศัตรู
        if (scatterTimer > 0f)
        {
            scatterTimer -= Time.deltaTime;
            transform.position += scatterVelocity * Time.deltaTime;
            scatterVelocity = Vector3.Lerp(scatterVelocity, Vector3.zero, Time.deltaTime * 6f);
            return;
        }

        // จังหวะที่ 2: พุ่งเข้าหาตำแหน่งผู้เล่นด้วย Vector3.MoveTowards (Phase 5)
        currentHomingSpeed = Mathf.Min(maxHomingSpeed, currentHomingSpeed + acceleration * Time.deltaTime);
        Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;

        transform.position = Vector3.MoveTowards(transform.position, targetCenter, currentHomingSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetCenter) <= 0.55f)
        {
            Collect();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || scatterTimer > 0.1f) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (playerHealth == null && targetPlayer != null)
        {
            playerHealth = targetPlayer.GetComponent<PlayerHealth>() ?? targetPlayer.GetComponentInChildren<PlayerHealth>();
        }

        Vector3 feedbackPos = targetPlayer != null ? targetPlayer.position + Vector3.up * 1.5f : transform.position;

        if (dropType == RewardDropType.ShatteredCore)
        {
            playerHealth?.AddShatteredCore(amount);
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.SpawnFloatingFeedback("CORE", "Shattered Core", amount, new Color(0.95f, 0.35f, 1f), feedbackPos);
            }
        }
        else
        {
            playerHealth?.AddStarDust(amount);
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.SpawnFloatingFeedback("STAR DUST", "Star Dust", amount, new Color(1f, 0.88f, 0.25f), feedbackPos);
            }
        }

        Destroy(gameObject);
    }
}

// =========================================================================
// STATE PATTERN CLASSES
// =========================================================================

public abstract class EnemyState
{
    protected EnemyController enemy;

    public EnemyState(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public virtual void Enter() { }
    public abstract void Tick();
    public virtual void Exit() { }
}

public class IdleState : EnemyState
{
    public IdleState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.StopMovement();
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
        }
    }
}

public class ChaseState : EnemyState
{
    public ChaseState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        if (enemy.PlayerTransform != null)
        {
            enemy.MoveTo(enemy.PlayerTransform.position);
        }
    }

    public override void Tick()
    {
        if (enemy.PlayerTransform == null) return;

        if (enemy.IsPlayerInAttackRange())
        {
            enemy.TransitionTo(new AttackState(enemy));
            return;
        }

        if (!enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(enemy.CreateLostSightState());
            return;
        }

        enemy.MoveTo(enemy.PlayerTransform.position);
    }
}

public class AttackState : EnemyState
{
    private float nextAttackTime = 0f;
    private Coroutine attackRoutine;

    public AttackState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.StopMovement();
        ExecuteComboAttack();
    }

    public override void Tick()
    {
        if (enemy.PlayerTransform == null) return;

        enemy.RotateTowards(enemy.PlayerTransform.position);

        if (enemy.IsAttacking) return;

        if (!enemy.IsPlayerInAttackRange())
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            ExecuteComboAttack();
        }
    }

    private void ExecuteComboAttack()
    {
        nextAttackTime = Time.time + enemy.AttackCooldown;

        if (enemy.Animator != null)
        {
            enemy.Animator.SetTrigger("Attack");
        }

        attackRoutine = enemy.StartCoroutine(ComboDurationRoutine());
    }

    private IEnumerator ComboDurationRoutine()
    {
        enemy.SetAttackingState(true);
        yield return new WaitForSeconds(1.5f);
        enemy.SetAttackingState(false);
    }

    public override void Exit()
    {
        if (attackRoutine != null)
        {
            enemy.StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        enemy.SetAttackingState(false);
    }
}

public class ReturnState : EnemyState
{
    public ReturnState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.MoveTo(enemy.SpawnPosition);
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (enemy.HasReachedDestination())
        {
            enemy.TransitionTo(new IdleState(enemy));
        }
    }
}

public class PatrolState : EnemyState
{
    private int currentPointIndex = 0;

    public PatrolState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        MoveToNextPatrolPoint();
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (enemy.HasReachedDestination())
        {
            MoveToNextPatrolPoint();
        }
    }

    private void MoveToNextPatrolPoint()
    {
        if (enemy.PatrolPoints == null || enemy.PatrolPoints.Length == 0) return;

        enemy.MoveTo(enemy.PatrolPoints[currentPointIndex].position);
        currentPointIndex = (currentPointIndex + 1) % enemy.PatrolPoints.Length;
    }
}

// =========================================================================
// ANIMATION EVENT FORWARDER
// =========================================================================

public class EnemyAnimationEvents : MonoBehaviour
{
    private EnemyController enemyController;

    private void Awake()
    {
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }
    }

    public void Initialize(EnemyController controller)
    {
        enemyController = controller;
    }

    public void hite(int hitIndex = 0)
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        enemyController?.hite(hitIndex);
    }

    public void end()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        enemyController?.end();
    }

    public void OnHitboxEnd() => end();
    public void EnableHitbox() => hite(0);
    public void DisableHitbox() => end();

    public void OnEnemyAttackHit()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        enemyController?.OnEnemyAttackHit();
    }

    public void OnAttackHit() => OnEnemyAttackHit();
    public void DealDamage() => OnEnemyAttackHit();
}