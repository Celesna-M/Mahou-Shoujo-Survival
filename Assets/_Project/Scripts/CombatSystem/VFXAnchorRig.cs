using UnityEngine;

public enum VFXSpawnAnchor
{
    Muzzle = 0,       // จุดยิงกระสุน/ปล่อยเวท (หันตรงตามตัวละครเสมอ ไม่เบี้ยวตามข้อมือ)
    MeleeSwing = 1,   // จุดปะทะระยะประชิดด้านหน้า
    CenterBody = 2,   // กึ่งกลางลำตัว/หน้าอก (สำหรับ Hit Impact ไม่ให้จมลงเท้า)
    GroundFeet = 3,   // พื้นใต้เท้าจริง (ยิง Raycast แนบพื้น + 0.05m สำหรับ AoE/Shockwave)
    Overhead = 4,     // เหนือศีรษะ (Stun / Phase 2 / Buff)
    RightHand = 5,    // มือขวา (ดึงจาก Humanoid Bone อัตโนมัติ)
    LeftHand = 6      // มือซ้าย (ดึงจาก Humanoid Bone อัตโนมัติ)
}

/// <summary>
/// CombatSystem: ตัวจัดการจุดเกิด VFX สากล (ใช้ได้กับทุกตัวละครและมอนสเตอร์ทุกตัว)
/// - แก้ปัญหา VFX ลอยกลางอากาศ, จมใต้พื้น, หรือยิงเบี้ยวตามองศาข้อมือตอนเล่นแอนิเมชัน
/// - แสดงจุด Gizmos สีแยกชัดเจนใน Scene View เพื่อให้ขยับปรับตำแหน่งใน Inspector ได้แม่นยำระดับมิลลิเมตร
/// </summary>
public class VFXAnchorRig : MonoBehaviour
{
    [Header("1. Optional Custom Anchor Transforms (ลากจุดวางเองได้ หรือเว้นว่างเพื่อใช้ Offset อัตโนมัติ)")]
    [SerializeField] private Transform customMuzzlePoint;
    [SerializeField] private Transform customMeleePoint;
    [SerializeField] private Transform customCenterPoint;
    [SerializeField] private Transform customGroundPoint;

    [Header("2. Universal Local Offsets (ปรับระยะจุดเกิด VFX เทียบกับตัวละคร)")]
    [Tooltip("จุดปล่อยกระสุน/Muzzle VFX (X=ซ้ายขวา, Y=สูงจากพื้น, Z=ยื่นไปข้างหน้า)")]
    [SerializeField] private Vector3 muzzleLocalOffset = new Vector3(0f, 1.25f, 0.85f);
    [Tooltip("จุดออกท่าโจมตีประชิด/สกิลด้านหน้า")]
    [SerializeField] private Vector3 meleeLocalOffset = new Vector3(0f, 1.05f, 1.15f);
    [Tooltip("ความสูงจุดกึ่งกลางอก (สำหรับ Hit Impact VFX)")]
    [SerializeField] private Vector3 centerBodyLocalOffset = new Vector3(0f, 1.05f, 0f);
    [Tooltip("ความสูงจุดเหนือศีรษะ")]
    [SerializeField] private Vector3 overheadLocalOffset = new Vector3(0f, 2.15f, 0f);
    [Tooltip("ระยะยกจากพื้นเล็กน้อยสำหรับ Ground AoE VFX เพื่อไม่ให้ซ้อนทับพื้นจนกะพริบ (Z-Fighting)")]
    [SerializeField] private float groundSurfaceLift = 0.05f;
    [SerializeField] private LayerMask groundRaycastLayers = ~0;

    private Animator cachedAnimator;
    private Transform rightHandBone;
    private Transform leftHandBone;

    private void Awake()
    {
        RefreshBoneAnchors();
    }

    /// <summary>
    /// เรียกซ้ำได้ทันทีเมื่อ PlayerCharacterLoader สวมโมเดล 3D ตัวใหม่ลงบน Player_Root
    /// </summary>
    public void RefreshBoneAnchors(Animator overrideAnimator = null)
    {
        cachedAnimator = overrideAnimator != null ? overrideAnimator : GetComponentInChildren<Animator>(true);
        rightHandBone = null;
        leftHandBone = null;

        if (cachedAnimator != null && cachedAnimator.isHuman)
        {
            rightHandBone = cachedAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHandBone = cachedAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        }
    }

    public Vector3 GetAnchorPosition(VFXSpawnAnchor anchor)
    {
        switch (anchor)
        {
            case VFXSpawnAnchor.Muzzle:
                if (customMuzzlePoint != null) return customMuzzlePoint.position;
                if (rightHandBone != null)
                {
                    // ใช้ความสูงของมือขวา แต่จัดแนว Z ให้อยู่ด้านหน้าตัวละครเพื่อไม่ให้กระสุนยิงทะลุตัวหรือเบี้ยว
                    Vector3 handPos = rightHandBone.position;
                    Vector3 safeFront = transform.TransformPoint(muzzleLocalOffset);
                    return Vector3.Lerp(handPos, safeFront, 0.45f);
                }
                return transform.TransformPoint(muzzleLocalOffset);

            case VFXSpawnAnchor.MeleeSwing:
                return customMeleePoint != null ? customMeleePoint.position : transform.TransformPoint(meleeLocalOffset);

            case VFXSpawnAnchor.CenterBody:
                return customCenterPoint != null ? customCenterPoint.position : transform.TransformPoint(centerBodyLocalOffset);

            case VFXSpawnAnchor.GroundFeet:
                Vector3 basePos = customGroundPoint != null ? customGroundPoint.position : transform.position;
                return SnapToGroundSurface(basePos, groundSurfaceLift, groundRaycastLayers);

            case VFXSpawnAnchor.Overhead:
                return transform.TransformPoint(overheadLocalOffset);

            case VFXSpawnAnchor.RightHand:
                return rightHandBone != null ? rightHandBone.position : transform.TransformPoint(muzzleLocalOffset);

            case VFXSpawnAnchor.LeftHand:
                return leftHandBone != null ? leftHandBone.position : transform.TransformPoint(new Vector3(-0.35f, muzzleLocalOffset.y, muzzleLocalOffset.z));

            default:
                return transform.TransformPoint(centerBodyLocalOffset);
        }
    }

    public Quaternion GetAnchorRotation(VFXSpawnAnchor anchor)
    {
        // ใช้แกน forward แนวราบของตัวละครเสมอ เพื่อไม่ให้ VFX ยิงทิ่มดินหรือเอียงตามการบิดข้อมือของ Animation
        Vector3 flatFwd = transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        return Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
    }

    public static Vector3 ResolvePosition(Transform owner, VFXSpawnAnchor anchor)
    {
        if (owner == null) return Vector3.zero;
        VFXAnchorRig rig = owner.GetComponent<VFXAnchorRig>() ?? owner.GetComponentInParent<VFXAnchorRig>();
        if (rig != null) return rig.GetAnchorPosition(anchor);

        switch (anchor)
        {
            case VFXSpawnAnchor.Muzzle: return owner.TransformPoint(new Vector3(0f, 1.25f, 0.85f));
            case VFXSpawnAnchor.MeleeSwing: return owner.TransformPoint(new Vector3(0f, 1.05f, 1.15f));
            case VFXSpawnAnchor.GroundFeet: return SnapToGroundSurface(owner.position, 0.05f, ~0);
            case VFXSpawnAnchor.Overhead: return owner.TransformPoint(new Vector3(0f, 2.15f, 0f));
            default: return owner.TransformPoint(new Vector3(0f, 1.05f, 0f));
        }
    }

    public static Quaternion ResolveRotation(Transform owner, VFXSpawnAnchor anchor)
    {
        if (owner == null) return Quaternion.identity;
        VFXAnchorRig rig = owner.GetComponent<VFXAnchorRig>() ?? owner.GetComponentInParent<VFXAnchorRig>();
        if (rig != null) return rig.GetAnchorRotation(anchor);

        Vector3 fwd = owner.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
    }

    public static Vector3 SnapToGroundSurface(Vector3 worldPos, float liftOffset = 0.05f, LayerMask mask = default)
    {
        int layerMask = (mask.value == 0) ? ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast") : mask.value;
        Vector3 rayOrigin = worldPos + Vector3.up * 1.2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5.0f, layerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * liftOffset;
        }
        return new Vector3(worldPos.x, worldPos.y + liftOffset, worldPos.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.TransformPoint(muzzleLocalOffset), 0.14f);

        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.TransformPoint(meleeLocalOffset), 0.16f);

        Gizmos.color = new Color(1f, 0.25f, 0.65f, 0.9f);
        Gizmos.DrawWireSphere(transform.TransformPoint(centerBodyLocalOffset), 0.15f);

        Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * groundSurfaceLift, 0.18f);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน VFXAnchorRig.cs (CombatSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. VFXSpawnAnchor (7 จุดมาตรฐานสากล)
 *    - Muzzle (จุดยิงเวท), MeleeSwing (จุดฟัน/ต่อยด้านหน้า), CenterBody (กึ่งกลางอกสำหรับ Hit Impact),
 *      GroundFeet (แนบพื้นจริงด้วย Raycast), Overhead (เหนือศีรษะ), RightHand, LeftHand
 *
 * 2. RefreshBoneAnchors(Animator overrideAnimator)
 *    - ดึงกระดูกมือซ้าย/ขวาจาก Humanoid Rig อัตโนมัติ และอัปเดตใหม่ทันทีเมื่อสวมโมเดลตัวละครใหม่
 *
 * 3. GetAnchorPosition() & GetAnchorRotation() & SnapToGroundSurface()
 *    - คำนวณพิกัดจุดเกิด VFX ให้แม่นยำ ไม่เบี้ยวตามองศาข้อมือ และแนบพื้นจริงสำหรับวง AoE / Shockwave
 * ============================================================================ */
