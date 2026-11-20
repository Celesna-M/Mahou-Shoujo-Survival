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
    public string characterId = "CHAR_01";
    public string characterName = "01 : HIKARI - BRAWLER STRIKER";
    [TextArea(1, 2)]
    public string characterRoleAndLore = "Balanced Striker | High mobility and fast melee combos.";
    public float baseMaxHealth = 100f;
    public float baseAttackDamage = 25f;
    public float baseMoveSpeed = 5.2f;
    public Color themeColor = new Color(0.92f, 0.28f, 0.58f, 1f);

    [Header("UI & 3D References")]
    public Button cardButton;
    public Image cardBackground;
    public GameObject characterPreviewObject;
}

public class ScrollRectDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private ScrollRect targetScrollRect;

    public void Initialize(ScrollRect scrollRect)
    {
        targetScrollRect = scrollRect;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnEndDrag(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnScroll(eventData);
    }
}

/// <summary>
/// Scene 2: The Hub / Lobby Manager
/// - Pulls ALL auto-generated strings & templates directly from GameTextMasterConfig!
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Energy Recovery in Hub (แก้ปัญหา Energy หมดแล้วติดลูปตัน)")]
    [Tooltip("รีฟิลพลังงาน (Play Energy) ให้เต็มอัตโนมัติเมื่อผู้เล่นกลับมาพักที่ห้อง The Hub")]
    [SerializeField] private bool autoRefillEnergyOnEnterHub = true;

    [Header("Scene Navigation (4-Scene Flow)")]
    [SerializeField] private string stageMapSceneName = "StageMap";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string fallbackActionSceneName = "The Action";

    [Header("Main Navigation Buttons")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Economy & Energy Display")]
    [SerializeField] private TextMeshProUGUI starDustText;
    [SerializeField] private TextMeshProUGUI shatteredCoreText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Button refillEnergyButton;
    [SerializeField] private int energyRefillStarDustCost = 20;

    [Header("GDD Data-Driven Character Roster (ลาก CharacterData_SO มาใส่เพื่อส่ง 2D Portrait เข้าฉาก VN)")]
    [SerializeField] private List<CharacterData_SO> characterDataAssets = new List<CharacterData_SO>();
    [Tooltip("ใช้สูตรราคาอัปเกรดตาม GDD Section 5: Base Cost * (Current Level * 1.5)")]
    [SerializeField] private bool useGddUpgradeCostFormula = true;

    [Header("Character Selection (Mouse Drag & Scroll Wheel)")]
    [SerializeField] private ScrollRect characterScrollRect;
    [SerializeField] private float mouseWheelScrollSensitivity = 0.15f;
    [SerializeField] private List<CharacterProfileData> characters = new List<CharacterProfileData>();

    [Header("Character Info, Rank & Combined Stats Display")]
    [SerializeField] private TextMeshProUGUI selectedCharacterNameText;
    [SerializeField] private TextMeshProUGUI selectedCharacterLoreText;
    [SerializeField] private TextMeshProUGUI combinedStatsSummaryText;

    [Header("1. Vitality Boost (Max HP Upgrade)")]
    [SerializeField] private Button upgradeMaxHpButton;
    [SerializeField] private TextMeshProUGUI upgradeMaxHpText;
    [SerializeField] private int baseHpUpgradeCost = 30;
    [SerializeField] private int hpCostIncreasePerLevel = 15;
    [SerializeField] private float bonusMaxHpPerUpgrade = 25f;

    [Header("2. Stamina Efficiency (Dodge Cost & Stamina Regen Upgrade)")]
    [SerializeField] private Button upgradeStaminaButton;
    [SerializeField] private TextMeshProUGUI upgradeStaminaText;
    [SerializeField] private int baseStaminaUpgradeCost = 25;
    [SerializeField] private int staminaCostIncreasePerLevel = 15;
    [SerializeField] private float bonusStaminaRegenPerUpgrade = 4.0f;
    [SerializeField] private float bonusDodgeReductionPerUpgrade = 1.5f;

    [Header("3. Raw Power (Base Attack Upgrade)")]
    [SerializeField] private Button upgradeAttackButton;
    [SerializeField] private TextMeshProUGUI upgradeAttackText;
    [SerializeField] private int baseAttackCoreCost = 1;
    [SerializeField] private float bonusAttackPerUpgrade = 5f;

    [Header("Milestone Unlocks (Flavor Text & Soft Pink -> Deep Crimson UI Shift)")]
    [SerializeField] private TextMeshProUGUI milestoneFlavorText;
    [SerializeField] private Image[] milestoneAccentImages;

        [Header("Hybrid Storytelling 1: Hub 2D Mascot Dialogue (ฝั่งซ้าย: ตุ๊กตามาสคอต + กล่องข้อความ)")]
    [Tooltip("ปุ่มกดสำรวจ/พูดคุยกับตุ๊กตามาสคอตที่มุมห้อง (หรือกดที่รูปมาสคอตโดยตรง)")]
    [SerializeField] private Button mascotInteractButton;
    [Tooltip("รูป 2D Portrait ของตุ๊กตามาสคอตฝั่งซ้าย")]
    [SerializeField] private Image mascotPortraitImage;
    [Tooltip("กรอบหน้าต่างกล่องข้อความของมาสคอต (ถ้ามี)")]
    [SerializeField] private GameObject mascotDialogueBoxPanel;
    [Tooltip("ข้อความชื่อผู้พูด (เช่น KYU-DOLL)")]
    [SerializeField] private TextMeshProUGUI mascotSpeakerNameText;
    [Tooltip("ข้อความบทพูดจิกกัด 1-2 ประโยคของมาสคอต (สุ่มจาก GameTextMasterConfig)")]
    [SerializeField] private TextMeshProUGUI mascotDialogueBodyText;
    [SerializeField] private bool showMascotGreetingOnEnterHub = true;
    [SerializeField] private bool useTypewriterEffectOnDialogue = true;
    [SerializeField] private float typewriterCharInterval = 0.02f;

    [Header("Hybrid Storytelling 2: Item Lore / Flavor Text (ฝั่งขวา: คำอธิบาย Lore ใต้ไอเทมอัปเกรด)")]
    [Tooltip("แสดงบรรทัดคำอธิบายตำนาน (Dark Souls Style Item Lore) ใต้ปุ่มอัปเกรดทั้ง 3 สายอัตโนมัติ")]
    [SerializeField] private bool appendItemLoreUnderUpgrades = true;
    [Tooltip("หากมี Text แยกสำหรับแสดง Item Lore ของแต่ละสาย สามารถลากมาใส่ได้ (ถ้าเว้นว่างไว้จะต่อท้ายในปุ่มอัปเกรดให้อัตโนมัติ)")]
    [SerializeField] private TextMeshProUGUI upgrade1VitalityLoreText;
    [SerializeField] private TextMeshProUGUI upgrade2StaminaLoreText;
    [SerializeField] private TextMeshProUGUI upgrade3RawPowerLoreText;

    [Header("Settings Panel (Available in Lobby Scene)")]
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
        if (autoRefillEnergyOnEnterHub)
        {
            GameProgressionManager.Instance.RefillFullEnergy();
        }

        ConfigureMouseScrollRect();
        BindHubListeners();
        BindSettingsPanelListeners();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        int savedCharIdx = Mathf.Clamp(GameProgressionManager.Instance.SelectedCharacterIndex, 0, Mathf.Max(0, characters.Count - 1));
        SelectCharacterByIndex(savedCharIdx);
        RefreshHubUI();
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
        RefreshEnergyDisplayOnly();
    }

    private void ConfigureMouseScrollRect()
    {
        if (characterScrollRect == null) return;

        characterScrollRect.horizontal = true;
        characterScrollRect.vertical = false;
        characterScrollRect.inertia = true;
        characterScrollRect.decelerationRate = 0.12f;
        characterScrollRect.scrollSensitivity = 40f;

        if (characterScrollRect.content != null)
        {
            if (!characterScrollRect.content.TryGetComponent<Graphic>(out _))
            {
                Image raycastImg = characterScrollRect.content.gameObject.AddComponent<Image>();
                raycastImg.color = new Color(0f, 0f, 0f, 0f);
                raycastImg.raycastTarget = true;
            }
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].cardButton != null)
            {
                var forwarder = characters[i].cardButton.GetComponent<ScrollRectDragForwarder>();
                if (forwarder == null)
                    forwarder = characters[i].cardButton.gameObject.AddComponent<ScrollRectDragForwarder>();
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
        if (characters != null && characters.Count > 0) return;

        characters = new List<CharacterProfileData>
        {
            new CharacterProfileData
            {
                characterId = GameTextMasterConfig.CharacterIds[0],
                characterName = GameTextMasterConfig.CharacterNames[0],
                characterRoleAndLore = GameTextMasterConfig.CharacterLores[0],
                baseMaxHealth = 100f,
                baseAttackDamage = 25f,
                baseMoveSpeed = 5.2f,
                themeColor = new Color(0.92f, 0.28f, 0.58f, 1f)
            },
            new CharacterProfileData
            {
                characterId = GameTextMasterConfig.CharacterIds[1],
                characterName = GameTextMasterConfig.CharacterNames[1],
                characterRoleAndLore = GameTextMasterConfig.CharacterLores[1],
                baseMaxHealth = 90f,
                baseAttackDamage = 34f,
                baseMoveSpeed = 5.0f,
                themeColor = new Color(0.90f, 0.24f, 0.28f, 1f)
            },
            new CharacterProfileData
            {
                characterId = GameTextMasterConfig.CharacterIds[2],
                characterName = GameTextMasterConfig.CharacterNames[2],
                characterRoleAndLore = GameTextMasterConfig.CharacterLores[2],
                baseMaxHealth = 140f,
                baseAttackDamage = 22f,
                baseMoveSpeed = 4.7f,
                themeColor = new Color(0.28f, 0.52f, 0.92f, 1f)
            }
        };
    }

    private void BindHubListeners()
    {
        if (startBattleButton != null)
        {
            startBattleButton.onClick.RemoveAllListeners();
            startBattleButton.onClick.AddListener(OnStartBattleClicked);
        }

        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.RemoveAllListeners();
            backToMainMenuButton.onClick.AddListener(OnBackToMainMenuClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        if (upgradeMaxHpButton != null)
        {
            upgradeMaxHpButton.onClick.RemoveAllListeners();
            upgradeMaxHpButton.onClick.AddListener(OnUpgradeMaxHpClicked);
        }

        if (upgradeStaminaButton != null)
        {
            upgradeStaminaButton.onClick.RemoveAllListeners();
            upgradeStaminaButton.onClick.AddListener(OnUpgradeStaminaClicked);
        }

        if (upgradeAttackButton != null)
        {
            upgradeAttackButton.onClick.RemoveAllListeners();
            upgradeAttackButton.onClick.AddListener(OnUpgradeAttackClicked);
        }

        if (mascotInteractButton != null)
        {
            mascotInteractButton.onClick.RemoveAllListeners();
            mascotInteractButton.onClick.AddListener(OnTalkToMascotClicked);
        }
        else if (mascotPortraitImage != null && mascotPortraitImage.TryGetComponent<Button>(out var portraitBtn))
        {
            portraitBtn.onClick.RemoveAllListeners();
            portraitBtn.onClick.AddListener(OnTalkToMascotClicked);
        }

        if (refillEnergyButton != null)
        {
            refillEnergyButton.onClick.RemoveAllListeners();
            refillEnergyButton.onClick.AddListener(OnRefillEnergyClicked);
        }

        for (int i = 0; i < characters.Count; i++)
        {
            int capturedIdx = i;
            if (characters[i].cardButton != null)
            {
                characters[i].cardButton.onClick.RemoveAllListeners();
                characters[i].cardButton.onClick.AddListener(() => SelectCharacterByIndex(capturedIdx));
            }
        }
    }

    private void BindSettingsPanelListeners()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(CloseSettingsPanel);
        }

        if (toggleFullscreenButton != null)
        {
            toggleFullscreenButton.onClick.RemoveAllListeners();
            toggleFullscreenButton.onClick.AddListener(OnToggleFullscreenClicked);
        }

        if (resetSaveDataButton != null)
        {
            resetSaveDataButton.onClick.RemoveAllListeners();
            resetSaveDataButton.onClick.AddListener(OnResetSaveDataClicked);
        }

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

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;
        if (settingsPanel.activeInHierarchy) CloseSettingsPanel();
        else OpenSettingsPanel();
    }

    public void OpenSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            RefreshSettingsUI();
        }
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
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
        RefreshSettingsUI();
        RefreshHubUI();
    }

    private void RefreshSettingsUI()
    {
        var prog = GameProgressionManager.Instance;
        if (volumeValueText != null)
            volumeValueText.text = string.Format(GameTextMasterConfig.Settings_VolumeValueFormat, Mathf.RoundToInt(prog.MasterVolume * 100f));
        if (sensitivityValueText != null)
            sensitivityValueText.text = string.Format(GameTextMasterConfig.Settings_SensitivityValueFormat, prog.MouseSensitivity);
        if (fullscreenStatusText != null)
            fullscreenStatusText.text = Screen.fullScreen ? GameTextMasterConfig.Settings_FullscreenOn : GameTextMasterConfig.Settings_FullscreenOff;
    }

    public void SelectCharacterByIndex(int index)
    {
        if (characters == null || characters.Count == 0) return;
        currentCharacterIndex = Mathf.Clamp(index, 0, characters.Count - 1);

        CharacterProfileData selected = characters[currentCharacterIndex];

        if (characterDataAssets != null && currentCharacterIndex < characterDataAssets.Count && characterDataAssets[currentCharacterIndex] != null)
        {
            GameProgressionManager.Instance.SetActiveCharacterData(characterDataAssets[currentCharacterIndex]);
        }
        else
        {
            GameProgressionManager.Instance.SelectCharacter(
                currentCharacterIndex,
                selected.characterName,
                selected.baseMaxHealth,
                selected.baseAttackDamage,
                selected.baseMoveSpeed
            );
        }

        for (int i = 0; i < characters.Count; i++)
        {
            bool isSelected = (i == currentCharacterIndex);
            if (characters[i].characterPreviewObject != null)
            {
                characters[i].characterPreviewObject.SetActive(isSelected);
            }

            Image bg = characters[i].cardBackground != null
                ? characters[i].cardBackground
                : (characters[i].cardButton != null ? characters[i].cardButton.GetComponent<Image>() : null);

            if (bg != null)
            {
                Color baseCol = characters[i].themeColor;
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
        int cost = GetCurrentHpUpgradeCost();
        if (GameProgressionManager.Instance.TryUpgradeMaxHealth(cost, bonusMaxHpPerUpgrade))
        {
            RefreshHubUI();
        }
    }

    public void OnUpgradeStaminaClicked()
    {
        int cost = GetCurrentStaminaUpgradeCost();
        if (GameProgressionManager.Instance.TryUpgradeStaminaEfficiency(cost, bonusStaminaRegenPerUpgrade, bonusDodgeReductionPerUpgrade))
        {
            RefreshHubUI();
        }
    }

    public void OnUpgradeAttackClicked()
    {
        if (GameProgressionManager.Instance.TryUpgradeAttack(baseAttackCoreCost, bonusAttackPerUpgrade))
        {
            RefreshHubUI();
        }
    }

    public void OnRefillEnergyClicked()
    {
        if (GameProgressionManager.Instance.TryBuyEnergyWithStarDust(energyRefillStarDustCost, 1))
        {
            RefreshHubUI();
        }
    }

    private void RefreshEnergyDisplayOnly()
    {
        // GDD Core Pillar #3 (Zero Energy Limit): ตัดและซ่อน UI พลังงานทั้งหมดออก
        if (energyText != null && energyText.gameObject.activeSelf)
            energyText.gameObject.SetActive(false);
        if (refillEnergyButton != null && refillEnergyButton.gameObject.activeSelf)
            refillEnergyButton.gameObject.SetActive(false);
    }

    public void RefreshHubUI()
    {
        var prog = GameProgressionManager.Instance;
        int totalStarDust = prog.StarDust;
        int totalShatteredCore = prog.ShatteredCore;

        RefreshEnergyDisplayOnly();
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

        float baseHp = currentChar != null ? currentChar.baseMaxHealth : 100f;
        float baseAtk = currentChar != null ? currentChar.baseAttackDamage : 25f;

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
                prog.GetMilestoneFlavorText()
            );
        }

        if (milestoneFlavorText != null)
        {
            milestoneFlavorText.text = $"{prog.CurrentRankStoryLine}\n{prog.GetMilestoneFlavorText()}";
        }

        int hpCost = GetCurrentHpUpgradeCost();
        if (upgradeMaxHpText != null)
        {
            string baseUpg1 = string.Format(GameTextMasterConfig.Lobby_Upgrade1_VitalityFormat, bonusMaxHpPerUpgrade, hpCost, prog.HpUpgradeLevel, totalHp);
            upgradeMaxHpText.text = (appendItemLoreUnderUpgrades && upgrade1VitalityLoreText == null)
                ? $"{baseUpg1}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade1_VitalityItemLore}</i></color></size>"
                : baseUpg1;
        }
        if (upgrade1VitalityLoreText != null)
        {
            upgrade1VitalityLoreText.text = GameTextMasterConfig.Story_Upgrade1_VitalityItemLore;
        }
        if (upgradeMaxHpButton != null)
        {
            upgradeMaxHpButton.interactable = (totalStarDust >= hpCost);
        }

        int stamCost = GetCurrentStaminaUpgradeCost();
        if (upgradeStaminaText != null)
        {
            string baseUpg2 = string.Format(GameTextMasterConfig.Lobby_Upgrade2_StaminaFormat, bonusStaminaRegenPerUpgrade, bonusDodgeReductionPerUpgrade, stamCost, prog.StaminaUpgradeLevel);
            upgradeStaminaText.text = (appendItemLoreUnderUpgrades && upgrade2StaminaLoreText == null)
                ? $"{baseUpg2}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade2_StaminaItemLore}</i></color></size>"
                : baseUpg2;
        }
        if (upgrade2StaminaLoreText != null)
        {
            upgrade2StaminaLoreText.text = GameTextMasterConfig.Story_Upgrade2_StaminaItemLore;
        }
        if (upgradeStaminaButton != null)
        {
            upgradeStaminaButton.interactable = (totalStarDust >= stamCost);
        }

        if (upgradeAttackText != null)
        {
            string baseUpg3 = string.Format(GameTextMasterConfig.Lobby_Upgrade3_RawPowerFormat, bonusAttackPerUpgrade, baseAttackCoreCost, prog.AttackUpgradeLevel, totalAtk);
            upgradeAttackText.text = (appendItemLoreUnderUpgrades && upgrade3RawPowerLoreText == null)
                ? $"{baseUpg3}\n<size=14><color=#B8A8C8><i>{GameTextMasterConfig.Story_Upgrade3_RawPowerItemLore}</i></color></size>"
                : baseUpg3;
        }
        if (upgrade3RawPowerLoreText != null)
        {
            upgrade3RawPowerLoreText.text = GameTextMasterConfig.Story_Upgrade3_RawPowerItemLore;
        }
        if (upgradeAttackButton != null)
        {
            upgradeAttackButton.interactable = (totalShatteredCore >= baseAttackCoreCost);
        }
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
        {
            startBtnImg.color = milestoneColor;
        }
    }

    
    // =========================================================================
    // HYBRID STORYTELLING 1: 2D MASCOT DIALOGUE IN THE HUB (RANDOM CYNICAL QUOTES)
    // =========================================================================

    private int lastMascotQuoteIndex = -1;
    private System.Collections.IEnumerator mascotTypewriterCoroutine;

    public void OnTalkToMascotClicked()
    {
        TriggerRandomMascotDialogue();
    }

    public void TriggerRandomMascotDialogue()
    {
        string[] pool = GameTextMasterConfig.Story_MascotRandomDialogues;
        if (pool == null || pool.Length == 0) return;

        int nextIdx = UnityEngine.Random.Range(0, pool.Length);
        if (pool.Length > 1 && nextIdx == lastMascotQuoteIndex)
        {
            nextIdx = (nextIdx + 1) % pool.Length;
        }
        lastMascotQuoteIndex = nextIdx;

        string chosenLine = pool[nextIdx];

        if (mascotDialogueBoxPanel != null)
            mascotDialogueBoxPanel.SetActive(true);

        if (mascotSpeakerNameText != null)
            mascotSpeakerNameText.text = GameTextMasterConfig.Story_MascotSpeakerName;

        TextMeshProUGUI targetText = mascotDialogueBodyText != null ? mascotDialogueBodyText : milestoneFlavorText;
        if (targetText == null) return;

        if (mascotTypewriterCoroutine != null)
        {
            StopCoroutine(mascotTypewriterCoroutine);
            mascotTypewriterCoroutine = null;
        }

        if (useTypewriterEffectOnDialogue && gameObject.activeInHierarchy)
        {
            mascotTypewriterCoroutine = TypeMascotDialogueRoutine(targetText, chosenLine);
            StartCoroutine(mascotTypewriterCoroutine);
        }
        else
        {
            targetText.text = chosenLine;
        }
    }

    private System.Collections.IEnumerator TypeMascotDialogueRoutine(TextMeshProUGUI label, string fullLine)
    {
        label.text = "";
        for (int i = 0; i < fullLine.Length; i++)
        {
            if (fullLine[i] == '<')
            {
                int closeIdx = fullLine.IndexOf('>', i);
                if (closeIdx != -1)
                {
                    i = closeIdx;
                }
            }
            label.text = fullLine.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.005f, typewriterCharInterval));
        }
        label.text = fullLine;
        mascotTypewriterCoroutine = null;
    }

    public void OnStartBattleClicked()
    {
        if (Application.CanStreamedLevelBeLoaded(stageMapSceneName))
        {
            SceneManager.LoadScene(stageMapSceneName);
        }
        else
        {
            SceneManager.LoadScene(fallbackActionSceneName);
        }
    }

    public void OnBackToMainMenuClicked()
    {
        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
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


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน LobbyManager.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Start() & ConfigureMouseScrollRect()
 *    - โหลดข้อมูลเซฟจาก GameProgressionManager และตั้งค่า ScrollRect สำหรับใช้เมาส์ลากหรือหมุนลูกกลิ้งเลือกตัวละครซ้าย-ขวา
 *    - ผูก ScrollRectDragForwarder เข้ากับการ์ดตัวละครแต่ละใบเพื่อให้คลิกค้างบนการ์ดแล้วลากเลื่อนแถบได้
 *
 * 2. SelectCharacterByIndex(int index)
 *    - เลือกตัวละครตามลำดับ (0 = HIKARI, 1 = AKANE, 2 = SHIRO) บันทึกสเตตัสพื้นฐานเข้า GameProgressionManager
 *      และเปลี่ยนสี/ขนาดการ์ดที่ถูกเลือกให้โดดเด่นขึ้น
 *
 * 3. OnUpgradeMaxHpClicked(), OnUpgradeStaminaClicked(), OnUpgradeAttackClicked()
 *    - ฟังก์ชันสำหรับปุ่มอัปเกรดถาวรทั้ง 3 สายใน Hub:
 *      • สายที่ 1 (Vitality Boost): ใช้ Star Dust เพิ่มเลือดสูงสุด (Max HP)
 *      • สายที่ 2 (Stamina Efficiency): ใช้ Star Dust เพิ่มความเร็วรีเจน Stamina และลดการใช้ Stamina ตอนกลิ้งหลบ
 *      • สายที่ 3 (Raw Power): ใช้ Shattered Core เพิ่มดาเมจโจมตีพื้นฐาน (Attack Damage)
 *
 * 4. RefreshHubUI() & ApplyMilestoneThemeColorShift()
 *    - อัปเดตตัวเลข Star Dust, Shattered Core, พลังงาน (Play Energy), ระดับ Rank (E ถึง S),
 *      เนื้อเรื่อง 1 บรรทัด, และข้อความลับ (Milestone Flavor Text) ขึ้นหน้าจอ
 *    - เปลี่ยนโทนสีของปุ่มหลักและกรอบ UI จากสีชมพูอ่อนเป็นสีแดงเข้มตามระดับ Milestone ของผู้เล่น
 *
 * 5. autoRefillEnergyOnEnterHub (ระบบป้องกัน Energy หมดแล้วติดลูปตัน)
 *    - รีฟิลค่าพลังงาน (Play Energy) ให้เต็มอัตโนมัติทันทีที่ผู้เล่นกลับเข้าสู่ห้องพัก (The Hub)
 *
 * 6. OnTalkToMascotClicked() & TriggerRandomMascotDialogue() (ระบบเล่าเรื่อง Hybrid ใน Hub)
 *    - ฝั่งซ้าย (Visual Novel 2D Mascot): เมื่อกดสำรวจตุ๊กตามาสคอต จะสุ่มประโยคจิกกัดสั้นๆ 1-2 ประโยคจาก GameTextMasterConfig
 *      มาแสดงพร้อมเอฟเฟกต์พิมพ์ดีด (Typewriter)
 *    - ฝั่งขวา (Item Lore / Flavor Text): แสดงคำอธิบายตำนานมืดหม่นใต้ไอเทมอัปเกรดทั้ง 3 สายโดยไม่ขัดจังหวะการเล่น
 * ============================================================================ */