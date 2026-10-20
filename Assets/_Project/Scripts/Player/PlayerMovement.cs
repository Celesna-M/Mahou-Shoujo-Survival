using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [DefaultExecutionOrder(2000)] ensures LateUpdate runs AFTER CinemachineBrain,
/// so Camera Anti-Clipping always pulls the camera in front of walls/props without being overwritten by Cinemachine.
/// Does NOT require any "Obstacle" Tag or Layer.
/// </summary>
[DefaultExecutionOrder(2000)]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Header("1. Camera Anti-Clipping (Prevent Camera Going Through Scene/Walls)")]
    [Tooltip("Automatically pulls the Main Camera forward when a wall, floor, or prop blocks the line of sight")]
    [SerializeField] private bool enableCameraAntiClip = true;
    [Tooltip("Height on the player where the camera raycast starts (shoulder/head height)")]
    [SerializeField] private float cameraPivotHeight = 1.45f;
    [Tooltip("Radius of the sphere cast used to keep the camera away from walls")]
    [SerializeField] private float cameraCollisionRadius = 0.28f;
    [Tooltip("Minimum distance allowed between camera and player pivot")]
    [SerializeField] private float minCameraDistance = 0.55f;
    [Tooltip("Layers that block the camera (defaults to everything except Player, Enemy, Ignore Raycast, UI)")]
    [SerializeField] private LayerMask cameraCollisionLayers;

    [Header("2. Anti-Climb / No-Step Prop Guard (Prevent Stepping Onto Props)")]
    [Tooltip("Locks CharacterController stepOffset to a tiny value (0.05m) so the player cannot step up onto props")]
    [SerializeField] private float maxStepOffset = 0.05f;
    [Tooltip("Locks CharacterController slopeLimit (15 deg) so the player cannot walk up sloped prop edges")]
    [SerializeField] private float maxSlopeLimit = 15f;

    [Header("Dodge & I-Frame Settings")]
    [SerializeField] private float dodgeSpeed = 14f;
    [SerializeField] private float dodgeDuration = 0.35f;
    [SerializeField] private float iFrameDuration = 0.2f;
    [SerializeField] private float dodgeStaminaCost = 20f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    [Header("Gravity")]
    [SerializeField] private float downwardForce = -9.8f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private PlayerStamina stamina;
    private PlayerLockOn playerLockOn;
    private PlayerCombat playerCombat;
    private PlayerExecution playerExecution;
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

    public bool IsInvincible => externalInvincible || (isDodging && (dodgeDuration - dodgeTimer) <= iFrameDuration);
    public bool IsDodging => isDodging;
    public bool CanMove { get; set; } = true;
    public Vector2 MoveInput => moveInput;
    public bool IsMoving => CanMove && !isDodging && moveInput.sqrMagnitude >= 0.01f;

    public bool IsGrounded => true;
    public Animator Animator => animator;
    public InputSystem_Actions InputActions => inputActions;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();
        playerLockOn = GetComponent<PlayerLockOn>();
        playerCombat = GetComponent<PlayerCombat>();
        playerExecution = GetComponent<PlayerExecution>();
        inputActions = new InputSystem_Actions();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (GameProgressionManager.Instance != null && GameProgressionManager.Instance.HasSelectedCharacter)
        {
            moveSpeed = GameProgressionManager.Instance.SelectedCharacterMoveSpeed;
        }

        if (cameraCollisionLayers.value == 0)
        {
            cameraCollisionLayers = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI");
        }

        ConfigureCharacterController();
        IgnoreSelfCollisions();
        initialGroundY = transform.position.y;
    }

    private void ConfigureCharacterController()
    {
        if (controller == null) return;

        if (controller.height < 1.5f || controller.height > 2.5f)
            controller.height = 2.0f;

        float correctCenterY = controller.height * 0.5f;
        if (Mathf.Abs(controller.center.y - correctCenterY) > 0.05f)
        {
            Vector3 center = controller.center;
            center.y = correctCenterY;
            controller.center = center;
        }

        // Lock stepOffset and slopeLimit so CharacterController never steps up onto props
        controller.stepOffset = Mathf.Clamp(maxStepOffset, 0.01f, 0.1f);
        controller.slopeLimit = Mathf.Clamp(maxSlopeLimit, 5f, 30f);
        controller.skinWidth = 0.02f;
        controller.minMoveDistance = 0f;

        if (animator != null && animator.applyRootMotion)
            animator.applyRootMotion = false;
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
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen)
            return;

        if (playerExecution != null && playerExecution.IsExecuting)
        {
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

        UpdateAnimatorParameters(horizontalVelocity);
    }

    private void LateUpdate()
    {
        if (enableCameraAntiClip)
        {
            ResolveCameraCollision();
        }
    }

    /// <summary>
    /// Runs after CinemachineBrain in LateUpdate (ExecutionOrder 2000).
    /// Casts a sphere from the player's shoulder/head pivot to the camera and pulls the camera in front of any hit wall or prop.
    /// </summary>
    private void ResolveCameraCollision()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform == null) return;
        }

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

    /// <summary>
    /// Slides the player off horizontally if they ever touch the upper surface of an elevated prop or enemy.
    /// Uses geometry & height checks instead of any "Obstacle" Tag or Layer.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null || hit.collider.isTrigger) return;

        bool isEnemy = hit.collider.GetComponentInParent<EnemyController>() != null;
        bool isElevatedSurface = hit.point.y > initialGroundY + 0.12f &&
                                 hit.collider.bounds.max.y > initialGroundY + 0.15f;

        if ((isEnemy || isElevatedSurface) && hit.normal.y > 0.35f && transform.position.y > initialGroundY + 0.08f)
        {
            Vector3 pushOut = new Vector3(
                transform.position.x - hit.collider.bounds.center.x,
                0f,
                transform.position.z - hit.collider.bounds.center.z);

            if (pushOut.sqrMagnitude < 0.001f) pushOut = -transform.forward;
            pushOut.Normalize();

            if (controller != null && controller.enabled)
            {
                controller.Move((pushOut * 5.0f + Vector3.down * 10f) * Time.deltaTime);
            }
        }
    }

    private void ComputeVerticalVelocity()
    {
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

        animator.SetFloat(PlayerAnimID.Speed, targetSpeed);
        animator.SetFloat(PlayerAnimID.X, targetX);
        animator.SetFloat(PlayerAnimID.Y, targetY);
    }

    private Vector3 ComputeMovementVelocity()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (inputDir.sqrMagnitude < 0.01f) return Vector3.zero;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

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
            return Vector3.zero;
        }

        return dodgeDirection * dodgeSpeed;
    }

    private void OnDodgePerformed(InputAction.CallbackContext context) => PerformDodge();

    public void PerformDodge()
    {
        if (isDodging || cooldownTimer > 0f) return;
        if (playerExecution != null && playerExecution.IsExecuting) return;

        if (stamina != null && !stamina.TryConsumeStamina(dodgeStaminaCost)) return;

        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
        if (playerCombat != null && playerCombat.IsAttacking)
        {
            playerCombat.CancelCombat();
        }

        CanMove = true;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

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