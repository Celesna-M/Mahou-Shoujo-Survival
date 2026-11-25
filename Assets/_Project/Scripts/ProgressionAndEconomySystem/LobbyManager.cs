using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public class CharacterProfileData
{
    [Tooltip("Optional CharacterData_SO. When assigned, automatically overrides card stats & spawns this exact SO in combat.")]
    public CharacterData_SO characterData;
    public string characterId = "CHAR_01";
    public string characterName = "01 : HIKARI - BRAWLER STRIKER";
    public CharacterCombatArchetype combatArchetype = CharacterCombatArchetype.MeleeStriker;
    [TextArea(1, 2)]
    public string characterRoleAndLore = "Balanced Striker | High mobility and fast melee combos.";
    public float baseMaxHealth = 110f;
    public float baseAttackDamage = 24f;
    public float baseMoveSpeed = 5.3f;
    public Color themeColor = new Color(0.92f, 0.28f, 0.58f, 1f);

    [Header("UI & 3D References")]
    public Button cardButton;
    public Image cardBackground;
    public GameObject characterPreviewObject;
}

public class ScrollRectDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private ScrollRect targetScrollRect;

    public void Initialize(ScrollRect scrollRect) => targetScrollRect = scrollRect;
    public void OnBeginDrag(PointerEventData eventData) { if (targetScrollRect != null) targetScrollRect.OnBeginDrag(eventData); }
    public void OnDrag(PointerEventData eventData) { if (targetScrollRect != null) targetScrollRect.OnDrag(eventData); }
    public void OnEndDrag(PointerEventData eventData) { if (targetScrollRect != null) targetScrollRect.OnEndDrag(eventData); }
    public void OnScroll(PointerEventData eventData) { if (targetScrollRect != null) targetScrollRect.OnScroll(eventData); }
}

/// <summary>
/// Scene 2: The Hub / Lobby Manager (Cleaned Zero-Energy, Zero-GC, Single-Source-of-Truth Edition)
/// - Syncs character selection & archetypes accurately into GameProgressionManager (no stale ActiveCharacterData).
/// - Supports optional CharacterData_SO per card (or characterDataAssets list) with balanced fallback defaults.
/// - Removed dead Energy UI & per-frame polling; kept optional Mascot Dialogue with zero-GC TMP typewriter.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("1. Scene Navigation")]
    [Tooltip("Stage selection scene loaded when clicking Start Battle.")]
    [SerializeField] private string stageMapSceneName = "StageMap";
    [Tooltip("Main menu scene loaded when clicking Back.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Fallback combat scene if StageMap is not in Build Settings.")]
    [SerializeField] private string fallbackActionSceneName = "The Action";
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("2. Economy Display")]
    [SerializeField] private TextMeshProUGUI starDustText;
    [SerializeField] private TextMeshProUGUI shatteredCoreText;

    [Header("3. Character Roster & Selection")]
    [Tooltip("Optional parallel list of CharacterData_SO (also supported directly on each card in 'characters').")]
    [SerializeField] private List<CharacterData_SO> characterDataAssets = new List<CharacterData_SO>();
    [SerializeField] private ScrollRect characterScrollRect;
    [SerializeField] private float mouseWheelScrollSensitivity = 0.15f;
    [SerializeField] private List<CharacterProfileData> characters = new List<CharacterProfileData>();
    [SerializeField] private TextMeshProUGUI selectedCharacterNameText;
    [SerializeField] private TextMeshProUGUI selectedCharacterLoreText;
    [SerializeField] private TextMeshProUGUI combinedStatsSummaryText;

    [Header("4. Hub Upgrades (Vitality / Stamina / Raw Power)")]
    [Tooltip("Use GDD scaling cost formula: BaseCost * (Level * 1.5).")]
    [SerializeField] private bool useGddUpgradeCostFormula = true;
    [SerializeField] private Button upgradeMaxHpButton;
    [SerializeField] private TextMeshProUGUI upgradeMaxHpText;
    [SerializeField] private int baseHpUpgradeCost = 30;
    [SerializeField] private int hpCostIncreasePerLevel = 15;
    [SerializeField] private float bonusMaxHpPerUpgrade = 25f;

    [SerializeField] private Button upgradeStaminaButton;
    [SerializeField] private TextMeshProUGUI upgradeStaminaText;
    [SerializeField] private int baseStaminaUpgradeCost = 25;
    [SerializeField] private int staminaCostIncreasePerLevel = 15;
    [SerializeField] private float bonusStaminaRegenPerUpgrade = 4.0f;
    [SerializeField] private float bonusDodgeReductionPerUpgrade = 1.5f;

    [SerializeField] private Button upgradeAttackButton;
    [SerializeField] private TextMeshProUGUI upgradeAttackText;
    [SerializeField] private int baseAttackCoreCost = 1;
    [SerializeField] private float bonusAttackPerUpgrade = 4f;

    [Header("5. Optional Mascot Dialogue & Milestone Theme")]
    [SerializeField] private TextMeshProUGUI milestoneFlavorText;
    [SerializeField] private Image[] milestoneAccentImages;
    [SerializeField] private Button mascotInteractButton;
    [SerializeField] private Image mascotPortraitImage;
    [SerializeField] private GameObject mascotDialogueBoxPanel;
    [SerializeField] private TextMeshProUGUI mascotSpeakerNameText;
    [SerializeField] private TextMeshProUGUI mascotDialogueBodyText;
    [SerializeField] private bool showMascotGreetingOnEnterHub = true;
    [SerializeField] private bool useTypewriterEffectOnDialogue = true;
    [SerializeField] private float typewriterCharInterval = 0.02f;
    [Tooltip("Disabled by default so upgrade buttons stay clean without multi-line text overflow.")]
    [SerializeField] private bool appendItemLoreUnderUpgrades = false;
    [SerializeField] private TextMeshProUGUI upgrade1VitalityLoreText;
    [SerializeField] private TextMeshProUGUI upgrade2StaminaLoreText;
    [SerializeField] private TextMeshProUGUI upgrade3RawPowerLoreText;

    [Header("6. Settings Panel")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private Button toggleFullscreenButton;
    [SerializeField] private TextMeshProUGUI fullscreenStatusText;
    [SerializeField] private Button resetSaveDataButton;
    [SerializeField] private Button closeSettingsButton;

    private int currentCharacterIndex = 0;
    private int lastMascotQuoteIndex = -1;
    private Coroutine mascotTypewriterCoroutine;
    private WaitForSecondsRealtime cachedTypewriterWait;
    private float cachedTypewriterInterval = -1f;

    private void Reset()
    {
        EnsureDefaultCharacters();
    }

    private void Awake()
    {
        EnsureDefaultCharacters();
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameProgressionManager.Instance.LoadFromPrefs();

        ConfigureMouseScrollRect();
        BindHubListeners();
        BindSettingsPanelListeners();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        int savedCharIdx = Mathf.Clamp(GameProgressionManager.Instance.SelectedCharacterIndex, 0, Mathf.Max(0, characters.Count - 1));
        SelectCharacterByIndex(savedCharIdx);
        RefreshSettingsUI();

        if (showMascotGreetingOnEnterHub)
        {
            TriggerRandomMascotDialogue();
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && settingsPanel != null)
        {
            ToggleSettingsPanel();
            return;
        }
#endif

        if (settingsPanel != null && settingsPanel.activeInHierarchy)
            return;

        HandleMouseWheelHorizontalScroll();
    }

    private void ConfigureMouseScrollRect()
    {
        if (characterScrollRect == null) return;

        characterScrollRect.horizontal = true;
        characterScrollRect.vertical = false;
        characterScrollRect.inertia = true;
        characterScrollRect.decelerationRate = 0.12f;
        characterScrollRect.scrollSensitivity = 40f;

        if (characterScrollRect.content != null && !characterScrollRect.content.TryGetComponent<Graphic>(out _))
        {
            Image raycastImg = characterScrollRect.content.gameObject.AddComponent<Image>();
            raycastImg.color = new Color(0f, 0f, 0f, 0f);
            raycastImg.raycastTarget = true;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].cardButton != null)
            {
                var forwarder = characters[i].cardButton.GetComponent<ScrollRectDragForwarder>()
                             ?? characters[i].cardButton.gameObject.AddComponent<ScrollRectDragForwarder>();
                forwarder.Initialize(characterScrollRect);
            }
        }
    }

    private void HandleMouseWheelHorizontalScroll()
    {
        if (characterScrollRect == null) return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.1f)
            {
                float delta = -Mathf.Sign(wheel) * mouseWheelScrollSensitivity;
                characterScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(characterScrollRect.horizontalNormalizedPosition + delta);
            }
        }
#endif
    }

    private void EnsureDefaultCharacters()
    {
        if (characters == null || characters.Count == 0)
        {
            characters = new List<CharacterProfileData>
            {
                new CharacterProfileData
                {
                    characterId = GameTextMasterConfig.CharacterIds[0],
                    characterName = GameTextMasterConfig.CharacterNames[0],
                    combatArchetype = CharacterCombatArchetype.MeleeStriker,
                    characterRoleAndLore = GameTextMasterConfig.CharacterLores[0],
                    baseMaxHealth = 110f,
                    baseAttackDamage = 24f,
                    baseMoveSpeed = 5.3f,
                    themeColor = new Color(0.92f, 0.28f, 0.58f, 1f)
                },
                new CharacterProfileData
                {
                    characterId = GameTextMasterConfig.CharacterIds[1],
                    characterName = GameTextMasterConfig.CharacterNames[1],
                    combatArchetype = CharacterCombatArchetype.HeavyBreaker,
                    characterRoleAndLore = GameTextMasterConfig.CharacterLores[1],
                    baseMaxHealth = 140f,
                    baseAttackDamage = 30f,
                    baseMoveSpeed = 4.8f,
                    themeColor = new Color(0.90f, 0.24f, 0.28f, 1f)
                },
                new CharacterProfileData
                {
                    characterId = GameTextMasterConfig.CharacterIds[2],
                    characterName = GameTextMasterConfig.CharacterNames[2],
                    combatArchetype = CharacterCombatArchetype.RangedCaster,
                    characterRoleAndLore = GameTextMasterConfig.CharacterLores[2],
                    baseMaxHealth = 95f,
                    baseAttackDamage = 20f,
                    baseMoveSpeed = 5.1f,
                    themeColor = new Color(0.28f, 0.52f, 0.92f, 1f)
                }
            };
        }

        // Single Source of Truth: sync each card from its assigned CharacterData_SO (either per-card or characterDataAssets[i])
        float[] safeHp = { 110f, 140f, 95f };
        float[] safeAtk = { 24f, 30f, 20f };
        float[] safeSpd = { 5.3f, 4.8f, 5.1f };

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterProfileData card = characters[i];
            if (card == null) continue;

            CharacterData_SO so = ResolveCharacterDataSO(i);
            if (so != null)
            {
                card.characterData = so;
                card.characterId = so.characterID;
                card.characterName = so.characterDisplayName;
                card.combatArchetype = so.combatArchetype;
                card.characterRoleAndLore = so.roleDescription;
                card.baseMaxHealth = so.baseMaxHP;
                card.baseAttackDamage = so.baseATK;
                card.baseMoveSpeed = so.moveSpeed;
                card.themeColor = so.themeColor;
            }
            else
            {
                int idx = Mathf.Clamp(i, 0, 2);
                if (card.baseMaxHealth <= 0f) card.baseMaxHealth = safeHp[idx];
                if (card.baseAttackDamage <= 0f) card.baseAttackDamage = safeAtk[idx];
                if (card.baseMoveSpeed <= 0f) card.baseMoveSpeed = safeSpd[idx];
            }
        }
    }

    private CharacterData_SO ResolveCharacterDataSO(int index)
    {
        if (characters != null && index >= 0 && index < characters.Count && characters[index] != null && characters[index].characterData != null)
            return characters[index].characterData;

        if (characterDataAssets != null && index >= 0 && index < characterDataAssets.Count && characterDataAssets[index] != null)
            return characterDataAssets[index];

        return null;
    }

    private void BindHubListeners()
    {
        BindButton(startBattleButton, OnStartBattleClicked);
        BindButton(backToMainMenuButton, OnBackToMainMenuClicked);
        BindButton(quitButton, OnQuitClicked);
        BindButton(upgradeMaxHpButton, OnUpgradeMaxHpClicked);
        BindButton(upgradeStaminaButton, OnUpgradeStaminaClicked);
        BindButton(upgradeAttackButton, OnUpgradeAttackClicked);

        if (mascotInteractButton != null)
            BindButton(mascotInteractButton, OnTalkToMascotClicked);
        else if (mascotPortraitImage != null && mascotPortraitImage.TryGetComponent<Button>(out var portraitBtn))
            BindButton(portraitBtn, OnTalkToMascotClicked);

        for (int i = 0; i < characters.Count; i++)
        {
            int capturedIdx = i;
            if (characters[i] != null && characters[i].cardButton != null)
            {
                characters[i].cardButton.onClick.RemoveAllListeners();
                characters[i].cardButton.onClick.AddListener(() => SelectCharacterByIndex(capturedIdx));
            }
        }
    }

    private void BindSettingsPanelListeners()
    {
        BindButton(openSettingsButton, OpenSettingsPanel);
        BindButton(closeSettingsButton, CloseSettingsPanel);
        BindButton(toggleFullscreenButton, OnToggleFullscreenClicked);
        BindButton(resetSaveDataButton, OnResetSaveDataClicked);

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(GameProgressionManager.Instance.MasterVolume);
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveAllListeners();
            sensitivitySlider.minValue = 0.2f;
            sensitivitySlider.maxValue = 3.0f;
            sensitivitySlider.SetValueWithoutNotify(GameProgressionManager.Instance.MouseSensitivity);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderChanged);
        }
    }

    private static void BindButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    // =========================================================================
    // SETTINGS PANEL
    // =========================================================================

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;
        if (settingsPanel.activeInHierarchy) CloseSettingsPanel();
        else OpenSettingsPanel();
    }

    public void OpenSettingsPanel()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(true);
        RefreshSettingsUI();
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnVolumeSliderChanged(float val)
    {
        GameProgressionManager.Instance.SetMasterVolume(val);
        RefreshSettingsUI();
    }

    public void OnSensitivitySliderChanged(float val)
    {
        GameProgressionManager.Instance.SetMouseSensitivity(val);
        RefreshSettingsUI();
    }

    public void OnToggleFullscreenClicked()
    {
        Screen.fullScreen = !Screen.fullScreen;
        RefreshSettingsUI();
    }

    public void OnResetSaveDataClicked()
    {
        GameProgressionManager.Instance.ResetAllSaveData();
        SelectCharacterByIndex(0);
        RefreshSettingsUI();
    }

    private void RefreshSettingsUI()
    {
        var prog = GameProgressionManager.Instance;
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(prog.MasterVolume);
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(prog.MouseSensitivity);
        if (volumeValueText != null)
            volumeValueText.text = string.Format(GameTextMasterConfig.Settings_VolumeValueFormat, Mathf.RoundToInt(prog.MasterVolume * 100f));
        if (sensitivityValueText != null)
            sensitivityValueText.text = string.Format(GameTextMasterConfig.Settings_SensitivityValueFormat, prog.MouseSensitivity);
        if (fullscreenStatusText != null)
            fullscreenStatusText.text = Screen.fullScreen ? GameTextMasterConfig.Settings_FullscreenOn : GameTextMasterConfig.Settings_FullscreenOff;
    }

    // =========================================================================
    // CHARACTER SELECTION & HUB UPGRADES
    // =========================================================================

    public void SelectCharacterByIndex(int index)
    {
        if (characters == null || characters.Count == 0) return;
        currentCharacterIndex = Mathf.Clamp(index, 0, characters.Count - 1);

        CharacterProfileData selected = characters[currentCharacterIndex];
        CharacterData_SO resolvedSo = ResolveCharacterDataSO(currentCharacterIndex);

        if (resolvedSo != null)
        {
            GameProgressionManager.Instance.SetActiveCharacterData(resolvedSo, currentCharacterIndex);
        }
        else if (selected != null)
        {
            GameProgressionManager.Instance.SelectCharacter(
                currentCharacterIndex,
                selected.characterName,
                selected.baseMaxHealth,
                selected.baseAttackDamage,
                selected.baseMoveSpeed,
                selected.combatArchetype);
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterProfileData card = characters[i];
            if (card == null) continue;

            bool isSelected = (i == currentCharacterIndex);
            if (card.characterPreviewObject != null)
                card.characterPreviewObject.SetActive(isSelected);

            Image bg = card.cardBackground != null
                ? card.cardBackground
                : (card.cardButton != null ? card.cardButton.GetComponent<Image>() : null);

            if (bg != null)
            {
                Color baseCol = card.themeColor;
                bg.color = isSelected
                    ? baseCol
                    : new Color(baseCol.r * 0.45f, baseCol.g * 0.45f, baseCol.b * 0.45f, 0.85f);
                bg.transform.localScale = isSelected ? Vector3.one * 1.06f : Vector3.one * 0.94f;
            }
        }

        RefreshHubUI();
    }

    public int GetCurrentHpUpgradeCost()
    {
        int lv = GameProgressionManager.Instance.HpUpgradeLevel;
        if (useGddUpgradeCostFormula)
            return lv <= 0 ? baseHpUpgradeCost : Mathf.RoundToInt(baseHpUpgradeCost * (lv * 1.5f));
        return baseHpUpgradeCost + (lv * hpCostIncreasePerLevel);
    }

    public int GetCurrentStaminaUpgradeCost()
    {
        int lv = GameProgressionManager.Instance.StaminaUpgradeLevel;
        if (useGddUpgradeCostFormula)
            return lv <= 0 ? baseStaminaUpgradeCost : Mathf.RoundToInt(baseStaminaUpgradeCost * (lv * 1.5f));
        return baseStaminaUpgradeCost + (lv * staminaCostIncreasePerLevel);
    }

    public void OnUpgradeMaxHpClicked()
    {
        if (GameProgressionManager.Instance.TryUpgradeMaxHealth(GetCurrentHpUpgradeCost(), bonusMaxHpPerUpgrade))
        {
            AudioManager.PlayUI(AudioKeys.UI_Upgrade);
            RefreshHubUI();
        }
        else
        {
            AudioManager.PlayUI(AudioKeys.UI_Error);
        }
    }

    public void OnUpgradeStaminaClicked()
    {
        if (GameProgressionManager.Instance.TryUpgradeStaminaEfficiency(GetCurrentStaminaUpgradeCost(), bonusStaminaRegenPerUpgrade, bonusDodgeReductionPerUpgrade))
        {
            AudioManager.PlayUI(AudioKeys.UI_Upgrade);
            RefreshHubUI();
        }
        else
        {
            AudioManager.PlayUI(AudioKeys.UI_Error);
        }
    }

    public void OnUpgradeAttackClicked()
    {
        if (GameProgressionManager.Instance.TryUpgradeAttack(baseAttackCoreCost, bonusAttackPerUpgrade))
        {
            AudioManager.PlayUI(AudioKeys.UI_Upgrade);
            RefreshHubUI();
        }
        else
        {
            AudioManager.PlayUI(AudioKeys.UI_Error);
        }
    }

    public void RefreshHubUI()
    {
        var prog = GameProgressionManager.Instance;
        int totalStarDust = prog.StarDust;
        int totalShatteredCore = prog.ShatteredCore;

        ApplyMilestoneThemeColorShift(prog.GetProgressionUiThemeColor());

        if (starDustText != null)
        {
            if (shatteredCoreText != null)
            {
                starDustText.text = string.Format(GameTextMasterConfig.Lobby_StarDustFormat, totalStarDust);
                shatteredCoreText.text = string.Format(GameTextMasterConfig.Lobby_ShatteredCoreFormat, totalShatteredCore);
            }
            else
            {
                starDustText.text = $"GOLD : {totalStarDust}  |  CORE : {totalShatteredCore}";
            }
        }

        CharacterProfileData currentChar = (characters != null && characters.Count > 0)
            ? characters[Mathf.Clamp(currentCharacterIndex, 0, characters.Count - 1)]
            : null;

        float baseHp = currentChar != null ? currentChar.baseMaxHealth : 110f;
        float baseAtk = currentChar != null ? currentChar.baseAttackDamage : 24f;

        if (selectedCharacterNameText != null && currentChar != null)
            selectedCharacterNameText.text = string.Format(GameTextMasterConfig.Lobby_SelectedCharTitleFormat, currentChar.characterName, prog.CurrentRankLetter);

        if (selectedCharacterLoreText != null && currentChar != null)
            selectedCharacterLoreText.text = string.Format(GameTextMasterConfig.Lobby_SelectedCharLoreFormat, currentChar.characterRoleAndLore, prog.CurrentRankStoryLine);

        float totalHp = baseHp + prog.BonusMaxHealth;
        float totalAtk = baseAtk + prog.BonusAttackDamage;

        if (combinedStatsSummaryText != null)
        {
            combinedStatsSummaryText.text = string.Format(
                GameTextMasterConfig.Lobby_CombinedStatsSummaryFormat,
                prog.CurrentRankLetter,
                prog.CurrentRankZoneName,
                totalHp,
                baseHp,
                prog.BonusMaxHealth,
                prog.HpUpgradeLevel,
                prog.BonusStaminaRegen,
                prog.BonusDodgeCostReduction,
                prog.StaminaUpgradeLevel,
                totalAtk,
                baseAtk,
                prog.BonusAttackDamage,
                prog.AttackUpgradeLevel,
                prog.GetMilestoneFlavorText());
        }

        if (milestoneFlavorText != null)
            milestoneFlavorText.text = $"{prog.CurrentRankStoryLine}\n{prog.GetMilestoneFlavorText()}";

        int hpCost = GetCurrentHpUpgradeCost();
        if (upgradeMaxHpText != null)
        {
            string baseUpg1 = string.Format(GameTextMasterConfig.Lobby_Upgrade1_VitalityFormat, bonusMaxHpPerUpgrade, hpCost, prog.HpUpgradeLevel, totalHp);
            upgradeMaxHpText.text = (appendItemLoreUnderUpgrades && upgrade1VitalityLoreText == null)
                ? $"{baseUpg1}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade1_VitalityItemLore}</i></color></size>"
                : baseUpg1;
        }
        if (upgrade1VitalityLoreText != null)
            upgrade1VitalityLoreText.text = GameTextMasterConfig.Story_Upgrade1_VitalityItemLore;
        if (upgradeMaxHpButton != null)
            upgradeMaxHpButton.interactable = (totalStarDust >= hpCost);

        int stamCost = GetCurrentStaminaUpgradeCost();
        if (upgradeStaminaText != null)
        {
            string baseUpg2 = string.Format(GameTextMasterConfig.Lobby_Upgrade2_StaminaFormat, bonusStaminaRegenPerUpgrade, bonusDodgeReductionPerUpgrade, stamCost, prog.StaminaUpgradeLevel);
            upgradeStaminaText.text = (appendItemLoreUnderUpgrades && upgrade2StaminaLoreText == null)
                ? $"{baseUpg2}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade2_StaminaItemLore}</i></color></size>"
                : baseUpg2;
        }
        if (upgrade2StaminaLoreText != null)
            upgrade2StaminaLoreText.text = GameTextMasterConfig.Story_Upgrade2_StaminaItemLore;
        if (upgradeStaminaButton != null)
            upgradeStaminaButton.interactable = (totalStarDust >= stamCost);

        if (upgradeAttackText != null)
        {
            string baseUpg3 = string.Format(GameTextMasterConfig.Lobby_Upgrade3_RawPowerFormat, bonusAttackPerUpgrade, baseAttackCoreCost, prog.AttackUpgradeLevel, totalAtk);
            upgradeAttackText.text = (appendItemLoreUnderUpgrades && upgrade3RawPowerLoreText == null)
                ? $"{baseUpg3}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade3_RawPowerItemLore}</i></color></size>"
                : baseUpg3;
        }
        if (upgrade3RawPowerLoreText != null)
            upgrade3RawPowerLoreText.text = GameTextMasterConfig.Story_Upgrade3_RawPowerItemLore;
        if (upgradeAttackButton != null)
            upgradeAttackButton.interactable = (totalShatteredCore >= baseAttackCoreCost);
    }

    private void ApplyMilestoneThemeColorShift(Color milestoneColor)
    {
        if (milestoneAccentImages != null)
        {
            for (int i = 0; i < milestoneAccentImages.Length; i++)
            {
                if (milestoneAccentImages[i] != null)
                    milestoneAccentImages[i].color = milestoneColor;
            }
        }

        if (startBattleButton != null && startBattleButton.TryGetComponent<Image>(out var startBtnImg))
            startBtnImg.color = milestoneColor;
    }

    // =========================================================================
    // OPTIONAL MASCOT DIALOGUE (ZERO-GC TMP maxVisibleCharacters TYPEWRITER)
    // =========================================================================

    public void OnTalkToMascotClicked() => TriggerRandomMascotDialogue();

    public void TriggerRandomMascotDialogue()
    {
        TextMeshProUGUI targetText = mascotDialogueBodyText != null ? mascotDialogueBodyText : milestoneFlavorText;
        if (targetText == null) return;

        string[] pool = GameTextMasterConfig.Story_MascotRandomDialogues;
        if (pool == null || pool.Length == 0) return;

        int nextIdx = Random.Range(0, pool.Length);
        if (pool.Length > 1 && nextIdx == lastMascotQuoteIndex)
            nextIdx = (nextIdx + 1) % pool.Length;
        lastMascotQuoteIndex = nextIdx;

        string chosenLine = pool[nextIdx];

        if (mascotDialogueBoxPanel != null)
            mascotDialogueBoxPanel.SetActive(true);
        if (mascotSpeakerNameText != null)
            mascotSpeakerNameText.text = GameTextMasterConfig.Story_MascotSpeakerName;

        if (mascotTypewriterCoroutine != null)
        {
            StopCoroutine(mascotTypewriterCoroutine);
            mascotTypewriterCoroutine = null;
        }

        if (useTypewriterEffectOnDialogue && gameObject.activeInHierarchy)
        {
            mascotTypewriterCoroutine = StartCoroutine(TypeMascotDialogueRoutine(targetText, chosenLine));
        }
        else
        {
            targetText.text = chosenLine;
            targetText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private IEnumerator TypeMascotDialogueRoutine(TextMeshProUGUI label, string fullLine)
    {
        float safeInterval = Mathf.Max(0.005f, typewriterCharInterval);
        if (cachedTypewriterWait == null || Mathf.Abs(cachedTypewriterInterval - safeInterval) > 0.0001f)
        {
            cachedTypewriterInterval = safeInterval;
            cachedTypewriterWait = new WaitForSecondsRealtime(safeInterval);
        }

        label.text = fullLine;
        label.ForceMeshUpdate();
        int totalVisible = label.textInfo.characterCount;
        label.maxVisibleCharacters = 0;

        for (int i = 1; i <= totalVisible; i++)
        {
            label.maxVisibleCharacters = i;
            yield return cachedTypewriterWait;
        }

        label.maxVisibleCharacters = int.MaxValue;
        mascotTypewriterCoroutine = null;
    }

    // =========================================================================
    // SCENE TRANSITIONS
    // =========================================================================

    public void OnStartBattleClicked()
    {
        AudioManager.PlayUI(AudioKeys.UI_Confirm);
        if (Application.CanStreamedLevelBeLoaded(stageMapSceneName))
            SceneManager.LoadScene(stageMapSceneName);
        else
            SceneManager.LoadScene(fallbackActionSceneName);
    }

    public void OnBackToMainMenuClicked()
    {
        AudioManager.PlayUI(AudioKeys.UI_Back);
        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
