using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Economy (Strict MVP)")]
    [SerializeField] private int starDust = 0;
    [SerializeField] private int shatteredCore = 0;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    public int StarDust => starDust;
    public int ShatteredCore => shatteredCore;

    public event Action<float, float> OnHealthChanged;
    public event Action<int, int> OnCurrencyChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (TryGetComponent<PlayerMovement>(out var movement) && movement.IsInvincible)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"[Player] Took {amount} damage! Current HP: {CurrentHealth}/{maxHealth}");
    }

    public void HealPercentage(float percent)
    {
        float healAmount = maxHealth * (percent / 100f);
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + healAmount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"<color=green>[Player] Healed {healAmount} HP ({percent}%)! Current HP: {CurrentHealth}/{maxHealth}</color>");
    }

    public void AddStarDust(int amount)
    {
        starDust += amount;
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
        Debug.Log($"<color=yellow>[Economy] +{amount} Star Dust! Total: {starDust}</color>");
    }

    public void AddShatteredCore(int amount)
    {
        shatteredCore += amount;
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
        Debug.Log($"<color=magenta>[Economy] +{amount} Shattered Core! Total: {shatteredCore}</color>");
    }
}