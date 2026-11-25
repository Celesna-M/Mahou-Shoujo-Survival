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
    [Tooltip("Short rank or stage badge shown on the map node button (e.g., RANK E).")]
    public string nodeCode = "RANK E";

    [Tooltip("Full stage title displayed in the stage detail header.")]
    public string stageTitle = "RANK E : ABANDONED CLASSROOM";

    [TextArea(2, 3)]
    [Tooltip("Stage description displayed in the stage detail panel.")]
    public string stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.";

    [TextArea(1, 2)]
    [Tooltip("Story log line shown under the stage description.")]
    public string stageStoryLine = "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.";

    [Tooltip("Exact Unity Scene name (must be enabled in Build Settings) loaded when deploying this stage.")]
    public string sceneToLoad = "The Action";

    [Tooltip("Optional StageData_SO for data-driven waves, lighting, and VN triggers. Leave empty to use scene defaults.")]
    public StageData_SO stageData;

    [Tooltip("When enabled, this node stays visible and previewable on the map but cannot be deployed yet.")]
    public bool isComingSoon = false;

    [Tooltip("Normal enemies spawned when no StageData_SO wave list is used.")]
    public int normalEnemyCount = 5;

    [Tooltip("Elite/Boss enemies spawned when no StageData_SO wave list is used.")]
    public int eliteBossCount = 1;

    [Header("UI Node References")]
    [Tooltip("Drag the StageNode Button from Map_Content_Long here. Auto-detected by trimmed name if left empty.")]
    public Button nodeButton;

    [Tooltip("Node background Image for selection/status tinting. Auto-cached from nodeButton if left empty.")]
    public Image nodeBackground;

    [Tooltip("Optional child TMP label on the node button. Auto-cached from nodeButton if left empty.")]
    public TextMeshProUGUI nodeLabelText;
}

/// <summary>
/// Handles single-click selection, double-click stage deployment, and drag passthrough to the parent ScrollRect
/// without duplicate Button.onClick firing.
/// </summary>
public class MapNodeClickAndDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private StageMapManager mapManager;
    private ScrollRect parentScrollRect;
    private int nodeIndex;

    private Vector2 pointerDownPos;
    private bool isDraggingMap;
    private float lastClickTime = -10f;
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
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pointerDownPos = eventData.position;
        isDraggingMap = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || isDraggingMap) return;

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
/// Scene 3: Stage & Rank Progression Map Manager (Single Source of Truth, Zero-GC Edition)
/// - Inspector StageNodeData (+ optional StageData_SO) controls stage titles, descriptions, story lines, and target scenes.
/// - Supports Active sequential stages (The Action -> Shibuya) alongside visible Coming Soon preview nodes.
/// - Event-driven UI refresh via GameProgressionManager.OnProgressionChanged (no per-frame UI allocations).
/// </summary>
public class StageMapManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Exact Lobby scene name in Build Settings.")]
    [SerializeField] private string lobbySceneName = "The Lobby";

    [Tooltip("Fallback combat scene if a node's sceneToLoad is blank or unavailable.")]
    [SerializeField] private string defaultActionSceneName = "The Action";

    [Header("1. Unrestricted Map Panning & Boundary Limits")]
    [Tooltip("Allow dragging/scrolling the map horizontally.")]
    [SerializeField] private bool allowHorizontalPan = true;

    [Tooltip("Allow dragging/scrolling the map vertically.")]
    [SerializeField] private bool allowVerticalPan = false;

    [Tooltip("Automatically expand pan bounds to encompass all StageNode positions.")]
    [SerializeField] private bool autoExpandBoundsFromNodes = true;

    [Tooltip("Extra pan margin around the outermost StageNodes.")]
    [SerializeField] private float nodeBoundaryPadding = 240f;

    [SerializeField] private float minMapPanX = -950f;
    [SerializeField] private float maxMapPanX = 950f;
    [SerializeField] private float minMapPanY = -220f;
    [SerializeField] private float maxMapPanY = 220f;

    [Tooltip("Horizontal pan speed when scrolling the mouse wheel.")]
    [SerializeField] private float mouseWheelScrollSpeed = 180f;

    [Header("2. Stage Unlock & Node Interaction Settings")]
    [Tooltip("Developer toggle: bypasses sequential stage locks for non-Coming-Soon nodes.")]
    [SerializeField] private bool unlockAllNodesForPrototype = false;

    [Tooltip("Allow double-clicking an unlocked stage node to enter immediately.")]
    [SerializeField] private bool doubleClickNodeToEnterStage = true;

    [Tooltip("Allow clicking an already-selected unlocked node again to enter immediately.")]
    [SerializeField] private bool clickSelectedNodeAgainToEnter = false;

    [Tooltip("Automatically format each StageNode button's TMP text from nodeCode + Coming Soon/Lock status.")]
    [SerializeField] private bool autoSyncNodeLabels = true;

    [Tooltip("Scale multiplier applied to the currently selected stage node.")]
    [SerializeField] private float selectedNodeScale = 1.18f;

    [Header("3. Stage Detail Auto-Text Format")]
    [Tooltip("Show [FIRST-CLEAR BONUS] / [CLEARED] prefix for playable stages.")]
    [SerializeField] private bool showFirstClearBonusTag = true;

    [Tooltip("Show (Normal: X | Boss: Y) suffix for playable stages.")]
    [SerializeField] private bool showEnemyCountInDetail = true;

    [Tooltip("Show the colored STORY line below the stage description.")]
    [SerializeField] private bool showStoryLineInDetail = true;

    [SerializeField] private string firstClearBadgeFormat = "<color=#FFD700>[FIRST-CLEAR BONUS: +{0} STAR DUST, +{1} CORE!]</color>";
    [SerializeField] private string clearedBadgeText = "<color=#40FF80>[CLEARED - REPEAT RUN]</color>";
    [SerializeField] private string comingSoonBadgeText = "<color=#B89CFF>[COMING SOON - IN DEVELOPMENT]</color>";
    [SerializeField] private string lockedBadgeFormat = "<color=#FF6B8A>[LOCKED - CLEAR RANK {0} FIRST]</color>";
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

    [Header("Stage Nodes List (Single Source of Truth)")]
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

    private static readonly Color LockedDeployButtonColor = new Color(0.30f, 0.30f, 0.35f, 0.90f);
    private static readonly Color ComingSoonDeployButtonColor = new Color(0.28f, 0.24f, 0.38f, 0.90f);
    private static readonly Color LockedNodeColor = new Color(0.22f, 0.22f, 0.28f, 0.85f);
    private static readonly Color ComingSoonNodeColor = new Color(0.24f, 0.20f, 0.34f, 0.82f);
    private static readonly Color ClearedNodeColor = new Color(0.18f, 0.55f, 0.45f, 0.95f);
    private static readonly Color UnlockedNodeColor = new Color(0.32f, 0.24f, 0.58f, 0.95f);

    private int currentSelectedNodeIndex = 0;
    private Image deployStageButtonImage;
    private bool isSubscribedToProgression;

    private void Reset()
    {
        EnsureDefaultStageNodes();
    }

    private void Awake()
    {
        EnsureDefaultStageNodes();
        if (deployStageButton != null)
        {
            deployStageButton.TryGetComponent(out deployStageButtonImage);
        }
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var prog = GameProgressionManager.Instance;
        prog.LoadFromPrefs();

        if (mapScrollRect == null)
        {
            mapScrollRect = FindAnyObjectByType<ScrollRect>();
        }

        if (mapContentRect == null && mapScrollRect != null)
        {
            mapContentRect = mapScrollRect.content;
        }

        if (deployStageButton != null && deployStageButtonImage == null)
        {
            deployStageButton.TryGetComponent(out deployStageButtonImage);
        }

        AutoFindAndBindStageNodeButtons();
        ConfigureUnrestrictedScrollRectAndBounds();
        BindMapButtonListeners();
        BindSettingsPanelListeners();
        SubscribeToProgressionEvents();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        int initialNode = ResolveInitialSelectedNodeIndex(prog);
        SelectStageNode(initialNode);
        RefreshSettingsUI();
    }

    private void OnDestroy()
    {
        UnsubscribeFromProgressionEvents();
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
    }

    private void LateUpdate()
    {
        ClampMapContentPositionWithinBounds();
    }

    private void SubscribeToProgressionEvents()
    {
        if (isSubscribedToProgression) return;
        GameProgressionManager.Instance.OnProgressionChanged += HandleProgressionChanged;
        isSubscribedToProgression = true;
    }

    private void UnsubscribeFromProgressionEvents()
    {
        if (!isSubscribedToProgression) return;
        var prog = FindAnyObjectByType<GameProgressionManager>();
        if (prog != null)
        {
            prog.OnProgressionChanged -= HandleProgressionChanged;
        }
        isSubscribedToProgression = false;
    }

    private void HandleProgressionChanged()
    {
        RefreshHeaderStatus();
        RefreshStageNodesVisualState();
    }

    private int ResolveInitialSelectedNodeIndex(GameProgressionManager prog)
    {
        if (stageNodes == null || stageNodes.Count == 0) return 0;

        if (prog != null && prog.HasSelectedStageFromMap)
        {
            int previousIdx = Mathf.Clamp(prog.SelectedStageIndex, 0, stageNodes.Count - 1);
            if (IsStageNodePlayable(previousIdx))
            {
                return previousIdx;
            }
        }

        int highestPlayable = 0;
        for (int i = 0; i < stageNodes.Count; i++)
        {
            if (IsStageNodePlayable(i))
            {
                highestPlayable = i;
            }
        }
        return highestPlayable;
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
        if (mapContentRect == null || !allowHorizontalPan) return;

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
        Button[] childButtons = mapContentRect != null
            ? mapContentRect.GetComponentsInChildren<Button>(true)
            : System.Array.Empty<Button>();

        // Pass 1: Match by trimmed GameObject name (StageNode_01 .. StageNode_XX) so trailing spaces never mis-bind order.
        for (int i = 0; i < stageNodes.Count; i++)
        {
            if (stageNodes[i].nodeButton != null) continue;

            string expectedName = $"StageNode_{i + 1:00}";
            for (int b = 0; b < childButtons.Length; b++)
            {
                Button candidate = childButtons[b];
                if (candidate == null) continue;
                if (candidate.gameObject.name.Trim() == expectedName)
                {
                    stageNodes[i].nodeButton = candidate;
                    break;
                }
            }
        }

        // Pass 2: Fallback assignment for any remaining unassigned nodes, ordered left-to-right by anchoredPosition.x.
        if (childButtons.Length > 0)
        {
            List<Button> unassignedButtons = new List<Button>();
            for (int b = 0; b < childButtons.Length; b++)
            {
                Button candidate = childButtons[b];
                if (candidate == null || candidate == deployStageButton || candidate == backToLobbyButton || candidate == openSettingsButton)
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
                    unassignedButtons.Add(candidate);
                }
            }

            unassignedButtons.Sort((a, b) =>
            {
                float ax = a.TryGetComponent<RectTransform>(out var rta) ? rta.anchoredPosition.x : 0f;
                float bx = b.TryGetComponent<RectTransform>(out var rtb) ? rtb.anchoredPosition.x : 0f;
                return ax.CompareTo(bx);
            });

            int fallbackIdx = 0;
            for (int i = 0; i < stageNodes.Count && fallbackIdx < unassignedButtons.Count; i++)
            {
                if (stageNodes[i].nodeButton == null)
                {
                    stageNodes[i].nodeButton = unassignedButtons[fallbackIdx++];
                }
            }
        }

        // Cache Image + TMP label and attach MapNodeClickAndDragHandler (single click/drag input source).
        for (int i = 0; i < stageNodes.Count; i++)
        {
            Button btn = stageNodes[i].nodeButton;
            if (btn == null) continue;

            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            if (stageNodes[i].nodeBackground == null)
                btn.TryGetComponent(out stageNodes[i].nodeBackground);

            if (stageNodes[i].nodeBackground != null)
                stageNodes[i].nodeBackground.raycastTarget = true;

            if (stageNodes[i].nodeLabelText == null)
                stageNodes[i].nodeLabelText = btn.GetComponentInChildren<TextMeshProUGUI>(true);

            if (!btn.TryGetComponent<MapNodeClickAndDragHandler>(out var handler))
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
    }

    public void OnStageNodeClicked(int index, bool isDoubleClick)
    {
        bool wasAlreadySelected = (index == currentSelectedNodeIndex);
        SelectStageNode(index);

        if ((isDoubleClick && doubleClickNodeToEnterStage) || (wasAlreadySelected && clickSelectedNodeAgainToEnter))
        {
            if (IsStageNodePlayable(index))
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
                stageTitle = "STAGE 01 : ABANDONED CLASSROOM",
                stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.",
                stageStoryLine = "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.",
                sceneToLoad = "The Action",
                isComingSoon = false,
                normalEnemyCount = 5,
                eliteBossCount = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK D",
                stageTitle = "STAGE 02 : SHIBUYA CROSSROADS",
                stageDescription = "Empty city intersection under a crimson sky. Defeat the stalking entities patrolling the crossing.",
                stageStoryLine = "STORY [RANK D] : Beyond the school gate, Shibuya stands frozen beneath a blood-red sky.",
                sceneToLoad = "Shibuya",
                isComingSoon = false,
                normalEnemyCount = 7,
                eliteBossCount = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK C",
                stageTitle = "STAGE 03 : SHATTERED SUBWAY LINE",
                stageDescription = "Underground tracks echoing with the whispers of fallen Magical Girls.",
                stageStoryLine = "STORY [RANK C] : Deep in the subway tunnels, the entities whisper fragments of our forgotten names.",
                sceneToLoad = defaultActionSceneName,
                isComingSoon = true,
                normalEnemyCount = 9,
                eliteBossCount = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK B",
                stageTitle = "STAGE 04 : FACELESS PLAZA",
                stageDescription = "Collapsed commercial center swarming with aggressive entities.",
                stageStoryLine = "STORY [RANK B] : My Soul Gem burns darker with every battle. The loop is beginning to fracture.",
                sceneToLoad = defaultActionSceneName,
                isComingSoon = true,
                normalEnemyCount = 11,
                eliteBossCount = 1
            },
            new StageNodeData
            {
                nodeCode = "RANK A",
                stageTitle = "STAGE 05 : CRIMSON SANCTUM",
                stageDescription = "Deep dimensional corridor guarded by Twin Elite Entities.",
                stageStoryLine = "STORY [RANK A] : At the end of the crimson corridor, the source of the anomaly pulses ahead.",
                sceneToLoad = defaultActionSceneName,
                isComingSoon = true,
                normalEnemyCount = 12,
                eliteBossCount = 2
            }
        };
    }

    // =========================================================================
    // 3. SETTINGS PANEL
    // =========================================================================

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
        var prog = GameProgressionManager.Instance;
        prog.ResetAllSaveData();

        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(prog.MasterVolume);
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(prog.MouseSensitivity);

        RefreshSettingsUI();
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

    // =========================================================================
    // 4. STAGE STATUS, SELECTION & DEPLOYMENT
    // =========================================================================

    public bool IsStageNodeComingSoon(int index)
    {
        if (stageNodes == null || index < 0 || index >= stageNodes.Count) return true;
        return stageNodes[index].isComingSoon;
    }

    public bool IsStageNodeUnlocked(int index)
    {
        if (IsStageNodeComingSoon(index)) return false;
        if (unlockAllNodesForPrototype) return true;
        return index <= GameProgressionManager.Instance.HighestUnlockedStageIndex;
    }

    public bool IsStageNodePlayable(int index)
    {
        return !IsStageNodeComingSoon(index) && IsStageNodeUnlocked(index);
    }

    private void RefreshHeaderStatus()
    {
        var prog = GameProgressionManager.Instance;

        if (energyStatusText != null)
        {
            energyStatusText.text = $"RANK {prog.CurrentRankLetter}  |  STAGE SELECT";
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
            bool comingSoon = IsStageNodeComingSoon(idx);
            bool isUnlocked = IsStageNodeUnlocked(idx);
            bool canDeploy = !comingSoon && isUnlocked;

            deployStageButton.interactable = canDeploy;

            if (deployStageButtonImage != null)
            {
                if (comingSoon)
                    deployStageButtonImage.color = ComingSoonDeployButtonColor;
                else if (!isUnlocked)
                    deployStageButtonImage.color = LockedDeployButtonColor;
                else
                    deployStageButtonImage.color = prog.GetProgressionUiThemeColor();
            }

            if (deployButtonLabelText != null)
            {
                string rankCode = GameProgressionManager.GetRankLetterForStage(idx);
                if (comingSoon)
                {
                    deployButtonLabelText.text = "COMING SOON";
                }
                else if (!isUnlocked)
                {
                    string prevRank = GameProgressionManager.GetRankLetterForStage(Mathf.Max(0, idx - 1));
                    deployButtonLabelText.text = $"LOCKED (CLEAR RANK {prevRank} FIRST)";
                }
                else
                {
                    deployButtonLabelText.text = $"ENTER STAGE {idx + 1} (RANK {rankCode}) >";
                }
            }
        }
    }

    public void SelectStageNode(int index)
    {
        if (stageNodes == null || stageNodes.Count == 0) return;
        currentSelectedNodeIndex = Mathf.Clamp(index, 0, stageNodes.Count - 1);

        StageNodeData selected = stageNodes[currentSelectedNodeIndex];
        var prog = GameProgressionManager.Instance;
        bool comingSoon = IsStageNodeComingSoon(currentSelectedNodeIndex);
        bool unlocked = IsStageNodeUnlocked(currentSelectedNodeIndex);

        if (selectedStageTitleText != null)
        {
            selectedStageTitleText.text = selected.stageTitle;
        }

        if (selectedStageDetailText != null)
        {
            string finalDetail = string.Empty;

            // 1. Status badge prefix ([COMING SOON], [LOCKED], [FIRST-CLEAR BONUS], or [CLEARED])
            if (comingSoon)
            {
                finalDetail += comingSoonBadgeText + " ";
            }
            else if (!unlocked)
            {
                string prevRank = GameProgressionManager.GetRankLetterForStage(Mathf.Max(0, currentSelectedNodeIndex - 1));
                finalDetail += string.Format(lockedBadgeFormat, prevRank) + " ";
            }
            else if (showFirstClearBonusTag)
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

            // 2. Stage Description
            finalDetail += selected.stageDescription;

            // 3. Optional (Normal: X | Boss: Y) suffix for playable stages
            if (!comingSoon && showEnemyCountInDetail && !string.IsNullOrEmpty(enemyCountSuffixFormat))
            {
                finalDetail += " " + string.Format(enemyCountSuffixFormat, selected.normalEnemyCount, selected.eliteBossCount);
            }

            // 4. Optional STORY line
            if (showStoryLineInDetail)
            {
                string story = !string.IsNullOrEmpty(selected.stageStoryLine)
                    ? selected.stageStoryLine
                    : GameProgressionManager.GetStoryLogForStage(currentSelectedNodeIndex);

                if (!string.IsNullOrEmpty(story))
                {
                    string formattedStory = !string.IsNullOrEmpty(storyLineColorFormat)
                        ? string.Format(storyLineColorFormat, story)
                        : story;
                    finalDetail += "\n" + formattedStory;
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
            if (node.nodeButton != null)
                node.nodeButton.interactable = true;

            bool comingSoon = IsStageNodeComingSoon(i);
            bool unlocked = IsStageNodeUnlocked(i);
            bool isSelected = (i == currentSelectedNodeIndex);
            bool isCleared = !comingSoon && prog.IsStageClearedBefore(i);

            if (autoSyncNodeLabels && node.nodeLabelText != null)
            {
                string code = string.IsNullOrEmpty(node.nodeCode)
                    ? $"RANK {GameProgressionManager.GetRankLetterForStage(i)}"
                    : node.nodeCode;

                if (comingSoon)
                    node.nodeLabelText.text = $"{code}\n<size=14>SOON</size>";
                else if (!unlocked)
                    node.nodeLabelText.text = $"{code}\n<size=14>LOCKED</size>";
                else if (isCleared)
                    node.nodeLabelText.text = $"{code}\n<size=14>CLEAR</size>";
                else
                    node.nodeLabelText.text = code;
            }

            Image bg = node.nodeBackground;
            if (bg == null) continue;

            if (isSelected)
            {
                bg.color = comingSoon ? new Color(0.55f, 0.45f, 0.78f, 1f) : milestoneAccent;
                bg.transform.localScale = Vector3.one * selectedNodeScale;
            }
            else if (comingSoon)
            {
                bg.color = ComingSoonNodeColor;
                bg.transform.localScale = Vector3.one * 0.94f;
            }
            else if (!unlocked)
            {
                bg.color = LockedNodeColor;
                bg.transform.localScale = Vector3.one * 0.95f;
            }
            else if (isCleared)
            {
                bg.color = ClearedNodeColor;
                bg.transform.localScale = Vector3.one;
            }
            else
            {
                bg.color = UnlockedNodeColor;
                bg.transform.localScale = Vector3.one;
            }
        }
    }

    public void DeploySelectedStage()
    {
        if (stageNodes == null || stageNodes.Count == 0)
        {
            LoadSceneSafe(defaultActionSceneName, lobbySceneName);
            return;
        }

        int idx = Mathf.Clamp(currentSelectedNodeIndex, 0, stageNodes.Count - 1);
        if (!IsStageNodePlayable(idx))
        {
            return;
        }

        StageNodeData selected = stageNodes[idx];
        var prog = GameProgressionManager.Instance;

        if (selected.stageData != null)
        {
            prog.SetActiveStageData(selected.stageData, idx);
        }
        else
        {
            prog.SelectStage(
                idx,
                selected.stageTitle,
                selected.normalEnemyCount,
                selected.eliteBossCount,
                0
            );
        }

        string targetScene = string.IsNullOrWhiteSpace(selected.sceneToLoad)
            ? defaultActionSceneName
            : selected.sceneToLoad.Trim();

        LoadSceneSafe(targetScene, defaultActionSceneName);
    }

    public void OnBackToLobbyClicked()
    {
        LoadSceneSafe(lobbySceneName, "The Lobby");
    }

    private static void LoadSceneSafe(string targetScene, string fallbackScene)
    {
        Time.timeScale = 1.0f;

        if (!string.IsNullOrEmpty(targetScene) && Application.CanStreamedLevelBeLoaded(targetScene))
        {
            SceneManager.LoadScene(targetScene);
            return;
        }

        if (!string.IsNullOrEmpty(fallbackScene) && fallbackScene != targetScene && Application.CanStreamedLevelBeLoaded(fallbackScene))
        {
            Debug.LogWarning($"[StageMapManager] Scene '{targetScene}' is not in Build Settings. Falling back to '{fallbackScene}'.");
            SceneManager.LoadScene(fallbackScene);
            return;
        }

        Debug.LogError($"[StageMapManager] Cannot load scene '{targetScene}' (and fallback '{fallbackScene}' is unavailable). Add it to File > Build Profiles / Build Settings.");
    }
}
