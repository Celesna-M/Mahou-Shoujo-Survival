using System.Collections;
using UnityEngine;

/// <summary>
/// จัดการระบบ Hit-stop (หน่วงเวลาสั้นๆ เมื่อโจมตีโดนศัตรู) ตาม Phase 2
/// รองรับการเรียกใช้งานอัตโนมัติแม้ลืมวาง GameObject ในฉาก
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

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
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
        // ป้องกันการรบกวนฉากจบหรือตอนผู้เล่นตายที่ปรับ Time.timeScale ค้างไว้
        if (Time.timeScale <= 0.01f) return;

        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(DoHitStop(duration, timeScale));
    }

    private IEnumerator DoHitStop(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = 0.02f * timeScale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        hitStopCoroutine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            instance = null;
        }
    }
}