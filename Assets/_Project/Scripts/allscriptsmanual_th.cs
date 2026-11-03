/* ============================================================================
 * คู่มือรวมคำอธิบายโค้ดและฟังก์ชันทั้งหมดในโปรเจกต์
 * ============================================================================

# คู่มือรวมคำอธิบายโค้ดและฟังก์ชันทั้งหมดในโปรเจกต์ (All Scripts & Functions Reference)

เอกสารนี้รวบรวมคำอธิบายการทำงานของสคริปต์และฟังก์ชันทั้งหมด **20 ไฟล์** ในโปรเจกต์ **Mahou Shoujo (3D Action Brawler)** แบ่งตามหมวดหมู่โฟลเดอร์ (`Combat`, `Enemy`, `Managers`, `Player`, `UI`) เพื่อให้อ่านทบทวนหรือค้นหาจุดแก้ไขได้ง่ายในที่เดียว โดยข้อความทั้งหมดถูกซ่อนไว้ในบล็อกคอมเมนต์ (`/ * ... * /`) จึงไม่มีผลกระทบต่อระบบคอมไพล์ของ Unity ค่ะ

## ตารางสารบัญสคริปต์ทั้งหมด (Quick Index)

| โฟลเดอร์ (Folder) | ชื่อไฟล์สคริปต์ (Script File) | หน้าที่หลักโดยสรุป |
| :--- | :--- | :--- |
| **Combat** | `IDamageable.cs` | อินเทอร์เฟซมาตรฐานสำหรับออบเจกต์ที่รับดาเมจได้ (`TakeDamage`, `IsDead`) |
| **Combat** | `IExecutable.cs` | อินเทอร์เฟซสำหรับบอส/Elite ที่เกราะแตกติด Stun และรอให้กด `[F]` สังหาร |
| **Combat** | `MagicProjectile.cs` | ระบบกระสุนเวทมนตร์เลี้ยวตามเป้า (Soft Homing) และระเบิดดาเมจรอบข้าง (Splash AoE) |
| **Enemy** | `EnemyController.cs` | AI มอนสเตอร์/บอส, ระบบ Hitbox Event `List<string>` (`hite`/`end`), และระบบกันชนผู้เล่นจมแมพ |
| **Managers** | `CombatDebugTester.cs` | ศูนย์กลางปรับสมดุลเกม (Central Master Config) และแผงควบคุมเสกมอน/ปรับค่าขณะเล่น |
| **Managers** | `GameTextMasterConfig.cs` | ศูนย์กลางรวมข้อความ Auto-Text ทุกประโยคในเกมให้แก้ไขได้จาก Inspector ที่เดียว |
| **Managers** | `HitStopManager.cs` | ระบบชะลอเวลาชั่วขณะ (Hit-stop) ตอนฟันหรือต่อยโดนศัตรูเพื่อเพิ่มน้ำหนักการปะทะ |
| **Managers** | `LobbyManager.cs` | จัดการฉากห้องพัก (The Hub), เลือกตัวละคร, และระบบอัปเกรดสเตตัสถาวร 3 สาย |
| **Managers** | `MainMenuManager.cs` | จัดการหน้าเมนูหลัก (Main Menu), ปรับระดับเสียง, ความไวเมาส์, และลบเซฟ |
| **Managers** | `StageMapManager.cs` | จัดการแผนที่เลือกด่าน (`Unrestricted` แบบมีขอบเขตล็อก), โหนดด่านกดได้ทุกจุด, และตั้งค่าคำอธิบายด่าน |
| **Managers** | `WaveSpawner.cs` | ระบบทยอยปล่อยมอนสเตอร์/บอสตามโควตาด่าน และเปลี่ยนแสงสีบรรยากาศฉากตาม Rank E–S |
| **Player** | `PlayerAnimID.cs` | เก็บค่า Hash ของพารามิเตอร์ Animator ทั้งหมดเพื่อลดภาระ CPU |
| **Player** | `PlayerAnimationEvents.cs` | ตัวรับ Animation Event ฝั่งผู้เล่นด้วย `List<string>` (`hit`/`end`) เพิ่มลดชื่อเองได้ใน Inspector |
| **Player** | `PlayerCombat.cs` | ระบบโจมตีระยะประชิด 3 หมัดคอมโบ, เปิด/ปิด Hitbox, Hit-stop, และสั่นกล้อง |
| **Player** | `PlayerExecution.cs` | ระบบกดปุ่ม `[F]` ทำท่าสังหารบอสที่ติด Stun พร้อมเป็นอมตะ (I-Frame) และฟื้นฟูเลือด |
| **Player** | `PlayerHealth.cs` | ระบบเลือดผู้เล่น และคลาส `GameProgressionManager` (เซฟเกม, Rank E–S, อัปเกรด 3 สาย, Milestone) |
| **Player** | `PlayerLockOn.cs` | ระบบล็อกเป้าศัตรู (`Tab` / คลิกกลาง) พร้อมแสดงวงแหวนเป้าล็อกบนตัวมอนสเตอร์ |
| **Player** | `PlayerMovement.cs` | ระบบเดิน/วิ่ง, กลิ้งหลบติด I-Frame, กันกล้องทะลุกำแพง, และล็อกระดับพื้นป้องกันตกแมพ |
| **Player** | `PlayerStamina.cs` | ระบบหลอด Stamina และการฟื้นฟูอัตโนมัติ (บวกโบนัสจากสายอัปเกรด Stamina Efficiency) |
| **UI** | `BattleHUD.cs` | จัดการหน้าจอ UI ในฉากต่อสู้, หลอดเลือด/Stamina, ป้าย `[F] EXECUTE`, และหน้าสรุปผลชนะ |

## หมวดที่ 1: โฟลเดอร์ `Combat/` (ระบบการปะทะและอินเทอร์เฟซต่อสู้)

### 1.1 `IDamageable.cs`
* **`TakeDamage(float amount, Vector3 hitPoint)`**: สัญญาฟังก์ชันมาตรฐานสำหรับทุกออบเจกต์ที่รับดาเมจได้ (ทั้งผู้เล่นและมอนสเตอร์) โดยรับค่าความเสียหายและพิกัดจุดที่ถูกโจมตี
* **`IsDead { get; }`**: คืนค่าสถานะว่าออบเจกต์นั้นตายหรือยัง เพื่อป้องกันการตีซ้ำศพหรือการคิดดาเมจซ้อน

### 1.2 `IExecutable.cs`
* **`CanBeExecuted { get; }`**: ตรวจสอบว่าศัตรูระดับ Elite/Boss ตัวนั้นเกราะแตก (Poise/Stagger หมด) และติดสถานะมึนงง (Stun) พร้อมให้กดปุ่ม `[F]` สังหารหรือยัง
* **`Transform { get; }`**: คืนค่า `Transform` ของเป้าหมาย เพื่อให้ระบบท่าสังหาร (`PlayerExecution`) ดึงพิกัดมาล็อกตำแหน่งยืนของผู้เล่น
* **`OnExecutionStart()`**: ล็อกสถานะศัตรูทันทีที่ผู้เล่นเริ่มกดท่าสังหาร เพื่อไม่ให้ศัตรูหลุด Stun หรือขยับหนีระหว่างออกท่า
* **`ExecuteKill()`**: สั่งปลิดชีพศัตรูทันทีในจังหวะที่หมัด/อาวุธของผู้เล่นปะทะเป้าหมาย

### 1.3 `MagicProjectile.cs`
* **`Awake()`**: ดึง `CinemachineImpulseSource` และตั้งค่า `SphereCollider` ให้เป็น `isTrigger = true`
* **`Initialize(GameObject caster, Transform target, float dmg, float splashDmg, float splashRad)`**: ตั้งค่าข้อมูลเริ่มต้นของกระสุนเวท (ผู้ยิง, เป้าหมาย Homing, ดาเมจตรง, ดาเมจระเบิดรอบข้าง และรัศมี)
* **`Update()`**: ควบคุมการเคลื่อนที่ของกระสุนเวท ระบบเลี้ยวเข้าหาเป้าหมายอัตโนมัติ (Soft Homing) และการตรวจจับการชนด้วย `SphereCast`
* **`OnTriggerEnter(Collider other)`**: ตรวจจับเมื่อกระสุนชนเป้าหมายหรือสิ่งกีดขวาง แล้วสั่งระเบิดทำงาน
* **`Explode(Vector3 hitPoint, Collider directHitCollider)`**: สร้างดาเมจใส่เป้าหมายหลักและดาเมจลูกหลง (Splash AoE) ใส่ศัตรูรอบข้าง พร้อมเล่นเอฟเฟกต์ VFX, Hit-stop และสั่นหน้าจอแล้วทำลายกระสุนทิ้ง

## หมวดที่ 2: โฟลเดอร์ `Enemy/` (ระบบ AI มอนสเตอร์และบอส)

### 2.1 `EnemyController.cs`
* **`Awake()` & `EnsureDefaultEventLists()`**:
  * เตรียมค่าเริ่มต้นของมอนสเตอร์ ดึงคอมโพเนนต์ `NavMeshAgent`, `Animator`, `Renderer` และปิด `applyRootMotion` เพื่อไม่ให้แอนิเมชันดึงตัวมอนสเตอร์จมพื้น
  * ตั้งค่ารายการชื่อ Animation Event เริ่มต้นใน `List<string>` บน Inspector:
    * `openHitboxEventNames = ["hite"]` (สำหรับเปิด Hitbox และทำดาเมจ)
    * `closeHitboxEventNames = ["end"]` (สำหรับปิด Hitbox)
* **`Start()` & `IgnorePhysicalPushAgainstPlayer()`**:
  * ตั้งค่าระยะหยุดของ `NavMeshAgent` (`stoppingDistance`) ไม่ให้เดินชนซ้อนทับตัวผู้เล่น
  * สั่ง `Physics.IgnoreCollision` ระหว่าง `CharacterController` ของผู้เล่น กับ Collider ลูกบนกระดูกแขน/อาวุธของมอนสเตอร์ เพื่อป้องกันไม่ให้แขนของมอนสเตอร์ตบผู้เล่นจมทะลุพื้นแมพ
* **`SetupWeaponHitboxAndPreventSolidBonePush()`**:
  * บังคับให้ Collider ลูกทุกชิ้นบนตัวมอนสเตอร์ (เช่น มือ, แขน, อาวุธ) เป็น Trigger (`isTrigger = true`)
  * ผูก `HitboxTriggerForwarder` เข้ากับก้อน Hitbox เพื่อส่งข้อมูลการชนกลับมาหา `EnemyController`
* **`BindCustomAnimationClipEvents()` & `OnCustomEnemyAnimEvent(string eventName)`**:
  * สแกนคลิปแอนิเมชันทั้งหมดใน `Animator` อัตโนมัติ หากคุณกด `+` เพิ่มชื่อ Event ใหม่เข้าไปใน List บน Inspector (นอกเหนือจาก `hite` / `end`) ระบบจะเชื่อมชื่อ Event นั้นเข้ากับฟังก์ชันเปิด/ปิด Hitbox ให้อัตโนมัติโดยไม่ต้องเขียนโค้ดเพิ่ม
* **`hite(int hitIndex)` & `ExecuteOpenHitbox(int hitIndex)`**:
  * ถูกเรียกเมื่อแอนิเมชันโจมตีของมอนสเตอร์เล่นถึงเฟรมที่ปักหมุด Event `"hite"` โดยจะเช็กว่าชื่อ `"hite"` อยู่ใน `openHitboxEventNames` หรือไม่ ถ้าอยู่จะเปิดใช้งานก้อน Hitbox ตั้งเวลาหน้าต่างดาเมจ (`activeHitWindowDuration = 0.25` วินาที) และสแกนทำดาเมจทันที
* **`end()` & `ExecuteCloseHitbox()`**:
  * ถูกเรียกเมื่อแอนิเมชันเล่นถึงหมุด Event `"end"` (หรือเมื่อหมดเวลาหน้าต่างดาเมจ 0.25 วินาที) เพื่อปิดก้อน Hitbox ทั้งหมดทันที
* **`ScanActiveHitboxesAndAttackArc()` & `DealDamageToPlayerOnce()`**:
  * ทำงานทุกเฟรมในช่วงที่ Hitbox เปิดอยู่ ตรวจจับการโดนตัวผู้เล่นทั้งจากก้อน Hitbox (`OverlapSphere`) และระยะแนวราบ XZ ด้านหน้ามอนสเตอร์ พร้อมล็อก `hasDealtDamageThisSwing = true` เพื่อการันตีว่าต่อย 1 หมัด เลือดลดแค่ 1 ครั้ง
* **`MoveTo(Vector3 destination)` & `GetHorizontalDistanceToPlayer()`**:
  * คำนวณระยะห่างแนวราบ (แกน X, Z ไม่รวมความสูง Y) เพื่อให้เช็กระยะโจมตีแม่นยำ และหยุดเดินทันทีเมื่อเข้าใกล้ผู้เล่นถึงระยะ `minBodySeparationDistance` (1.15m)
* **`TakeDamage(float amount, Vector3 hitPoint)` & `TriggerStun()`**:
  * ลดเลือดมอนสเตอร์เมื่อถูกผู้เล่นโจมตี และลดเกจ Stagger สำหรับบอส/Elite
  * หากมอนสเตอร์กำลังออกท่าโจมตีอยู่ (`isAttacking == true`) จะกะพริบตัวสีแดงแต่ไม่สั่งเล่นท่า `Hurt` แทรก เพื่อไม่ให้แอนิเมชันโจมตีของมอนสเตอร์ถูกตัดทิ้งก่อนถึงเฟรมทำดาเมจ
  * หากเกจ Stagger ของบอสลดเหลือ 0 จะเข้าสู่สถานะ Stun (ตัวสีเหลืองทอง) รอให้ผู้เล่นกดปุ่ม `[F]` สังหาร
* **`Die()` & `RewardDropParticle`**:
  * เมื่อเลือดหมด จะหยุดแอนิเมชันทันที บันทึกจำนวนการสังหารเข้า `GameProgressionManager` และเสกลูกแก้วพลังงาน `Star Dust` / `Shattered Core` ให้ลอยพุ่งเข้าหาตัวผู้เล่น

## หมวดที่ 3: โฟลเดอร์ `Managers/` (ระบบจัดการฉาก สมดุลเกม และข้อความกลาง)

### 3.1 `CombatDebugTester.cs`
* **`Awake()`, `OnSceneLoaded()` & `EnforceMasterConfigOnCurrentScene()`**:
  * ทำงานเป็นอันดับแรกสุดของเกม (`[DefaultExecutionOrder(-5000)]`) และอยู่ข้ามทุกฉากด้วย `DontDestroyOnLoad`
  * เมื่อเปิด `enforceMasterConfig = true` จะทำหน้าที่เป็นศูนย์กลางสมดุลเกม (Central Master Config) ที่ส่งค่าสเตตัสจาก Inspector นี้ไปทับสคริปต์อื่นๆ ในทุกฉากโดยไม่ยุ่งกับพื้นที่หรือ Collider ในฉาก
* **`EnforceMasterConfigOnPlayer()`**:
  * คำนวณค่าเลือดสูงสุด (`Base HP + Vitality Boost`), ดาเมจ (`Base ATK + Raw Power`), และค่า Stamina (`Base Regen/Dodge Cost + Stamina Efficiency`) แล้วส่งไปตั้งค่าใน `PlayerMovement`, `PlayerHealth`, `PlayerStamina`, `PlayerCombat`, และ `PlayerExecution`
* **`EnforceMasterConfigOnWaveSpawner()` & `EnforceMasterConfigOnNewEnemies()`**:
  * ตั้งค่าความถี่การเกิดของมอนสเตอร์ (`trickleSpawnInterval`) และจำนวนศัตรูสูงสุดในฉาก (`maxConcurrentEnemies`)
  * ตรวจจับมอนสเตอร์และบอสทุกตัวที่เพิ่งเกิดในฉาก (ผ่าน `HashSet<EnemyController>` ที่รองรับ Unity 6.3) แล้วยัดค่าเลือด ความเร็ว ดาเมจ ระยะตี เกราะ Stagger และอัตราดรอปแร่จากศูนย์กลางนี้ทับทันที
* **`OnGUI()` & `HandleHotkeys()`**:
  * แสดงปุ่มเปิด/ปิดแผงควบคุม Master Hub มุมซ้ายบน (หรือกดปุ่ม `~` / `F10` บนคีย์บอร์ด) มี 4 แท็บสำหรับเปิดโหมดอมตะ (God Mode), ตีทีเดียวตาย (One-Hit Kill), เสกมอนสเตอร์/บอส `[F1-F4]`, ปั๊มแร่/อัปเกรด 3 สาย, ปลดล็อก Rank E–S, ปรับความเร็วเกม `Time.timeScale`, และวาร์ปข้ามทั้ง 4 ฉาก

### 3.2 `GameTextMasterConfig.cs`
* **`AutoCreateOrHookTextMaster()` (`[RuntimeInitializeOnLoadMethod]`)**:
  * ทำงานอัตโนมัติเมื่อเริ่มเกมโดยไม่ต้องลากสคริปต์ไปแปะตามปุ่มหรือ Text รายชิ้น หากยังไม่มี `GameTextMasterConfig` ในฉาก จะสร้างหรือแปะเข้ากับออบเจกต์ศูนย์กลางให้อัตโนมัติและคงอยู่ข้ามทุกฉาก
* **`SyncInspectorToStaticFields()` & `OnValidate()`**:
  * ดึงข้อความทั้งหมดที่คุณพิมพ์แก้ไขในหน้าต่าง Unity Inspector (หมวด 0 ถึง 8) ไปอัปเดตลงตัวแปร Static กลางที่สคริปต์อื่นๆ (`LobbyManager`, `StageMapManager`, `WaveSpawner`, `BattleHUD`, `PlayerHealth`) เรียกใช้ และแสดงผลทันทีแบบเรียลไทม์
* **`ApplyAllTextOverridesToCurrentScene()`**:
  * ส่งชื่อตัวละคร คำอธิบายตัวละคร ชื่อด่าน และคำอธิบายด่านจาก Inspector นี้ไปอัปเดตใน `LobbyManager`, `StageMapManager`, และ `CombatDebugTester` อัตโนมัติทุกครั้งที่โหลดฉาก

### 3.3 `HitStopManager.cs`
* **`Awake()` / `OnDestroy()`**: จัดการ Singleton Instance ของ `HitStopManager` และคืนค่า `Time.timeScale = 1.0f` เสมอเมื่อถูกทำลาย
* **`Trigger(float duration, float hitTimeScale)` / `TriggerHitStop(...)`**: สั่งชะลอหรือหยุดเวลาชั่วขณะ (Hit-stop) เมื่อการโจมตีปะทะเป้าหมาย เพื่อเพิ่มน้ำหนักและความสะใจในการต่อสู้
* **`HitStopRoutine(float duration, float hitTimeScale)`**: Coroutine นับเวลาจริงแบบไม่ขึ้นกับ TimeScale (`WaitForSecondsRealtime`) แล้วคืนค่าความเร็วเวลาของเกมกลับเป็น `1.0f` เมื่อครบกำหนด

### 3.4 `LobbyManager.cs`
* **`Start()` & `ConfigureMouseScrollRect()`**:
  * โหลดข้อมูลเซฟจาก `GameProgressionManager` และตั้งค่า `ScrollRect` สำหรับใช้เมาส์ลากหรือหมุนลูกกลิ้งเลือกตัวละครซ้าย-ขวา พร้อมผูก `ScrollRectDragForwarder` เข้ากับการ์ดตัวละครแต่ละใบ
* **`SelectCharacterByIndex(int index)`**:
  * เลือกตัวละครตามลำดับ (`0 = HIKARI`, `1 = AKANE`, `2 = SHIRO`) บันทึกสเตตัสพื้นฐานเข้า `GameProgressionManager` และเปลี่ยนสี/ขนาดการ์ดที่ถูกเลือกให้โดดเด่นขึ้น
* **`OnUpgradeMaxHpClicked()`, `OnUpgradeStaminaClicked()`, `OnUpgradeAttackClicked()`**:
  * ฟังก์ชันสำหรับปุ่มอัปเกรดถาวรทั้ง 3 สายใน Hub:
    1. **Vitality Boost**: ใช้ `Star Dust` เพิ่มเลือดสูงสุด (Max HP)
    2. **Stamina Efficiency**: ใช้ `Star Dust` เพิ่มความเร็วรีเจน Stamina และลดการใช้ Stamina ตอนกลิ้งหลบ
    3. **Raw Power**: ใช้ `Shattered Core` เพิ่มดาเมจโจมตีพื้นฐาน (Attack Damage)
* **`RefreshHubUI()` & `ApplyMilestoneThemeColorShift()`**:
  * อัปเดตตัวเลข `Star Dust`, `Shattered Core`, พลังงาน (`Play Energy`), ระดับ Rank (E ถึง S), เนื้อเรื่อง 1 บรรทัด, และข้อความลับ (Milestone Flavor Text) ขึ้นหน้าจอ พร้อมเปลี่ยนโทนสีของปุ่มหลักและกรอบ UI จากชมพูอ่อนเป็นแดงเข้มตามระดับ Milestone

### 3.5 `MainMenuManager.cs`
* **`Start()` & `BindUIEvents()`**: รีเซ็ต `TimeScale = 1.0f`, ปลดล็อกเมาส์, โหลดข้อมูลเซฟจาก `GameProgressionManager` และเชื่อมโยงปุ่ม `Start`, `Settings`, `Quit`, `Fullscreen`, `Reset Save` รวมถึงแถบเลื่อน `Slider` ปรับเสียง/ความไวเมาส์
* **`OnStartGameClicked()`**: โหลดเข้าสู่ฉากห้องพัก (`The lobby`)
* **`OpenSettingsPanel()` / `CloseSettingsPanel()`**: เปิด/ปิดหน้าต่างตั้งค่า (Settings Panel)
* **`OnVolumeSliderChanged(float val)` / `OnSensitivitySliderChanged(float val)`**: บันทึกค่าระดับเสียงหลักและความไวเมาส์ลงใน `GameProgressionManager` ทันทีที่ผู้เล่นลากแถบเลื่อน
* **`OnToggleFullscreenClicked()` & `OnResetSaveDataClicked()`**: สลับโหมดหน้าจอ Fullscreen/Windowed และล้างข้อมูลเซฟทั้งหมดกลับเป็นค่าเริ่มต้น (Rank E)

### 3.6 `StageMapManager.cs`
* **`MapNodeClickAndDragHandler` (คลาสย่อยสำหรับปุ่มโหนดด่าน)**:
  * แยกแยะระหว่าง "การคลิกเลือกโหนดด่าน" (เมาส์ขยับไม่เกิน 12 พิกเซล) กับ "การคลิกค้างลากแผนที่" ทำให้ผู้เล่นคลิกซ้ายที่โหนดเพื่อเลือกด่าน ดับเบิลคลิกเพื่อเข้าด่าน หรือคลิกค้างบนโหนดแล้วลากเลื่อนแผนที่ได้ลื่นไหล
* **`ConfigureUnrestrictedScrollRectAndBounds()` & `ClampMapContentPositionWithinBounds()`**:
  * บังคับให้ `ScrollRect` ของแผนที่เป็นโหมด `Unrestricted` เพื่อแก้ปัญหาแผนที่ดีดกลับไปจุด `(0,0)` และคำนวณขอบเขตการเลื่อนซ้าย-ขวา (`minMapPanX` ถึง `maxMapPanX`) และบน-ล่าง (`minMapPanY` ถึง `maxMapPanY`) จากตำแหน่งปุ่ม `StageNode` ทั้งหมดโดยอัตโนมัติ
* **`AutoFindAndBindStageNodeButtons()`**:
  * ค้นหาปุ่ม `StageNode_01` ถึง `StageNode_06` ในฉากมาผูกเข้ากับรายการ `stageNodes` อัตโนมัติ และบังคับเปิด `interactable = true` กับ `raycastTarget = true` เพื่อให้กดโหนดด่านได้ครบทุกจุด 100%
* **`SelectStageNode(int index)`**:
  * อัปเดตชื่อด่านและคำอธิบายด่านเมื่อผู้เล่นคลิกเลือกโหนดบนแผนที่ โดยนำตัวเลือกในหมวด `3. Stage Detail Auto-Text Format` บน Inspector (เช่น ป้าย `[FIRST-CLEAR BONUS]`, จำนวน `(Normal | Boss)`, และบรรทัดเนื้อเรื่อง `STORY [RANK X]`) มาประกอบแสดงผลตามที่ติ๊กเปิด/ปิดไว้
* **`DeploySelectedStage()`**:
  * ตรวจสอบและหักค่าพลังงาน (`Play Energy`) ก่อนบันทึกข้อมูลด่านที่เลือกเข้า `GameProgressionManager` แล้วโหลดเข้าสู่ฉากต่อสู้ (`The Action`)

### 3.7 `WaveSpawner.cs`
* **`ApplyRankEnvironmentAndAtmosphere()`**:
  * สลับชุดฉากหลัง (`rankEnvironmentSets`) และเปลี่ยนสีแสงไฟบรรยากาศในฉากต่อสู้ (`Directional Light` & `Ambient Light`) ตามระดับ Rank ของด่านที่เลือก (ตั้งแต่แสงสลัวห้องเรียนร้าง Rank E ไปจนถึงสีแดงเข้มแห่งห้วงลึก Rank S) โดยไม่ยุ่งหรือแก้ไข Collider ใดๆ ในฉาก
* **`ContinuousTrickleSpawnRoutine()`**:
  * ระบบทยอยปล่อยมอนสเตอร์อย่างต่อเนื่องตามโควตาของด่านที่เลือกจาก `StageMap` รักษาจำนวนมอนสเตอร์ในฉากไม่ให้เกิน `maxConcurrentEnemies` และปล่อยบอสประจำ Rank ออกมาในช่วงท้าย
* **`GetRandomSpawnPoint()` & `SpawnEnemyInArena()`**:
  * สุ่มจุดเกิดของมอนสเตอร์จากอาร์เรย์ `spawnPoints` และตั้งค่าบอสให้เปิดระบบเกจ Stagger พร้อมแสดงชื่อบอสประจำ Rank บนหลอดเลือดด้านบนจอ
* **`Update()`, `CleanupDeadEnemies()` & `StageClearDelayRoutine()`**:
  * ตรวจสอบสถานะมอนสเตอร์ในฉากทุกเฟรม เมื่อมอนสเตอร์และบอสถูกกำจัดครบตามโควตาแล้ว จะสั่งบันทึกการผ่านด่าน (`MarkCurrentStageCleared`) แสดงป้าย `STAGE CLEAR!` พร้อมโบนัสเคลียร์ครั้งแรก แล้วเปิดหน้าจอสรุปผลชนะ (`Victory Screen`)

## หมวดที่ 4: โฟลเดอร์ `Player/` (ระบบควบคุมผู้เล่น การต่อสู้ และความก้าวหน้า)

### 4.1 `PlayerAnimID.cs`
* **`PlayerAnimID` (Static Class)**: แปลงชื่อ Parameter ทั้งหมดของ `Animator` (`Speed`, `IsGrounded`, `Jump`, `Dodge`, `Attack`, `Hurt`, `Dead`, `Execute`, `InCombat`) เป็นค่าตัวเลข Hash ล่วงหน้าด้วย `Animator.StringToHash` เพื่อลดภาระ CPU ในทุกเฟรม

### 4.2 `PlayerAnimationEvents.cs`
* **`openHitboxEventNames` & `closeHitboxEventNames` (`List<string>`)**:
  * รายการชื่อ Animation Event ของฝั่งผู้เล่นที่สามารถกด `+` เพิ่ม หรือกด `-` ลบได้ใน Inspector โดยค่าเริ่มต้นคือ:
    * `openHitboxEventNames = ["hit"]` (เปิด Hitbox หมัด/อาวุธเพื่อทำดาเมจ)
    * `closeHitboxEventNames = ["end"]` (ปิด Hitbox หมัด/อาวุธ)
* **`BindCustomAnimationClipEvents()` & `OnCustomPlayerAnimEvent(string eventName)`**:
  * สแกนคลิปแอนิเมชันทั้งหมดของตัวละครผู้เล่นตอนเริ่มเกม หากคุณเพิ่มชื่อ Event อื่นเข้าไปใน List บน Inspector ระบบจะผูกชื่อ Event นั้นเข้ากับคำสั่งเปิด/ปิด Hitbox ของ `PlayerCombat` ให้อัตโนมัติทันที
* **`hit(int hitIndex = 0)` & `end()`**:
  * รับคำสั่งจากหมุด Animation Event `"hit"` และ `"end"` บนคลิปแอนิเมชัน แล้วส่งต่อไปเปิด/ปิด Hitbox ใน `PlayerCombat`
* **`OnExecuteImpact()`**: หมุดเสริมสำหรับท่าสังหารบอส (`Execution [F]`) เพื่อสั่งปลิดชีพบอสและฟื้นฟูเลือดในจังหวะปะทะ

### 4.3 `PlayerCombat.cs`
* **`Awake()` / `Start()` & `SetupHitboxColliders()`**:
  * ค้นหาและเตรียมคอมโพเนนต์ทั้งหมด ตั้งค่า Collider ของหมัด/อาวุธทุกชิ้นเป็น `isTrigger = true` และปิดไว้จนกว่าจะถึงเฟรมโจมตี พร้อมผูก `HitboxTriggerForwarder`
* **`Update()` / `CheckDirectAttackInput()`**:
  * ตรวจจับการกดโจมตี (คลิกซ้าย / ปุ่ม `J` / จอยเกม) กรองการลากเมาส์หันกล้องออกจากคลิกโจมตี และสแกนทำดาเมจเมื่อ Hitbox เปิดทำงาน
* **`PerformAttack()` & `IsHeavyComboFinisher()`**:
  * สั่งเริ่มท่าโจมตีและนับจังหวะคอมโบ 3 หมัด (`Cross Punch` -> `Hook Punch` -> `Surprise Uppercut`) หากเป็นหมัดที่ 3 จะคูณโบนัสดาเมจและสั่นหน้าจอแรงขึ้น
* **`hit(int hitIndex = 0)` / `end()`**:
  * เปิดและปิดก้อน Hitbox ตามเฟรมแอนิเมชันที่ส่งมาจาก `PlayerAnimationEvents`
* **`ProcessHit(Collider other)` / `OnTriggerEnter(Collider other)` / `ScanAndDealDamage()`**:
  * คำนวณดาเมจรวมกับโบนัสอัปเกรดจาก Hub (`Raw Power`) ส่งดาเมจให้ศัตรู พร้อมสั่ง Hit-stop และสั่นหน้าจอ (`Cinemachine Impulse`)
* **`CancelCombat()`**: ยกเลิกท่าโจมตีทันทีเมื่อผู้เล่นกดกลิ้งหลบ (Animation Cancel) หรือกดใช้ท่าสังหาร (`Execution`)

### 4.4 `PlayerExecution.cs`
* **`Update()` & `TryExecuteNearbyTarget()` / `FindNearestExecutableTarget()`**:
  * เมื่อกดปุ่ม `[F]` จะสแกนหาศัตรูระดับ Elite / Boss ในระยะที่เกราะแตกและติด Stun (`CanBeExecuted == true`) ที่อยู่ใกล้ผู้เล่นที่สุด
* **`ExecutionRoutine(IExecutable target)`**:
  * ล็อกเป้าหมายให้อยู่กับที่ เปิดอมตะ (`I-Frame`) ให้ผู้เล่น วาร์ปผู้เล่นไปยืนด้านหน้าเป้าหมาย เร่งความเร็วแอนิเมชันสังหาร และคืนการควบคุมเมื่อจบท่า
* **`TriggerExecutionImpact()`**:
  * สั่งปลิดชีพเป้าหมายทันทีในจังหวะปะทะ ฟื้นฟูเลือด (HP) ให้ผู้เล่นตามเปอร์เซ็นต์ที่กำหนด พร้อมทำ Hit-stop และสั่นหน้าจอ

### 4.5 `PlayerHealth.cs` (รวมคลาส `GameProgressionManager`)
* **ส่วนที่ 1: คลาส `GameProgressionManager` (ตัวจัดการข้อมูลข้ามฉากและเซฟเกม)**
  * **`LoadFromPrefs()` & `SaveToPrefs()`**: โหลดและบันทึกค่า `Star Dust`, `Shattered Core`, เลเวลอัปเกรดทั้ง 3 สาย, ด่านสูงสุดที่ปลดล็อก, จำนวนการสังหารสะสม, และค่าพลังงาน (`Play Energy`) ลงใน `PlayerPrefs`
  * **`TryUpgradeMaxHealth()`, `TryUpgradeStaminaEfficiency()`, `TryUpgradeAttack()`**: หักแร่และเพิ่มค่าโบนัสถาวรของทั้ง 3 สายอัปเกรด (`Vitality Boost`, `Stamina Efficiency`, `Raw Power`)
  * **`MarkCurrentStageCleared()` & `IsStageClearedBefore()`**: บันทึกการเคลียร์ด่านปัจจุบัน หากเป็นการเคลียร์ด่านนั้นครั้งแรก จะแจกโบนัสก้อนใหญ่ (`First-Clear Bonus`) และปลดล็อกระดับ Rank ถัดไป (Rank E -> D -> C -> B -> A -> S)
  * **`RegisterEnemyKill()`, `GetCurrentMilestoneTier()`, `GetProgressionUiThemeColor()`**: นับจำนวนมอนสเตอร์ที่สังหารสะสม แจกโบนัสทุกๆ 25 ตัว และคำนวณระดับ Milestone (0 ถึง 3) เพื่อปลดล็อกข้อความลับ (Flavor Text) และคืนค่าสีธีม UI ที่ค่อยๆ เปลี่ยนจากชมพูอ่อนเป็นแดงเข้ม
* **ส่วนที่ 2: คลาส `PlayerHealth` (ระบบเลือดผู้เล่นในฉากต่อสู้)**
  * **`Awake()` & `TakeDamage()`**: คำนวณเลือดสูงสุดรวม (`Base HP + Vitality Boost`), ลดเลือดเมื่อโดนมอนสเตอร์โจมตี (ยกเว้นตอนติด I-Frame), สั่นกล้อง และเปิดอมตะชั่วคราวหลังโดนตี (`HurtInvincibleRoutine`)
  * **`HealPercentage()`, `AddStarDust()`, `AddShatteredCore()`, `Die()`, `RestartStage()`**: ฟื้นฟูเลือดเมื่อทำท่า Execution สำเร็จ, บวกแร่เมื่อเก็บลูกแก้วจากมอนสเตอร์, จัดการสถานะตอนผู้เล่นตาย (`ShowGameOverScreen`), และหักพลังงานเมื่อกดเล่นด่านซ้ำ (`Retry`)

### 4.6 `PlayerLockOn.cs`
* **`Update()` / `HandleLockOnInput()`**: ตรวจจับการกดปุ่มล็อกเป้า (ปุ่ม `Tab` / คลิกเมาส์กลาง / กด R3 บนจอย) เพื่อเปิด-ปิดโหมด Lock-On
* **`ToggleLockOn()` / `AcquireNearestTarget()` / `ClearLockOn()`**: ค้นหาศัตรูที่ยังมีชีวิตในระยะและอยู่ในมุมกล้องเพื่อล็อกเป้า หรือยกเลิกการล็อกเป้าเมื่อศัตรูตาย/ออกนอกระยะ
* **`UpdateLockedOrientation()` / `UpdateReticlePosition()`**: หมุนตัวละครช่วยหันเข้าหาเป้าหมายที่ล็อกไว้ และอัปเดตตำแหน่งวงแหวนเป้าล็อกบนหน้าจอตามตัวศัตรู

### 4.7 `PlayerMovement.cs`
* **`Awake()` & `IgnoreSelfCollisions()`**:
  * ดึงคอมโพเนนต์หลัก ซิงก์ความเร็วการเดิน (`moveSpeed`) และส่วนลด Stamina ตอนกลิ้งหลบจาก `GameProgressionManager`
  * บันทึกความสูงระดับพื้นสนามเริ่มต้น (`initialGroundY`) สำหรับใช้ป้องกันตัวละครตกแมพ และสั่ง `Physics.IgnoreCollision` ไม่ให้ `CharacterController` ชนกับ Collider ลูกของตัวเอง
* **`Update()` & `ComputeMovementVelocity()`**:
  * อ่านค่าปุ่มเดิน `WASD` / ลูกศร / จอยเกม คำนวณทิศทางการเดินอ้างอิงตามมุมกล้องหลัก และรองรับการเดินสเตรฟซ้าย-ขวาขณะล็อกเป้าศัตรู
* **`PerformDodge()` & `ComputeDodgeVelocity()`**:
  * เมื่อกดปุ่ม `Left Shift` หรือ `Space` จะหักค่า Stamina และพุ่งตัวหลบ (`Dodge`) พร้อมเปิดสถานะอมตะชั่วคราว (`IsInvincible` / `I-Frame`) ตามเวลา `iFrameDuration`
* **`ClampAboveGroundFloor()` (ระบบป้องกันตัวละครจมพื้น/ตกแมพเวลามอนตี)**:
  * ทำงานทั้งใน `Update()` และ `LateUpdate()` ตรวจสอบทุกเฟรมว่าค่าความสูงแกน Y ของตัวละครต่ำกว่าระดับพื้นเริ่มต้น (`initialGroundY - floorHeightTolerance`) หรือไม่ หากโดนมอนสเตอร์กระแทกหรือฟันจนกดตัวละครจมลงไปใต้พื้น ระบบจะดึงตัวละครกลับขึ้นมาบนระดับพื้นทันที ไม่ให้ตกแมพเด็ดขาด
* **`LateUpdate()` & `ResolveCameraCollision()` (ระบบกันกล้องทะลุกำแพง/ฉาก)**:
  * ยิง `Physics.SphereCast` จากระดับไหล่ผู้เล่น (`cameraPivotHeight`) ไปยังตำแหน่งกล้อง หากมีกำแพงหรือฉากขวางกั้น จะดึงกล้องเข้ามาด้านหน้ากำแพงทันทีโดยไม่ชนตัวผู้เล่นหรือมอนสเตอร์
* **`WarpToPositionAndLookAt()`**: ปิด `CharacterController` ชั่วคราวก่อนย้ายตำแหน่งตัวละคร (เช่น ตอนเริ่มด่านหรือทำท่า Execution) แล้วเปิดใหม่

### 4.8 `PlayerStamina.cs`
* **`Awake()`**: ดึงค่าโบนัสความเร็วฟื้นฟู Stamina (`BonusStaminaRegen`) จากสายอัปเกรดที่ 2 (`Stamina Efficiency`) ใน `GameProgressionManager` มาบวกเพิ่มเข้ากับ `regenRate` พื้นฐาน
* **`Update()`**: นับถอยหลังเวลาหน่วงก่อนเริ่มฟื้นฟู (`delayTimer`) หลังจากผู้เล่นเพิ่งกลิ้งหลบ เมื่อพ้นเวลาหน่วงแล้วจะค่อยๆ ฟื้นฟูค่า `CurrentStamina` ตามอัตรา `regenRate` ต่อวินาที
* **`TryConsumeStamina(float amount)` & `RestoreFullStamina()`**: ตรวจสอบว่ามี Stamina พอสำหรับการกลิ้งหลบหรือไม่ ถ้าพอจะหักค่า Stamina และรีเซ็ตเวลาหน่วงฟื้นฟู

## หมวดที่ 5: โฟลเดอร์ `UI/` (ระบบหน้าจอต่อสู้และสรุปผล)

### 5.1 `BattleHUD.cs`
* **`Start()` & `OnHealthChanged()` / `OnStaminaChanged()` / `OnCurrencyChanged()`**:
  * เชื่อมต่ออีเวนต์จาก `PlayerHealth` และ `PlayerStamina` เพื่ออัปเดตหลอดเลือด หลอด Stamina และจำนวนแร่บนหน้าจอ พร้อมเปลี่ยนสีหลอดเลือดผู้เล่นตามระดับ Milestone (ชมพูอ่อน -> แดงเข้ม)
* **`OpenSettingsPanel()`, `CloseSettingsPanel()` & `ReturnToLobby()`**:
  * เปิด/ปิดหน้าต่างตั้งค่าระหว่างเล่นด้วยปุ่ม `ESC` หรือปุ่มบนจอ พร้อมหยุดเวลาเกม (`Time.timeScale = 0`) และรองรับปุ่มกดออกจากด่านกลางคัน (`Exit to Lobby / StageMap / MainMenu`)
* **`UpdateExecutionPromptVisibility()`**:
  * ตรวจจับว่ามีบอสที่เกราะแตกติดสถานะ Stun อยู่ในระยะหรือไม่ ถ้ามีจะแสดงข้อความ `"[F] EXECUTE (HEAL HP)"`
* **`SpawnFloatingFeedback()` & `AnimateFloatingFeedbackRoutine()`**:
  * สร้างข้อความเด้งขึ้นมุมซ้ายบน (เช่น `"+ [STAR DUST] 15"`, `"+ [CORE] 1"`) เมื่อลูกแก้วพลังงานลอยเข้าตัวผู้เล่น
* **`ShowVictoryScreen()` & `UpdateScoreSummaryDisplay()`**:
  * แสดงหน้าจอชนะด่าน พร้อมสรุปจำนวนแร่ที่ได้ในด่าน โบนัสเคลียร์ครั้งแรก (`[FIRST-CLEAR BONUS!]`) การเลื่อนขั้น Rank (`[RANK PROMOTION!]`) และเนื้อเรื่องสั้น 1 บรรทัดประจำ Rank ที่ปลดล็อกใหม่

============================================================================ */