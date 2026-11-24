using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CameraSystem: Persistent Cross-Stage Player Camera Rig (DontDestroyOnLoad Singleton).
/// - Survives across all combat stages without being destroyed.
/// - Automatically sleeps (disables RigContent) in non-combat UI scenes (MainMenu, The Lobby, StageMap).
/// - Automatically disables duplicate scene-local Cameras, AudioListeners, and CinemachineCameras in combat stages.
/// - Dynamically binds to any newly spawned character (existing or future) via event, explicit API, or Player tag fallback.
/// - Re-links PlayerMovement, PlayerLockOn, and PlayerStateMachine automatically.
/// - Syncs live MouseSensitivity from GameProgressionManager to CinemachineInputAxisController while preserving axis inversion.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PersistentPlayerCameraRig : MonoBehaviour
{
    public static PersistentPlayerCameraRig Instance { get; private set; }

    [Header("1. Rig Container & Core References")]
    [Tooltip("Child container holding Main Camera, CM_FreeLook, CM_LockOn, and CM_TargetGroup. Toggled off in non-combat UI scenes.")]
    [SerializeField] private GameObject rigContent;

    [Tooltip("The persistent URP Main Camera with CinemachineBrain and AudioListener.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("The primary Third-Person FreeLook / Orbital CinemachineCamera.")]
    [SerializeField] private CinemachineCamera freeLookCamera;

    [Tooltip("Optional Lock-On CinemachineCamera used when PlayerLockOn locks onto an enemy.")]
    [SerializeField] private CinemachineCamera lockOnCamera;

    [Tooltip("Optional CinemachineTargetGroup populated by PlayerLockOn during lock-on.")]
    [SerializeField] private CinemachineTargetGroup targetGroup;

    [Tooltip("CinemachineInputAxisController on CM_FreeLook used to apply GameProgressionManager MouseSensitivity.")]
    [SerializeField] private CinemachineInputAxisController inputAxisController;

    [Header("2. Scene Lifecycle Settings")]
    [Tooltip("Scenes where this combat camera rig will sleep (RigContent disabled) so UI/Menu cameras run without conflict.")]
    [SerializeField] private string[] nonCombatSceneNames = new string[]
    {
        "MainMenu",
        "The Lobby",
        "StageMap"
    };

    [Tooltip("Automatically disable any duplicate Camera, AudioListener, or CinemachineCamera found in combat stage scenes.")]
    [SerializeField] private bool autoDisableDuplicateSceneCameras = true;

    [Header("3. Dynamic Player Binding Settings")]
    [Tooltip("Tag used as a fallback search if a player is already spawned when a combat stage loads.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Preferred child Transform name on the player to track (e.g., CameraTarget). Auto-created if missing on future characters.")]
    [SerializeField] private string cameraTargetChildName = "CameraTarget";

    [Tooltip("Local Y height used when auto-creating a CameraTarget child on a character prefab that doesn't have one.")]
    [SerializeField] private float defaultTargetHeightOffset = 1.45f;

    [Header("4. Stage Entry Camera Snap Settings")]
    [Tooltip("Distance behind the newly spawned player to snap the camera on bind (prevents fly-in from previous stage coordinates).")]
    [SerializeField] private float initialSnapBackDistance = 4.5f;

    [Tooltip("Vertical offset above CameraTarget when snapping the camera behind a newly spawned player.")]
    [SerializeField] private float initialSnapUpOffset = 1.2f;

    [Header("5. Mouse Sensitivity Sync")]
    [Tooltip("Automatically scale CinemachineInputAxisController gains using GameProgressionManager.Instance.MouseSensitivity.")]
    [SerializeField] private bool syncMouseSensitivity = true;

    [Tooltip("Minimum allowed sensitivity multiplier.")]
    [SerializeField] private float minSensitivityMultiplier = 0.2f;

    [Tooltip("Maximum allowed sensitivity multiplier.")]
    [SerializeField] private float maxSensitivityMultiplier = 3.0f;

    private float[] cachedBaseAxisGains;
    private Transform currentPlayerRoot;
    private Transform currentCameraTarget;
    private bool isCombatSceneActive;
    private GameProgressionManager subscribedProgressionManager;

    public Camera MainCamera => mainCamera;
    public CinemachineCamera FreeLookCamera => freeLookCamera;
    public CinemachineCamera LockOnCamera => lockOnCamera;
    public CinemachineTargetGroup TargetGroup => targetGroup;
    public Transform CurrentPlayerRoot => currentPlayerRoot;
    public bool IsCombatSceneActive => isCombatSceneActive;

    /// <summary>
    /// Fired whenever the camera rig successfully binds to a newly spawned player.
    /// </summary>
    public event Action<Transform> OnPlayerBound;

    /// <summary>
    /// Static convenience method for any dynamic character spawner script to immediately bind the persistent camera rig.
    /// </summary>
    /// <param name="playerRoot">Root Transform of the newly spawned player.</param>
    public static void BindPlayer(Transform playerRoot)
    {
        if (Instance != null)
        {
            Instance.BindToPlayer(playerRoot);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad requires a root GameObject
        if (transform.parent != null)
        {
            transform.SetParent(null, false);
        }

        DontDestroyOnLoad(gameObject);

        ResolveInternalReferences();
        CacheBaseAxisGains();

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerMovement.OnPlayerSpawned += HandlePlayerSpawned;

        EvaluateSceneState(SceneManager.GetActiveScene());
    }

    private void Start()
    {
        SubscribeToProgressionManager();
        SyncSensitivityFromProgression();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerMovement.OnPlayerSpawned -= HandlePlayerSpawned;

        if (subscribedProgressionManager != null)
        {
            subscribedProgressionManager.OnProgressionChanged -= HandleProgressionChanged;
            subscribedProgressionManager = null;
        }

        Instance = null;
    }

    // =========================================================================
    // 1. SCENE LIFECYCLE & DUPLICATE CAMERA PROTECTION
    // =========================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        EvaluateSceneState(scene);
    }

    private void EvaluateSceneState(Scene scene)
    {
        bool isNonCombat = IsNonCombatScene(scene.name);
        isCombatSceneActive = !isNonCombat;

        // Always clear stale references from the previous scene
        UnbindPlayer();

        if (isNonCombat)
        {
            SetRigActive(false);
            return;
        }

        SetRigActive(true);

        if (autoDisableDuplicateSceneCameras)
        {
            DisableDuplicateSceneCameras();
        }

        SubscribeToProgressionManager();
        SyncSensitivityFromProgression();

        // Fallback bind in case a player was already spawned before sceneLoaded finished
        TryFindAndBindExistingPlayer();
    }

    private bool IsNonCombatScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || nonCombatSceneNames == null) return false;

        for (int i = 0; i < nonCombatSceneNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(nonCombatSceneNames[i]) &&
                string.Equals(sceneName, nonCombatSceneNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetRigActive(bool active)
    {
        if (rigContent != null)
        {
            rigContent.SetActive(active);
        }
        else if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(active);
        }

        if (active && lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Disables any scene-local Camera, AudioListener, or CinemachineCamera that does not belong to this persistent rig.
    /// Runs once per combat stage load (never in Update).
    /// </summary>
    private void DisableDuplicateSceneCameras()
    {
        Camera[] sceneCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sceneCameras.Length; i++)
        {
            Camera cam = sceneCameras[i];
            if (cam == null || cam.transform.IsChildOf(transform)) continue;

            // Leave non-main render-texture utility cameras (e.g., minimap/portrait cams) untouched
            if (cam.targetTexture != null) continue;

            cam.gameObject.SetActive(false);
        }

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || listener.transform.IsChildOf(transform)) continue;

            listener.enabled = false;
        }

        CinemachineCamera[] cmCameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cmCameras.Length; i++)
        {
            CinemachineCamera cmCam = cmCameras[i];
            if (cmCam == null || cmCam.transform.IsChildOf(transform)) continue;

            cmCam.gameObject.SetActive(false);
        }
    }

    // =========================================================================
    // 2. DYNAMIC MULTI-CHARACTER AUTO-BINDING
    // =========================================================================

    private void HandlePlayerSpawned(Transform playerRoot)
    {
        if (!isCombatSceneActive || playerRoot == null) return;

        BindToPlayer(playerRoot);
    }

    private void TryFindAndBindExistingPlayer()
    {
        if (currentPlayerRoot != null) return;

        PlayerMovement existingMovement = FindAnyObjectByType<PlayerMovement>();
        if (existingMovement != null)
        {
            BindToPlayer(existingMovement.transform);
            return;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindWithTag(playerTag);
            if (taggedPlayer != null)
            {
                BindToPlayer(taggedPlayer.transform);
            }
        }
    }

    /// <summary>
    /// Binds this persistent camera rig to a newly spawned player character (existing or future)
    /// and re-links PlayerMovement, PlayerLockOn, and PlayerStateMachine automatically.
    /// </summary>
    /// <param name="playerRoot">Root Transform of the spawned player.</param>
    public void BindToPlayer(Transform playerRoot)
    {
        if (playerRoot == null) return;

        if (!isCombatSceneActive)
        {
            isCombatSceneActive = true;
            SetRigActive(true);
            if (autoDisableDuplicateSceneCameras)
            {
                DisableDuplicateSceneCameras();
            }
        }

        Transform targetTransform = ResolveOrCreateCameraTarget(playerRoot);

        currentPlayerRoot = playerRoot;
        currentCameraTarget = targetTransform;

        // 1. Bind FreeLook CinemachineCamera and snap behind the player to avoid cross-map fly-in
        if (freeLookCamera != null)
        {
            freeLookCamera.Target.TrackingTarget = targetTransform;
            freeLookCamera.Target.CustomLookAtTarget = false;
            freeLookCamera.Target.LookAtTarget = null;
            freeLookCamera.gameObject.SetActive(true);

            Vector3 backDir = playerRoot.forward.sqrMagnitude > 0.001f ? playerRoot.forward : Vector3.forward;
            Vector3 snapPos = targetTransform.position - (backDir * initialSnapBackDistance) + (Vector3.up * initialSnapUpOffset);
            Vector3 lookDir = targetTransform.position - snapPos;
            Quaternion snapRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : playerRoot.rotation;

            freeLookCamera.ForceCameraPosition(snapPos, snapRot);
        }

        // 2. Reset TargetGroup and prepare LockOn CinemachineCamera (inactive until player presses Lock-On)
        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.Target.TrackingTarget = targetTransform;
            if (targetGroup != null)
            {
                lockOnCamera.Target.CustomLookAtTarget = true;
                lockOnCamera.Target.LookAtTarget = targetGroup.transform;
            }
            else
            {
                lockOnCamera.Target.CustomLookAtTarget = false;
                lockOnCamera.Target.LookAtTarget = null;
            }

            lockOnCamera.gameObject.SetActive(false);
        }

        // 3. Re-link player combat/movement scripts with zero Reflection
        if (playerRoot.TryGetComponent<PlayerMovement>(out var movement) && mainCamera != null)
        {
            movement.SetCameraTransform(mainCamera.transform);
        }

        if (playerRoot.TryGetComponent<PlayerLockOn>(out var lockOn))
        {
            lockOn.BindCameraRig(mainCamera, freeLookCamera, lockOnCamera, targetGroup);
        }

        if (playerRoot.TryGetComponent<PlayerStateMachine>(out var stateMachine) && mainCamera != null)
        {
            stateMachine.SetMainCamera(mainCamera);
        }

        SyncSensitivityFromProgression();
        OnPlayerBound?.Invoke(playerRoot);
    }

    /// <summary>
    /// Clears active target references cleanly when leaving a stage or despawning the player.
    /// </summary>
    public void UnbindPlayer()
    {
        currentPlayerRoot = null;
        currentCameraTarget = null;

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
        }

        if (freeLookCamera != null)
        {
            freeLookCamera.Target.TrackingTarget = null;
            freeLookCamera.Target.LookAtTarget = null;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.Target.TrackingTarget = null;
            lockOnCamera.Target.LookAtTarget = null;
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private Transform ResolveOrCreateCameraTarget(Transform playerRoot)
    {
        if (!string.IsNullOrEmpty(cameraTargetChildName))
        {
            Transform existingTarget = FindChildRecursive(playerRoot, cameraTargetChildName);
            if (existingTarget != null)
            {
                return existingTarget;
            }
        }

        // Auto-create a clean CameraTarget pivot on future character prefabs that lack one
        GameObject pivotObj = new GameObject(string.IsNullOrEmpty(cameraTargetChildName) ? "CameraTarget" : cameraTargetChildName);
        pivotObj.transform.SetParent(playerRoot, false);
        pivotObj.transform.localPosition = new Vector3(0f, defaultTargetHeightOffset, 0f);
        pivotObj.transform.localRotation = Quaternion.identity;
        return pivotObj.transform;
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        int childCount = parent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            if (child.childCount > 0)
            {
                Transform nested = FindChildRecursive(child, targetName);
                if (nested != null) return nested;
            }
        }

        return null;
    }

    // =========================================================================
    // 3. LIVE MOUSE SENSITIVITY SYNC
    // =========================================================================

    private void SubscribeToProgressionManager()
    {
        if (!syncMouseSensitivity) return;

        GameProgressionManager mgr = GameProgressionManager.Instance;
        if (mgr == null || mgr == subscribedProgressionManager) return;

        if (subscribedProgressionManager != null)
        {
            subscribedProgressionManager.OnProgressionChanged -= HandleProgressionChanged;
        }

        subscribedProgressionManager = mgr;
        subscribedProgressionManager.OnProgressionChanged += HandleProgressionChanged;
    }

    private void HandleProgressionChanged()
    {
        SyncSensitivityFromProgression();
    }

    private void SyncSensitivityFromProgression()
    {
        if (!syncMouseSensitivity) return;

        float sensitivity = subscribedProgressionManager != null
            ? subscribedProgressionManager.MouseSensitivity
            : 1.0f;

        ApplyMouseSensitivity(sensitivity);
    }

    /// <summary>
    /// Scales every CinemachineInputAxisController axis gain relative to its cached base gain,
    /// preserving original X/Y direction signs and vertical inversion.
    /// </summary>
    /// <param name="sensitivityMultiplier">Sensitivity multiplier (e.g., 0.2x to 3.0x).</param>
    public void ApplyMouseSensitivity(float sensitivityMultiplier)
    {
        if (inputAxisController == null || inputAxisController.Controllers == null) return;

        var controllers = inputAxisController.Controllers;
        if (cachedBaseAxisGains == null || cachedBaseAxisGains.Length != controllers.Count)
        {
            CacheBaseAxisGains();
        }

        float clamped = Mathf.Clamp(sensitivityMultiplier, minSensitivityMultiplier, maxSensitivityMultiplier);
        int count = Mathf.Min(controllers.Count, cachedBaseAxisGains != null ? cachedBaseAxisGains.Length : 0);

        for (int i = 0; i < count; i++)
        {
            if (controllers[i] != null && controllers[i].Input != null)
            {
                controllers[i].Input.Gain = cachedBaseAxisGains[i] * clamped;
            }
        }
    }

    private void CacheBaseAxisGains()
    {
        if (inputAxisController == null || inputAxisController.Controllers == null)
        {
            cachedBaseAxisGains = Array.Empty<float>();
            return;
        }

        var controllers = inputAxisController.Controllers;
        int count = controllers.Count;
        cachedBaseAxisGains = new float[count];

        for (int i = 0; i < count; i++)
        {
            cachedBaseAxisGains[i] = (controllers[i] != null && controllers[i].Input != null)
                ? controllers[i].Input.Gain
                : 1.0f;
        }
    }

    // =========================================================================
    // 4. INTERNAL REFERENCE AUTO-RESOLUTION (INSPECTOR SAFETY NET)
    // =========================================================================

    private void ResolveInternalReferences()
    {
        if (rigContent == null && transform.childCount > 0)
        {
            Transform foundRig = transform.Find("RigContent");
            rigContent = foundRig != null ? foundRig.gameObject : transform.GetChild(0).gameObject;
        }

        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>(true);
        }

        if (freeLookCamera == null || lockOnCamera == null)
        {
            CinemachineCamera[] childCams = GetComponentsInChildren<CinemachineCamera>(true);
            for (int i = 0; i < childCams.Length; i++)
            {
                CinemachineCamera cm = childCams[i];
                if (cm == null) continue;

                if (freeLookCamera == null && cm.name.IndexOf("FreeLook", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    freeLookCamera = cm;
                }
                else if (lockOnCamera == null && cm.name.IndexOf("LockOn", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lockOnCamera = cm;
                }
            }

            // Fallback: if naming differs, assign the first CinemachineCamera as FreeLook
            if (freeLookCamera == null && childCams.Length > 0)
            {
                freeLookCamera = childCams[0];
            }
        }

        if (targetGroup == null)
        {
            targetGroup = GetComponentInChildren<CinemachineTargetGroup>(true);
        }

        if (inputAxisController == null && freeLookCamera != null)
        {
            inputAxisController = freeLookCamera.GetComponent<CinemachineInputAxisController>();
        }

        if (inputAxisController == null)
        {
            inputAxisController = GetComponentInChildren<CinemachineInputAxisController>(true);
        }
    }
}
