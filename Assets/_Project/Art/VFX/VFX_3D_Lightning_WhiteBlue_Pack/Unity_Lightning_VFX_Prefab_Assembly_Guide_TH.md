# คู่มือประกอบร่างและสร้าง Prefab เอฟเฟกต์สายฟ้า 3D สีขาว-น้ำเงิน (Unity 3D Assembly & Prefab Guide)

คู่มือฉบับนี้อธิบายโครงสร้างไฟล์ทั้งหมดที่อยู่ในแพ็กเกจ **`VFX_3D_Lightning_WhiteBlue_Pack.zip`** พร้อมวิธีประกอบร่างให้เป็น **Prefab ทั้ง 3 ช่วง (1. จุดปล่อยสายฟ้า ➔ 2. สายฟ้าวิ่งเป็นเส้น 1 เส้น ➔ 3. ลูกแก้วสายฟ้า Impact)** ทั้งแบบ **สั่งประกอบร่างอัตโนมัติใน 1 คลิก** และแบบ **ตั้งค่าประกอบเองทีละชิ้นใน Unity Inspector** รวมถึงวิธีนำไปผูกเข้ากับ `PlayerCombat.cs` และ `MagicProjectile.cs` ในโปรเจกต์เกมของคุณค่ะ

## 1. รายการไฟล์ทั้งหมดในแพ็กเกจ (`VFX_3D_Lightning_WhiteBlue_Pack.zip`)

| โฟลเดอร์ / ชื่อไฟล์ | ประเภทไฟล์ | หน้าที่ในการประกอบร่าง |
| :--- | :---: | :--- |
| **`Scripts/LightningVFXPrefabBuilder.cs`** | Unity C# Editor | **ตัวประกอบร่างอัตโนมัติใน 1 คลิก:** สร้าง Material และบันทึกไฟล์ `.prefab` ทั้ง 4 ตัวลงในโฟลเดอร์โปรเจกต์ให้ทันที |
| **`Scripts/LightningBoltVFXController.cs`** | Unity C# Runtime | **ตัวควบคุมลำดับ VFX:** สั่งปล่อยประจุสายฟ้า ➔ ยิงสายฟ้า 1 เส้นพุ่งไปข้างหน้า ➔ เสกลูกแก้วสายฟ้า Impact เมื่อถึงเป้า |
| **`Meshes/VFX_Lightning_WhiteBlue_Meshes.obj`** | 3D Mesh (`.obj`) | โมเดล 3D แยก 3 ชิ้นย่อย:<br>1. `VFX_Projectile_TaperedSpear` (หัวลูกศรลำแสงทรงเรียวแหลม)<br>2. `VFX_Coiled_LightningRibbon` (แถบเกลียวสายฟ้าพันรอบลำแสง)<br>3. `VFX_Impact_ElectricSphere` (โดมทรงกลมสำหรับทำลูกแก้วปะทะ) |
| **`Meshes/VFX_Lightning_WhiteBlue_Meshes.ma`** | Maya ASCII (`.ma`) | ไฟล์ต้นฉบับสำหรับเปิดปรับแก้ทรงโพลีกอนใน Autodesk Maya หากต้องการแก้ไขทรงเพิ่มเติม |
| **`Textures/T_ProjectileStreak_WhiteBlue.png`** | Texture (512×128) | แผ่นภาพไล่เฉดแสงทรงเรียวแหลม (แกนกลางสีขาวบริสุทธิ์ ขอบสีน้ำเงินไฟฟ้า) สำหรับใส่ในลำแสงและ `TrailRenderer` |
| **`Textures/T_LightningArc_WhiteBlue_2x2.png`** | Texture (512×512) | แผ่นภาพเส้นสายฟ้าแตกแขนงแบบ 2×2 เฟรม สำหรับใส่ใน `LineRenderer` หรือ `ParticleSystem` |
| **`Textures/T_ElectricGlow_WhiteBlue.png`** | Texture (256×256) | แผ่นภาพแสงฟุ้งทรงกลมสีขาว-น้ำเงิน สำหรับทำประกายไฟ (`Sparks`) และออร่าลูกแก้วปะทะ |

## 2. วิธีที่ 1: สั่งประกอบร่างเป็น Prefab อัตโนมัติใน 1 คลิก (เร็วที่สุด แนะนำ!)

ไม่ต้องลากปั้นทีละชิ้นให้เสียเวลา เพราะมีสคริปต์ **`LightningVFXPrefabBuilder.cs`** จัดการประกอบร่างให้ครบทุกขั้นตอน:

1. แตกไฟล์ `VFX_3D_Lightning_WhiteBlue_Pack.zip` แล้วลากโฟลเดอร์ทั้ง 3 (`Meshes`, `Textures`, `Scripts`) ไปวางในโปรเจกต์ Unity ของคุณ (เช่นใน `Assets/_Project/VFX_Lightning_WhiteBlue/`)
2. รอให้ Unity คอมไพล์สคริปต์เสร็จ (ประมาณ 2–3 วินาที)
3. ไปที่แถบเมนูด้านบนสุดของโปรแกรม Unity คลิกที่เมนู:
   **`Tools` ➔ `MahouShoujo VFX` ➔ `Build All White-Blue Lightning Prefabs (1-Click)`**
   *(หรือคลิกเมนู `GameObject` ➔ `VFX` ➔ `Build All White-Blue Lightning Prefabs (1-Click)`)*
4. สคริปต์จะสร้างโฟลเดอร์ `Assets/_Project/VFX_Lightning_WhiteBlue/Prefabs/` และประกอบร่าง **Prefab ทั้ง 4 ตัว** พร้อมใช้งานทันที:
   * **`PF_VFX_1_Lightning_CastBurst_WhiteBlue.prefab`** — ประจุสายฟ้าจังหวะปล่อยออกจากตัวเรา (ไม่มีก้อนโมเดลแทนตัวละคร)
   * **`PF_VFX_2_Lightning_SingleBolt_WhiteBlue.prefab`** — ลำแสงสายฟ้าวิ่งเป็นเส้น 1 เส้น (แกนขาว + ออร่าสีน้ำเงิน + หางแสง + เกลียวสายฟ้า)
   * **`PF_VFX_3_Lightning_ImpactSphere_WhiteBlue.prefab`** — ลูกแก้วทรงกลมโปร่งแสงสีน้ำเงินที่มีเส้นสายฟ้าสีขาว-ฟ้าวิ่งรอบลูกแก้วเมื่อชนเป้าหมาย
   * **`PF_VFX_Lightning_CompleteSequence_WhiteBlue.prefab`** — ตัวรันลำดับ `1 ➔ 2 ➔ 3` อัตโนมัติสำหรับวางทดสอบในฉาก

## 3. วิธีที่ 2: ขั้นตอนประกอบร่างด้วยตัวเองทีละชิ้นใน Unity (Manual Step-by-Step Assembly)

หากคุณต้องการปรับแต่งโครงสร้าง Hierarchy หรือสร้าง Material เองใน URP / Built-in Pipeline ให้ทำตามลำดับดังนี้:

### ขั้นตอนที่ 3.1: ตั้งค่า Texture และสร้าง Material ทั้ง 3 ตัว

1. คลิกเลือกไฟล์ภาพทั้ง 3 ไฟล์ในโฟลเดอร์ `Textures/` แล้วตั้งค่าใน Inspector:
   * ติ๊กถูก **`Alpha Is Transparency = true`** แล้วกด **Apply**
2. สร้าง Material 3 ตัว (`Right Click ➔ Create ➔ Material`):

| ชื่อ Material ที่สร้าง | Shader ที่เลือกใช้ | Texture ที่ลากใส่ (`Base Map` / `MainTex`) | ค่าสี (`Tint` / `HDR Color`) |
| :--- | :--- | :--- | :--- |
| **`M_VFX_WhiteCore_Additive`** | `Universal Render Pipeline/Particles/Unlit` (Blending = **Additive**) | `T_ProjectileStreak_WhiteBlue` | สีขาวสว่าง `#FFFFFF` (HDR Intensity = `+2.0`) |
| **`M_VFX_LightningArc_Additive`** | `Universal Render Pipeline/Particles/Unlit` (Blending = **Additive**) | `T_LightningArc_WhiteBlue_2x2` | สีฟ้าอมขาว `#80D8FF` (HDR Intensity = `+1.8`) |
| **`M_VFX_ImpactDome_BlueTranslucent`** | `Universal Render Pipeline/Particles/Unlit` (Surface = **Transparent**, Blending = **Alpha**) | `T_ElectricGlow_WhiteBlue` | สีน้ำเงินโปร่งแสง `#1E90FF` (**Alpha = `80` จาก 255**) |

### ขั้นตอนที่ 3.2: ประกอบร่างชิ้นที่ 1 — จุดปล่อยสายฟ้า (`PF_VFX_1_Lightning_CastBurst`)

```
PF_VFX_1_Lightning_CastBurst (Empty GameObject)
 ├── ReleaseLightningArcs (LineRenderer x 6–8 เส้น ใช้ Material: M_VFX_LightningArc_Additive)
 └── ReleaseSparks_PS (Particle System ยิงประกายไฟสีขาว-น้ำเงินแบบ Burst 28 เม็ด)
```

* **การตั้งค่า `ReleaseSparks_PS` (`ParticleSystem`):**
  * `Start Lifetime`: `0.2` ถึง `0.42` วินาที
  * `Start Speed`: `2.0` ถึง `4.8`
  * `Start Size`: `0.05` ถึง `0.14`
  * `Start Color`: สุ่มระหว่าง 2 สี (`Random Between Two Colors`) คือ **สีขาว `#FFFFFF`** และ **สีฟ้าไฟฟ้า `#2EA8FF`**
  * `Emission`: ปิด `Rate over Time = 0` และเพิ่ม `Burst` ที่เวลา `0.00` จำนวน `28` เม็ด
  * `Renderer`: เลือก `Render Mode = Stretched Billboard` (`Length Scale = 2.4`) และลาก `M_VFX_WhiteCore_Additive` มาใส่

### ขั้นตอนที่ 3.3: ประกอบร่างชิ้นที่ 2 — ลำแสงสายฟ้าวิ่งเป็นเส้น 1 เส้น (`PF_VFX_2_Lightning_SingleBolt`)

```
PF_VFX_2_Lightning_SingleBolt (Empty GameObject + TrailRenderer + MagicProjectile.cs)
 ├── InnerWhiteCoreSpear (Mesh: VFX_Projectile_TaperedSpear | Material: M_VFX_WhiteCore_Additive | Scale: 1, 1, 1)
 ├── OuterBlueSheath     (Mesh: VFX_Projectile_TaperedSpear | Material: M_VFX_ImpactDome_BlueTranslucent | Scale: 1.85, 1.85, 1.15)
 ├── CoiledRibbonMesh    (Mesh: VFX_Coiled_LightningRibbon  | Material: M_VFX_LightningArc_Additive | Scale: 1, 1, 1)
 └── TrailingSparks_PS   (Particle System ปล่อยสะเก็ดไฟสีขาว-น้ำเงินต่อเนื่อง Rate over Time = 40)
```

* **การตั้งค่า `TrailRenderer` บนตัวแม่ (`PF_VFX_2_Lightning_SingleBolt`):**
  * `Time`: `0.22` วินาที
  * `Start Width`: `0.42` ➔ `End Width`: `0.00`
  * `Color Gradient`: จุดแรก (`0%`) สีขาว `#FFFFFF` ➔ จุดกลาง (`35%`) สีฟ้า `#40B8FF` ➔ จุดท้าย (`100%`) สีน้ำเงินเข้ม `#0A52FF` (Alpha = `0%`)
  * `Materials`: ลาก `M_VFX_WhiteCore_Additive` มาใส่

### ขั้นตอนที่ 3.4: ประกอบร่างชิ้นที่ 3 — ลูกแก้วสายฟ้าปะทะเป้าหมาย (`PF_VFX_3_Lightning_ImpactSphere`)

```
PF_VFX_3_Lightning_ImpactSphere (Empty GameObject)
 ├── TranslucentBlueEnergySphere (Mesh: VFX_Impact_ElectricSphere | Material: M_VFX_ImpactDome_BlueTranslucent | Scale: 2.8, 2.8, 2.8)
 ├── SphereLightningArc_01..12   (LineRenderer 12 เส้นวิ่งเกาะผิวทรงกลม | Material: M_VFX_LightningArc_Additive)
 └── ImpactSparks_PS             (Particle System ยิงสะเก็ดไฟระเบิด Burst 38 เม็ด)
```

## 4. วิธีนำ Prefab ทั้ง 3 ตัวไปใส่ในสคริปต์ตัวตีไกล (`PlayerCombat.cs` & `MagicProjectile.cs`)

เมื่อได้ Prefab ทั้ง 3 ชิ้นแล้ว คุณสามารถนำไปผูกเข้ากับระบบตัวละครสายยิงไกล (`SHIRO`) ในโปรเจกต์ของคุณได้ทันที:

1. **บนตัวละคร `Player` (ใน Inspector ของ `PlayerCombat.cs`):**
   * ช่อง **`Ranged Muzzle Cast VFX Prefab`**: ลาก **`PF_VFX_1_Lightning_CastBurst_WhiteBlue`** มาใส่
   * ช่อง **`Magic Projectile Prefab`**: ลาก **`PF_VFX_2_Lightning_SingleBolt_WhiteBlue`** (ที่แปะสคริปต์ `MagicProjectile.cs` ไว้) มาใส่
   * ช่อง **`Ranged Impact VFX Prefab`**: ลาก **`PF_VFX_3_Lightning_ImpactSphere_WhiteBlue`** มาใส่
2. **ผลลัพธ์เมื่อกดโจมตี:**
   * ทันทีที่ปล่อยพลัง: เกิดประจุสายฟ้าสีขาว-น้ำเงิน (`PF_VFX_1`) ที่จุดปล่อยโดยไม่มีก้อนตัวละครจำลอง
   * ระหว่างพุ่งกลางอากาศ: ลำแสงสายฟ้าสีขาว-น้ำเงิน 1 เส้น (`PF_VFX_2`) พุ่งเป็นเส้นตรงเข้าหาเป้าหมาย
   * เมื่อถึงตัวเป้าหมาย: ระเบิดออกเป็นโดมลูกแก้วสีน้ำเงินที่มีสายฟ้าสีขาว-ฟ้าวิ่งรอบลูกแก้ว (`PF_VFX_3`) ตรงตามลำดับที่คุณต้องการทุกประการค่ะ
