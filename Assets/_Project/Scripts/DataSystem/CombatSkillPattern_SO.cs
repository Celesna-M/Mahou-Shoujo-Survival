using System;
using UnityEngine;

// 1. หมวดหมู่ใหญ่ของสกิล/การโจมตี (แยก Executor 3 ตัวไม่ให้ทำงานทับกัน)
public enum SkillDeliveryCategory
{
    Projectile = 0,
    Beam = 1,
    AreaOrDash = 2
}

// 2. ตัวกรองสายตัวละคร (ป้องกันสายใกล้-สายไกลทำงานทับกัน)
[Flags]
public enum SkillArchetypeMask
{
    None = 0,
    MeleeStriker = 1 << 0,
    HeavyBreaker = 1 << 1,
    RangedCaster = 1 << 2,
    Enemy = 1 << 3,
    AllPlayers = MeleeStriker | HeavyBreaker | RangedCaster,
    Everyone = MeleeStriker | HeavyBreaker | RangedCaster | Enemy
}

// 3. รูปแบบย่อยครบทั้ง 22 รูปแบบ (11 Projectiles + 5 Beams + 6 Area/Melee-Ranged)
public enum CombatPatternPreset
{
    // --- กลุ่ม 1: Projectiles (11 รูปแบบ) ---
    AimedSnipe = 0,
    SpreadFan = 1,
    BarrageRapidFire = 2,
    RadialNova360 = 3,
    SpiralVortex = 4,
    HomingGuided = 5,
    DelayedHomingItano = 6,
    OrbitSatellite = 7,
    WaveSineZigZag = 8,
    SplitCluster = 9,
    RicochetBounce = 10,

    // --- กลุ่ม 2: Beams & Lasers (5 รูปแบบ) ---
    ContinuousChannelBeam = 20,
    TelegraphedBeam = 21,
    SweepingBeam = 22,
    CrossRotatingBeam = 23,
    OrbitalSkyBeam = 24,

    // --- กลุ่ม 3: Area & Melee-Ranged Skills (6 รูปแบบ) ---
    SlashWaveCrescent = 40,
    MeteorRain = 41,
    GroundEruptionSpike = 42,
    ShockwaveExpandingRing = 43,
    DashBlinkStrike = 44,
    ZoneAoEPool = 45
}

public enum ProjectileImpactBehavior
{
    ExplodeOnHit = 0,
    PierceTargets = 1,
    ChainBounce = 2,
    WallRicochet = 3,
    StickyDelayedDetonate = 4,
    SplitOnHit = 5
}

[CreateAssetMenu(fileName = "NewCombatSkillPattern", menuName = "StellaClash/Data/Combat Skill Pattern", order = 4)]
public class CombatSkillPattern_SO : ScriptableObject
{
    [Header("1. Identity & Non-Overlap Guard")]
    public string patternID = "PAT_01";
    public string displayName = "New Skill Pattern";
    public SkillDeliveryCategory deliveryCategory = SkillDeliveryCategory.Projectile;
    public CombatPatternPreset patternPreset = CombatPatternPreset.AimedSnipe;
    [Tooltip("กำหนดว่าตัวละครสายไหนใช้แพตเทิร์นนี้ได้ เพื่อไม่ให้สายใกล้และสายไกลทำงานทับกัน")]
    public SkillArchetypeMask allowedArchetypes = SkillArchetypeMask.Everyone;

    [Header("2. Animation & Timing")]
    [Tooltip("ชื่อ Trigger ใน Animator (เว้นว่างได้หากใช้ Attack/Skill ปกติของตัวละคร)")]
    public string animatorTriggerName = "";
    public float animatorSpeedMultiplier = 1.0f;
    [Tooltip("เวลาก่อนยิงจริง เช่น เส้นเล็งแจ้งเตือน (Telegraph) หรือจังหวะลอยชาร์จ (Delayed Homing)")]
    public float windupOrDelayDuration = 0.25f;
    [Tooltip("ระยะเวลาทำงานต่อเนื่อง (ใช้กับ Beam, Zone Pool, Orbit, Shockwave)")]
    public float activeDuration = 1.6f;
    [Tooltip("ความถี่การคิดดาเมจต่อเนื่อง (วินาทีต่อ 1 Tick สำหรับ Beam / Zone Pool)")]
    public float damageTickInterval = 0.2f;

    [Header("3. Count, Spread & Formation")]
    [Min(1)] public int count = 1;
    [Tooltip("มุมกางพัด (องศา) สำหรับ SpreadFan / SweepingBeam / SlashWave")]
    public float spreadAngle = 28f;
    [Tooltip("ระยะห่างเวลาระหว่างแต่ละนัด (วินาที) สำหรับ Barrage / Spiral / Meteor / GroundEruption")]
    public float sequenceInterval = 0.08f;
    [Tooltip("รัศมีจัดวาง (เช่น รัศมีวง Orbit, รัศมีตก Meteor Rain, หรือระยะ Eruption)")]
    public float formationRadius = 2.2f;
    [Tooltip("จำนวนแฉกของ CrossRotatingBeam (2-4 ทิศ)")]
    [Range(2, 4)] public int crossBeamArms = 4;

    [Header("4. Motion, Beam & Area Physics")]
    public float speed = 34f;
    public float acceleration = 0f;
    public float lifeTime = 3.0f;
    public float homingAngularSpeed = 240f;
    public float waveAmplitude = 1.1f;
    public float waveFrequency = 8.0f;
    public float beamRange = 18f;
    public float beamWidth = 0.75f;
    public float beamSweepDegrees = 90f;
    public float areaMaxRadius = 6.5f;
    public float ringThickness = 1.1f;
    public float dashDistance = 7.5f;

    [Header("5. Damage, Impact & Sub-Pattern")]
    public float directDamageMultiplier = 1.0f;
    public float splashDamageMultiplier = 0.55f;
    public float splashRadius = 2.6f;
    public float collisionRadius = 0.32f;
    public ProjectileImpactBehavior impactBehavior = ProjectileImpactBehavior.ExplodeOnHit;
    [Min(1)] public int maxPierceOrBounceCount = 3;
    public float chainSearchRadius = 6.5f;
    public float stickyDetonateDelay = 0.65f;
    [Tooltip("แพตเทิร์นลูกที่จะแตกตัวออกเมื่อใช้ SplitCluster / SplitOnHit (จำกัดความลึกอัตโนมัติไม่ให้เกิด Infinite Loop)")]
    public CombatSkillPattern_SO subPatternOnHit;

    [Header("6. Modular VFX & SFX (เสียบ VFX ไหนก็ได้)")]
    public MagicProjectile customProjectilePrefab;
    public GameObject telegraphWarningVFXPrefab;
    public GameObject muzzleCastVFXPrefab;
    public GameObject mainBodyVFXPrefab;
    public GameObject impactVFXPrefab;
    public GameObject splashWaveVFXPrefab;
    public bool useWhiteBlueLightningVFX = true;
    public Color themeColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
    public float visualScale = 1.0f;
    public float trailDetachDelay = 0.45f;
    public AudioClip castSfx;
    public AudioClip impactSfx;

    [Header("7. Game Feel")]
    public float hitStopDuration = 0.04f;
    public float shakeIntensity = 0.30f;

    public bool IsAllowedFor(CharacterCombatArchetype archetype, bool isEnemy)
    {
        if (isEnemy) return (allowedArchetypes & SkillArchetypeMask.Enemy) != 0;
        switch (archetype)
        {
            case CharacterCombatArchetype.MeleeStriker:
                return (allowedArchetypes & SkillArchetypeMask.MeleeStriker) != 0;
            case CharacterCombatArchetype.HeavyBreaker:
                return (allowedArchetypes & SkillArchetypeMask.HeavyBreaker) != 0;
            case CharacterCombatArchetype.RangedCaster:
                return (allowedArchetypes & SkillArchetypeMask.RangedCaster) != 0;
            default:
                return true;
        }
    }

    public SkillDeliveryCategory ResolveEffectiveCategory()
    {
        switch (patternPreset)
        {
            case CombatPatternPreset.ContinuousChannelBeam:
            case CombatPatternPreset.TelegraphedBeam:
            case CombatPatternPreset.SweepingBeam:
            case CombatPatternPreset.CrossRotatingBeam:
            case CombatPatternPreset.OrbitalSkyBeam:
                return SkillDeliveryCategory.Beam;

            case CombatPatternPreset.GroundEruptionSpike:
            case CombatPatternPreset.ShockwaveExpandingRing:
            case CombatPatternPreset.DashBlinkStrike:
            case CombatPatternPreset.ZoneAoEPool:
                return SkillDeliveryCategory.AreaOrDash;

            default:
                return SkillDeliveryCategory.Projectile;
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ CombatSkillPattern_SO.cs (DataSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. CombatSkillPattern_SO:
 *    - เป็น ScriptableObject กลางสำหรับออกแบบท่าโจมตีและสกิลได้ครบทั้ง 22 รูปแบบยอดนิยม
 *      (11 Projectiles, 5 Beams, 6 Area/Melee-Ranged) โดยไม่ต้องเขียนโค้ดใหม่ทุกครั้ง
 *    - แค่คลิกขวา Create > StellaClash > Data > Combat Skill Pattern แล้วเลือก patternPreset
 *      พร้อมลาก Anim Trigger, VFX (Telegraph/Muzzle/Body/Impact) และ SFX มาเสียบได้ทันที
 *
 * 2. IsAllowedFor(...) & ResolveEffectiveCategory():
 *    - ตรวจสิทธิ์ allowedArchetypes เพื่อไม่ให้สกิลสายใกล้และสายไกลทำงานทับกัน
 *    - แปลง patternPreset ไปยัง Executor ที่ถูกต้อง (Projectile / Beam / AreaOrDash) อัตโนมัติ
 * ============================================================================ */
