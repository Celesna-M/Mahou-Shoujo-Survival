using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenRate = 25f; // อัตราการฟื้นฟูต่อวินาที
    [SerializeField] private float regenDelay = 1f;  // ดีเลย์ก่อนเริ่มฟื้นฟูหลังใช้

    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;

    private float delayTimer;

    // Event สำหรับเชื่อมต่อกับแถบ UI ในอนาคต
    public event Action<float, float> OnStaminaChanged;

    private void Awake()
    {
        CurrentStamina = maxStamina;
    }

    private void Update()
    {
        HandleRegen();
    }

    /// <summary>
    /// ฟังก์ชันพยายามใช้ Stamina หากพอจะคืนค่า true และหักค่าทันที
    /// </summary>
    public bool TryConsumeStamina(float amount)
    {
        if (CurrentStamina >= amount)
        {
            CurrentStamina -= amount;
            delayTimer = regenDelay; // รีเซ็ตตัวนับเวลาก่อนฟื้นฟู
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
            return true;
        }

        return false;
    }

    private void HandleRegen()
    {
        if (delayTimer > 0f)
        {
            delayTimer -= Time.deltaTime;
            return;
        }

        if (CurrentStamina < maxStamina)
        {
            CurrentStamina += regenRate * Time.deltaTime;
            CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, maxStamina);
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }
    }
}