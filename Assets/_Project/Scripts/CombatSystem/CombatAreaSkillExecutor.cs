using System;
using System.Collections.Generic;
using UnityEngine;

// CombatSystem: ตัวรันสกิลพื้นที่และระยะประชิดกึ่งไกล (GroundEruptionSpike, ShockwaveExpandingRing, DashBlinkStrike, ZoneAoEPool)
// - ใช้ Physics.OverlapSphereNonAlloc (Zero-GC) และคืน Pool ผ่าน UniversalVFXManager
// - ShockwaveExpandingRing คำนวณวงแหวนกลวง (innerR..outerR) เพื่อให้ผู้เล่นกระโดดหลบได้จริง
public class CombatAreaSkillExecutor : MonoBehaviour
{
    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();

    private CombatSkillPattern_SO activePattern;
    private GameObject casterObj;
    private Transform casterTransform;
    private CharacterController casterController;
    private bool isEnemySkill;
    private LayerMask targetLayers;
    private float baseDamage;

    private bool isRunning;
    private float elapsed;
    private float tickTimer;
    private int eruptionIndex;
    private Vector3 startPos;
    private Vector3 forwardDir;
    private PooledVFXInstance activeZoneVfx;
    private Action<CombatAreaSkillExecutor> returnToPoolCallback;

    public void Launch(
        CombatSkillPattern_SO pattern,
        Vector3 origin,
        Quaternion rotation,
        GameObject caster,
        Transform target,
        float baseAtk,
        bool firedByEnemy,
        Action<CombatAreaSkillExecutor> onReturnToPool)
    {
        activePattern = pattern;
        casterObj = caster;
        casterTransform = caster != null ? caster.transform : null;
        casterController = caster != null ? caster.GetComponent<CharacterController>() : null;
        isEnemySkill = firedByEnemy;
        baseDamage = Mathf.Max(1f, baseAtk * pattern.directDamageMultiplier);
        returnToPoolCallback = onReturnToPool;

        ResolveTargetLayers();

        startPos = VFXAnchorRig.SnapToGroundSurface(origin, 0.05f);
        forwardDir = (rotation * Vector3.forward);
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;
        forwardDir.Normalize();

        transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(forwardDir));

        elapsed = 0f;
        tickTimer = 0f;
        eruptionIndex = 0;
        hitSet.Clear();
        isRunning = true;

        // Zone AoE Pool spawns persistent ground visual once
        if (pattern.patternPreset == CombatPatternPreset.ZoneAoEPool)
        {
            Vector3 poolPos = target != null ? VFXAnchorRig.SnapToGroundSurface(target.position, 0.05f) : startPos;
            transform.position = poolPos;
            startPos = poolPos;

            if (pattern.mainBodyVFXPrefab != null)
            {
                activeZoneVfx = UniversalVFXManager.SpawnPooledVFX(
                    pattern.mainBodyVFXPrefab,
                    poolPos,
                    Quaternion.identity,
                    Mathf.Max(0.2f, pattern.visualScale),
                    VFXFollowMode.FollowPositionOnly,
                    transform,
                    transform,
                    Vector3.zero,
                    Quaternion.identity,
                    false,
                    Mathf.Max(0.5f, pattern.activeDuration + 0.15f),
                    "ZoneAoEPool",
                    true);
            }
            else
            {
                UniversalVFXManager.SpawnAtPoint(VFXEventType.GroundShockwave, poolPos, Quaternion.identity, null, pattern.themeColor, pattern.visualScale * 1.4f, pattern.useWhiteBlueLightningVFX, true);
            }
        }
        else if (pattern.patternPreset == CombatPatternPreset.DashBlinkStrike)
        {
            ExecuteDashBlinkStrikeImmediate();
            FinishAndReturnToPool();
        }
    }

    private void Update()
    {
        if (!isRunning || activePattern == null) return;

        float dt = Time.deltaTime;
        elapsed += dt;

        switch (activePattern.patternPreset)
        {
            case CombatPatternPreset.GroundEruptionSpike:
                UpdateGroundEruption(dt);
                break;

            case CombatPatternPreset.ShockwaveExpandingRing:
                UpdateShockwaveRing();
                break;

            case CombatPatternPreset.ZoneAoEPool:
                UpdateZoneAoEPool(dt);
                break;

            default:
                FinishAndReturnToPool();
                break;
        }
    }

    // 1. Ground Eruption / Spike: ผุดขึ้นจากพื้นไล่เป็นเส้นตรงทีละจุด
    private void UpdateGroundEruption(float dt)
    {
        tickTimer -= dt;
        if (tickTimer <= 0f && eruptionIndex < activePattern.count)
        {
            tickTimer = Mathf.Max(0.04f, activePattern.sequenceInterval);
            float spacing = Mathf.Max(1.2f, activePattern.formationRadius);
            Vector3 pillarPos = VFXAnchorRig.SnapToGroundSurface(startPos + forwardDir * (spacing * (eruptionIndex + 1)), 0.05f);

            UniversalVFXManager.SpawnAtPoint(
                VFXEventType.GroundShockwave,
                pillarPos,
                Quaternion.LookRotation(forwardDir),
                activePattern.impactVFXPrefab != null ? activePattern.impactVFXPrefab : activePattern.mainBodyVFXPrefab,
                activePattern.themeColor,
                activePattern.visualScale,
                activePattern.useWhiteBlueLightningVFX,
                true);

            DealSphereDamage(pillarPos, Mathf.Max(1.0f, activePattern.splashRadius), baseDamage, false);
            eruptionIndex++;
        }

        if (eruptionIndex >= activePattern.count)
        {
            FinishAndReturnToPool();
        }
    }

    // 2. Shockwave / Expanding Ring: วงคลื่นกลวงขยายออกรอบทิศ (กระโดดข้ามได้!)
    private void UpdateShockwaveRing()
    {
        float duration = Mathf.Max(0.25f, activePattern.activeDuration);
        float t = Mathf.Clamp01(elapsed / duration);
        float outerRadius = Mathf.Lerp(0.6f, Mathf.Max(2.0f, activePattern.areaMaxRadius), t);
        float innerRadius = Mathf.Max(0f, outerRadius - Mathf.Max(0.4f, activePattern.ringThickness));

        if (elapsed <= Time.deltaTime * 1.5f)
        {
            UniversalVFXManager.SpawnAtPoint(
                VFXEventType.GroundShockwave,
                startPos,
                Quaternion.identity,
                activePattern.splashWaveVFXPrefab != null ? activePattern.splashWaveVFXPrefab : activePattern.mainBodyVFXPrefab,
                activePattern.themeColor,
                activePattern.visualScale * (activePattern.areaMaxRadius * 0.25f),
                activePattern.useWhiteBlueLightningVFX,
                true);
        }

        int count = Physics.OverlapSphereNonAlloc(startPos, outerRadius, overlapBuffer, targetLayers);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || IsFriendly(col)) continue;

            Vector3 targetPos = col.bounds.center;
            float heightDiff = Mathf.Abs(targetPos.y - startPos.y);
            if (heightDiff > 1.35f) continue; // Allow jumping over ground shockwave ring

            Vector3 flatDelta = targetPos - startPos;
            flatDelta.y = 0f;
            float flatDist = flatDelta.magnitude;
            if (flatDist < innerRadius || flatDist > outerRadius) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead && hitSet.Add(dmg))
            {
                dmg.TakeDamage(baseDamage, col.ClosestPoint(startPos));
            }
        }

        if (t >= 1f)
        {
            FinishAndReturnToPool();
        }
    }

    // 3. Dash / Blink Strike: พุ่งผ่านเป้าหมายอย่างรวดเร็วพร้อมรอยฟันทำดาเมจตลอดเส้นทาง
    private void ExecuteDashBlinkStrikeImmediate()
    {
        if (casterTransform == null) return;

        Vector3 dashStart = casterTransform.position;
        float dist = Mathf.Max(2.0f, activePattern.dashDistance);

        UniversalVFXManager.SpawnAtPoint(
            VFXEventType.MuzzleCast,
            dashStart + Vector3.up * 0.9f,
            Quaternion.LookRotation(forwardDir),
            activePattern.muzzleCastVFXPrefab,
            activePattern.themeColor,
            activePattern.visualScale,
            activePattern.useWhiteBlueLightningVFX,
            false);

        if (casterController != null && casterController.enabled)
        {
            casterController.Move(forwardDir * dist);
        }
        else
        {
            casterTransform.position = dashStart + forwardDir * dist;
        }

        Vector3 dashEnd = casterTransform.position;
        Vector3 midPoint = (dashStart + dashEnd) * 0.5f;
        float halfLen = Vector3.Distance(dashStart, dashEnd) * 0.5f + 0.8f;

        DealSphereDamage(midPoint + Vector3.up * 0.9f, Mathf.Max(halfLen, activePattern.splashRadius), baseDamage, true);

        UniversalVFXManager.SpawnAtPoint(
            VFXEventType.SkillAoE,
            dashEnd + Vector3.up * 0.9f,
            Quaternion.LookRotation(forwardDir),
            activePattern.impactVFXPrefab,
            activePattern.themeColor,
            activePattern.visualScale,
            activePattern.useWhiteBlueLightningVFX,
            false);
    }

    // 4. Zone / AoE Pool: อาณาเขตพื้นทำดาเมจต่อเนื่องตาม tickInterval
    private void UpdateZoneAoEPool(float dt)
    {
        tickTimer -= dt;
        if (tickTimer <= 0f)
        {
            tickTimer = Mathf.Max(0.1f, activePattern.damageTickInterval);
            float tickDmg = Mathf.Max(1f, baseDamage * Mathf.Clamp(activePattern.damageTickInterval, 0.15f, 1.0f));
            hitSet.Clear();
            DealSphereDamage(startPos, Mathf.Max(1.5f, activePattern.areaMaxRadius), tickDmg, false);
        }

        if (elapsed >= Mathf.Max(0.5f, activePattern.activeDuration))
        {
            FinishAndReturnToPool();
        }
    }

    private void DealSphereDamage(Vector3 center, float radius, float dmgAmount, bool oncePerCast)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, targetLayers);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || IsFriendly(col)) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                if (!oncePerCast || hitSet.Add(dmg))
                {
                    dmg.TakeDamage(dmgAmount, col.ClosestPoint(center));
                }
            }
        }
    }

    private bool IsFriendly(Collider col)
    {
        if (casterObj != null && (col.gameObject == casterObj || col.transform.root == casterObj.transform.root))
            return true;
        if (!isEnemySkill && col.GetComponentInParent<PlayerHealth>() != null) return true;
        if (isEnemySkill && col.GetComponentInParent<EnemyController>() != null) return true;
        return false;
    }

    private void ResolveTargetLayers()
    {
        if (isEnemySkill)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            targetLayers = (playerLayer != -1) ? (1 << playerLayer) : ~LayerMask.GetMask("Enemy", "Ignore Raycast");
        }
        else
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            targetLayers = (enemyLayer != -1) ? (1 << enemyLayer) : ~LayerMask.GetMask("Player", "Ignore Raycast");
        }
    }

    private void FinishAndReturnToPool()
    {
        isRunning = false;
        if (activeZoneVfx != null)
        {
            activeZoneVfx.ForceStopAndReturn();
            activeZoneVfx = null;
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
        if (activeZoneVfx != null)
        {
            activeZoneVfx.ForceStopAndReturn();
            activeZoneVfx = null;
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ CombatAreaSkillExecutor.cs (CombatSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. รองรับสกิลพื้นที่และระยะประชิดกึ่งไกล 4 รูปแบบหลัก:
 *    - GroundEruptionSpike    : เสกเสาพลัง/หนามผุดไล่ตามพื้นเป็นแนวเส้นตรงทีละจุด
 *    - ShockwaveExpandingRing : วงคลื่นกลวงขยายออกรอบตัว (เช็กความสูงเพื่อให้กระโดดข้ามได้จริง)
 *    - DashBlinkStrike        : พุ่งผ่านเป้าหมายความเร็วสูงพร้อมทำดาเมจและปล่อย VFX ตลอดเส้นทาง
 *    - ZoneAoEPool            : บ่ออาณาเขตบนพื้นทำดาเมจต่อเนื่องตาม tickInterval
 *
 * 2. Zero-GC & Pooling:
 *    - ใช้ OverlapSphereNonAlloc กับ overlapBuffer[32] และคืนตัวกลับเข้า Pool ของ UniversalVFXManager
 * ============================================================================ */
