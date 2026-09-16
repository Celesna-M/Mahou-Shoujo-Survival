using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerExecution : MonoBehaviour
{
    [Header("Execution Settings")]
    [SerializeField] private float executeRange = 2.5f;     // ระยะห่างที่สามารถกดประหารได้
    [SerializeField] private float executionDuration = 1.2f; // ความยาวคัตซีนสังหาร
    [SerializeField] private float healReward = 35f;        // เลือดที่ได้รับเมื่อประหาร
    [SerializeField] private LayerMask enemyLayers;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera executionCamera;

    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private PlayerLockOn playerLockOn;
    private CinemachineImpulseSource impulseSource;

    private bool isExecuting = false;
    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        playerLockOn = GetComponent<PlayerLockOn>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
        playerMovement.InputActions.Player.Interact.performed += OnInteractPerformed;

        if (executionCamera != null)
        {
            executionCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Interact.performed -= OnInteractPerformed;
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isExecuting || playerMovement.IsDodging)
            return;

        // ตรวจหาศัตรูในระยะที่ติดสถานะ Stun
        DummyEnemy targetEnemy = FindStunnedEnemy();

        if (targetEnemy != null)
        {
            StartCoroutine(DoExecutionSequence(targetEnemy));
        }
    }

    private DummyEnemy FindStunnedEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, executeRange, enemyLayers);
        foreach (var col in colliders)
        {
            if (col.TryGetComponent<DummyEnemy>(out var enemy))
            {
                if (enemy.IsStunned)
                {
                    return enemy;
                }
            }
        }
        return null;
    }

    private IEnumerator DoExecutionSequence(DummyEnemy enemy)
    {
        isExecuting = true;

        // 1. ล็อกการเคลื่อนที่ของผู้เล่น และให้สถานะอมตะ (I-Frame เต็มรูปแบบ)
        playerMovement.CanMove = false;

        // 2. หันหน้าเข้าหาศัตรูอย่างแม่นยำ
        Vector3 lookDir = (enemy.transform.position - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        // 3. สลับมุมกล้องไปที่กล้องคัตซีนประหาร
        if (executionCamera != null)
        {
            executionCamera.Target.LookAtTarget = enemy.transform;
            executionCamera.gameObject.SetActive(true);
        }

        // 4. จังหวะคัตซีน: สโลว์โมชันเปิดฉาก
        if (HitStopManager.Instance != null)
        {
            HitStopManager.Instance.TriggerHitStop(0.15f, 0.1f);
        }

        Debug.Log("<color=red><b>[EXECUTION] CUTSCENE TRIGGERED! SLICING ENEMY!</b></color>");

        // หน่วงเวลาสำหรับแอนิเมชันสังหาร (หรือช่วงเงื้ออาวุธ)
        yield return new WaitForSecondsRealtime(executionDuration * 0.6f);

        // 5. จังหวะปลิดชีพ (Impact Hit): สั่นกล้องอย่างรุนแรง + Hit Stop
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(1.8f); // สั่นกล้องแรงสุด
        }
        if (HitStopManager.Instance != null)
        {
            HitStopManager.Instance.TriggerHitStop(0.2f, 0.0f); // หยุดสนิท
        }

        // สั่งให้ศัตรูตาย
        enemy.ExecuteKill();

        // 6. รางวัลจากการประหารตาม GDD:
        // - ฟื้นฟู HP
        playerHealth.Heal(healReward);
        // - ดรอป Shattered Core (ทรัพยากรพิเศษสำหรับปลดล็อกอาวุธ/คอมโบ)
        Debug.Log("<color=cyan><b>[DROP] +1 Shattered Core (Rare Item) Acquired!</b></color>");

        // หน่วงเวลาหลังฟันเสร็จเล็กน้อย
        yield return new WaitForSecondsRealtime(executionDuration * 0.4f);

        // 7. คืนมุมกล้องเดิมและการควบคุม
        if (executionCamera != null)
        {
            executionCamera.gameObject.SetActive(false);
        }

        playerMovement.CanMove = true;
        isExecuting = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, executeRange);
    }
}