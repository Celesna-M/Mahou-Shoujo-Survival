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
public class EnemyController : MonoBehaviour, IDamageable, IExecutable
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

    // Runtime Fields
    private NavMeshAgent agent;
    private Collider[] bodyColliders;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private CharacterController playerCharacterController;
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

    public bool CanBeExecuted => isEliteOrBoss && isStunned && !isDead && !isBeingExecuted;
    public Transform Transform => transform;

    private void Reset()
    {
        EnsureDefaultEventLists();
    }

    private void Awake()
    {
        EnsureDefaultEventLists();

        agent = GetComponent<NavMeshAgent>();
        bodyColliders = GetComponentsInChildren<Collider>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            defaultColor = enemyRenderer.material.color;
        }

        string lowerName = gameObject.name.ToLower();
        if (!isEliteOrBoss && (lowerName.Contains("elite") || lowerName.Contains("boss") || maxHealth >= 200f))
        {
            isEliteOrBoss = true;
        }

        currentHealth = maxHealth;
        currentStagger = isEliteOrBoss ? maxStagger : 0f;
        spawnPosition = transform.position;

        SetupAnimationEventForwarder();
        SetupWeaponHitboxAndPreventSolidBonePush();
        BindCustomAnimationClipEvents();
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

        EnsureAgentOnNavMesh();
        FindPlayerTarget();
        IgnorePhysicalPushAgainstPlayer();

        if (isEliteOrBoss && BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowBossBar(enemyDisplayName, currentHealth, maxHealth, currentStagger, maxStagger);
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

        if (animator != null && animator.applyRootMotion)
            animator.applyRootMotion = false;

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
            IgnorePhysicalPushAgainstPlayer();
            if (playerTransform == null)
            {
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            ExecuteCloseHitbox();
            StopMovement();
            SetInCombat(false);
            if (animator != null) animator.SetFloat("Speed", 0f);
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
                if (animator != null) animator.SetFloat("Speed", moveSpeed);
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
            animator.SetTrigger("Taunt");
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

    public void ExecuteOpenHitbox(int hitIndex = 0)
    {
        if (isDead || isStunned || isBeingExecuted) return;

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
        if (!isHitboxActive || hasDealtDamageThisSwing || isDead || isStunned || isBeingExecuted) return;

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
        if (isDead || isBeingExecuted) return;

        currentHealth -= amount;

        if (isEliteOrBoss)
        {
            if (!isStunned)
            {
                float staggerReduction = amount * staggerDamageMultiplier;
                currentStagger = Mathf.Max(0f, currentStagger - staggerReduction);

                if (currentStagger <= 0f)
                {
                    if (currentHealth <= 0f)
                    {
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

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        isTaunting = false;

        if (!isAttacking && !(currentState is AttackState))
        {
            if (animator != null)
            {
                animator.SetTrigger("Hurt");
            }
            TransitionTo(new ChaseState(this));
        }

        StartCoroutine(HurtFlashOnlyRoutine());
    }

    public void TriggerStun()
    {
        if (isDead) return;

        isEliteOrBoss = true;
        currentStagger = 0f;
        isStunned = true;
        isAttacking = false;
        isTaunting = false;
        stunTimer = stunDuration;

        ExecuteCloseHitbox();
        StopMovement();

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetTrigger("Hurt");
        }

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = new Color(1f, 0.85f, 0.2f);
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
        ExecuteCloseHitbox();
        StopMovement();
    }

    public void ExecuteKill()
    {
        if (isDead) return;
        currentHealth = 0f;
        Die();
    }

    private IEnumerator HurtFlashOnlyRoutine()
    {
        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = Color.red;
        }

        yield return new WaitForSeconds(0.18f);

        if (!isDead && enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = isStunned ? new Color(1f, 0.85f, 0.2f) : defaultColor;
        }
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

        StopAllCoroutines();
        currentState?.Exit();
        currentState = null;
        ExecuteCloseHitbox();

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
            foreach (var col in bodyColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            enemyRenderer.material.color = defaultColor;
        }

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

// =========================================================================
// PHASE 5: HOMING PARTICLE DROP
// =========================================================================

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
            ? new Color(0.95f, 0.25f, 1.0f)
            : new Color(1.0f, 0.88f, 0.25f);

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

        if (scatterTimer > 0f)
        {
            scatterTimer -= Time.deltaTime;
            transform.position += scatterVelocity * Time.deltaTime;
            scatterVelocity = Vector3.Lerp(scatterVelocity, Vector3.zero, Time.deltaTime * 6f);
            return;
        }

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

        if (enemy.GetHorizontalDistanceToPlayer() > enemy.AttackRange * 1.15f)
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

        if (enemy.PlayerTransform != null)
        {
            Vector3 lookDir = enemy.PlayerTransform.position - enemy.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                enemy.transform.rotation = Quaternion.LookRotation(lookDir.normalized);
            }
        }

        if (enemy.Animator != null)
        {
            enemy.Animator.ResetTrigger("Hurt");
            enemy.Animator.SetTrigger("Attack");
        }

        if (attackRoutine != null)
            enemy.StopCoroutine(attackRoutine);
        attackRoutine = enemy.StartCoroutine(ComboDurationRoutine());
    }

    private IEnumerator ComboDurationRoutine()
    {
        enemy.SetAttackingState(true);
        yield return new WaitForSeconds(Mathf.Max(0.8f, enemy.AttackCooldown * 0.85f));
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
// ENEMY ANIMATION EVENT FORWARDER (LIST-DRIVEN: DEFAULT hite / end)
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

    private EnemyController GetEnemy()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>() ?? GetComponent<EnemyController>();
        return enemyController;
    }

    // Default Enemy Events: hite / end
    public void hite(int hitIndex = 0) => GetEnemy()?.hite(hitIndex);
    public void end() => GetEnemy()?.end();

    // Custom Event Handler for any extra event names added to the Inspector List
    public void OnCustomEnemyAnimEvent(string eventName) => GetEnemy()?.OnCustomEnemyAnimEvent(eventName);
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
 *    - หากเกจ Stagger ของบอสลดเหลือ 0 จะเข้าสู่สถานะ Stun (ตัวสีเหลืองทอง) รอให้ผู้เล่นกดปุ่ม [F] สังหาร
 *
 * 10. Die() & RewardDropParticle
 *    - เมื่อเลือดหมด จะหยุดแอนิเมชันทันที บันทึกจำนวนการสังหารเข้า GameProgressionManager
 *    - เสกลูกแก้วพลังงาน Star Dust และสุ่มดรอป Shattered Core ให้ลอยพุ่งเข้าหาตัวผู้เล่นก่อนลบออบเจกต์ทิ้ง
 * ============================================================================ */