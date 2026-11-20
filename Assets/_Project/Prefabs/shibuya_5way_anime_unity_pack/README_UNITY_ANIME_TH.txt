========================================================================
โมเดล 3D: 5 แยกชิบูย่า สไตล์อนิเมะ + ระบบกำแพง Collider กั้นเดินออกสำหรับ Unity 3D
========================================================================
สถิติโพลีกอนของซีน:
- จำนวนชิ้นส่วนทั้งหมด: 50 ชิ้น (รวมกลุ่ม GRP_Unity_Arena_Colliders 13 ชิ้น)
- จำนวน Polygon Faces: 16,021 Faces
- จำนวน Triangles (Tris): 31,446 Tris
- จำนวนจุดยอด (Vertices): 18,809 Verts

1. รายชื่อไฟล์ในแพ็กเกจนี้:
   - shibuya_5way_anime_unity.fbx          : ไฟล์ FBX สำหรับลากเข้า Unity 3D / Maya / Blender ได้ทันที
   - shibuya_5way_anime_unity.ma           : ไฟล์โปรเจกต์ Autodesk Maya ASCII
   - shibuya_5way_anime_unity.obj & .mtl   : ไฟล์ Wavefront OBJ พร้อมพิกัด UV เชื่อมกับชุด Texture
   - textures/T_Shibuya_Anime_Atlas_Albedo.png : แผ่น Texture สไตล์อนิเมะ (หน้าต่างตึก ป้ายไฟนีออน พื้นถนน ใบไม้)
   - textures/T_Shibuya_Anime_ToonRamp.png     : แผ่นไล่เฉดสี Cel-Shading (Toon Ramp) สำหรับใส่ใน Toon Shader ของ Unity
   - ShibuyaArenaColliderSetup.cs          : สคริปต์ C# สำหรับ Unity 3D ตั้งค่ากำแพงล่องหนกั้นเดินออกในคลิกเดียว
   - build_shibuya_in_maya.py              : สคริปต์ Python สำหรับรันใน Maya Script Editor

2. วิธีตั้งค่า Collider กั้นเดินออกตรงกลาง 5 แยกใน Unity 3D:
   วิธีที่ 1 (อัตโนมัติด้วยสคริปต์ C#):
   - ลากไฟล์ shibuya_5way_anime_unity.fbx และ ShibuyaArenaColliderSetup.cs เข้าโฟลเดอร์ Assets ใน Unity
   - ลากโมเดลลงใน Scene แล้วลากสคริปต์ ShibuyaArenaColliderSetup ไปใส่ที่ตัวแม่ของโมเดล
   - คลิกขวาที่ชื่อสคริปต์ใน Inspector เลือก "Setup Shibuya Arena Colliders" -> กำแพงทั้ง 12 ด้าน (UCX_Wall_01..12)
     และพื้นเรียบกลางแยก (UCX_Floor_CenterArena) จะถูกใส่ MeshCollider และซ่อน MeshRenderer ให้กลายเป็นกำแพงล่องหนทันที!

   วิธีที่ 2 (ตั้งค่าด้วยตัวเองใน Inspector):
   - เปิดกิ่ง GRP_Unity_Arena_Colliders ใน Hierarchy
   - เลือก UCX_Wall_01 ถึง UCX_Wall_12 ทั้งหมด -> กด Add Component > Mesh Collider (หรือ Box Collider)
   - ติ๊กเครื่องหมายถูกหน้า Mesh Renderer ออก เพื่อให้กำแพงโปร่งใสมองไม่เห็นในเกม
