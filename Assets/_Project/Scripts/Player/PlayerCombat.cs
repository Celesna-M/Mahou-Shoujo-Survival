using Unity.Cinemachine; // สำหรับ Cinemachine ใน Unity 6
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

    [Header("Game Feel (Hit-Stop & Shake)")]
    // ระยะเวลาหยุดของแต่ละฮิต (ฮิต 3 นานสุดเพื่อปิดคอมโบ)
    [SerializeField] private float[] hitStopDurations = { 0.04f, 0.05f, 0.12f };
    // แรงสั่นกล้องของแต่ละฮิต
    [SerializeField] private float[] shakeForces = { 0.3f, 0.4f, 1.0f };

    [Header("Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private LayerMask enemyLayers;

    private PlayerMovement playerMovement;
    private CinemachineImpulseSource impulseSource;

    private int comboIndex = 0;
    private int currentHitIndex = 0; // จำว่าฮิตปัจจุบันคือฮิตไหน
    private float attackTimer = 0f;
    private float comboResetTimer = 0f;
    private bool isAttacking = false;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
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

        if (comboIndex > 0 && !isAttacking)
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
        if (playerMovement.IsDodging || isAttacking)
            return;

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
            // 1. กระตุ้น Hit-Stop ชั่วเสี้ยววินาที
            if (HitStopManager.Instance != null)
            {
                HitStopManager.Instance.TriggerHitStop(hitStopDurations[hitStep]);
            }

            // 2. สั่งกล้องสั่นตามแรงของแต่ละฮิต
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(shakeForces[hitStep]);
            }

            // 3. สร้างความเสียหายให้กับเป้าหมายทั้งหมดที่อยู่ในระยะ
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
    }
}