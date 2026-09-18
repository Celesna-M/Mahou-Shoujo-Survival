using System.Collections;
using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable, IExecutable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 200f;
    private float currentHealth;

    [Header("Stagger & Stun Settings")]
    [SerializeField] private float maxStagger = 100f;
    [SerializeField] private float stunDuration = 5f; // ระยะเวลามึนงง
    private float currentStagger = 0f;
    private bool isStunned = false;
    private Coroutine stunCoroutine;

    private Renderer enemyRenderer;
    private Color defaultColor;

    public bool IsStunned => isStunned;

    private void Awake()
    {
        currentHealth = maxHealth;
        enemyRenderer = GetComponent<Renderer>();
        if (enemyRenderer != null)
        {
            defaultColor = enemyRenderer.material.color;
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        currentHealth -= amount;

        // สะสมเกจ Stagger (ถ้ายังไม่ติด Stun)
        if (!isStunned)
        {
            // ดาเมจต่อเกราะ Stagger (ประมาณ 60% ของความเสียหาย)
            AddStagger(amount * 0.6f);
        }

        // กระพริบสีแดงเมื่อโดนตี (ถ้าไม่ได้ติด Stun อยู่)
        if (enemyRenderer != null && !isStunned)
        {
            enemyRenderer.material.color = Color.red;
            CancelInvoke(nameof(ResetColor));
            Invoke(nameof(ResetColor), 0.12f);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void AddStagger(float amount)
    {
        currentStagger += amount;
        Debug.Log($"[Enemy] Stagger: {currentStagger}/{maxStagger}");

        if (currentStagger >= maxStagger)
        {
            TriggerStun();
        }
    }

    private void TriggerStun()
    {
        isStunned = true;
        currentStagger = maxStagger;
        Debug.Log("<color=yellow>[Enemy] *** STAGGER BREAK! ENEMY IS STUNNED! (Press E to Execute) ***</color>");

        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = new Color(1f, 0.5f, 0f); // เปลี่ยนเป็นสีส้มเพื่อเตือนว่าประหารได้
        }

        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunDurationRoutine());
    }

    private IEnumerator StunDurationRoutine()
    {
        yield return new WaitForSeconds(stunDuration);
        RecoverFromStun();
    }

    public void RecoverFromStun()
    {
        isStunned = false;
        currentStagger = 0f;
        ResetColor();
        Debug.Log("[Enemy] Recovered from Stun.");
    }

    /// <summary>
    /// ฟังก์ชันถูกเรียกเมื่อถูก Finisher สังหาร
    /// </summary>
    public void ExecuteKill()
    {
        Debug.Log("<color=magenta>[Enemy] EXECUTED BY FINISHER!</color>");
        Die();
    }

    private void Die()
    {
        // รีเซ็ตสถานะสำหรับ Dummy ซ้อมตี
        currentHealth = maxHealth;
        currentStagger = 0f;
        isStunned = false;
        ResetColor();
    }

    private void ResetColor()
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = defaultColor;
        }
    }
    public bool CanBeExecuted => isStunned;
}