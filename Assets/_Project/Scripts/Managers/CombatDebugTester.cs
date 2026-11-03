using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public class MasterCharacterConfig
{
    public string characterId = "CHAR_01";
    public string characterName = "01 : HIKARI - BRAWLER STRIKER";
    [TextArea(1, 2)]
    public string characterRoleAndLore = "Balanced Striker | High mobility and fast melee combos.";
    [Range(20f, 2000f)] public float baseMaxHealth = 100f;
    [Range(5f, 500f)] public float baseAttackDamage = 25f;
    [Range(1.5f, 15f)] public float baseMoveSpeed = 5.2f;
    public Color themeColor = new Color(0.92f, 0.28f, 0.58f, 1f);
}

[Serializable]
public class MasterStageConfig
{
    public string nodeCode = "RANK E";
    public string stageTitle = "RANK E : ABANDONED CLASSROOM";
    [TextArea(1, 2)]
    public string stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.";
    [Range(1, 100)] public int normalEnemyCount = 5;
    [Range(0, 20)] public int eliteBossCount = 1;
    [Range(0, 10)] public int energyCost = 1;
}

/// <summary>
/// CENTRAL GAME MASTER CONFIG & TEST HUB (CombatDebugTester — 3-Pillar Progression Edition)
/// - Controls Player Stats, 3 Hub Upgrades (Vitality, Stamina Efficiency, Raw Power), Rank E -> S Stages, First-Clear Bonus, and Enemies.
/// - DOES NOT touch or modify any Area/Arena Bounds, Walls, Prop Colliders, or CharacterController Colliders!
/// - Unity 6.3+ Compatible (uses HashSet<EnemyController> instead of obsolete GetInstanceID).
/// </summary>
[DefaultExecutionOrder(-5000)]
public class CombatDebugTester : MonoBehaviour
{
    public static CombatDebugTester Instance { get; private set; }

    [Header("=== MASTER HUB SWITCHES ===")]
    [Tooltip("Keep this Central Master Config alive across all 4 scenes (MainMenu, The lobby, StageMap, The Action)")]
    [SerializeField] private bool persistAcrossAllScenes = true;
    [Tooltip("Enforce the Master Config values below onto Player, Enemies, Spawner, Lobby, and StageMap (excludes all Area & Collider settings)")]
    [SerializeField] private bool enforceMasterConfig = true;
    [Tooltip("If true, uses Global Player Base Stats below for all characters. If false, uses the per-character stats from Character Roster.")]
    [SerializeField] private bool useSingleGlobalPlayerBaseStats = false;
    [Tooltip("Show the top-left [TEST HUB] button and allow opening the in-game test menu with [~] or [F10]")]
    [SerializeField] private bool enableInGameTestMenu = true;
    [SerializeField] private bool showOnScreenMenu = false;

    public enum TestHubTab
    {
        PlayerAndCheats = 0,
        EnemyAndSpawner = 1,
        ProgressionAndHub = 2,
        CameraAndScenes = 3
    }

    // =========================================================================
    // 1. PLAYER BASE STATS, MOVEMENT, DODGE & STAMINA (NO COLLIDER OVERRIDES)
    // =========================================================================
    [Header("1. Player Base Stats & Movement (Master Override)")]
    [SerializeField] private bool godModeInvincible = false;
    [SerializeField] private bool infiniteStamina = false;
    [SerializeField] private bool oneHitKillEnemies = false;
    [Range(20f, 2000f)]
    [SerializeField] private float globalPlayerBaseMaxHp = 100f;
    [Range(5f, 500f)]
    [SerializeField] private float globalPlayerBaseAttack = 25f;
    [Range(1.5f, 16f)]
    [SerializeField] private float globalPlayerMoveSpeed = 5.2f;
    [Range(0.01f, 0.3f)]
    [SerializeField] private float playerRotationSmoothTime = 0.08f;

    [Header("2. Player Dodge, I-Frame & Base Stamina (Master Override)")]
    [Range(5f, 30f)]
    [SerializeField] private float dodgeSpeed = 14.0f;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float dodgeDuration = 0.35f;
    [Range(0.05f, 1.0f)]
    [SerializeField] private float iFrameDuration = 0.20f;
    [Range(0f, 50f)]
    [SerializeField] private float baseDodgeStaminaCost = 20f;
    [Range(0f, 2.0f)]
    [SerializeField] private float dodgeCooldown = 0.20f;
    [Range(20f, 500f)]
    [SerializeField] private float maxStamina = 100f;
    [Range(5f, 150f)]
    [SerializeField] private float baseStaminaRegenRate = 25f;
    [Range(0f, 3.0f)]
    [SerializeField] private float staminaRegenDelay = 1.0f;

    // =========================================================================
    // 3. PLAYER COMBAT, HIT-STOP, EXECUTION & HURT (MASTER OVERRIDE)
    // =========================================================================
    [Header("3. Player Combat & Execution [F] (Master Override)")]
    [Range(1.0f, 3.5f)]
    [SerializeField] private float heavyFinisherDamageMultiplier = 1.4f;
    [Range(0.05f, 1.5f)]
    [SerializeField] private float playerAttackCooldown = 0.25f;
    [Range(0f, 0.25f)]
    [SerializeField] private float hitStopDuration = 0.05f;
    [Range(0.01f, 1.0f)]
    [SerializeField] private float hitStopTimeScale = 0.1f;
    [Range(0f, 2.0f)]
    [SerializeField] private float heavyComboShakeForce = 0.45f;
    [Range(0f, 1.0f)]
    [SerializeField] private float normalHitShakeForce = 0.08f;
    [Range(1.0f, 6.0f)]
    [SerializeField] private float executionRange = 3.5f;
    [Range(0f, 100f)]
    [SerializeField] private float executionHealPercentage = 35f;
    [Range(0.1f, 2.5f)]
    [SerializeField] private float playerHurtInvincibleDuration = 0.6f;

    // =========================================================================
    // 4. NORMAL ENEMY (GRUNT) MASTER STATS
    // =========================================================================
    [Header("4. Normal Enemy Master Config")]
    [SerializeField] private GameObject gruntPrefab;
    [SerializeField] private GameObject elitePrefab;
    [SerializeField] private float spawnDistanceInFront = 4.0f;
    [SerializeField] private bool freezeAllEnemiesAI = false;

    [Range(10f, 2000f)]
    [SerializeField] private float normalEnemyMaxHealth = 100f;
    [Range(0.5f, 12f)]
    [SerializeField] private float normalEnemyMoveSpeed = 3.5f;
    [Range(1f, 30f)]
    [SerializeField] private float normalEnemyRotationSpeed = 10f;
    [Range(0.8f, 5.0f)]
    [SerializeField] private float normalEnemyAttackRange = 1.9f;
    [Range(1f, 300f)]
    [SerializeField] private float normalEnemyAttackDamage = 12f;
    [Range(0.3f, 5.0f)]
    [SerializeField] private float normalEnemyAttackCooldown = 1.6f;
    [Range(0, 200)]
    [SerializeField] private int normalEnemyStarDustReward = 15;
    [Range(0f, 100f)]
    [SerializeField] private float normalEnemyShatteredCoreChance = 20f;
    [Range(0, 10)]
    [SerializeField] private int normalEnemyShatteredCoreAmount = 1;

    // =========================================================================
    // 5. ELITE / BOSS ENEMY MASTER STATS
    // =========================================================================
    [Header("5. Elite / Boss Enemy Master Config")]
    [SerializeField] private string eliteBossDisplayName = "RANK GUARDIAN (BOSS)";
    [Range(50f, 10000f)]
    [SerializeField] private float eliteBossMaxHealth = 300f;
    [Range(0.5f, 12f)]
    [SerializeField] private float eliteBossMoveSpeed = 4.0f;
    [Range(1f, 30f)]
    [SerializeField] private float eliteBossRotationSpeed = 10f;
    [Range(1.0f, 6.0f)]
    [SerializeField] private float eliteBossAttackRange = 2.2f;
    [Range(1f, 500f)]
    [SerializeField] private float eliteBossAttackDamage = 20f;
    [Range(0.3f, 5.0f)]
    [SerializeField] private float eliteBossAttackCooldown = 1.8f;
    [Range(20f, 1000f)]
    [SerializeField] private float eliteBossMaxStagger = 100f;
    [Range(0.2f, 5.0f)]
    [SerializeField] private float eliteBossStaggerDamageMultiplier = 1.15f;
    [Range(1.0f, 15.0f)]
    [SerializeField] private float eliteBossStunDuration = 5.0f;
    [Range(0, 500)]
    [SerializeField] private int eliteBossStarDustReward = 50;
    [Range(0f, 100f)]
    [SerializeField] private float eliteBossShatteredCoreChance = 100f;
    [Range(0, 10)]
    [SerializeField] private int eliteBossShatteredCoreAmount = 1;
    [Range(0f, 3.0f)]
    [SerializeField] private float enemyDestroyDelayOnDeath = 0f;

    // =========================================================================
    // 6. WAVE SPAWNER TIMING ONLY (DOES NOT TOUCH ARENA AREA OR COLLIDERS!)
    // =========================================================================
    [Header("6. Wave Spawner Timing Config (Excludes Arena Area & Colliders)")]
    [Range(0.3f, 10.0f)]
    [SerializeField] private float trickleSpawnInterval = 2.2f;
    [Range(1, 30)]
    [SerializeField] private int maxConcurrentEnemies = 6;
    [Range(1.0f, 8.0f)]
    [SerializeField] private float stageClearDelay = 4.0f;

    // =========================================================================
    // 7. CAMERA ANTI-CLIPPING (DOES NOT MODIFY SCENE COLLIDERS)
    // =========================================================================
    [Header("7. Camera Anti-Clipping & TimeScale")]
    [SerializeField] private bool enableCameraAntiClip = true;
    [Range(0.8f, 2.4f)]
    [SerializeField] private float cameraPivotHeight = 1.45f;
    [Range(0.08f, 0.6f)]
    [SerializeField] private float cameraCollisionRadius = 0.28f;
    [Range(0.25f, 2.0f)]
    [SerializeField] private float minCameraDistance = 0.55f;
    [Range(0.1f, 3.0f)]
    [SerializeField] private float testTimeScale = 1.0f;

    // =========================================================================
    // 8. THE 3 HUB UPGRADES, FIRST-CLEAR BONUS & PLAY ENERGY
    // =========================================================================
    [Header("8. Play Energy, 3 Hub Upgrades & First-Clear Bonus")]
    [SerializeField] private bool infinitePlayEnergy = false;
    [Range(1, 20)]
    [SerializeField] private int maxPlayEnergy = 5;
    [Range(5f, 600f)]
    [SerializeField] private float energyRechargeSeconds = 60f;
    [Range(1, 200)]
    [SerializeField] private int energyRefillStarDustCost = 20;

    [Tooltip("1. Vitality Boost (Max HP)")]
    [Range(5, 500)]
    [SerializeField] private int baseHpUpgradeStarDustCost = 30;
    [Range(0, 200)]
    [SerializeField] private int hpCostIncreasePerLevel = 15;
    [Range(5f, 200f)]
    [SerializeField] private float bonusMaxHpPerUpgrade = 25f;

    [Tooltip("2. Stamina Efficiency (Regen + Dodge Cost Reduction)")]
    [Range(5, 500)]
    [SerializeField] private int baseStaminaUpgradeStarDustCost = 25;
    [Range(0, 200)]
    [SerializeField] private int staminaCostIncreasePerLevel = 15;
    [Range(1f, 50f)]
    [SerializeField] private float bonusStaminaRegenPerUpgrade = 4.0f;
    [Range(0.2f, 5.0f)]
    [SerializeField] private float bonusDodgeReductionPerUpgrade = 1.5f;

    [Tooltip("3. Raw Power (Base Attack)")]
    [Range(1, 20)]
    [SerializeField] private int baseAttackCoreCost = 1;
    [Range(1f, 100f)]
    [SerializeField] private float bonusAttackPerUpgrade = 5f;

    [Tooltip("First-Clear Milestone Rewards")]
    [Range(10, 1000)]
    [SerializeField] private int baseFirstClearStarDust = 80;
    [Range(0, 500)]
    [SerializeField] private int firstClearStarDustPerStage = 40;
    [Range(0, 20)]
    [SerializeField] private int baseFirstClearCore = 1;

    [Header("9. Character Roster Master Config (Lobby Scene)")]
    [SerializeField] private List<MasterCharacterConfig> masterCharacters = new List<MasterCharacterConfig>();

    [Header("10. Rank E -> S Stage Nodes Master Config (StageMap & Action Scenes)")]
    [SerializeField] private List<MasterStageConfig> masterStages = new List<MasterStageConfig>();

    // Runtime Tracking (Uses object references directly — 100% compatible with Unity 6.3+ without obsolete GetInstanceID)
    private TestHubTab activeTab = TestHubTab.PlayerAndCheats;
    private Vector2 scrollPosition = Vector2.zero;
    private readonly HashSet<EnemyController> configuredEnemies = new HashSet<EnemyController>();
    private const BindingFlags AllInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private void Reset()
    {
        EnsureDefaultMasterLists();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossAllScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureDefaultMasterLists();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (enforceMasterConfig)
        {
            EnforceMasterConfigOnCurrentScene(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        configuredEnemies.Clear();
        if (enforceMasterConfig)
        {
            EnforceMasterConfigOnCurrentScene(true);
        }
    }

    private void Start()
    {
        if (enforceMasterConfig)
        {
            EnforceMasterConfigOnCurrentScene(true);
        }
    }

    private void Update()
    {
        HandleHotkeys();
        EnforceContinuousCheats();

        if (enforceMasterConfig)
        {
            EnforceMasterConfigOnNewEnemies();
        }
    }

    private void OnValidate()
    {
        EnsureDefaultMasterLists();
        if (Application.isPlaying && enforceMasterConfig)
        {
            EnforceMasterConfigOnCurrentScene(false);
            ForceRefreshAllLivingEnemies();
        }
    }

    private void HandleHotkeys()
    {
        if (!enableInGameTestMenu) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        if (Keyboard.current.backquoteKey.wasPressedThisFrame || Keyboard.current.f10Key.wasPressedThisFrame)
        {
            showOnScreenMenu = !showOnScreenMenu;
            if (showOnScreenMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame) SpawnEnemy(gruntPrefab, false);
        if (Keyboard.current.f2Key.wasPressedThisFrame) SpawnEnemy(elitePrefab != null ? elitePrefab : gruntPrefab, true);
        if (Keyboard.current.f3Key.wasPressedThisFrame) InstantStunNearestEnemy();
        if (Keyboard.current.f4Key.wasPressedThisFrame) KillAllEnemies();
        if (Keyboard.current.f5Key.wasPressedThisFrame) FullHealPlayer();
#endif
    }

    private void EnforceContinuousCheats()
    {
        if (showOnScreenMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
        {
            movement.SetExternalInvincible(godModeInvincible);
        }

        var health = FindAnyObjectByType<PlayerHealth>();
        if (godModeInvincible && health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth)
        {
            health.HealPercentage(100f);
        }

        var stamina = FindAnyObjectByType<PlayerStamina>();
        if (infiniteStamina && stamina != null)
        {
            stamina.RestoreFullStamina();
        }

        if (infinitePlayEnergy && GameProgressionManager.Instance != null)
        {
            if (GameProgressionManager.Instance.CurrentEnergy < GameProgressionManager.Instance.MaxEnergy)
            {
                GameProgressionManager.Instance.RefillFullEnergy();
            }
        }

        EnemyController.GlobalExecutionFreeze = freezeAllEnemiesAI;
    }

    private void EnsureDefaultMasterLists()
    {
        if (masterCharacters == null || masterCharacters.Count == 0)
        {
            masterCharacters = new List<MasterCharacterConfig>
            {
                new MasterCharacterConfig
                {
                    characterId = "CHAR_01",
                    characterName = "01 : HIKARI - BRAWLER STRIKER",
                    characterRoleAndLore = "Balanced Striker | High mobility and fast melee combos.",
                    baseMaxHealth = 100f,
                    baseAttackDamage = 25f,
                    baseMoveSpeed = 5.2f,
                    themeColor = new Color(0.92f, 0.28f, 0.58f, 1f)
                },
                new MasterCharacterConfig
                {
                    characterId = "CHAR_02",
                    characterName = "02 : AKANE - CRIMSON BREAKER",
                    characterRoleAndLore = "Heavy Breaker | Crushes enemy stagger gauges with heavy blows.",
                    baseMaxHealth = 90f,
                    baseAttackDamage = 34f,
                    baseMoveSpeed = 5.0f,
                    themeColor = new Color(0.90f, 0.24f, 0.28f, 1f)
                },
                new MasterCharacterConfig
                {
                    characterId = "CHAR_03",
                    characterName = "03 : SHIRO - AEGIS GUARDIAN",
                    characterRoleAndLore = "Aegis Tank | High base health built for surviving relentless swarms.",
                    baseMaxHealth = 140f,
                    baseAttackDamage = 22f,
                    baseMoveSpeed = 4.7f,
                    themeColor = new Color(0.28f, 0.52f, 0.92f, 1f)
                }
            };
        }

        if (masterStages == null || masterStages.Count == 0)
        {
            masterStages = new List<MasterStageConfig>
            {
                new MasterStageConfig
                {
                    nodeCode = "RANK E",
                    stageTitle = "RANK E : ABANDONED CLASSROOM",
                    stageDescription = "Trapped in a derelict classroom. Break the static seal to escape outside.",
                    normalEnemyCount = 5,
                    eliteBossCount = 1,
                    energyCost = 1
                },
                new MasterStageConfig
                {
                    nodeCode = "RANK D",
                    stageTitle = "RANK D : DERELICT CITY STREET",
                    stageDescription = "Ruined city streets beneath a blood-red sky.",
                    normalEnemyCount = 7,
                    eliteBossCount = 1,
                    energyCost = 1
                },
                new MasterStageConfig
                {
                    nodeCode = "RANK C",
                    stageTitle = "RANK C : SHATTERED SUBWAY LINE",
                    stageDescription = "Underground tracks echoing with the whispers of fallen Magical Girls.",
                    normalEnemyCount = 9,
                    eliteBossCount = 1,
                    energyCost = 1
                },
                new MasterStageConfig
                {
                    nodeCode = "RANK B",
                    stageTitle = "RANK B : FACELESS PLAZA",
                    stageDescription = "Collapsed commercial center swarming with aggressive entities.",
                    normalEnemyCount = 11,
                    eliteBossCount = 1,
                    energyCost = 1
                },
                new MasterStageConfig
                {
                    nodeCode = "RANK A",
                    stageTitle = "RANK A : CRIMSON SANCTUM",
                    stageDescription = "Deep dimensional corridor guarded by Twin Elite Entities.",
                    normalEnemyCount = 12,
                    eliteBossCount = 2,
                    energyCost = 1
                },
                new MasterStageConfig
                {
                    nodeCode = "RANK S",
                    stageTitle = "RANK S : CORE OF THE ABYSS [FINAL]",
                    stageDescription = "The heart of the dimensional loop where the Abyss Sovereign awaits.",
                    normalEnemyCount = 15,
                    eliteBossCount = 2,
                    energyCost = 2
                }
            };
        }
    }

    // =========================================================================
    // MASTER ENFORCEMENT LOGIC (EXCLUDES ALL AREA / COLLIDER SETTINGS)
    // =========================================================================

    [ContextMenu("Force Apply Master Config To Current Scene Now")]
    public void ForceApplyFromContextMenu()
    {
        EnforceMasterConfigOnCurrentScene(true);
        ForceRefreshAllLivingEnemies();
    }

    public void EnforceMasterConfigOnCurrentScene(bool resetHealthOnSceneStart)
    {
        EnsureDefaultMasterLists();

        // 1. Enforce GameProgressionManager Master Settings
        var prog = GameProgressionManager.Instance;
        if (prog != null)
        {
            SetField(prog, "maxEnergy", maxPlayEnergy);
            SetField(prog, "energyRechargeSeconds", energyRechargeSeconds);
            SetField(prog, "baseFirstClearStarDust", baseFirstClearStarDust);
            SetField(prog, "firstClearStarDustPerStage", firstClearStarDustPerStage);
            SetField(prog, "baseFirstClearCore", baseFirstClearCore);

            int charIdx = Mathf.Clamp(prog.SelectedCharacterIndex, 0, masterCharacters.Count - 1);
            MasterCharacterConfig charCfg = masterCharacters[charIdx];

            float baseHp = useSingleGlobalPlayerBaseStats ? globalPlayerBaseMaxHp : charCfg.baseMaxHealth;
            float baseAtk = useSingleGlobalPlayerBaseStats ? globalPlayerBaseAttack : charCfg.baseAttackDamage;
            float moveSpd = useSingleGlobalPlayerBaseStats ? globalPlayerMoveSpeed : charCfg.baseMoveSpeed;

            prog.SelectCharacter(charIdx, charCfg.characterName, baseHp, baseAtk, moveSpd);

            int stageIdx = Mathf.Clamp(prog.SelectedStageIndex, 0, masterStages.Count - 1);
            MasterStageConfig stageCfg = masterStages[stageIdx];
            prog.SelectStage(stageIdx, stageCfg.stageTitle, stageCfg.normalEnemyCount, stageCfg.eliteBossCount, stageCfg.energyCost);
        }

        // 2. Enforce LobbyManager Settings (if in Lobby Scene)
        var lobby = FindAnyObjectByType<LobbyManager>();
        if (lobby != null)
        {
            SetField(lobby, "energyRefillStarDustCost", energyRefillStarDustCost);
            SetField(lobby, "baseHpUpgradeCost", baseHpUpgradeStarDustCost);
            SetField(lobby, "hpCostIncreasePerLevel", hpCostIncreasePerLevel);
            SetField(lobby, "bonusMaxHpPerUpgrade", bonusMaxHpPerUpgrade);
            SetField(lobby, "baseStaminaUpgradeCost", baseStaminaUpgradeStarDustCost);
            SetField(lobby, "staminaCostIncreasePerLevel", staminaCostIncreasePerLevel);
            SetField(lobby, "bonusStaminaRegenPerUpgrade", bonusStaminaRegenPerUpgrade);
            SetField(lobby, "bonusDodgeReductionPerUpgrade", bonusDodgeReductionPerUpgrade);
            SetField(lobby, "baseAttackCoreCost", baseAttackCoreCost);
            SetField(lobby, "bonusAttackPerUpgrade", bonusAttackPerUpgrade);

            var lobbyChars = GetField<List<CharacterProfileData>>(lobby, "characters");
            if (lobbyChars != null)
            {
                int count = Mathf.Min(lobbyChars.Count, masterCharacters.Count);
                for (int i = 0; i < count; i++)
                {
                    lobbyChars[i].characterId = masterCharacters[i].characterId;
                    lobbyChars[i].characterName = masterCharacters[i].characterName;
                    lobbyChars[i].characterRoleAndLore = masterCharacters[i].characterRoleAndLore;
                    lobbyChars[i].baseMaxHealth = useSingleGlobalPlayerBaseStats ? globalPlayerBaseMaxHp : masterCharacters[i].baseMaxHealth;
                    lobbyChars[i].baseAttackDamage = useSingleGlobalPlayerBaseStats ? globalPlayerBaseAttack : masterCharacters[i].baseAttackDamage;
                    lobbyChars[i].baseMoveSpeed = useSingleGlobalPlayerBaseStats ? globalPlayerMoveSpeed : masterCharacters[i].baseMoveSpeed;
                    lobbyChars[i].themeColor = masterCharacters[i].themeColor;
                }
            }

            int savedCharIdx = prog != null ? Mathf.Clamp(prog.SelectedCharacterIndex, 0, masterCharacters.Count - 1) : 0;
            lobby.SelectCharacterByIndex(savedCharIdx);
            lobby.RefreshHubUI();
        }

        // 3. Enforce StageMapManager Settings (if in StageMap Scene)
        var stageMap = FindAnyObjectByType<StageMapManager>();
        if (stageMap != null)
        {
            var nodes = GetField<List<StageNodeData>>(stageMap, "stageNodes");
            if (nodes != null)
            {
                int count = Mathf.Min(nodes.Count, masterStages.Count);
                for (int i = 0; i < count; i++)
                {
                    nodes[i].nodeCode = masterStages[i].nodeCode;
                    nodes[i].stageTitle = masterStages[i].stageTitle;
                    nodes[i].stageDescription = masterStages[i].stageDescription;
                    nodes[i].normalEnemyCount = masterStages[i].normalEnemyCount;
                    nodes[i].eliteBossCount = masterStages[i].eliteBossCount;
                    nodes[i].energyCost = masterStages[i].energyCost;
                }
            }
            stageMap.SendMessage("RefreshHeaderStatus", SendMessageOptions.DontRequireReceiver);
            stageMap.SendMessage("SelectStageNode", prog != null ? prog.SelectedStageIndex : 0, SendMessageOptions.DontRequireReceiver);
        }

        // 4. Enforce Player Components (Stats & Camera Anti-Clip ONLY — NEVER touches CharacterController Collider!)
        EnforceMasterConfigOnPlayer(resetHealthOnSceneStart);

        // 5. Enforce WaveSpawner Timing & Enemy Prefabs ONLY (NEVER touches Arena Bounds or Prop Colliders!)
        EnforceMasterConfigOnWaveSpawner();
    }

    private void EnforceMasterConfigOnPlayer(bool resetHealthToFull)
    {
        var prog = GameProgressionManager.Instance;
        int charIdx = prog != null ? Mathf.Clamp(prog.SelectedCharacterIndex, 0, masterCharacters.Count - 1) : 0;
        MasterCharacterConfig charCfg = masterCharacters[charIdx];

        float baseHp = useSingleGlobalPlayerBaseStats ? globalPlayerBaseMaxHp : charCfg.baseMaxHealth;
        float baseAtk = useSingleGlobalPlayerBaseStats ? globalPlayerBaseAttack : charCfg.baseAttackDamage;
        float effectiveMoveSpeed = useSingleGlobalPlayerBaseStats ? globalPlayerMoveSpeed : charCfg.baseMoveSpeed;

        float bonusHp = prog != null ? prog.BonusMaxHealth : 0f;
        float bonusAtk = prog != null ? prog.BonusAttackDamage : 0f;
        float bonusStamRegen = prog != null ? prog.BonusStaminaRegen : 0f;
        float bonusDodgeReduce = prog != null ? prog.BonusDodgeCostReduction : 0f;

        float totalMaxHp = baseHp + bonusHp;
        float totalAttackDamage = oneHitKillEnemies ? 99999f : (baseAtk + bonusAtk);
        float effectiveDodgeCost = infiniteStamina ? 0f : Mathf.Max(5f, baseDodgeStaminaCost - bonusDodgeReduce);
        float effectiveStaminaRegen = baseStaminaRegenRate + bonusStamRegen;

        // PlayerMovement (Does NOT overwrite CharacterController height/center/stepOffset/slopeLimit)
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
        {
            SetField(movement, "moveSpeed", effectiveMoveSpeed);
            SetField(movement, "rotationSmoothTime", playerRotationSmoothTime);
            SetField(movement, "dodgeSpeed", dodgeSpeed);
            SetField(movement, "dodgeDuration", dodgeDuration);
            SetField(movement, "iFrameDuration", iFrameDuration);
            SetField(movement, "dodgeStaminaCost", effectiveDodgeCost);
            SetField(movement, "dodgeCooldown", dodgeCooldown);
            SetField(movement, "enableCameraAntiClip", enableCameraAntiClip);
            SetField(movement, "cameraPivotHeight", cameraPivotHeight);
            SetField(movement, "cameraCollisionRadius", cameraCollisionRadius);
            SetField(movement, "minCameraDistance", minCameraDistance);
            movement.SetExternalInvincible(godModeInvincible);
        }

        // PlayerHealth
        var health = FindAnyObjectByType<PlayerHealth>();
        if (health != null)
        {
            SetField(health, "baseMaxHealth", baseHp);
            SetField(health, "effectiveMaxHealth", totalMaxHp);
            SetField(health, "hurtInvincibleDuration", playerHurtInvincibleDuration);

            if (resetHealthToFull || health.CurrentHealth > totalMaxHp)
            {
                health.HealPercentage(100f);
            }
        }

        // PlayerStamina
        var stamina = FindAnyObjectByType<PlayerStamina>();
        if (stamina != null)
        {
            SetField(stamina, "maxStamina", maxStamina);
            SetField(stamina, "regenRate", effectiveStaminaRegen);
            SetField(stamina, "regenDelay", staminaRegenDelay);
            if (resetHealthToFull || infiniteStamina)
            {
                stamina.RestoreFullStamina();
            }
        }

        // PlayerCombat
        var combat = FindAnyObjectByType<PlayerCombat>();
        if (combat != null)
        {
            SetField(combat, "damage", totalAttackDamage);
            SetField(combat, "heavyFinisherDamageMultiplier", heavyFinisherDamageMultiplier);
            SetField(combat, "attackCooldown", playerAttackCooldown);
            SetField(combat, "hitStopDuration", hitStopDuration);
            SetField(combat, "hitStopTimeScale", hitStopTimeScale);
            SetField(combat, "heavyComboShakeForce", heavyComboShakeForce);
            SetField(combat, "normalHitShakeForce", normalHitShakeForce);
        }

        // PlayerExecution
        var execution = FindAnyObjectByType<PlayerExecution>();
        if (execution != null)
        {
            SetField(execution, "executionRange", executionRange);
            SetField(execution, "healPercentageOnExecute", executionHealPercentage);
        }
    }

    private void EnforceMasterConfigOnWaveSpawner()
    {
        var spawner = FindAnyObjectByType<WaveSpawner>();
        if (spawner == null) return;

        SetField(spawner, "trickleSpawnInterval", trickleSpawnInterval);
        SetField(spawner, "maxConcurrentEnemies", maxConcurrentEnemies);
        SetField(spawner, "stageClearDelay", stageClearDelay);

        if (gruntPrefab == null) gruntPrefab = GetField<GameObject>(spawner, "NormalPrefab");
        if (elitePrefab == null) elitePrefab = GetField<GameObject>(spawner, "elitePrefab");

        if (gruntPrefab != null && gruntPrefab.TryGetComponent<EnemyController>(out var normalEc))
        {
            ApplyMasterStatsToEnemy(normalEc, false, false);
        }

        if (elitePrefab != null && elitePrefab.TryGetComponent<EnemyController>(out var eliteEc))
        {
            ApplyMasterStatsToEnemy(eliteEc, true, false);
        }
    }

    private void EnforceMasterConfigOnNewEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController ec = enemies[i];
            if (ec == null || ec.IsDead) continue;

            if (!configuredEnemies.Contains(ec))
            {
                configuredEnemies.Add(ec);
                ApplyMasterStatsToEnemy(ec, ec.IsEliteOrBoss, true);
            }
        }
    }

    private void ForceRefreshAllLivingEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController ec = enemies[i];
            if (ec == null || ec.IsDead) continue;
            ApplyMasterStatsToEnemy(ec, ec.IsEliteOrBoss, false);
        }
    }

    private void ApplyMasterStatsToEnemy(EnemyController ec, bool isEliteOrBoss, bool resetFullHpAndStagger)
    {
        if (ec == null) return;

        SetField(ec, "destroyDelayOnDeath", enemyDestroyDelayOnDeath);

        if (isEliteOrBoss)
        {
            SetField(ec, "enemyDisplayName", eliteBossDisplayName);
            SetField(ec, "maxHealth", eliteBossMaxHealth);
            SetField(ec, "moveSpeed", eliteBossMoveSpeed);
            SetField(ec, "rotationSpeed", eliteBossRotationSpeed);
            SetField(ec, "attackRange", eliteBossAttackRange);
            SetField(ec, "attackDamage", eliteBossAttackDamage);
            SetField(ec, "attackCooldown", eliteBossAttackCooldown);
            SetField(ec, "maxStagger", eliteBossMaxStagger);
            SetField(ec, "staggerDamageMultiplier", eliteBossStaggerDamageMultiplier);
            SetField(ec, "stunDuration", eliteBossStunDuration);
            SetField(ec, "starDustReward", eliteBossStarDustReward);
            SetField(ec, "shatteredCoreDropChance", eliteBossShatteredCoreChance);
            SetField(ec, "shatteredCoreDropAmount", eliteBossShatteredCoreAmount);

            if (resetFullHpAndStagger)
            {
                SetField(ec, "currentHealth", eliteBossMaxHealth);
                SetField(ec, "currentStagger", eliteBossMaxStagger);
                if (BattleHUD.Instance != null)
                {
                    BattleHUD.Instance.ShowBossBar(eliteBossDisplayName, eliteBossMaxHealth, eliteBossMaxHealth, eliteBossMaxStagger, eliteBossMaxStagger);
                }
            }
        }
        else
        {
            SetField(ec, "maxHealth", normalEnemyMaxHealth);
            SetField(ec, "moveSpeed", normalEnemyMoveSpeed);
            SetField(ec, "rotationSpeed", normalEnemyRotationSpeed);
            SetField(ec, "attackRange", normalEnemyAttackRange);
            SetField(ec, "attackDamage", normalEnemyAttackDamage);
            SetField(ec, "attackCooldown", normalEnemyAttackCooldown);
            SetField(ec, "starDustReward", normalEnemyStarDustReward);
            SetField(ec, "shatteredCoreDropChance", normalEnemyShatteredCoreChance);
            SetField(ec, "shatteredCoreDropAmount", normalEnemyShatteredCoreAmount);

            if (resetFullHpAndStagger)
            {
                SetField(ec, "currentHealth", normalEnemyMaxHealth);
            }
        }
    }

    // =========================================================================
    // QUICK SPAWN, COMBAT & SCENE ACTIONS FOR IN-GAME TEST MENU
    // =========================================================================

    public void SpawnEnemy(GameObject prefab, bool asEliteBoss = false)
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement == null) return;

        var spawner = FindAnyObjectByType<WaveSpawner>();
        if (prefab == null && spawner != null)
        {
            prefab = asEliteBoss
                ? (GetField<GameObject>(spawner, "elitePrefab") ?? GetField<GameObject>(spawner, "NormalPrefab"))
                : GetField<GameObject>(spawner, "NormalPrefab");
        }
        if (prefab == null) return;

        Transform pTrans = movement.transform;
        Vector3 spawnPos = pTrans.position + (pTrans.forward * spawnDistanceInFront);
        spawnPos.y = pTrans.position.y;

        GameObject go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(-pTrans.forward));
        if (go.TryGetComponent<EnemyController>(out var ec))
        {
            if (asEliteBoss)
            {
                ec.ConfigureAsEliteOrBoss(true, eliteBossDisplayName);
            }
            configuredEnemies.Add(ec);
            ApplyMasterStatsToEnemy(ec, asEliteBoss || ec.IsEliteOrBoss, true);
        }
    }

    public void InstantStunNearestEnemy()
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement == null) return;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        EnemyController nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            float dist = Vector3.Distance(movement.transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }

        if (nearest != null)
        {
            if (!nearest.IsEliteOrBoss)
                nearest.ConfigureAsEliteOrBoss(true, eliteBossDisplayName);
            nearest.TriggerStun();
        }
    }

    public void KillAllEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
                enemy.ExecuteKill();
        }
    }

    public void FullHealPlayer()
    {
        var health = FindAnyObjectByType<PlayerHealth>();
        if (health != null) health.HealPercentage(100f);

        var stamina = FindAnyObjectByType<PlayerStamina>();
        if (stamina != null) stamina.RestoreFullStamina();
    }

    // =========================================================================
    // ON-SCREEN TEST HUB UI ([~] OR [F10])
    // =========================================================================

    private void OnGUI()
    {
        if (!enableInGameTestMenu) return;

        GUI.backgroundColor = showOnScreenMenu ? new Color(0.2f, 0.9f, 0.5f) : new Color(0.15f, 0.15f, 0.22f, 0.9f);
        if (GUI.Button(new Rect(14, 12, 235, 30), showOnScreenMenu ? "[-] CLOSE MASTER HUB [~ / F10]" : "[+] OPEN MASTER HUB [~ / F10]"))
        {
            showOnScreenMenu = !showOnScreenMenu;
        }
        GUI.backgroundColor = Color.white;

        if (!showOnScreenMenu) return;

        float winWidth = Mathf.Min(500f, Screen.width - 28f);
        float winHeight = Mathf.Min(620f, Screen.height - 60f);

        GUILayout.BeginArea(new Rect(14, 48, winWidth, winHeight), "CENTRAL MASTER CONFIG & PROGRESSION HUB", GUI.skin.window);
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        DrawTabButton(TestHubTab.PlayerAndCheats, "1. PLAYER");
        DrawTabButton(TestHubTab.EnemyAndSpawner, "2. ENEMY/WAVE");
        DrawTabButton(TestHubTab.ProgressionAndHub, "3. RANK/HUB");
        DrawTabButton(TestHubTab.CameraAndScenes, "4. CAM/SCENES");
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        switch (activeTab)
        {
            case TestHubTab.PlayerAndCheats:
                DrawPlayerTab();
                break;
            case TestHubTab.EnemyAndSpawner:
                DrawEnemyTab();
                break;
            case TestHubTab.ProgressionAndHub:
                DrawProgressionAndHubTab();
                break;
            case TestHubTab.CameraAndScenes:
                DrawCameraAndScenesTab();
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawTabButton(TestHubTab tab, string label)
    {
        GUI.backgroundColor = (activeTab == tab) ? Color.cyan : new Color(0.75f, 0.75f, 0.75f);
        if (GUILayout.Button(label, GUILayout.Height(28)))
        {
            activeTab = tab;
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawPlayerTab()
    {
        GUILayout.Label("<b>[CHEAT TOGGLES]</b>");
        godModeInvincible = GUILayout.Toggle(godModeInvincible, " GOD MODE (Invincible)");
        infiniteStamina = GUILayout.Toggle(infiniteStamina, " INFINITE STAMINA");
        oneHitKillEnemies = GUILayout.Toggle(oneHitKillEnemies, " ONE-HIT KILL ENEMIES");
        useSingleGlobalPlayerBaseStats = GUILayout.Toggle(useSingleGlobalPlayerBaseStats, " Override All Characters With Global Stats Below");

        GUILayout.Space(6);
        if (GUILayout.Button("[F5] Full Heal HP & Stamina")) FullHealPlayer();

        GUILayout.Space(6);
        GUILayout.Label("<b>[MASTER PLAYER STATS]</b>");
        globalPlayerBaseMaxHp = DrawSliderRow("Base Max HP", globalPlayerBaseMaxHp, 20f, 1500f, "F0");
        globalPlayerBaseAttack = DrawSliderRow("Base Attack", globalPlayerBaseAttack, 5f, 400f, "F0");
        heavyFinisherDamageMultiplier = DrawSliderRow("Heavy Combo Mult", heavyFinisherDamageMultiplier, 1.0f, 3.0f, "F2");
        globalPlayerMoveSpeed = DrawSliderRow("Move Speed", globalPlayerMoveSpeed, 1.5f, 15f, "F1");
        dodgeSpeed = DrawSliderRow("Dodge Speed", dodgeSpeed, 5f, 28f, "F1");
        baseDodgeStaminaCost = DrawSliderRow("Base Dodge Cost", baseDodgeStaminaCost, 0f, 50f, "F0");
        baseStaminaRegenRate = DrawSliderRow("Base Stam Regen /s", baseStaminaRegenRate, 5f, 100f, "F0");

        EnforceMasterConfigOnPlayer(false);
    }

    private void DrawEnemyTab()
    {
        GUILayout.Label("<b>[QUICK SPAWN & COMBAT]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F1] Spawn Normal")) SpawnEnemy(gruntPrefab, false);
        if (GUILayout.Button("[F2] Spawn Elite Boss")) SpawnEnemy(elitePrefab != null ? elitePrefab : gruntPrefab, true);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F3] Stun Boss (Test F)")) InstantStunNearestEnemy();
        if (GUILayout.Button("[F4] Kill All Enemies")) KillAllEnemies();
        GUILayout.EndHorizontal();

        freezeAllEnemiesAI = GUILayout.Toggle(freezeAllEnemiesAI, " Freeze All Enemy AI");

        GUILayout.Space(6);
        GUILayout.Label("<b>[NORMAL ENEMY MASTER STATS]</b>");
        normalEnemyMaxHealth = DrawSliderRow("Normal Max HP", normalEnemyMaxHealth, 10f, 1000f, "F0");
        normalEnemyMoveSpeed = DrawSliderRow("Normal Speed", normalEnemyMoveSpeed, 0.5f, 10f, "F1");
        normalEnemyAttackDamage = DrawSliderRow("Normal Damage", normalEnemyAttackDamage, 1f, 150f, "F0");
        normalEnemyAttackRange = DrawSliderRow("Normal Atk Range", normalEnemyAttackRange, 0.8f, 4.5f, "F1");

        GUILayout.Space(6);
        GUILayout.Label("<b>[ELITE BOSS MASTER STATS]</b>");
        eliteBossMaxHealth = DrawSliderRow("Boss Max HP", eliteBossMaxHealth, 50f, 5000f, "F0");
        eliteBossMoveSpeed = DrawSliderRow("Boss Speed", eliteBossMoveSpeed, 0.5f, 10f, "F1");
        eliteBossAttackDamage = DrawSliderRow("Boss Damage", eliteBossAttackDamage, 1f, 250f, "F0");
        eliteBossMaxStagger = DrawSliderRow("Boss Max Stagger", eliteBossMaxStagger, 20f, 500f, "F0");

        GUILayout.Space(6);
        GUILayout.Label("<b>[WAVE SPAWNER TIMING]</b>");
        trickleSpawnInterval = DrawSliderRow("Spawn Interval (s)", trickleSpawnInterval, 0.3f, 8.0f, "F1");
        maxConcurrentEnemies = Mathf.RoundToInt(DrawSliderRow("Max Active Enemies", maxConcurrentEnemies, 1f, 25f, "F0"));

        EnforceMasterConfigOnWaveSpawner();
        ForceRefreshAllLivingEnemies();
    }

    private void DrawProgressionAndHubTab()
    {
        var prog = GameProgressionManager.Instance;
        GUILayout.Box(
            $"<b>RANK {prog.CurrentRankLetter} ({prog.CurrentRankZoneName})</b>\n" +
            $"Star Dust: {prog.StarDust}  |  Core: {prog.ShatteredCore}  |  Energy: {prog.CurrentEnergy}/{prog.MaxEnergy}\n" +
            $"1. Vitality Lv.{prog.HpUpgradeLevel}  |  2. Stamina Lv.{prog.StaminaUpgradeLevel}  |  3. Power Lv.{prog.AttackUpgradeLevel}\n" +
            $"Total Kills: {prog.TotalEnemiesKilled}  |  Milestone Tier: {prog.GetCurrentMilestoneTier() + 1}/4",
            GUILayout.ExpandWidth(true));

        infinitePlayEnergy = GUILayout.Toggle(infinitePlayEnergy, " INFINITE PLAY ENERGY");

        GUILayout.Space(4);
        GUILayout.Label("<b>[THE 3 HUB UPGRADES (FREE TEST BOOST)]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 Vitality (HP)"))
        {
            prog.AddStarDust(500);
            prog.TryUpgradeMaxHealth(0, bonusMaxHpPerUpgrade);
            EnforceMasterConfigOnCurrentScene(false);
        }
        if (GUILayout.Button("+1 Stamina Eff."))
        {
            prog.AddStarDust(500);
            prog.TryUpgradeStaminaEfficiency(0, bonusStaminaRegenPerUpgrade, bonusDodgeReductionPerUpgrade);
            EnforceMasterConfigOnCurrentScene(false);
        }
        if (GUILayout.Button("+1 Raw Power"))
        {
            prog.AddShatteredCore(5);
            prog.TryUpgradeAttack(0, bonusAttackPerUpgrade);
            EnforceMasterConfigOnCurrentScene(false);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("<b>[CURRENCY, RANK (E -> S) & MILESTONES]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+200 Star Dust")) { prog.AddStarDust(200); EnforceMasterConfigOnCurrentScene(false); }
        if (GUILayout.Button("+5 Shattered Core")) { prog.AddShatteredCore(5); EnforceMasterConfigOnCurrentScene(false); }
        if (GUILayout.Button("+25 Kills (Milestone)"))
        {
            for (int i = 0; i < 25; i++) prog.RegisterEnemyKill(false);
            EnforceMasterConfigOnCurrentScene(false);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refill Full Energy")) prog.RefillFullEnergy();
        if (GUILayout.Button("Unlock All Ranks (E->S)"))
        {
            PlayerPrefs.SetInt(GameProgressionManager.HighestStagePrefKey, 5);
            PlayerPrefs.Save();
            prog.LoadFromPrefs();
            EnforceMasterConfigOnCurrentScene(false);
        }
        if (GUILayout.Button("Reset Save Data"))
        {
            prog.ResetAllSaveData();
            EnforceMasterConfigOnCurrentScene(false);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawCameraAndScenesTab()
    {
        GUILayout.Label("<b>[GAME SPEED / TIMESCALE]</b>");
        float newScale = DrawSliderRow("Time.timeScale", testTimeScale, 0.1f, 3.0f, "F2");
        if (Mathf.Abs(newScale - testTimeScale) > 0.01f)
        {
            testTimeScale = newScale;
            Time.timeScale = testTimeScale;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.25x")) { testTimeScale = 0.25f; Time.timeScale = 0.25f; }
        if (GUILayout.Button("0.5x")) { testTimeScale = 0.5f; Time.timeScale = 0.5f; }
        if (GUILayout.Button("1.0x")) { testTimeScale = 1.0f; Time.timeScale = 1.0f; }
        if (GUILayout.Button("2.0x")) { testTimeScale = 2.0f; Time.timeScale = 2.0f; }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("<b>[CAMERA ANTI-CLIPPING]</b>");
        enableCameraAntiClip = GUILayout.Toggle(enableCameraAntiClip, " Enable Camera Anti-Clipping");
        cameraPivotHeight = DrawSliderRow("Pivot Height (m)", cameraPivotHeight, 0.8f, 2.2f, "F2");
        cameraCollisionRadius = DrawSliderRow("Sphere Radius (m)", cameraCollisionRadius, 0.08f, 0.6f, "F2");
        minCameraDistance = DrawSliderRow("Min Cam Dist (m)", minCameraDistance, 0.25f, 2.0f, "F2");

        GUILayout.Space(8);
        GUILayout.Label("<b>[INSTANT SCENE SWITCHER]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1. MainMenu")) { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
        if (GUILayout.Button("2. The lobby")) { Time.timeScale = 1f; SceneManager.LoadScene("The lobby"); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3. StageMap")) { Time.timeScale = 1f; SceneManager.LoadScene("StageMap"); }
        if (GUILayout.Button("4. The Action")) { Time.timeScale = 1f; SceneManager.LoadScene("The Action"); }
        GUILayout.EndHorizontal();
    }

    private float DrawSliderRow(string label, float value, float min, float max, string format)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: <b>{value.ToString(format)}</b>", GUILayout.Width(175));
        float result = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.EndHorizontal();
        return result;
    }

    // =========================================================================
    // REFLECTION HELPERS
    // =========================================================================

    private static T GetField<T>(object target, string fieldName, T fallback = default)
    {
        if (target == null) return fallback;
        FieldInfo fi = target.GetType().GetField(fieldName, AllInstanceFlags);
        if (fi != null && fi.GetValue(target) is T val)
            return val;
        return fallback;
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        if (target == null) return;
        FieldInfo fi = target.GetType().GetField(fieldName, AllInstanceFlags);
        if (fi != null)
        {
            fi.SetValue(target, value);
        }
    }
}


/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน CombatDebugTester.cs
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. Awake(), OnSceneLoaded() & EnforceMasterConfigOnCurrentScene()
 *    - ทำงานเป็นอันดับแรกสุดของเกม ([DefaultExecutionOrder(-5000)]) และอยู่ข้ามทุกฉากด้วย DontDestroyOnLoad
 *    - เมื่อเปิด enforceMasterConfig = true จะทำหน้าที่เป็นศูนย์กลางสมดุลเกม (Central Master Config)
 *      ที่ส่งค่าสเตตัสจาก Inspector นี้ไปทับสคริปต์อื่นๆ ในทุกฉากโดยไม่ยุ่งกับพื้นที่หรือ Collider ในฉาก
 *
 * 2. EnforceMasterConfigOnPlayer()
 *    - คำนวณค่าเลือดสูงสุด (Base HP + Vitality Boost), ดาเมจ (Base ATK + Raw Power),
 *      และค่า Stamina (Base Regen/Dodge Cost + Stamina Efficiency) แล้วส่งไปตั้งค่าใน
 *      PlayerMovement, PlayerHealth, PlayerStamina, PlayerCombat, และ PlayerExecution
 *
 * 3. EnforceMasterConfigOnWaveSpawner() & EnforceMasterConfigOnNewEnemies()
 *    - ตั้งค่าความถี่การเกิดของมอนสเตอร์ (trickleSpawnInterval) และจำนวนศัตรูสูงสุดในฉาก (maxConcurrentEnemies)
 *    - ตรวจจับมอนสเตอร์และบอสทุกตัวที่เพิ่งเกิดในฉาก (ผ่าน HashSet<EnemyController> ที่รองรับ Unity 6.3)
 *      แล้วยัดค่าเลือด ความเร็ว ดาเมจ ระยะตี เกราะ Stagger และอัตราดรอปแร่จากศูนย์กลางนี้ทับทันที
 *
 * 4. OnGUI() & HandleHotkeys()
 *    - แสดงปุ่มเปิด/ปิดแผงควบคุม Master Hub มุมซ้ายบน (หรือกดปุ่ม ~ / F10 บนคีย์บอร์ด)
 *    - มี 4 แท็บสำหรับเปิดโหมดอมตะ (God Mode), ตีทีเดียวตาย (One-Hit Kill), เสกมอนสเตอร์/บอส [F1-F4],
 *      ปั๊มแร่/อัปเกรด 3 สาย, ปลดล็อก Rank E->S, ปรับความเร็วเกม Time.timeScale, และวาร์ปข้ามทั้ง 4 ฉากได้ทันที
 * ============================================================================ */