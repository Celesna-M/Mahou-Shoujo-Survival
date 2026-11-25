# Project Session Memory — Mahou-shoujo-Survival

**Last Updated:** 2026-11-25  
**Workspace Root:** `E:\Unity Setup\Mahou-shoujo-Survival`  
**Unity Stack:** Unity 6 (URP `17.5.0`, Cinemachine `3.1.7`, New Input System `1.20.0`)

---

## 1. Project Snapshot & Architecture Context

* **Multi-Scene Flow:** `MainMenu.unity` → `The Lobby.unity` → `StageMap.unity` → Combat Stages (`The Action.unity`, `Shibuya.unity`, and future stages).
* **Persistent Systems (`DontDestroyOnLoad`):**
  * `GameProgressionManager.cs` (`Assets/_Project/Scripts/ProgressionAndEconomySystem/GameProgressionManager.cs`) — stores progression, character selection (`ActiveCharacterData`, `SelectedCharacterIndex`, `SelectedCharacterArchetype`), upgrades, and `MouseSensitivity` (`OnProgressionChanged`).
  * `GameTextMasterConfig.cs` (`Assets/_Project/Scripts/UISystem/GameTextMasterConfig.cs`).
  * `PersistentPlayerCameraRig.cs` (`Assets/_Project/Scripts/CameraSystem/PersistentPlayerCameraRig.cs`) — combat-only persistent camera rig.
* **Single Source of Truth (Zero Hardcoded Overrides):**
  * `CombatDebugTester.enforceMasterConfig` is **`false` (`0`)** by default in both `The Action.unity` and `Shibuya.unity` so `CharacterData_SO`, `CombatSkillPattern_SO`, and Enemy Prefabs control all combat stats directly.
  * Player characters are dynamically mounted on `Player_Root` via `PlayerCharacterLoader.cs` (`-150` execution order), preserving Lobby-selected stats/archetype when no `CharacterData_SO` is assigned to a slot.
* **Standardized VFX Folder Layout:**
  * `Assets/_Project/Art/VFX/{Prefabs, Materials, Meshes, Textures, Shaders, Recipes}`
  * `Assets/_Project/Scripts/VFXSystem/` (+ `Editor/UnifiedVFXStudioBuilder.cs`)
  * `Assets/_Project/Data/{Characters, Dialogues, SkillPatterns, Stages, VFXLibraries}`

---

## 2. Work Completed Today (2026-11-25)

### A. Unified Pooled VFX Animation Event & Anchor Fix System (`1C`, `2B`, `3C`, `4: Yes/Yes`)
* **Fixed `VFXAnchorRig.cs`:** Replaced `~0` ground raycast mask (which hit actor self-colliders and floated ground VFX) with Zero-GC `Physics.RaycastNonAlloc` excluding `Player`/`Enemy`/`Ignore Raycast` + actor root. Added `WeaponRoot`, `WeaponTip`, `RightFoot`, `LeftFoot`, `CustomNamed` (auto-discovers `WeaponRoot`, `WeaponTip`, `VFX_*`), `VFXRotationMode` (4 modes), `VFXFollowMode` (including `FollowPositionOnly` to prevent hand-bone twist), and directional Scene View Gizmos.
* **Zero-GC Object Pooling:** Created `PooledVFXInstance.cs` and upgraded `UniversalVFXManager.cs` with `ObjectPool<PooledVFXInstance>` (`Pooled_VFX_Root`) + centralized zero-GC lifetime ticker.
* **Unified Player & Enemy Events:** Created `AnimVFXLibrary_SO.cs` and `AnimVFXController.cs`; added `PlayVFX(string)`, `PlayVFXPrefab(UnityEngine.Object)`, `PlayVFXEvent(AnimationEvent)`, `StopVFX(string)`, and `StopAllVFX()` on both `PlayerAnimationEvents.cs` and `EnemyAnimationEvents.cs` (plus root-animator forwarders on `EnemyController.cs`).
* **Interrupt Cancellation:** Auto-stops & returns interruptible VFX to pool on Player Dodge / `CancelCombat` / Hurt / Death and Enemy Stun / Death.

### B. VFX Folder Cleanup & Unified Future-Proof `VFX_Recipe` Builder (`1B`, `2B`, `3A`)
* Reorganized all active VFX assets into `Assets/_Project/Art/VFX/{Prefabs, Materials, Meshes, Textures, Shaders, Recipes}` and runtime scripts into `Assets/_Project/Scripts/VFXSystem/`, preserving every `.meta`/GUID pair (**0 orphan `.meta` files**).
* Moved `vfx haru.asset` → `Assets/_Project/Data/VFXLibraries/` and `NewCombatSkillPattern.asset` → `Assets/_Project/Data/SkillPatterns/`.
* Created `Assets/_Project/Scripts/VFXSystem/Editor/UnifiedVFXStudioBuilder.cs` combining all 3 legacy builders with smart local-first → central `Art/VFX/` fallback, GUID-safe in-place material/prefab updates, auto `PooledVFXInstance` attachment, single & batch `VFX_Recipe.json` menus, and Unified Lightning + Sample Patterns builder.
* Deleted `VFX_3D_Lightning_WhiteBlue_Pack/`, empty old pack shells, and verified unused ThirdParty Demo folders (`Vefects/Trails/Scene`, `Vefects/Trails/Scripts`, `Cartoon FX Remaster/Demo Assets`, `CFXRF Demo.unity`, `Welcome Screen`) — **~24.8 MB reclaimed**, 0 references broken.

### C. Fast Stylish Action (`1A`) Balance Overhaul + Ranged Multi-Tick Beam Skill
* **Ranged Beam Active Skill (`~2.4x` Total Burst):** Added `splitDamageAcrossDuration = true` on `CombatSkillPattern_SO`, updated `CombatBeamExecutor.cs` to divide `baseAtk × directDamageMultiplier` evenly across ticks (and pooled its `mainBodyVFXPrefab`), replaced `PlayerCombat`'s old 5-bolt Ranged Skill fallback with a pooled multi-tick beam, and fixed 3-bolt Ranged Finisher to split `rangedFinisherTotalDamageMultiplier` (`1.65x`) across shots.
* **Zero-Hardcode Inspector/SO Control:** Expanded `CharacterData_SO` with all combat/combo/finisher/beam/AoE fields; `PlayerCharacterLoader` injects them via `PlayerCombat.ApplyCharacterData(data)`. Set `CombatDebugTester.enforceMasterConfig = false` (`0`) in code and both combat scenes (`The Action.unity`, `Shibuya.unity`), and preserved `ApplyStageRankScaling()` if manually enabled.
* **Balanced Stats (Stage 1 / Rank E, Lv.0):**
  * **Players:** HIKARI (`MeleeStriker`) `110 HP / 24 ATK / 5.3 SPD`, AKANE (`HeavyBreaker`) `140 HP / 30 ATK / 4.8 SPD / 1.6x Stagger`, SHIRO & `haru.asset` (`RangedCaster`) `95 HP / 20 ATK / 5.1 SPD / 2.4x Beam Skill (6 ticks over 1.0s)`. Raw Power Hub upgrade tuned to `+4 ATK/lv`.
  * **Enemies:** `Shibahu_skechfab.prefab` (Normal) `95 HP / 14 ATK / +28% HP & +16% ATK per Rank`; `Asuka.prefab` (Boss) `650 HP / 24 ATK / 240 Max Stagger (1.0x mult, 4.5s stun) / +28% HP, +16% ATK, +20% Stagger per Rank`.
  * **Pattern Asset:** Fixed `NewCombatSkillPattern.asset` (`patternPreset = 20` `ContinuousChannelBeam`, `directDamageMultiplier = 2.4`, `splitDamageAcrossDuration = 1`, `activeDuration = 1.0`, `damageTickInterval = 0.2`) and linked it to `haru.asset.activeSkillPattern`.

### D. `The Lobby` Fix & Cleanup (`1B`, Energy Removal, `2A`, `3C`)
* **Fixed Lobby → Combat Sync Bug:** `GameProgressionManager.SelectCharacter(...)` now clears stale `ActiveCharacterData` and tracks `SelectedCharacterArchetype` cleanly; `SetActiveCharacterData(data, slotIndex)` keeps real slot index; `PlayerCharacterLoader` never overwrites a Lobby-selected character's stats/archetype with `fallbackCharacterData` (borrows fallback 3D model only when no SO exists, plus optional `characterRosterLookup[]`).
* **Cleaned `LobbyManager.cs`:** Removed dead Energy fields/methods & per-frame polling, added per-card `characterData` + `combatArchetype`, fixed `OnResetSaveDataClicked()` to reset card selection + slider knobs, upgraded Mascot Dialogue to zero-GC `TMP.maxVisibleCharacters` typewriter, and turned off `appendItemLoreUnderUpgrades` by default.
* **Tidied `The Lobby.unity`:** Reset `LobbyManager` Transform from `(2720.85, 0, 127812.22)` → `(0, 0, 0)`, disabled legacy `Text_Energy` & `Button_RefillEnergy`, trimmed all 7 trailing-space GameObject names, disabled `appendItemLoreUnderUpgrades`, and set `combatArchetype` (`0/1/2`) + balanced stats on the 3 character cards.

---

## 3. Pending / Next Session Resume Checklist

* [ ] **Unity Editor Reimport & Console Check:** Focus Unity Editor to compile new/moved scripts (`AnimVFXLibrary_SO`, `PooledVFXInstance`, `AnimVFXController`, `UnifiedVFXStudioBuilder`, moved `VFXSystem` scripts) and confirm **0 Console errors / 0 pink materials**.
* [ ] **Play Mode QA — VFX Animation Events:** Test `PlayVFX("Key")` / `PlayVFXPrefab` / `PlayVFXEvent` in Play Mode; verify `GroundFeet` floor snap, `FollowPositionOnly`, `Pooled_VFX_Root` recycling, and Dodge/Stun cancellation.
* [ ] **Play Mode QA — Balance & Beam Skill:** Test `HARU` `[E]/[F]` Lightning HyperLaser Beam (`6 × 8 = 48` total dmg at 20 ATK), `Shibahu` (`95 HP / 14 ATK`), and `Asuka` Boss (`650 HP / 240 Stagger / 24 ATK`).
* [ ] **Play Mode QA — Lobby → Combat Sync:** Select Card 0 (`HIKARI`), Card 1 (`AKANE`), and Card 2 (`SHIRO/HARU`) in `The Lobby` and verify each carries its exact HP/ATK/Archetype into combat without `haru.asset` overwriting them. Optionally delete disabled `Text_Energy`/`Button_RefillEnergy` in `The Lobby.unity` and link `haru.asset` to Card 2's `Character Data` slot.
* [ ] **Camera Rig (`PF_PlayerCameraRig`) Pending from 2026-11-24:** Finish assembling/saving `Assets/_Project/Prefabs/Camera/PF_PlayerCameraRig.prefab`, place in `The Action.unity` & `Shibuya.unity`, and test cross-stage transitions & UI scene sleep mode.
* [ ] **Git Hygiene (Smart Slicing):** Commit logically by system (1. Camera Rig, 2. VFX Anim Events & Pooling, 3. VFX Folder Cleanup & Recipe Builder, 4. Balance & Beam Skill, 5. Lobby Cleanup) with every `.meta` file paired.

---

## 4. Session History Log

| Date | Summary | Key Files |
| :--- | :--- | :--- |
| 2026-11-24 | Designed & implemented Persistent Cross-Stage Player Camera Rig (`PF_PlayerCameraRig`) with dynamic multi-character auto-binding, UI-scene sleep mode, duplicate camera protection, and live mouse sensitivity sync. Added session memory workflow (`memory.md`). | `PersistentPlayerCameraRig.cs`, `PlayerMovement.cs`, `PlayerLockOn.cs`, `PlayerStateMachine.cs`, `memory.md`, `GEMINI.md` |
| 2026-11-25 | Built unified Pooled VFX Animation Event & Anchor Fix system (Player + Enemy + Interrupt Cancel); cleaned & categorized `Art/VFX` + trimmed ~24.8 MB ThirdParty demos + built unified future-proof `UnifiedVFXStudioBuilder.cs`; rebalanced Player/Enemy HP & ATK (`1A`), converted Ranged Skill to multi-tick `2.4x` Beam, made Character/Skill SOs Single Source of Truth (`enforceMasterConfig = false`); fixed Lobby → Combat character sync bug & cleaned `LobbyManager.cs` + `The Lobby.unity`. | `AnimVFXLibrary_SO.cs`, `PooledVFXInstance.cs`, `AnimVFXController.cs`, `VFXAnchorRig.cs`, `UniversalVFXManager.cs`, `UnifiedVFXStudioBuilder.cs`, `CombatBeamExecutor.cs`, `CombatSkillPattern_SO.cs`, `CharacterData_SO.cs`, `PlayerCombat.cs`, `PlayerCharacterLoader.cs`, `GameProgressionManager.cs`, `LobbyManager.cs`, `CombatDebugTester.cs`, `The Lobby.unity`, `The Action.unity`, `Shibuya.unity`, `haru.asset`, `NewCombatSkillPattern.asset`, `Shibahu_skechfab.prefab`, `Asuka.prefab` |
