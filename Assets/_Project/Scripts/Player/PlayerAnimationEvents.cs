using UnityEngine;

/// <summary>
/// ตัวรับและส่งต่อ Animation Events จากคลิปแอนิเมชันต่อย/ฟัน และท่า Execution ของผู้เล่น (สาย Brawler)
/// Function Names:
/// - hit(int hitIndex): หมุดที่ 1 และ 3 (ระบุหมายเลข Hitbox เช่น 1, 2, 3 หรือใส่ 0 เพื่อเปิดทั้งหมด)
/// - end(): หมุดที่ 2 และ 4 (ปิดทุก Hitbox)
/// </summary>
public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerCombat meleeCombat;
    private PlayerExecution playerExecution;

    private void Awake()
    {
        FindCombatComponents();
    }

    private void FindCombatComponents()
    {
        if (meleeCombat == null)
            meleeCombat = GetComponentInParent<PlayerCombat>() ?? GetComponent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>();

        if (playerExecution == null)
            playerExecution = GetComponentInParent<PlayerExecution>() ?? GetComponent<PlayerExecution>() ?? GetComponentInChildren<PlayerExecution>();
    }

    // =========================================================================
    // PLAYER MELEE EVENTS (hit & end)
    // =========================================================================

    /// <summary>
    /// หมุดที่ 1 และ 3: เริ่มจังหวะฟัน/ต่อย (ระบุหมายเลข Hitbox เช่น 1, 2, 3 หรือใส่ 0 เพื่อเปิดทั้งหมด)
    /// </summary>
    public void hit(int hitIndex = 0)
    {
        if (meleeCombat == null) FindCombatComponents();
        meleeCombat?.hit(hitIndex);
    }

    /// <summary>
    /// หมุดที่ 2 และ 4: สิ้นสุดจังหวะฟัน/ต่อย (ปิดทุก Hitbox)
    /// </summary>
    public void end()
    {
        if (meleeCombat == null) FindCombatComponents();
        meleeCombat?.end();
    }

    // Aliases สำหรับ Animation Clips ต่างๆ
    public void OnHitboxEnd() => end();
    public void EnableHitbox() => hit(0);
    public void DisableHitbox() => end();
    public void ResetAttack() => meleeCombat?.ResetAttack();

    public void OnHit() => hit(0);
    public void Hit() => hit(0);
    public void DealDamage() => hit(0);
    public void OnAttackHit() => hit(0);
    public void ResetCombo() => ResetAttack();

    // =========================================================================
    // EXECUTION EVENTS (Optional Animation Event Hooks)
    // =========================================================================

    public void OnExecuteImpact()
    {
        if (playerExecution == null) FindCombatComponents();
        playerExecution?.TriggerExecutionImpactFromAnim();
    }

    // Stub ปลอดภัยเผื่อมีคลิปแอนิเมชันเก่าฝัง Event ชื่อนี้ไว้ จะได้ไม่เกิด Error ใน Console
    public void SpawnMagicProjectile() { }
    public void Shoot() { }
}