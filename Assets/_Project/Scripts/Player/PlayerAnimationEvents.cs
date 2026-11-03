using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerAnimationEvents (List-Driven Edition)
/// - Uses customizable Inspector Lists so you can freely add or remove Animation Event names!
/// - Default Player Events: Open Hitbox = ["hit"], Close Hitbox = ["end"]
/// </summary>
public class PlayerAnimationEvents : MonoBehaviour
{
    [Header("Player Animation Event Name Lists (เพิ่ม/ลดชื่อ Event ได้ตามใจ)")]
    [Tooltip("List of Animation Event names on the Player that open the hitbox & deal damage (Default: hit)")]
    [SerializeField] private List<string> openHitboxEventNames = new List<string> { "hit" };
    [Tooltip("List of Animation Event names on the Player that close the hitbox (Default: end)")]
    [SerializeField] private List<string> closeHitboxEventNames = new List<string> { "end" };

    private PlayerCombat meleeCombat;
    private PlayerExecution playerExecution;
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
        {
            openHitboxEventNames = new List<string> { "hit" };
        }
        if (closeHitboxEventNames == null || closeHitboxEventNames.Count == 0)
        {
            closeHitboxEventNames = new List<string> { "end" };
        }
    }

    private void FindCombatComponents()
    {
        if (meleeCombat == null)
            meleeCombat = GetComponentInParent<PlayerCombat>() ?? GetComponent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>();

        if (playerExecution == null)
            playerExecution = GetComponentInParent<PlayerExecution>() ?? GetComponent<PlayerExecution>() ?? GetComponentInChildren<PlayerExecution>();

        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Automatically binds any custom event name added to openHitboxEventNames or closeHitboxEventNames in the Inspector!
    /// </summary>
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

                if (fn != "hit" && fn != "end" && (IsOpenEventName(fn) || IsCloseEventName(fn)))
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
            {
                return true;
            }
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
            {
                return true;
            }
        }
        return false;
    }

    public void OnCustomPlayerAnimEvent(string eventName)
    {
        if (meleeCombat == null) FindCombatComponents();

        if (IsOpenEventName(eventName))
        {
            meleeCombat?.hit(0);
        }
        else if (IsCloseEventName(eventName))
        {
            meleeCombat?.end();
        }
    }

    // =========================================================================
    // DEFAULT PLAYER EVENTS: hit / end (Checked against the Inspector Lists)
    // =========================================================================

    public void hit(int hitIndex = 0)
    {
        if (!IsOpenEventName("hit")) return;
        if (meleeCombat == null) FindCombatComponents();
        meleeCombat?.hit(hitIndex);
    }

    public void end()
    {
        if (!IsCloseEventName("end")) return;
        if (meleeCombat == null) FindCombatComponents();
        meleeCombat?.end();
    }

    // =========================================================================
    // EXECUTION EVENTS
    // =========================================================================

    public void OnExecuteImpact()
    {
        if (playerExecution == null) FindCombatComponents();
        playerExecution?.TriggerExecutionImpactFromAnim();
    }
}


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerAnimationEvents.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. openHitboxEventNames & closeHitboxEventNames (List<string>)
 *    - รายการชื่อ Animation Event ของฝั่งผู้เล่นที่สามารถกด + เพิ่ม หรือกด - ลบได้ใน Inspector
 *    - ค่าเริ่มต้นของฝั่งผู้เล่นจะมีแค่:
 *      • openHitboxEventNames = ["hit"] (เปิด Hitbox หมัด/อาวุธเพื่อทำดาเมจ)
 *      • closeHitboxEventNames = ["end"] (ปิด Hitbox หมัด/อาวุธ)
 *
 * 2. Awake() & FindCombatComponents()
 *    - ค้นหาสคริปต์ PlayerCombat, PlayerExecution, และ Animator บนตัวละครผู้เล่นอัตโนมัติ
 *
 * 3. BindCustomAnimationClipEvents() & OnCustomPlayerAnimEvent(string eventName)
 *    - สแกนคลิปแอนิเมชันทั้งหมดของตัวละครผู้เล่นตอนเริ่มเกม
 *    - หากคุณเพิ่มชื่อ Event อื่นเข้าไปใน List บน Inspector (นอกเหนือจากคำว่า "hit" และ "end")
 *      ระบบจะผูกชื่อ Event นั้นเข้ากับคำสั่งเปิด/ปิด Hitbox ของ PlayerCombat ให้อัตโนมัติทันที
 *
 * 4. hit(int hitIndex = 0)
 *    - ถูกเรียกเมื่อคลิปแอนิเมชันโจมตีของผู้เล่นวิ่งถึงหมุด Event ชื่อ "hit"
 *    - ตรวจสอบว่าคำว่า "hit" ยังอยู่ใน openHitboxEventNames List หรือไม่ ถ้าอยู่จะสั่ง PlayerCombat.hit(hitIndex)
 *      เพื่อเปิด Hitbox และตรวจจับการฟันโดนมอนสเตอร์
 *
 * 5. end()
 *    - ถูกเรียกเมื่อคลิปแอนิเมชันโจมตีของผู้เล่นวิ่งถึงหมุด Event ชื่อ "end"
 *    - ตรวจสอบว่าคำว่า "end" ยังอยู่ใน closeHitboxEventNames List หรือไม่ ถ้าอยู่จะสั่ง PlayerCombat.end()
 *      เพื่อปิด Hitbox ทันที
 *
 * 6. OnExecuteImpact()
 *    - หมุดเสริมสำหรับท่าสังหารบอส (Execution [F]) เพื่อสั่งปลิดชีพบอสและฟื้นฟูเลือดในจังหวะปะทะ
 * ============================================================================ */