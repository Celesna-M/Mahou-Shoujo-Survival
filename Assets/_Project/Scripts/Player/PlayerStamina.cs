using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenRate = 25f;
    [SerializeField] private float regenDelay = 1f;

    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;

    private float delayTimer;
    public event Action<float, float> OnStaminaChanged;

    private void Awake() => CurrentStamina = maxStamina;

    private void Update()
    {
        if (delayTimer > 0f)
        {
            delayTimer -= Time.deltaTime;
            return;
        }

        if (CurrentStamina < maxStamina)
        {
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + (regenRate * Time.deltaTime));
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }
    }

    public bool HasEnoughStamina(float amount) => CurrentStamina >= amount;

    public bool TryConsumeStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (CurrentStamina >= amount)
        {
            CurrentStamina -= amount;
            delayTimer = regenDelay;
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
            return true;
        }
        return false;
    }

    public void RestoreFullStamina()
    {
        CurrentStamina = maxStamina;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}