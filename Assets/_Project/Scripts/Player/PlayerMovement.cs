using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeSpeed = 16f;
    [SerializeField] private float dodgeDuration = 0.25f;
    [SerializeField] private float dodgeStaminaCost = 25f;
    [SerializeField] private float dodgeCooldown = 0.4f;

    [Header("Gravity Settings")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private PlayerStamina stamina;
    private InputSystem_Actions inputActions; // เปลี่ยนมาใช้ InputSystem_Actions

    private Vector2 moveInput;
    private bool isSprinting;
    private bool isDodging;
    private float dodgeTimer;
    private float cooldownTimer;
    private Vector3 dodgeDirection;

    private PlayerLockOn playerLockOn;
    private Vector3 verticalVelocity;
    private float currentVelocity;

    // สถานะสำหรับระบบภายนอกตรวจสอบ
    public bool IsInvincible => isDodging;
    public bool IsDodging => isDodging;
    public bool CanMove { get; set; } = true; // ใช้ล็อกตัวละครขณะกำลังฟันดาบ

    public InputSystem_Actions InputActions => inputActions; // ให้ระบบ Combat เข้าถึง Input ได้

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();
        playerLockOn = GetComponent<PlayerLockOn>(); // ดึงคอมโพเนนต์ Lock-On
        inputActions = new InputSystem_Actions();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Sprint.performed += OnSprintStarted;
        inputActions.Player.Sprint.canceled += OnSprintCanceled;
        inputActions.Player.Dodge.performed += OnDodgePerformed;
    }

    private void OnDisable()
    {
        inputActions.Player.Sprint.performed -= OnSprintStarted;
        inputActions.Player.Sprint.canceled -= OnSprintCanceled;
        inputActions.Player.Dodge.performed -= OnDodgePerformed;
        inputActions.Player.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (isDodging)
        {
            HandleDodgeMovement();
        }
        else if (CanMove)
        {
            HandleMovement();
        }

        ApplyGravity();
    }

    private void HandleMovement()
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // --- กรณีที่ 1: กำลังล็อกเป้าศัตรู (Strafe Mode) ---
        if (playerLockOn != null && playerLockOn.IsLockedOn)
        {
            Transform target = playerLockOn.CurrentTarget;

            // หันหน้ามองตรงไปที่ศัตรูเสมอ
            Vector3 directionToTarget = (target.position - transform.position);
            directionToTarget.y = 0f;
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
            }

            // เคลื่อนที่ Strafe สัมพันธ์กับมุมมองกล้อง
            if (inputDirection.magnitude >= 0.1f)
            {
                Vector3 cameraForward = cameraTransform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                Vector3 cameraRight = cameraTransform.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                Vector3 moveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x).normalized;
                controller.Move(moveDirection * (currentSpeed * Time.deltaTime));
            }
        }
        // --- กรณีที่ 2: โหมดวิ่งอิสระ (Free Roam Mode) ---
        else
        {
            if (inputDirection.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

                Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDirection.normalized * (currentSpeed * Time.deltaTime));
            }
        }
    }

    private void HandleDodgeMovement()
    {
        dodgeTimer -= Time.deltaTime;
        controller.Move(dodgeDirection * (dodgeSpeed * Time.deltaTime));

        if (dodgeTimer <= 0f)
        {
            isDodging = false;
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = groundedGravity;
            }
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void OnDodgePerformed(InputAction.CallbackContext context)
    {
        if (isDodging || cooldownTimer > 0f || !controller.isGrounded)
            return;

        if (!stamina.TryConsumeStamina(dodgeStaminaCost))
            return;

        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            dodgeDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }
        else
        {
            dodgeDirection = transform.forward;
        }

        isDodging = true;
        dodgeTimer = dodgeDuration;
        cooldownTimer = dodgeCooldown;
    }

    private void OnSprintStarted(InputAction.CallbackContext context) => isSprinting = true;
    private void OnSprintCanceled(InputAction.CallbackContext context) => isSprinting = false;
}