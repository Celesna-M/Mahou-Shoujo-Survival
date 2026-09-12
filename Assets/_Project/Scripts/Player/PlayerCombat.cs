using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private float attackDuration = 0.4f;
    [SerializeField] private float comboResetTime = 0.8f;
    [SerializeField] private float[] comboDamages = { 20f, 25f, 45f };

    [Header("Homing Dash (Gap Closer)")]
    [SerializeField] private float meleeRange = 2.0f;        // ระยะฟันปกติ
    [SerializeField] private float homingMaxDistance = 12f;  // ระยะไกลสุดที่พุ่งหาได้
    [SerializeField] private float homingSpeed = 26f;        // ความเร็วในการพุ่ง
    [SerializeField] private TrailRenderer dashTrail;        // เอฟเฟกต์เส้นแสงตอนพุ่ง

    [Header("Game Feel (Hit-Stop & Shake)")]
    [SerializeField] private float[] hitStopDurations = { 0.04f, 0.05f, 0.12f };
    [SerializeField] private float[] shakeForces = { 0.3f, 0.4f, 1.0f };

    [Header("Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private LayerMask enemyLayers;

    private PlayerMovement playerMovement;
    private PlayerLockOn playerLockOn;
    private CharacterController controller;
    private CinemachineImpulseSource impulseSource;

    private int comboIndex = 0;
    private int currentHitIndex = 0;
    private float attackTimer = 0f;
    private float comboResetTimer = 0f;
    private bool isAttacking = false;
    private bool isHoming = false;
    private Coroutine homingCoroutine;

    public bool IsHoming => isHoming;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerLockOn = GetComponent<PlayerLockOn>();
        controller = GetComponent<CharacterController>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }
    }

    private void Start()
    {
        playerMovement.InputActions.Player.Attack.performed += OnAttackPerformed;
        playerMovement.InputActions.Player.Dodge.performed += OnDodgeTriggered;
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.Attack.performed -= OnAttackPerformed;
            playerMovement.InputActions.Player.Dodge.performed -= OnDodgeTriggered;
        }
    }

    private void Update()
    {
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                EndAttack();
            }
        }

        if (comboIndex > 0 && !isAttacking && !isHoming)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                comboIndex = 0;
            }
        }
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        // ไม่ออกท่าหากกำลังหลบ, กำลังพุ่ง, หรือกำลังฟันอยู่
        if (playerMovement.IsDodging || isHoming || isAttacking)
            return;

        // ตรวจสอบเงื่อนไข Homing Dash: ถ้าล็อกเป้าอยู่และศัตรูอยู่ห่างเกินระยะฟัน
        if (playerLockOn != null && playerLockOn.IsLockedOn)
        {
            Transform target = playerLockOn.CurrentTarget;
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            if (distanceToTarget > meleeRange && distanceToTarget <= homingMaxDistance)
            {
                StartHomingDash(target);
                return;
            }
        }

        // หากอยู่ในระยะประชิดอยู่แล้ว ให้ฟันปกติ
        StartAttack();
    }

    private void StartHomingDash(Transform target)
    {
        if (homingCoroutine != null)
        {
            StopCoroutine(homingCoroutine);
        }

        homingCoroutine = StartCoroutine(DoHomingDash(target));
    }

    private IEnumerator DoHomingDash(Transform target)
    {
        isHoming = true;
        playerMovement.CanMove = false;

        if (dashTrail != null)
        {
            dashTrail.emitting = true;
        }

        float maxDashTime = 0.4f; // Timeout ป้องกันการพุ่งติดสิ่งกีดขวาง
        float timer = 0f;

        while (timer < maxDashTime)
        {
            // หากเป้าหมายตายหรือถูกทำลายกลางคัน
            if (target == null || !target.gameObject.activeInHierarchy)
                break;

            Vector3 direction = (target.position - transform.position);
            direction.y = 0f;
            float distance = direction.magnitude;

            // หมุนหน้าเข้าหาเป้าหมาย
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // เมื่อเข้าถึงระยะฟัน ให้หยุดพุ่ง
            if (distance <= meleeRange)
            {
                break;
            }

            // พุ่งตัวเข้าหาเป้าหมาย
            controller.Move(direction.normalized * (homingSpeed * Time.deltaTime));

            timer += Time.deltaTime;
            yield return null;
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }

        isHoming = false;
        homingCoroutine = null;

        // เมื่อถึงตัวศัตรูแล้ว ออกดาบฟันทันที!
        StartAttack();
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackDuration;
        comboResetTimer = comboResetTime;

        playerMovement.CanMove = false;

        currentHitIndex = comboIndex;
        ExecuteAttackHitbox(comboDamages[currentHitIndex], currentHitIndex);

        comboIndex = (comboIndex + 1) % comboDamages.Length;
    }

    private void EndAttack()
    {
        isAttacking = false;
        playerMovement.CanMove = true;
    }

    private void OnDodgeTriggered(InputAction.CallbackContext context)
    {
        // สามารถกด Dodge เพื่อแคนเซิลการพุ่ง (Homing Dash) ได้ด้วย
        if (isHoming)
        {
            if (homingCoroutine != null)
            {
                StopCoroutine(homingCoroutine);
                homingCoroutine = null;
            }
            if (dashTrail != null)
            {
                dashTrail.emitting = false;
            }
            isHoming = false;
            Debug.Log("[Combat] Homing Dash Cancelled by Dodge!");
        }

        if (isAttacking)
        {
            EndAttack();
            comboIndex = 0;
        }
    }

    private void ExecuteAttackHitbox(float damage, int hitStep)
    {
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Collider[] hitColliders = Physics.OverlapSphere(origin, attackRadius, enemyLayers);

        if (hitColliders.Length > 0)
        {
            if (HitStopManager.Instance != null)
            {
                HitStopManager.Instance.TriggerHitStop(hitStopDurations[hitStep]);
            }

            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(shakeForces[hitStep]);
            }

            foreach (var hit in hitColliders)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage, hit.ClosestPoint(origin));
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Gizmos.DrawWireSphere(origin, attackRadius);

        // แสดงระยะตรวจจับ Homing Dash
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, homingMaxDistance);
    }
}