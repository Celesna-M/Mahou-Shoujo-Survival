using System;
using UnityEngine;

/// <summary>
/// MovementSystem: 5 สถานะหลักของตัวละครผู้เล่นตาม GDD Section 3.3 (Character State Machine - FSM)
/// 1. Locomotion : ยืนนิ่ง เดิน วิ่ง
/// 2. Attack     : กำลังออกท่าคอมโบ (ล็อกการเดินชั่วขณะ แต่หมุนตัวตามทิศเมาส์/กล้องได้เล็กน้อย)
/// 3. Skill      : กำลังใช้ท่าพิเศษประจำตัว
/// 4. Dash       : กำลังพุ่งหลบ (ตัดจังหวะจาก Attack State ได้ทันที - Animation Cancel)
/// 5. HitOrDead  : ถูกโจมตีชะงัก (Stagger) หรือพลังชีวิตเหลือ 0
/// </summary>
public enum PlayerStateType
{
    Locomotion = 0,
    Attack = 1,
    Skill = 2,
    Dash = 3,
    HitOrDead = 4
}

public interface IPlayerState
{
    PlayerStateType StateType { get; }
    void OnEnter(PlayerStateMachine fsm);
    void OnTick(PlayerStateMachine fsm, float deltaTime);
    void OnExit(PlayerStateMachine fsm);
}

[DefaultExecutionOrder(-120)]
public class PlayerStateMachine : MonoBehaviour
{
    [Header("1. FSM Live State Monitor (ดูสถานะปัจจุบันแบบเรียลไทม์)")]
    [SerializeField] private PlayerStateType currentStateType = PlayerStateType.Locomotion;
    [SerializeField] private bool isDeadState = false;

    [Header("2. Attack State Aim Assist & Hit Stagger Settings")]
    [Tooltip("ความเร็วในการหันตัวเล็กน้อยตามทิศเมาส์/กล้องระหว่างออกท่าคอมโบใน Attack State")]
    [SerializeField] private float attackStateTurnSpeed = 7.5f;
    [Tooltip("ระยะเวลาชะงักเมื่อโดนโจมตีใน Hit State (วินาที)")]
    [SerializeField] private float hitStaggerDuration = 0.28f;

    private IPlayerState currentState;
    private readonly LocomotionState locomotionState = new LocomotionState();
    private readonly AttackState attackState = new AttackState();
    private readonly SkillState skillState = new SkillState();
    private readonly DashState dashState = new DashState();
    private readonly HitOrDeadState hitOrDeadState = new HitOrDeadState();

    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private PlayerHealth playerHealth;
    private Camera mainCam;

    private float hitStaggerTimer = 0f;

    public PlayerStateType CurrentStateType => currentStateType;
    public bool IsDead => isDeadState;
    public bool IsInLocomotion => currentStateType == PlayerStateType.Locomotion;
    public bool IsInAttack => currentStateType == PlayerStateType.Attack;
    public bool IsInSkill => currentStateType == PlayerStateType.Skill;
    public bool IsInDash => currentStateType == PlayerStateType.Dash;
    public bool IsInHitOrDead => currentStateType == PlayerStateType.HitOrDead;

    public float AttackStateTurnSpeed => attackStateTurnSpeed;
    public PlayerMovement Movement => playerMovement;
    public PlayerCombat Combat => playerCombat;
    public PlayerHealth Health => playerHealth;
    public Camera MainCamera => mainCam != null
        ? mainCam
        : (mainCam = (PersistentPlayerCameraRig.Instance != null && PersistentPlayerCameraRig.Instance.MainCamera != null)
            ? PersistentPlayerCameraRig.Instance.MainCamera
            : Camera.main);

    /// <summary>
    /// Updates the cached MainCamera reference when the persistent camera rig binds to a newly spawned player.
    /// </summary>
    public void SetMainCamera(Camera cam)
    {
        if (cam != null) mainCam = cam;
    }

    public event Action<PlayerStateType, PlayerStateType> OnStateChanged;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
        playerHealth = GetComponent<PlayerHealth>();
        mainCam = (PersistentPlayerCameraRig.Instance != null && PersistentPlayerCameraRig.Instance.MainCamera != null)
            ? PersistentPlayerCameraRig.Instance.MainCamera
            : Camera.main;

        currentState = locomotionState;
        currentStateType = PlayerStateType.Locomotion;
        currentState.OnEnter(this);
    }

    private void Update()
    {
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;
        currentState?.OnTick(this, Time.deltaTime);
    }

    // =========================================================================
    // STRICT STATE TRANSITION GATEKEEPER (ป้องกันบั๊กกดปุ่มซ้อน 100%)
    // =========================================================================

    public bool CanTransitionTo(PlayerStateType targetState, bool forceDead = false)
    {
        // 1. หากตายแล้ว ห้ามสลับไปสถานะอื่นเด็ดขาดจนกว่าจะโหลดฉากใหม่
        if (isDeadState) return false;
        if (forceDead) return true;

        switch (targetState)
        {
            case PlayerStateType.Locomotion:
                return currentStateType != PlayerStateType.Locomotion;

            case PlayerStateType.Attack:
                // โจมตีได้เฉพาะจาก Locomotion หรือต่อเนื่องใน Attack (ห้ามกดตีระหว่าง Dash / Skill / Hit)
                return currentStateType == PlayerStateType.Locomotion || currentStateType == PlayerStateType.Attack;

            case PlayerStateType.Skill:
                // กดสกิลได้จาก Locomotion หรือตัดจังหวะจาก Attack (ห้ามกดระหว่าง Dash / Hit / Skill ซ้อน)
                return currentStateType == PlayerStateType.Locomotion || currentStateType == PlayerStateType.Attack;

            case PlayerStateType.Dash:
                // พุ่งหลบตัดจังหวะจาก Locomotion, Attack หรือ Skill ได้ทันที (Animation Cancel!) แต่ห้ามตัดตอนติด Hit/Dead
                return currentStateType == PlayerStateType.Locomotion
                    || currentStateType == PlayerStateType.Attack
                    || currentStateType == PlayerStateType.Skill;

            case PlayerStateType.HitOrDead:
                // พุ่งหลบ (Dash I-Frame) และ Skill (Super Armor) จะไม่ถูกตัดเข้า Hit Stagger
                return currentStateType != PlayerStateType.Dash && currentStateType != PlayerStateType.Skill;

            default:
                return false;
        }
    }

    public bool TryTransitionTo(PlayerStateType targetState, bool forceDead = false)
    {
        if (!CanTransitionTo(targetState, forceDead)) return false;

        PlayerStateType previous = currentStateType;
        if (forceDead) isDeadState = true;

        // หากสลับเข้า Dash หรือ HitOrDead จาก Attack/Skill ให้ยกเลิก Hitbox ค้างทันที
        if ((targetState == PlayerStateType.Dash || targetState == PlayerStateType.HitOrDead) &&
            (previous == PlayerStateType.Attack || previous == PlayerStateType.Skill))
        {
            playerCombat?.CancelCombat();
        }

        currentState?.OnExit(this);
        currentStateType = targetState;
        currentState = ResolveStateObject(targetState);

        if (targetState == PlayerStateType.HitOrDead && !isDeadState)
        {
            hitStaggerTimer = hitStaggerDuration;
        }

        currentState.OnEnter(this);
        OnStateChanged?.Invoke(previous, currentStateType);
        return true;
    }

    public void TriggerHitStagger()
    {
        TryTransitionTo(PlayerStateType.HitOrDead, false);
    }

    public void TriggerDeathState()
    {
        TryTransitionTo(PlayerStateType.HitOrDead, true);
    }

    public void ReturnToLocomotionIfState(PlayerStateType expectedCurrent)
    {
        if (!isDeadState && currentStateType == expectedCurrent)
        {
            TryTransitionTo(PlayerStateType.Locomotion);
        }
    }

    public void TickHitStaggerTimer(float dt)
    {
        if (isDeadState) return;
        hitStaggerTimer -= dt;
        if (hitStaggerTimer <= 0f)
        {
            TryTransitionTo(PlayerStateType.Locomotion);
        }
    }

    private IPlayerState ResolveStateObject(PlayerStateType type)
    {
        switch (type)
        {
            case PlayerStateType.Attack: return attackState;
            case PlayerStateType.Skill: return skillState;
            case PlayerStateType.Dash: return dashState;
            case PlayerStateType.HitOrDead: return hitOrDeadState;
            default: return locomotionState;
        }
    }

    // =========================================================================
    // 5 CONCRETE FSM STATES (ZERO-GC PRE-ALLOCATED INSTANCES)
    // =========================================================================

    private sealed class LocomotionState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Locomotion;

        public void OnEnter(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null) fsm.Movement.CanMove = true;
        }

        public void OnTick(PlayerStateMachine fsm, float deltaTime) { }

        public void OnExit(PlayerStateMachine fsm) { }
    }

    private sealed class AttackState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Attack;

        public void OnEnter(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null) fsm.Movement.CanMove = false;
        }

        public void OnTick(PlayerStateMachine fsm, float deltaTime)
        {
            // GDD Section 3.3: ล็อกการเดินชั่วขณะ แต่หมุนตัวตามทิศเมาส์/กล้องได้เล็กน้อยระหว่างออกท่าคอมโบ
            if (fsm.MainCamera != null)
            {
                Vector3 camForward = fsm.MainCamera.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(camForward.normalized);
                    fsm.transform.rotation = Quaternion.Slerp(fsm.transform.rotation, desiredRot, fsm.AttackStateTurnSpeed * deltaTime);
                }
            }
        }

        public void OnExit(PlayerStateMachine fsm) { }
    }

    private sealed class SkillState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Skill;

        public void OnEnter(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null) fsm.Movement.CanMove = false;
        }

        public void OnTick(PlayerStateMachine fsm, float deltaTime) { }

        public void OnExit(PlayerStateMachine fsm) { }
    }

    private sealed class DashState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Dash;

        public void OnEnter(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null) fsm.Movement.CanMove = false;
        }

        public void OnTick(PlayerStateMachine fsm, float deltaTime) { }

        public void OnExit(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null && !fsm.IsDead) fsm.Movement.CanMove = true;
        }
    }

    private sealed class HitOrDeadState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.HitOrDead;

        public void OnEnter(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null) fsm.Movement.CanMove = false;
        }

        public void OnTick(PlayerStateMachine fsm, float deltaTime)
        {
            fsm.TickHitStaggerTimer(deltaTime);
        }

        public void OnExit(PlayerStateMachine fsm)
        {
            if (fsm.Movement != null && !fsm.IsDead) fsm.Movement.CanMove = true;
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerStateMachine.cs (MovementSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. PlayerStateType (5 สถานะหลักตาม GDD Section 3.3)
 *    - Locomotion (ยืน/เดิน/วิ่ง), Attack (ออกท่าคอมโบ + หันตัวตามกล้องได้เล็กน้อย),
 *      Skill (ออกท่าสกิลประจำตัว), Dash (พุ่งหลบ ตัดจังหวะจาก Attack ได้ทันที),
 *      HitOrDead (โดนโจมตีชะงัก Stagger หรือพลังชีวิตเหลือ 0)
 *
 * 2. CanTransitionTo(PlayerStateType targetState) & TryTransitionTo(...)
 *    - ตัวคุมกฎการเปลี่ยนสถานะกลาง (Single Source of Truth) ป้องกันบั๊กกดปุ่มซ้อน เช่น กดตีระหว่างกลิ้งหลบ
 *      หรือกดสกิลซ้อนตอนตาย และสั่งยกเลิก Hitbox ค้างทันทีเมื่อกด Dash ตัดท่าโจมตี (Animation Cancel)
 *
 * 3. Pre-allocated 5 Concrete States (Zero-GC)
 *    - จองอินสแตนซ์ของทั้ง 5 สถานะไว้ล่วงหน้า ไม่สร้าง Garbage Collection ระหว่างเปลี่ยนสถานะในฉากต่อสู้
 * ============================================================================ */
