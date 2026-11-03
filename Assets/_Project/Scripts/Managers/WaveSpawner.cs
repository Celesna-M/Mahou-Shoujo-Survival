using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class WaveData
{
    public string waveName = "Stage Flow";
    public int NormalCount = 3;
    public int eliteCount = 0;
}

/// <summary>
/// WaveSpawner (Clean Collider Edition + Rank E -> S Atmosphere Switcher + GameTextMasterConfig Integration)
/// - Pulls all stage banners, boss approaching warnings, and stage clear banners directly from GameTextMasterConfig!
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject NormalPrefab;
    [SerializeField] private GameObject elitePrefab;

    private GameObject gruntPrefab => NormalPrefab;

    [Header("Spawn Locations (Uses Spawn Points or Optional Bounds)")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private BoxCollider arenaBoundsCollider;
    [SerializeField] private float arenaInnerMargin = 2.0f;

    [Header("1. Rank Atmosphere & Background Progression (Rank E -> Rank S)")]
    [SerializeField] private GameObject[] rankEnvironmentSets;
    [SerializeField] private bool applyRankAtmosphereLighting = true;
    [SerializeField] private Light directionalLight;

    [Header("Continuous Spawner (Trickle Flow)")]
    [SerializeField] private bool autoStartWaves = true;
    [SerializeField] private bool useContinuousTrickleFlow = true;
    [SerializeField] private float trickleSpawnInterval = 2.2f;
    [SerializeField] private int maxConcurrentEnemies = 6;
    [Range(3.0f, 5.0f)]
    [SerializeField] private float stageClearDelay = 4.0f;

    [Header("Stage Enemy Quota")]
    [SerializeField] private float timeBetweenWaves = 2.0f;
    [SerializeField] private List<WaveData> waves = new List<WaveData>();

    [Header("UI Feedback (Optional)")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private GameObject victoryPanel;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private bool isSpawningInProgress = false;
    private bool allQuotaSpawned = false;
    private bool isStageCleared = false;
    private int currentWaveIndex = 0;

    private void Start()
    {
        ApplyRankEnvironmentAndAtmosphere();

        if (waves.Count == 0)
        {
            waves.Add(new WaveData { waveName = "Phase 1", NormalCount = 3, eliteCount = 0 });
            waves.Add(new WaveData { waveName = "Phase 2", NormalCount = 5, eliteCount = 0 });
            waves.Add(new WaveData { waveName = "BOSS PHASE", NormalCount = 2, eliteCount = 1 });
        }

        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (autoStartWaves)
        {
            if (useContinuousTrickleFlow)
            {
                StartCoroutine(ContinuousTrickleSpawnRoutine());
            }
            else
            {
                StartCoroutine(LegacyWaveSpawnRoutine());
            }
        }
    }

    public void ApplyRankEnvironmentAndAtmosphere()
    {
        int stageIdx = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.SelectedStageIndex : 0;

        if (rankEnvironmentSets != null && rankEnvironmentSets.Length > 0)
        {
            int activeEnvIdx = Mathf.Clamp(stageIdx, 0, rankEnvironmentSets.Length - 1);
            for (int i = 0; i < rankEnvironmentSets.Length; i++)
            {
                if (rankEnvironmentSets[i] != null)
                {
                    rankEnvironmentSets[i].SetActive(i == activeEnvIdx);
                }
            }
        }

        if (!applyRankAtmosphereLighting) return;

        if (directionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    directionalLight = l;
                    break;
                }
            }
        }

        Color lightColor;
        Color ambientColor;
        switch (Mathf.Clamp(stageIdx, 0, 5))
        {
            case 0:
                lightColor = new Color(1.0f, 0.88f, 0.78f);
                ambientColor = new Color(0.24f, 0.20f, 0.28f);
                break;
            case 1:
                lightColor = new Color(1.0f, 0.72f, 0.62f);
                ambientColor = new Color(0.28f, 0.16f, 0.22f);
                break;
            case 2:
                lightColor = new Color(0.75f, 0.78f, 1.0f);
                ambientColor = new Color(0.16f, 0.18f, 0.30f);
                break;
            case 3:
                lightColor = new Color(0.96f, 0.64f, 0.82f);
                ambientColor = new Color(0.26f, 0.14f, 0.24f);
                break;
            case 4:
                lightColor = new Color(1.0f, 0.50f, 0.58f);
                ambientColor = new Color(0.30f, 0.10f, 0.16f);
                break;
            default:
                lightColor = new Color(1.0f, 0.38f, 0.45f);
                ambientColor = new Color(0.35f, 0.06f, 0.12f);
                break;
        }

        if (directionalLight != null)
        {
            directionalLight.color = lightColor;
        }
        RenderSettings.ambientLight = ambientColor;
    }

    public Vector3 GetRandomSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (pt != null)
            {
                return pt.position + new Vector3(Random.Range(-1.0f, 1.0f), 0f, Random.Range(-1.0f, 1.0f));
            }
        }

        if (arenaBoundsCollider != null)
        {
            Bounds b = arenaBoundsCollider.bounds;
            float marginX = Mathf.Min(arenaInnerMargin, b.extents.x * 0.5f);
            float marginZ = Mathf.Min(arenaInnerMargin, b.extents.z * 0.5f);
            float randomX = Random.Range(b.min.x + marginX, b.max.x - marginX);
            float randomZ = Random.Range(b.min.z + marginZ, b.max.z - marginZ);
            return new Vector3(randomX, transform.position.y, randomZ);
        }

        return transform.position + new Vector3(Random.Range(-3.0f, 3.0f), 0f, Random.Range(-3.0f, 3.0f));
    }

    private void Update()
    {
        if (isStageCleared || (!isSpawningInProgress && !allQuotaSpawned)) return;

        CleanupDeadEnemies();

        if (allQuotaSpawned && activeEnemies.Count == 0 && !isStageCleared)
        {
            isStageCleared = true;
            StartCoroutine(StageClearDelayRoutine());
        }
    }

    private void CleanupDeadEnemies()
    {
        activeEnemies.RemoveAll(enemy =>
        {
            if (enemy == null || !enemy.activeInHierarchy) return true;
            if (enemy.TryGetComponent<EnemyController>(out var controller) && controller.IsDead) return true;
            if (enemy.TryGetComponent<IDamageable>(out var damageable) && damageable.CurrentHealth <= 0f) return true;
            return false;
        });
    }

    private IEnumerator ContinuousTrickleSpawnRoutine()
    {
        isSpawningInProgress = true;
        allQuotaSpawned = false;

        int totalNormals = 0;
        int totalElites = 0;
        string bannerTitle = GameTextMasterConfig.Action_DefaultStageBanner;

        if (GameProgressionManager.Instance != null && GameProgressionManager.Instance.HasSelectedStageFromMap)
        {
            totalNormals = GameProgressionManager.Instance.SelectedStageNormalCount;
            totalElites = GameProgressionManager.Instance.SelectedStageEliteCount;
            bannerTitle = GameProgressionManager.Instance.SelectedStageTitle;
        }
        else
        {
            foreach (var w in waves)
            {
                totalNormals += Mathf.Max(0, w.NormalCount);
                totalElites += Mathf.Max(0, w.eliteCount);
            }
        }

        if (totalNormals == 0 && totalElites == 0)
        {
            totalNormals = 8;
            totalElites = 1;
        }

        if (waveText != null)
        {
            int stageIdx = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.SelectedStageIndex : 0;
            string storySub = GameProgressionManager.GetStoryLogForStage(stageIdx);
            waveText.text = string.Format(GameTextMasterConfig.Action_StageStartBannerFormat, bannerTitle, storySub);
            waveText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(2.2f);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        int spawnedNormals = 0;
        while (spawnedNormals < totalNormals)
        {
            CleanupDeadEnemies();

            if (activeEnemies.Count < maxConcurrentEnemies)
            {
                SpawnEnemyInArena(NormalPrefab, false);
                spawnedNormals++;
                yield return new WaitForSeconds(trickleSpawnInterval);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        int spawnedElites = 0;
        if (totalElites > 0 && waveText != null)
        {
            string rankCode = GameProgressionManager.Instance != null
                ? GameProgressionManager.GetRankLetterForStage(GameProgressionManager.Instance.SelectedStageIndex)
                : GameTextMasterConfig.RankLetters[0];
            waveText.text = string.Format(GameTextMasterConfig.Action_BossApproachingBannerFormat, rankCode);
            waveText.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.8f);
            waveText.gameObject.SetActive(false);
        }

        while (spawnedElites < totalElites)
        {
            GameObject bossPrefab = elitePrefab != null ? elitePrefab : NormalPrefab;
            SpawnEnemyInArena(bossPrefab, true);
            spawnedElites++;

            if (spawnedElites < totalElites)
                yield return new WaitForSeconds(trickleSpawnInterval * 1.5f);
        }

        isSpawningInProgress = false;
        allQuotaSpawned = true;
    }

    private IEnumerator LegacyWaveSpawnRoutine()
    {
        isSpawningInProgress = true;
        allQuotaSpawned = false;

        for (currentWaveIndex = 0; currentWaveIndex < waves.Count; currentWaveIndex++)
        {
            WaveData currentWave = waves[currentWaveIndex];

            if (waveText != null)
            {
                waveText.text = currentWave.waveName;
                waveText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(timeBetweenWaves);

            if (waveText != null)
                waveText.gameObject.SetActive(false);

            for (int i = 0; i < currentWave.NormalCount; i++)
            {
                SpawnEnemyInArena(NormalPrefab, false);
                yield return new WaitForSeconds(0.35f);
            }

            for (int i = 0; i < currentWave.eliteCount; i++)
            {
                SpawnEnemyInArena(elitePrefab != null ? elitePrefab : NormalPrefab, true);
                yield return new WaitForSeconds(0.35f);
            }

            while (activeEnemies.Count > 0)
            {
                CleanupDeadEnemies();
                yield return new WaitForSeconds(0.25f);
            }
        }

        isSpawningInProgress = false;
        allQuotaSpawned = true;
    }

    private void SpawnEnemyInArena(GameObject prefab, bool isEliteBoss)
    {
        if (prefab == null) return;

        Vector3 spawnPos = GetRandomSpawnPoint();
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (isEliteBoss && enemy.TryGetComponent<EnemyController>(out var ec))
        {
            string rankCode = GameProgressionManager.Instance != null
                ? GameProgressionManager.GetRankLetterForStage(GameProgressionManager.Instance.SelectedStageIndex)
                : GameTextMasterConfig.RankLetters[0];
            ec.ConfigureAsEliteOrBoss(true, string.Format(GameTextMasterConfig.Enemy_DefaultBossNameFormat, rankCode));
        }

        activeEnemies.Add(enemy);
    }

    private IEnumerator StageClearDelayRoutine()
    {
        if (GameProgressionManager.Instance != null)
        {
            GameProgressionManager.Instance.MarkCurrentStageCleared();
        }

        if (waveText != null)
        {
            var prog = GameProgressionManager.Instance;
            if (prog != null && prog.LastClearWasFirstTime)
            {
                waveText.text = string.Format(GameTextMasterConfig.Action_StageClearFirstTimeBannerFormat, prog.LastFirstClearStarDustBonus, prog.LastFirstClearCoreBonus);
            }
            else
            {
                waveText.text = GameTextMasterConfig.Action_StageClearBanner;
            }
            waveText.gameObject.SetActive(true);
        }

        Time.timeScale = 1.0f;
        yield return new WaitForSeconds(stageClearDelay);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        TriggerVictory();
    }

    private void TriggerVictory()
    {
        if (BattleHUD.Instance != null)
        {
            BattleHUD.Instance.ShowVictoryScreen();
        }
        else if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        activeEnemies.Clear();
    }
}


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน WaveSpawner.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. ApplyRankEnvironmentAndAtmosphere()
 *    - สลับชุดฉากหลัง (rankEnvironmentSets) และเปลี่ยนสีแสงไฟบรรยากาศในฉากต่อสู้ (Directional Light & Ambient Light)
 *      ตามระดับ Rank ของด่านที่เลือก (ตั้งแต่แสงสลัวห้องเรียนร้าง Rank E ไปจนถึงสีแดงเข้มแห่งห้วงลึก Rank S)
 *      โดยไม่ยุ่งหรือแก้ไข Collider ใดๆ ในฉาก
 *
 * 2. ContinuousTrickleSpawnRoutine()
 *    - ระบบทยอยปล่อยมอนสเตอร์อย่างต่อเนื่องตามโควตาของด่านที่เลือกจาก StageMap
 *    - รักษาจำนวนมอนสเตอร์ในฉากไม่ให้เกิน maxConcurrentEnemies และปล่อยบอสประจำ Rank ออกมาในช่วงท้าย
 *
 * 3. GetRandomSpawnPoint() & SpawnEnemyInArena()
 *    - สุ่มจุดเกิดของมอนสเตอร์จากอาร์เรย์ spawnPoints (หรือภายในขอบเขต arenaBoundsCollider ถ้ากำหนดไว้)
 *    - ตั้งค่าบอสให้เปิดระบบเกจ Stagger และแสดงชื่อบอสประจำ Rank บนหลอดเลือดด้านบนจอ
 *
 * 4. Update(), CleanupDeadEnemies() & StageClearDelayRoutine()
 *    - ตรวจสอบสถานะมอนสเตอร์ในฉากทุกเฟรม เมื่อมอนสเตอร์และบอสถูกกำจัดครบตามโควตาแล้ว
 *      จะสั่งบันทึกการผ่านด่าน (MarkCurrentStageCleared) แสดงป้าย STAGE CLEAR! พร้อมโบนัสเคลียร์ครั้งแรก
 *      แล้วเปิดหน้าจอสรุปผลชนะ (Victory Screen)
 * ============================================================================ */