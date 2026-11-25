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

    private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropId = Shader.PropertyToID("_Color");
    private static readonly WaitForSeconds FlashIntervalWait = new WaitForSeconds(0.1f);

    [Header("Health Settings (Synced with Vitality Boost Upgrade)")]
    [Tooltip("Base Max HP before Hub Vitality Boost upgrades (overridden by CharacterData_SO / Lobby).")]
    [SerializeField] private float baseMaxHealth = 100f;
    [Tooltip("Invulnerability duration after taking damage (seconds).")]
    [SerializeField] private float hurtInvincibleDuration = 0.6f;
    [Tooltip("Camera impulse shake intensity on player hurt.")]
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
    private PlayerMovement cachedMovement;
    private PlayerLockOn cachedLockOn;
    private AnimVFXController cachedAnimVfx;
    private PlayerStateMachine stateMachine;
    private MaterialPropertyBlock propBlock;
    private Coroutine hurtInvincibleCoroutine;

    private bool isHurtInvincible = false;
    private Color defaultColor = Color.white;
    private bool hasBaseColorProp;
    private bool hasColorProp;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        playerCombat = GetComponent<PlayerCombat>();
        cachedMovement = GetComponent<PlayerMovement>();
        cachedLockOn = GetComponent<PlayerLockOn>();
        cachedAnimVfx = GetComponent<AnimVFXController>();
        stateMachine = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();
        propBlock = new MaterialPropertyBlock();

        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<Renderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheRendererColorProperties();

        var prog = GameProgressionManager.Instance;
        float charBaseHp = prog.HasSelectedCharacter ? prog.SelectedCharacterBaseHp : baseMaxHealth;
        effectiveMaxHealth = charBaseHp + prog.BonusMaxHealth;
        CurrentHealth = effectiveMaxHealth;

        starDust = prog.StarDust;
        shatteredCore = prog.ShatteredCore;
        StageStarDustEarned = 0;
        StageShatteredCoreEarned = 0;
    }

    private void CacheRendererColorProperties()
    {
        hasBaseColorProp = false;
        hasColorProp = false;
        defaultColor = Color.white;

        if (playerRenderer == null) return;
        Material shared = playerRenderer.sharedMaterial;
        if (shared == null) return;

        hasBaseColorProp = shared.HasProperty(BaseColorPropId);
        hasColorProp = shared.HasProperty(ColorPropId);

        if (hasBaseColorProp)
            defaultColor = shared.GetColor(BaseColorPropId);
        else if (hasColorProp)
            defaultColor = shared.GetColor(ColorPropId);
    }

    private void ApplyTintColor(Color tint)
    {
        if (playerRenderer == null || (!hasBaseColorProp && !hasColorProp)) return;
        playerRenderer.GetPropertyBlock(propBlock);
        if (hasBaseColorProp) propBlock.SetColor(BaseColorPropId, tint);
        if (hasColorProp) propBlock.SetColor(ColorPropId, tint);
        playerRenderer.SetPropertyBlock(propBlock);
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (IsDead || isHurtInvincible || amount <= 0f) return;

        if (cachedMovement != null && cachedMovement.IsInvincible)
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

            if (hurtInvincibleCoroutine != null)
                StopCoroutine(hurtInvincibleCoroutine);
            hurtInvincibleCoroutine = StartCoroutine(HurtInvincibleRoutine());
        }
    }

    private IEnumerator HurtInvincibleRoutine()
    {
        isHurtInvincible = true;
        float elapsed = 0f;

        while (elapsed < hurtInvincibleDuration && !IsDead)
        {
            ApplyTintColor(Color.red);
            yield return FlashIntervalWait;
            if (IsDead) break;

            ApplyTintColor(Color.white);
            yield return FlashIntervalWait;
            elapsed += 0.2f;
        }

        if (!IsDead)
        {
            ApplyTintColor(defaultColor);
        }

        isHurtInvincible = false;
        hurtInvincibleCoroutine = null;
    }

    public void HealPercentage(float percent)
    {
        if (IsDead || percent <= 0f) return;
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
        isHurtInvincible = false;

        if (hurtInvincibleCoroutine != null)
        {
            StopCoroutine(hurtInvincibleCoroutine);
            hurtInvincibleCoroutine = null;
        }

        playerCombat?.CancelCombat();
        cachedAnimVfx?.StopAllActiveVFX();

        stateMachine?.TriggerDeathState();
        if (animator != null)
            PlayerAnimID.SafeSetTrigger(animator, PlayerAnimID.Dead);

        if (cachedMovement != null) cachedMovement.CanMove = false;
        if (cachedLockOn != null) cachedLockOn.UnlockTarget();

        ApplyTintColor(Color.gray);

        OnDeath?.Invoke();
        OnPlayerDeath?.Invoke();
    }

    public void RestartStage()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
