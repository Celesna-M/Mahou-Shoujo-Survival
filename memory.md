# Project Session Memory — Mahou-shoujo-Survival

**Last Updated:** 2026-11-24  
**Workspace Root:** `E:\Unity Setup\Mahou-shoujo-Survival`  
**Unity Stack:** Unity 6 (URP `17.5.0`, Cinemachine `3.1.7`, New Input System `1.20.0`)

---

## 1. Project Snapshot & Architecture Context

* **Multi-Scene Flow:** `MainMenu.unity` → `The Lobby.unity` → `StageMap.unity` → Combat Stages (`The Action.unity`, `Shibuya.unity`, and future stages).
* **Existing Persistent Systems (`DontDestroyOnLoad`):**
  * `GameProgressionManager.cs` (`Assets/_Project/Scripts/ProgressionAndEconomySystem/GameProgressionManager.cs`) — stores progression, character selection, and `MouseSensitivity` (`OnProgressionChanged` event).
  * `GameTextMasterConfig.cs` (`Assets/_Project/Scripts/UISystem/GameTextMasterConfig.cs`).
* **Player Architecture:**
  * Players are **dynamically spawned** when entering combat stages (not pre-placed in stages), supporting multiple selectable characters (existing and future prefabs).
  * Core player scripts in `Assets/_Project/Scripts/MovementSystem/`: `PlayerMovement.cs`, `PlayerLockOn.cs`, `PlayerStateMachine.cs`, `PlayerCharacterLoader.cs`.
* **Combat Camera Stack:**
  * URP `Main Camera` (`Camera`, `AudioListener`, `UniversalAdditionalCameraData`, `CinemachineBrain`)
  * `CM_FreeLook` (`CinemachineCamera`, `CinemachineOrbitalFollow`, `CinemachineInputAxisController`, `CinemachineDeoccluder`, `CinemachineRotationComposer`, `CinemachineImpulseListener`)
  * `CM_LockOn` (`CinemachineCamera`, `CinemachineThirdPersonFollow`, `CinemachineRotationComposer`, `CinemachineImpulseListener`)
  * Optional `CM_TargetGroup` (`CinemachineTargetGroup`)

---

## 2. Work Completed Today (2026-11-24)

### Feature: Persistent Cross-Stage Player Camera Rig (`PF_PlayerCameraRig`)
Approved through **Phase 1 → Phase 2 → Phase 3** with the following confirmed requirements:
* **Complete Rig Prefab (`1A`):** Bundles `Main Camera` + `CinemachineBrain` + `CM_FreeLook` + `CM_LockOn` + optional `CM_TargetGroup` under one persistent root.
* **Combat-Only Persistence (`2A`):** Uses `DontDestroyOnLoad` across all combat stages; automatically sleeps (`RigContent.SetActive(false)`) when entering non-combat UI scenes (`MainMenu`, `The Lobby`, `StageMap`) to avoid camera/`AudioListener` conflicts.
* **Dynamic Multi-Character Auto-Binding:** Automatically binds to any newly spawned character (existing or future) via `PlayerMovement.OnPlayerSpawned`, explicit `PersistentPlayerCameraRig.BindPlayer(Transform)`, or `"Player"` tag fallback. Auto-creates a `CameraTarget` pivot (`Y = 1.45`) on future characters that lack one, snaps camera position cleanly via `ForceCameraPosition`, and re-links `PlayerMovement`, `PlayerLockOn`, and `PlayerStateMachine`.
* **Duplicate Scene Camera Protection:** On every combat stage load, automatically disables scene-local `Camera`, `AudioListener`, and `CinemachineCamera` objects outside the persistent rig (leaves render-texture cameras untouched).
* **Live Mouse Sensitivity Sync:** Caches original `CinemachineInputAxisController` gains (preserving axis inversion) and scales live via `GameProgressionManager.Instance.MouseSensitivity` + `OnProgressionChanged`.

### Files Created
1. `Assets/_Project/Scripts/CameraSystem/PersistentPlayerCameraRig.cs` — `[DefaultExecutionOrder(-200)]` singleton controller implementing all features above (zero `Update`/`LateUpdate` allocations; 100% event-driven).

### Files Modified
1. `Assets/_Project/Scripts/MovementSystem/PlayerMovement.cs`
   * Added `public static event Action<Transform> OnPlayerSpawned;` fired in `Start()`.
   * Added `public void SetCameraTransform(Transform camTransform)`.
   * Updated `Awake()` to prefer `PersistentPlayerCameraRig.Instance.MainCamera` before falling back to `Camera.main`.
2. `Assets/_Project/Scripts/MovementSystem/PlayerLockOn.cs`
   * Added `public void BindCameraRig(Camera cam, CinemachineCamera freeLook, CinemachineCamera lockOn, CinemachineTargetGroup group)`.
   * Updated `Awake()` to prefer `PersistentPlayerCameraRig.Instance.MainCamera`.
3. `Assets/_Project/Scripts/MovementSystem/PlayerStateMachine.cs`
   * Added `public void SetMainCamera(Camera cam)`.
   * Updated `Awake()` and `MainCamera` property to prefer `PersistentPlayerCameraRig.Instance.MainCamera`.

---

## 3. Pending / Next Session Resume Checklist

* [ ] **Unity Editor Prefab Assembly:** Finish assembling and saving `Assets/_Project/Prefabs/Camera/PF_PlayerCameraRig.prefab` from `The Action.unity` following the Phase 3 Playbook (`PF_PlayerCameraRig` root → `RigContent` child → `Main Camera`, `CM_FreeLook`, `CM_LockOn`, optional `CM_TargetGroup`).
* [ ] **Wire Inspector Fields on `PF_PlayerCameraRig`:** Assign `RigContent`, `Main Camera`, `CM_FreeLook`, `CM_LockOn`, optional `CM_TargetGroup`, and `Input Axis Controller`. Clear `TrackingTarget` on `CM_FreeLook` & `CM_LockOn` before saving prefab.
* [ ] **Stage Placement & QA Testing:**
  * Place `PF_PlayerCameraRig.prefab` in `The Action.unity` and `Shibuya.unity` (and future stages).
  * Test stage transition (`The Action` → `Shibuya`), UI scene sleep (`The Lobby` / `StageMap`), dynamic player spawning across characters, Lock-On switching, and live Mouse Sensitivity slider.
* [ ] **Git Hygiene:** Commit `PersistentPlayerCameraRig.cs`, `PF_PlayerCameraRig.prefab` (+ `.meta` files), and the 3 updated `MovementSystem` scripts once verified in Unity Play Mode.

---

## 4. Session History Log

| Date | Summary | Key Files |
| :--- | :--- | :--- |
| 2026-11-24 | Designed & implemented Persistent Cross-Stage Player Camera Rig (`PF_PlayerCameraRig`) with dynamic multi-character auto-binding, UI-scene sleep mode, duplicate camera protection, and live mouse sensitivity sync. Added session memory workflow (`memory.md`). | `PersistentPlayerCameraRig.cs`, `PlayerMovement.cs`, `PlayerLockOn.cs`, `PlayerStateMachine.cs`, `memory.md`, `GEMINI.md` |
