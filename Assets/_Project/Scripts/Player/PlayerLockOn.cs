using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ระบบล็อกเป้าหมาย (Lock-On System) แบบ Smart Camera Viewport & Swarm Defense
/// แก้ไขปัญหาการโดนรุม:
/// 1. Camera Viewport Detection: คัดกรองศัตรูที่ปรากฏบนจอภาพจริง (Viewport Bounds)
/// 2. Close-Range Swarm Priority (360 องศา): หากมีศัตรูประชิดตัวรอบทิศทางในระยะอันตราย (Close Threat Radius)
///    จะสามารถล็อกได้ทันทีแม้จะอยู่ด้านหลัง โดยไม่ต้องเสียเวลาหมุนกล้องก่อน
/// 3. Smart Target Weighting: ให้คะแนนลำดับความสำคัญเป้าหมาย (ระยะประชิดเร่งด่วน + ตำแหน่งกึ่งกลางจอ)
/// 4. 360 Target Cycling: สลับเป้าหมายรอบตัวผู้เล่นได้อย่างลื่นไหลตอนโดนรุม
/// </summary>
public class PlayerLockOn : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("ระยะตรวจจับสูงสุดของระบบล็อกเป้า")]
    [SerializeField] private float lockOnRadius = 16f;

    [Tooltip("ระยะประชิดรอบตัว 360 องศา (แก้ปัญหาตอนโดนรุม: ล็อกศัตรูข้างหลัง/ข้างๆ ได้ทันทีโดยไม่ต้องหันกล้อง)")]
    [SerializeField] private float closeThreatRadius = 4.5f;

    [Tooltip("ขอบจอผ่อนปรน (Viewport Padding) เพื่อให้จับศัตรูที่อยู่เฉียดๆ ขอบจอได้")]
    [Range(0f, 0.3f)]
    [SerializeField] private float viewportMargin = 0.1f;

    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float targetHeightOffset = 1.2f;

    [Header("Tracking & Rotation Smoothing")]
    [SerializeField] private float playerRotateSmoothing = 12f;
    [SerializeField] private float cameraFacingSmoothing = 8f;

    [Header("UI Marker (World to Screen)")]
    [SerializeField] private RectTransform lockOnUIMarker;

    [Header("Cinemachine 3 Integration (Optional)")]
    [SerializeField] private CinemachineCamera freeLookCamera;
    [SerializeField] private CinemachineCamera lockOnCamera;
    [SerializeField] private CinemachineTargetGroup targetGroup;

    private Transform currentTarget;
    private PlayerMovement playerMovement;
    private Camera mainCamera;

    private readonly List<Transform> detectedTargets = new List<Transform>();
    private readonly Collider[] overlapBuffer = new Collider[32];
    private int currentTargetIndex = 0;
    private float switchCooldown = 0.2f;
    private float switchTimer = 0f;

    public bool IsLockedOn => currentTarget != null;
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        mainCamera = Camera.main;

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.LockOn.performed += OnLockOnInput;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.LockOn.performed -= OnLockOnInput;
        }
    }

    private void Update()
    {
        if (switchTimer > 0f)
            switchTimer -= Time.deltaTime;

        // ปุ่ม Fallback: [E] สำหรับทดสอบ
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            ToggleLockOn();
        }

        if (IsLockedOn)
        {
            if (ShouldUnlockTarget())
            {
                FindNextOrUnlock();
                return;
            }

            // อัปเดตตำแหน่งมาร์กเกอร์ UI
            UpdateUIMarkerPosition();

            // ตัวละครหันหน้าหาเป้าหมาย
            RotatePlayerTowardsTarget();

            // ปรับมุมกล้อง
            UpdateCameraFacing();

            // สลับเป้าหมายด้วย Scroll Wheel
            HandleTargetSwitching();
        }
    }

    private void OnLockOnInput(InputAction.CallbackContext context)
    {
        ToggleLockOn();
    }

    public void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            UnlockTarget();
        }
        else
        {
            FindAndLockBestTarget();
        }
    }

    public void FindAndLockBestTarget()
    {
        ScanCandidates();

        if (detectedTargets.Count == 0)
        {
            UnlockTarget();
            return;
        }

        // เรียงลำดับตามคะแนนความเร่งด่วน (คะแนนน้อย = ความสำคัญสูงสุด)
        detectedTargets.Sort((a, b) =>
        {
            float scoreA = EvaluateTargetScore(a);
            float scoreB = EvaluateTargetScore(b);
            return scoreA.CompareTo(scoreB);
        });

        currentTargetIndex = 0;
        LockOnto(detectedTargets[0]);
    }

    /// <summary>
    /// คำนวณคะแนนความสำคัญ:
    /// - ศัตรูระยะประชิด (โดนรุม) ได้ความสำคัญอันดับ 1
    /// - ศัตรูในจอภาพ จะคำนวณจากระยะห่าง + ความใกล้จุดศูนย์กลางจอ
    /// </summary>
    private float EvaluateTargetScore(Transform enemy)
    {
        if (enemy == null) return float.MaxValue;

        float distToPlayer = Vector3.Distance(transform.position, enemy.position);

        // กรณีศัตรูอยู่ในระยะประชิดรอบตัว 360 องศา (โดนรุม)
        if (distToPlayer <= closeThreatRadius)
        {
            return distToPlayer; // ยิ่งชิดตัว ยิ่งได้คะแนนต่ำ (ถูกเลือกก่อน)
        }

        // กรณีศัตรูอยู่ในสายตากล้อง
        if (mainCamera != null)
        {
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(enemy.position + Vector3.up * targetHeightOffset);
            if (viewportPos.z > 0)
            {
                float distFromCenter = Vector2.Distance(new Vector2(viewportPos.x, viewportPos.y), new Vector2(0.5f, 0.5f));
                return 10f + distToPlayer + (distFromCenter * 15f);
            }
        }

        return 50f + distToPlayer;
    }

    private void ScanCandidates()
    {
        detectedTargets.Clear();
        if (mainCamera == null) mainCamera = Camera.main;

        int count = Physics.OverlapSphereNonAlloc(transform.position, lockOnRadius, overlapBuffer, enemyLayers);

        for (int i = 0; i < count; i++)
        {
            Transform enemy = overlapBuffer[i].transform;

            if (enemy.TryGetComponent<IDamageable>(out var dmg) && dmg.IsDead)
                continue;

            float distToPlayer = Vector3.Distance(transform.position, enemy.position);
            Vector3 targetCenter = enemy.position + Vector3.up * targetHeightOffset;

            bool isEligible = false;

            // 1. เช็คระยะประชิดรอบตัว 360 องศา (ช่วยตอนโดนรุม)
            if (distToPlayer <= closeThreatRadius)
            {
                isEligible = true;
            }
            // 2. เช็คว่าอยู่ในระยะสายตาหน้าจอจริง (Viewport Bounds Check)
            else if (mainCamera != null)
            {
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetCenter);
                if (viewportPos.z > 0)
                {
                    if (viewportPos.x >= -viewportMargin && viewportPos.x <= 1f + viewportMargin &&
                        viewportPos.y >= -viewportMargin && viewportPos.y <= 1f + viewportMargin)
                    {
                        isEligible = true;
                    }
                }
            }

            if (!isEligible) continue;

            // 3. เช็คสิ่งกีดขวาง (Raycast ป้องกันการล็อกทะลุกำแพงหนา)
            Vector3 rayOrigin = transform.position + Vector3.up * targetHeightOffset;
            Vector3 toTarget = targetCenter - rayOrigin;

            if (!Physics.Raycast(rayOrigin, toTarget.normalized, toTarget.magnitude, obstacleLayers))
            {
                if (!detectedTargets.Contains(enemy))
                {
                    detectedTargets.Add(enemy);
                }
            }
        }
    }

    private void LockOnto(Transform target)
    {
        currentTarget = target;

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.gameObject.SetActive(true);
            UpdateUIMarkerPosition();
        }

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
            targetGroup.AddMember(transform, 1f, 1.5f);
            targetGroup.AddMember(currentTarget, 1f, 2.0f);
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(true);
        }

        Debug.Log($"<color=cyan>[Lock-On] Locked: {currentTarget.name}</color>");
    }

    public void UnlockTarget()
    {
        currentTarget = null;

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.gameObject.SetActive(false);
        }

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }

        Debug.Log("<color=yellow>[Lock-On] Unlocked</color>");
    }

    private bool ShouldUnlockTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy) return true;

        if (currentTarget.TryGetComponent<IDamageable>(out var dmg) && dmg.IsDead) return true;

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        if (distance > lockOnRadius * 1.35f) return true;

        return false;
    }

    private void FindNextOrUnlock()
    {
        Transform oldTarget = currentTarget;
        ScanCandidates();
        detectedTargets.Remove(oldTarget);

        if (detectedTargets.Count > 0)
        {
            detectedTargets.Sort((a, b) =>
            {
                float scoreA = EvaluateTargetScore(a);
                float scoreB = EvaluateTargetScore(b);
                return scoreA.CompareTo(scoreB);
            });

            currentTargetIndex = 0;
            LockOnto(detectedTargets[0]);
        }
        else
        {
            UnlockTarget();
        }
    }

    private void UpdateUIMarkerPosition()
    {
        if (lockOnUIMarker == null || currentTarget == null || mainCamera == null) return;

        Vector3 targetWorldPos = currentTarget.position + Vector3.up * targetHeightOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetWorldPos);

        if (screenPos.z > 0)
        {
            lockOnUIMarker.gameObject.SetActive(true);
            lockOnUIMarker.position = screenPos;
        }
        else
        {
            // ซ่อนมาร์กเกอร์เมื่อเป้าหมายหลุดไปด้านหลังกล้อง
            lockOnUIMarker.gameObject.SetActive(false);
        }
    }

    private void RotatePlayerTowardsTarget()
    {
        Vector3 direction = (currentTarget.position - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, playerRotateSmoothing * Time.deltaTime);
        }
    }

    private void UpdateCameraFacing()
    {
        if (mainCamera == null || lockOnCamera != null) return;

        Vector3 dir = (currentTarget.position + Vector3.up * 1.2f) - mainCamera.transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, lookRot, cameraFacingSmoothing * Time.deltaTime);
        }
    }

    private void HandleTargetSwitching()
    {
        if (Mouse.current == null || switchTimer > 0f) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 15f)
        {
            ScanCandidates();
            if (detectedTargets.Count <= 1) return;

            // เรียงลำดับเป้าหมายรอบตัว 360 องศาตามเข็มนาฬิกา
            detectedTargets.Sort((a, b) =>
            {
                Vector3 dirA = a.position - transform.position;
                Vector3 dirB = b.position - transform.position;
                float angleA = Mathf.Atan2(dirA.x, dirA.z) * Mathf.Rad2Deg;
                float angleB = Mathf.Atan2(dirB.x, dirB.z) * Mathf.Rad2Deg;
                return angleA.CompareTo(angleB);
            });

            int currentIndex = detectedTargets.IndexOf(currentTarget);
            if (currentIndex == -1) currentIndex = 0;

            int newIndex = scroll > 0f
                ? (currentIndex + 1) % detectedTargets.Count
                : (currentIndex - 1 + detectedTargets.Count) % detectedTargets.Count;

            switchTimer = switchCooldown;
            LockOnto(detectedTargets[newIndex]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // วงกลมสีฟ้า: ระยะตรวจจับทั่วไปบนจอภาพ
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);

        // วงกลมสีแดง: ระยะประชิดรอบตัว 360 องศา (Swarm Defense)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, closeThreatRadius);
    }
}