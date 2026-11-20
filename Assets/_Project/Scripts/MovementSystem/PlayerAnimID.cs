using UnityEngine;

/// <summary>
/// MovementSystem: มาตรฐานพารามิเตอร์พื้นฐานของ Animator โดยใช้ Parameter เดิมที่มีอยู่เป็น Base
/// และเพิ่ม Parameter สำหรับการทำแอนิเมชันสายใกล้/สายไกล + ท่า Skill (ลบ Execute ออกทั้งหมดแล้ว)
///
/// [Base Parameters เดิมที่คงไว้ 100%]
/// - Float   : Speed, x, y
/// - Bool    : InCombat
/// - Trigger : Attack, Hurt, Dodge
///
/// [Standard Parameters ที่เพิ่มสำหรับ Anim ตัวละครใหม่หลังจากนี้]
/// - Bool    : IsMoving, IsGrounded
/// - Int     : ComboStep (0-3), ArchetypeID (0 = สายใกล้เร็ว, 1 = สายใกล้หนัก, 2 = สายไกล)
/// - Trigger : Skill (แทนที่ Execute), Dead
/// </summary>
public static class PlayerAnimID
{
    // === 1. Existing Base Parameters (ใช้เป็น Base เดิม 100%) ===
    public static readonly int Speed = Animator.StringToHash("Speed");
    public static readonly int X = Animator.StringToHash("x");
    public static readonly int Y = Animator.StringToHash("y");
    public static readonly int InCombat = Animator.StringToHash("InCombat");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Hurt = Animator.StringToHash("Hurt");
    public static readonly int Dodge = Animator.StringToHash("Dodge");

    // === 2. Extended Base Parameters (สำหรับ Anim ตัวละครสายใกล้ / สายไกล / Skill หลังจากนี้) ===
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    public static readonly int ComboStep = Animator.StringToHash("ComboStep");
    public static readonly int ArchetypeID = Animator.StringToHash("ArchetypeID");
    public static readonly int Skill = Animator.StringToHash("Skill");
    public static readonly int Dead = Animator.StringToHash("Dead");

    // === 3. Standard State Names ===
    public const string STATE_IDLE = "Fight Idle";
    public const string STATE_ATTACK = "Attack";
    public const string STATE_SKILL = "Skill";
    public const string STATE_DODGE = "Dodge";

    public static bool HasParameter(Animator anim, int paramHash)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == paramHash) return true;
        }
        return false;
    }

    public static void SafeSetFloat(Animator anim, int paramHash, float value)
    {
        if (HasParameter(anim, paramHash)) anim.SetFloat(paramHash, value);
    }

    public static void SafeSetBool(Animator anim, int paramHash, bool value)
    {
        if (HasParameter(anim, paramHash)) anim.SetBool(paramHash, value);
    }

    public static void SafeSetInt(Animator anim, int paramHash, int value)
    {
        if (HasParameter(anim, paramHash)) anim.SetInteger(paramHash, value);
    }

    public static void SafeSetTrigger(Animator anim, int paramHash)
    {
        if (HasParameter(anim, paramHash)) anim.SetTrigger(paramHash);
    }

    public static void SafeResetTrigger(Animator anim, int paramHash)
    {
        if (HasParameter(anim, paramHash)) anim.ResetTrigger(paramHash);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerAnimID.cs (MovementSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. Existing Base Parameters (Speed, x, y, InCombat, Attack, Hurt, Dodge)
 *    - คงพารามิเตอร์เดิมทั้งหมดของโปรเจกต์ไว้ 100% เพื่อให้ Animator เดิมและ PlayerMovement ทำงานได้ทันทีโดยไม่พัง
 *
 * 2. Extended Base Parameters (IsMoving, IsGrounded, ComboStep, ArchetypeID, Skill, Dead)
 *    - เพิ่มพารามิเตอร์มาตรฐานสำหรับแยกแอนิเมชันสายใกล้/สายไกล และแทนที่ Execute ด้วย Skill
 *
 * 3. SafeSetFloat / SafeSetBool / SafeSetInt / SafeSetTrigger
 *    - เช็กว่า Animator มีพารามิเตอร์นั้นจริงก่อนสั่งตั้งค่า ป้องกัน Warning ใน Console
 * ============================================================================ */
