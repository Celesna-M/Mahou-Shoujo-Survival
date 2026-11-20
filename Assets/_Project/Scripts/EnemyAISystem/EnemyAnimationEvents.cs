using UnityEngine;

// =========================================================================
// ENEMY ANIMATION EVENT FORWARDER (LIST-DRIVEN: DEFAULT hite / end)
// =========================================================================

public class EnemyAnimationEvents : MonoBehaviour
{
    private EnemyController enemyController;

    private void Awake()
    {
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }
    }

    public void Initialize(EnemyController controller)
    {
        enemyController = controller;
    }

    private EnemyController GetEnemy()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>() ?? GetComponent<EnemyController>();
        return enemyController;
    }

    // Default Enemy Events: hite / end
    public void hite(int hitIndex = 0) => GetEnemy()?.hite(hitIndex);
    public void end() => GetEnemy()?.end();

    // Custom Event Handler for any extra event names added to the Inspector List
    public void OnCustomEnemyAnimEvent(string eventName) => GetEnemy()?.OnCustomEnemyAnimEvent(eventName);
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
