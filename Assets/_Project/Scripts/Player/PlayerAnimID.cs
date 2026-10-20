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