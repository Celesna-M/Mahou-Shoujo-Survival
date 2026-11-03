using UnityEngine;

public static class PlayerAnimID
{
    public static readonly int Speed = Animator.StringToHash("Speed");
    public static readonly int X = Animator.StringToHash("x");
    public static readonly int Y = Animator.StringToHash("y");
    public static readonly int InCombat = Animator.StringToHash("InCombat");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Execute = Animator.StringToHash("Execute");
    public static readonly int Hurt = Animator.StringToHash("Hurt");
    public static readonly int Dodge = Animator.StringToHash("Dodge");

    public const string STATE_IDLE = "Fight Idle";
    public const string STATE_ATTACK = "Attack";
    public const string STATE_EXECUTION = "Standing Melee Run Jump Attack";
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ PlayerAnimID.cs]


1. PlayerAnimID (Static Class)
   - แปลงชื่อ Parameter ทั้งหมดของ Animator (เช่น Speed, IsGrounded, Jump, Dodge, Attack, Hurt, Dead, Execute, InCombat) เป็นค่าตัวเลข Hash ล่วงหน้าด้วย Animator.StringToHash
   - ช่วยลดการใช้ String ซ้ำๆ ในทุกเฟรมและเพิ่มประสิทธิภาพการทำงานของ Animator
===============================================================================
*/
