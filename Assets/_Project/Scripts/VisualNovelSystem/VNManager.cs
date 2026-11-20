using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// VisualNovelSystem: ตัวจัดการฉากสนทนา Visual Novel ก่อนและหลังต่อสู้ (GDD Section 6 & 8.2)
/// - รองรับรูปตัวละคร 2D ครึ่งตัว (ซ้าย = ผู้เล่น, ขวา = บอส/NPC) พร้อมสลับอารมณ์ Normal / Angry
/// - ดึงชื่อ รูปภาพ และบทพูดเฉพาะตัวของตัวละครที่ผู้เล่นเลือกใช้อยู่อัตโนมัติ (Dynamic Character Reaction)
/// - รองรับปุ่ม Click / Spacebar (แสดงข้อความเต็มหรือไปประโยคถัดไป), ปุ่ม Skip, และปุ่ม Auto (ทุก 2.5 วินาที)
/// - หากยังไม่ได้สร้าง UI กล่องข้อความ VN ในฉาก ระบบจะสร้าง Overlay Canvas ให้ชั่วคราวอัตโนมัติ
/// </summary>
public class VNManager : MonoBehaviour
{
    public static VNManager Instance { get; private set; }

    [Header("1. VN UI Panel & Portraits (ลาก UI จากฉากมาใส่ หรือเว้นว่างเพื่อให้ระบบสร้างอัตโนมัติ)")]
    [SerializeField] private GameObject vnRootPanel;
    [SerializeField] private Image leftPlayerPortrait;
    [SerializeField] private Image rightNpcOrBossPortrait;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;

    [Header("2. VN Control Buttons")]
    [SerializeField] private Button nextAdvanceButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button autoToggleButton;
    [SerializeField] private TextMeshProUGUI autoButtonLabel;

    [Header("3. Pacing & Typewriter Settings")]
    [SerializeField] private float charTypeInterval = 0.022f;
    [SerializeField] private float autoAdvanceDelay = 2.5f;
    [SerializeField] private Color activeSpeakerTint = Color.white;
    [SerializeField] private Color inactiveSpeakerTint = new Color(0.45f, 0.45f, 0.5f, 0.85f);

    private DialogueData_SO currentDialogue;
    private int currentLineIndex = 0;
    private bool isPlayingVN = false;
    private bool isTypingLine = false;
    private bool isAutoMode = false;
    private string fullCurrentLineText = "";
    private float autoTimer = 0f;
    private Action onDialogueFinishedCallback;
    private Coroutine typingCoroutine;

    public bool IsPlayingVN => isPlayingVN;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (vnRootPanel != null)
            vnRootPanel.SetActive(false);

        BindButtons();
    }

    private void BindButtons()
    {
        if (nextAdvanceButton != null)
        {
            nextAdvanceButton.onClick.RemoveAllListeners();
            nextAdvanceButton.onClick.AddListener(OnAdvanceClicked);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipDialogue);
        }

        if (autoToggleButton != null)
        {
            autoToggleButton.onClick.RemoveAllListeners();
            autoToggleButton.onClick.AddListener(ToggleAutoMode);
        }
    }

    public static VNManager EnsureInstance()
    {
        if (Instance == null)
        {
            Instance = FindAnyObjectByType<VNManager>();
            if (Instance == null)
            {
                GameObject go = new GameObject("[VNManager]");
                Instance = go.AddComponent<VNManager>();
            }
        }
        return Instance;
    }

    public void StartDialogue(DialogueData_SO dialogueData, Action onFinished)
    {
        if (dialogueData == null || dialogueData.lines == null || dialogueData.lines.Count == 0)
        {
            onFinished?.Invoke();
            return;
        }

        EnsureRuntimeFallbackUIIfMissing();

        currentDialogue = dialogueData;
        currentLineIndex = 0;
        isPlayingVN = true;
        isAutoMode = false;
        autoTimer = 0f;
        onDialogueFinishedCallback = onFinished;

        if (vnRootPanel != null)
        {
            vnRootPanel.SetActive(true);
            vnRootPanel.transform.SetAsLastSibling();
        }

        UpdateAutoLabel();

        // Pause gameplay physics while reading VN and unlock cursor
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowCurrentLine();
    }

    private void Update()
    {
        if (!isPlayingVN) return;

        bool advancePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            advancePressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            advancePressed = true;
#endif

        if (advancePressed)
        {
            OnAdvanceClicked();
            return;
        }

        if (isAutoMode && !isTypingLine)
        {
            autoTimer += Time.unscaledDeltaTime;
            if (autoTimer >= autoAdvanceDelay)
            {
                autoTimer = 0f;
                AdvanceToNextLine();
            }
        }
    }

    public void OnAdvanceClicked()
    {
        if (!isPlayingVN) return;

        if (isTypingLine)
        {
            // แสดงข้อความทั้งประโยคทันที
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            isTypingLine = false;
            if (dialogueBodyText != null)
                dialogueBodyText.text = fullCurrentLineText;
            autoTimer = 0f;
        }
        else
        {
            AdvanceToNextLine();
        }
    }

    public void ToggleAutoMode()
    {
        isAutoMode = !isAutoMode;
        autoTimer = 0f;
        UpdateAutoLabel();
    }

    private void UpdateAutoLabel()
    {
        if (autoButtonLabel != null)
            autoButtonLabel.text = isAutoMode ? "AUTO : ON" : "AUTO : OFF";
    }

    public void SkipDialogue()
    {
        if (!isPlayingVN) return;
        FinishDialogue();
    }

    private void AdvanceToNextLine()
    {
        currentLineIndex++;
        if (currentDialogue == null || currentLineIndex >= currentDialogue.lines.Count)
        {
            FinishDialogue();
        }
        else
        {
            ShowCurrentLine();
        }
    }

    private void ShowCurrentLine()
    {
        if (currentDialogue == null || currentLineIndex >= currentDialogue.lines.Count)
        {
            FinishDialogue();
            return;
        }

        DialogueLine line = currentDialogue.lines[currentLineIndex];
        var prog = GameProgressionManager.Instance;
        string activeCharId = prog != null ? prog.SelectedCharacterId : "CHAR_01";
        string activeCharName = prog != null ? prog.SelectedCharacterName : "HIKARI";
        CharacterData_SO activeCharSO = prog != null ? prog.ActiveCharacterData : null;

        bool isPlayerSpeaking = (line.speaker == VNSpeakerType.CurrentPlayer);

        // 1. Update Speaker Name
        if (speakerNameText != null)
        {
            speakerNameText.text = isPlayerSpeaking
                ? (activeCharSO != null ? activeCharSO.characterDisplayName : activeCharName)
                : line.customSpeakerName;
        }

        // 2. Update Left/Right Portraits & Highlight Active Speaker
        if (leftPlayerPortrait != null)
        {
            if (activeCharSO != null)
            {
                Sprite pSprite = (line.emotion == VNEmotionType.Angry && activeCharSO.vnPortraitAngry != null)
                    ? activeCharSO.vnPortraitAngry
                    : activeCharSO.vnPortraitNormal;
                if (pSprite != null) leftPlayerPortrait.sprite = pSprite;
            }
            leftPlayerPortrait.gameObject.SetActive(leftPlayerPortrait.sprite != null);
            leftPlayerPortrait.color = isPlayerSpeaking ? activeSpeakerTint : inactiveSpeakerTint;
        }

        if (rightNpcOrBossPortrait != null)
        {
            if (!isPlayerSpeaking && line.customPortrait != null)
            {
                rightNpcOrBossPortrait.sprite = line.customPortrait;
            }
            rightNpcOrBossPortrait.gameObject.SetActive(rightNpcOrBossPortrait.sprite != null);
            rightNpcOrBossPortrait.color = !isPlayerSpeaking ? activeSpeakerTint : inactiveSpeakerTint;
        }

        // 3. Resolve Dynamic Text for Current Character & Start Typewriter
        fullCurrentLineText = line.ResolveTextForCharacter(activeCharId);
        autoTimer = 0f;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypewriterRoutine(fullCurrentLineText));
    }

    private IEnumerator TypewriterRoutine(string text)
    {
        isTypingLine = true;
        if (dialogueBodyText != null)
            dialogueBodyText.text = "";

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int closeIdx = text.IndexOf('>', i);
                if (closeIdx != -1) i = closeIdx;
            }

            if (dialogueBodyText != null)
                dialogueBodyText.text = text.Substring(0, i + 1);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.005f, charTypeInterval));
        }

        if (dialogueBodyText != null)
            dialogueBodyText.text = text;

        isTypingLine = false;
        typingCoroutine = null;
    }

    private void FinishDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isPlayingVN = false;
        isTypingLine = false;
        isAutoMode = false;

        if (vnRootPanel != null)
            vnRootPanel.SetActive(false);

        Time.timeScale = 1.0f;

        Action cb = onDialogueFinishedCallback;
        onDialogueFinishedCallback = null;
        cb?.Invoke();
    }

    private void EnsureRuntimeFallbackUIIfMissing()
    {
        if (vnRootPanel != null && dialogueBodyText != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cObj = new GameObject("VN_RuntimeCanvas");
            canvas = cObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            cObj.AddComponent<CanvasScaler>();
            cObj.AddComponent<GraphicRaycaster>();
        }

        vnRootPanel = new GameObject("VN_OverlayPanel");
        vnRootPanel.transform.SetParent(canvas.transform, false);
        RectTransform rootRt = vnRootPanel.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image dimBg = vnRootPanel.AddComponent<Image>();
        dimBg.color = new Color(0.05f, 0.04f, 0.08f, 0.72f);

        // Dialogue Box at Bottom
        GameObject box = new GameObject("VN_DialogueBox");
        box.transform.SetParent(vnRootPanel.transform, false);
        RectTransform boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.08f, 0.04f);
        boxRt.anchorMax = new Vector2(0.92f, 0.30f);
        boxRt.offsetMin = Vector2.zero;
        boxRt.offsetMax = Vector2.zero;

        Image boxBg = box.AddComponent<Image>();
        boxBg.color = new Color(0.09f, 0.07f, 0.14f, 0.94f);
        nextAdvanceButton = box.AddComponent<Button>();

        // Speaker Name
        GameObject nameGo = new GameObject("SpeakerNameText");
        nameGo.transform.SetParent(box.transform, false);
        RectTransform nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.03f, 0.72f);
        nameRt.anchorMax = new Vector2(0.65f, 0.95f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;
        speakerNameText = nameGo.AddComponent<TextMeshProUGUI>();
        speakerNameText.fontSize = 26f;
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.color = new Color(1f, 0.65f, 0.85f, 1f);

        // Dialogue Body
        GameObject bodyGo = new GameObject("DialogueBodyText");
        bodyGo.transform.SetParent(box.transform, false);
        RectTransform bodyRt = bodyGo.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.03f, 0.12f);
        bodyRt.anchorMax = new Vector2(0.97f, 0.68f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;
        dialogueBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        dialogueBodyText.fontSize = 22f;
        dialogueBodyText.color = Color.white;

        // Skip Button (Top-Right of Box)
        skipButton = CreateSmallButton(box.transform, "SkipButton", "SKIP >>", new Vector2(0.84f, 0.75f), new Vector2(0.97f, 0.94f), out _);
        // Auto Button
        autoToggleButton = CreateSmallButton(box.transform, "AutoButton", "AUTO : OFF", new Vector2(0.68f, 0.75f), new Vector2(0.82f, 0.94f), out autoButtonLabel);

        BindButtons();
    }

    private Button CreateSmallButton(Transform parent, string name, string label, Vector2 minAnchor, Vector2 maxAnchor, out TextMeshProUGUI txt)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = minAnchor;
        rt.anchorMax = maxAnchor;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.28f, 0.18f, 0.36f, 0.95f);
        Button btn = go.AddComponent<Button>();

        GameObject lblGo = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        RectTransform lRt = lblGo.AddComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        txt = lblGo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 16f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        return btn;
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดใน VNManager.cs (VisualNovelSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. StartDialogue(DialogueData_SO dialogueData, Action onFinished)
 *    - เปิดฉากสนทนา Visual Novel หยุดเวลาเกมชั่วคราว (Time.timeScale = 0)
 *      และส่ง Callback onFinished กลับไปให้ WaveSpawner/ArenaManager เมื่ออ่านจบหรือกด Skip
 *
 * 2. ShowCurrentLine() & TypewriterRoutine(string text)
 *    - ดึงรูป Portrait (ซ้าย = ผู้เล่น, ขวา = บอส/NPC) และบทพูดเฉพาะตัวของตัวละครที่เลือกใช้อยู่
 *      (Dynamic Character Reaction) มาแสดงพร้อมเอฟเฟกต์พิมพ์ดีด
 *
 * 3. OnAdvanceClicked(), ToggleAutoMode(), SkipDialogue()
 *    - รองรับการกดคลิก/Spacebar เพื่อข้ามพิมพ์ดีดหรือไปประโยคถัดไป, ปุ่ม Auto เล่นอัตโนมัติทุก 2.5 วินาที,
 *      และปุ่ม Skip เพื่อเข้าสู่ฉากต่อสู้ทันที
 * ============================================================================ */
