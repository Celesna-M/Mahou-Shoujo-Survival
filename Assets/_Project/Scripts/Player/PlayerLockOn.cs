using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockOn : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float noticeRadius = 15f; // ระยะตรวจจับศัตรู
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private Transform cameraTransform;

    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera freeLookCamera;
    [SerializeField] private CinemachineCamera lockOnCamera;

    [Header("Lock-On Marker (Optional)")]
    [SerializeField] private GameObject lockOnMarkerPrefab;

    private PlayerMovement playerMovement;
    private Transform currentTarget;
    private GameObject activeMarker;

    public bool IsLockedOn => currentTarget != null;
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Start()
    {
        playerMovement.InputActions.Player.LockOn.performed += OnLockOnToggle;

        // เริ่มต้นด้วยการปิดกล้อง Lock-On
        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.LockOn.performed -= OnLockOnToggle;
        }
    }

    private void Update()
    {
        if (IsLockedOn)
        {
            // ตรวจสอบว่าศัตรูยังอยู่ หรือหลุดระยะหรือไม่
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy ||
                Vector3.Distance(transform.position, currentTarget.position) > noticeRadius * 1.3f)
            {
                UnlockTarget();
                return;
            }

            // ให้กล้อง Lock-On หันมองไปที่ศัตรู
            if (lockOnCamera != null)
            {
                lockOnCamera.Target.LookAtTarget = currentTarget;
            }

            // อัปเดตตำแหน่ง Marker บนหัวศัตรู
            if (activeMarker != null)
            {
                activeMarker.transform.position = currentTarget.position + Vector3.up * 2f;
            }
        }
    }

    private void OnLockOnToggle(InputAction.CallbackContext context)
    {
        if (IsLockedOn)
        {
            UnlockTarget();
        }
        else
        {
            FindAndLockTarget();
        }
    }

    private void FindAndLockTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, noticeRadius, enemyLayers);
        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (var col in colliders)
        {
            Vector3 directionToTarget = (col.transform.position - cameraTransform.position).normalized;
            // เช็คเฉพาะศัตรูที่อยู่หน้ามุมกล้อง (Dot Product > 0.3)
            if (Vector3.Dot(cameraTransform.forward, directionToTarget) > 0.3f)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = col.transform;
                }
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;

            // สลับกล้อง Cinemachine
            if (lockOnCamera != null)
            {
                lockOnCamera.Target.LookAtTarget = currentTarget;
                lockOnCamera.gameObject.SetActive(true);
            }

            // สร้าง Marker ชี้เป้า
            if (lockOnMarkerPrefab != null && activeMarker == null)
            {
                activeMarker = Instantiate(lockOnMarkerPrefab, currentTarget.position + Vector3.up * 2f, Quaternion.identity);
            }

            Debug.Log($"<color=cyan>[Lock-On] Locked onto: {currentTarget.name}</color>");
        }
    }

    public void UnlockTarget()
    {
        currentTarget = null;

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }

        if (activeMarker != null)
        {
            Destroy(activeMarker);
            activeMarker = null;
        }

        Debug.Log("<color=yellow>[Lock-On] Unlocked</color>");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, noticeRadius);
    }
}