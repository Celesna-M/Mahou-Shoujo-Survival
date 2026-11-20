#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MahouShoujo.VFX
{
    public static class LightningVFXPrefabBuilder
    {
        private const string BaseFolder = "Assets/_Project/VFX_Lightning_WhiteBlue";
        private const string MaterialsFolder = BaseFolder + "/Materials";
        private const string PrefabsFolder = BaseFolder + "/Prefabs";
        private const string TexturesFolder = BaseFolder + "/Textures";
        private const string MeshesFolder = BaseFolder + "/Meshes";

        [MenuItem("Tools/MahouShoujo VFX/Build All White-Blue Lightning Prefabs (1-Click)", false, 1)]
        [MenuItem("GameObject/VFX/Build All White-Blue Lightning Prefabs (1-Click)", false, 11)]
        public static void BuildAllLightningPrefabs()
        {
            EnsureFolders();

            // 1. Load or generate Textures
            Texture2D streakTex = FindOrCreateTexture("T_ProjectileStreak_WhiteBlue", CreateStreakTexture);
            Texture2D glowTex = FindOrCreateTexture("T_ElectricGlow_WhiteBlue", CreateGlowTexture);
            Texture2D arcTex = FindOrCreateTexture("T_LightningArc_WhiteBlue_2x2", CreateArcTexture);

            // 2. Create 3 Materials (White Core Additive, Electric Blue Additive, Impact Dome Translucent)
            Material matWhiteCore = CreateOrUpdateAdditiveMaterial(
                MaterialsFolder + "/M_VFX_WhiteCore_Additive.mat",
                streakTex,
                new Color(3.2f, 3.6f, 4.0f, 1.0f)
            );

            Material matElectricBlue = CreateOrUpdateAdditiveMaterial(
                MaterialsFolder + "/M_VFX_ElectricBlue_Additive.mat",
                glowTex,
                new Color(0.25f, 1.65f, 3.6f, 1.0f)
            );

            Material matArcLine = CreateOrUpdateAdditiveMaterial(
                MaterialsFolder + "/M_VFX_LightningArc_Additive.mat",
                arcTex,
                new Color(1.8f, 2.8f, 4.0f, 1.0f)
            );

            Material matDomeTranslucent = CreateOrUpdateTranslucentDomeMaterial(
                MaterialsFolder + "/M_VFX_ImpactDome_BlueTranslucent.mat",
                glowTex,
                new Color(0.10f, 0.55f, 1.0f, 0.32f)
            );

            // 3. Load Custom Meshes from Meshes/VFX_Lightning_WhiteBlue_Meshes.obj if present
            Mesh spearMesh = FindSubMeshInObj("VFX_Projectile_TaperedSpear");
            Mesh sphereMesh = FindSubMeshInObj("VFX_Impact_ElectricSphere");
            Mesh ribbonMesh = FindSubMeshInObj("VFX_Coiled_LightningRibbon");

            // 4. Build Prefab 1: Cast Burst (Muzzle)
            string muzzlePrefabPath = PrefabsFolder + "/PF_VFX_1_Lightning_CastBurst_WhiteBlue.prefab";
            GameObject muzzlePrefab = BuildCastBurstPrefab(muzzlePrefabPath, matWhiteCore, matElectricBlue, matArcLine);

            // 5. Build Prefab 2: Single Lightning Projectile Bolt (Line Flight)
            string boltPrefabPath = PrefabsFolder + "/PF_VFX_2_Lightning_SingleBolt_WhiteBlue.prefab";
            GameObject boltPrefab = BuildSingleBoltPrefab(boltPrefabPath, spearMesh, ribbonMesh, matWhiteCore, matElectricBlue, matArcLine, matDomeTranslucent);

            // 6. Build Prefab 3: Spherical Lightning Impact Dome
            string impactPrefabPath = PrefabsFolder + "/PF_VFX_3_Lightning_ImpactSphere_WhiteBlue.prefab";
            GameObject impactPrefab = BuildImpactSpherePrefab(impactPrefabPath, sphereMesh, matWhiteCore, matElectricBlue, matArcLine, matDomeTranslucent);

            // 7. Build Prefab 4: Complete Sequence Spawner (1 -> 2 -> 3)
            string completePrefabPath = PrefabsFolder + "/PF_VFX_Lightning_CompleteSequence_WhiteBlue.prefab";
            GameObject completeRoot = new GameObject("PF_VFX_Lightning_CompleteSequence_WhiteBlue");
            var ctrl = completeRoot.AddComponent<LightningBoltVFXController>();
            ctrl.autoLoopInPlayMode = true;
            GameObject savedComplete = PrefabUtility.SaveAsPrefabAsset(completeRoot, completePrefabPath);
            Object.DestroyImmediate(completeRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = savedComplete != null ? savedComplete : boltPrefab;
            EditorGUIUtility.PingObject(Selection.activeObject);

            Debug.Log("[LightningVFXPrefabBuilder] สร้าง Prefab สายฟ้าสีขาว-น้ำเงินครบทั้ง 4 ตัวไว้ในโฟลเดอร์: " + PrefabsFolder);
        }

        private static GameObject BuildCastBurstPrefab(string prefabPath, Material matWhite, Material matBlue, Material matArc)
        {
            GameObject root = new GameObject("PF_VFX_1_Lightning_CastBurst_WhiteBlue");

            // Electric Arcs around release point (no character sphere mesh)
            for (int i = 0; i < 8; i++)
            {
                GameObject arcObj = new GameObject($"ReleaseLightningArc_{i + 1}");
                arcObj.transform.SetParent(root.transform, false);
                LineRenderer lr = AddConfiguredLineRenderer(arcObj, matArc, 0.08f, 0.015f, Color.white, new Color(0.2f, 0.7f, 1.0f, 1.0f));
                PopulateSphereArcPoints(lr, 0.85f, 10, 0.16f, i * 17);
            }

            // Release Spark Particle System
            CreateConfiguredSparkParticleSystem(root.transform, "ReleaseSparks_WhiteBlue", matWhite, 28, 4.8f, 0.42f, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildSingleBoltPrefab(
            string prefabPath,
            Mesh spearMesh,
            Mesh ribbonMesh,
            Material matWhite,
            Material matBlue,
            Material matArc,
            Material matDome)
        {
            GameObject root = new GameObject("PF_VFX_2_Lightning_SingleBolt_WhiteBlue");

            // 1. Inner White Core Spear
            GameObject innerCore;
            if (spearMesh != null)
            {
                innerCore = new GameObject("InnerWhiteCoreSpear");
                innerCore.transform.SetParent(root.transform, false);
                innerCore.AddComponent<MeshFilter>().sharedMesh = spearMesh;
                innerCore.AddComponent<MeshRenderer>().sharedMaterial = matWhite;
            }
            else
            {
                innerCore = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                innerCore.name = "InnerWhiteCoreSpear";
                innerCore.transform.SetParent(root.transform, false);
                innerCore.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                innerCore.transform.localScale = new Vector3(0.22f, 1.55f, 0.22f);
                if (innerCore.TryGetComponent<Collider>(out var c)) Object.DestroyImmediate(c);
                innerCore.GetComponent<Renderer>().sharedMaterial = matWhite;
            }

            // 2. Outer Electric Blue Sheath
            GameObject outerSheath;
            if (spearMesh != null)
            {
                outerSheath = new GameObject("OuterBlueSheath");
                outerSheath.transform.SetParent(root.transform, false);
                outerSheath.transform.localScale = new Vector3(1.85f, 1.85f, 1.15f);
                outerSheath.AddComponent<MeshFilter>().sharedMesh = spearMesh;
                outerSheath.AddComponent<MeshRenderer>().sharedMaterial = matBlue;
            }
            else
            {
                outerSheath = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                outerSheath.name = "OuterBlueSheath";
                outerSheath.transform.SetParent(root.transform, false);
                outerSheath.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                outerSheath.transform.localScale = new Vector3(0.45f, 1.95f, 0.45f);
                if (outerSheath.TryGetComponent<Collider>(out var c)) Object.DestroyImmediate(c);
                outerSheath.GetComponent<Renderer>().sharedMaterial = matDome;
            }

            // 3. Optional 3D Coiled Ribbon Mesh if present
            if (ribbonMesh != null)
            {
                GameObject ribObj = new GameObject("CoiledRibbonMesh");
                ribObj.transform.SetParent(root.transform, false);
                ribObj.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
                ribObj.AddComponent<MeshRenderer>().sharedMaterial = matArc;
            }

            // 4. TrailRenderer (White Core -> Electric Blue Tail)
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.42f;
            trail.endWidth = 0.0f;
            trail.sharedMaterial = matBlue;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(0.25f, 0.72f, 1.0f), 0.35f),
                    new GradientColorKey(new Color(0.04f, 0.32f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.75f, 0.45f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = grad;

            // 5. Two Coiled Lightning Arcs
            GameObject coilA = new GameObject("CoiledLightningArc_A");
            coilA.transform.SetParent(root.transform, false);
            LineRenderer lrA = AddConfiguredLineRenderer(coilA, matArc, 0.085f, 0.02f, Color.white, new Color(0.25f, 0.78f, 1.0f, 1.0f));
            PopulateHelixPoints(lrA, 2.8f, 0.28f, 14, 0f);

            GameObject coilB = new GameObject("CoiledLightningArc_B");
            coilB.transform.SetParent(root.transform, false);
            LineRenderer lrB = AddConfiguredLineRenderer(coilB, matArc, 0.065f, 0.015f, new Color(0.35f, 0.82f, 1.0f, 1.0f), new Color(0.05f, 0.4f, 1.0f, 0.9f));
            PopulateHelixPoints(lrB, 2.6f, 0.34f, 14, 2.1f);

            // 6. Continuous Trailing Sparks
            CreateConfiguredSparkParticleSystem(root.transform, "TrailingSparks_WhiteBlue", matWhite, 32, 2.2f, 0.50f, true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildImpactSpherePrefab(
            string prefabPath,
            Mesh sphereMesh,
            Material matWhite,
            Material matBlue,
            Material matArc,
            Material matDome)
        {
            GameObject root = new GameObject("PF_VFX_3_Lightning_ImpactSphere_WhiteBlue");

            // 1. Translucent Glowing Blue Sphere Shell
            GameObject domeObj;
            if (sphereMesh != null)
            {
                domeObj = new GameObject("TranslucentBlueEnergySphere");
                domeObj.transform.SetParent(root.transform, false);
                domeObj.transform.localScale = Vector3.one * 2.8f;
                domeObj.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                domeObj.AddComponent<MeshRenderer>().sharedMaterial = matDome;
            }
            else
            {
                domeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                domeObj.name = "TranslucentBlueEnergySphere";
                domeObj.transform.SetParent(root.transform, false);
                domeObj.transform.localScale = Vector3.one * 2.8f;
                if (domeObj.TryGetComponent<Collider>(out var c)) Object.DestroyImmediate(c);
                domeObj.GetComponent<Renderer>().sharedMaterial = matDome;
            }

            // 2. 12 Surface Crackling White-Blue Lightning Arcs wrapping the sphere
            for (int i = 0; i < 12; i++)
            {
                GameObject arcObj = new GameObject($"SphereLightningArc_{i + 1}");
                arcObj.transform.SetParent(root.transform, false);
                bool primaryWhite = (i % 2 == 0);
                LineRenderer lr = AddConfiguredLineRenderer(
                    arcObj,
                    matArc,
                    primaryWhite ? 0.11f : 0.075f,
                    0.015f,
                    primaryWhite ? Color.white : new Color(0.35f, 0.82f, 1.0f, 1.0f),
                    primaryWhite ? new Color(0.25f, 0.75f, 1.0f, 1.0f) : new Color(0.05f, 0.42f, 1.0f, 0.9f)
                );
                PopulateSphereArcPoints(lr, 1.44f, 16, 0.22f, i * 31 + 7);
            }

            // 3. Impact Burst Sparks
            CreateConfiguredSparkParticleSystem(root.transform, "ImpactSparks_WhiteBlue", matWhite, 38, 5.5f, 0.55f, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static LineRenderer AddConfiguredLineRenderer(GameObject go, Material mat, float startW, float endW, Color startCol, Color endCol)
        {
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.startColor = startCol;
            lr.endColor = endCol;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static void PopulateSphereArcPoints(LineRenderer lr, float radius, int segments, float jitter, int seed)
        {
            Random.State oldState = Random.state;
            Random.InitState(seed);
            lr.positionCount = segments + 1;
            Vector3 startDir = Random.onUnitSphere;
            Vector3 endDir = Vector3.Slerp(startDir, -startDir + Random.insideUnitSphere * 0.6f, 0.75f).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 dir = Vector3.Slerp(startDir, endDir, t).normalized;
                float midWeight = Mathf.Sin(t * Mathf.PI);
                Vector3 pt = (dir * radius) + Random.insideUnitSphere * (jitter * midWeight);
                if (pt.magnitude < radius * 0.94f)
                    pt = pt.normalized * (radius * 1.02f);
                lr.SetPosition(i, pt);
            }
            Random.state = oldState;
        }

        private static void PopulateHelixPoints(LineRenderer lr, float length, float radius, int segments, float phase)
        {
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float z = Mathf.Lerp(-length * 0.78f, length * 0.22f, t);
                float env = Mathf.Sin(t * Mathf.PI);
                float ang = t * Mathf.PI * 3.5f + phase;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius * env, Mathf.Sin(ang) * radius * env, z));
            }
        }

        private static void CreateConfiguredSparkParticleSystem(Transform parent, string name, Material mat, int count, float speed, float lifetime, bool continuous)
        {
            GameObject psObj = new GameObject(name);
            psObj.transform.SetParent(parent, false);
            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.18f, 0.68f, 1.0f, 1.0f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = continuous;

            var emission = ps.emission;
            if (continuous)
            {
                emission.rateOverTime = count * 1.4f;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
            }

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var rend = psObj.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.4f;
            rend.velocityScale = 0.08f;
            rend.sharedMaterial = mat;
        }

        private static void EnsureFolders()
        {
            string[] folders = { "Assets/_Project", BaseFolder, MaterialsFolder, PrefabsFolder, TexturesFolder, MeshesFolder };
            foreach (string f in folders)
            {
                if (!AssetDatabase.IsValidFolder(f))
                {
                    string parent = Path.GetDirectoryName(f).Replace("\\", "/");
                    string name = Path.GetFileName(f);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static Mesh FindSubMeshInObj(string meshName)
        {
            string[] guids = AssetDatabase.FindAssets("VFX_Lightning_WhiteBlue_Meshes t:Model");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                Object[] reps = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object obj in reps)
                {
                    if (obj is Mesh m && m.name.Contains(meshName))
                        return m;
                }
            }
            return null;
        }

        private static Texture2D FindOrCreateTexture(string texName, System.Func<Texture2D> fallbackGenerator)
        {
            string[] guids = AssetDatabase.FindAssets(texName + " t:Texture2D");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            Texture2D generated = fallbackGenerator();
            string outPath = TexturesFolder + "/" + texName + ".png";
            File.WriteAllBytes(outPath, generated.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Texture2D CreateGlowTexture()
        {
            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    Color c = Color.Lerp(new Color(0.1f, 0.6f, 1.0f, a), new Color(1f, 1f, 1f, a), a * a);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateStreakTexture()
        {
            return CreateGlowTexture();
        }

        private static Texture2D CreateArcTexture()
        {
            return CreateGlowTexture();
        }

        private static Material CreateOrUpdateAdditiveMaterial(string path, Texture2D tex, Color hdrColor)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdrColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdrColor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateTranslucentDomeMaterial(string path, Texture2D tex, Color tint)
        {
            Shader sh = Shader.Find("Sprites/Default");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            mat.mainTexture = tex;
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif

/*
====================================================================================================
คำอธิบายการทำงานของโค้ดและฟังก์ชันในไฟล์ LightningVFXPrefabBuilder.cs (ภาษาไทย)
====================================================================================================
1. หน้าที่หลักของสคริปต์ (Overview):
   - เป็นสคริปต์ Editor สำหรับประกอบร่างไฟล์ Mesh, Texture, Material และสคริปต์ในแพ็กเกจนี้ให้กลายเป็นไฟล์
     Unity Prefab (.prefab) ทั้ง 4 ตัวโดยอัตโนมัติในคลิกเดียว

2. วิธีเรียกใช้งานใน Unity Editor:
   - ไปที่แถบเมนูด้านบนของ Unity เลือก:
     Tools -> MahouShoujo VFX -> Build All White-Blue Lightning Prefabs (1-Click)
   - ระบบจะสร้างโฟลเดอร์ Assets/_Project/VFX_Lightning_WhiteBlue/Prefabs/ พร้อมสร้าง Prefab ทั้ง 4 ตัวให้ทันที:
     (1) PF_VFX_1_Lightning_CastBurst_WhiteBlue.prefab      : ประจุสายฟ้าจังหวะปล่อยออกจากตัวละคร
     (2) PF_VFX_2_Lightning_SingleBolt_WhiteBlue.prefab     : ลำแสงสายฟ้าวิ่งเป็นเส้น 1 เส้น
     (3) PF_VFX_3_Lightning_ImpactSphere_WhiteBlue.prefab   : ลูกแก้วสายฟ้าตอนปะทะเป้าหมาย
     (4) PF_VFX_Lightning_CompleteSequence_WhiteBlue.prefab : ตัวรันลำดับ 1 -> 2 -> 3 อัตโนมัติ
====================================================================================================
*/
