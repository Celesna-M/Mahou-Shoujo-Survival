using System.Collections;
using UnityEngine;

public enum GruntState
{
    Chase,      // ระยะ > 2 เมตร: เดินเข้าหา
    WindUp,     // ระยะ <= 2 เมตร: ง้างหมัด 1 วินาที
    Attack,     // ปล่อยหมัดต่อย
    Dead        // ตาย
}

[RequireComponent(typeof(CharacterController))]
public class GruntEnemy : MonoBehaviour, IDamageable
{
    [Header("Stats (Strict MVP)")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float moveSpeed = 2.5f;       // เดินช้ากว่าผู้เล่น
    [SerializeField] private float attackRange = 2.0f;     // ระยะเริ่มง้างหมัด
    [SerializeField] private float windUpDuration = 1.0f;  // ง้างหมัดนาน 1 วินาทีให้หลบง่ายๆ
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private int starDustReward = 5;       // แต้มเงินที่ดรอปเข้าตัว

    [Header("Visual Feedback")]
    [SerializeField] private Renderer enemyRenderer;

    private float currentHealth;
    private GruntState currentState = GruntState.Chase;
    private CharacterController controller;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private Color defaultColor;
    private bool isAttacking = false;

    // --- เชื่อมต่อกับระบบ PlayerExecution (แก้ Error CS1061) ---
    // ตามสเปก MVP Grunt จะไม่มีเกราะ Stagger จึงคืนค่า false เสมอ
    public bool IsStunned => false;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentHealth = maxHealth;

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
            defaultColor = enemyRenderer.material.color;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (currentState == GruntState.Dead || playerTransform == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // หมุนตัวหันหน้าเข้าหาผู้เล่นเสมอ
        Vector3 lookDirection = (playerTransform.position - transform.position);
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 8f);
        }

        switch (currentState)
        {
            case GruntState.Chase:
                // ถ้าห่างเกิน 2 เมตร ให้เดินเข้าหาตรงๆ (ตาม Out of Scope: ลานโล่ง ไม่ต้องใช้ NavMesh ซับซ้อน)
                if (distanceToPlayer > attackRange)
                {
                    Vector3 moveDir = lookDirection.normalized;
                    controller.Move(moveDir * (moveSpeed * Time.deltaTime));
                }
                else
                {
                    if (!isAttacking)
                    {
                        StartCoroutine(AttackRoutine());
                    }
                }
                break;
        }

        // แรงโน้มถ่วงติดพื้น
        if (!controller.isGrounded)
        {
            controller.Move(Vector3.down * 9.8f * Time.deltaTime);
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        currentState = GruntState.WindUp;

        // 1. ช่วงง้างหมัด (Wind-Up): ตัวจะเปลี่ยนเป็นสีเหลืองเตือน 1 วินาทีเต็ม
        if (enemyRenderer != null)
            enemyRenderer.material.color = Color.yellow;

        yield return new WaitForSeconds(windUpDuration);

        // 2. ช่วงปล่อยหมัด (Attack Phase)
        currentState = GruntState.Attack;
        if (enemyRenderer != null)
            enemyRenderer.material.color = Color.magenta;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= attackRange + 0.5f && playerHealth != null)
        {
            // ทำดาเมจใส่ผู้เล่น (หากผู้เล่นกด Shift ในช่วงนี้ จะติด I-Frame 0.2s และไม่เสียเลือด)
            playerHealth.TakeDamage(attackDamage, transform.position);
        }

        yield return new WaitForSeconds(0.3f);

        if (enemyRenderer != null && currentState != GruntState.Dead)
            enemyRenderer.material.color = defaultColor;

        currentState = GruntState.Chase;
        isAttacking = false;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (currentState == GruntState.Dead)
            return;

        currentHealth -= amount;
        Debug.Log($"<color=orange>[Grunt] HP: {currentHealth}/{maxHealth}</color>");

        // กระพริบสีแดงเมื่อโดนตี
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = Color.red;
            CancelInvoke(nameof(ResetColor));
            Invoke(nameof(ResetColor), 0.1f);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// ฟังก์ชันรองรับการสังหารจาก PlayerExecution
    /// </summary>
    public void ExecuteKill()
    {
        Die();
    }

    private void Die()
    {
        currentState = GruntState.Dead;
        StopAllCoroutines();

        // ได้รับแต้ม Star Dust +5 เข้าตัวทันทีตามข้อ 2.4
        if (playerHealth != null)
        {
            playerHealth.AddStarDust(starDustReward);
        }

        if (enemyRenderer != null)
            enemyRenderer.material.color = Color.gray;

        controller.enabled = false;

        Debug.Log("<color=green>[Grunt] Defeated! +5 Star Dust</color>");
        Destroy(gameObject, 0.8f);
    }

    private void ResetColor()
    {
        if (enemyRenderer != null && currentState != GruntState.Dead && !isAttacking)
            enemyRenderer.material.color = defaultColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}