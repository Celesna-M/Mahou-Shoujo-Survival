using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public const string StarDustPrefKey = GameProgressionManager.StarDustPrefKey;
    public const string ShatteredCorePrefKey = GameProgressionManager.ShatteredCorePrefKey;

    [Header("Health Settings (Synced with Vitality Boost Upgrade)")]
    [SerializeField] private float baseMaxHealth = 100f;
    [SerializeField] private float hurtInvincibleDuration = 0.6f;
    [SerializeField] private float hurtShakeForce = 0.6f;

    [Header("Economy Variables (Phase 6 - No Inventory)")]
    [SerializeField] private int starDust = 0;
    [SerializeField] private int shatteredCore = 0;

    [Header("Visual References")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Animator animator;

    private float effectiveMaxHealth;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => effectiveMaxHealth;
    public bool IsDead { get; private set; } = false;

    public int StarDust => starDust;
    public int ShatteredCore => shatteredCore;

    public int StageStarDustEarned { get; private set; } = 0;
    public int StageShatteredCoreEarned { get; private set; } = 0;

    public event Action<float, float> OnHealthChanged;
    public event Action<int, int> OnCurrencyChanged;
    public event Action OnPlayerHurt;
    public event Action OnDeath;
    public event Action OnPlayerDeath;

    private CinemachineImpulseSource impulseSource;
    private PlayerCombat playerCombat;
    private PlayerStateMachine stateMachine;
    private bool isHurtInvincible = false;
    private Color defaultColor;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        playerCombat = GetComponent<PlayerCombat>();
        stateMachine = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();

        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<Renderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
            defaultColor = playerRenderer.material.color;

        var prog = GameProgressionManager.Instance;
        float charBaseHp = prog.HasSelectedCharacter ? prog.SelectedCharacterBaseHp : baseMaxHealth;
        effectiveMaxHealth = charBaseHp + prog.BonusMaxHealth;
        CurrentHealth = effectiveMaxHealth;

        starDust = prog.StarDust;
        shatteredCore = prog.ShatteredCore;
        StageStarDustEarned = 0;
        StageShatteredCoreEarned = 0;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (IsDead || isHurtInvincible) return;

        if (TryGetComponent<PlayerMovement>(out var movement) && movement.IsInvincible)
            return;

        if (playerCombat != null && playerCombat.IsUsingSkill)
            return;

        if (playerCombat != null)
            playerCombat.EnterCombatState();

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, effectiveMaxHealth);
        OnPlayerHurt?.Invoke();

        if (impulseSource != null)
            impulseSource.GenerateImpulse(hurtShakeForce);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
        else
        {
            stateMachine?.TriggerHitStagger();
            if (animator != null)
                PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Hurt);

            StartCoroutine(HurtInvincibleRoutine());
        }
    }

    private IEnumerator HurtInvincibleRoutine()
    {
        isHurtInvincible = true;
        float elapsed = 0f;

        while (elapsed < hurtInvincibleDuration)
        {
            if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
                playerRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
                playerRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.2f;
        }

        if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
            playerRenderer.material.color = defaultColor;
        isHurtInvincible = false;
    }

    public void HealPercentage(float percent)
    {
        if (IsDead) return;
        float healAmount = effectiveMaxHealth * (percent / 100f);
        CurrentHealth = Mathf.Min(effectiveMaxHealth, CurrentHealth + healAmount);
        OnHealthChanged?.Invoke(CurrentHealth, effectiveMaxHealth);
    }

    public void AddStarDust(int amount)
    {
        if (amount <= 0) return;
        StageStarDustEarned += amount;
        GameProgressionManager.Instance.AddStarDust(amount);
        starDust = GameProgressionManager.Instance.StarDust;
        shatteredCore = GameProgressionManager.Instance.ShatteredCore;
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
    }

    public void AddShatteredCore(int amount)
    {
        if (amount <= 0) return;
        StageShatteredCoreEarned += amount;
        GameProgressionManager.Instance.AddShatteredCore(amount);
        starDust = GameProgressionManager.Instance.StarDust;
        shatteredCore = GameProgressionManager.Instance.ShatteredCore;
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        stateMachine?.TriggerDeathState();
        if (animator != null) PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Dead);

        if (TryGetComponent<PlayerMovement>(out var movement)) movement.CanMove = false;
        if (TryGetComponent<PlayerLockOn>(out var lockOn)) lockOn.UnlockTarget();

        if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
            playerRenderer.material.color = Color.gray;

        OnDeath?.Invoke();
        OnPlayerDeath?.Invoke();
    }

    public void RestartStage()
    {
        // GDD Core Pillar #3 (Zero Energy Limit): รีสตาร์ตด่านได้อิสระทันทีโดยไม่หักพลังงาน
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerHealth.cs (CombatSystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake()
 *    - ดึงค่าเลือดพื้นฐานของตัวละครที่เลือก (SelectedCharacterBaseHp) บวกกับโบนัสอัปเกรด Vitality Boost (BonusMaxHealth)
 *      จาก GameProgressionManager มาตั้งเป็นเลือดสูงสุดของผู้เล่นในด่าน
 *
 * 2. TakeDamage(float amount, Vector3 hitPoint) & HurtInvincibleRoutine()
 *    - ลดเลือดผู้เล่นเมื่อถูกมอนสเตอร์โจมตี (ยกเว้นขณะติดสถานะอมตะ I-Frame จากการกลิ้งหลบหรือระหว่างออกท่าสกิล)
 *    - สั่งสั่นกล้องด้วย CinemachineImpulseSource และเปิดอมตะชั่วคราวหลังโดนตีพร้อมกะพริบตัวสีแดง-ขาว
 *
 * 3. HealPercentage(float percent)
 *    - ฟื้นฟูเลือดตามเปอร์เซ็นต์ที่กำหนดทันทีเมื่อใช้เอฟเฟกต์ฟื้นฟูเลือด
 *
 * 4. AddStarDust(int amount) & AddShatteredCore(int amount)
 *    - บวกแร่เข้าสู่ GameProgressionManager เมื่อลูกแก้วพลังงานจากมอนสเตอร์ลอยเข้าตัวผู้เล่น และแจ้งอัปเดตตัวเลขบน BattleHUD
 *
 * 5. Die() & RestartStage()
 *    - เมื่อ HP เหลือ 0 จะล็อกการเดิน ปลดล็อกเป้า และส่งอีเวนต์ OnPlayerDeath ไปให้ BattleHUD แสดงหน้าจอดำพร้อมข้อความสิ้นหวัง
 * ============================================================================ */
