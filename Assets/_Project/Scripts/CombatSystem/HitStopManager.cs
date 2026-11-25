using System.Collections;
using UnityEngine;

/// <summary>
/// Manages brief hit-stop slow-motion on impact while staying 100% pause-safe
/// (never unpauses the game if the Pause/Settings menu is opened during hit-stop).
/// </summary>
public class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;
    public static HitStopManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<HitStopManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("[HitStopManager]");
                    instance = go.AddComponent<HitStopManager>();
                }
            }
            return instance;
        }
    }

    private Coroutine hitStopCoroutine;
    private float defaultFixedDeltaTime = 0.02f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime > 0.001f ? Time.fixedDeltaTime : 0.02f;
    }

    public static void Trigger(float duration = 0.05f, float timeScale = 0.1f)
    {
        if (Instance != null)
        {
            Instance.TriggerHitStop(duration, timeScale);
        }
    }

    public void TriggerHitStop(float duration = 0.05f, float timeScale = 0.1f)
    {
        if (duration <= 0f) return;
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;

        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(DoHitStop(duration, Mathf.Clamp(timeScale, 0.02f, 1.0f)));
    }

    private IEnumerator DoHitStop(float duration, float hitTimeScale)
    {
        Time.timeScale = hitTimeScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * hitTimeScale;

        yield return new WaitForSecondsRealtime(duration);

        // Only restore timescale if Pause/Settings or Death did not freeze the game during hit-stop.
        bool isExternallyPaused = (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen)
                               || Time.timeScale <= 0.005f;
        if (!isExternallyPaused)
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }

        hitStopCoroutine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (!(BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen))
            {
                Time.timeScale = 1.0f;
                Time.fixedDeltaTime = defaultFixedDeltaTime;
            }
            instance = null;
        }
    }
}
