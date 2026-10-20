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
/// Scene 2: Lobby / Hub Manager (The lobby)
/// - Mouse Drag & Wheel Character Selection
/// - Pure ASCII English UI Text (No unsupported symbols)
/// - Play Energy Limit Display & Refill
/// - Full Settings Panel support in Lobby
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Scene Navigation (4-Scene Flow)")]
    [SerializeField] private string stageMapSceneName = "StageMap";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string fallbackActionSceneName = "The Action";

    [Header("Main Navigation Buttons")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Economy & Energy Display (Pure English Text)")]
    [SerializeField] private TextMeshProUGUI starDustText;
    [SerializeField] private TextMeshProUGUI shatteredCoreText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Button refillEnergyButton;
    [SerializeField] private int energyRefillStarDustCost = 20;

    [Header("Character Selection (Mouse Drag & Scroll Wheel)")]
    [SerializeField] private ScrollRect characterScrollRect;
    [SerializeField] private float mouseWheelScrollSensitivity = 0.15f;
    [SerializeField] private List<CharacterProfileData> characters = new List<CharacterProfileData>();

    [Header("Character Info & Combined Stats Display")]
    [SerializeField] private TextMeshProUGUI selectedCharacterNameText;
    [SerializeField] private TextMeshProUGUI selectedCharacterLoreText;
    [SerializeField] private TextMeshProUGUI combinedStatsSummaryText;

    [Header("Linear Upgrade System")]
    [SerializeField] private Button upgradeMaxHpButton;
    [SerializeField] private TextMeshProUGUI upgradeMaxHpText;
    [SerializeField] private int baseHpUpgradeCost = 30;
    [SerializeField] private int hpCostIncreasePerLevel = 15;
    [SerializeField] private float bonusMaxHpPerUpgrade = 25f;

    [SerializeField] private Button upgradeAttackButton;
    [SerializeField] private TextMeshProUGUI upgradeAttackText;
    [SerializeField] private int baseAttackCoreCost = 1;
    [SerializeField] private float bonusAttackPerUpgrade = 5f;

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

        ConfigureMouseScrollRect();
        BindHubListeners();
        BindSettingsPanelListeners();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        int savedCharIdx = Mathf.Clamp(GameProgressionManager.Instance.SelectedCharacterIndex, 0, Mathf.Max(0, characters.Count - 1));
        SelectCharacterByIndex(savedCharIdx);
        RefreshHubUI();
        RefreshSettingsUI();
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
                characterId = "CHAR_01",
                characterName = "01 : HIKARI - BRAWLER STRIKER",
                characterRoleAndLore = "Balanced Striker | High mobility and fast melee combos.",
                baseMaxHealth = 100f,
                baseAttackDamage = 25f,
                baseMoveSpeed = 5.2f,
                themeColor = new Color(0.92f, 0.28f, 0.58f, 1f)
            },
            new CharacterProfileData
            {
                characterId = "CHAR_02",
                characterName = "02 : AKANE - CRIMSON BREAKER",
                characterRoleAndLore = "Heavy Breaker | Crushes enemy stagger gauges with heavy blows.",
                baseMaxHealth = 90f,
                baseAttackDamage = 34f,
                baseMoveSpeed = 5.0f,
                themeColor = new Color(0.90f, 0.24f, 0.28f, 1f)
            },
            new CharacterProfileData
            {
                characterId = "CHAR_03",
                characterName = "03 : SHIRO - AEGIS GUARDIAN",
                characterRoleAndLore = "Aegis Tank | High base health built for surviving relentless swarms.",
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

        if (upgradeAttackButton != null)
        {
            upgradeAttackButton.onClick.RemoveAllListeners();
            upgradeAttackButton.onClick.AddListener(OnUpgradeAttackClicked);
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
            volumeValueText.text = $"{Mathf.RoundToInt(prog.MasterVolume * 100f)}%";
        if (sensitivityValueText != null)
            sensitivityValueText.text = $"{prog.MouseSensitivity:F1}x";
        if (fullscreenStatusText != null)
            fullscreenStatusText.text = Screen.fullScreen ? "FULLSCREEN" : "WINDOWED";
    }

    public void SelectCharacterByIndex(int index)
    {
        if (characters == null || characters.Count == 0) return;
        currentCharacterIndex = Mathf.Clamp(index, 0, characters.Count - 1);

        CharacterProfileData selected = characters[currentCharacterIndex];

        GameProgressionManager.Instance.SelectCharacter(
            currentCharacterIndex,
            selected.characterName,
            selected.baseMaxHealth,
            selected.baseAttackDamage,
            selected.baseMoveSpeed
        );

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
        return baseHpUpgradeCost + (GameProgressionManager.Instance.HpUpgradeLevel * hpCostIncreasePerLevel);
    }

    public void OnUpgradeMaxHpClicked()
    {
        int cost = GetCurrentHpUpgradeCost();
        if (GameProgressionManager.Instance.TryUpgradeMaxHealth(cost, bonusMaxHpPerUpgrade))
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
        var prog = GameProgressionManager.Instance;
        if (energyText != null)
        {
            if (prog.CurrentEnergy < prog.MaxEnergy)
                energyText.text = $"ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (+1 in {Mathf.CeilToInt(prog.EnergyRechargeRemainingSeconds)}s)";
            else
                energyText.text = $"ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (MAX)";
        }
    }

    public void RefreshHubUI()
    {
        var prog = GameProgressionManager.Instance;
        int totalStarDust = prog.StarDust;
        int totalShatteredCore = prog.ShatteredCore;

        RefreshEnergyDisplayOnly();

        if (starDustText != null)
        {
            if (shatteredCoreText != null)
            {
                starDustText.text = $"STAR DUST : {totalStarDust}";
                shatteredCoreText.text = (energyText == null)
                    ? $"SHATTERED CORE : {totalShatteredCore}  |  ENERGY : {prog.CurrentEnergy}/{prog.MaxEnergy}"
                    : $"SHATTERED CORE : {totalShatteredCore}";
            }
            else
            {
                starDustText.text = $"STAR DUST : {totalStarDust}  |  CORE : {totalShatteredCore}  |  ENERGY : {prog.CurrentEnergy}/{prog.MaxEnergy}";
            }
        }

        CharacterProfileData currentChar = (characters != null && characters.Count > 0)
            ? characters[Mathf.Clamp(currentCharacterIndex, 0, characters.Count - 1)]
            : null;

        float baseHp = currentChar != null ? currentChar.baseMaxHealth : 100f;
        float baseAtk = currentChar != null ? currentChar.baseAttackDamage : 25f;
        float moveSpd = currentChar != null ? currentChar.baseMoveSpeed : 5.0f;

        if (selectedCharacterNameText != null && currentChar != null)
            selectedCharacterNameText.text = $"<b>{currentChar.characterName}</b>";

        if (selectedCharacterLoreText != null && currentChar != null)
            selectedCharacterLoreText.text = currentChar.characterRoleAndLore;

        float totalHp = baseHp + prog.BonusMaxHealth;
        float totalAtk = baseAtk + prog.BonusAttackDamage;

        if (combinedStatsSummaryText != null)
        {
            combinedStatsSummaryText.text =
                $"<b>CHARACTER STATUS SUMMARY</b>\n" +
                $"- MAX HP : <b><color=#40FF80>{totalHp:F0}</color></b>  <size=17>(Base {baseHp:F0} + Bonus {prog.BonusMaxHealth:F0} [Lv.{prog.HpUpgradeLevel}])</size>\n" +
                $"- ATTACK : <b><color=#FF6699>{totalAtk:F0}</color></b>  <size=17>(Base {baseAtk:F0} + Bonus {prog.BonusAttackDamage:F0} [Lv.{prog.AttackUpgradeLevel}])</size>\n" +
                $"- SPEED : <b><color=#66E0FF>{moveSpd:F1}</color></b>   |   ENERGY : <b>{prog.CurrentEnergy} / {prog.MaxEnergy}</b>";
        }

        int hpCost = GetCurrentHpUpgradeCost();
        if (upgradeMaxHpText != null)
        {
            upgradeMaxHpText.text = $"<b>UPGRADE MAX HP (+{bonusMaxHpPerUpgrade:F0} HP)</b>\nCost: {hpCost} Star Dust  |  Lv.{prog.HpUpgradeLevel} ({totalHp:F0} HP)";
        }

        if (upgradeMaxHpButton != null)
        {
            upgradeMaxHpButton.interactable = (totalStarDust >= hpCost);
        }

        if (upgradeAttackText != null)
        {
            upgradeAttackText.text = $"<b>UPGRADE ATTACK (+{bonusAttackPerUpgrade:F0} ATK)</b>\nCost: {baseAttackCoreCost} Shattered Core  |  Lv.{prog.AttackUpgradeLevel} ({totalAtk:F0} ATK)";
        }

        if (upgradeAttackButton != null)
        {
            upgradeAttackButton.interactable = (totalShatteredCore >= baseAttackCoreCost);
        }
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