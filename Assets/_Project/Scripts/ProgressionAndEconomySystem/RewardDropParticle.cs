using UnityEngine;

// =========================================================================
// PHASE 5: HOMING PARTICLE DROP
// =========================================================================

public class RewardDropParticle : MonoBehaviour
{
    private RewardDropType dropType;
    private int amount;
    private Transform targetPlayer;
    private PlayerHealth playerHealth;

    private Vector3 scatterVelocity;
    private float scatterTimer = 0.32f;
    private float currentHomingSpeed = 7f;
    private float maxHomingSpeed = 26f;
    private float acceleration = 32f;
    private bool isCollected = false;

    public static void Spawn(Vector3 position, RewardDropType type, int amount, GameObject optionalPrefab = null)
    {
        GameObject dropObj;

        if (optionalPrefab != null)
        {
            dropObj = Instantiate(optionalPrefab, position, Quaternion.identity);
        }
        else
        {
            dropObj = new GameObject($"RewardParticle_{type}");
            dropObj.transform.position = position;
            CreateProceduralParticleVisual(dropObj, type);
        }

        RewardDropParticle drop = dropObj.GetComponent<RewardDropParticle>();
        if (drop == null)
        {
            drop = dropObj.AddComponent<RewardDropParticle>();
        }

        drop.Initialize(type, amount);
    }

    private static void CreateProceduralParticleVisual(GameObject host, RewardDropType type)
    {
        Color mainColor = (type == RewardDropType.ShatteredCore)
            ? new Color(0.95f, 0.25f, 1.0f)
            : new Color(1.0f, 0.88f, 0.25f);

        GameObject visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualSphere.transform.SetParent(host.transform, false);
        visualSphere.transform.localScale = (type == RewardDropType.ShatteredCore) ? Vector3.one * 0.34f : Vector3.one * 0.24f;

        if (visualSphere.TryGetComponent<Collider>(out var col))
        {
            Destroy(col);
        }

        if (visualSphere.TryGetComponent<Renderer>(out var rend))
        {
            rend.material.color = mainColor;
        }

        ParticleSystem ps = host.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = mainColor;
        main.startSize = (type == RewardDropType.ShatteredCore) ? 0.22f : 0.15f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
    }

    public void Initialize(RewardDropType type, int rewardAmount)
    {
        dropType = type;
        amount = rewardAmount;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.isTrigger = true;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
        }
        trigger.isTrigger = true;
        trigger.radius = 0.5f;

        Vector2 randCircle = Random.insideUnitCircle * 2.2f;
        scatterVelocity = new Vector3(randCircle.x, Random.Range(2.2f, 3.6f), randCircle.y);

        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            var ph = FindAnyObjectByType<PlayerHealth>();
            if (ph != null) playerObj = ph.gameObject;
        }

        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>() ?? playerObj.GetComponentInChildren<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (targetPlayer == null)
        {
            FindPlayer();
            if (targetPlayer == null) return;
        }

        if (scatterTimer > 0f)
        {
            scatterTimer -= Time.deltaTime;
            transform.position += scatterVelocity * Time.deltaTime;
            scatterVelocity = Vector3.Lerp(scatterVelocity, Vector3.zero, Time.deltaTime * 6f);
            return;
        }

        currentHomingSpeed = Mathf.Min(maxHomingSpeed, currentHomingSpeed + acceleration * Time.deltaTime);
        Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;

        transform.position = Vector3.MoveTowards(transform.position, targetCenter, currentHomingSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetCenter) <= 0.55f)
        {
            Collect();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || scatterTimer > 0.1f) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (playerHealth == null && targetPlayer != null)
        {
            playerHealth = targetPlayer.GetComponent<PlayerHealth>() ?? targetPlayer.GetComponentInChildren<PlayerHealth>();
        }

        Vector3 feedbackPos = targetPlayer != null ? targetPlayer.position + Vector3.up * 1.5f : transform.position;

        if (dropType == RewardDropType.ShatteredCore)
        {
            playerHealth?.AddShatteredCore(amount);
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.SpawnFloatingFeedback("CORE", "Shattered Core", amount, new Color(0.95f, 0.35f, 1f), feedbackPos);
            }
        }
        else
        {
            playerHealth?.AddStarDust(amount);
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.SpawnFloatingFeedback("STAR DUST", "Star Dust", amount, new Color(1f, 0.88f, 0.25f), feedbackPos);
            }
        }

        Destroy(gameObject);
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน RewardDropParticle.cs (ProgressionAndEconomySystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Spawn(Vector3 position, RewardDropType type, int amount, GameObject optionalPrefab)
 *    - เสกลูกแก้วทรัพยากร Star Dust (สีทอง) หรือ Shattered Core (สีม่วง) ณ ตำแหน่งที่มอนสเตอร์ตาย
 *
 * 2. CreateProceduralParticleVisual(GameObject host, RewardDropType type)
 *    - สร้างทรงกลมเรืองแสงและ ParticleSystem อัตโนมัติหากไม่ได้กำหนด Prefab ไว้
 *
 * 3. Update() & Collect()
 *    - ลูกแก้วจะกระเด็นกระจายออกสั้นๆ (0.32 วินาที) แล้วพุ่งเข้าหาตัวผู้เล่นด้วยความเร่งอัตโนมัติ (Homing)
 *    - เมื่อเข้าถึงตัวผู้เล่นจะบวกค่าแร่เข้า PlayerHealth / GameProgressionManager และแสดงตัวเลขเด้งบน BattleHUD
 * ============================================================================ */
