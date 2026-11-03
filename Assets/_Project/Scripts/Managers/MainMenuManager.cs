using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene 1: จัดการหน้าเมนูหลัก (Main Menu) — รุ่นประกอบ UI เอง (No Auto-Gen)
/// ใช้เมาส์ลาก Slider ปรับค่าเสียงและความไวเมาส์โดยตรงใน Settings_Panel (เต็มจอ Scale 0.99 : ซ้ายหัวข้อ / ขวาปรับแต่ง)
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("ชื่อฉาก Lobby / Hub ที่จะโหลดเมื่อกดปุ่ม START")]
    [SerializeField] private string lobbySceneName = "The lobby";

    [Header("Main Menu UI Buttons (ลากปุ่มจาก Hierarchy มาใส่)")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings Panel & Mouse Sliders (ซ้ายหัวข้อ / ขวาใช้เมาส์ลากปรับค่า)")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("ลาก Slider ปรับเสียงฝั่งขวามาใส่ (ใช้เมาส์ลากเลื่อนซ้าย-ขวา)")]
    [SerializeField] private Slider volumeSlider;
    [Tooltip("ข้อความตัวเลขเปอร์เซ็นต์เสียงฝั่งขวา (เช่น 80%)")]
    [SerializeField] private TextMeshProUGUI volumeValueText;

    [Tooltip("ลาก Slider ปรับความไวเมาส์ฝั่งขวามาใส่ (ใช้เมาส์ลากเลื่อนซ้าย-ขวา)")]
    [SerializeField] private Slider sensitivitySlider;
    [Tooltip("ข้อความตัวเลขความไวเมาส์ฝั่งขวา (เช่น 1.0x)")]
    [SerializeField] private TextMeshProUGUI sensitivityValueText;

    [SerializeField] private Button toggleFullscreenButton;
    [Tooltip("ข้อความลูกในปุ่มสลับโหมดหน้าจอฝั่งขวา (FULLSCREEN / WINDOWED)")]
    [SerializeField] private TextMeshProUGUI fullscreenStatusText;
    [SerializeField] private Button resetSaveDataButton;
    [SerializeField] private Button closeSettingsButton;

    private void Start()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameProgressionManager.Instance.LoadFromPrefs();

        BindUIEvents();
        RefreshSettingsUI();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void BindUIEvents()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartGameClicked);
        }

        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitGameClicked);
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
            volumeSlider.wholeNumbers = false;
            volumeSlider.SetValueWithoutNotify(GameProgressionManager.Instance.MasterVolume);
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveAllListeners();
            sensitivitySlider.minValue = 0.2f;
            sensitivitySlider.maxValue = 3.0f;
            sensitivitySlider.wholeNumbers = false;
            sensitivitySlider.SetValueWithoutNotify(GameProgressionManager.Instance.MouseSensitivity);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderChanged);
        }
    }

    public void OnStartGameClicked()
    {
        SceneManager.LoadScene(lobbySceneName);
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
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameProgressionManager.Instance.MasterVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameProgressionManager.Instance.MouseSensitivity);
        RefreshSettingsUI();
        Debug.Log("<color=yellow>[MainMenu] ล้างข้อมูลเซฟเรียบร้อยแล้ว!</color>");
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

    public void OnQuitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ MainMenuManager.cs]
1. Start()
   - รีเซ็ต TimeScale, ปลดล็อกเมาส์, โหลดข้อมูลเซฟจาก GameProgressionManager และผูกอีเวนต์ปุ่มในหน้าเมนูหลัก

2. BindUIEvents()
   - เชื่อมโยงปุ่ม Start, Settings, Quit, Fullscreen, Reset Save และแถบเลื่อน Slider ปรับเสียง/ความไวเมาส์เข้ากับฟังก์ชันทำงาน

3. OnStartGameClicked()
   - โหลดเข้าสู่ฉากห้องพัก (The Lobby / Hub)

4. OpenSettingsPanel() / CloseSettingsPanel()
   - เปิด/ปิดหน้าต่างตั้งค่า (Settings Panel)

5. OnVolumeSliderChanged(float val) / OnSensitivitySliderChanged(float val)
   - บันทึกค่าระดับเสียงหลักและความไวเมาส์ลงใน GameProgressionManager ทันทีที่ผู้เล่นลากแถบเลื่อน

6. OnToggleFullscreenClicked()
   - สลับโหมดหน้าจอระหว่าง Fullscreen และ Windowed

7. OnResetSaveDataClicked()
   - ล้างข้อมูลเซฟทั้งหมดกลับเป็นค่าเริ่มต้น (Rank E)

8. RefreshSettingsUI()
   - อัปเดตตัวเลขเปอร์เซ็นต์เสียง ความไวเมาส์ และสถานะหน้าจอบน UI

9. OnQuitGameClicked()
   - ออกจากเกม (หรือหยุดโหมด Play ใน Unity Editor)
===============================================================================
*/
