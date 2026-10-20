using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

[System.Serializable]
public class WaveData
{
    public string waveName = "Stage Flow";
    public int NormalCount = 3;
    public int eliteCount = 0;
}

/// <summary>
/// WaveSpawner + Bounded Arena + Automatic Prop Collider Fitter & Anti-Climb Blocker
/// Does NOT use or require any "Obstacle" Tag or Layer.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject NormalPrefab;
    [SerializeField] private GameObject elitePrefab;

    private GameObject gruntPrefab => NormalPrefab;

    [Header("Phase 4: The Bounded Arena (BoxCollider)")]
    [SerializeField] private BoxCollider arenaBoundsCollider;
    [SerializeField] private Vector3 defaultArenaSize = new Vector3(28f, 6f, 28f);
    [SerializeField] private float arenaInnerMargin = 2.0f;
    [SerializeField] private bool autoCreateInvisibleWalls = true;
    [SerializeField] private bool autoSpawnPlayerAtEdge = true;
    [SerializeField] private bool randomizePlayerStartEdge = false;

    [Header("Prop Collider Auto-Fit & Anti-Climb Blocker")]
    [Tooltip("Automatically fits tight BoxColliders to props inside Props Root Parent and extends their top height upward so the player cannot step onto them")]
    [SerializeField] private bool autoFitAndBlockClimbOnProps = true;
    [Tooltip("Parent GameObject containing scene props (e.g. Props_Container). Keeps props on their existing Default layer without needing an Obstacle tag/layer.")]
    [SerializeField] private Transform propsRootParent;
    [Tooltip("Footprint tightness multiplier (0.95 = fits tightly around the prop mesh without invisible edge snagging)")]
    [Range(0.75f, 1.05f)]
    [SerializeField] private float propFootprintFitRatio = 0.95f;
    [Tooltip("Minimum world height of prop colliders so the player can never step on top of low props")]
    [SerializeField] private float minAntiClimbColliderHeight = 3.5f;

    [Header("Phase 5: Continuous Spawner (Trickle Flow)")]
    [SerializeField] private bool autoStartWaves = true;
    [SerializeField] private bool useContinuousTrickleFlow = true;
    [SerializeField] private float trickleSpawnInterval = 2.2f;
    [SerializeField] private int maxConcurrentEnemies = 6;
    [Range(3.0f, 5.0f)]
    [SerializeField] private float stageClearDelay = 4.0f;

    [Header("Stage Enemy Quota")]
    [SerializeField] private float timeBetweenWaves = 2.0f;
    [SerializeField] private List<WaveData> waves = new List<WaveData>();

    [Header("Legacy Spawn Points (Optional Fallback)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("UI Feedback (Optional)")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private GameObject victoryPanel;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private bool isSpawningInProgress = false;
    private bool allQuotaSpawned = false;
    private bool isStageCleared = false;
    private int currentWaveIndex = 0;

    private void Awake()
    {
        SetupBoundedArena();
        if (autoFitAndBlockClimbOnProps)
        {
            FitAndBlockClimbOnSceneProps();
        }
    }

    private void Start()
    {
        if (waves.Count == 0)
        {
            waves.Add(new WaveData { waveName = "Phase 1", NormalCount = 3, eliteCount = 0 });
            waves.Add(new WaveData { waveName = "Phase 2", NormalCount = 5, eliteCount = 0 });
            waves.Add(new WaveData { waveName = "BOSS PHASE", NormalCount = 2, eliteCount = 1 });
        }

        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (autoSpawnPlayerAtEdge)
        {
            AutoSpawnPlayerAtArenaEdge();
        }

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

    // =========================================================================
    // PROP COLLIDER AUTO-FITTER & ANTI-CLIMB PILLAR GENERATOR (NO OBSTACLE TAG/LAYER)
    // =========================================================================

    /// <summary>
    /// Right-click WaveSpawner in the Inspector and select "Auto-Fit Prop Colliders & Block Climbing"
    /// to fit colliders in Editor mode and see the green collider bounds immediately!
    /// </summary>
    [ContextMenu("Auto-Fit Prop Colliders & Block Climbing")]
    public void FitAndBlockClimbOnSceneProps()
    {
        var targetRenderers = new List<Renderer>();

        if (propsRootParent == null)
        {
            GameObject foundContainer = GameObject.Find("Props_Container") ?? GameObject.Find("Props");
            if (foundContainer != null)
            {
                propsRootParent = foundContainer.transform;
            }
        }

        if (propsRootParent != null)
        {
            targetRenderers.AddRange(propsRootParent.GetComponentsInChildren<Renderer>(true));
        }

        int fittedCount = 0;
        foreach (var rend in targetRenderers)
        {
            if (rend == null || rend is ParticleSystemRenderer) continue;
            if (rend.GetComponentInParent<PlayerMovement>() != null || rend.GetComponentInParent<EnemyController>() != null) continue;

            GameObject propObj = rend.gameObject;

            if (propObj.TryGetComponent<MeshCollider>(out var meshCol))
            {
                meshCol.enabled = false;
            }

            BoxCollider box = propObj.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = propObj.AddComponent<BoxCollider>();
            }

            box.isTrigger = false;

            if (propObj.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                Bounds meshBounds = mf.sharedMesh.bounds;
                float worldScaleY = Mathf.Max(0.01f, Mathf.Abs(propObj.transform.lossyScale.y));
                float requiredLocalHeight = Mathf.Max(meshBounds.size.y, minAntiClimbColliderHeight / worldScaleY);

                float heightDiff = requiredLocalHeight - meshBounds.size.y;
                box.size = new Vector3(
                    meshBounds.size.x * propFootprintFitRatio,
                    requiredLocalHeight,
                    meshBounds.size.z * propFootprintFitRatio);

                box.center = new Vector3(
                    meshBounds.center.x,
                    meshBounds.center.y + heightDiff * 0.5f,
                    meshBounds.center.z);

                fittedCount++;
            }
        }

        if (fittedCount > 0)
        {
            Debug.Log($"[WaveSpawner] Fitted tight anti-climb BoxColliders on {fittedCount} prop(s).");
        }
    }

    // =========================================================================
    // PHASE 4: BOUNDED ARENA, INVISIBLE WALLS, AUTO PLAYER & ENEMY SPAWN
    // =========================================================================

    private void SetupBoundedArena()
    {
        if (arenaBoundsCollider == null)
        {
            arenaBoundsCollider = GetComponent<BoxCollider>() ?? GetComponentInChildren<BoxCollider>();
        }

        if (arenaBoundsCollider == null)
        {
            foreach (var box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                string n = box.gameObject.name.ToLower();
                if (n.Contains("arena") || n.Contains("bound") || n.Contains("spawner") || n.Contains("wall"))
                {
                    arenaBoundsCollider = box;
                    break;
                }
            }
        }

        if (arenaBoundsCollider == null)
        {
            arenaBoundsCollider = gameObject.AddComponent<BoxCollider>();
            arenaBoundsCollider.size = defaultArenaSize;
            arenaBoundsCollider.center = new Vector3(0f, defaultArenaSize.y * 0.5f, 0f);
        }

        arenaBoundsCollider.isTrigger = true;

        if (autoCreateInvisibleWalls)
        {
            CreateInvisiblePerimeterWalls(arenaBoundsCollider.bounds);
        }
    }

    private void CreateInvisiblePerimeterWalls(Bounds bounds)
    {
        Transform existingWalls = transform.Find("[Arena_InvisibleWalls]");
        if (existingWalls != null) return;

        GameObject wallRoot = new GameObject("[Arena_InvisibleWalls]");
        wallRoot.transform.SetParent(transform, true);
        wallRoot.transform.position = Vector3.zero;
        wallRoot.transform.rotation = Quaternion.identity;

        float wallThickness = 1.0f;
        float wallHeight = Mathf.Max(bounds.size.y, 8.0f);
        float centerY = bounds.min.y + wallHeight * 0.5f;

        CreateSingleWall(wallRoot.transform, "Wall_North",
            new Vector3(bounds.center.x, centerY, bounds.max.z + wallThickness * 0.5f),
            new Vector3(bounds.size.x + wallThickness * 2f, wallHeight, wallThickness));

        CreateSingleWall(wallRoot.transform, "Wall_South",
            new Vector3(bounds.center.x, centerY, bounds.min.z - wallThickness * 0.5f),
            new Vector3(bounds.size.x + wallThickness * 2f, wallHeight, wallThickness));

        CreateSingleWall(wallRoot.transform, "Wall_East",
            new Vector3(bounds.max.x + wallThickness * 0.5f, centerY, bounds.center.z),
            new Vector3(wallThickness, wallHeight, bounds.size.z + wallThickness * 2f));

        CreateSingleWall(wallRoot.transform, "Wall_West",
            new Vector3(bounds.min.x - wallThickness * 0.5f, centerY, bounds.center.z),
            new Vector3(wallThickness, wallHeight, bounds.size.z + wallThickness * 2f));
    }

    private void CreateSingleWall(Transform parent, string wallName, Vector3 worldCenter, Vector3 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent, true);
        wall.transform.position = worldCenter;

        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;
        col.isTrigger = false;
    }

    public void AutoSpawnPlayerAtArenaEdge()
    {
        if (arenaBoundsCollider == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }
        if (player == null) return;

        Bounds b = arenaBoundsCollider.bounds;
        float marginX = Mathf.Min(arenaInnerMargin, b.extents.x * 0.4f);
        float marginZ = Mathf.Min(arenaInnerMargin, b.extents.z * 0.4f);

        int edgeIndex = randomizePlayerStartEdge ? Random.Range(0, 4) : 0;
        Vector3 spawnPos = b.center;

        switch (edgeIndex)
        {
            case 0:
                spawnPos = new Vector3(b.center.x, b.min.y, b.min.z + marginZ);
                break;
            case 1:
                spawnPos = new Vector3(b.center.x, b.min.y, b.max.z - marginZ);
                break;
            case 2:
                spawnPos = new Vector3(b.min.x + marginX, b.min.y, b.center.z);
                break;
            case 3:
                spawnPos = new Vector3(b.max.x - marginX, b.min.y, b.center.z);
                break;
        }

        spawnPos.y = SampleGroundHeight(spawnPos, player.transform.position.y);
        Vector3 arenaCenterLookTarget = new Vector3(b.center.x, spawnPos.y, b.center.z);

        if (player.TryGetComponent<PlayerMovement>(out var movement))
        {
            movement.WarpToPositionAndLookAt(spawnPos, arenaCenterLookTarget);
        }
        else
        {
            player.transform.position = spawnPos;
            player.transform.LookAt(arenaCenterLookTarget);
        }
    }

    public Vector3 GetRandomSpawnPointInBounds()
    {
        if (arenaBoundsCollider != null)
        {
            Bounds b = arenaBoundsCollider.bounds;
            float marginX = Mathf.Min(arenaInnerMargin, b.extents.x * 0.5f);
            float marginZ = Mathf.Min(arenaInnerMargin, b.extents.z * 0.5f);

            int checkMask = ~LayerMask.GetMask("Ignore Raycast", "UI");

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float randomX = Random.Range(b.min.x + marginX, b.max.x - marginX);
                float randomZ = Random.Range(b.min.z + marginZ, b.max.z - marginZ);
                Vector3 candidate = new Vector3(randomX, b.min.y, randomZ);
                candidate.y = SampleGroundHeight(candidate, transform.position.y);

                // Sphere centered 1.0m above the floor with 0.45m radius avoids the floor itself while detecting any prop/wall
                if (!Physics.CheckSphere(candidate + Vector3.up * 1.0f, 0.45f, checkMask, QueryTriggerInteraction.Ignore))
                {
                    return candidate;
                }
            }
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (pt != null) return pt.position;
        }

        return transform.position;
    }

    private float SampleGroundHeight(Vector3 pos, float fallbackY)
    {
        float baseFloorY = arenaBoundsCollider != null ? arenaBoundsCollider.bounds.min.y : fallbackY;
        Vector3 rayOrigin = new Vector3(pos.x, baseFloorY + 10f, pos.z);
        int mask = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI");

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 25f, mask, QueryTriggerInteraction.Ignore);
        float bestY = baseFloorY;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            float diff = Mathf.Abs(hits[i].point.y - baseFloorY);
            if (diff <= 1.0f && diff < bestDiff)
            {
                bestDiff = diff;
                bestY = hits[i].point.y;
            }
        }

        return bestY;
    }

    // =========================================================================
    // PHASE 5: CONTINUOUS TRICKLE SPAWNER & 3-5s STAGE CLEAR DELAY
    // =========================================================================

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
        string bannerTitle = "SURVIVE THE ENTITIES";

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
            waveText.text = bannerTitle;
            waveText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(1.8f);

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
            waveText.text = "WARNING: ELITE ENTITY APPROACHING";
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

        Vector3 spawnPos = GetRandomSpawnPointInBounds();
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (isEliteBoss && enemy.TryGetComponent<EnemyController>(out var ec))
        {
            ec.ConfigureAsEliteOrBoss(true, "THE ENTITY (BOSS)");
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
            waveText.text = "STAGE CLEAR!";
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

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = arenaBoundsCollider != null ? arenaBoundsCollider : GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
        }
    }
}