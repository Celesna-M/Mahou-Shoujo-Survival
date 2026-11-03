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
public class StageNodeData
{
    public string nodeCode = "RANK E";
    public string stageTitle = "RANK E : ABANDONED CLASSROOM";
    [TextArea(2, 3)]
    public string stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.";
    [TextArea(1, 2)]
    public string stageStoryLine = "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.";
    public string sceneToLoad = "The Action";
    public int normalEnemyCount = 5;
    public int eliteBossCount = 1;
    public int energyCost = 1;

    [Header("UI Node References")]
    [Tooltip("Drag the StageNode Button (or Image GameObject) from Map_Content_Long here. Auto-detected by name if left empty.")]
    public Button nodeButton;
    public Image nodeBackground;
}

/// <summary>
/// Handles both clicking a Stage Node AND dragging the Unrestricted ScrollRect smoothly without blocking either.
/// </summary>
public class MapNodeClickAndDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private StageMapManager mapManager;
    private ScrollRect parentScrollRect;
    private int nodeIndex;

    private Vector2 pointerDownPos;
    private bool isDraggingMap;
    private float lastClickTime;
    private const float DragPixelThreshold = 12f;
    private const float DoubleClickWindow = 0.32f;

    public void Initialize(StageMapManager manager, ScrollRect scrollRect, int index)
    {
        mapManager = manager;
        parentScrollRect = scrollRect;
        nodeIndex = index;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPos = eventData.position;
        isDraggingMap = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDraggingMap) return;

        float moveDist = Vector2.Distance(pointerDownPos, eventData.position);
        if (moveDist <= DragPixelThreshold && mapManager != null)
        {
            float now = Time.unscaledTime;
            bool isDoubleClick = (now - lastClickTime) <= DoubleClickWindow;
            lastClickTime = now;

            mapManager.OnStageNodeClicked(nodeIndex, isDoubleClick);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDraggingMap = true;
        if (parentScrollRect != null) parentScrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(pointerDownPos, eventData.position) > DragPixelThreshold)
        {
            isDraggingMap = true;
        }
        if (parentScrollRect != null) parentScrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (parentScrollRect != null) parentScrollRect.OnEndDrag(eventData);
        isDraggingMap = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (parentScrollRect != null) parentScrollRect.OnScroll(eventData);
    }
}

/// <summary>
/// Scene 3: Stage & Rank Progression Map Manager
/// - Unrestricted ScrollRect + Clamped Boundary
/// - 100% Clickable Stage Nodes
/// - All Stage Detail Auto-Texts ([FIRST-CLEAR BONUS], (Normal | Boss), and STORY [RANK X]) are editable or toggleable right in this Inspector!
/// </summary>
public class StageMapManager : MonoBehaviour
{
    [Header("Scene Navigation (4-Scene Flow)")]
    [SerializeField] private string lobbySceneName = "The lobby";
    [SerializeField] private string defaultActionSceneName = "The Action";

    [Header("1. Unrestricted Map Panning & Boundary Limits (ขอบเขตการเลื่อนแผนที่)")]
    [SerializeField] private bool allowHorizontalPan = true;
    [SerializeField] private bool allowVerticalPan = true;
    [SerializeField] private bool autoExpandBoundsFromNodes = true;
    [SerializeField] private float nodeBoundaryPadding = 240f;
    [SerializeField] private float minMapPanX = -950f;
    [SerializeField] private float maxMapPanX = 950f;
    [SerializeField] private float minMapPanY = -220f;
    [SerializeField] private float maxMapPanY = 220f;
    [SerializeField] private float mouseWheelScrollSpeed = 180f;

    [Header("2. Clickable Stage Nodes Settings (การกดโหนดด่าน)")]
    [SerializeField] private bool unlockAllNodesForPrototype = true;
    [SerializeField] private bool doubleClickNodeToEnterStage = true;
    [SerializeField] private bool clickSelectedNodeAgainToEnter = false;
    [SerializeField] private float selectedNodeScale = 1.18f;

    [Header("3. Stage Detail Auto-Text Format (แก้ข้อความที่สร้างเองตรงนี้ได้เลย)")]
    [Tooltip("Show/Hide the yellow [FIRST-CLEAR BONUS: ...] / [CLEARED] prefix in Selected Stage Detail")]
    [SerializeField] private bool showFirstClearBonusTag = true;
    [Tooltip("Show/Hide the (Normal: X | Boss: Y) suffix in Selected Stage Detail")]
    [SerializeField] private bool showEnemyCountInDetail = true;
    [Tooltip("Show/Hide the pink STORY [RANK X] second line in Selected Stage Detail")]
    [SerializeField] private bool showStoryLineInDetail = true;
    [SerializeField] private string firstClearBadgeFormat = "<color=#FFD700>[FIRST-CLEAR BONUS: +{0} STAR DUST, +{1} CORE!]</color>";
    [SerializeField] private string clearedBadgeText = "<color=#40FF80>[CLEARED - REPEAT RUN]</color>";
    [SerializeField] private string enemyCountSuffixFormat = "(Normal: {0} | Boss: {1})";
    [SerializeField] private string storyLineColorFormat = "<size=17><color=#FF99BB>{0}</color></size>";

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

    [Header("Stage Nodes List (แก้ชื่อด่าน, คำอธิบาย, และบรรทัด STORY ของแต่ละด่านตรงนี้)")]
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

        if (mapScrollRect == null)
        {
            mapScrollRect = FindAnyObjectByType<ScrollRect>();
        }

        if (mapContentRect == null && mapScrollRect != null)
        {
            mapContentRect = mapScrollRect.content;
        }

        AutoFindAndBindStageNodeButtons();
        ConfigureUnrestrictedScrollRectAndBounds();
        BindMapButtonListeners();
        BindSettingsPanelListeners();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        int defaultNode = Mathf.Clamp(GameProgressionManager.Instance.HighestUnlockedStageIndex, 0, Mathf.Max(0, stageNodes.Count - 1));
        SelectStageNode(defaultNode);
        RefreshHeaderStatus();
        RefreshSettingsUI();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && stageNodes != null && stageNodes.Count > 0)
        {
            SelectStageNode(currentSelectedNodeIndex);
        }
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

        HandleMouseWheelPan();
        RefreshHeaderStatus();
    }

    private void LateUpdate()
    {
        ClampMapContentPositionWithinBounds();
    }

    // =========================================================================
    // 1. UNRESTRICTED SCROLLRECT + CUSTOM BOUNDARY CLAMPING
    // =========================================================================

    private void ConfigureUnrestrictedScrollRectAndBounds()
    {
        if (mapScrollRect != null)
        {
            mapScrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            mapScrollRect.horizontal = allowHorizontalPan;
            mapScrollRect.vertical = allowVerticalPan;
            mapScrollRect.inertia = true;
            mapScrollRect.decelerationRate = 0.10f;
            mapScrollRect.scrollSensitivity = 35f;
        }

        if (mapContentRect != null && !mapContentRect.TryGetComponent<Graphic>(out _))
        {
            Image raycastImg = mapContentRect.gameObject.AddComponent<Image>();
            raycastImg.color = new Color(0f, 0f, 0f, 0f);
            raycastImg.raycastTarget = true;
        }

        if (autoExpandBoundsFromNodes && stageNodes != null && stageNodes.Count > 0)
        {
            float lowestNodeX = 0f;
            float highestNodeX = 0f;
            float lowestNodeY = 0f;
            float highestNodeY = 0f;
            bool foundAnyNodeRect = false;

            for (int i = 0; i < stageNodes.Count; i++)
            {
                if (stageNodes[i].nodeButton != null && stageNodes[i].nodeButton.TryGetComponent<RectTransform>(out var nodeRt))
                {
                    Vector2 pos = nodeRt.anchoredPosition;
                    if (!foundAnyNodeRect)
                    {
                        lowestNodeX = highestNodeX = pos.x;
                        lowestNodeY = highestNodeY = pos.y;
                        foundAnyNodeRect = true;
                    }
                    else
                    {
                        if (pos.x < lowestNodeX) lowestNodeX = pos.x;
                        if (pos.x > highestNodeX) highestNodeX = pos.x;
                        if (pos.y < lowestNodeY) lowestNodeY = pos.y;
                        if (pos.y > highestNodeY) highestNodeY = pos.y;
                    }
                }
            }

            if (foundAnyNodeRect)
            {
                float calcMinX = -highestNodeX - nodeBoundaryPadding;
                float calcMaxX = -lowestNodeX + nodeBoundaryPadding;
                float calcMinY = -highestNodeY - (nodeBoundaryPadding * 0.5f);
                float calcMaxY = -lowestNodeY + (nodeBoundaryPadding * 0.5f);

                minMapPanX = Mathf.Min(minMapPanX, calcMinX);
                maxMapPanX = Mathf.Max(maxMapPanX, calcMaxX);
                minMapPanY = Mathf.Min(minMapPanY, calcMinY);
                maxMapPanY = Mathf.Max(maxMapPanY, calcMaxY);
            }
        }

        if (minMapPanX > maxMapPanX)
        {
            float temp = minMapPanX;
            minMapPanX = maxMapPanX;
            maxMapPanX = temp;
        }

        if (minMapPanY > maxMapPanY)
        {
            float temp = minMapPanY;
            minMapPanY = maxMapPanY;
            maxMapPanY = temp;
        }
    }

    private void ClampMapContentPositionWithinBounds()
    {
        if (mapContentRect == null) return;

        Vector2 pos = mapContentRect.anchoredPosition;
        Vector2 vel = mapScrollRect != null ? mapScrollRect.velocity : Vector2.zero;

        if (allowHorizontalPan)
        {
            if (pos.x < minMapPanX)
            {
                pos.x = minMapPanX;
                if (vel.x < 0f) vel.x = 0f;
            }
            else if (pos.x > maxMapPanX)
            {
                pos.x = maxMapPanX;
                if (vel.x > 0f) vel.x = 0f;
            }
        }
        else
        {
            pos.x = 0f;
            vel.x = 0f;
        }

        if (allowVerticalPan)
        {
            if (pos.y < minMapPanY)
            {
                pos.y = minMapPanY;
                if (vel.y < 0f) vel.y = 0f;
            }
            else if (pos.y > maxMapPanY)
            {
                pos.y = maxMapPanY;
                if (vel.y > 0f) vel.y = 0f;
            }
        }
        else
        {
            pos.y = 0f;
            vel.y = 0f;
        }

        mapContentRect.anchoredPosition = pos;
        if (mapScrollRect != null)
        {
            mapScrollRect.velocity = vel;
        }
    }

    private void HandleMouseWheelPan()
    {
        if (mapContentRect == null) return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.05f)
            {
                Vector2 pos = mapContentRect.anchoredPosition;
                pos.x += Mathf.Sign(wheel) * mouseWheelScrollSpeed;
                mapContentRect.anchoredPosition = pos;
                ClampMapContentPositionWithinBounds();
            }
        }
#endif
    }

    // =========================================================================
    // 2. AUTO-DETECT & BIND CLICKABLE STAGE NODES
    // =========================================================================

    private void AutoFindAndBindStageNodeButtons()
    {
        string[] candidateNames =
        {
            "StageNode_01", "StageNode_02", "StageNode_03",
            "StageNode_04", "StageNode_05", "StageNode_06"
        };

        for (int i = 0; i < stageNodes.Count; i++)
        {
            if (stageNodes[i].nodeButton == null && i < candidateNames.Length)
            {
                GameObject foundObj = null;
                if (mapContentRect != null)
                {
                    Transform t = mapContentRect.Find(candidateNames[i]);
                    if (t != null) foundObj = t.gameObject;
                }
                if (foundObj == null)
                {
                    foundObj = GameObject.Find(candidateNames[i]);
                }

                if (foundObj != null)
                {
                    Button btn = foundObj.GetComponent<Button>();
                    if (btn == null) btn = foundObj.AddComponent<Button>();
                    stageNodes[i].nodeButton = btn;
                }
            }
        }

        if (mapContentRect != null)
        {
            Button[] childButtons = mapContentRect.GetComponentsInChildren<Button>(true);
            int assignIdx = 0;
            for (int b = 0; b < childButtons.Length && assignIdx < stageNodes.Count; b++)
            {
                Button candidate = childButtons[b];
                if (candidate == deployStageButton || candidate == backToLobbyButton || candidate == openSettingsButton)
                    continue;

                bool alreadyUsed = false;
                for (int j = 0; j < stageNodes.Count; j++)
                {
                    if (stageNodes[j].nodeButton == candidate)
                    {
                        alreadyUsed = true;
                        break;
                    }
                }

                if (!alreadyUsed)
                {
                    while (assignIdx < stageNodes.Count && stageNodes[assignIdx].nodeButton != null)
                        assignIdx++;

                    if (assignIdx < stageNodes.Count)
                    {
                        stageNodes[assignIdx].nodeButton = candidate;
                        assignIdx++;
                    }
                }
            }
        }

        for (int i = 0; i < stageNodes.Count; i++)
        {
            Button btn = stageNodes[i].nodeButton;
            if (btn == null) continue;

            btn.interactable = true;

            if (stageNodes[i].nodeBackground == null)
                stageNodes[i].nodeBackground = btn.GetComponent<Image>();

            if (stageNodes[i].nodeBackground != null)
                stageNodes[i].nodeBackground.raycastTarget = true;

            var handler = btn.GetComponent<MapNodeClickAndDragHandler>();
            if (handler == null)
                handler = btn.gameObject.AddComponent<MapNodeClickAndDragHandler>();

            handler.Initialize(this, mapScrollRect, i);
        }
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
                stageNodes[i].nodeButton.onClick.RemoveAllListeners();
                stageNodes[i].nodeButton.onClick.AddListener(() => SelectStageNode(capturedIndex));
            }
        }
    }

    public void OnStageNodeClicked(int index, bool isDoubleClick)
    {
        bool wasAlreadySelected = (index == currentSelectedNodeIndex);
        SelectStageNode(index);

        if ((isDoubleClick && doubleClickNodeToEnterStage) || (wasAlreadySelected && clickSelectedNodeAgainToEnter))
        {
            if (IsStageNodeUnlocked(index))
            {
                DeploySelectedStage();
            }
        }
    }

    private void EnsureDefaultStageNodes()
    {
        if (stageNodes != null && stageNodes.Count > 0) return;

        stageNodes = new List<StageNodeData>
        {
            new StageNodeData
            {
                nodeCode = "RANK E",
                stageTitle = "RANK E : ABANDONED CLASSROOM",
                stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.",
                stageStoryLine = "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 5,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK D",
                stageTitle = "RANK D : DERELICT CITY STREET",
                stageDescription = "Ruined city streets beneath a blood-red sky.",
                stageStoryLine = "STORY [RANK D] : Beyond the school gate, the city streets are frozen under a blood-red sky.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 7,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK C",
                stageTitle = "RANK C : SHATTERED SUBWAY LINE",
                stageDescription = "Underground tracks echoing with the whispers of fallen Magical Girls.",
                stageStoryLine = "STORY [RANK C] : Deep in the subway tunnels, the entities whisper fragments of our forgotten names.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 9,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK B",
                stageTitle = "RANK B : FACELESS PLAZA",
                stageDescription = "Collapsed commercial center swarming with aggressive entities.",
                stageStoryLine = "STORY [RANK B] : My Soul Gem burns darker with every battle. The loop is beginning to fracture.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 11,
                eliteBossCount = 1,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK A",
                stageTitle = "RANK A : CRIMSON SANCTUM",
                stageDescription = "Deep dimensional corridor guarded by Twin Elite Entities.",
                stageStoryLine = "STORY [RANK A] : At the end of the crimson corridor, the source of the anomaly pulses ahead.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 12,
                eliteBossCount = 2,
                energyCost = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK S",
                stageTitle = "RANK S : CORE OF THE ABYSS [FINAL]",
                stageDescription = "The heart of the dimensional loop where the Abyss Sovereign awaits.",
                stageStoryLine = "STORY [RANK S] : The Abyss Core is breached. I am no longer prey trapped in this dimension.",
                sceneToLoad = defaultActionSceneName,
                normalEnemyCount = 15,
                eliteBossCount = 2,
                energyCost = 2
            }
        };
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
        SelectStageNode(0);
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
                ? $"RANK {prog.CurrentRankLetter}  |  ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (+1 in {Mathf.CeilToInt(prog.EnergyRechargeRemainingSeconds)}s)"
                : $"RANK {prog.CurrentRankLetter}  |  ENERGY : {prog.CurrentEnergy} / {prog.MaxEnergy} (MAX)";
        }

        if (headerCharacterStatusText != null)
        {
            headerCharacterStatusText.text =
                $"{prog.SelectedCharacterName} <color=#FFD700>[RANK {prog.CurrentRankLetter}]</color>\n" +
                $"<size=16>HP: {prog.TotalEffectiveMaxHp:F0} | ATK: {prog.TotalEffectiveAtk:F0} | DUST: {prog.StarDust} | CORE: {prog.ShatteredCore} | KILLS: {prog.TotalEnemiesKilled}</size>";
        }

        if (deployStageButton != null && stageNodes != null && stageNodes.Count > 0)
        {
            int idx = Mathf.Clamp(currentSelectedNodeIndex, 0, stageNodes.Count - 1);
            int cost = stageNodes[idx].energyCost;
            bool isUnlocked = IsStageNodeUnlocked(idx);
            bool hasEnergy = prog.HasEnoughEnergy(cost);

            deployStageButton.interactable = isUnlocked && hasEnergy;

            if (deployStageButton.TryGetComponent<Image>(out var deployImg))
            {
                deployImg.color = isUnlocked ? prog.GetProgressionUiThemeColor() : new Color(0.3f, 0.3f, 0.35f, 0.9f);
            }

            if (deployButtonLabelText != null)
            {
                string rankCode = GameProgressionManager.GetRankLetterForStage(idx);
                if (!isUnlocked)
                {
                    string prevRank = GameProgressionManager.GetRankLetterForStage(Mathf.Max(0, idx - 1));
                    deployButtonLabelText.text = $"LOCKED (CLEAR RANK {prevRank} FIRST)";
                }
                else if (!hasEnergy)
                {
                    deployButtonLabelText.text = $"NO ENERGY (0 / {prog.MaxEnergy})";
                }
                else
                {
                    deployButtonLabelText.text = $"ENTER RANK {rankCode} (-{cost} ENERGY) >";
                }
            }
        }
    }

    public bool IsStageNodeUnlocked(int index)
    {
        if (unlockAllNodesForPrototype) return true;
        return index <= GameProgressionManager.Instance.HighestUnlockedStageIndex;
    }

    // =========================================================================
    // HERE IS WHERE THAT EXACT STAGE DETAIL TEXT IS BUILT (SelectStageNode)
    // =========================================================================

    public void SelectStageNode(int index)
    {
        if (stageNodes == null || stageNodes.Count == 0) return;
        currentSelectedNodeIndex = Mathf.Clamp(index, 0, stageNodes.Count - 1);

        StageNodeData selected = stageNodes[currentSelectedNodeIndex];
        var prog = GameProgressionManager.Instance;

        if (selectedStageTitleText != null)
        {
            selectedStageTitleText.text = selected.stageTitle;
        }

        if (selectedStageDetailText != null)
        {
            string finalDetail = "";

            // 1. Optional [FIRST-CLEAR BONUS: ...] or [CLEARED] prefix
            if (showFirstClearBonusTag)
            {
                bool clearedBefore = prog.IsStageClearedBefore(currentSelectedNodeIndex);
                if (clearedBefore)
                {
                    finalDetail += clearedBadgeText + " ";
                }
                else
                {
                    int firstDust = prog.GetFirstClearStarDustBonusForStage(currentSelectedNodeIndex);
                    int firstCore = prog.GetFirstClearCoreBonusForStage(currentSelectedNodeIndex);
                    finalDetail += string.Format(firstClearBadgeFormat, firstDust, firstCore) + " ";
                }
            }

            // 2. Stage Description (from Stage Nodes list in Inspector)
            finalDetail += selected.stageDescription;

            // 3. Optional (Normal: 5 | Boss: 1) suffix
            if (showEnemyCountInDetail)
            {
                finalDetail += " " + string.Format(enemyCountSuffixFormat, selected.normalEnemyCount, selected.eliteBossCount);
            }

            // 4. Optional pink STORY [RANK E] second line
            if (showStoryLineInDetail)
            {
                string story = !string.IsNullOrEmpty(selected.stageStoryLine)
                    ? selected.stageStoryLine
                    : GameProgressionManager.GetStoryLogForStage(currentSelectedNodeIndex);

                if (!string.IsNullOrEmpty(story))
                {
                    finalDetail += "\n" + string.Format(storyLineColorFormat, story);
                }
            }

            selectedStageDetailText.text = finalDetail;
        }

        RefreshStageNodesVisualState();
        RefreshHeaderStatus();
    }

    private void RefreshStageNodesVisualState()
    {
        var prog = GameProgressionManager.Instance;
        Color milestoneAccent = prog.GetProgressionUiThemeColor();

        for (int i = 0; i < stageNodes.Count; i++)
        {
            StageNodeData node = stageNodes[i];
            Image bg = node.nodeBackground != null
                ? node.nodeBackground
                : (node.nodeButton != null ? node.nodeButton.GetComponent<Image>() : null);

            if (node.nodeButton != null)
                node.nodeButton.interactable = true;

            if (bg == null) continue;

            bool unlocked = IsStageNodeUnlocked(i);
            bool isSelected = (i == currentSelectedNodeIndex);
            bool isCleared = prog.IsStageClearedBefore(i);

            if (isSelected)
            {
                bg.color = milestoneAccent;
                bg.transform.localScale = Vector3.one * selectedNodeScale;
            }
            else if (!unlocked)
            {
                bg.color = new Color(0.22f, 0.22f, 0.28f, 0.85f);
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

        int idx = Mathf.Clamp(currentSelectedNodeIndex, 0, stageNodes.Count - 1);
        if (!IsStageNodeUnlocked(idx))
        {
            return;
        }

        StageNodeData selected = stageNodes[idx];

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
            idx,
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


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน StageMapManager.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. MapNodeClickAndDragHandler (คลาสย่อยสำหรับปุ่มโหนดด่าน)
 *    - แยกแยะระหว่าง "การคลิกเลือกโหนดด่าน" (เมาส์ขยับไม่เกิน 12 พิกเซล) กับ "การคลิกค้างลากแผนที่"
 *    - ทำให้ผู้เล่นสามารถคลิกซ้ายที่โหนดเพื่อเลือกด่าน ดับเบิลคลิกที่โหนดเพื่อเข้าด่าน หรือคลิกค้างบนโหนดแล้วลากเลื่อนแผนที่ได้ลื่นไหล
 *
 * 2. ConfigureUnrestrictedScrollRectAndBounds() & ClampMapContentPositionWithinBounds()
 *    - บังคับให้ ScrollRect ของแผนที่เป็นโหมด Unrestricted เพื่อแก้ปัญหาแผนที่ดีดกลับไปจุด (0,0)
 *    - คำนวณขอบเขตการเลื่อนซ้าย-ขวา (minMapPanX ถึง maxMapPanX) และบน-ล่าง (minMapPanY ถึง maxMapPanY)
 *      จากตำแหน่งปุ่ม StageNode ทั้งหมดโดยอัตโนมัติ และล็อกไม่ให้ลากแผนที่หลุดขอบจอใน LateUpdate()
 *
 * 3. AutoFindAndBindStageNodeButtons()
 *    - ค้นหาปุ่ม StageNode_01 ถึง StageNode_06 ในฉากมาผูกเข้ากับรายการ stageNodes อัตโนมัติ
 *    - บังคับเปิด interactable = true และ raycastTarget = true เพื่อให้กดโหนดด่านได้ครบทุกจุด 100%
 *
 * 4. SelectStageNode(int index)
 *    - อัปเดตชื่อด่านและคำอธิบายด่านเมื่อผู้เล่นคลิกเลือกโหนดบนแผนที่
 *    - นำตัวเลือกในหมวด "3. Stage Detail Auto-Text Format" บน Inspector (เช่น ป้าย [FIRST-CLEAR BONUS],
 *      จำนวน (Normal | Boss), และบรรทัดเนื้อเรื่อง STORY [RANK X]) มาประกอบแสดงผลตามที่คุณติ๊กเปิด/ปิดไว้
 *
 * 5. DeploySelectedStage()
 *    - ตรวจสอบและหักค่าพลังงาน (Play Energy) ก่อนบันทึกข้อมูลด่านที่เลือกเข้า GameProgressionManager
 *      แล้วโหลดเข้าสู่ฉากต่อสู้ (The Action)
 * ============================================================================ */