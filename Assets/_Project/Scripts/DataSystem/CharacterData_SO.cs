using UnityEngine;

// DataSystem: ScriptableObject เก็บข้อมูลตัวละครแต่ละตัว (Unified Character + Skill Pattern Architecture)
[CreateAssetMenu(fileName = "NewCharacterData", menuName = "StellaClash/Data/Character Data", order = 1)]
public class CharacterData_SO : ScriptableObject
{
    [Header("1. Identity & Combat Archetype")]
    public string characterID = "CHAR_01";
    public string characterDisplayName = "HIKARI";
    public CharacterCombatArchetype combatArchetype = CharacterCombatArchetype.MeleeStriker;
    [TextArea(2, 3)]
    public string roleDescription = "Balanced Striker | Fast melee combos & responsive dodge cancel.";

    [Header("2. Pure 3D Visual Model & Animator")]
    [Tooltip("ลาก Prefab โมเดล 3D ที่มีแค่ Mesh + Animator มาใส่ได้เลย PlayerCharacterLoader จะสวมเข้ากับ Player_Root ให้อัตโนมัติ")]
    public GameObject characterPrefab;
    [Tooltip("AnimatorController หรือ AnimatorOverrideController เฉพาะตัวละครนี้")]
    public RuntimeAnimatorController animatorControllerOverride;
    public Vector3 modelLocalOffset = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Header("3. Visual Novel Portraits & Theme")]
    public Sprite vnPortraitNormal;
    public Sprite vnPortraitAngry;
    public Color themeColor = new Color(0.92f, 0.28f, 0.58f, 1f);

    [Header("4. Base Combat Stats")]
    public float baseMaxHP = 100f;
    public float baseATK = 25f;
    public float moveSpeed = 5.2f;
    public float skillCooldown = 5.0f;
    public float skillDamageMultiplier = 1.85f;

    [Header("5. Combat Skill Patterns (Data-Driven — เสียบแพตเทิร์นได้ทั้งใกล้และไกล)")]
    [Tooltip("แพตเทิร์นสำหรับคอมโบแต่ละจังหวะ (Index 0 = ฮิต 1, 1 = ฮิต 2, 2 = ฮิต 3 — เว้นว่างได้เพื่อใช้โหมดปกติ)")]
    public CombatSkillPattern_SO[] comboSkillPatterns;
    [Tooltip("แพตเทิร์นสำหรับ Active Skill (ปุ่ม E / F) — รองรับทั้ง Projectile, Beam และ Area/Dash")]
    public CombatSkillPattern_SO activeSkillPattern;

    [Header("6. Character VFX Setup (Legacy / Fallback Compatible)")]
    [Tooltip("ตาราง VFX Animation Event ประจำตัวละคร (ใช้กับ PlayVFX(\"Key\") ใน Animation Clip)")]
    public AnimVFXLibrary_SO animVFXLibrary;
    [Tooltip("ติ๊กเปิดเพื่อใช้เอฟเฟกต์ 3D สายฟ้าสีขาว-น้ำเงินจาก UniversalVFXManager ตอนยิงและตอนระเบิดอัตโนมัติ")]
    public bool useWhiteBlueLightningVFX = false;
    [Tooltip("VFX เฉพาะตัวตอนยิง/ปล่อยสกิล")]
    public GameObject customMuzzleVFXPrefab;
    public GameObject customImpactVFXPrefab;
    public GameObject customActiveSkillVFXPrefab;
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดใน CharacterData_SO.cs (DataSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. การอัปเกรดเพิ่มระบบ Pattern:
 *    - เพิ่มช่อง comboSkillPatterns[] (ตั้งแพตเทิร์นเฉพาะให้ฮิต 1, 2, 3 แยกกันได้ เช่น ฮิต 3 ของสายใกล้ปล่อย SlashWaveCrescent)
 *    - เพิ่มช่อง activeSkillPattern สำหรับผูกท่าสกิลหลักทั้ง 22 รูปแบบ (Projectile / Beam / AreaOrDash)
 *    - คงฟิลด์เดิมทุกตัวไว้ 100% เพื่อให้ Asset ตัวละครเก่าและ PlayerCharacterLoader ใช้งานต่อได้ทันที
 * ============================================================================ */
