using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cross-Scene Data & Energy Manager (DontDestroyOnLoad + PlayerPrefs)
/// Connects all 4 scenes:
/// 1. MainMenu (Volume, Mouse Sensitivity, Fullscreen, Reset Save)
/// 2. The lobby (Character selection, Upgrades, Star Dust, Shattered Core, Play Energy)
/// 3. StageMap (Stage node selection, Energy consumption per run, Stage unlock progress)
/// 4. The Action (Applies character + upgrade stats, Pause/Settings mid-game exit)
/// </summary>
public class GameProgressionManager : MonoBehaviour
{
    private static GameProgressionManager instance;
    public static GameProgressionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameProgressionManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("[GameProgressionManager]");
                    instance = go.AddComponent<GameProgressionManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    public const string StarDustPrefKey = "PLAYER_STAR_DUST";
    public const string ShatteredCorePrefKey = "PLAYER_SHATTERED_CORE";
    public const string HpLevelPrefKey = "PLAYER_HP_UPGRADE_LEVEL";
    public const string BonusHpPrefKey = "PLAYER_BONUS_MAX_HP";
    public const string AtkLevelPrefKey = "PLAYER_ATK_UPGRADE_LEVEL";
    public const string BonusAtkPrefKey = "PLAYER_BONUS_ATK";
    public const string HighestStagePrefKey = "PLAYER_HIGHEST_UNLOCKED_STAGE";
    public const string SelectedCharIndexPrefKey = "PLAYER_SELECTED_CHAR_INDEX";
    public const string CurrentEnergyPrefKey = "PLAYER_CURRENT_ENERGY";
    public const string MasterVolumePrefKey = "SETTINGS_MASTER_VOLUME";
    public const string MouseSensPrefKey = "SETTINGS_MOUSE_SENSITIVITY";

    // 1. Settings Data (Available across all 4 scenes)
    public float MasterVolume { get; private set; } = 0.8f;
    public float MouseSensitivity { get; private set; } = 1.0f;

    // 2. Play Energy Limit System (Stamina/Energy to enter stages)
    [Header("Energy System Settings")]
    [SerializeField] private int maxEnergy = 5;
    [SerializeField] private float energyRechargeSeconds = 60f;
    private float energyRechargeTimer = 0f;

    public int CurrentEnergy { get; private set; } = 5;
    public int MaxEnergy => maxEnergy;
    public float EnergyRechargeRemainingSeconds => Mathf.Max(0f, energyRechargeSeconds - energyRechargeTimer);

    // 3. Economy & Upgrade Data
    public int StarDust { get; private set; }
    public int ShatteredCore { get; private set; }
    public int HpUpgradeLevel { get; private set; }
    public float BonusMaxHealth { get; private set; }
    public int AttackUpgradeLevel { get; private set; }
    public float BonusAttackDamage { get; private set; }

    // 4. Selected Character Data
    public bool HasSelectedCharacter { get; private set; } = false;
    public int SelectedCharacterIndex { get; private set; } = 0;
    public string SelectedCharacterName { get; private set; } = "01 : HIKARI - BRAWLER STRIKER";
    public float SelectedCharacterBaseHp { get; private set; } = 100f;
    public float SelectedCharacterBaseAtk { get; private set; } = 25f;
    public float SelectedCharacterMoveSpeed { get; private set; } = 5.0f;

    // 5. Selected Stage Data
    public int HighestUnlockedStageIndex { get; private set; }
    public bool HasSelectedStageFromMap { get; private set; } = false;
    public int SelectedStageIndex { get; private set; } = 0;
    public string SelectedStageTitle { get; private set; } = "STAGE 01 : EMPTY CROSSROADS";
    public int SelectedStageNormalCount { get; private set; } = 5;
    public int SelectedStageEliteCount { get; private set; } = 1;
    public int SelectedStageEnergyCost { get; private set; } = 1;

    public float TotalEffectiveMaxHp => SelectedCharacterBaseHp + BonusMaxHealth;
    public float TotalEffectiveAtk => SelectedCharacterBaseAtk + BonusAttackDamage;

    public event Action OnProgressionChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromPrefs();
    }

    private void Update()
    {
        // Auto-recharge 1 Energy over time when below MaxEnergy
        if (CurrentEnergy < maxEnergy)
        {
            energyRechargeTimer += Time.unscaledDeltaTime;
            if (energyRechargeTimer >= energyRechargeSeconds)
            {
                energyRechargeTimer = 0f;
                CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + 1);
                SaveToPrefs();
            }
        }
        else
        {
            energyRechargeTimer = 0f;
        }
    }

    public void LoadFromPrefs()
    {
        StarDust = PlayerPrefs.GetInt(StarDustPrefKey, 0);
        ShatteredCore = PlayerPrefs.GetInt(ShatteredCorePrefKey, 0);
        HpUpgradeLevel = PlayerPrefs.GetInt(HpLevelPrefKey, 0);
        BonusMaxHealth = PlayerPrefs.GetFloat(BonusHpPrefKey, 0f);
        AttackUpgradeLevel = PlayerPrefs.GetInt(AtkLevelPrefKey, 0);
        BonusAttackDamage = PlayerPrefs.GetFloat(BonusAtkPrefKey, 0f);
        HighestUnlockedStageIndex = PlayerPrefs.GetInt(HighestStagePrefKey, 0);
        SelectedCharacterIndex = PlayerPrefs.GetInt(SelectedCharIndexPrefKey, 0);
        CurrentEnergy = Mathf.Clamp(PlayerPrefs.GetInt(CurrentEnergyPrefKey, maxEnergy), 0, maxEnergy);

        MasterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, 0.8f);
        MouseSensitivity = PlayerPrefs.GetFloat(MouseSensPrefKey, 1.0f);
        AudioListener.volume = MasterVolume;
    }

    public void SaveToPrefs()
    {
        PlayerPrefs.SetInt(StarDustPrefKey, StarDust);
        PlayerPrefs.SetInt(ShatteredCorePrefKey, ShatteredCore);
        PlayerPrefs.SetInt(HpLevelPrefKey, HpUpgradeLevel);
        PlayerPrefs.SetFloat(BonusHpPrefKey, BonusMaxHealth);
        PlayerPrefs.SetInt(AtkLevelPrefKey, AttackUpgradeLevel);
        PlayerPrefs.SetFloat(BonusAtkPrefKey, BonusAttackDamage);
        PlayerPrefs.SetInt(HighestStagePrefKey, HighestUnlockedStageIndex);
        PlayerPrefs.SetInt(SelectedCharIndexPrefKey, SelectedCharacterIndex);
        PlayerPrefs.SetInt(CurrentEnergyPrefKey, CurrentEnergy);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, MasterVolume);
        PlayerPrefs.SetFloat(MouseSensPrefKey, MouseSensitivity);
        PlayerPrefs.Save();
        OnProgressionChanged?.Invoke();
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        AudioListener.volume = MasterVolume;
        SaveToPrefs();
    }

    public void SetMouseSensitivity(float sens)
    {
        MouseSensitivity = Mathf.Clamp(sens, 0.2f, 3.0f);
        SaveToPrefs();
    }

    public bool HasEnoughEnergy(int cost)
    {
        return CurrentEnergy >= cost;
    }

    public bool TryConsumeEnergy(int cost)
    {
        if (cost <= 0) return true;
        if (CurrentEnergy < cost) return false;

        CurrentEnergy -= cost;
        SaveToPrefs();
        return true;
    }

    public void RefillFullEnergy()
    {
        CurrentEnergy = maxEnergy;
        energyRechargeTimer = 0f;
        SaveToPrefs();
    }

    public bool TryBuyEnergyWithStarDust(int starDustCost, int energyGain = 1)
    {
        if (CurrentEnergy >= maxEnergy || StarDust < starDustCost) return false;
        StarDust -= starDustCost;
        CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + energyGain);
        SaveToPrefs();
        return true;
    }

    public void ResetAllSaveData()
    {
        StarDust = 0;
        ShatteredCore = 0;
        HpUpgradeLevel = 0;
        BonusMaxHealth = 0f;
        AttackUpgradeLevel = 0;
        BonusAttackDamage = 0f;
        HighestUnlockedStageIndex = 0;
        SelectedCharacterIndex = 0;
        CurrentEnergy = maxEnergy;
        energyRechargeTimer = 0f;
        SaveToPrefs();
    }

    public void SelectCharacter(int index, string charName, float baseHp, float baseAtk, float moveSpeed)
    {
        HasSelectedCharacter = true;
        SelectedCharacterIndex = Mathf.Max(0, index);
        SelectedCharacterName = string.IsNullOrEmpty(charName) ? $"CHARACTER 0{index + 1}" : charName;
        SelectedCharacterBaseHp = Mathf.Max(10f, baseHp);
        SelectedCharacterBaseAtk = Mathf.Max(1f, baseAtk);
        SelectedCharacterMoveSpeed = Mathf.Max(2f, moveSpeed);
        SaveToPrefs();
    }

    public void AddStarDust(int amount)
    {
        if (amount <= 0) return;
        StarDust += amount;
        SaveToPrefs();
    }

    public void AddShatteredCore(int amount)
    {
        if (amount <= 0) return;
        ShatteredCore += amount;
        SaveToPrefs();
    }

    public bool TryUpgradeMaxHealth(int starDustCost, float bonusHpGain)
    {
        if (StarDust < starDustCost) return false;

        StarDust -= starDustCost;
        HpUpgradeLevel++;
        BonusMaxHealth += bonusHpGain;
        SaveToPrefs();
        return true;
    }

    public bool TryUpgradeAttack(int shatteredCoreCost, float bonusAtkGain)
    {
        if (ShatteredCore < shatteredCoreCost) return false;

        ShatteredCore -= shatteredCoreCost;
        AttackUpgradeLevel++;
        BonusAttackDamage += bonusAtkGain;
        SaveToPrefs();
        return true;
    }

    public void SelectStage(int stageIndex, string stageTitle, int normalEnemies, int eliteBosses, int energyCost = 1)
    {
        HasSelectedStageFromMap = true;
        SelectedStageIndex = Mathf.Max(0, stageIndex);
        SelectedStageTitle = string.IsNullOrEmpty(stageTitle) ? $"STAGE {stageIndex + 1}" : stageTitle;
        SelectedStageNormalCount = Mathf.Max(1, normalEnemies);
        SelectedStageEliteCount = Mathf.Max(0, eliteBosses);
        SelectedStageEnergyCost = Mathf.Max(0, energyCost);
    }

    public void MarkCurrentStageCleared()
    {
        if (SelectedStageIndex + 1 > HighestUnlockedStageIndex)
        {
            HighestUnlockedStageIndex = SelectedStageIndex + 1;
            SaveToPrefs();
        }
    }
}

[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public const string StarDustPrefKey = GameProgressionManager.StarDustPrefKey;
    public const string ShatteredCorePrefKey = GameProgressionManager.ShatteredCorePrefKey;

    [Header("Health Settings (Synced with Lobby Upgrades)")]
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
    private bool isHurtInvincible = false;
    private Color defaultColor;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        playerCombat = GetComponent<PlayerCombat>();

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

        if (TryGetComponent<PlayerExecution>(out var execution) && execution.IsExecuting)
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
            if (animator != null)
                animator.SetTrigger(PlayerAnimID.Hurt);

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
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
    }

    public void AddShatteredCore(int amount)
    {
        if (amount <= 0) return;
        StageShatteredCoreEarned += amount;
        GameProgressionManager.Instance.AddShatteredCore(amount);
        shatteredCore = GameProgressionManager.Instance.ShatteredCore;
        OnCurrencyChanged?.Invoke(starDust, shatteredCore);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        if (TryGetComponent<PlayerMovement>(out var movement)) movement.CanMove = false;
        if (TryGetComponent<PlayerLockOn>(out var lockOn)) lockOn.UnlockTarget();

        if (playerRenderer != null && playerRenderer.material.HasProperty("_Color"))
            playerRenderer.material.color = Color.gray;

        OnDeath?.Invoke();
        OnPlayerDeath?.Invoke();
    }

    public void RestartStage()
    {
        int cost = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.SelectedStageEnergyCost : 1;
        if (GameProgressionManager.Instance != null && !GameProgressionManager.Instance.TryConsumeEnergy(cost))
        {
            Debug.Log("<color=yellow>[PlayerHealth] Not enough Energy to retry! Returning to Lobby.</color>");
            Time.timeScale = 1.0f;
            SceneManager.LoadScene("The lobby");
            return;
        }

        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}