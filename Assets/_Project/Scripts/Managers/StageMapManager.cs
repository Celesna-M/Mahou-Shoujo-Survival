using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public class StageNodeData
{
    public string nodeCode = "01";
    public string stageTitle = "STAGE 01 : EMPTY CROSSROADS";
    [TextArea(1, 2)]
    public string stageDescription = "Abandoned intersection flickering in the dark void. (Normal: 5 | Boss: 1)";
    public string sceneToLoad = "The Action";
    public int normalEnemyCount = 5;
    public int eliteBossCount = 1;
    public int energyCost = 1;

    [Header("UI References")]
    public Button nodeButton;
    public Image nodeBackground;
}

/// <summary>
/// Scene 3: Stage Map Manager (StageMap)
/// - Mouse Drag & Wheel Horizontal Scrolling
/// - Pure ASCII English UI Text (No unsupported symbols)
/// - Play Energy Limit Check before entering stage (Consumes Energy on Deploy)
/// - Full Settings Panel support in StageMap Scene
/// </summary>
public class StageMapManager : MonoBehaviour
{
    [Header("Scene Navigation (4-Scene Flow)")]
    [SerializeField] private string lobbySceneName = "The lobby";
    [SerializeField] private string defaultActionSceneName = "The Action";

    [Header("Map Mouse Scroll Settings")]
    [SerializeField] private bool unlockAllNodesForPrototype = true;
    [SerializeField] private float mouseWheelScrollSensitivity = 0.14f;

    [Header("Stage Map UI References")]
    [SerializeField] private ScrollRect mapScrollRect;
    [SerializeField] private RectTransform mapContentRect;
    [SerializeField] private Button deployStageButton;
    [SerializeField] private TextMeshProUGUI deployButtonLabelText;
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private TextMeshProUGUI headerCharacterStatusText;
    [SerializeField] private TextMeshProUGUI energyStatusText;
    [SerializeField] private TextMeshProUGUI selectedStageTitleText;
    [SerializeField] private TextMeshProUGUI selectedStageDetailText;

    [Header("Stage Nodes List (5 Stage Nodes)")]
    [SerializeField] private List<StageNodeData> stageNodes = new List<StageNodeData>();

    [Header("Settings Panel (Available in StageMap Scene)")]
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

    private int currentSelectedNodeIndex = 0;

    private void Reset()
    {
        EnsureDefaultStageNodes();
    }

    private void Awake()
    {
        EnsureDefaultStageNodes();
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameProgressionManager.Instance.LoadFromPrefs();

        if (mapContentRect == null && mapScrollRect != null)
        {
            mapContentRect = mapScrollRect.content;
        }

        ConfigureMouseScrollRect();
        BindMapButtonListeners();
        BindSettingsPanelListeners();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        int defaultNode = Mathf.Clamp(GameProgressionManager.Instance.HighestUnlockedStageIndex, 0, Mathf.Max(0, stageNodes.Count - 1));
        SelectStageNode(defaultNode);
        RefreshHeaderStatus();
        RefreshSettingsUI();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPanel != null)
            {
                ToggleSettingsPanel();
                return;
            }
        }
#endif

        if (settingsPanel != null && settingsPanel.activeInHierarchy)
            return;

        if (mapScrollRect != null)
        {
            HandleMouseWheelScroll();
        }

        RefreshHeaderStatus();
    }

    private void ConfigureMouseScrollRect()
    {
        if (mapScrollRect == null) return;

        mapScrollRect.horizontal = true;
        mapScrollRect.vertical = false;
        mapScrollRect.inertia = true;
        mapScrollRect.decelerationRate = 0.12f;
        mapScrollRect.scrollSensitivity = 45f;

        if (mapContentRect != null && !mapContentRect.TryGetComponent<Graphic>(out _))
        {
            Image raycastImg = mapContentRect.gameObject.AddComponent<Image>();
            raycastImg.color = new Color(0f, 0f, 0f, 0f);
            raycastImg.raycastTarget = true;
        }

        for (int i = 0; i < stageNodes.Count; i++)
        {
            if (stageNodes[i].nodeButton != null)
            {
                var forwarder = stageNodes[i].nodeButton.GetComponent<ScrollRectDragForwarder>();
                if (forwarder == null)
                    forwarder = stageNodes[i].nodeButton.gameObject.AddComponent<ScrollRectDragForwarder>();
                forwarder.Initialize(mapScrollRect);
            }
        }
    }

    private void HandleMouseWheelScroll()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.1f)
            {
                float delta = -Mathf.Sign(wheel) * mouseWheelScrollSensitivity;
                mapScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(mapScrollRect.horizontalNormalizedPosition + delta);
            }
        }
#endif
    }

    private void EnsureDefaultStageNodes()
    {
        if (stageNodes != null && stageNodes.Count > 0) return;

        stageNodes = new List<StageNodeData>
        {
            new StageNodeData
            {
                nodeCode = "01",
                stageTitle = "STAGE 01 : EMPTY CROSSROADS",
                stageDescription = "Abandoned intersection flickering in the dark void. (Normal: 5 | Boss: 1 | Cost: 1 Energy)",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 5,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "02",
                stageTitle = "STAGE 02 : SUBWAY PLATFORM",
                stageDescription = "Derelict underground station echoing with footsteps. (Normal: 7 | Boss: 1 | Cost: 1 Energy)",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 7,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "03",
                stageTitle = "STAGE 03 : FACELESS MALL",
                stageDescription = "Empty shopping center swarming with faceless mannequins. (Normal: 9 | Boss: 1 | Cost: 1 Energy)",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 9,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "04",
                stageTitle = "STAGE 04 : SILENT CORRIDOR",
                stageDescription = "Long flickering hallway with aggressive entity waves. (Normal: 12 | Boss: 1 | Cost: 1 Energy)",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 12,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "05",
                stageTitle = "STAGE 05 : ROOFTOP ABYSS [BOSS]",
                stageDescription = "The final rooftop at the edge of the abyss. (Normal: 10 | Boss: 2 | Cost: 1 Energy)",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 10,
                eliteBossCount = 2,
                energyCost = 1
            }
        };
    }

    private void BindMapButtonListeners()
    {
        if (deployStageButton != null)
        {
            deployStageButton.onClick.RemoveAllListeners();
            deployStageButton.onClick.AddListener(DeploySelectedStage);
        }

        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
        }

        for (int i = 0; i < stageNodes.Count; i++)
        {
            int capturedIndex = i;
            if (stageNodes[i].nodeButton != null)
            {
                if (stageNodes[i].nodeBackground == null)
                    stageNodes[i].nodeBackground = stageNodes[i].nodeButton.GetComponent<Image>();

                stageNodes[i].nodeButton.onClick.RemoveAllListeners();
                stageNodes[i].nodeButton.onClick.AddListener(() => SelectStageNode(capturedIndex));
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
        RefreshHeaderStatus();
        SelectStageNode(currentSelectedNodeIndex);
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

    private void RefreshHeaderStatus()
    {
        var prog = GameProgressionManager.Instance;

        if (energyStatusText != null)
        {
            energyStatusText.text = (prog.CurrentEnergy < prog.MaxEnergy)
                ? $"ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (+1 in {Mathf.CeilToInt(prog.EnergyRechargeRemainingSeconds)}s)"
                : $"ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (MAX)";
        }

        if (headerCharacterStatusText != null)
        {
            headerCharacterStatusText.text =
                $"{prog.SelectedCharacterName}\n" +
                $"<size=16>HP: {prog.TotalEffectiveMaxHp:F0} | ATK: {prog.TotalEffectiveAtk:F0} | DUST: {prog.StarDust} | CORE: {prog.ShatteredCore} | ENERGY: {prog.CurrentEnergy}/{prog.MaxEnergy}</size>";
        }

        if (deployStageButton != null && stageNodes != null && stageNodes.Count > 0)
        {
            int cost = stageNodes[Mathf.Clamp(currentSelectedNodeIndex, 0, stageNodes.Count - 1)].energyCost;
            bool hasEnergy = prog.HasEnoughEnergy(cost);
            deployStageButton.interactable = hasEnergy;

            if (deployButtonLabelText != null)
            {
                deployButtonLabelText.text = hasEnergy
                    ? $"ENTER STAGE (-{cost} ENERGY) >"
                    : $"NO ENERGY (0 / {prog.MaxEnergy})";
            }
        }
    }

    public bool IsStageNodeUnlocked(int index)
    {
        if (unlockAllNodesForPrototype) return true;
        return index <= GameProgressionManager.Instance.HighestUnlockedStageIndex;
    }

    public void SelectStageNode(int index)
    {
        if (stageNodes == null || stageNodes.Count == 0) return;
        index = Mathf.Clamp(index, 0, stageNodes.Count - 1);

        if (!IsStageNodeUnlocked(index))
        {
            return;
        }

        currentSelectedNodeIndex = index;
        StageNodeData selected = stageNodes[currentSelectedNodeIndex];

        if (selectedStageTitleText != null)
            selectedStageTitleText.text = selected.stageTitle;

        if (selectedStageDetailText != null)
        {
            string statusTag = (index < GameProgressionManager.Instance.HighestUnlockedStageIndex)
                ? "<color=#40FF80>[CLEARED]</color>"
                : "<color=#00E5FF>[READY]</color>";
            selectedStageDetailText.text = $"{statusTag} {selected.stageDescription}";
        }

        RefreshStageNodesVisualState();
        RefreshHeaderStatus();
    }

    private void RefreshStageNodesVisualState()
    {
        int highestUnlocked = GameProgressionManager.Instance.HighestUnlockedStageIndex;

        for (int i = 0; i < stageNodes.Count; i++)
        {
            StageNodeData node = stageNodes[i];
            Image bg = node.nodeBackground != null
                ? node.nodeBackground
                : (node.nodeButton != null ? node.nodeButton.GetComponent<Image>() : null);

            if (bg == null) continue;

            bool unlocked = IsStageNodeUnlocked(i);
            bool isSelected = (i == currentSelectedNodeIndex);
            bool isCleared = (i < highestUnlocked);

            if (node.nodeButton != null)
                node.nodeButton.interactable = unlocked;

            if (isSelected)
            {
                bg.color = new Color(0.95f, 0.28f, 0.58f, 1f);
                bg.transform.localScale = Vector3.one * 1.14f;
            }
            else if (!unlocked)
            {
                bg.color = new Color(0.16f, 0.16f, 0.20f, 0.75f);
                bg.transform.localScale = Vector3.one * 0.95f;
            }
            else if (isCleared)
            {
                bg.color = new Color(0.18f, 0.55f, 0.45f, 0.95f);
                bg.transform.localScale = Vector3.one;
            }
            else
            {
                bg.color = new Color(0.32f, 0.24f, 0.58f, 0.95f);
                bg.transform.localScale = Vector3.one;
            }
        }
    }

    public void DeploySelectedStage()
    {
        if (stageNodes == null || stageNodes.Count == 0)
        {
            SceneManager.LoadScene(defaultActionSceneName);
            return;
        }

        StageNodeData selected = stageNodes[Mathf.Clamp(currentSelectedNodeIndex, 0, stageNodes.Count - 1)];

        // Check and consume Play Energy before entering stage
        if (!GameProgressionManager.Instance.TryConsumeEnergy(selected.energyCost))
        {
            if (selectedStageDetailText != null)
            {
                selectedStageDetailText.text = "<color=#FF4444>[NOT ENOUGH ENERGY!] Wait for Energy recharge or refill in Lobby.</color>";
            }
            RefreshHeaderStatus();
            return;
        }

        GameProgressionManager.Instance.SelectStage(
            currentSelectedNodeIndex,
            selected.stageTitle,
            selected.normalEnemyCount,
            selected.eliteBossCount,
            selected.energyCost
        );

        string targetScene = string.IsNullOrEmpty(selected.sceneToLoad) ? defaultActionSceneName : selected.sceneToLoad;
        SceneManager.LoadScene(targetScene);
    }

    public void OnBackToLobbyClicked()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}
