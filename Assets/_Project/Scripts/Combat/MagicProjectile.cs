using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class MagicProjectile : MonoBehaviour
{
    [Header("Flight & Ballistics")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float lifeTime = 3.5f;
    [SerializeField] private float homingAngularSpeed = 160f;

    [Header("Damage & Proximity Splash (Semi-AoE)")]
    [SerializeField] private float directDamage = 35f;
    [SerializeField] private float splashDamage = 22f;
    [SerializeField] private float splashRadius = 3.2f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("FX")]
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private GameObject splashWaveVFXPrefab;
    [SerializeField] private float hitStopDuration = 0.04f;
    [SerializeField] private float shakeIntensity = 0.35f;

    private Transform homingTarget;
    private GameObject shooter;
    private bool hasImpacted = false;
    private float lifeTimer = 0f;

    private readonly Collider[] splashBuffer = new Collider[16];
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (TryGetComponent<SphereCollider>(out var col))
            col.isTrigger = true;
    }

    public void Initialize(GameObject caster, Transform target, float dmg, float splashDmg, float splashRad)
    {
        shooter = caster;
        homingTarget = target;
        directDamage = dmg;
        splashDamage = splashDmg;
        splashRadius = splashRad;
        hasImpacted = false;
        lifeTimer = 0f;
    }

    private void Update()
    {
        if (hasImpacted) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeTime)
        {
            Explode(transform.position, null);
            return;
        }

        // Soft Homing เข้าหาเป้าหมาย Lock-On
        if (homingTarget != null && homingTarget.gameObject.activeInHierarchy)
        {
            Vector3 aimPos = homingTarget.position + Vector3.up * 1.0f;
            Vector3 dir = (aimPos - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.forward = Vector3.RotateTowards(transform.forward, dir, homingAngularSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            }
        }

        float stepDist = speed * Time.deltaTime;
        Vector3 moveStep = transform.forward * stepDist;

        LayerMask mask = enemyLayers | obstacleLayers;
        if (Physics.SphereCast(transform.position, 0.25f, transform.forward, out RaycastHit hit, stepDist, mask))
        {
            if (shooter != null && (hit.collider.gameObject == shooter || hit.collider.transform.IsChildOf(shooter.transform)))
            {
                transform.position += moveStep;
                return;
            }

            Explode(hit.point, hit.collider);
            return;
        }

        transform.position += moveStep;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;
        if (shooter != null && (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform)))
            return;

        Explode(other.ClosestPoint(transform.position), other);
    }

    private void Explode(Vector3 hitPoint, Collider directHitCollider)
    {
        if (hasImpacted) return;
        hasImpacted = true;

        IDamageable primaryTarget = null;

        // 1. ดาเมจตรงเป้าหมายหลัก
        if (directHitCollider != null)
        {
            primaryTarget = directHitCollider.GetComponentInParent<IDamageable>();
            if (primaryTarget != null && !primaryTarget.IsDead)
                primaryTarget.TakeDamage(directDamage, hitPoint);
        }

        // 2. ดาเมจลูกหลงกึ่ง AoE (เฉพาะศัตรูที่อยู่ใกล้เป้าหมายหลัก)
        int nearbyCount = Physics.OverlapSphereNonAlloc(hitPoint, splashRadius, splashBuffer, enemyLayers);
        damagedTargets.Clear();
        bool hitSecondaryTarget = false;

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider col = splashBuffer[i];
            if (col == null || (directHitCollider != null && col.transform.IsChildOf(directHitCollider.transform)))
                continue;

            IDamageable splashTarget = col.GetComponentInParent<IDamageable>();
            if (splashTarget != null && splashTarget != primaryTarget && !splashTarget.IsDead)
            {
                if (damagedTargets.Add(splashTarget))
                {
                    splashTarget.TakeDamage(splashDamage, col.ClosestPoint(hitPoint));
                    hitSecondaryTarget = true;
                }
            }
        }

        // 3. VFX & Feedback
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, hitPoint, Quaternion.identity);

        if (hitSecondaryTarget && splashWaveVFXPrefab != null)
            Instantiate(splashWaveVFXPrefab, hitPoint, Quaternion.identity);

        if (HitStopManager.Instance != null && hitStopDuration > 0f)
            HitStopManager.Instance.TriggerHitStop(hitStopDuration, 0.1f);

        if (impulseSource != null && shakeIntensity > 0.05f)
            impulseSource.GenerateImpulse(shakeIntensity);

        Destroy(gameObject);
    }
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ MagicProjectile.cs]

1. Awake()
   - ดึง CinemachineImpulseSource และตั้งค่า SphereCollider ให้เป็น Trigger

2. Initialize(GameObject caster, Transform target, float dmg, float splashDmg, float splashRad)
   - ตั้งค่าข้อมูลเริ่มต้นของกระสุนเวท (ผู้ยิง, เป้าหมาย Homing, ดาเมจตรง, ดาเมจระเบิดรอบข้าง และรัศมี)

3. Update()
   - ควบคุมการเคลื่อนที่ของกระสุนเวท ระบบเลี้ยวเข้าหาเป้าหมายอัตโนมัติ (Soft Homing) และการตรวจจับการชนด้วย SphereCast

4. OnTriggerEnter(Collider other)
   - ตรวจจับเมื่อกระสุนชนเป้าหมายหรือสิ่งกีดขวาง แล้วสั่งระเบิดทำงาน

5. Explode(Vector3 hitPoint, Collider directHitCollider)
   - สร้างดาเมจใส่เป้าหมายหลักและดาเมจลูกหลง (Splash AoE) ใส่ศัตรูรอบข้าง พร้อมเล่นเอฟเฟกต์ VFX, Hit-stop และสั่นหน้าจอแล้วทำลายกระสุนทิ้ง
===============================================================================
*/
