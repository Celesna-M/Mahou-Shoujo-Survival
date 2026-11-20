using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CombatSystem: ตัวรับ Animation Event ของผู้เล่น (รองรับทั้งสายใกล้ สายไกล และท่า Skill)
/// - ค่าเริ่มต้นสายใกล้: Open Hitbox = ["hit"], Close Hitbox = ["end"]
/// - ค่าเริ่มต้นสายไกล: ยิงกระสุน = "shoot" (หรือใช้ "hit" ร่วมกันก็ได้ ระบบจะแยกตามสายอัตโนมัติ)
/// - ค่าเริ่มต้นท่า Skill: "skill" หรือ "OnSkillImpact" (แทนระบบ Execution เดิมทั้งหมด)
/// </summary>
public class PlayerAnimationEvents : MonoBehaviour
{
    [Header("Player Animation Event Name Lists (เพิ่ม/ลดชื่อ Event ได้ตามใจ)")]
    [Tooltip("ชื่อ Event สำหรับเปิด Hitbox สายใกล้ หรือสั่งยิงสายไกล (ค่าเริ่มต้น: hit)")]
    [SerializeField] private List<string> openHitboxEventNames = new List<string> { "hit" };
    [Tooltip("ชื่อ Event สำหรับปิด Hitbox / จบเฟรมโจมตี (ค่าเริ่มต้น: end)")]
    [SerializeField] private List<string> closeHitboxEventNames = new List<string> { "end" };

    private PlayerCombat playerCombat;
    private Animator animator;

    private void Reset()
    {
        EnsureDefaultEventLists();
    }

    private void Awake()
    {
        EnsureDefaultEventLists();
        FindCombatComponents();
        BindCustomAnimationClipEvents();
    }

    private void EnsureDefaultEventLists()
    {
        if (openHitboxEventNames == null || openHitboxEventNames.Count == 0)
            openHitboxEventNames = new List<string> { "hit" };
        if (closeHitboxEventNames == null || closeHitboxEventNames.Count == 0)
            closeHitboxEventNames = new List<string> { "end" };
    }

    private void FindCombatComponents()
    {
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>() ?? GetComponent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>();

        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    private void BindCustomAnimationClipEvents()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null) return;

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            AnimationEvent[] events = clip.events;
            if (events == null || events.Length == 0) continue;

            bool modified = false;
            for (int i = 0; i < events.Length; i++)
            {
                string fn = events[i].functionName;
                if (string.IsNullOrEmpty(fn)) continue;

                if (fn != "hit" && fn != "end" && fn != "shoot" && fn != "skill" && fn != "OnSkillImpact" &&
                    (IsOpenEventName(fn) || IsCloseEventName(fn)))
                {
                    events[i].stringParameter = fn;
                    events[i].functionName = nameof(OnCustomPlayerAnimEvent);
                    modified = true;
                }
            }

            if (modified)
            {
                clip.events = events;
            }
        }
    }

    public bool IsOpenEventName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || openHitboxEventNames == null) return false;
        for (int i = 0; i < openHitboxEventNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(openHitboxEventNames[i]) &&
                string.Equals(openHitboxEventNames[i].Trim(), eventName.Trim(), System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool IsCloseEventName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || closeHitboxEventNames == null) return false;
        for (int i = 0; i < closeHitboxEventNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(closeHitboxEventNames[i]) &&
                string.Equals(closeHitboxEventNames[i].Trim(), eventName.Trim(), System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public void OnCustomPlayerAnimEvent(string eventName)
    {
        if (playerCombat == null) FindCombatComponents();

        if (IsOpenEventName(eventName))
            playerCombat?.hit(0);
        else if (IsCloseEventName(eventName))
            playerCombat?.end();
    }

    // =========================================================================
    // MELEE, RANGED & SKILL ANIMATION EVENTS
    // =========================================================================

    public void hit(int hitIndex = 0)
    {
        if (!IsOpenEventName("hit")) return;
        if (playerCombat == null) FindCombatComponents();
        playerCombat?.hit(hitIndex);
    }

    public void shoot(int volleyMode = 0)
    {
        if (playerCombat == null) FindCombatComponents();
        playerCombat?.shoot(volleyMode);
    }

    public void end()
    {
        if (!IsCloseEventName("end")) return;
        if (playerCombat == null) FindCombatComponents();
        playerCombat?.end();
    }

    public void skill() => OnSkillImpact();

    public void OnSkillImpact()
    {
        if (playerCombat == null) FindCombatComponents();
        playerCombat?.TriggerSkillImpactFromAnim();
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerAnimationEvents.cs (CombatSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. hit(int hitIndex) & end()
 *    - รับหมุด Animation Event ของท่าโจมตีปกติ (สายใกล้เปิด/ปิด Hitbox, สายไกลยิงกระสุนเวท)
 *
 * 2. shoot(int volleyMode)
 *    - หมุด Animation Event เฉพาะสำหรับสายตีไกล (สั่งยิงกระสุนเวท + VFX ออกไปทันที)
 *
 * 3. skill() / OnSkillImpact()
 *    - หมุด Animation Event สำหรับท่าสกิลประจำตัว (แทนระบบ Execution เดิมทั้งหมด)
 * ============================================================================ */
