using System;
using System.Collections.Generic;
using UnityEngine;

// CombatSystem: ตัวรันลำแสงทั้ง 5 รูปแบบ (Continuous, Telegraphed, Sweeping, CrossRotating, OrbitalSky)
// - ใช้ Physics.CapsuleCastNonAlloc / OverlapCapsuleNonAlloc (Zero-GC Hot Path)
// - รองรับ Object Pooling ผ่าน UniversalVFXManager (ไม่ Instantiate/Destroy ซ้ำซ้อน)
public class CombatBeamExecutor : MonoBehaviour
{
    private const int MaxBeamArms = 4;
    private readonly LineRenderer[] beamLines = new LineRenderer[MaxBeamArms];
    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly HashSet<IDamageable> tickDamaged = new HashSet<IDamageable>();

    private CombatSkillPattern_SO activePattern;
    private GameObject casterObj;
    private Transform casterTransform;
    private IDamageable casterDamageable;
    private bool isEnemyBeam;
    private LayerMask targetLayers;
    private LayerMask obstacleLayers;
    private float damagePerTick;

    private bool isRunning;
    private bool isInTelegraphPhase;
    private float phaseTimer;
    private float tickTimer;
    private float currentSweepAngle;
    private float baseYaw;
    private int activeArms;
    private Vector3 orbitalTargetPos;
    private PooledVFXInstance spawnedBodyVfx;
    private Action<CombatBeamExecutor> returnToPoolCallback;

    private void Awake()
    {
        EnsureLineRenderers();
    }

    private void EnsureLineRenderers()
    {
        for (int i = 0; i < MaxBeamArms; i++)
        {
            if (beamLines[i] != null) continue;
            GameObject armGo = new GameObject($"BeamArm_{i}");
            armGo.transform.SetParent(transform, false);
            LineRenderer lr = armGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            beamLines[i] = lr;
        }
    }

    public void Launch(
        CombatSkillPattern_SO pattern,
        Vector3 origin,
        Quaternion rotation,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool firedByEnemy,
        Action<CombatBeamExecutor> onReturnToPool)
    {
        EnsureLineRenderers();

        activePattern = pattern;
        casterObj = caster;
        casterTransform = caster != null ? caster.transform : null;
        casterDamageable = caster != null ? (caster.GetComponentInParent<IDamageable>() ?? caster.GetComponent<IDamageable>()) : null;
        isEnemyBeam = firedByEnemy;
        returnToPoolCallback = onReturnToPool;

        float safeDuration = Mathf.Max(0.15f, pattern.activeDuration);
        float safeTickInterval = Mathf.Max(0.05f, pattern.damageTickInterval);
        if (pattern.splitDamageAcrossDuration)
        {
            int expectedTicks = Mathf.Max(1, Mathf.FloorToInt(safeDuration / safeTickInterval) + 1);
            damagePerTick = Mathf.Max(1f, (baseAtk * pattern.directDamageMultiplier) / expectedTicks);
        }
        else
        {
            damagePerTick = Mathf.Max(1f, baseAtk * pattern.directDamageMultiplier * Mathf.Clamp(safeTickInterval, 0.05f, 1.0f));
        }

        ResolveLayers();

        transform.SetPositionAndRotation(origin, rotation);
        baseYaw = rotation.eulerAngles.y;
        currentSweepAngle = (pattern.patternPreset == CombatPatternPreset.SweepingBeam)
            ? -pattern.beamSweepDegrees * 0.5f
            : 0f;

        activeArms = (pattern.patternPreset == CombatPatternPreset.CrossRotatingBeam)
            ? Mathf.Clamp(pattern.crossBeamArms, 2, MaxBeamArms)
            : 1;

        if (pattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam)
        {
            Vector3 groundTarget = target != null
                ? target.position
                : origin + rotation * Vector3.forward * Mathf.Min(10f, pattern.beamRange * 0.6f);
            orbitalTargetPos = VFXAnchorRig.SnapToGroundSurface(groundTarget, 0.05f);
        }

        // Start in Telegraph Warning Phase if TelegraphedBeam or OrbitalSkyBeam with windup > 0
        bool needsTelegraph = (pattern.patternPreset == CombatPatternPreset.TelegraphedBeam
                            || pattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam)
                            && pattern.windupOrDelayDuration > 0.01f;

        isInTelegraphPhase = needsTelegraph;
        phaseTimer = needsTelegraph ? pattern.windupOrDelayDuration : Mathf.Max(0.15f, pattern.activeDuration);
        tickTimer = 0f;
        isRunning = true;

        if (needsTelegraph)
        {
            ConfigureLinesForTelegraph();
            if (pattern.telegraphWarningVFXPrefab != null)
            {
                Vector3 warnPos = pattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam ? orbitalTargetPos : origin;
                UniversalVFXManager.SpawnAtPoint(VFXEventType.GroundShockwave, warnPos, rotation, pattern.telegraphWarningVFXPrefab, Color.red, pattern.visualScale, false, pattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam);
            }
        }
        else
        {
            EnterFirePhase();
        }
    }

    private void EnterFirePhase()
    {
        isInTelegraphPhase = false;
        phaseTimer = Mathf.Max(0.15f, activePattern.activeDuration);
        tickTimer = 0f;

        ConfigureLinesForBeamFire();

        Vector3 fxPos = activePattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam ? orbitalTargetPos : transform.position;
        UniversalVFXManager.SpawnAtPoint(
            VFXEventType.MuzzleCast,
            fxPos,
            transform.rotation,
            activePattern.muzzleCastVFXPrefab,
            activePattern.themeColor,
            activePattern.visualScale,
            activePattern.useWhiteBlueLightningVFX,
            false);

        if (activePattern.mainBodyVFXPrefab != null)
        {
            spawnedBodyVfx = UniversalVFXManager.SpawnPooledVFX(
                activePattern.mainBodyVFXPrefab,
                fxPos,
                transform.rotation,
                Mathf.Max(0.1f, activePattern.visualScale),
                VFXFollowMode.FollowActorRoot,
                transform,
                transform,
                Vector3.zero,
                Quaternion.identity,
                false,
                Mathf.Max(0.25f, activePattern.activeDuration + 0.15f),
                "BeamBody",
                true);
        }
    }

    private void Update()
    {
        if (!isRunning || activePattern == null) return;

        if (activePattern.patternPreset != CombatPatternPreset.OrbitalSkyBeam &&
            (casterObj == null || !casterObj.activeInHierarchy || (casterDamageable != null && casterDamageable.IsDead)))
        {
            FinishAndReturnToPool();
            return;
        }

        float dt = Time.deltaTime;
        phaseTimer -= dt;

        // Keep beam anchored to caster for channeling / sweeping / cross beams
        if (casterTransform != null && activePattern.patternPreset != CombatPatternPreset.OrbitalSkyBeam)
        {
            Vector3 anchorPos = VFXAnchorRig.ResolvePosition(casterTransform, VFXSpawnAnchor.Muzzle);
            transform.position = anchorPos;
        }

        if (isInTelegraphPhase)
        {
            UpdateBeamGeometry(false);
            if (phaseTimer <= 0f)
            {
                EnterFirePhase();
            }
            return;
        }

        // Rotate sweeping or cross turbine beams
        if (activePattern.patternPreset == CombatPatternPreset.SweepingBeam)
        {
            float sweepSpeed = activePattern.beamSweepDegrees / Mathf.Max(0.1f, activePattern.activeDuration);
            currentSweepAngle += sweepSpeed * dt;
        }
        else if (activePattern.patternPreset == CombatPatternPreset.CrossRotatingBeam)
        {
            float rotSpeed = Mathf.Max(30f, activePattern.beamSweepDegrees);
            currentSweepAngle += rotSpeed * dt;
        }

        tickTimer -= dt;
        bool dealDamageThisFrame = false;
        if (tickTimer <= 0f)
        {
            tickTimer = Mathf.Max(0.05f, activePattern.damageTickInterval);
            dealDamageThisFrame = true;
            tickDamaged.Clear();
        }

        UpdateBeamGeometry(dealDamageThisFrame);

        if (phaseTimer <= 0f)
        {
            FinishAndReturnToPool();
        }
    }

    private void UpdateBeamGeometry(bool dealDamage)
    {
        if (activePattern.patternPreset == CombatPatternPreset.OrbitalSkyBeam)
        {
            Vector3 skyTop = orbitalTargetPos + Vector3.up * Mathf.Max(14f, activePattern.beamRange);
            beamLines[0].SetPosition(0, skyTop);
            beamLines[0].SetPosition(1, orbitalTargetPos);

            if (dealDamage)
            {
                ApplyBeamCapsuleDamage(skyTop, orbitalTargetPos, activePattern.beamWidth * 0.65f);
            }
            return;
        }

        float armStepDeg = 360f / activeArms;
        for (int i = 0; i < activeArms; i++)
        {
            float yaw = baseYaw + currentSweepAngle + (i * armStepDeg);
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 start = transform.position;
            float len = activePattern.beamRange;

            if (obstacleLayers.value != 0 && Physics.Raycast(start, dir, out RaycastHit wallHit, len, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                len = wallHit.distance;
            }

            Vector3 end = start + dir * len;
            beamLines[i].SetPosition(0, start);
            beamLines[i].SetPosition(1, end);

            if (dealDamage)
            {
                ApplyBeamCapsuleDamage(start, end, activePattern.beamWidth * 0.5f);
            }
        }
    }

    private void ApplyBeamCapsuleDamage(Vector3 start, Vector3 end, float radius)
    {
        Vector3 delta = end - start;
        float dist = delta.magnitude;
        if (dist < 0.05f) return;

        int hitCount = Physics.SphereCastNonAlloc(start, radius, delta / dist, hitBuffer, dist, targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null || IsFriendly(col)) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead && tickDamaged.Add(dmg))
            {
                Vector3 hitPt = hitBuffer[i].point.sqrMagnitude > 0.001f ? hitBuffer[i].point : col.ClosestPoint(start);
                dmg.TakeDamage(damagePerTick, hitPt);

                UniversalVFXManager.SpawnAtPoint(
                    VFXEventType.ProjectileImpact,
                    hitPt,
                    Quaternion.identity,
                    activePattern.impactVFXPrefab,
                    activePattern.themeColor,
                    activePattern.visualScale * 0.7f,
                    activePattern.useWhiteBlueLightningVFX,
                    false);
            }
        }
    }

    private bool IsFriendly(Collider col)
    {
        if (casterObj != null && (col.gameObject == casterObj || col.transform.root == casterObj.transform.root))
            return true;
        if (!isEnemyBeam && col.GetComponentInParent<PlayerHealth>() != null) return true;
        if (isEnemyBeam && col.GetComponentInParent<EnemyController>() != null) return true;
        return false;
    }

    private void ConfigureLinesForTelegraph()
    {
        Material warnMat = UniversalVFXManager.GetSharedOuterTrailMaterial();
        Color warnCol = new Color(1.0f, 0.22f, 0.25f, 0.75f);
        for (int i = 0; i < MaxBeamArms; i++)
        {
            beamLines[i].enabled = (i < activeArms);
            if (i >= activeArms) continue;
            beamLines[i].sharedMaterial = warnMat;
            beamLines[i].startWidth = 0.14f;
            beamLines[i].endWidth = 0.14f;
            beamLines[i].startColor = warnCol;
            beamLines[i].endColor = warnCol;
        }
    }

    private void ConfigureLinesForBeamFire()
    {
        Material beamMat = UniversalVFXManager.GetSharedInnerTrailMaterial();
        float width = Mathf.Max(0.2f, activePattern.beamWidth * activePattern.visualScale);
        for (int i = 0; i < MaxBeamArms; i++)
        {
            beamLines[i].enabled = (i < activeArms);
            if (i >= activeArms) continue;
            beamLines[i].sharedMaterial = beamMat;
            beamLines[i].startWidth = width;
            beamLines[i].endWidth = width * 0.85f;
            beamLines[i].startColor = Color.white;
            beamLines[i].endColor = activePattern.themeColor;
        }
    }

    private void ResolveLayers()
    {
        if (isEnemyBeam)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            targetLayers = (playerLayer != -1) ? (1 << playerLayer) : ~LayerMask.GetMask("Enemy", "Ignore Raycast");
        }
        else
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            targetLayers = (enemyLayer != -1) ? (1 << enemyLayer) : ~LayerMask.GetMask("Player", "Ignore Raycast");
        }

        int envLayer = LayerMask.NameToLayer("Environment");
        obstacleLayers = (envLayer != -1) ? (1 << envLayer) : 0;
    }

    private void FinishAndReturnToPool()
    {
        isRunning = false;
        for (int i = 0; i < MaxBeamArms; i++)
        {
            if (beamLines[i] != null) beamLines[i].enabled = false;
        }

        if (spawnedBodyVfx != null)
        {
            spawnedBodyVfx.ForceStopAndReturn();
            spawnedBodyVfx = null;
        }

        casterDamageable = null;

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
        if (spawnedBodyVfx != null)
        {
            spawnedBodyVfx.ForceStopAndReturn();
            spawnedBodyVfx = null;
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ CombatBeamExecutor.cs (CombatSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. รองรับลำแสงครบทั้ง 5 รูปแบบ:
 *    - ContinuousChannelBeam : ลำแสงยิงค้างทำดาเมจตาม tickInterval
 *    - TelegraphedBeam       : แสดงเส้นแจ้งเตือนสีแดงก่อนตาม windupOrDelayDuration แล้วค่อยยิงลำแสงจริง
 *    - SweepingBeam          : ลำแสงกวาดหมุนด้านข้างตาม beamSweepDegrees
 *    - CrossRotatingBeam     : ลำแสง 2-4 ทิศหมุนรอบตัวเป็นกังหัน
 *    - OrbitalSkyBeam        : ลำแสงดิ่งจากฟ้าลงพิกัดเป้าหมาย (มี Telegraph บนพื้นล่วงหน้า)
 *
 * 2. Zero-GC & Pooling:
 *    - ใช้ SphereCastNonAlloc กับ hitBuffer[24] และคืนตัวกลับเข้า Pool ของ UniversalVFXManager เมื่อจบระยะเวลา
 * ============================================================================ */
