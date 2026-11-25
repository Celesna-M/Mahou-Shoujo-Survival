using UnityEngine;

// =========================================================================
// ENEMY ANIMATION EVENT FORWARDER (LIST-DRIVEN: DEFAULT hite / end)
// =========================================================================

public class EnemyAnimationEvents : MonoBehaviour
{
    private EnemyController enemyController;
    private AnimVFXController animVfxController;

    private void Awake()
    {
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }
        ResolveVfxController();
    }

    public void Initialize(EnemyController controller)
    {
        enemyController = controller;
        ResolveVfxController();
    }

    private EnemyController GetEnemy()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>() ?? GetComponent<EnemyController>();
        return enemyController;
    }

    private AnimVFXController ResolveVfxController()
    {
        if (animVfxController == null)
        {
            animVfxController = (enemyController != null ? enemyController.GetComponent<AnimVFXController>() : null)
                             ?? GetComponentInParent<AnimVFXController>()
                             ?? GetComponent<AnimVFXController>();
        }
        return animVfxController;
    }

    // Default Enemy Events: hite / end
    public void hite(int hitIndex = 0) => GetEnemy()?.hite(hitIndex);
    public void end() => GetEnemy()?.end();

    // Custom Event Handler for any extra event names added to the Inspector List
    public void OnCustomEnemyAnimEvent(string eventName) => GetEnemy()?.OnCustomEnemyAnimEvent(eventName);

    // =========================================================================
    // POOLED VFX ANIMATION EVENTS (Key / Direct Prefab / Full AnimationEvent)
    // =========================================================================

    /// <summary>Spawns a pooled VFX mapped to 'key' in the Enemy's AnimVFXController / AnimVFXLibrary_SO.</summary>
    public void PlayVFX(string key) => ResolveVfxController()?.PlayByKey(key);

    /// <summary>Spawns a direct VFX Prefab dragged into the Animation Event's Object parameter.</summary>
    public void PlayVFXPrefab(UnityEngine.Object prefabObj)
    {
        if (prefabObj is GameObject prefabGo)
            ResolveVfxController()?.PlayDirectPrefab(prefabGo);
    }

    /// <summary>Full multi-parameter VFX event (String=Key/CustomAnchor, Object=Prefab, Int=VFXSpawnAnchor, Float=Scale).</summary>
    public void PlayVFXEvent(AnimationEvent evt) => ResolveVfxController()?.PlayFromAnimationEvent(evt);

    /// <summary>Stops an active looping/attached VFX by key and returns it to the pool.</summary>
    public void StopVFX(string key) => ResolveVfxController()?.StopByKey(key);

    /// <summary>Stops all active interruptible VFX on this enemy and returns them to the pool.</summary>
    public void StopAllVFX() => ResolveVfxController()?.CancelInterruptibleVFX();

    // =========================================================================
    // POOLED SFX ANIMATION EVENTS
    // =========================================================================

    /// <summary>Plays a pooled SFX cue by key from DefaultAudioLibrary at this enemy's position.</summary>
    public void PlaySFX(string key) => AudioManager.PlaySFX(key, transform.position);

    /// <summary>Plays a randomized pooled footstep SFX at this enemy's position.</summary>
    public void PlayFootstep() => AudioManager.PlaySFX(AudioKeys.Combat_Footstep, transform.position);
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน EnemyAnimationEvents.cs (EnemyAISystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake() & Initialize(EnemyController controller)
 *    - ติดตั้งตัวเองลงบน GameObject ลูกที่มี Animator ของมอนสเตอร์โดยอัตโนมัติ
 *
 * 2. hite(int hitIndex = 0) & end()
 *    - รับคำสั่งจากหมุด Animation Event เริ่มต้นของมอนสเตอร์ ("hite" สำหรับเปิด Hitbox และ "end" สำหรับปิด Hitbox)
 *      แล้วส่งต่อไปให้ EnemyController ทันที
 *
 * 3. OnCustomEnemyAnimEvent(string eventName)
 *    - รับคำสั่งจากชื่อ Event เพิ่มเติมที่คุณกด + เพิ่มเข้าไปใน openHitboxEventNames หรือ closeHitboxEventNames List บน Inspector
 * ============================================================================ */
