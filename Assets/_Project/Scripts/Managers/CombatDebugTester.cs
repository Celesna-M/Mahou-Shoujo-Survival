using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Developer Debug Tester & Movement Diagnostics (Pure ASCII English OnGUI)
/// </summary>
public class CombatDebugTester : MonoBehaviour
{
    public enum CameraViewMode
    {
        DefaultGameCamera = 0,
        BehindNormal = 1,
        ShoulderClose = 2,
        SideView90 = 3,
        FrontView180 = 4,
        TopDown75 = 5
    }

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject gruntPrefab;
    [SerializeField] private GameObject elitePrefab;

    [Header("Settings")]
    [SerializeField] private float spawnDistanceInFront = 4.0f;
    [SerializeField] private bool showOnScreenMenu = true;
    [SerializeField] private bool showMovementDiagnostics = true;

    [Header("Camera Test Mode")]
    [SerializeField] private CameraViewMode currentCameraView = CameraViewMode.DefaultGameCamera;
    [SerializeField] private float cameraFollowSmooth = 10f;

    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;
    private PlayerMovement playerMovement;
    private CharacterController characterController;
    private Animator playerAnimator;

    private Camera mainCamera;
    private Behaviour cinemachineBrain;

    private float lastCalculatedDriftAngle = 0f;
    private float lastFacingAngle = 0f;
    private string lastMovementStatus = "IDLE";
    private string lastTiltStatus = "LEVEL (0 deg)";

    private void Start()
    {
        InitializePlayerReferences();
        InitializeCameraReferences();
        FindEnemyPrefabs();
    }

    private void InitializePlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
            playerStamina = player.GetComponent<PlayerStamina>();
            playerMovement = player.GetComponent<PlayerMovement>();
            characterController = player.GetComponent<CharacterController>();
            playerAnimator = player.GetComponentInChildren<Animator>();
        }
    }

    private void InitializeCameraReferences()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cinemachineBrain = mainCamera.GetComponent("CinemachineBrain") as Behaviour;
        }
    }

    private void FindEnemyPrefabs()
    {
        if (gruntPrefab == null || elitePrefab == null)
        {
            WaveSpawner spawner = FindAnyObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                var normalField = typeof(WaveSpawner).GetField("NormalPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var gruntField = typeof(WaveSpawner).GetField("gruntPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var eliteField = typeof(WaveSpawner).GetField("elitePrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (gruntPrefab == null)
                {
                    if (normalField != null) gruntPrefab = (GameObject)normalField.GetValue(spawner);
                    else if (gruntField != null) gruntPrefab = (GameObject)gruntField.GetValue(spawner);
                }

                if (eliteField != null && elitePrefab == null)
                    elitePrefab = (GameObject)eliteField.GetValue(spawner);
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            InitializePlayerReferences();
        }

        HandleHotkeys();
        MeasureMovementAndTilt();
    }

    private void LateUpdate()
    {
        UpdateTestCamera();
    }

    private void HandleHotkeys()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            showOnScreenMenu = !showOnScreenMenu;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            showMovementDiagnostics = !showMovementDiagnostics;
        }

        if (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            CycleCameraView();
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.f1Key.wasPressedThisFrame) SpawnEnemy(gruntPrefab, false);
        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.f2Key.wasPressedThisFrame) SpawnEnemy(elitePrefab != null ? elitePrefab : gruntPrefab, true);
        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.f3Key.wasPressedThisFrame) InstantStunNearestEnemy();
        if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.f4Key.wasPressedThisFrame) KillAllEnemies();
        if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.f5Key.wasPressedThisFrame) FullHealPlayer();
    }

    public void SetCameraView(CameraViewMode mode)
    {
        currentCameraView = mode;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        if (cinemachineBrain == null)
            cinemachineBrain = mainCamera.GetComponent("CinemachineBrain") as Behaviour;

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = (currentCameraView == CameraViewMode.DefaultGameCamera);
        }
    }

    public void CycleCameraView()
    {
        int next = ((int)currentCameraView + 1) % 6;
        SetCameraView((CameraViewMode)next);
    }

    private void UpdateTestCamera()
    {
        if (currentCameraView == CameraViewMode.DefaultGameCamera) return;
        if (mainCamera == null || playerTransform == null) return;

        Vector3 targetPos = Vector3.zero;
        Vector3 lookAtTarget = playerTransform.position + Vector3.up * 1.3f;

        switch (currentCameraView)
        {
            case CameraViewMode.BehindNormal:
                targetPos = playerTransform.position - (playerTransform.forward * 4.5f) + (Vector3.up * 1.8f);
                break;

            case CameraViewMode.ShoulderClose:
                targetPos = playerTransform.position
                          - (playerTransform.forward * 2.0f)
                          + (playerTransform.right * 0.65f)
                          + (Vector3.up * 1.5f);
                lookAtTarget = playerTransform.position + (playerTransform.forward * 1.0f) + Vector3.up * 1.4f;
                break;

            case CameraViewMode.SideView90:
                targetPos = playerTransform.position + (playerTransform.right * 4.0f) + (Vector3.up * 1.4f);
                break;

            case CameraViewMode.FrontView180:
                targetPos = playerTransform.position + (playerTransform.forward * 3.8f) + (Vector3.up * 1.5f);
                break;

            case CameraViewMode.TopDown75:
                targetPos = playerTransform.position - (playerTransform.forward * 1.8f) + (Vector3.up * 7.5f);
                lookAtTarget = playerTransform.position + Vector3.up * 0.5f;
                break;
        }

        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, Time.deltaTime * cameraFollowSmooth);
        Quaternion targetRot = Quaternion.LookRotation(lookAtTarget - mainCamera.transform.position);
        mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRot, Time.deltaTime * cameraFollowSmooth);
    }

    private void MeasureMovementAndTilt()
    {
        if (playerTransform == null) return;

        float pitch = Mathf.DeltaAngle(0f, playerTransform.eulerAngles.x);
        float roll = Mathf.DeltaAngle(0f, playerTransform.eulerAngles.z);

        if (Mathf.Abs(pitch) < 0.5f && Mathf.Abs(roll) < 0.5f)
        {
            lastTiltStatus = "<color=#00FF66>[OK] LEVEL (0 deg)</color>";
        }
        else
        {
            lastTiltStatus = $"<color=#FFCC00>[WARN] TILTED (Pitch={pitch:F1}, Roll={roll:F1})</color>";
        }

        Vector2 input = (playerMovement != null) ? playerMovement.MoveInput : Vector2.zero;
        Vector3 actualVel = (characterController != null) ? characterController.velocity : Vector3.zero;
        actualVel.y = 0f;

        if (input.sqrMagnitude < 0.01f || actualVel.sqrMagnitude < 0.01f)
        {
            lastMovementStatus = "IDLE";
            lastCalculatedDriftAngle = 0f;
            lastFacingAngle = 0f;
            return;
        }

        Transform camTrans = mainCamera != null ? mainCamera.transform : (Camera.main != null ? Camera.main.transform : null);
        Vector3 camFwd = camTrans != null ? camTrans.forward : Vector3.forward;
        Vector3 camRight = camTrans != null ? camTrans.right : Vector3.right;
        camFwd.y = 0f; camRight.y = 0f;
        Vector3 intendedDir = (camFwd.normalized * input.y + camRight.normalized * input.x).normalized;

        Vector3 moveDir = actualVel.normalized;
        lastCalculatedDriftAngle = Vector3.Angle(intendedDir, moveDir);
        lastFacingAngle = Vector3.SignedAngle(playerTransform.forward, moveDir, Vector3.up);

        bool isPressingStraightW = Mathf.Abs(input.x) < 0.15f && input.y > 0.5f;

        if (isPressingStraightW)
        {
            if (lastCalculatedDriftAngle < 2.0f)
                lastMovementStatus = $"<color=#00FF66>[OK] STRAIGHT ({lastCalculatedDriftAngle:F1} deg)</color>";
            else
                lastMovementStatus = $"<color=#FF4444>[WARN] DRIFT ({lastCalculatedDriftAngle:F1} deg)</color>";
        }
        else
        {
            float inputAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
            lastMovementStatus = $"MOVING (Angle {inputAngle:F0} deg | Drift {lastCalculatedDriftAngle:F1} deg)";
        }

        Vector3 rayOrigin = playerTransform.position + Vector3.up * 0.2f;
        Debug.DrawRay(rayOrigin, playerTransform.forward * 2f, Color.blue);
        Debug.DrawRay(rayOrigin, moveDir * 2f, Color.green);
        Debug.DrawRay(rayOrigin, intendedDir * 2f, Color.red);
    }

    public void SpawnEnemy(GameObject prefab, bool asEliteBoss = false)
    {
        if (prefab == null) FindEnemyPrefabs();
        if (prefab == null || playerTransform == null) return;

        Vector3 spawnPos = playerTransform.position + (playerTransform.forward * spawnDistanceInFront);
        spawnPos.y = playerTransform.position.y;

        GameObject go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(-playerTransform.forward));
        if (asEliteBoss && go.TryGetComponent<EnemyController>(out var ec))
        {
            ec.ConfigureAsEliteOrBoss(true, "ELITE ENTITY (BOSS)");
        }
    }

    public void InstantStunNearestEnemy()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        EnemyController nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            float dist = Vector3.Distance(playerTransform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }

        if (nearest != null)
        {
            nearest.TriggerStun();
        }
    }

    public void KillAllEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead) enemy.ExecuteKill();
        }
    }

    public void FullHealPlayer()
    {
        if (playerHealth != null) playerHealth.HealPercentage(100f);
        if (playerStamina != null) playerStamina.RestoreFullStamina();
    }

    private void OnGUI()
    {
        if (!showOnScreenMenu) return;

        GUILayout.BeginArea(new Rect(20, 20, 240, 480), "DEBUG & TEST CONTROLS", GUI.skin.window);

        GUILayout.Label("<b>CAMERA PRESETS [C / 6]:</b>");
        GUI.color = currentCameraView == CameraViewMode.DefaultGameCamera ? Color.cyan : Color.white;
        if (GUILayout.Button("0. Default (Cinemachine)")) SetCameraView(CameraViewMode.DefaultGameCamera);

        GUI.color = currentCameraView == CameraViewMode.BehindNormal ? Color.cyan : Color.white;
        if (GUILayout.Button("1. Behind (4.5m)")) SetCameraView(CameraViewMode.BehindNormal);

        GUI.color = currentCameraView == CameraViewMode.ShoulderClose ? Color.cyan : Color.white;
        if (GUILayout.Button("2. Shoulder (2.0m)")) SetCameraView(CameraViewMode.ShoulderClose);

        GUI.color = currentCameraView == CameraViewMode.SideView90 ? Color.cyan : Color.white;
        if (GUILayout.Button("3. Side View (90 deg)")) SetCameraView(CameraViewMode.SideView90);

        GUI.color = currentCameraView == CameraViewMode.FrontView180 ? Color.cyan : Color.white;
        if (GUILayout.Button("4. Front View (180 deg)")) SetCameraView(CameraViewMode.FrontView180);

        GUI.color = currentCameraView == CameraViewMode.TopDown75 ? Color.cyan : Color.white;
        if (GUILayout.Button("5. Top-Down (75 deg)")) SetCameraView(CameraViewMode.TopDown75);
        GUI.color = Color.white;

        GUILayout.Space(8);
        GUILayout.Label("<b>SPAWN & COMBAT TEST:</b>");
        if (GUILayout.Button("[F1] Spawn Grunt")) SpawnEnemy(gruntPrefab, false);
        if (GUILayout.Button("[F2] Spawn Elite Boss")) SpawnEnemy(elitePrefab != null ? elitePrefab : gruntPrefab, true);

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("[F3] Instant Stun (Test F)")) InstantStunNearestEnemy();
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("[F4] Kill All Enemies")) KillAllEnemies();
        if (GUILayout.Button("[F5] Full Heal Player")) FullHealPlayer();

        GUILayout.Label("[~] Hide Menu | [Tab] Hide Diag", GUI.skin.box);
        GUILayout.EndArea();

        if (showMovementDiagnostics && playerTransform != null)
        {
            float panelWidth = 320;
            float panelHeight = 310;
            float posX = Screen.width - panelWidth - 20;

            GUILayout.BeginArea(new Rect(posX, 20, panelWidth, panelHeight), "MOVEMENT DIAGNOSTICS", GUI.skin.window);

            GUILayout.Label($"<b>Movement:</b>\n{lastMovementStatus}");
            GUILayout.Label($"<b>Posture:</b>\n{lastTiltStatus}");

            GUILayout.Space(4);

            float pitch = Mathf.DeltaAngle(0f, playerTransform.eulerAngles.x);
            float roll = Mathf.DeltaAngle(0f, playerTransform.eulerAngles.z);
            float yaw = playerTransform.eulerAngles.y;

            Vector2 input = (playerMovement != null) ? playerMovement.MoveInput : Vector2.zero;
            Vector3 vel = (characterController != null) ? characterController.velocity : Vector3.zero;
            float speedMag = new Vector2(vel.x, vel.z).magnitude;

            GUILayout.Box($"<b>ROTATION:</b>\n" +
                          $"- Pitch X: {pitch:F1} deg\n" +
                          $"- Yaw Y: {yaw:F1} deg\n" +
                          $"- Roll Z: {roll:F1} deg", GUILayout.ExpandWidth(true));

            float animSpeed = (playerAnimator != null) ? playerAnimator.GetFloat("Speed") : 0f;
            float animX = (playerAnimator != null) ? playerAnimator.GetFloat("x") : 0f;
            float animY = (playerAnimator != null) ? playerAnimator.GetFloat("y") : 0f;

            GUILayout.Box($"<b>VELOCITY & ANIMATOR:</b>\n" +
                          $"- Input: ({input.x:F2}, {input.y:F2})\n" +
                          $"- Speed: {speedMag:F2} m/s\n" +
                          $"- Facing Diff: {lastFacingAngle:F1} deg\n" +
                          $"- Anim: Speed={animSpeed:F2}, X={animX:F2}, Y={animY:F2}", GUILayout.ExpandWidth(true));

            GUILayout.EndArea();
        }
    }
}
