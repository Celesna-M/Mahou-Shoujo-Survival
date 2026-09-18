using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != cameraInstanceValid())
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private bool cameraInstanceValid() => this;

    /// <summary>
    /// สั่งหยุดเวลาเสี้ยววินาที โดยใช้เวลาจริง (Realtime) เพื่อไม่ให้ Coroutine หยุดทำงานตาม TimeScale
    /// </summary>
    /// <param name="duration">ระยะเวลาหยุด (เช่น 0.05 วินาที)</param>
    /// <param name="timeScale">ความเร็วเวลาขณะหยุด (0.0f = หยุดสนิท, 0.05f = สโลว์โมชันสุดขีด)</param>
    // ใน HitStopManager.cs
    public void TriggerHitStop(float duration = 0.05f, float timeScale = 0.1f)
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }

        hitStopCoroutine = StartCoroutine(DoHitStop(duration, timeScale));
    }

    private IEnumerator DoHitStop(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        hitStopCoroutine = null;
    }

    private void OnDisable()
    {
        // ป้องกันกรณีเปลี่ยน Scene หรือหยุดเล่นแล้วเวลายังค้างอยู่ที่ 0
        Time.timeScale = 1.0f;
    }
}