#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// CoreAndDebugSystem (Editor Tool): ตัวสร้าง Base Animator Parameters อัตโนมัติสำหรับแอนิเมชันตัวละครใหม่ทุกตัว
/// วิธีใช้งานใน Unity Editor:
/// 1. เมนูด้านบน Tools -> StellaClash -> Create New Character Animator (With Base Parameters)
///    เพื่อสร้างไฟล์ AnimatorController ตัวใหม่พร้อม Parameter พื้นฐานและ State เริ่มต้นครบใน 1 คลิก
/// 2. คลิกขวาที่ไฟล์ AnimatorController เดิม -> Assets -> StellaClash -> Apply Base Parameters To Selected Animator
///    เพื่อเติม Parameter พื้นฐานที่ขาด และลบ Parameter "Execute" เดิมออกแล้วแทนด้วย "Skill" อัตโนมัติ
/// </summary>
public static class AnimatorBaseParameterGenerator
{
    private struct ParamSpec
    {
        public string name;
        public AnimatorControllerParameterType type;

        public ParamSpec(string name, AnimatorControllerParameterType type)
        {
            this.name = name;
            this.type = type;
        }
    }

    // รายการ Base Parameters มาตรฐานทั้งหมด (ใช้ Base เดิม + ส่วนขยายสำหรับสายใกล้/สายไกล/Skill)
    private static readonly ParamSpec[] BaseParameters = new ParamSpec[]
    {
        // Base เดิมที่มีอยู่
        new ParamSpec("Speed", AnimatorControllerParameterType.Float),
        new ParamSpec("x", AnimatorControllerParameterType.Float),
        new ParamSpec("y", AnimatorControllerParameterType.Float),
        new ParamSpec("InCombat", AnimatorControllerParameterType.Bool),
        new ParamSpec("Attack", AnimatorControllerParameterType.Trigger),
        new ParamSpec("Hurt", AnimatorControllerParameterType.Trigger),
        new ParamSpec("Dodge", AnimatorControllerParameterType.Trigger),

        // ส่วนขยายมาตรฐานสำหรับ Anim ตัวละครหลังจากนี้ (แทนที่ Execute ด้วย Skill)
        new ParamSpec("IsMoving", AnimatorControllerParameterType.Bool),
        new ParamSpec("IsGrounded", AnimatorControllerParameterType.Bool),
        new ParamSpec("ComboStep", AnimatorControllerParameterType.Int),
        new ParamSpec("ArchetypeID", AnimatorControllerParameterType.Int),
        new ParamSpec("Skill", AnimatorControllerParameterType.Trigger),
        new ParamSpec("Dead", AnimatorControllerParameterType.Trigger)
    };

    [MenuItem("Tools/StellaClash/Create New Character Animator (With Base Parameters)", false, 20)]
    public static void CreateNewBaseCharacterAnimator()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Character Animator Controller",
            "AC_NewCharacter",
            "controller",
            "เลือกโฟลเดอร์สำหรับบันทึก Animator Controller ตัวใหม่ที่มี Base Parameters ครบชุด"
        );

        if (string.IsNullOrEmpty(path)) return;

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        ApplyBaseParametersToController(controller);
        CreateDefaultStarterStates(controller);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;

        Debug.Log($"<color=#40FF80>[StellaClash] สร้าง Animator Controller '{controller.name}' พร้อม Base Parameters ครบ 13 ตัวเรียบร้อยแล้ว!</color>");
    }

    [MenuItem("Assets/StellaClash/Apply Base Parameters To Selected Animator (Remove Execute & Add Skill)", false, 21)]
    public static void ApplyToSelectedAnimators()
    {
        Object[] selected = Selection.objects;
        int updatedCount = 0;

        foreach (Object obj in selected)
        {
            if (obj is AnimatorController controller)
            {
                ApplyBaseParametersToController(controller);
                EditorUtility.SetDirty(controller);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#40FF80>[StellaClash] อัปเดต Base Parameters (ลบ Execute + เพิ่ม Skill) ให้ Animator จำนวน {updatedCount} ไฟล์เรียบร้อยแล้ว!</color>");
        }
        else
        {
            EditorUtility.DisplayDialog("StellaClash Animator Tool", "กรุณาคลิกเลือกไฟล์ Animator Controller (.controller) ในหน้าต่าง Project ก่อนใช้งานคำสั่งนี้ครับ", "ตกลง");
        }
    }

    [MenuItem("Assets/StellaClash/Apply Base Parameters To Selected Animator (Remove Execute & Add Skill)", true)]
    private static bool ValidateApplyToSelectedAnimators()
    {
        return Selection.activeObject is AnimatorController;
    }

    public static void ApplyBaseParametersToController(AnimatorController controller)
    {
        if (controller == null) return;

        // 1. ลบ Parameter "Execute" เดิมออกหากมีค้างอยู่
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            if (controller.parameters[i].name == "Execute")
            {
                controller.RemoveParameter(i);
            }
        }

        // 2. ตรวจสอบและเพิ่ม Base Parameters ที่ยังไม่มี
        HashSet<string> existingNames = new HashSet<string>();
        foreach (var p in controller.parameters)
        {
            existingNames.Add(p.name);
        }

        foreach (var spec in BaseParameters)
        {
            if (!existingNames.Contains(spec.name))
            {
                controller.AddParameter(spec.name, spec.type);
            }
        }
    }

    private static void CreateDefaultStarterStates(AnimatorController controller)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0) return;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState(PlayerAnimID.STATE_IDLE, new Vector3(260, 120, 0));
        sm.defaultState = idleState;

        AnimatorState attackState = sm.AddState(PlayerAnimID.STATE_ATTACK, new Vector3(540, 40, 0));
        AnimatorState skillState = sm.AddState(PlayerAnimID.STATE_SKILL, new Vector3(540, 120, 0));
        AnimatorState dodgeState = sm.AddState(PlayerAnimID.STATE_DODGE, new Vector3(540, 200, 0));
        AnimatorState hurtState = sm.AddState("Hurt", new Vector3(540, 280, 0));
        AnimatorState deadState = sm.AddState("Dead", new Vector3(540, 360, 0));

        // Any State -> Dodge / Skill / Hurt / Dead
        var toDodge = sm.AddAnyStateTransition(dodgeState);
        toDodge.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
        toDodge.hasExitTime = false;
        toDodge.duration = 0.05f;

        var toSkill = sm.AddAnyStateTransition(skillState);
        toSkill.AddCondition(AnimatorConditionMode.If, 0, "Skill");
        toSkill.hasExitTime = false;
        toSkill.duration = 0.06f;

        var toHurt = sm.AddAnyStateTransition(hurtState);
        toHurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
        toHurt.hasExitTime = false;
        toHurt.duration = 0.05f;

        var toDead = sm.AddAnyStateTransition(deadState);
        toDead.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        toDead.hasExitTime = false;
        toDead.duration = 0.08f;

        // Idle -> Attack
        var idleToAttack = idleState.AddTransition(attackState);
        idleToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        idleToAttack.hasExitTime = false;
        idleToAttack.duration = 0.06f;

        // Return to Idle after Attack / Skill / Dodge / Hurt finish
        foreach (var st in new AnimatorState[] { attackState, skillState, dodgeState, hurtState })
        {
            var ret = st.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 0.90f;
            ret.duration = 0.10f;
        }
    }
}
#endif

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดใน AnimatorBaseParameterGenerator.cs (CoreAndDebugSystem/Editor)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. CreateNewBaseCharacterAnimator() (เมนู Tools -> StellaClash -> Create New Character Animator)
 *    - สร้างไฟล์ Animator Controller ตัวใหม่พร้อม Parameter พื้นฐานครบ 13 ตัว
 *      (Speed, x, y, InCombat, Attack, Hurt, Dodge, IsMoving, IsGrounded, ComboStep, ArchetypeID, Skill, Dead)
 *      และสร้าง State + Transition เริ่มต้น (Fight Idle, Attack, Skill, Dodge, Hurt, Dead) ให้อัตโนมัติ
 *
 * 2. ApplyToSelectedAnimators() (คลิกขวาที่ Animator เดิม -> Assets -> StellaClash -> Apply Base Parameters)
 *    - ลบ Parameter "Execute" เดิมทิ้งอัตโนมัติ และเติม Parameter พื้นฐานที่ยังขาดให้ครบใน 1 คลิก
 * ============================================================================ */
