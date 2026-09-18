using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Metrics (MVP Spec)")]
    [SerializeField] private float moveSpeed = 5f; // ความเร็ววิ่งพื้นฐาน 5 m/s
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Header("Dodge Settings (MVP Spec)")]
    [SerializeField] private float dodgeSpeed = 14f;
    [SerializeField] private float dodgeDuration = 0.35f;
    [SerializeField] private float iFrameDuration = 0.2f;    // อมตะ 0.2 วินาทีแรก
    [SerializeField] private float dodgeStaminaCost = 20f;   // ใช้ Stamina 20 หน่วย
    [SerializeField] private float dodgeCooldown = 0.2f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private PlayerStamina stamina;
    private PlayerLockOn playerLockOn;
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;
    private bool isDodging;
    private float dodgeTimer;
    private float cooldownTimer;
    private Vector3 dodgeDirection;

    private Vector3 verticalVelocity;
    private float currentVelocity;

    private bool externalInvincible = false;
    public bool IsInvincible => externalInvincible || (isDodging && (dodgeDuration - dodgeTimer) <= iFrameDuration);
    public bool IsDodging => isDodging;
    public bool CanMove { get; set; } = true;

    public InputSystem_Actions InputActions => inputActions;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();
        playerLockOn = GetComponent<PlayerLockOn>();
        inputActions = new InputSystem_Actions();

        EnsureCameraReference();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Dodge.performed += OnDodgePerformed;
    }

    private void OnDisable()
    {
        inputActions.Player.Dodge.performed -= OnDodgePerformed;
        inputActions.Player.Disable();
    }

    private void Start()
    {
        EnsureCameraReference();

        // ซ่อนและล็อกเมาส์ไว้กลางจอ
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EnsureCameraReference()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                // ค้นหากล้องที่มีแท็ก MainCamera หรือกล้องตัวแรกในฉาก
                Camera cam = FindAnyObjectByType<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }
    }

    private void Update()
    {
        // 1. อ่านค่าจาก Input System Action
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        // Fallback: หาก Action ยังไม่ทำงาน ให้อ่านจากคีย์บอร์ด WASD โดยตรงเพื่อความชัวร์
        if (moveInput.sqrMagnitude < 0.01f && Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            moveInput = new Vector2(x, y).normalized;
        }

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

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

        if (playerLockOn != null && playerLockOn.IsLockedOn)
        {
            Transform target = playerLockOn.CurrentTarget;
            Vector3 directionToTarget = (target.position - transform.position);
            directionToTarget.y = 0f;
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToTarget), Time.deltaTime * 15f);
            }

            if (inputDirection.magnitude >= 0.1f)
            {
                Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                camFwd.y = 0f;
                camFwd.Normalize();

                Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
                camRight.y = 0f;
                camRight.Normalize();

                Vector3 moveDir = (camFwd * inputDirection.z + camRight * inputDirection.x).normalized;
                controller.Move(moveDir * (moveSpeed * Time.deltaTime));
            }
        }
        else
        {
            if (inputDirection.magnitude >= 0.1f)
            {
                float camY = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + camY;
                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDir.normalized * (moveSpeed * Time.deltaTime));
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
                verticalVelocity.y = groundedGravity;
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
            float camY = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + camY;
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

    public void SetExternalInvincible(bool state)
    {
        externalInvincible = state;
    }
}