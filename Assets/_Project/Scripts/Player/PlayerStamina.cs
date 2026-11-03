using System;
using UnityEngine;

/// <summary>
/// Player Stamina System (Synced with Hub Upgrade 2: Stamina Efficiency)
/// - Applies BonusStaminaRegen from GameProgressionManager automatically.
/// </summary>
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

    private void Awake()
    {
        if (GameProgressionManager.Instance != null && GameProgressionManager.Instance.BonusStaminaRegen > 0f)
        {
            regenRate += GameProgressionManager.Instance.BonusStaminaRegen;
        }
        CurrentStamina = maxStamina;
    }

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


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerStamina.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake()
 *    - ดึงค่าโบนัสความเร็วฟื้นฟู Stamina (BonusStaminaRegen) จากสายอัปเกรดที่ 2 (Stamina Efficiency)
 *      ใน GameProgressionManager มาบวกเพิ่มเข้ากับ regenRate พื้นฐาน และตั้งค่า Stamina เริ่มต้นให้เต็มหลอด
 *
 * 2. Update()
 *    - นับถอยหลังเวลาหน่วงก่อนเริ่มฟื้นฟู (delayTimer) หลังจากผู้เล่นเพิ่งกลิ้งหลบ
 *    - เมื่อพ้นเวลาหน่วงแล้ว จะค่อยๆ ฟื้นฟูค่า CurrentStamina ตามอัตรา regenRate ต่อวินาที และส่งอีเวนต์อัปเดตหลอด UI
 *
 * 3. TryConsumeStamina(float amount) & RestoreFullStamina()
 *    - ตรวจสอบว่ามี Stamina พอสำหรับการกลิ้งหลบหรือไม่ ถ้าพอจะหักค่า Stamina และรีเซ็ตเวลาหน่วงฟื้นฟู
 * ============================================================================ */