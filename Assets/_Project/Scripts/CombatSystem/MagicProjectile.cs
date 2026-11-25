using MahouShoujo.VFX;
using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// CombatSystem: ระบบกระสุน Data-Driven + Object Pool (รองรับครบ 11 Projectile Patterns + SlashWave + MeteorRain)
// - Zero-GC Shutdown: ใช้ตัวจับเวลาใน Update() คืน Pool แทน Destroy(gameObject) และไม่สร้าง Coroutine
// - รองรับ 6 พฤติกรรมตอนปะทะ: ExplodeOnHit, PierceTargets, ChainBounce, WallRicochet, StickyDelayedDetonate, SplitOnHit
[RequireComponent(typeof(SphereCollider))]
public class MagicProjectile : MonoBehaviour
{
    [Header("1. Flight & Homing Ballistics")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float acceleration = 0f;
    [SerializeField] private float lifeTime = 3.5f;
    [SerializeField] private float homingAngularSpeed = 180f;
    [SerializeField] private float collisionSphereRadius = 0.28f;

    [Header("2. Damage & Splash")]
    [SerializeField] private float directDamage = 35f;
    [SerializeField] private float splashDamage = 22f;
    [SerializeField] private float splashRadius = 3.2f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("3. VFX Prefabs")]
    [SerializeField] private GameObject muzzleCastVFXPrefab;
    [SerializeField] private GameObject flightTrailVFXPrefab;
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private GameObject splashWaveVFXPrefab;
    [SerializeField] private float trailDetachDelay = 0.45f;
    [SerializeField] private bool useWhiteBlueLightningVFX = false;
    [SerializeField] private bool autoCreateProceduralVFXIfMissing = true;
    [SerializeField] private Color projectileVfxColor = new Color(0.35f, 0.82f, 1.0f, 1.0f);

    [Header("4. Game Feel")]
    [SerializeField] private float hitStopDuration = 0.04f;
    [SerializeField] private float shakeIntensity = 0.35f;

    // Runtime Pattern State
    private CombatSkillPattern_SO activePattern;
    private CombatPatternPreset activePreset = CombatPatternPreset.AimedSnipe;
    private ProjectileImpactBehavior impactBehavior = ProjectileImpactBehavior.ExplodeOnHit;
    private int remainingPierceOrBounce = 0;
    private int currentSplitDepth = 0;
    private float currentSpeed;
    private float delayPhaseTimer;
    private float orbitAngle;
    private float orbitRadius = 2.0f;
    private float orbitSpeedDeg = 220f;
    private float waveAmp = 1.0f;
    private float waveFreq = 8.0f;
    private Vector3 initialRightAxis;
    private bool isStickyAttached;
    private Transform stickyTarget;
    private Vector3 stickyLocalOffset;
    private float stickyTimer;

    // Pool & Lifecycle State
    private Transform homingTarget;
    private GameObject shooter;
    private Transform shooterTransform;
    private bool isEnemyProjectile;
    private bool hasImpacted;
    private bool isShuttingDown;
    private float shutdownTimer;
    private float lifeTimer;
    private Action<MagicProjectile> returnToPoolCallback;
    private PooledVFXInstance dynamicTrailVfx;

    private SphereCollider cachedCollider;
    private LightningProjectileVisuals cachedVisuals;
    private readonly Collider[] splashBuffer = new Collider[24];
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        cachedVisuals = GetComponent<LightningProjectileVisuals>();
        if (TryGetComponent<SphereCollider>(out cachedCollider))
        {
            cachedCollider.isTrigger = true;
            if (cachedCollider.radius < 0.2f) cachedCollider.radius = 0.3f;
        }
    }

    public void BindPoolReturn(Action<MagicProjectile> onReturn)
    {
        returnToPoolCallback = onReturn;
    }

    // Standard Legacy Initialize (100% Backward Compatible)
    public void Initialize(
        GameObject caster,
        Transform target,
        float dmg,
        float splashDmg,
        float splashRad,
        GameObject customMuzzleVfx = null,
        GameObject customImpactVfx = null,
        GameObject customSplashVfx = null,
        Color? customThemeColor = null,
        bool firedByEnemy = false)
    {
        activePattern = null;
        activePreset = CombatPatternPreset.HomingGuided;
        impactBehavior = ProjectileImpactBehavior.ExplodeOnHit;
        remainingPierceOrBounce = 0;
        currentSplitDepth = 0;
        delayPhaseTimer = 0f;

        ResetCommonState(caster, target, dmg, splashDmg, splashRad, customMuzzleVfx, customImpactVfx, customSplashVfx, customThemeColor, firedByEnemy);
    }

    // Data-Driven Pattern Initialize (Supports all 11 Projectile Patterns + SlashWave + MeteorRain)
    public void InitializeFromPattern(
        CombatSkillPattern_SO pattern,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool firedByEnemy,
        int indexInVolley,
        int totalInVolley,
        int splitDepth = 0)
    {
        activePattern = pattern;
        activePreset = pattern.patternPreset;
        impactBehavior = pattern.impactBehavior;
        currentSplitDepth = splitDepth;

        speed = Mathf.Max(4f, pattern.speed);
        acceleration = pattern.acceleration;
        lifeTime = Mathf.Max(0.25f, pattern.lifeTime);
        homingAngularSpeed = Mathf.Max(0f, pattern.homingAngularSpeed);
        collisionSphereRadius = Mathf.Max(0.15f, pattern.collisionRadius);
        trailDetachDelay = Mathf.Max(0.15f, pattern.trailDetachDelay);
        useWhiteBlueLightningVFX = pattern.useWhiteBlueLightningVFX;
        hitStopDuration = pattern.hitStopDuration;
        shakeIntensity = pattern.shakeIntensity;
        waveAmp = pattern.waveAmplitude;
        waveFreq = pattern.waveFrequency;
        orbitRadius = Mathf.Max(0.8f, pattern.formationRadius);
        orbitAngle = (totalInVolley > 0) ? (360f * indexInVolley / totalInVolley) : 0f;
        delayPhaseTimer = (activePreset == CombatPatternPreset.DelayedHomingItano)
            ? Mathf.Max(0.15f, pattern.windupOrDelayDuration)
            : 0f;

        // Preset-specific automatic defaults
        if (activePreset == CombatPatternPreset.RicochetBounce)
            impactBehavior = ProjectileImpactBehavior.WallRicochet;
        else if (activePreset == CombatPatternPreset.SplitCluster && pattern.subPatternOnHit != null)
            impactBehavior = ProjectileImpactBehavior.SplitOnHit;
        else if (activePreset == CombatPatternPreset.SlashWaveCrescent && impactBehavior == ProjectileImpactBehavior.ExplodeOnHit)
            impactBehavior = ProjectileImpactBehavior.PierceTargets;

        remainingPierceOrBounce = Mathf.Max(1, pattern.maxPierceOrBounceCount);

        float directDmg = Mathf.Max(1f, baseAtk * pattern.directDamageMultiplier);
        float splashDmg = Mathf.Max(0f, baseAtk * pattern.splashDamageMultiplier);

        transform.localScale = Vector3.one * Mathf.Max(0.2f, pattern.visualScale);
        if (activePreset == CombatPatternPreset.SlashWaveCrescent)
        {
            transform.localScale = new Vector3(pattern.visualScale * 2.4f, pattern.visualScale * 0.65f, pattern.visualScale * 1.1f);
            collisionSphereRadius = Mathf.Max(collisionSphereRadius, 0.9f * pattern.visualScale);
        }

        if (pattern.mainBodyVFXPrefab != null) flightTrailVFXPrefab = pattern.mainBodyVFXPrefab;

        ResetCommonState(
            caster,
            target,
            directDmg,
            splashDmg,
            pattern.splashRadius,
            pattern.muzzleCastVFXPrefab,
            pattern.impactVFXPrefab,
            pattern.splashWaveVFXPrefab,
            pattern.themeColor,
            firedByEnemy);
    }

    private void ResetCommonState(
        GameObject caster,
        Transform target,
        float dmg,
        float splashDmg,
        float splashRad,
        GameObject customMuzzleVfx,
        GameObject customImpactVfx,
        GameObject customSplashVfx,
        Color? customThemeColor,
        bool firedByEnemy)
    {
        shooter = caster;
        shooterTransform = caster != null ? caster.transform : null;
        homingTarget = target;
        directDamage = dmg;
        splashDamage = splashDmg;
        splashRadius = splashRad;
        isEnemyProjectile = firedByEnemy;

        hasImpacted = false;
        isShuttingDown = false;
        shutdownTimer = 0f;
        lifeTimer = 0f;
        currentSpeed = speed;
        isStickyAttached = false;
        stickyTarget = null;
        damagedTargets.Clear();
        initialRightAxis = transform.right;

        if (cachedCollider != null) cachedCollider.enabled = true;
        RestoreVisualRenderers();

        if (customMuzzleVfx != null) muzzleCastVFXPrefab = customMuzzleVfx;
        if (customImpactVfx != null) impactVFXPrefab = customImpactVfx;
        if (customSplashVfx != null) splashWaveVFXPrefab = customSplashVfx;
        if (customThemeColor.HasValue) projectileVfxColor = customThemeColor.Value;

        ResolveLayerMasks();

        // Stage 1: Muzzle Cast VFX
        SpawnMuzzleCastVFX(transform.position, transform.rotation);

        // Stage 2: 4-Layer Flight Trail VFX (Clear old pooled Trail AFTER SetPositionAndRotation!)
        AttachFlightTrailVFX();
    }

    private void ResolveLayerMasks()
    {
        if (isEnemyProjectile)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            targetLayers = (playerLayer != -1) ? (1 << playerLayer) : ~LayerMask.GetMask("Enemy", "Ignore Raycast");
        }
        else
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            targetLayers = (enemyLayer != -1) ? (1 << enemyLayer) : ~LayerMask.GetMask("Player", "Ignore Raycast");
        }

        if (obstacleLayers.value == 0)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            int envLayer = LayerMask.NameToLayer("Environment");
            int mask = 0;
            if (defaultLayer != -1) mask |= (1 << defaultLayer);
            if (envLayer != -1) mask |= (1 << envLayer);
            obstacleLayers = mask;
        }
    }

    public void SetUseWhiteBlueLightningVFX(bool enabled) { useWhiteBlueLightningVFX = enabled; }

    public void ConfigureFlightSpeed(float projectileSpeed, float homingTurnSpeed = 180f)
    {
        speed = Mathf.Max(4f, projectileSpeed);
        currentSpeed = speed;
        homingAngularSpeed = Mathf.Max(0f, homingTurnSpeed);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Zero-GC Graceful Pool Return countdown
        if (isShuttingDown)
        {
            shutdownTimer -= dt;
            if (shutdownTimer <= 0f)
            {
                CompleteReturnToPool();
            }
            return;
        }

        if (hasImpacted) return;

        // Sticky Delayed Detonate state
        if (isStickyAttached)
        {
            if (stickyTarget != null) transform.position = stickyTarget.TransformPoint(stickyLocalOffset);
            stickyTimer -= dt;
            if (stickyTimer <= 0f)
            {
                DetonateAndShutdown(transform.position, Vector3.up, null);
            }
            return;
        }

        lifeTimer += dt;
        if (lifeTimer >= lifeTime)
        {
            DetonateAndShutdown(transform.position, -transform.forward, null);
            return;
        }

        // Delayed Homing (Itano Circus / Gate of Babylon): hover first, then launch hard at target
        if (delayPhaseTimer > 0f)
        {
            delayPhaseTimer -= dt;
            if (homingTarget != null && homingTarget.gameObject.activeInHierarchy)
            {
                Vector3 aim = (homingTarget.position + Vector3.up * 1.0f) - transform.position;
                if (aim.sqrMagnitude > 0.001f)
                    transform.forward = Vector3.RotateTowards(transform.forward, aim.normalized, 360f * Mathf.Deg2Rad * dt, 0f);
            }
            return;
        }

        // Orbit / Satellite around caster
        if (activePreset == CombatPatternPreset.OrbitSatellite && shooterTransform != null)
        {
            orbitAngle += orbitSpeedDeg * dt;
            float rad = orbitAngle * Mathf.Deg2Rad;
            Vector3 desired = shooterTransform.position + Vector3.up * 1.1f + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius;
            Vector3 moveDelta = desired - transform.position;
            if (moveDelta.sqrMagnitude > 0.0001f) transform.forward = moveDelta.normalized;
            transform.position = desired;
            CheckOrbitContact();
            return;
        }

        // Acceleration & Homing steering
        currentSpeed = Mathf.Max(2f, currentSpeed + acceleration * dt);
        bool usesHoming = activePreset == CombatPatternPreset.HomingGuided
                       || activePreset == CombatPatternPreset.DelayedHomingItano
                       || (activePattern == null && homingAngularSpeed > 0.1f);

        if (usesHoming && homingTarget != null && homingTarget.gameObject.activeInHierarchy)
        {
            if (!homingTarget.TryGetComponent<IDamageable>(out var dmg) || !dmg.IsDead)
            {
                Vector3 aimPos = homingTarget.position + Vector3.up * 1.0f;
                Vector3 dir = (aimPos - transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                {
                    float turnRate = (activePreset == CombatPatternPreset.DelayedHomingItano)
                        ? Mathf.Max(320f, homingAngularSpeed * 1.5f)
                        : homingAngularSpeed;
                    transform.forward = Vector3.RotateTowards(transform.forward, dir, turnRate * Mathf.Deg2Rad * dt, 0f);
                }
            }
        }

        Vector3 moveDir = transform.forward;
        Vector3 moveStep = moveDir * (currentSpeed * dt);

        // Wave / Sine / ZigZag lateral offset
        if (activePreset == CombatPatternPreset.WaveSineZigZag)
        {
            float lateralVel = Mathf.Cos(lifeTimer * waveFreq) * waveAmp * waveFreq * dt;
            moveStep += initialRightAxis * lateralVel;
        }

        float stepDist = moveStep.magnitude;
        if (stepDist > 0.0001f)
        {
            LayerMask mask = (targetLayers | obstacleLayers);
            if (mask.value == 0) mask = ~LayerMask.GetMask("Ignore Raycast");

            if (Physics.SphereCast(transform.position, collisionSphereRadius, moveStep / stepDist, out RaycastHit hit, stepDist, mask, QueryTriggerInteraction.Collide))
            {
                if (!ShouldIgnoreCollider(hit.collider))
                {
                    ProcessImpact(hit.point, hit.normal, hit.collider);
                    return;
                }
            }
        }

        transform.position += moveStep;
    }

    private void CheckOrbitContact()
    {
        int cnt = Physics.OverlapSphereNonAlloc(transform.position, collisionSphereRadius + 0.25f, splashBuffer, targetLayers);
        for (int i = 0; i < cnt; i++)
        {
            Collider col = splashBuffer[i];
            if (col == null || ShouldIgnoreCollider(col)) continue;
            IDamageable dmg = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead && damagedTargets.Add(dmg))
            {
                dmg.TakeDamage(directDamage, col.ClosestPoint(transform.position));
                SpawnImpactAndSplashVFX(col.ClosestPoint(transform.position), Vector3.up, false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted || isShuttingDown || isStickyAttached || other == null) return;
        if (ShouldIgnoreCollider(other)) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (transform.position - hitPoint).sqrMagnitude > 0.0001f
            ? (transform.position - hitPoint).normalized
            : -transform.forward;

        ProcessImpact(hitPoint, hitNormal, other);
    }

    private void ProcessImpact(Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        IDamageable target = hitCollider != null
            ? (hitCollider.GetComponentInParent<IDamageable>() ?? hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInChildren<IDamageable>())
            : null;

        // 1. Wall Ricochet / Bounce
        if (impactBehavior == ProjectileImpactBehavior.WallRicochet && target == null && remainingPierceOrBounce > 0)
        {
            remainingPierceOrBounce--;
            transform.position = hitPoint + hitNormal * 0.1f;
            Vector3 reflected = Vector3.Reflect(transform.forward, hitNormal);
            if (reflected.sqrMagnitude > 0.001f) transform.forward = reflected.normalized;
            SpawnImpactAndSplashVFX(hitPoint, hitNormal, false);
            return;
        }

        // 2. Sticky Delayed Detonate
        if (impactBehavior == ProjectileImpactBehavior.StickyDelayedDetonate && activePattern != null)
        {
            isStickyAttached = true;
            stickyTimer = Mathf.Max(0.15f, activePattern.stickyDetonateDelay);
            transform.position = hitPoint;
            if (hitCollider != null)
            {
                stickyTarget = hitCollider.transform;
                stickyLocalOffset = stickyTarget.InverseTransformPoint(hitPoint);
            }
            return;
        }

        // 3. Pierce Targets (e.g. SlashWaveCrescent / Piercing Lightning)
        if (impactBehavior == ProjectileImpactBehavior.PierceTargets && target != null)
        {
            if (!target.IsDead && damagedTargets.Add(target))
            {
                target.TakeDamage(directDamage, hitPoint);
                SpawnImpactAndSplashVFX(hitPoint, hitNormal, false);
                remainingPierceOrBounce--;
                if (remainingPierceOrBounce > 0)
                {
                    transform.position = hitPoint + transform.forward * (collisionSphereRadius * 1.5f);
                    return;
                }
            }
            else
            {
                transform.position += transform.forward * (collisionSphereRadius * 1.2f);
                return;
            }
        }

        // 4. Chain Bounce (Chain Lightning!)
        if (impactBehavior == ProjectileImpactBehavior.ChainBounce && target != null)
        {
            if (!target.IsDead && damagedTargets.Add(target))
            {
                target.TakeDamage(directDamage, hitPoint);
                SpawnImpactAndSplashVFX(hitPoint, hitNormal, false);
                remainingPierceOrBounce--;

                if (remainingPierceOrBounce > 0)
                {
                    Transform nextTarget = FindNextChainTarget(hitPoint, activePattern != null ? activePattern.chainSearchRadius : 6.5f);
                    if (nextTarget != null)
                    {
                        homingTarget = nextTarget;
                        transform.position = hitPoint;
                        Vector3 nextDir = (nextTarget.position + Vector3.up * 1.0f) - hitPoint;
                        if (nextDir.sqrMagnitude > 0.001f) transform.forward = nextDir.normalized;
                        return;
                    }
                }
            }
        }

        // 5. Split / Cluster on Hit (with recursion depth guard <= 2)
        if (impactBehavior == ProjectileImpactBehavior.SplitOnHit
            && activePattern != null
            && activePattern.subPatternOnHit != null
            && currentSplitDepth < 2)
        {
            float baseAtkEstimate = activePattern.directDamageMultiplier > 0.01f
                ? directDamage / activePattern.directDamageMultiplier
                : directDamage;

            UniversalVFXManager.ExecuteSkillPattern(
                activePattern.subPatternOnHit,
                hitPoint + hitNormal * 0.2f,
                Quaternion.LookRotation(hitNormal.sqrMagnitude > 0.001f ? hitNormal : transform.forward),
                shooter,
                null,
                baseAtkEstimate,
                isEnemyProjectile,
                currentSplitDepth + 1);
        }

        DetonateAndShutdown(hitPoint, hitNormal, hitCollider);
    }

    private Transform FindNextChainTarget(Vector3 origin, float searchRadius)
    {
        int cnt = Physics.OverlapSphereNonAlloc(origin, searchRadius, splashBuffer, targetLayers);
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < cnt; i++)
        {
            Collider c = splashBuffer[i];
            if (c == null || IsSelfOrFriendlyCollider(c)) continue;
            IDamageable dmg = c.GetComponentInParent<IDamageable>() ?? c.GetComponent<IDamageable>();
            if (dmg == null || dmg.IsDead || damagedTargets.Contains(dmg)) continue;

            Transform t = (dmg is MonoBehaviour mb) ? mb.transform : c.transform;
            float sqr = (t.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }

    private void DetonateAndShutdown(Vector3 hitPoint, Vector3 hitNormal, Collider directHitCollider)
    {
        if (hasImpacted) return;
        hasImpacted = true;

        transform.position = hitPoint;
        if (cachedCollider != null) cachedCollider.enabled = false;

        IDamageable primaryTarget = null;
        bool dealtAnyDamage = false;

        if (directHitCollider != null)
        {
            primaryTarget = directHitCollider.GetComponentInParent<IDamageable>()
                         ?? directHitCollider.GetComponent<IDamageable>()
                         ?? directHitCollider.GetComponentInChildren<IDamageable>();

            if (primaryTarget != null && !primaryTarget.IsDead && damagedTargets.Add(primaryTarget))
            {
                primaryTarget.TakeDamage(directDamage, hitPoint);
                dealtAnyDamage = true;
            }
        }

        bool hitSecondaryTarget = false;
        if (splashRadius > 0.1f && splashDamage > 0f)
        {
            int splashMask = targetLayers.value != 0 ? targetLayers.value : ~LayerMask.GetMask("Ignore Raycast");
            int nearbyCount = Physics.OverlapSphereNonAlloc(hitPoint, splashRadius, splashBuffer, splashMask);

            for (int i = 0; i < nearbyCount; i++)
            {
                Collider col = splashBuffer[i];
                if (col == null || IsSelfOrFriendlyCollider(col)) continue;

                IDamageable splashTarget = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
                if (splashTarget != null && splashTarget != primaryTarget && !splashTarget.IsDead && damagedTargets.Add(splashTarget))
                {
                    splashTarget.TakeDamage(splashDamage, col.ClosestPoint(hitPoint));
                    hitSecondaryTarget = true;
                    dealtAnyDamage = true;
                }
            }
        }

        SpawnImpactAndSplashVFX(hitPoint, hitNormal, hitSecondaryTarget || splashRadius >= 2.5f);

        if (dealtAnyDamage && !isEnemyProjectile && HitStopManager.Instance != null && hitStopDuration > 0f)
        {
            HitStopManager.Instance.TriggerHitStop(hitStopDuration, 0.1f);
        }

        if (impulseSource != null && shakeIntensity > 0.05f)
        {
            impulseSource.GenerateImpulse(shakeIntensity);
        }

        BeginGracefulShutdown();
    }

    private void BeginGracefulShutdown()
    {
        isShuttingDown = true;
        float waitTime = trailDetachDelay;

        if (cachedVisuals != null)
        {
            waitTime = cachedVisuals.BeginGracefulDetach(trailDetachDelay);
        }
        else
        {
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++) meshRenderers[i].enabled = false;

            TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].emitting = false;
                waitTime = Mathf.Max(waitTime, trails[i].time + 0.05f);
            }

            LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++) lines[i].enabled = false;

            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        shutdownTimer = Mathf.Max(0.15f, waitTime);
    }

    private void CompleteReturnToPool()
    {
        isShuttingDown = false;
        if (dynamicTrailVfx != null)
        {
            dynamicTrailVfx.ForceStopAndReturn();
            dynamicTrailVfx = null;
        }

        if (returnToPoolCallback != null)
        {
            returnToPoolCallback.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (dynamicTrailVfx != null)
        {
            dynamicTrailVfx.ForceStopAndReturn();
            dynamicTrailVfx = null;
        }
    }

    private void RestoreVisualRenderers()
    {
        if (cachedVisuals != null) return;
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++) meshRenderers[i].enabled = true;

        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Clear();
            trails[i].emitting = true;
        }
    }

    private bool ShouldIgnoreCollider(Collider col)
    {
        if (col == null) return true;
        if (IsSelfOrFriendlyCollider(col)) return true;

        int colLayerBit = 1 << col.gameObject.layer;
        LayerMask validMask = targetLayers | obstacleLayers;
        if (validMask.value != 0 && (validMask.value & colLayerBit) == 0) return true;

        if (col.isTrigger)
        {
            bool hasDamageable = col.GetComponentInParent<IDamageable>() != null || col.GetComponent<IDamageable>() != null;
            if (!hasDamageable) return true;
        }

        return false;
    }

    private bool IsSelfOrFriendlyCollider(Collider col)
    {
        if (col == null) return true;
        if (shooter != null && (col.gameObject == shooter || col.transform.IsChildOf(shooter.transform) || col.transform.root == shooter.transform.root))
            return true;
        if (col.GetComponent<MagicProjectile>() != null || col.GetComponent<RewardDropParticle>() != null)
            return true;
        if (!isEnemyProjectile && col.GetComponentInParent<PlayerHealth>() != null) return true;
        if (isEnemyProjectile && col.GetComponentInParent<EnemyController>() != null) return true;
        return false;
    }

    private void SpawnMuzzleCastVFX(Vector3 pos, Quaternion rot)
    {
        UniversalVFXManager.SpawnAtPoint(VFXEventType.MuzzleCast, pos, rot, muzzleCastVFXPrefab, projectileVfxColor, 1.0f, useWhiteBlueLightningVFX, false);
    }

    private void AttachFlightTrailVFX()
    {
        if (cachedVisuals != null)
        {
            cachedVisuals.ResetForLaunch();
            return;
        }

        if (flightTrailVFXPrefab != null)
        {
            if (dynamicTrailVfx != null)
            {
                dynamicTrailVfx.ForceStopAndReturn();
                dynamicTrailVfx = null;
            }
            dynamicTrailVfx = UniversalVFXManager.SpawnPooledVFX(
                flightTrailVFXPrefab,
                transform.position,
                transform.rotation,
                1.0f,
                VFXFollowMode.FollowActorRoot,
                transform,
                transform,
                Vector3.zero,
                Quaternion.identity,
                false,
                Mathf.Max(0.5f, lifeTime + trailDetachDelay + 0.5f),
                "ProjectileTrail",
                true);
            return;
        }

        if (!autoCreateProceduralVFXIfMissing) return;

        cachedVisuals = gameObject.AddComponent<LightningProjectileVisuals>();

        GameObject coreHead = new GameObject("Layer1_PlasmaCore");
        coreHead.transform.SetParent(transform, false);

        GameObject innerTrailGo = new GameObject("Layer2A_InnerBrightTrail");
        innerTrailGo.transform.SetParent(transform, false);
        TrailRenderer innerTr = innerTrailGo.AddComponent<TrailRenderer>();
        innerTr.time = 0.24f;
        innerTr.minVertexDistance = 0.08f;
        innerTr.startWidth = 0.30f;
        innerTr.endWidth = 0.0f;
        innerTr.sortingOrder = 1;
        innerTr.sharedMaterial = UniversalVFXManager.GetSharedInnerTrailMaterial();
        innerTr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        innerTr.receiveShadows = false;

        GameObject outerTrailGo = new GameObject("Layer2B_OuterBlueTrail");
        outerTrailGo.transform.SetParent(transform, false);
        TrailRenderer outerTr = outerTrailGo.AddComponent<TrailRenderer>();
        outerTr.time = 0.38f;
        outerTr.minVertexDistance = 0.08f;
        outerTr.startWidth = 0.42f;
        outerTr.endWidth = 0.0f;
        outerTr.sortingOrder = 0;
        outerTr.sharedMaterial = UniversalVFXManager.GetSharedOuterTrailMaterial();
        outerTr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outerTr.receiveShadows = false;

        LineRenderer arcA = null;
        LineRenderer arcB = null;
        if (useWhiteBlueLightningVFX)
        {
            arcA = CreateProceduralArc("Layer2C_CoiledArc_A", 0.075f, 0.015f, Color.white, projectileVfxColor);
            arcB = CreateProceduralArc("Layer2D_CoiledArc_B", 0.055f, 0.012f, projectileVfxColor, new Color(0.08f, 0.35f, 1f, 0.85f));
        }

        GameObject sparkGo = new GameObject("Layer3_WorldTurbulenceSparks");
        sparkGo.transform.SetParent(transform, false);
        ParticleSystem sparkPs = sparkGo.AddComponent<ParticleSystem>();
        var main = sparkPs.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, projectileVfxColor);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = sparkPs.emission;
        emission.rateOverTime = 42f;
        var shape = sparkPs.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;
        var noise = sparkPs.noise;
        noise.enabled = true;
        noise.strength = 2.4f;
        noise.frequency = 2.0f;
        noise.scrollSpeed = 1.5f;
        var psRend = sparkGo.GetComponent<ParticleSystemRenderer>();
        psRend.renderMode = ParticleSystemRenderMode.Stretch;
        psRend.lengthScale = 2.2f;
        psRend.velocityScale = 0.06f;
        psRend.sharedMaterial = UniversalVFXManager.GetSharedParticleMaterial();

        GameObject lightGo = new GameObject("Layer4_ElectricPointLight");
        lightGo.transform.SetParent(transform, false);
        Light ptLight = lightGo.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.color = projectileVfxColor;
        ptLight.intensity = 2.4f;
        ptLight.range = 4.5f;
        ptLight.shadows = LightShadows.None;

        cachedVisuals.BindReferences(coreHead, innerTr, outerTr, arcA, arcB, sparkPs, ptLight);
        cachedVisuals.ResetForLaunch();
    }

    private LineRenderer CreateProceduralArc(string objName, float startW, float endW, Color startCol, Color endCol)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.sharedMaterial = UniversalVFXManager.GetSharedInnerTrailMaterial();
        lr.startWidth = startW;
        lr.endWidth = endW;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.startColor = startCol;
        lr.endColor = endCol;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    private void SpawnImpactAndSplashVFX(Vector3 hitPoint, Vector3 hitNormal, bool triggerSplashRing)
    {
        float impactScale = Mathf.Clamp(splashRadius * 0.4f, 0.9f, 1.8f);
        Quaternion impactRot = hitNormal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;

        UniversalVFXManager.SpawnAtPoint(
            VFXEventType.ProjectileImpact,
            hitPoint,
            impactRot,
            impactVFXPrefab,
            projectileVfxColor,
            impactScale,
            useWhiteBlueLightningVFX,
            false);

        if (triggerSplashRing && splashWaveVFXPrefab != null)
        {
            Color ringCol = new Color(projectileVfxColor.r, Mathf.Min(1f, projectileVfxColor.g + 0.2f), 1f, 0.9f);
            UniversalVFXManager.SpawnAtPoint(VFXEventType.GroundShockwave, hitPoint, Quaternion.identity, splashWaveVFXPrefab, ringCol, impactScale, false, true);
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ MagicProjectile.cs (Pattern-Driven + Pooled)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. InitializeFromPattern(...):
 *    - รับค่าจาก CombatSkillPattern_SO เพื่อรันวิถีการพุ่งครบทั้ง 11 รูปแบบ Projectile
 *      รวมถึง SlashWaveCrescent และ MeteorRain พร้อมพฤติกรรมการชน 6 แบบ (Explode, Pierce,
 *      ChainBounce, WallRicochet, StickyDelayedDetonate, SplitOnHit ที่มีตัวกัน Loop <= 2 ชั้น)
 *
 * 2. Zero-GC Graceful Pool Return:
 *    - เมื่อชนเป้าหมายจะเรียก BeginGracefulShutdown() เพื่อซ่อนหัวกระสุนและนับเวลาถอยหลังใน Update()
 *      จนหาง Trail จางหมดแล้วคืนตัวกลับเข้า ObjectPool ของ UniversalVFXManager โดยไม่สร้าง Garbage
 * ============================================================================ */
