using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    public event Action<float, float> OnHealthChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        // ป้องกันดาเมจหากอยู่ในสถานะอมตะ (เช่น กำลังกลิ้ง หรือกำลังทำคัตซีนประหาร)
        if (TryGetComponent<PlayerMovement>(out var movement) && movement.IsInvincible)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"[Player] Took {amount} damage! Current HP: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
        {
            Debug.Log("<color=red>[Player] Defeated!</color>");
        }
    }

    /// <summary>
    /// ฟื้นฟูเลือด (ใช้เมื่อทำ Finisher Execution สำเร็จ)
    /// </summary>
    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"<color=green>[Player] Healed for {amount}! Current HP: {CurrentHealth}/{maxHealth}</color>");
    }
}