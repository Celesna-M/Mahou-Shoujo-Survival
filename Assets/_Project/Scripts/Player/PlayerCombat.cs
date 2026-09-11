using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private float attackDuration = 0.4f; // ระยะเวลาของแต่ละท่าฟัน
    [SerializeField] private float comboResetTime = 0.8f; // เวลาที่คอมโบจะรีเซ็ตกลับเป็นท่าแรก
    [SerializeField] private float[] comboDamages = { 20f, 25f, 40f }; // ดาเมจแต่ละฮิต

    [Header("Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private LayerMask enemyLayers;

    private PlayerMovement playerMovement;
    private int comboIndex = 0;
    private float attackTimer = 0f;
    private float comboResetTimer = 0f;
    private bool isAttacking = false;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // ผูก Event การโจมตีจาก InputSystem_Actions
        playerMovement.InputActions.Player.Attack.performed += OnAttackPerformed;
        playerMovement.InputActions.Player.Dodge.performed += OnDodgeTriggered;
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Attack.performed -= OnAttackPerformed;
            playerMovement.InputActions.Player.Dodge.performed -= OnDodgeTriggered;
        }
    }

    private void Update()
    {
        // จัดการระยะเวลาของการฟันแต่ละครั้ง
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                EndAttack();
            }
        }

        // จัดการนับถอยหลังรีเซ็ตคอมโบกลับสู่ท่า 1
        if (comboIndex > 0 && !isAttacking)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                comboIndex = 0;
            }
        }
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        // ไม่สามารถโจมตีได้ขณะกำลังกลิ้งหลบอยู่
        if (playerMovement.IsDodging)
            return;

        // หากกำลังโจมตีอยู่และยังไม่หมดเวลาท่าปัจจุบัน ยังไม่ให้กดท่าต่อไป
        if (isAttacking)
            return;

        StartAttack();
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackDuration;
        comboResetTimer = comboResetTime;

        // ล็อกไม่ให้เดินขณะกำลังฟัน
        playerMovement.CanMove = false;

        // ดำเนินการโจมตี
        ExecuteAttackHitbox(comboDamages[comboIndex]);
        Debug.Log($"[Combat] Light Attack Hit {comboIndex + 1} | Damage: {comboDamages[comboIndex]}");

        // ขยับจังหวะคอมโบ
        comboIndex = (comboIndex + 1) % comboDamages.Length;
    }

    private void EndAttack()
    {
        isAttacking = false;
        playerMovement.CanMove = true; // คืนสิทธิ์การเดิน
    }

    private void OnDodgeTriggered(InputAction.CallbackContext context)
    {
        // Cancel Window: หากผู้เล่นกด Dodge ระหว่างกำลังโจมตี ให้แคนเซิลท่าฟันทิ้งทันที!
        if (isAttacking)
        {
            Debug.Log("[Combat] Attack Cancelled by Dodge!");
            EndAttack();
            comboIndex = 0; // รีเซ็ตคอมโบเมื่อกดหลบ
        }
    }

    private void ExecuteAttackHitbox(float damage)
    {
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Collider[] hitColliders = Physics.OverlapSphere(origin, attackRadius, enemyLayers);

        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage, hit.ClosestPoint(origin));
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // แสดงระยะ Hitbox ในหน้า Scene วิว
        Gizmos.color = Color.red;
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Gizmos.DrawWireSphere(origin, attackRadius);
    }
}