using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Header("Gravity Settings")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private bool isSprinting;

    private Vector3 verticalVelocity;
    private float currentVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        // ดักจับการกดปุ่มวิ่ง (Sprint)
        inputActions.Player.Sprint.performed += OnSprintStarted;
        inputActions.Player.Sprint.canceled += OnSprintCanceled;
    }

    private void OnDisable()
    {
        inputActions.Player.Sprint.performed -= OnSprintStarted;
        inputActions.Player.Sprint.canceled -= OnSprintCanceled;

        inputActions.Player.Disable();
    }

    private void Start()
    {
        // ล็อกเมาส์ไว้กลางจอสำหรับเกมแนว Action 3D
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // อ่านค่า Input Vector2 จาก New Input System
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        HandleMovement();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (inputDirection.magnitude >= 0.1f)
        {
            // คำนวณมุมหันหน้าสัมพันธ์กับทิศทางที่กล้องมอง (Camera-Relative Movement)
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;

            // หมุนตัวละครเข้าหาทิศทางอย่างนุ่มนวล
            float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

            // เคลื่อนที่ไปในทิศทางที่หันหน้าไป
            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDirection.normalized * (currentSpeed * Time.deltaTime));
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            // ค่าแรงกดลงพื้นเล็กน้อยเพื่อให้ติดพื้นผิวอย่างมั่นคง
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = groundedGravity;
            }
        }
        else
        {
            // คำนวณแรงโน้มถ่วงสะสมตามเวลาเมื่ออยู่กลางอากาศ
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void OnSprintStarted(InputAction.CallbackContext context)
    {
        isSprinting = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }
}