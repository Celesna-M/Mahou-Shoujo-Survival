using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private Renderer enemyRenderer;
    private Color defaultColor;

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
        Debug.Log($"<color=orange>[Enemy] Took {amount} damage! Current HP: {currentHealth}/{maxHealth}</color>");

        // กระพริบสีแดงเมื่อโดนตี
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = Color.red;
            CancelInvoke(nameof(ResetColor));
            Invoke(nameof(ResetColor), 0.15f);
        }

        if (currentHealth <= 0f)
        {
            Debug.Log("<color=red>[Enemy] Dummy Destroyed!</color>");
            currentHealth = maxHealth; // รีเซ็ตเลือดเพื่อให้เป็นกระสอบทรายซ้อมตีได้เรื่อยๆ
        }
    }

    private void ResetColor()
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = defaultColor;
        }
    }
}