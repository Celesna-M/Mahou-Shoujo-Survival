using UnityEngine;

/// <summary>
/// CombatSystem: ตัวส่งต่อข้อมูลการชนของก้อน Hitbox ลูก (หมัด/อาวุธของผู้เล่น หรือ แขน/อาวุธของมอนสเตอร์)
/// กลับไปยัง PlayerCombat หรือ EnemyController โดยอัตโนมัติ
/// </summary>
public class HitboxTriggerForwarder : MonoBehaviour
{
    private PlayerCombat playerCombat;
    private EnemyController enemyController;

    public void Initialize(PlayerCombat combat) => playerCombat = combat;
    public void Initialize(EnemyController enemy) => enemyController = enemy;

    private void OnTriggerEnter(Collider other)
    {
        if (playerCombat != null) playerCombat.ProcessHit(other);
        if (enemyController != null) enemyController.ProcessHit(other);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน HitboxTriggerForwarder.cs (CombatSystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Initialize(PlayerCombat combat) / Initialize(EnemyController enemy)
 *    - ผูกอ้างอิงเจ้าของ Hitbox (ผู้เล่นหรือมอนสเตอร์) เข้ากับก้อน Collider ลูกที่ติดอยู่ตามมือ แขน หรืออาวุธ
 *
 * 2. OnTriggerEnter(Collider other)
 *    - เมื่อก้อน Hitbox ลูกเปิดใช้งานในเฟรมโจมตีและชนเป้าหมาย จะส่งข้อมูล Collider ที่ชนกลับไปให้
 *      PlayerCombat.ProcessHit(other) หรือ EnemyController.ProcessHit(other) คำนวณดาเมจทันที
 * ============================================================================ */
