/* ============================================================================
 * คู่มือสถาปัตยกรรมระบบทั้งหมดแบบแยกตามประเภท System (_AllSystemsArchitectureManual_TH.cs)
 * หมายเหตุ: ไฟล์นี้ทั้งไฟล์อยู่ในบล็อกคอมเมนต์ล้วน 100%
 * จึงไม่ถูกนำไปคอมไพเลอร์หรือรันในระบบเกมเด็ดขาด สร้างไว้เพื่อเปิดอ่านอ้างอิงโครงสร้างโปรเจกต์ได้ง่ายๆ
 * ============================================================================
 *
 * [ภาพรวมการแยกโฟลเดอร์ตามประเภท System ทั้ง 9 หมวด (รวม 29 สคริปต์) — ลบ Execution แล้วแทนด้วย Skill + แยกสายใกล้/ไกลชัดเจน]
 *
 * 1. CombatSystem/ (7 สคริปต์):
 *    - PlayerCombat (แยก Pipeline สายใกล้ vs สายไกล ไม่ทับกัน + Active Skill ปุ่ม E/F แทน Execution),
 *      PlayerAnimationEvents (hit / shoot / end / skill), PlayerHealth, MagicProjectile (VFX 3 จังหวะ + Lightning),
 *      HitStopManager, HitboxTriggerForwarder, IDamageable
 * 2. MovementSystem/ (5 สคริปต์):
 *    - PlayerCharacterLoader (Single-Shell สวมโมเดล 3D เปล่าๆ ไม่ต้องแปะ Component ซ้ำ), PlayerMovement,
 *      PlayerStamina, PlayerLockOn, PlayerAnimID (Base Animator Parameters: Speed, IsMoving, IsGrounded, InCombat, ComboStep, ArchetypeID, Attack, Skill, Dodge, Hurt, Dead)
 * 3. EnemyAISystem/ (3 สคริปต์):
 *    - EnemyController (Melee/Ranged 1.0s Aim Line + Boss 3 Patterns + Phase 2 <50% HP), EnemyStateMachine, EnemyAnimationEvents
 * 4. ProgressionAndEconomySystem/ (3 สคริปต์):
 *    - GameProgressionManager (Zero Energy Limit, Gold, MaxHP/ATK/SkillHaste, Active SO), LobbyManager, RewardDropParticle
 * 5. StageAndWaveSystem/ (2 สคริปต์):
 *    - StageMapManager (Core 3 Stages), WaveSpawner (ArenaManager + Pre/Post-Stage VN)
 * 6. VisualNovelSystem/ (1 สคริปต์):
 *    - VNManager (2D Portrait ซ้าย-ขวา, Dynamic Character Reaction, Click/Space/Skip/Auto 2.5s)
 * 7. DataSystem/ (3 สคริปต์):
 *    - CharacterData_SO, StageData_SO, DialogueData_SO
 * 8. UISystem/ (3 สคริปต์):
 *    - BattleHUD (Skill CD HUD + Floating Damage + Liminal Death Screen + Endgame Clear), MainMenuManager, GameTextMasterConfig
 * 9. CoreAndDebugSystem/ (2 สคริปต์):
 *    - CombatDebugTester, _AllSystemsArchitectureManual_TH
 * ============================================================================ */
