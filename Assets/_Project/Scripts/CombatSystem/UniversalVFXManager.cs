using System.Collections.Generic;
using MahouShoujo.VFX;
using UnityEngine;
using UnityEngine.Pool;

public enum VFXEventType
{
    MuzzleCast = 0,
    MeleeHit = 1,
    ProjectileImpact = 2,
    SkillAoE = 3,
    GroundShockwave = 4,
    StatusBurst = 5
}

// CombatSystem: ศูนย์กลางรวม VFX, ObjectPool และตัวจ่ายงานสกิล 22 รูปแบบ (Projectiles / Beams / Area & Melee-Ranged)
// - จัดการ ObjectPool แยก 3 Executor: MagicProjectile, CombatBeamExecutor, CombatAreaSkillExecutor
// - รวม Shared URP Transparent/Additive Material Factory ไว้ที่เดียว (Zero Material Leak, Zero Black-Box Bug)
public class UniversalVFXManager : MonoBehaviour
{
    public static UniversalVFXManager Instance { get; private set; }

    [Header("1. White-Blue Lightning Prefabs (URP 3-Stage)")]
    [SerializeField] private GameObject lightningMuzzlePrefab;
    [SerializeField] private MagicProjectile lightningProjectileMainPrefab;
    [SerializeField] private GameObject lightningImpactPrefab;
    [SerializeField] private GameObject lightningShockwavePrefab;

    [Header("2. Global Default VFX Prefabs")]
    [SerializeField] private GameObject defaultMuzzleCastVFX;
    [SerializeField] private GameObject defaultMeleeHitVFX;
    [SerializeField] private GameObject defaultProjectileImpactVFX;
    [SerializeField] private GameObject defaultSkillAoEVFX;
    [SerializeField] private GameObject defaultGroundShockwaveVFX;
    [SerializeField] private GameObject defaultStatusBurstVFX;

    [Header("3. Pool & Cleanup Settings")]
    [SerializeField] private float defaultAutoDestroyDelay = 2.5f;
    [SerializeField] private int defaultPoolCapacity = 24;
    [SerializeField] private int maxPoolSize = 96;

    // Object Pools (Zero Instantiate/Destroy spikes)
    private readonly Dictionary<EntityId, ObjectPool<MagicProjectile>> projectilePools = new Dictionary<EntityId, ObjectPool<MagicProjectile>>();
    private ObjectPool<CombatBeamExecutor> beamPool;
    private ObjectPool<CombatAreaSkillExecutor> areaPool;

    // Zero-GC Deferred Volley Scheduler (handles BarrageRapidFire, SpiralVortex, MeteorRain without allocating Coroutines)
    private struct ScheduledShot
    {
        public float delayRemaining;
        public CombatSkillPattern_SO pattern;
        public Vector3 origin;
        public Quaternion rotation;
        public GameObject caster;
        public Transform target;
        public float baseAtk;
        public bool isEnemy;
        public int index;
        public int total;
        public int splitDepth;
    }

    private readonly List<ScheduledShot> scheduledShots = new List<ScheduledShot>(32);

    // Single Source of Truth: Shared URP Materials
    private static Texture2D sharedGlowTex;
    private static Material sharedInnerLightningTrailMat;
    private static Material sharedOuterLightningTrailMat;
    private static Material sharedProceduralParticleMat;
    private static Material sharedTranslucentDomeMat;

    public MagicProjectile LightningProjectileMainPrefab => lightningProjectileMainPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeExecutorPools();
    }

    private void InitializeExecutorPools()
    {
        beamPool = new ObjectPool<CombatBeamExecutor>(
            createFunc: () =>
            {
                GameObject go = new GameObject("Pooled_CombatBeamExecutor");
                go.transform.SetParent(transform, false);
                return go.AddComponent<CombatBeamExecutor>();
            },
            actionOnGet: exec => exec.gameObject.SetActive(true),
            actionOnRelease: exec => exec.gameObject.SetActive(false),
            actionOnDestroy: exec => { if (exec != null) Destroy(exec.gameObject); },
            collectionCheck: false,
            defaultCapacity: 6,
            maxSize: 24);

        areaPool = new ObjectPool<CombatAreaSkillExecutor>(
            createFunc: () =>
            {
                GameObject go = new GameObject("Pooled_CombatAreaSkillExecutor");
                go.transform.SetParent(transform, false);
                return go.AddComponent<CombatAreaSkillExecutor>();
            },
            actionOnGet: exec => exec.gameObject.SetActive(true),
            actionOnRelease: exec => exec.gameObject.SetActive(false),
            actionOnDestroy: exec => { if (exec != null) Destroy(exec.gameObject); },
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: 32);
    }

    private ObjectPool<MagicProjectile> GetOrCreateProjectilePool(MagicProjectile prefab)
    {
        EntityId key = prefab != null ? prefab.GetEntityId() : default;
        if (!projectilePools.TryGetValue(key, out var pool))
        {
            MagicProjectile capturedPrefab = prefab;
            pool = new ObjectPool<MagicProjectile>(
                createFunc: () =>
                {
                    MagicProjectile inst;
                    if (capturedPrefab != null)
                    {
                        inst = Instantiate(capturedPrefab);
                    }
                    else
                    {
                        GameObject go = new GameObject("Pooled_RuntimeMagicProjectile");
                        inst = go.AddComponent<MagicProjectile>();
                    }
                    return inst;
                },
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: false,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolSize);

            projectilePools[key] = pool;
        }
        return pool;
    }

    private void Update()
    {
        if (scheduledShots.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = scheduledShots.Count - 1; i >= 0; i--)
        {
            ScheduledShot shot = scheduledShots[i];
            shot.delayRemaining -= dt;
            if (shot.delayRemaining <= 0f)
            {
                scheduledShots.RemoveAt(i);
                if (shot.caster != null && shot.pattern != null)
                {
                    SpawnSinglePatternProjectile(
                        shot.pattern,
                        shot.origin,
                        shot.rotation,
                        shot.caster,
                        shot.target,
                        shot.baseAtk,
                        shot.isEnemy,
                        shot.index,
                        shot.total,
                        shot.splitDepth);
                }
            }
            else
            {
                scheduledShots[i] = shot;
            }
        }
    }

    public void AssignLightningPrefabs(GameObject muzzle, MagicProjectile mainProj, GameObject impact, GameObject shockwave = null)
    {
        lightningMuzzlePrefab = muzzle;
        lightningProjectileMainPrefab = mainProj;
        lightningImpactPrefab = impact;
        if (shockwave != null) lightningShockwavePrefab = shockwave;
    }

    // =========================================================================
    // MASTER SKILL PATTERN DISPATCHER (22 PATTERNS: PROJECTILE / BEAM / AREA)
    // =========================================================================

    public static void ExecuteSkillPattern(
        CombatSkillPattern_SO pattern,
        Vector3 origin,
        Quaternion baseRotation,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool isEnemy,
        int splitDepth = 0)
    {
        if (pattern == null) return;

        SkillDeliveryCategory category = pattern.ResolveEffectiveCategory();

        // Category 2: Beams (5 Patterns)
        if (category == SkillDeliveryCategory.Beam)
        {
            CombatBeamExecutor beam = (Instance != null && Instance.beamPool != null)
                ? Instance.beamPool.Get()
                : new GameObject("Runtime_CombatBeamExecutor").AddComponent<CombatBeamExecutor>();

            beam.Launch(
                pattern,
                origin,
                baseRotation,
                caster,
                target,
                baseAtk,
                isEnemy,
                Instance != null ? (b => Instance.beamPool.Release(b)) : null);
            return;
        }

        // Category 3: Area & Dash-Strike (4 Patterns)
        if (category == SkillDeliveryCategory.AreaOrDash)
        {
            CombatAreaSkillExecutor area = (Instance != null && Instance.areaPool != null)
                ? Instance.areaPool.Get()
                : new GameObject("Runtime_CombatAreaSkillExecutor").AddComponent<CombatAreaSkillExecutor>();

            area.Launch(
                pattern,
                origin,
                baseRotation,
                caster,
                target,
                baseAtk,
                isEnemy,
                Instance != null ? (a => Instance.areaPool.Release(a)) : null);
            return;
        }

        // Category 1: Projectiles (11 Patterns + SlashWaveCrescent + MeteorRain)
        DispatchProjectileVolley(pattern, origin, baseRotation, caster, target, baseAtk, isEnemy, splitDepth);
    }

    private static void DispatchProjectileVolley(
        CombatSkillPattern_SO pattern,
        Vector3 origin,
        Quaternion baseRotation,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool isEnemy,
        int splitDepth)
    {
        int total = Mathf.Max(1, pattern.count);

        for (int i = 0; i < total; i++)
        {
            Vector3 shotPos = origin;
            Quaternion shotRot = baseRotation;
            float delay = 0f;

            switch (pattern.patternPreset)
            {
                case CombatPatternPreset.SpreadFan:
                case CombatPatternPreset.SlashWaveCrescent:
                    if (total > 1)
                    {
                        float norm = (float)i / (total - 1) - 0.5f;
                        shotRot = baseRotation * Quaternion.Euler(0f, norm * pattern.spreadAngle * 2f, 0f);
                    }
                    break;

                case CombatPatternPreset.RadialNova360:
                    shotRot = baseRotation * Quaternion.Euler(0f, (360f / total) * i, 0f);
                    break;

                case CombatPatternPreset.SpiralVortex:
                    shotRot = baseRotation * Quaternion.Euler(0f, (360f / total) * i, 0f);
                    delay = i * Mathf.Max(0.03f, pattern.sequenceInterval);
                    break;

                case CombatPatternPreset.BarrageRapidFire:
                    delay = i * Mathf.Max(0.03f, pattern.sequenceInterval);
                    shotRot = baseRotation * Quaternion.Euler(0f, Random.Range(-pattern.spreadAngle * 0.25f, pattern.spreadAngle * 0.25f), 0f);
                    break;

                case CombatPatternPreset.DelayedHomingItano:
                    // Spawn in a semicircular magical halo behind/around caster
                    float haloAngle = (total > 1 ? ((float)i / (total - 1) - 0.5f) : 0f) * 140f;
                    Vector3 offset = baseRotation * Quaternion.Euler(0f, haloAngle, 0f) * (Vector3.right * pattern.formationRadius + Vector3.up * (0.6f + (i % 2) * 0.45f));
                    shotPos = origin + offset;
                    shotRot = baseRotation * Quaternion.Euler(-12f, haloAngle * 0.5f, 0f);
                    break;

                case CombatPatternPreset.OrbitSatellite:
                    float orbRad = (360f * i / total) * Mathf.Deg2Rad;
                    shotPos = origin + new Vector3(Mathf.Cos(orbRad), 0f, Mathf.Sin(orbRad)) * pattern.formationRadius;
                    break;

                case CombatPatternPreset.MeteorRain:
                    Vector3 rainCenter = target != null ? target.position : origin + baseRotation * Vector3.forward * 8f;
                    Vector2 rndCircle = Random.insideUnitCircle * Mathf.Max(1.0f, pattern.formationRadius);
                    Vector3 landPos = rainCenter + new Vector3(rndCircle.x, 0f, rndCircle.y);
                    shotPos = landPos + Vector3.up * 12f - (baseRotation * Vector3.forward * 3.5f);
                    shotRot = Quaternion.LookRotation((landPos - shotPos).normalized);
                    delay = i * Mathf.Max(0.05f, pattern.sequenceInterval);
                    break;
            }

            if (delay > 0.001f && Instance != null)
            {
                Instance.scheduledShots.Add(new ScheduledShot
                {
                    delayRemaining = delay,
                    pattern = pattern,
                    origin = shotPos,
                    rotation = shotRot,
                    caster = caster,
                    target = target,
                    baseAtk = baseAtk,
                    isEnemy = isEnemy,
                    index = i,
                    total = total,
                    splitDepth = splitDepth
                });
            }
            else
            {
                SpawnSinglePatternProjectile(pattern, shotPos, shotRot, caster, target, baseAtk, isEnemy, i, total, splitDepth);
            }
        }
    }

    private static MagicProjectile SpawnSinglePatternProjectile(
        CombatSkillPattern_SO pattern,
        Vector3 spawnPos,
        Quaternion shotRot,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool isEnemy,
        int index,
        int total,
        int splitDepth)
    {
        MagicProjectile prefabToUse = pattern.customProjectilePrefab != null
            ? pattern.customProjectilePrefab
            : (pattern.useWhiteBlueLightningVFX && Instance != null ? Instance.lightningProjectileMainPrefab : null);

        MagicProjectile proj = RentPooledProjectile(prefabToUse, spawnPos, shotRot);
        proj.InitializeFromPattern(pattern, caster, target, baseAtk, isEnemy, index, total, splitDepth);
        return proj;
    }

    // =========================================================================
    // POOLED PROJECTILE SPAWNER (LEGACY / FALLBACK — ZERO MATERIAL LEAK)
    // =========================================================================

    public static MagicProjectile SpawnProjectile(
        Vector3 spawnPos,
        Quaternion rotation,
        GameObject caster,
        Transform homingTarget,
        float directDmg,
        float splashDmg,
        float splashRad,
        float flightSpeed,
        float homingTurnSpeed,
        MagicProjectile overrideProjectilePrefab,
        GameObject customMuzzleVfx,
        GameObject customImpactVfx,
        GameObject customSplashVfx,
        Color themeColor,
        bool useLightningStyle,
        bool firedByEnemy)
    {
        MagicProjectile prefabToUse = overrideProjectilePrefab != null
            ? overrideProjectilePrefab
            : (useLightningStyle && Instance != null ? Instance.lightningProjectileMainPrefab : null);

        MagicProjectile instance = RentPooledProjectile(prefabToUse, spawnPos, rotation);
        instance.SetUseWhiteBlueLightningVFX(useLightningStyle);
        instance.ConfigureFlightSpeed(flightSpeed, homingTurnSpeed);
        instance.Initialize(
            caster,
            homingTarget,
            directDmg,
            splashDmg,
            splashRad,
            customMuzzleVfx != null ? customMuzzleVfx : (useLightningStyle && Instance != null ? Instance.lightningMuzzlePrefab : null),
            customImpactVfx != null ? customImpactVfx : (useLightningStyle && Instance != null ? Instance.lightningImpactPrefab : null),
            customSplashVfx != null ? customSplashVfx : (useLightningStyle && Instance != null ? Instance.lightningShockwavePrefab : null),
            themeColor,
            firedByEnemy);
        return instance;
    }

    private static MagicProjectile RentPooledProjectile(MagicProjectile prefab, Vector3 pos, Quaternion rot)
    {
        if (Instance != null)
        {
            ObjectPool<MagicProjectile> pool = Instance.GetOrCreateProjectilePool(prefab);
            MagicProjectile inst = pool.Get();
            // Move BEFORE Initialize/Trail Clear so TrailRenderer never stretches across the map!
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.BindPoolReturn(p => pool.Release(p));
            return inst;
        }

        MagicProjectile fallback = prefab != null
            ? Instantiate(prefab, pos, rot)
            : new GameObject("RuntimeMagicProjectile").AddComponent<MagicProjectile>();
        fallback.transform.SetPositionAndRotation(pos, rot);
        fallback.BindPoolReturn(null);
        return fallback;
    }

    // =========================================================================
    // STANDARD VFX EVENT SPAWNERS (100% BACKWARD COMPATIBLE)
    // =========================================================================

    public static GameObject SpawnOnOwner(
        VFXEventType eventType,
        Transform owner,
        VFXSpawnAnchor anchor,
        GameObject overridePrefab = null,
        Color? themeColor = null,
        float scale = 1.0f,
        bool useLightningStyle = false,
        bool attachToOwner = false)
    {
        Vector3 pos = VFXAnchorRig.ResolvePosition(owner, anchor);
        Quaternion rot = VFXAnchorRig.ResolveRotation(owner, anchor);
        bool snapGround = (anchor == VFXSpawnAnchor.GroundFeet);

        GameObject spawned = SpawnAtPoint(eventType, pos, rot, overridePrefab, themeColor, scale, useLightningStyle, snapGround);
        if (spawned != null && attachToOwner && owner != null)
            spawned.transform.SetParent(owner, true);
        return spawned;
    }

    public static GameObject SpawnAtPoint(
        VFXEventType eventType,
        Vector3 worldPos,
        Quaternion rotation,
        GameObject overridePrefab = null,
        Color? themeColor = null,
        float scale = 1.0f,
        bool useLightningStyle = false,
        bool snapToGround = false)
    {
        Vector3 finalPos = snapToGround ? VFXAnchorRig.SnapToGroundSurface(worldPos, 0.05f) : worldPos;
        Color finalColor = themeColor ?? ResolveDefaultColor(eventType);
        float destroyDelay = Instance != null ? Instance.defaultAutoDestroyDelay : 2.5f;

        if (overridePrefab != null)
            return InstantiateAndPlayPrefab(overridePrefab, finalPos, rotation, scale, destroyDelay);

        if (useLightningStyle)
        {
            GameObject lightningPrefab = Instance != null ? Instance.GetLightningPrefabForEvent(eventType) : null;
            if (lightningPrefab != null)
                return InstantiateAndPlayPrefab(lightningPrefab, finalPos, rotation, scale, destroyDelay);

            Vector3 resolvedNormal = (rotation * Vector3.forward).sqrMagnitude > 0.001f
                ? (rotation * Vector3.forward).normalized
                : Vector3.up;

            if (eventType == VFXEventType.MuzzleCast)
                return LightningBoltVFXController.CreateMuzzleVFXInstance(finalPos, rotation);
            if (eventType == VFXEventType.ProjectileImpact || eventType == VFXEventType.SkillAoE || eventType == VFXEventType.GroundShockwave)
                return LightningBoltVFXController.CreateImpactVFXInstance(finalPos, resolvedNormal, Mathf.Max(1.1f, 1.4f * scale));
        }

        GameObject globalPrefab = Instance != null ? Instance.GetGlobalDefaultPrefab(eventType) : null;
        if (globalPrefab != null)
            return InstantiateAndPlayPrefab(globalPrefab, finalPos, rotation, scale, destroyDelay);

        return SpawnProceduralBurst(eventType, finalPos, finalColor, scale);
    }

    private GameObject GetLightningPrefabForEvent(VFXEventType eventType)
    {
        switch (eventType)
        {
            case VFXEventType.MuzzleCast: return lightningMuzzlePrefab;
            case VFXEventType.ProjectileImpact:
            case VFXEventType.MeleeHit: return lightningImpactPrefab;
            case VFXEventType.SkillAoE:
            case VFXEventType.GroundShockwave: return lightningShockwavePrefab != null ? lightningShockwavePrefab : lightningImpactPrefab;
            default: return null;
        }
    }

    private GameObject GetGlobalDefaultPrefab(VFXEventType eventType)
    {
        switch (eventType)
        {
            case VFXEventType.MuzzleCast: return defaultMuzzleCastVFX;
            case VFXEventType.MeleeHit: return defaultMeleeHitVFX;
            case VFXEventType.ProjectileImpact: return defaultProjectileImpactVFX;
            case VFXEventType.SkillAoE: return defaultSkillAoEVFX;
            case VFXEventType.GroundShockwave: return defaultGroundShockwaveVFX;
            case VFXEventType.StatusBurst: return defaultStatusBurstVFX;
            default: return null;
        }
    }

    private static GameObject InstantiateAndPlayPrefab(GameObject prefab, Vector3 pos, Quaternion rot, float scale, float destroyDelay)
    {
        GameObject vfx = Instantiate(prefab, pos, rot);
        if (Mathf.Abs(scale - 1.0f) > 0.01f)
            vfx.transform.localScale *= Mathf.Max(0.1f, scale);

        vfx.SetActive(true);
        ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].gameObject.SetActive(true);
            systems[i].Play(true);
        }

        Destroy(vfx, Mathf.Max(0.5f, destroyDelay));
        return vfx;
    }

    private static GameObject SpawnProceduralBurst(VFXEventType type, Vector3 pos, Color color, float scale)
    {
        int count = 20;
        float speed = 5.5f;
        float size = 0.28f * scale;
        float life = 0.35f;

        switch (type)
        {
            case VFXEventType.MuzzleCast: count = 14; speed = 3.5f; size = 0.22f * scale; life = 0.24f; break;
            case VFXEventType.MeleeHit: count = 18; speed = 6.2f; size = 0.24f * scale; life = 0.28f; break;
            case VFXEventType.ProjectileImpact: count = 26; speed = 7.0f; size = 0.32f * scale; life = 0.38f; break;
            case VFXEventType.SkillAoE:
            case VFXEventType.GroundShockwave: count = 38; speed = 9.5f; size = 0.38f * scale; life = 0.48f; break;
            case VFXEventType.StatusBurst: count = 28; speed = 7.5f; size = 0.30f * scale; life = 0.42f; break;
        }

        GameObject vfxGo = new GameObject($"UniversalVFX_{type}");
        vfxGo.transform.position = pos;

        ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = life + 0.1f;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = (type == VFXEventType.GroundShockwave) ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f * scale;

        var psRenderer = vfxGo.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null) psRenderer.sharedMaterial = GetSharedParticleMaterial();

        ps.Play();
        Destroy(vfxGo, life + 0.4f);
        return vfxGo;
    }

    // =========================================================================
    // CENTRALIZED SHARED URP MATERIAL FACTORY (SINGLE SOURCE OF TRUTH)
    // =========================================================================

    public static Texture2D GetSharedGlowTexture()
    {
        if (sharedGlowTex == null)
        {
            sharedGlowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                name = "Shared_UniversalVFX_GlowTex",
                wrapMode = TextureWrapMode.Clamp
            };
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    Color c = Color.Lerp(new Color(0.15f, 0.65f, 1f, a), new Color(1f, 1f, 1f, a), a * a);
                    sharedGlowTex.SetPixel(x, y, c);
                }
            }
            sharedGlowTex.Apply();
        }
        return sharedGlowTex;
    }

    private static Material CreateConfiguredUrpAdditiveMaterial(string matName, Color coreHdr, Color edgeHdr, bool isOuter)
    {
        Shader customUrp = Shader.Find("MahouShoujo/VFX/LightningTrailURP");
        if (customUrp != null)
        {
            Material mat = new Material(customUrp) { name = matName };
            mat.SetTexture("_MainTex", GetSharedGlowTexture());
            mat.SetColor("_ColorCore", coreHdr);
            mat.SetColor("_ColorEdge", edgeHdr);
            mat.SetVector("_MainTexSpeed", isOuter ? new Vector4(-1.8f, 0f, 0f, 0f) : new Vector4(-2.8f, 0f, 0f, 0f));
            mat.SetFloat("_DistortStrength", isOuter ? 0.11f : 0.07f);
            mat.SetFloat("_DissolveScale", isOuter ? 28f : 38f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            return mat;
        }

        Shader fallback = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                       ?? Shader.Find("Particles/Standard Unlit")
                       ?? Shader.Find("Sprites/Default");
        Material fallbackMat = new Material(fallback) { name = matName };
        fallbackMat.mainTexture = GetSharedGlowTexture();
        if (fallbackMat.HasProperty("_Surface")) fallbackMat.SetFloat("_Surface", 1f);
        if (fallbackMat.HasProperty("_Blend")) fallbackMat.SetFloat("_Blend", 2f);
        if (fallbackMat.HasProperty("_SrcBlend")) fallbackMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (fallbackMat.HasProperty("_DstBlend")) fallbackMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (fallbackMat.HasProperty("_ZWrite")) fallbackMat.SetFloat("_ZWrite", 0f);
        if (fallbackMat.HasProperty("_BaseColor")) fallbackMat.SetColor("_BaseColor", coreHdr);
        if (fallbackMat.HasProperty("_Color")) fallbackMat.SetColor("_Color", coreHdr);
        fallbackMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fallbackMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return fallbackMat;
    }

    public static Material GetSharedInnerTrailMaterial()
    {
        if (sharedInnerLightningTrailMat == null)
        {
            sharedInnerLightningTrailMat = CreateConfiguredUrpAdditiveMaterial(
                "Shared_UniversalVFX_InnerTrailMat",
                new Color(3.2f, 3.8f, 4.5f, 1f),
                new Color(0.25f, 0.85f, 3.2f, 1f),
                false);
        }
        return sharedInnerLightningTrailMat;
    }

    public static Material GetSharedOuterTrailMaterial()
    {
        if (sharedOuterLightningTrailMat == null)
        {
            sharedOuterLightningTrailMat = CreateConfiguredUrpAdditiveMaterial(
                "Shared_UniversalVFX_OuterTrailMat",
                new Color(0.25f, 0.78f, 2.6f, 0.9f),
                new Color(0.05f, 0.28f, 1.4f, 0.65f),
                true);
        }
        return sharedOuterLightningTrailMat;
    }

    public static Material GetSharedParticleMaterial()
    {
        if (sharedProceduralParticleMat == null)
        {
            sharedProceduralParticleMat = CreateConfiguredUrpAdditiveMaterial(
                "Shared_UniversalVFX_ParticleMat",
                new Color(2.4f, 3.0f, 4.0f, 1f),
                new Color(0.20f, 0.75f, 2.5f, 1f),
                false);
        }
        return sharedProceduralParticleMat;
    }

    public static Material GetSharedTranslucentDomeMaterial()
    {
        if (sharedTranslucentDomeMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            sharedTranslucentDomeMat = new Material(sh) { name = "Shared_UniversalVFX_DomeMat" };
            sharedTranslucentDomeMat.mainTexture = GetSharedGlowTexture();
            if (sharedTranslucentDomeMat.HasProperty("_Surface")) sharedTranslucentDomeMat.SetFloat("_Surface", 1f);
            if (sharedTranslucentDomeMat.HasProperty("_Blend")) sharedTranslucentDomeMat.SetFloat("_Blend", 0f);
            if (sharedTranslucentDomeMat.HasProperty("_SrcBlend")) sharedTranslucentDomeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (sharedTranslucentDomeMat.HasProperty("_DstBlend")) sharedTranslucentDomeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (sharedTranslucentDomeMat.HasProperty("_ZWrite")) sharedTranslucentDomeMat.SetFloat("_ZWrite", 0f);
            Color domeCol = new Color(0.12f, 0.55f, 1.0f, 0.28f);
            if (sharedTranslucentDomeMat.HasProperty("_BaseColor")) sharedTranslucentDomeMat.SetColor("_BaseColor", domeCol);
            if (sharedTranslucentDomeMat.HasProperty("_Color")) sharedTranslucentDomeMat.SetColor("_Color", domeCol);
            sharedTranslucentDomeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedTranslucentDomeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return sharedTranslucentDomeMat;
    }

    private static Color ResolveDefaultColor(VFXEventType type)
    {
        switch (type)
        {
            case VFXEventType.MuzzleCast: return new Color(0.35f, 0.85f, 1.0f, 1.0f);
            case VFXEventType.MeleeHit: return new Color(0.95f, 0.35f, 0.80f, 1.0f);
            case VFXEventType.ProjectileImpact: return new Color(0.35f, 0.82f, 1.0f, 1.0f);
            case VFXEventType.SkillAoE: return new Color(1.0f, 0.25f, 0.65f, 1.0f);
            case VFXEventType.GroundShockwave: return new Color(1.0f, 0.18f, 0.28f, 1.0f);
            default: return new Color(1.0f, 0.85f, 0.35f, 1.0f);
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ UniversalVFXManager.cs (Unified 22-Pattern Hub)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. ExecuteSkillPattern(CombatSkillPattern_SO, ...):
 *    - จุดเรียกกลางจุดเดียวสำหรับทุกท่าโจมตีทั้ง 22 รูปแบบ โดยจ่ายงานแยก 3 Executor ไม่ทับกัน:
 *      (1) Projectile -> ObjectPool<MagicProjectile> (รองรับ Barrage/Spiral/Meteor แบบ Zero-GC List)
 *      (2) Beam       -> ObjectPool<CombatBeamExecutor> (ลำแสง 5 แบบ)
 *      (3) AreaOrDash -> ObjectPool<CombatAreaSkillExecutor> (สกิลพื้นที่/วงคลื่น/พุ่งฟัน/บ่อ DoT)
 *
 * 2. SpawnProjectile(...) & Shared URP Material Factory:
 *    - ดึงกระสุนจาก ObjectPool พร้อมตั้งตำแหน่งก่อนล้าง Trail ป้องกันเส้น Trail ลากข้ามฉาก
 *    - รวม Shared URP Material ไว้จุดเดียว แก้ปัญหา GPU Material Leak และกล่องดำใน Unity 6 URP
 * ============================================================================ */
