using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerExecution : MonoBehaviour
{
    [Header("Execution Settings (MVP Spec)")]
    [SerializeField] private float executeRange = 2.5f;
    [SerializeField] private float executionDuration = 2.0f; // อมตะและคัตซีน 2 วินาที
    [SerializeField] private LayerMask enemyLayers;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera executionCamera;

    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private CinemachineImpulseSource impulseSource;

    private bool isExecuting = false;
    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
        playerMovement.InputActions.Player.Execute.performed += OnExecutePerformed;

        if (executionCamera != null)
        {
            executionCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Execute.performed -= OnExecutePerformed;
        }
    }

    private void OnExecutePerformed(InputAction.CallbackContext context)
    {
        if (isExecuting || playerMovement.IsDodging)
            return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, executeRange, enemyLayers);
        foreach (var col in colliders)
        {
            // ค้นหาเป้าหมายใดๆ ที่รองรับการประหาร และอยู่ในสถานะที่ประหารได้ (CanBeExecuted)
            if (col.TryGetComponent<IExecutable>(out var executable) && executable.CanBeExecuted)
            {
                StartCoroutine(DoExecutionSequence(col.gameObject, executable));
                return;
            }
        }
    }

    private IEnumerator DoExecutionSequence(GameObject targetEnemy, IExecutable executable)
    {
        isExecuting = true;
        playerMovement.CanMove = false;
        playerMovement.SetExternalInvincible(true); // อมตะตลอดช่วง 2 วินาที

        Vector3 lookDir = (targetEnemy.transform.position - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        if (executionCamera != null)
        {
            executionCamera.Target.LookAtTarget = targetEnemy.transform;
            executionCamera.gameObject.SetActive(true);
        }

        Debug.Log("<color=red><b>[EXECUTION] PRESS [F] SUCCESS! EXECUTING TARGET!</b></color>");

        // หน่วงเวลาช่วงง้างสังหาร 1.2 วินาที
        yield return new WaitForSecondsRealtime(1.2f);

        // จังหวะ Impact: สั่นกล้อง + หยุดเวลา
        if (impulseSource != null)
            impulseSource.GenerateImpulse(1.5f);

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.TriggerHitStop(0.1f, 0.05f);

        // สั่งสังหารเป้าหมาย
        executable.ExecuteKill();

        // ฟื้นฟูเลือด 20% และดรอป Shattered Core ตามสเปก
        playerHealth.HealPercentage(20f);
        playerHealth.AddShatteredCore(1);

        yield return new WaitForSecondsRealtime(0.8f);

        if (executionCamera != null)
        {
            executionCamera.gameObject.SetActive(false);
        }

        playerMovement.SetExternalInvincible(false);
        playerMovement.CanMove = true;
        isExecuting = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, executeRange);
    }
}