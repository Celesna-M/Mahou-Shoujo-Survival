using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// แนบสคริปต์นี้ไว้ที่ Root GameObject ของโมเดล 5 แยกชิบูย่าใน Unity 3D
/// แล้วคลิกขวาที่ชื่อคอมโพเนนต์ เลือก "Setup Shibuya Arena Colliders (ตั้งค่ากำแพงกั้นเดินออกอัตโนมัติ)"
/// สคริปต์จะใส่ MeshCollider ให้กับกลุ่ม GRP_Unity_Arena_Colliders และซ่อน MeshRenderer ของกำแพงให้มองไม่เห็นทันที!
/// </summary>
public class ShibuyaArenaColliderSetup : MonoBehaviour
{
    [Header("Unity Arena Boundary Settings")]
    [Tooltip("ซ่อนกำแพงกั้นเดินออก (UCX_Wall_*) ไม่ให้แสดงผลในเกม แต่ยังชนได้ปกติ")]
    public bool hideBoundaryWallsInGame = true;

    [Tooltip("เปิดใช้งาน Collider พื้นเรียบตรงกลางแยก (UCX_Floor_CenterArena)")]
    public bool useSmoothCenterFloorCollider = true;

    private void Awake()
    {
        ApplyArenaColliders();
    }

    [ContextMenu("Setup Shibuya Arena Colliders (ตั้งค่ากำแพงกั้นเดินออกอัตโนมัติ)")]
    public void ApplyArenaColliders()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        int colliderCount = 0;

        foreach (Transform child in allChildren)
        {
            if (child.name.StartsWith("UCX_Wall_") || child.name.StartsWith("UCX_Floor_"))
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        mc = child.gameObject.AddComponent<MeshCollider>();
                    }
                    mc.sharedMesh = mf.sharedMesh;
                    colliderCount++;
                }

                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (child.name.StartsWith("UCX_Wall_"))
                    {
                        mr.enabled = !hideBoundaryWallsInGame;
                    }
                    else if (child.name.StartsWith("UCX_Floor_"))
                    {
                        mr.enabled = false;
                        child.gameObject.SetActive(useSmoothCenterFloorCollider);
                    }
                }
            }
            else if (child.name.StartsWith("Bld_") || child.name.StartsWith("Sidewalk_") || child.name.StartsWith("Prop_06_Arena_Roadblock"))
            {
                // Add static MeshCollider to surrounding buildings, sidewalks, and roadblocks
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && child.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = child.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        Debug.Log($"[ShibuyaArenaColliderSetup] ตั้งค่ากำแพง Collider กั้นเดินออกรอบ 5 แยกสำเร็จ ({colliderCount} ชิ้น)!");
    }
}
