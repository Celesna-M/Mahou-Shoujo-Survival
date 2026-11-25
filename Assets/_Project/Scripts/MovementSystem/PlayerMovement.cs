using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlayerMovement (Anti-Fall-Through-Map Edition)
/// - Removed OnControllerColliderHit downward push that caused the player to fall through the floor when hit by enemies.
/// - Added Ground Floor Safety Clamp (lockMinGroundHeight) so the player can NEVER be pushed under the map by enemy attacks!
/// - Respects the exact CharacterController collider settings configured in the Inspector.
/// </summary>
[DefaultExecutionOrder(2000)]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
        [Header("3-Character Visual Model Switcher (ลากโมเดล 3 ตัวละครมาใส่ได้ ถ้าเว้นว่างจะใช้โมเดลหลักตัวเดียว)")]
    [Tooltip("Element 0 = Hikari (ประชิด), Element 1 = Akane (ทำลายเกราะ), Element 2 = Shiro (เวทระยะไกล)")]
    [SerializeField] private GameObject[] characterVisualModels;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Header("1. Anti-Fall-Through-Map Protection (กันตกแมพเวลามอนตี)")]
    [Tooltip("Prevents the player's Y position from ever being pushed below the starting floor height when enemies attack")]
    [SerializeField] private bool preventFallingThroughMap = true;
    [SerializeField] private float floorHeightTolerance = 0.05f;

    [Header("2. Camera Anti-Clipping (Prevent Camera Going Through Scene/Walls)")]
    [SerializeField] private bool enableCameraAntiClip = true;
    [SerializeField] private float cameraPivotHeight = 1.45f;
    [SerializeField] private float cameraCollisionRadius = 0.28f;
    [SerializeField] private float minCameraDistance = 0.55f;
    [SerializeField] private LayerMask cameraCollisionLayers;

    [Header("Dodge & I-Frame Settings")]
    [SerializeField] private float dodgeSpeed = 14f;
    [SerializeField] private float dodgeDuration = 0.35f;
    [SerializeField] private float iFrameDuration = 0.2f;
    [SerializeField] private float dodgeStaminaCost = 20f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    [Header("Gravity")]
    [SerializeField] private float downwardForce = -3.5f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private PlayerStamina stamina;
    private PlayerLockOn playerLockOn;
    private PlayerCombat playerCombat;
    private PlayerHealth playerHealth;
    private PlayerStateMachine stateMachine;
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;
    private bool isDodging;
    private float dodgeTimer;
    private float cooldownTimer;
    private Vector3 dodgeDirection;
    private float verticalVelocity;
    private float currentVelocity;
    private bool externalInvincible;
    private float initialGroundY;

    /// <summary>
    /// Fired when any player character initializes in a stage so PersistentPlayerCameraRig can auto-bind to it.
    /// </summary>
    public static event Action<Transform> OnPlayerSpawned;

    public bool IsInvincible => externalInvincible || (isDodging && (dodgeDuration - dodgeTimer) <= iFrameDuration);
    public bool IsDodging => isDodging;
    public bool CanMove { get; set; } = true;
    public Vector2 MoveInput => moveInput;
    public bool IsMoving => CanMove && !isDodging && moveInput.sqrMagnitude >= 0.01f;

    public bool IsGrounded => true;
    public Animator Animator => animator;
    public InputSystem_Actions InputActions => inputActions;

    /// <summary>
    /// Updates the camera transform reference used for movement direction and anti-clipping.
    /// Called automatically by PersistentPlayerCameraRig when binding to a newly spawned player.
    /// </summary>
    public void SetCameraTransform(Transform camTransform)
    {
        if (camTransform != null)
        {
            cameraTransform = camTransform;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();
        playerLockOn = GetComponent<PlayerLockOn>();
        playerCombat = GetComponent<PlayerCombat>();
        playerHealth = GetComponent<PlayerHealth>();
        stateMachine = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();
        inputActions = new InputSystem_Actions();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (PersistentPlayerCameraRig.Instance != null && PersistentPlayerCameraRig.Instance.MainCamera != null)
            cameraTransform = PersistentPlayerCameraRig.Instance.MainCamera.transform;
        else if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (GameProgressionManager.Instance != null)
        {
            if (GameProgressionManager.Instance.HasSelectedCharacter)
            {
                moveSpeed = GameProgressionManager.Instance.SelectedCharacterMoveSpeed;
            }
            if (GameProgressionManager.Instance.BonusDodgeCostReduction > 0f)
            {
                dodgeStaminaCost = Mathf.Max(5f, dodgeStaminaCost - GameProgressionManager.Instance.BonusDodgeCostReduction);
            }
        }

        if (cameraCollisionLayers.value == 0)
        {
            cameraCollisionLayers = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI");
        }

        if (animator != null && animator.applyRootMotion)
            animator.applyRootMotion = false;

        initialGroundY = transform.position.y;
        ActivateSelectedCharacterVisualModel();
        IgnoreSelfCollisions();
    }

    
    private void ActivateSelectedCharacterVisualModel()
    {
        if (characterVisualModels == null || characterVisualModels.Length == 0) return;

        int selectedIdx = GameProgressionManager.Instance != null
            ? Mathf.Clamp(GameProgressionManager.Instance.SelectedCharacterIndex, 0, characterVisualModels.Length - 1)
            : 0;

        bool anyAssigned = false;
        for (int i = 0; i < characterVisualModels.Length; i++)
        {
            if (characterVisualModels[i] != null)
            {
                anyAssigned = true;
                break;
            }
        }
        if (!anyAssigned) return;

        for (int i = 0; i < characterVisualModels.Length; i++)
        {
            if (characterVisualModels[i] != null)
            {
                bool active = (i == selectedIdx);
                characterVisualModels[i].SetActive(active);
                if (active)
                {
                    Animator modelAnim = characterVisualModels[i].GetComponentInChildren<Animator>(true);
                    if (modelAnim != null)
                    {
                        animator = modelAnim;
                    }
                }
            }
        }
    }

    private void IgnoreSelfCollisions()
    {
        if (controller == null) return;

        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in myColliders)
        {
            if (col != null && col != controller && !col.isTrigger)
            {
                Physics.IgnoreCollision(controller, col, true);
            }
        }
    }

    private void Start()
    {
        OnPlayerSpawned?.Invoke(transform);
    }

    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
            inputActions.Player.Dodge.performed += OnDodgePerformed;
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Dodge.performed -= OnDodgePerformed;
            inputActions.Player.Disable();
        }
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen)
            return;

        if ((VNManager.Instance != null && VNManager.Instance.IsPlayingVN) || (playerHealth != null && playerHealth.IsDead))
        {
            moveInput = Vector2.zero;
            ClampAboveGroundFloor();
            UpdateAnimatorParameters(Vector3.zero);
            return;
        }

        if (playerCombat != null && playerCombat.IsUsingSkill)
        {
            moveInput = Vector2.zero;
            ClampAboveGroundFloor();
            UpdateAnimatorParameters(Vector3.zero);
            return;
        }

        if (inputActions != null)
            moveInput = inputActions.Player.Move.ReadValue<Vector2>();

#if ENABLE_INPUT_SYSTEM
        if (moveInput.sqrMagnitude < 0.01f && Keyboard.current != null)
        {
            float h = 0f, v = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            Vector2 raw = new Vector2(h, v);
            if (raw.sqrMagnitude > 0.01f) moveInput = raw.normalized;
        }

        if (Keyboard.current != null && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            PerformDodge();
        }
        else if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            PerformDodge();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (moveInput.sqrMagnitude < 0.01f)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 raw = new Vector2(h, v);
            if (raw.sqrMagnitude > 0.01f) moveInput = raw.normalized;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space))
        {
            PerformDodge();
        }
#endif

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        Vector3 horizontalVelocity = Vector3.zero;

        if (isDodging)
            horizontalVelocity = ComputeDodgeVelocity();
        else if (CanMove)
            horizontalVelocity = ComputeMovementVelocity();

        ComputeVerticalVelocity();

        Vector3 finalVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);
        if (controller != null && controller.enabled)
            controller.Move(finalVelocity * Time.deltaTime);

        ClampAboveGroundFloor();
        UpdateAnimatorParameters(horizontalVelocity);
    }

    private void LateUpdate()
    {
        ClampAboveGroundFloor();

        if (enableCameraAntiClip)
        {
            ResolveCameraCollision();
        }
    }

    /// <summary>
    /// Guarantees the player can NEVER fall through the map floor when hit or pushed by enemies!
    /// </summary>
    private void ClampAboveGroundFloor()
    {
        if (!preventFallingThroughMap) return;

        if (transform.position.y < initialGroundY - floorHeightTolerance)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;

            Vector3 pos = transform.position;
            pos.y = initialGroundY;
            transform.position = pos;

            if (wasEnabled) controller.enabled = true;
        }
    }

    private void ResolveCameraCollision()
    {
        if (cameraTransform == null) return;

        Vector3 pivotPos = transform.position + Vector3.up * cameraPivotHeight;
        Vector3 camPos = cameraTransform.position;
        Vector3 dirToCam = camPos - pivotPos;
        float desiredDistance = dirToCam.magnitude;

        if (desiredDistance < 0.05f) return;
        dirToCam /= desiredDistance;

        if (Physics.SphereCast(
                pivotPos,
                cameraCollisionRadius,
                dirToCam,
                out RaycastHit hit,
                desiredDistance,
                cameraCollisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root != transform.root &&
                hit.collider.GetComponentInParent<EnemyController>() == null)
            {
                float safeDistance = Mathf.Max(minCameraDistance, hit.distance - cameraCollisionRadius * 0.5f);
                cameraTransform.position = pivotPos + dirToCam * safeDistance;
            }
        }
    }

    private void ComputeVerticalVelocity()
    {
        if (controller != null && controller.isGrounded)
            verticalVelocity = -1.5f;
        else
            verticalVelocity = downwardForce;
    }

    private void UpdateAnimatorParameters(Vector3 currentWorldVelocity)
    {
        if (animator == null) return;

        float inputMag = moveInput.magnitude;
        float targetSpeed = (CanMove && !isDodging && inputMag >= 0.01f) ? inputMag : 0f;

        float targetX = 0f;
        float targetY = 0f;

        if (targetSpeed > 0f && currentWorldVelocity.sqrMagnitude >= 0.001f)
        {
            Vector3 localDir = transform.InverseTransformDirection(currentWorldVelocity.normalized) * targetSpeed;
            targetX = Mathf.Abs(localDir.x) >= 0.01f ? localDir.x : 0f;
            targetY = Mathf.Abs(localDir.z) >= 0.01f ? localDir.z : 0f;
        }

        PlayerAnimID.SafeSetBool(animator, PlayerAnimID.IsMoving, IsMoving);
            animator.SetFloat(PlayerAnimID.Speed, targetSpeed);
        animator.SetFloat(PlayerAnimID.X, targetX);
        animator.SetFloat(PlayerAnimID.Y, targetY);
    }

    private Vector3 ComputeMovementVelocity()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (inputDir.sqrMagnitude < 0.01f) return Vector3.zero;

        Vector3 moveDir;

        if (playerLockOn != null && playerLockOn.IsLockedOn && playerLockOn.CurrentTarget != null)
        {
            Vector3 dirToTarget = playerLockOn.CurrentTarget.position - transform.position;
            dirToTarget.y = 0f;
            if (dirToTarget.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToTarget), Time.deltaTime * 15f);

            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camFwd.y = 0f; camRight.y = 0f;

            moveDir = (camFwd.normalized * inputDir.z + camRight.normalized * inputDir.x).normalized;
        }
        else
        {
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camFwd.y = 0f; camRight.y = 0f;

            moveDir = (camFwd.normalized * inputDir.z + camRight.normalized * inputDir.x).normalized;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        return moveDir * moveSpeed;
    }

    private Vector3 ComputeDodgeVelocity()
    {
        dodgeTimer -= Time.deltaTime;
        if (dodgeTimer <= 0f)
        {
            isDodging = false;
            cooldownTimer = dodgeCooldown;
            stateMachine?.ReturnToLocomotionIfState(PlayerStateType.Dash);
            return Vector3.zero;
        }

        return dodgeDirection * dodgeSpeed;
    }

    private void OnDodgePerformed(InputAction.CallbackContext context) => PerformDodge();

    public void PerformDodge()
    {
        if (isDodging || cooldownTimer > 0f) return;
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (stateMachine != null && !stateMachine.CanTransitionTo(PlayerStateType.Dash)) return;

        if (stamina != null && !stamina.TryConsumeStamina(dodgeStaminaCost)) return;

        // TryTransitionTo(Dash) automatically cancels Basic Attack & interruptible VFX once Stamina & CD succeed.
        if (stateMachine != null && !stateMachine.TryTransitionTo(PlayerStateType.Dash)) return;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (inputDir.sqrMagnitude > 0.01f)
        {
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camFwd.y = 0f; camRight.y = 0f;

            dodgeDirection = (camFwd.normalized * inputDir.z + camRight.normalized * inputDir.x).normalized;
            transform.rotation = Quaternion.LookRotation(dodgeDirection);
        }
        else
        {
            dodgeDirection = transform.forward;
        }

        isDodging = true;
        dodgeTimer = dodgeDuration;

        AudioManager.PlaySFX(AudioKeys.Combat_Dodge, transform.position);

        if (animator != null)
            animator.SetTrigger(PlayerAnimID.Dodge);
    }

    public void SetExternalInvincible(bool invincible)
    {
        externalInvincible = invincible;
    }

    public void WarpToPositionAndLookAt(Vector3 targetPosition, Vector3 lookAtWorldPoint)
    {
        isDodging = false;

        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;

        transform.position = targetPosition;
        initialGroundY = targetPosition.y;

        Vector3 lookDir = lookAtWorldPoint - targetPosition;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        if (wasEnabled) controller.enabled = true;
    }
}


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerMovement.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake() & IgnoreSelfCollisions()
 *    - ดึงคอมโพเนนต์ CharacterController, PlayerStamina, PlayerLockOn, PlayerCombat
 *    - ซิงก์ความเร็วการเดิน (moveSpeed) และส่วนลด Stamina ตอนกลิ้งหลบจาก GameProgressionManager
 *    - บันทึกความสูงระดับพื้นสนามเริ่มต้น (initialGroundY) สำหรับใช้ป้องกันตัวละครตกแมพ
 *    - สั่ง Physics.IgnoreCollision ไม่ให้ CharacterController ชนกับ Collider ลูกของตัวเอง
 *
 * 2. Update() & ComputeMovementVelocity()
 *    - อ่านค่าปุ่มเดิน WASD / ลูกศร / จอยเกม และคำนวณทิศทางการเดินอ้างอิงตามมุมกล้องหลัก
 *    - หากล็อกเป้าศัตรูอยู่ (PlayerLockOn) ตัวละครจะหันหน้าหาศัตรูพร้อมเดินสเตรฟซ้าย-ขวาได้
 *    - หากไม่ได้ล็อกเป้า ตัวละครจะหมุนตัวไปตามทิศทางที่เดินอย่างสมูทด้วย SmoothDampAngle
 *
 * 3. PerformDodge() & ComputeDodgeVelocity()
 *    - เมื่อกดปุ่ม Left Shift หรือ Space จะหักค่า Stamina และพุ่งตัวหลบ (Dodge) ไปตามทิศทางที่กด
 *    - เปิดสถานะอมตะชั่วคราว (IsInvincible / I-Frame) ตามเวลา iFrameDuration และสามารถกดกลิ้งเพื่อยกเลิกท่าโจมตีได้ทันที
 *
 * 4. ClampAboveGroundFloor() (ระบบป้องกันตัวละครจมพื้น/ตกแมพเวลามอนตี)
 *    - ทำงานทั้งใน Update() และ LateUpdate()
 *    - ตรวจสอบทุกเฟรมว่าค่าความสูงแกน Y ของตัวละครต่ำกว่าระดับพื้นเริ่มต้น (initialGroundY - floorHeightTolerance) หรือไม่
 *    - หากโดนมอนสเตอร์กระแทกหรือฟันจนกดตัวละครจมลงไปใต้พื้น ระบบจะดึงตัวละครกลับขึ้นมาบนระดับพื้นทันที ไม่ให้ตกแมพเด็ดขาด
 *
 * 5. LateUpdate() & ResolveCameraCollision() (ระบบกันกล้องทะลุกำแพง/ฉาก)
 *    - ทำงานเป็นลำดับสุดท้ายหลังจาก Cinemachine อัปเดตตำแหน่งกล้องเสร็จแล้ว ([DefaultExecutionOrder(2000)])
 *    - ยิง Physics.SphereCast จากระดับไหล่ผู้เล่น (cameraPivotHeight) ไปยังตำแหน่งกล้อง
 *    - หากมีกำแพงหรือฉากขวางกั้น จะดึงกล้องเข้ามาด้านหน้ากำแพงทันทีโดยไม่ชนตัวผู้เล่นหรือมอนสเตอร์
 *
 * 6. WarpToPositionAndLookAt()
 *    - ปิด CharacterController ชั่วคราวก่อนย้ายตำแหน่งตัวละคร (เช่น ตอนเริ่มด่านหรือวาร์ปตำแหน่ง) แล้วเปิดใหม่
 *
 * 7. ActivateSelectedCharacterVisualModel() (ระบบสลับโมเดล 3 ตัวละครในฉากต่อสู้)
 *    - หากลากโมเดล 3 ตัวละครใส่ในอาร์เรย์ characterVisualModels (0=Hikari, 1=Akane, 2=Shiro)
 *      ระบบจะเปิดโมเดลตัวที่เลือกจากหน้า Hub และผูก Animator ของโมเดลนั้นให้อัตโนมัติ (หากเว้นว่างไว้จะใช้โมเดลหลักตัวเดิม)
 * ============================================================================ */