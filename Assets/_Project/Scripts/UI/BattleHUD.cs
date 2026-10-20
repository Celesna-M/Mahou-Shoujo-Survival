using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Battle HUD & In-Game Settings / Pause Menu (Scene 4: The Action)
/// - Pure ASCII English text (no unsupported symbols)
/// - Full Settings Panel in Action scene (ESC or Settings Button) with Volume, Sensitivity, Fullscreen, and Mid-Game Exit buttons (Exit to Lobby / StageMap / MainMenu)
/// - Top-Left Floating Text "+ [STAR DUST] 15" / "+ [CORE] 1"
/// </summary>
public class BattleHUD : MonoBehaviour
{
    public static BattleHUD Instance { get; private set; }

    [Header("Player Status Bars")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Image manaFillImage;

    [Header("Boss Health & Stagger Bar")]
    [SerializeField] private GameObject bossBarContainer;
    [SerializeField] private Image bossHpFillImage;
    [SerializeField] private Image bossStaggerFillImage;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Execution Prompt [F]")]
    [SerializeField] private TextMeshProUGUI executePromptText;
    [SerializeField] private float executionDetectionRange = 3.5f;

    [Header("Cinematic Letterbox")]
    [SerializeField] private GameObject letterboxGroup;
    [SerializeField] private GameObject topBar;
    [SerializeField] private GameObject bottomBar;
    [SerializeField] private Image whiteFlashImage;

    [Header("Damage Screen Flash")]
    [SerializeField] private Image damageFlashImage;

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button returnToHubButton;

    [Header("Victory / Score Summary Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI scoreSummaryText;
    [SerializeField] private Button victoryRetryButton;
    [SerializeField] private Button victoryReturnButton;

    [Header("Economy Counter & Top-Left Floating Text")]
    [SerializeField] private TextMeshProUGUI starDustText;
    [SerializeField] private TextMeshProUGUI shatteredCoreText;
    [SerializeField] private RectTransform topLeftFloatingAnchor;

    [Header("Settings Panel (In-Game Pause & Exit Mid-Game)")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private Button toggleFullscreenButton;
    [SerializeField] private TextMeshProUGUI fullscreenStatusText;
    [SerializeField] private Button closeSettingsButton;
    [Tooltip("Button inside Settings Panel to quit the current battle and return to Lobby")]
    [SerializeField] private Button exitToLobbyMidGameButton;
    [Tooltip("Optional button inside Settings Panel to quit battle and return to StageMap")]
    [SerializeField] private Button exitToStageMapMidGameButton;
    [Tooltip("Optional button inside Settings Panel to quit battle and return to MainMenu")]
    [SerializeField] private Button exitToMainMenuMidGameButton;

    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;
    private PlayerExecution playerExecution;
    private Canvas rootCanvas;
    private TMP_FontAsset cachedFont;

    private Coroutine flashCoroutine;
    private Coroutine whiteFlashCoroutine;
    private float promptCheckTimer = 0f;
    private int activeFloatingCount = 0;
    private bool isSettingsOpen = false;

    public bool IsSettingsOpen => isSettingsOpen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        rootCanvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindAnyObjectByType<Canvas>();

        var anyTmp = GetComponentInChildren<TextMeshProUGUI>(true);
        if (anyTmp != null) cachedFont = anyTmp.font;

        if (topBar == null) topBar = GameObject.Find("Top_Bar");
        if (bottomBar == null) bottomBar = GameObject.Find("Bottom_Bar");

        HideUnnecessaryUI();
    }

    private void Start()
    {
        GameProgressionManager.Instance.LoadFromPrefs();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerStamina = player.GetComponent<PlayerStamina>();
            playerExecution = player.GetComponent<PlayerExecution>();

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += OnHealthChanged;
                playerHealth.OnCurrencyChanged += OnCurrencyChanged;
                playerHealth.OnPlayerHurt += TriggerDamageFlash;
                playerHealth.OnPlayerDeath += ShowGameOverScreen;

                OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                OnCurrencyChanged(playerHealth.StarDust, playerHealth.ShatteredCore);
            }

            if (playerStamina != null)
            {
                playerStamina.OnStaminaChanged += OnStaminaChanged;
                OnStaminaChanged(playerStamina.CurrentStamina, playerStamina.MaxStamina);
            }
        }

        if (manaFillImage != null)
        {
            if (manaFillImage.transform.parent != null && manaFillImage.transform.parent != transform)
                manaFillImage.transform.parent.gameObject.SetActive(false);
            else
                manaFillImage.gameObject.SetActive(false);
        }

        HideUnnecessaryUI();

        if (retryButton != null && playerHealth != null)
            retryButton.onClick.AddListener(playerHealth.RestartStage);

        if (returnToHubButton != null)
            returnToHubButton.onClick.AddListener(ReturnToLobby);

        if (victoryRetryButton != null && playerHealth != null)
            victoryRetryButton.onClick.AddListener(playerHealth.RestartStage);

        if (victoryReturnButton != null)
            victoryReturnButton.onClick.AddListener(ReturnToLobby);

        BindSettingsPanelEvents();
        RefreshSettingsUI();
    }

    private void HideUnnecessaryUI()
    {
        if (bossBarContainer != null) bossBarContainer.SetActive(false);
        if (executePromptText != null) executePromptText.gameObject.SetActive(false);
        if (letterboxGroup != null) letterboxGroup.SetActive(false);
        if (topBar != null) topBar.SetActive(false);
        if (bottomBar != null) bottomBar.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(1f, 0f, 0f, 0f);
            damageFlashImage.raycastTarget = false;
        }

        if (whiteFlashImage != null)
        {
            whiteFlashImage.color = new Color(1f, 1f, 1f, 0f);
            whiteFlashImage.raycastTarget = false;
        }
    }

    private void BindSettingsPanelEvents()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.AddListener(ToggleSettingsPanel);
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

        if (exitToLobbyMidGameButton != null)
        {
            exitToLobbyMidGameButton.onClick.RemoveAllListeners();
            exitToLobbyMidGameButton.onClick.AddListener(ReturnToLobby);
        }

        if (exitToStageMapMidGameButton != null)
        {
            exitToStageMapMidGameButton.onClick.RemoveAllListeners();
            exitToStageMapMidGameButton.onClick.AddListener(ReturnToStageMap);
        }

        if (exitToMainMenuMidGameButton != null)
        {
            exitToMainMenuMidGameButton.onClick.RemoveAllListeners();
            exitToMainMenuMidGameButton.onClick.AddListener(ReturnToMainMenu);
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
        if (isSettingsOpen) CloseSettingsPanel();
        else OpenSettingsPanel();
    }

    public void OpenSettingsPanel()
    {
        if (settingsPanel == null) return;
        isSettingsOpen = true;
        settingsPanel.SetActive(true);
        RefreshSettingsUI();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel == null) return;
        isSettingsOpen = false;
        settingsPanel.SetActive(false);

        bool isEndScreenActive = (gameOverPanel != null && gameOverPanel.activeInHierarchy) ||
                                 (victoryPanel != null && victoryPanel.activeInHierarchy);
        if (!isEndScreenActive)
        {
            Time.timeScale = 1.0f;
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

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
            playerHealth.OnCurrencyChanged -= OnCurrencyChanged;
            playerHealth.OnPlayerHurt -= TriggerDamageFlash;
            playerHealth.OnPlayerDeath -= ShowGameOverScreen;
        }

        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged -= OnStaminaChanged;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPanel != null)
            {
                ToggleSettingsPanel();
                return;
            }
        }

        if (isSettingsOpen) return;

        promptCheckTimer -= Time.deltaTime;
        if (promptCheckTimer <= 0f)
        {
            promptCheckTimer = 0.12f;
            UpdateExecutionPromptVisibility();
        }

        if (gameOverPanel != null && gameOverPanel.activeInHierarchy)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (playerHealth != null) playerHealth.RestartStage();
            }
        }

        if (victoryPanel != null && victoryPanel.activeInHierarchy)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ReturnToLobby();
            }
        }
    }

    private void UpdateExecutionPromptVisibility()
    {
        if (executePromptText == null) return;

        bool canExecuteNearby = false;
        float range = (playerExecution != null) ? playerExecution.ExecutionRange : executionDetectionRange;

        if (playerHealth != null && !playerHealth.IsDead && (playerExecution == null || !playerExecution.IsExecuting))
        {
            Collider[] colliders = Physics.OverlapSphere(playerHealth.transform.position, range);
            for (int i = 0; i < colliders.Length; i++)
            {
                var executable = colliders[i].GetComponentInParent<IExecutable>() ?? colliders[i].GetComponent<IExecutable>();
                if (executable != null && executable.CanBeExecuted)
                {
                    canExecuteNearby = true;
                    break;
                }
            }
        }

        if (executePromptText.gameObject.activeSelf != canExecuteNearby)
        {
            if (canExecuteNearby)
            {
                executePromptText.text = "[F] EXECUTE (HEAL HP)";
            }
            executePromptText.gameObject.SetActive(canExecuteNearby);
        }
    }

    // =========================================================================
    // PHASE 5: TOP-LEFT FLOATING UI FEEDBACK (Pure ASCII English)
    // =========================================================================

    public void SpawnFloatingFeedback(string iconSymbol, string label, int amount, Color textColor, Vector3 worldPos, string suffix = "")
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();

        if (rootCanvas == null) return;

        GameObject floatObj = new GameObject($"FloatingFeedback_{label}");
        floatObj.transform.SetParent(rootCanvas.transform, false);

        RectTransform rt = floatObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(380f, 45f);

        float stackOffset = (activeFloatingCount % 5) * 30f;

        if (topLeftFloatingAnchor != null)
        {
            rt.position = topLeftFloatingAnchor.position - new Vector3(0f, stackOffset, 0f);
        }
        else if (starDustText != null)
        {
            rt.position = starDustText.rectTransform.position + new Vector3(10f, -38f - stackOffset, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(36f, -115f - stackOffset);
        }

        TextMeshProUGUI tmp = floatObj.AddComponent<TextMeshProUGUI>();
        if (cachedFont != null) tmp.font = cachedFont;
        tmp.text = $"+ [{iconSymbol}] {amount}{suffix}";
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        StartCoroutine(AnimateFloatingFeedbackRoutine(rt, tmp));
    }

    private IEnumerator AnimateFloatingFeedbackRoutine(RectTransform rt, TextMeshProUGUI tmp)
    {
        activeFloatingCount++;
        float duration = 1.2f;
        float elapsed = 0f;
        float floatSpeed = 52f;
        Color baseColor = tmp.color;

        while (elapsed < duration && rt != null && tmp != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            rt.position += Vector3.up * (floatSpeed * Time.unscaledDeltaTime);

            float alpha = t < 0.35f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.35f) / 0.65f);
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        activeFloatingCount = Mathf.Max(0, activeFloatingCount - 1);
        if (rt != null) Destroy(rt.gameObject);
    }

    // --- UI Update Events ---

    private void OnHealthChanged(float current, float max)
    {
        if (hpFillImage != null && max > 0) hpFillImage.fillAmount = current / max;
    }

    private void OnStaminaChanged(float current, float max)
    {
        if (staminaFillImage != null && max > 0) staminaFillImage.fillAmount = current / max;
    }

    private void OnCurrencyChanged(int starDust, int shatteredCore)
    {
        if (starDustText != null) starDustText.text = $"STAR DUST : {starDust}";
        if (shatteredCoreText != null) shatteredCoreText.text = $"CORE : {shatteredCore}";
    }

    // --- Boss Bar Management ---

    public void ShowBossBar(string bossName, float currentHp, float maxHp, float stagger, float maxStagger)
    {
        if (bossBarContainer != null) bossBarContainer.SetActive(true);
        if (bossNameText != null) bossNameText.text = bossName;
        UpdateBossHealth(currentHp, maxHp);
        UpdateBossStagger(stagger, maxStagger);
    }

    public void UpdateBossHealth(float currentHp, float maxHp)
    {
        if (bossHpFillImage != null && maxHp > 0)
            bossHpFillImage.fillAmount = currentHp / maxHp;
    }

    public void UpdateBossStagger(float currentStagger, float maxStagger)
    {
        if (bossStaggerFillImage != null && maxStagger > 0)
            bossStaggerFillImage.fillAmount = currentStagger / maxStagger;
    }

    public void HideBossBar()
    {
        if (bossBarContainer != null) bossBarContainer.SetActive(false);
    }

    // --- Screen Flashes ---

    public void SetCinematicBars(bool active)
    {
        if (letterboxGroup != null) letterboxGroup.SetActive(active);
        if (topBar != null) topBar.SetActive(active);
        if (bottomBar != null) bottomBar.SetActive(active);
    }

    public void TriggerWhiteFlash(float duration = 0.12f)
    {
        if (whiteFlashImage == null) return;
        if (whiteFlashCoroutine != null) StopCoroutine(whiteFlashCoroutine);
        whiteFlashCoroutine = StartCoroutine(DoWhiteFlashRoutine(duration));
    }

    private IEnumerator DoWhiteFlashRoutine(float duration)
    {
        whiteFlashImage.color = new Color(1f, 1f, 1f, 0.9f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0.9f, 0f, elapsed / duration);
            whiteFlashImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
        whiteFlashImage.color = new Color(1f, 1f, 1f, 0f);
        whiteFlashCoroutine = null;
    }

    public void TriggerDamageFlash()
    {
        if (damageFlashImage == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DoDamageFlashRoutine());
    }

    private IEnumerator DoDamageFlashRoutine()
    {
        damageFlashImage.color = new Color(1f, 0f, 0f, 0.35f);
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0.35f, 0f, elapsed / duration);
            damageFlashImage.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }
        damageFlashImage.color = new Color(1f, 0f, 0f, 0f);
        flashCoroutine = null;
    }

    // --- Game Over, Victory & Mid-Game Exit States ---

    public void ShowGameOverScreen()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0.2f;
    }

    public void ShowVictoryScreen()
    {
        SaveCurrency();

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            UpdateScoreSummaryDisplay();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0.3f;
    }

    private void UpdateScoreSummaryDisplay()
    {
        if (playerHealth == null) return;

        if (scoreSummaryText == null && victoryPanel != null)
        {
            Transform existing = victoryPanel.transform.Find("AutoScoreSummaryText");
            if (existing != null)
            {
                scoreSummaryText = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject summaryGo = new GameObject("AutoScoreSummaryText");
                summaryGo.transform.SetParent(victoryPanel.transform, false);
                RectTransform rt = summaryGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(540f, 160f);
                rt.anchoredPosition = new Vector2(0f, -20f);

                scoreSummaryText = summaryGo.AddComponent<TextMeshProUGUI>();
                if (cachedFont != null) scoreSummaryText.font = cachedFont;
                scoreSummaryText.fontSize = 24f;
                scoreSummaryText.alignment = TextAlignmentOptions.Center;
                scoreSummaryText.color = Color.white;
            }
        }

        if (scoreSummaryText != null)
        {
            scoreSummaryText.text =
                $"<b>SCORE SUMMARY</b>\n" +
                $"<color=#FFE040>+ [STAR DUST] : {playerHealth.StageStarDustEarned} (TOTAL: {playerHealth.StarDust})</color>\n" +
                $"<color=#F259FF>+ [SHATTERED CORE] : {playerHealth.StageShatteredCoreEarned} (TOTAL: {playerHealth.ShatteredCore})</color>";
        }
    }

    public void ReturnToStageMap()
    {
        SaveCurrency();
        Time.timeScale = 1.0f;
        EnemyController.GlobalExecutionFreeze = false;
        if (Application.CanStreamedLevelBeLoaded("StageMap"))
            SceneManager.LoadScene("StageMap");
        else
            SceneManager.LoadScene("The lobby");
    }

    public void ReturnToLobby()
    {
        SaveCurrency();
        Time.timeScale = 1.0f;
        EnemyController.GlobalExecutionFreeze = false;
        SceneManager.LoadScene("The lobby");
    }

    public void ReturnToMainMenu()
    {
        SaveCurrency();
        Time.timeScale = 1.0f;
        EnemyController.GlobalExecutionFreeze = false;
        if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            SceneManager.LoadScene("MainMenu");
        else
            SceneManager.LoadScene("The lobby");
    }

    private void SaveCurrency()
    {
        if (GameProgressionManager.Instance != null)
        {
            GameProgressionManager.Instance.SaveToPrefs();
        }
    }
}