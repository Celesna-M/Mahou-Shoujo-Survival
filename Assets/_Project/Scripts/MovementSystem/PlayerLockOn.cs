using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HoYoverse / Zenless Zone Zero Style Cinematic Lock-On System (Zero-GC Hot Path Edition)
/// - Wide, readable action framing via dual smooth anchors (FollowAnchor + weighted LookAtAnchor) on CM_LockOn.
/// - Seamless FreeLook yaw/pitch handoff via CinemachineOrbitalFollow on unlock (zero camera snap).
/// - Trigger-safe NonAlloc Line-of-Sight check (ignores Triggers, Player, and Target's own colliders).
/// - Auto-switches smoothly to the next highest-priority enemy when the locked target dies.
/// - Zero-GC pulsing UI Reticle updated in LateUpdate() AFTER CinemachineBrain (zero 1-frame jitter),
///   supporting a custom Sprite (ToggleOff_Bright) or custom Inspector RectTransform.
/// </summary>
public class PlayerLockOn : MonoBehaviour
{
    [Header("1. Detection Settings")]
    [Tooltip("Maximum detection distance for acquiring a Lock-On target.")]
    [SerializeField] private float lockOnRadius = 16f;

    [Tooltip("360-degree close-range threat radius (immediately locks flanking enemies even behind the camera).")]
    [SerializeField] private float closeThreatRadius = 4.5f;

    [Tooltip("Viewport padding around screen edges when checking visible enemies.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float viewportMargin = 0.12f;

    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("Chest height offset on target enemies when no CenterBody anchor is available.")]
    [SerializeField] private float targetHeightOffset = 1.2f;

    [Header("2. ZZZ Wide Cinematic Framing & Tracking")]
    [Tooltip("Player facing rotation smoothing toward the locked target (paused during Dodge rolls).")]
    [SerializeField] private float playerRotateSmoothing = 12f;

    [Tooltip("Fallback main camera look smoothing when no Cinemachine lockOnCamera is assigned.")]
    [SerializeField] private float cameraFacingSmoothing = 8f;

    [Tooltip("Height offset above Player root for the smooth Lock-On follow anchor.")]
    [SerializeField] private float followAnchorHeight = 1.45f;

    [Tooltip("How smoothly the Lock-On camera orbits behind the Player->Enemy combat axis.")]
    [SerializeField] private float followOrbitSmoothSpeed = 5.5f;

    [Tooltip("Blend weight of the LookAt point between Player chest (0) and Enemy chest (1). ~0.62 gives wide ZZZ framing.")]
    [Range(0.25f, 1.0f)]
    [SerializeField] private float lookAtEnemyWeight = 0.62f;

    [Tooltip("Maximum distance the LookAt point can pull away from the Player so surrounding threats remain readable.")]
    [SerializeField] private float maxLookAtPullDistance = 6.5f;

    [Tooltip("How smoothly the Lock-On LookAt point glides when enemies move or when switching targets.")]
    [SerializeField] private float lookAtSmoothSpeed = 10f;

    [Tooltip("Optional override for CM_LockOn ThirdPersonFollow CameraDistance (leave unchecked to tune directly on CM_LockOn).")]
    [SerializeField] private bool overrideLockOnCameraDistance = false;

    [Tooltip("Lock-On camera distance used when overrideLockOnCameraDistance is enabled.")]
    [SerializeField] private float lockOnCameraDistance = 7.5f;

    [Header("3. UI Lock-On Reticle & Zero-GC Pulse")]
    [Tooltip("Optional pre-placed UI RectTransform reticle. If left empty, one is auto-created using Lock-On Reticle Sprite.")]
    [SerializeField] private RectTransform lockOnUIMarker;

    [Tooltip("Sprite assigned to the auto-generated Lock-On reticle (e.g., ToggleOff_Bright).")]
    [SerializeField] private Sprite lockOnReticleSprite;

    [Tooltip("Size (width, height) in pixels for the auto-generated Lock-On reticle.")]
    [SerializeField] private Vector2 reticleSize = new Vector2(68f, 68f);

    [Tooltip("Tint color applied to the auto-generated Lock-On reticle Image.")]
    [SerializeField] private Color reticleTintColor = Color.white;

    [Tooltip("Minimum pulsing localScale while locked on.")]
    [SerializeField] private float reticleMinScale = 0.85f;

    [Tooltip("Maximum pulsing localScale while locked on.")]
    [SerializeField] private float reticleMaxScale = 1.18f;

    [Tooltip("Pulsing speed (radians/sec) driven by Mathf.Sin in LateUpdate.")]
    [SerializeField] private float reticlePulseSpeed = 5.0f;

    [Header("4. Cinemachine 3 Integration")]
    [SerializeField] private CinemachineCamera freeLookCamera;
    [SerializeField] private CinemachineCamera lockOnCamera;
    [SerializeField] private CinemachineTargetGroup targetGroup;

    private Transform currentTarget;
    private IDamageable currentTargetDamageable;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private Camera mainCamera;
    private CinemachineOrbitalFollow cachedFreeLookOrbital;
    private CinemachineThirdPersonFollow cachedLockOnFollow;

    // Pre-allocated runtime anchors (zero GC, reused across all lock-ons)
    private Transform lockOnFollowAnchor;
    private Transform lockOnLookAtAnchor;
    private GameObject autoCreatedReticleCanvas;

    private readonly List<Transform> detectedTargets = new List<Transform>(16);
    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly RaycastHit[] losHitBuffer = new RaycastHit[12];
    private float switchCooldown = 0.2f;
    private float switchTimer = 0f;

    public bool IsLockedOn => currentTarget != null;
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        mainCamera = (PersistentPlayerCameraRig.Instance != null && PersistentPlayerCameraRig.Instance.MainCamera != null)
            ? PersistentPlayerCameraRig.Instance.MainCamera
            : Camera.main;

        if (enemyLayers.value == 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            enemyLayers = (enemyLayer != -1) ? (1 << enemyLayer) : ~LayerMask.GetMask("Player", "Ignore Raycast", "UI");
        }

        CacheCameraComponents();
        EnsureLockOnAnchors();

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.gameObject.SetActive(false);
        }
    }

    private void CacheCameraComponents()
    {
        cachedFreeLookOrbital = freeLookCamera != null ? freeLookCamera.GetComponent<CinemachineOrbitalFollow>() : null;
        cachedLockOnFollow = lockOnCamera != null ? lockOnCamera.GetComponent<CinemachineThirdPersonFollow>() : null;
        if (overrideLockOnCameraDistance && cachedLockOnFollow != null)
        {
            cachedLockOnFollow.CameraDistance = Mathf.Max(2.5f, lockOnCameraDistance);
        }
    }

    private void EnsureLockOnAnchors()
    {
        if (lockOnFollowAnchor == null)
        {
            GameObject followObj = new GameObject("[LockOn_FollowAnchor]");
            lockOnFollowAnchor = followObj.transform;
            lockOnFollowAnchor.SetPositionAndRotation(transform.position + Vector3.up * followAnchorHeight, transform.rotation);
        }

        if (lockOnLookAtAnchor == null)
        {
            GameObject lookObj = new GameObject("[LockOn_LookAtAnchor]");
            lockOnLookAtAnchor = lookObj.transform;
            lockOnLookAtAnchor.position = transform.position + Vector3.up * followAnchorHeight + transform.forward * 3f;
        }
    }

    private void EnsureReticleMarker()
    {
        if (lockOnUIMarker != null) return;

        GameObject canvasObj = new GameObject("[LockOn_ReticleCanvas]");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        autoCreatedReticleCanvas = canvasObj;

        GameObject reticleObj = new GameObject("LockOn_Reticle");
        reticleObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = reticleObj.AddComponent<RectTransform>();
        rt.sizeDelta = reticleSize;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image img = reticleObj.AddComponent<Image>();
        img.sprite = lockOnReticleSprite;
        img.color = reticleTintColor;
        img.preserveAspect = true;
        img.raycastTarget = false;

        lockOnUIMarker = rt;
        lockOnUIMarker.gameObject.SetActive(false);
    }

    /// <summary>
    /// Re-links this PlayerLockOn instance to the persistent cross-stage camera rig.
    /// Called automatically by PersistentPlayerCameraRig when a player spawns in a new stage.
    /// </summary>
    public void BindCameraRig(
        Camera cam,
        CinemachineCamera freeLook,
        CinemachineCamera lockOn,
        CinemachineTargetGroup group)
    {
        if (cam != null) mainCamera = cam;
        if (freeLook != null) freeLookCamera = freeLook;
        if (lockOn != null) lockOnCamera = lockOn;
        if (group != null) targetGroup = group;

        CacheCameraComponents();

        if (IsLockedOn)
        {
            UnlockTarget();
        }
        else if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.LockOn.performed += OnLockOnInput;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null && playerMovement.InputActions != null)
        {
            playerMovement.InputActions.Player.LockOn.performed -= OnLockOnInput;
        }

        if (lockOnFollowAnchor != null) Destroy(lockOnFollowAnchor.gameObject);
        if (lockOnLookAtAnchor != null) Destroy(lockOnLookAtAnchor.gameObject);
        if (autoCreatedReticleCanvas != null) Destroy(autoCreatedReticleCanvas);
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            if (IsLockedOn) UnlockTarget();
            return;
        }

        if (switchTimer > 0f)
            switchTimer -= Time.deltaTime;

        // Keyboard fallback: [Q] (Middle Click, [Tab], and Gamepad R3 are bound via InputSystem_Actions; [E] is Active Skill).
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            ToggleLockOn();
        }

        if (IsLockedOn)
        {
            if (ShouldUnlockTarget())
            {
                FindNextOrUnlock();
                return;
            }

            UpdateLockOnAnchors(Time.deltaTime);
            RotatePlayerTowardsTarget();
            UpdateCameraFacingFallback();
            HandleTargetSwitching();
        }
    }

    private void LateUpdate()
    {
        if (!IsLockedOn) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;

        // Update UI marker in LateUpdate AFTER CinemachineBrain moves mainCamera -> zero 1-frame jitter!
        UpdateUIMarkerPositionAndPulse();
    }

    private void OnLockOnInput(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0.01f) return;
        if (BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen) return;
        if (VNManager.Instance != null && VNManager.Instance.IsPlayingVN) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        ToggleLockOn();
    }

    public void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            UnlockTarget();
        }
        else
        {
            FindAndLockBestTarget();
        }
    }

    public void FindAndLockBestTarget()
    {
        ScanCandidates();

        if (detectedTargets.Count == 0)
        {
            UnlockTarget();
            return;
        }

        detectedTargets.Sort(CompareTargetPriority);
        LockOnto(detectedTargets[0], snapAnchorsFromCamera: true);
    }

    private int CompareTargetPriority(Transform a, Transform b)
    {
        return EvaluateTargetScore(a).CompareTo(EvaluateTargetScore(b));
    }

    private float EvaluateTargetScore(Transform enemy)
    {
        if (enemy == null) return float.MaxValue;

        float distToPlayer = Vector3.Distance(transform.position, enemy.position);

        // Close-range 360 swarm priority
        if (distToPlayer <= closeThreatRadius)
        {
            return distToPlayer;
        }

        // Viewport center + distance score
        if (mainCamera != null)
        {
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(GetTargetChestPosition(enemy));
            if (viewportPos.z > 0f)
            {
                float dx = viewportPos.x - 0.5f;
                float dy = viewportPos.y - 0.5f;
                float distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                return 10f + distToPlayer + (distFromCenter * 15f);
            }
        }

        return 50f + distToPlayer;
    }

    private void ScanCandidates()
    {
        detectedTargets.Clear();
        if (mainCamera == null && PersistentPlayerCameraRig.Instance != null)
            mainCamera = PersistentPlayerCameraRig.Instance.MainCamera;

        int count = Physics.OverlapSphereNonAlloc(transform.position, lockOnRadius, overlapBuffer, enemyLayers);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (dmg == null || dmg.IsDead)
                continue;

            Transform enemy = (dmg is MonoBehaviour mb) ? mb.transform : col.transform.root;
            if (detectedTargets.Contains(enemy))
                continue;

            float distToPlayer = Vector3.Distance(transform.position, enemy.position);
            Vector3 targetCenter = GetTargetChestPosition(enemy);

            bool isEligible = false;

            if (distToPlayer <= closeThreatRadius)
            {
                isEligible = true;
            }
            else if (mainCamera != null)
            {
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetCenter);
                if (viewportPos.z > 0f &&
                    viewportPos.x >= -viewportMargin && viewportPos.x <= 1f + viewportMargin &&
                    viewportPos.y >= -viewportMargin && viewportPos.y <= 1f + viewportMargin)
                {
                    isEligible = true;
                }
            }

            if (!isEligible) continue;

            Vector3 rayOrigin = transform.position + Vector3.up * followAnchorHeight;
            if (HasLineOfSight(rayOrigin, enemy, targetCenter))
            {
                detectedTargets.Add(enemy);
            }
        }
    }

    private bool HasLineOfSight(Vector3 rayOrigin, Transform enemyRoot, Vector3 targetCenter)
    {
        if (obstacleLayers.value == 0) return true;

        Vector3 toTarget = targetCenter - rayOrigin;
        float dist = toTarget.magnitude;
        if (dist <= 0.2f) return true;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            toTarget / dist,
            losHitBuffer,
            dist,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCol = losHitBuffer[i].collider;
            if (hitCol == null) continue;

            Transform hitRoot = hitCol.transform.root;
            if (hitRoot == transform.root || hitRoot == enemyRoot.root)
                continue;

            // Blocked by an actual solid world obstacle
            return false;
        }

        return true;
    }

    private void LockOnto(Transform target, bool snapAnchorsFromCamera)
    {
        if (target == null)
        {
            UnlockTarget();
            return;
        }

        EnsureLockOnAnchors();
        EnsureReticleMarker();

        currentTarget = target;
        currentTargetDamageable = target.GetComponentInParent<IDamageable>() ?? target.GetComponent<IDamageable>();

        Vector3 playerChest = transform.position + Vector3.up * followAnchorHeight;
        Vector3 desiredLookAt = ComputeWeightedLookAtPosition(playerChest, currentTarget);

        if (snapAnchorsFromCamera)
        {
            lockOnFollowAnchor.position = playerChest;

            // Initialize follow yaw from current camera forward so CM_LockOn blends smoothly with zero pop
            Vector3 startForward = mainCamera != null ? mainCamera.transform.forward : (currentTarget.position - transform.position);
            startForward.y = 0f;
            if (startForward.sqrMagnitude < 0.001f) startForward = transform.forward;
            lockOnFollowAnchor.rotation = Quaternion.LookRotation(startForward.normalized, Vector3.up);
            lockOnLookAtAnchor.position = desiredLookAt;
        }

        if (overrideLockOnCameraDistance && cachedLockOnFollow != null)
        {
            cachedLockOnFollow.CameraDistance = Mathf.Max(2.5f, lockOnCameraDistance);
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.Target.TrackingTarget = lockOnFollowAnchor;
            lockOnCamera.Target.CustomLookAtTarget = true;
            lockOnCamera.Target.LookAtTarget = lockOnLookAtAnchor;
            lockOnCamera.gameObject.SetActive(true);
        }

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
            targetGroup.AddMember(transform, 1f, 1.5f);
            targetGroup.AddMember(currentTarget, 1f, 2.0f);
        }

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.gameObject.SetActive(true);
            UpdateUIMarkerPositionAndPulse();
        }

        AudioManager.PlayUI(snapAnchorsFromCamera ? AudioKeys.Combat_LockOn : AudioKeys.Combat_LockSwitch);
    }

    public void UnlockTarget()
    {
        bool wasLocked = currentTarget != null;
        currentTarget = null;
        currentTargetDamageable = null;

        if (wasLocked)
        {
            SyncFreeLookCameraToCurrentView();
        }

        if (lockOnUIMarker != null)
        {
            lockOnUIMarker.localScale = Vector3.one;
            lockOnUIMarker.gameObject.SetActive(false);
        }

        if (targetGroup != null)
        {
            targetGroup.Targets.Clear();
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.gameObject.SetActive(false);
        }
    }

    private void SyncFreeLookCameraToCurrentView()
    {
        if (mainCamera == null || cachedFreeLookOrbital == null) return;

        Vector3 camForward = mainCamera.transform.forward;
        Vector3 flatFwd = new Vector3(camForward.x, 0f, camForward.z);
        if (flatFwd.sqrMagnitude > 0.001f)
        {
            float yaw = Mathf.Atan2(flatFwd.x, flatFwd.z) * Mathf.Rad2Deg;
            cachedFreeLookOrbital.HorizontalAxis.Value = yaw;
        }

        float pitch = -Mathf.Asin(Mathf.Clamp(camForward.y, -0.95f, 0.95f)) * Mathf.Rad2Deg;
        cachedFreeLookOrbital.VerticalAxis.Value = Mathf.Clamp(
            pitch,
            cachedFreeLookOrbital.VerticalAxis.Range.x,
            cachedFreeLookOrbital.VerticalAxis.Range.y);
    }

    private void UpdateLockOnAnchors(float dt)
    {
        if (currentTarget == null || lockOnFollowAnchor == null || lockOnLookAtAnchor == null) return;

        Vector3 playerChest = transform.position + Vector3.up * followAnchorHeight;
        lockOnFollowAnchor.position = playerChest;

        // Softly orbit behind the Player -> Enemy combat axis (wide ZZZ action feel, never whips during dodge)
        Vector3 toEnemyFlat = currentTarget.position - transform.position;
        toEnemyFlat.y = 0f;
        if (toEnemyFlat.sqrMagnitude > 0.04f)
        {
            Quaternion targetOrbitRot = Quaternion.LookRotation(toEnemyFlat.normalized, Vector3.up);
            float orbitT = 1f - Mathf.Exp(-followOrbitSmoothSpeed * dt);
            lockOnFollowAnchor.rotation = Quaternion.Slerp(lockOnFollowAnchor.rotation, targetOrbitRot, orbitT);
        }

        Vector3 desiredLookAt = ComputeWeightedLookAtPosition(playerChest, currentTarget);
        float lookT = 1f - Mathf.Exp(-lookAtSmoothSpeed * dt);
        lockOnLookAtAnchor.position = Vector3.Lerp(lockOnLookAtAnchor.position, desiredLookAt, lookT);
    }

    private Vector3 ComputeWeightedLookAtPosition(Vector3 playerChest, Transform enemy)
    {
        Vector3 enemyChest = GetTargetChestPosition(enemy);
        Vector3 offset = (enemyChest - playerChest) * lookAtEnemyWeight;
        float maxPull = Mathf.Max(1.0f, maxLookAtPullDistance);
        if (offset.sqrMagnitude > maxPull * maxPull)
        {
            offset = offset.normalized * maxPull;
        }
        return playerChest + offset;
    }

    private Vector3 GetTargetChestPosition(Transform target)
    {
        if (target == null) return Vector3.zero;
        return target.position + Vector3.up * targetHeightOffset;
    }

    private bool ShouldUnlockTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy) return true;
        if (currentTargetDamageable != null && currentTargetDamageable.IsDead) return true;

        float maxDist = lockOnRadius * 1.35f;
        if ((currentTarget.position - transform.position).sqrMagnitude > maxDist * maxDist) return true;

        return false;
    }

    private void FindNextOrUnlock()
    {
        Transform oldTarget = currentTarget;
        ScanCandidates();
        detectedTargets.Remove(oldTarget);

        if (detectedTargets.Count > 0)
        {
            detectedTargets.Sort(CompareTargetPriority);
            // Smooth glide onto the next enemy (do not hard-snap anchors!)
            LockOnto(detectedTargets[0], snapAnchorsFromCamera: false);
        }
        else
        {
            UnlockTarget();
        }
    }

    private void UpdateUIMarkerPositionAndPulse()
    {
        if (lockOnUIMarker == null || currentTarget == null || mainCamera == null) return;

        Vector3 targetWorldPos = GetTargetChestPosition(currentTarget);
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetWorldPos);

        if (screenPos.z > 0f)
        {
            if (!lockOnUIMarker.gameObject.activeSelf)
                lockOnUIMarker.gameObject.SetActive(true);

            lockOnUIMarker.position = screenPos;

            // Zero-GC pulsing scale using Mathf.Sin on localScale
            float pulseT = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * reticlePulseSpeed);
            float scale = Mathf.Lerp(reticleMinScale, reticleMaxScale, pulseT);
            lockOnUIMarker.localScale = new Vector3(scale, scale, 1f);
        }
        else if (lockOnUIMarker.gameObject.activeSelf)
        {
            lockOnUIMarker.gameObject.SetActive(false);
        }
    }

    private void RotatePlayerTowardsTarget()
    {
        // Do not pull player facing off-axis while executing a Dodge roll
        if (playerMovement != null && playerMovement.IsDodging) return;

        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, playerRotateSmoothing * Time.deltaTime);
        }
    }

    private void UpdateCameraFacingFallback()
    {
        if (mainCamera == null || lockOnCamera != null) return;

        Vector3 dir = GetTargetChestPosition(currentTarget) - mainCamera.transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, lookRot, cameraFacingSmoothing * Time.deltaTime);
        }
    }

    private void HandleTargetSwitching()
    {
        if (switchTimer > 0f) return;

        int switchDir = 0;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.1f) switchDir = 1;
            else if (scroll < -0.1f) switchDir = -1;
        }

        if (switchDir == 0 && Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame) switchDir = 1;
            else if (Keyboard.current.zKey.wasPressedThisFrame) switchDir = -1;
        }

        if (switchDir == 0) return;

        ScanCandidates();
        if (detectedTargets.Count <= 1) return;

        detectedTargets.Sort(CompareTargetAngleClockwise);

        int currentIndex = detectedTargets.IndexOf(currentTarget);
        if (currentIndex == -1) currentIndex = 0;

        int newIndex = switchDir > 0
            ? (currentIndex + 1) % detectedTargets.Count
            : (currentIndex - 1 + detectedTargets.Count) % detectedTargets.Count;

        switchTimer = switchCooldown;
        LockOnto(detectedTargets[newIndex], snapAnchorsFromCamera: false);
    }

    private int CompareTargetAngleClockwise(Transform a, Transform b)
    {
        Vector3 dirA = a.position - transform.position;
        Vector3 dirB = b.position - transform.position;
        float angleA = Mathf.Atan2(dirA.x, dirA.z);
        float angleB = Mathf.Atan2(dirB.x, dirB.z);
        return angleA.CompareTo(angleB);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, closeThreatRadius);
    }
}
