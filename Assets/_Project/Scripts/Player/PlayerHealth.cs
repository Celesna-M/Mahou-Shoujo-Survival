using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cross-Scene Progression, Rank (E -> S), 3-Line Hub Upgrades & Milestone Manager
/// - Pulls all Rank Letters, Zone Names, Story Logs, and Milestone Flavor Texts directly from GameTextMasterConfig!
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
    public const string StaminaLevelPrefKey = "PLAYER_STAMINA_UPGRADE_LEVEL";
    public const string BonusStaminaRegenPrefKey = "PLAYER_BONUS_STAMINA_REGEN";
    public const string BonusDodgeReductionPrefKey = "PLAYER_BONUS_DODGE_REDUCTION";
    public const string AtkLevelPrefKey = "PLAYER_ATK_UPGRADE_LEVEL";
    public const string BonusAtkPrefKey = "PLAYER_BONUS_ATK";
    public const string HighestStagePrefKey = "PLAYER_HIGHEST_UNLOCKED_STAGE";
    public const string ClearedStageMaskPrefKey = "PLAYER_CLEARED_STAGE_MASK";
    public const string TotalKillsPrefKey = "PLAYER_TOTAL_ENEMIES_KILLED";
    public const string SelectedCharIndexPrefKey = "PLAYER_SELECTED_CHAR_INDEX";
    public const string CurrentEnergyPrefKey = "PLAYER_CURRENT_ENERGY";
    public const string MasterVolumePrefKey = "SETTINGS_MASTER_VOLUME";
    public const string MouseSensPrefKey = "SETTINGS_MOUSE_SENSITIVITY";

    // 1. Settings Data (Available across all 4 scenes)
    public float MasterVolume { get; private set; } = 0.8f;
    public float MouseSensitivity { get; private set; } = 1.0f;

    // 2. Play Energy Limit System
    [Header("Energy System Settings")]
    [SerializeField] private int maxEnergy = 5;
    [SerializeField] private float energyRechargeSeconds = 60f;
    private float energyRechargeTimer = 0f;

    public int CurrentEnergy { get; private set; } = 5;
    public int MaxEnergy => maxEnergy;
    public float EnergyRechargeRemainingSeconds => Mathf.Max(0f, energyRechargeSeconds - energyRechargeTimer);

    // 3. Economy & 3-Line Hub Upgrade Data
    public int StarDust { get; private set; }
    public int ShatteredCore { get; private set; }

    public int HpUpgradeLevel { get; private set; }
    public float BonusMaxHealth { get; private set; }

    public int StaminaUpgradeLevel { get; private set; }
    public float BonusStaminaRegen { get; private set; }
    public float BonusDodgeCostReduction { get; private set; }

    public int AttackUpgradeLevel { get; private set; }
    public float BonusAttackDamage { get; private set; }

    public int TotalUpgradeLevels => HpUpgradeLevel + StaminaUpgradeLevel + AttackUpgradeLevel;

    // 4. Rank, Stage & Milestone Data (Pulled from GameTextMasterConfig)
    public int HighestUnlockedStageIndex { get; private set; }
    public int ClearedStageBitmask { get; private set; }
    public int TotalEnemiesKilled { get; private set; }

    public int CurrentRankIndex => Mathf.Clamp(HighestUnlockedStageIndex, 0, GameTextMasterConfig.RankLetters.Length - 1);
    public string CurrentRankLetter => GameTextMasterConfig.RankLetters[CurrentRankIndex];
    public string CurrentRankZoneName => GameTextMasterConfig.RankZoneNames[Mathf.Clamp(CurrentRankIndex, 0, GameTextMasterConfig.RankZoneNames.Length - 1)];
    public string CurrentRankStoryLine => GameTextMasterConfig.RankStoryLogs[Mathf.Clamp(CurrentRankIndex, 0, GameTextMasterConfig.RankStoryLogs.Length - 1)];

    // First-Clear Bonus Tracking for Victory Screen
    [Header("First-Clear Bonus Settings")]
    [SerializeField] private int baseFirstClearStarDust = 80;
    [SerializeField] private int firstClearStarDustPerStage = 40;
    [SerializeField] private int baseFirstClearCore = 1;

    public bool LastClearWasFirstTime { get; private set; } = false;
    public int LastFirstClearStarDustBonus { get; private set; } = 0;
    public int LastFirstClearCoreBonus { get; private set; } = 0;
    public string LastUnlockedRankLetter { get; private set; } = "";

    // 5. Selected Character Data
    public bool HasSelectedCharacter { get; private set; } = false;
    public int SelectedCharacterIndex { get; private set; } = 0;
    public string SelectedCharacterName { get; private set; } = GameTextMasterConfig.CharacterNames[0];
    public float SelectedCharacterBaseHp { get; private set; } = 100f;
    public float SelectedCharacterBaseAtk { get; private set; } = 25f;
    public float SelectedCharacterMoveSpeed { get; private set; } = 5.0f;

    // 6. Selected Stage Data
    public bool HasSelectedStageFromMap { get; private set; } = false;
    public int SelectedStageIndex { get; private set; } = 0;
    public string SelectedStageTitle { get; private set; } = GameTextMasterConfig.StageNodeTitles[0];
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

        StaminaUpgradeLevel = PlayerPrefs.GetInt(StaminaLevelPrefKey, 0);
        BonusStaminaRegen = PlayerPrefs.GetFloat(BonusStaminaRegenPrefKey, 0f);
        BonusDodgeCostReduction = PlayerPrefs.GetFloat(BonusDodgeReductionPrefKey, 0f);

        AttackUpgradeLevel = PlayerPrefs.GetInt(AtkLevelPrefKey, 0);
        BonusAttackDamage = PlayerPrefs.GetFloat(BonusAtkPrefKey, 0f);

        HighestUnlockedStageIndex = PlayerPrefs.GetInt(HighestStagePrefKey, 0);
        ClearedStageBitmask = PlayerPrefs.GetInt(ClearedStageMaskPrefKey, 0);
        TotalEnemiesKilled = PlayerPrefs.GetInt(TotalKillsPrefKey, 0);
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

        PlayerPrefs.SetInt(StaminaLevelPrefKey, StaminaUpgradeLevel);
        PlayerPrefs.SetFloat(BonusStaminaRegenPrefKey, BonusStaminaRegen);
        PlayerPrefs.SetFloat(BonusDodgeReductionPrefKey, BonusDodgeCostReduction);

        PlayerPrefs.SetInt(AtkLevelPrefKey, AttackUpgradeLevel);
        PlayerPrefs.SetFloat(BonusAtkPrefKey, BonusAttackDamage);

        PlayerPrefs.SetInt(HighestStagePrefKey, HighestUnlockedStageIndex);
        PlayerPrefs.SetInt(ClearedStageMaskPrefKey, ClearedStageBitmask);
        PlayerPrefs.SetInt(TotalKillsPrefKey, TotalEnemiesKilled);
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

    public bool HasEnoughEnergy(int cost) => CurrentEnergy >= cost;

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
        StaminaUpgradeLevel = 0;
        BonusStaminaRegen = 0f;
        BonusDodgeCostReduction = 0f;
        AttackUpgradeLevel = 0;
        BonusAttackDamage = 0f;
        HighestUnlockedStageIndex = 0;
        ClearedStageBitmask = 0;
        TotalEnemiesKilled = 0;
        SelectedCharacterIndex = 0;
        CurrentEnergy = maxEnergy;
        energyRechargeTimer = 0f;
        LastClearWasFirstTime = false;
        LastFirstClearStarDustBonus = 0;
        LastFirstClearCoreBonus = 0;
        SaveToPrefs();
    }

    public void SelectCharacter(int index, string charName, float baseHp, float baseAtk, float moveSpeed)
    {
        HasSelectedCharacter = true;
        SelectedCharacterIndex = Mathf.Max(0, index);
        int clampedIdx = Mathf.Clamp(index, 0, GameTextMasterConfig.CharacterNames.Length - 1);
        SelectedCharacterName = string.IsNullOrEmpty(charName) ? GameTextMasterConfig.CharacterNames[clampedIdx] : charName;
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

    public bool TryUpgradeStaminaEfficiency(int starDustCost, float regenGain, float dodgeCostReductionGain)
    {
        if (StarDust < starDustCost) return false;

        StarDust -= starDustCost;
        StaminaUpgradeLevel++;
        BonusStaminaRegen += regenGain;
        BonusDodgeCostReduction += dodgeCostReductionGain;
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

    public static string GetRankLetterForStage(int stageIndex)
    {
        int clamped = Mathf.Clamp(stageIndex, 0, GameTextMasterConfig.RankLetters.Length - 1);
        return GameTextMasterConfig.RankLetters[clamped];
    }

    public static string GetStoryLogForStage(int stageIndex)
    {
        int clamped = Mathf.Clamp(stageIndex, 0, GameTextMasterConfig.RankStoryLogs.Length - 1);
        return GameTextMasterConfig.RankStoryLogs[clamped];
    }

    public bool IsStageClearedBefore(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= 30) return false;
        return (ClearedStageBitmask & (1 << stageIndex)) != 0;
    }

    public int GetFirstClearStarDustBonusForStage(int stageIndex)
    {
        return baseFirstClearStarDust + (Mathf.Max(0, stageIndex) * firstClearStarDustPerStage);
    }

    public int GetFirstClearCoreBonusForStage(int stageIndex)
    {
        return baseFirstClearCore + (stageIndex >= 3 ? 1 : 0);
    }

    public void SelectStage(int stageIndex, string stageTitle, int normalEnemies, int eliteBosses, int energyCost = 1)
    {
        HasSelectedStageFromMap = true;
        SelectedStageIndex = Mathf.Max(0, stageIndex);
        int clampedIdx = Mathf.Clamp(stageIndex, 0, GameTextMasterConfig.StageNodeTitles.Length - 1);
        SelectedStageTitle = string.IsNullOrEmpty(stageTitle) ? GameTextMasterConfig.StageNodeTitles[clampedIdx] : stageTitle;
        SelectedStageNormalCount = Mathf.Max(1, normalEnemies);
        SelectedStageEliteCount = Mathf.Max(0, eliteBosses);
        SelectedStageEnergyCost = Mathf.Max(0, energyCost);
    }

    public void MarkCurrentStageCleared()
    {
        int stageIdx = Mathf.Max(0, SelectedStageIndex);
        bool wasClearedBefore = IsStageClearedBefore(stageIdx);

        if (!wasClearedBefore)
        {
            LastClearWasFirstTime = true;
            LastFirstClearStarDustBonus = GetFirstClearStarDustBonusForStage(stageIdx);
            LastFirstClearCoreBonus = GetFirstClearCoreBonusForStage(stageIdx);

            ClearedStageBitmask |= (1 << stageIdx);
            StarDust += LastFirstClearStarDustBonus;
            ShatteredCore += LastFirstClearCoreBonus;
        }
        else
        {
            LastClearWasFirstTime = false;
            LastFirstClearStarDustBonus = 0;
            LastFirstClearCoreBonus = 0;
        }

        if (stageIdx + 1 > HighestUnlockedStageIndex)
        {
            HighestUnlockedStageIndex = Mathf.Min(GameTextMasterConfig.RankLetters.Length - 1, stageIdx + 1);
            LastUnlockedRankLetter = GameTextMasterConfig.RankLetters[HighestUnlockedStageIndex];
        }
        else
        {
            LastUnlockedRankLetter = "";
        }

        SaveToPrefs();
    }

    public void RegisterEnemyKill(bool isEliteBoss)
    {
        TotalEnemiesKilled++;

        if (TotalEnemiesKilled % 25 == 0)
        {
            StarDust += 50;
            if (BattleHUD.Instance != null)
            {
                string suffix = string.Format(GameTextMasterConfig.HUD_KillMilestoneSuffixFormat, TotalEnemiesKilled);
                BattleHUD.Instance.SpawnFloatingFeedback(GameTextMasterConfig.HUD_IconMilestone, "KILL_BONUS", 50, new Color(1f, 0.35f, 0.55f), Vector3.zero, suffix);
            }
        }

        SaveToPrefs();
    }

    public int GetCurrentMilestoneTier()
    {
        int score = (TotalEnemiesKilled / 15) + (TotalUpgradeLevels / 3) + CurrentRankIndex;
        if (score >= 8) return 3;
        if (score >= 5) return 2;
        if (score >= 2) return 1;
        return 0;
    }

    public Color GetProgressionUiThemeColor()
    {
        switch (GetCurrentMilestoneTier())
        {
            case 0: return new Color(0.95f, 0.48f, 0.72f, 1f);
            case 1: return new Color(0.92f, 0.25f, 0.52f, 1f);
            case 2: return new Color(0.86f, 0.14f, 0.28f, 1f);
            default: return new Color(0.72f, 0.06f, 0.16f, 1f);
        }
    }

    public string GetMilestoneFlavorText()
    {
        switch (GetCurrentMilestoneTier())
        {
            case 0:
                return string.Format(GameTextMasterConfig.Milestone_Tier1_FragileSoulFormat, TotalEnemiesKilled, TotalUpgradeLevels);
            case 1:
                return string.Format(GameTextMasterConfig.Milestone_Tier2_AwakenedCrestFormat, TotalEnemiesKilled, TotalUpgradeLevels);
            case 2:
                return string.Format(GameTextMasterConfig.Milestone_Tier3_CrimsonResonanceFormat, TotalEnemiesKilled, TotalUpgradeLevels);
            default:
                return string.Format(GameTextMasterConfig.Milestone_Tier4_AbyssSovereignFormat, TotalEnemiesKilled, TotalUpgradeLevels);
        }
    }
}

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


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerHealth.cs (และ GameProgressionManager)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * [ส่วนที่ 1: คลาส GameProgressionManager — ตัวจัดการข้อมูลข้ามฉากและเซฟเกม]
 * 1. LoadFromPrefs() & SaveToPrefs()
 *    - โหลดและบันทึกค่า Star Dust, Shattered Core, เลเวลอัปเกรดทั้ง 3 สาย, ด่านสูงสุดที่ปลดล็อก,
 *      จำนวนการสังหารสะสม, และค่าพลังงาน (Play Energy) ลงใน PlayerPrefs
 *
 * 2. TryUpgradeMaxHealth(), TryUpgradeStaminaEfficiency(), TryUpgradeAttack()
 *    - หักแร่และเพิ่มค่าโบนัสถาวรของทั้ง 3 สายอัปเกรด (Vitality Boost, Stamina Efficiency, Raw Power)
 *
 * 3. MarkCurrentStageCleared() & IsStageClearedBefore()
 *    - บันทึกการเคลียร์ด่านปัจจุบัน หากเป็นการเคลียร์ด่านนั้นครั้งแรก จะแจกโบนัสก้อนใหญ่ (First-Clear Bonus)
 *      และปลดล็อกระดับ Rank ถัดไป (Rank E -> D -> C -> B -> A -> S)
 *
 * 4. RegisterEnemyKill(), GetCurrentMilestoneTier(), GetProgressionUiThemeColor()
 *    - นับจำนวนมอนสเตอร์ที่สังหารสะสม แจกโบนัสทุกๆ 25 ตัว และคำนวณระดับ Milestone (0 ถึง 3)
 *      เพื่อปลดล็อกข้อความลับ (Flavor Text) และคืนค่าสีธีม UI ที่ค่อยๆ เปลี่ยนจากชมพูอ่อนเป็นแดงเข้ม
 *
 * [ส่วนที่ 2: คลาส PlayerHealth — ระบบเลือดผู้เล่นในฉากต่อสู้]
 * 5. Awake() & TakeDamage()
 *    - คำนวณเลือดสูงสุดรวม (Base HP ของตัวละคร + โบนัสจาก Vitality Boost)
 *    - ลดเลือดเมื่อโดนมอนสเตอร์โจมตี (ยกเว้นตอนติด I-Frame จากการกลิ้งหลบหรือทำท่า Execution)
 *      สั่นกล้องด้วย CinemachineImpulseSource และเปิดอมตะชั่วคราวหลังโดนตี (HurtInvincibleRoutine)
 *
 * 6. HealPercentage(), AddStarDust(), AddShatteredCore(), Die(), RestartStage()
 *    - ฟื้นฟูเลือดเมื่อทำท่า Execution สำเร็จ, บวกแร่เมื่อเก็บลูกแก้วจากมอนสเตอร์,
 *      จัดการสถานะตอนผู้เล่นตาย (ShowGameOverScreen), และหักพลังงานเมื่อกดเล่นด่านซ้ำ (Retry)
 * ============================================================================ */