#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MahouShoujo.VFX.Editor
{
    // =========================================================================
    // VFX_RECIPE.JSON DATA SCHEMA (100% Backward & Future Compatible)
    // =========================================================================

    [Serializable]
    public struct HDRColorData
    {
        public float r;
        public float g;
        public float b;
        public float a;
        public Color ToColor() => new Color(r, g, b, a <= 0f ? 1f : a);
    }

    [Serializable]
    public struct TrackTimingData
    {
        public string id;
        public string label;
        public float startDelay;
        public float duration;
    }

    [Serializable]
    public struct CurveKeyData
    {
        public float time;
        public float value;
    }

    [Serializable]
    public class BakedCurvesData
    {
        public CurveKeyData[] scaleCurve;
        public CurveKeyData[] velocityCurve;
        public CurveKeyData[] alphaDissolveCurve;
        public CurveKeyData[] hdrGlowCurve;
    }

    [Serializable]
    public class VFXStudioRecipe
    {
        public string vfxName;
        public string archetype;       // "Projectile", "Beam", "Slash", "AoE", "Impact"
        public string variant;
        public string element;
        public string blendMode;       // "Additive" or "AlphaBlend"
        public float totalDuration = 1.5f;
        public bool loop;
        public HDRColorData coreColorHDR;
        public HDRColorData edgeColorHDR;
        public float distortionStrength;
        public float dissolveThreshold;
        public float sharpness = 1f;
        public float fresnelPower = 3f;
        public float fresnelIntensity = 1.5f;
        public float uvScrollX;
        public float uvScrollY;
        public float scale = 1f;
        public int particleCount = 24;
        public string shaderName = "Custom/URP_StylizedVFX";
        public TrackTimingData[] tracks;
        public BakedCurvesData curves;

        // Future-proof optional overrides (safe if omitted in older/external VFX_Recipe.json files)
        public string noiseTextureName;
        public string runeTextureName;
        public string outputSubfolder;
        public bool disableAutoPoolingComponent;
    }

    /// <summary>
    /// Unified VFX Studio & Lightning Prefab Builder for Unity 6 URP.
    /// Replaces legacy UnityVFXPrefabsBuilder, LightningProjectilePrefabBuilder, and LightningVFXPrefabBuilder.
    /// - Writes cleanly into categorized Assets/_Project/Art/VFX/{Prefabs,Materials,Meshes,Textures,Shaders,Recipes}
    ///   and Assets/_Project/Data/SkillPatterns/.
    /// - Updates existing Materials & Prefabs in-place so GUID references NEVER break when re-building recipes!
    /// - Resolves Shaders, Textures, and Meshes from the Recipe's local folder first, then falls back cleanly
    ///   to centralized Assets/_Project/Art/VFX/ folders so future recipes only need a single .json file.
    /// </summary>
    public static class UnifiedVFXStudioBuilder
    {
        private const string VfxRootFolder = "Assets/_Project/Art/VFX";
        private const string PrefabsFolder = VfxRootFolder + "/Prefabs";
        private const string MaterialsFolder = VfxRootFolder + "/Materials";
        private const string MeshesFolder = VfxRootFolder + "/Meshes";
        private const string TexturesFolder = VfxRootFolder + "/Textures";
        private const string ShadersFolder = VfxRootFolder + "/Shaders";
        private const string RecipesFolder = VfxRootFolder + "/Recipes";
        private const string PatternsFolder = "Assets/_Project/Data/SkillPatterns";

        // =====================================================================
        // MENU 1: BUILD SINGLE VFX_RECIPE.JSON
        // =====================================================================

        [MenuItem("Tools/MahouShoujo VFX/Build From VFX_Recipe.json (Single)", false, 1)]
        [MenuItem("Tools/Stylized VFX Studio/Build Prefabs With Baked Curves", false, 1)]
        public static void BuildFromSingleRecipeDialog()
        {
            string startDir = Directory.Exists(Path.GetFullPath(RecipesFolder))
                ? Path.GetFullPath(RecipesFolder)
                : Application.dataPath;

            string jsonPath = EditorUtility.OpenFilePanel("Select VFX_Recipe.json", startDir, "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            GameObject created = BuildFromRecipePath(jsonPath);
            if (created != null)
            {
                Selection.activeObject = created;
                EditorGUIUtility.PingObject(created);
            }
        }

        // =====================================================================
        // MENU 2: BATCH BUILD ALL RECIPES IN Art/VFX/Recipes/
        // =====================================================================

        [MenuItem("Tools/MahouShoujo VFX/Batch Build All Recipes in Art/VFX/Recipes", false, 2)]
        public static void BatchBuildAllRecipes()
        {
            EnsureCategorizedFolders();
            string absRecipesDir = Path.GetFullPath(RecipesFolder);
            if (!Directory.Exists(absRecipesDir))
            {
                Debug.LogWarning($"[UnifiedVFXStudioBuilder] Recipes folder not found: {RecipesFolder}");
                return;
            }

            string[] jsonFiles = Directory.GetFiles(absRecipesDir, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"[UnifiedVFXStudioBuilder] No .json recipe files found in {RecipesFolder}");
                return;
            }

            int builtCount = 0;
            for (int i = 0; i < jsonFiles.Length; i++)
            {
                if (BuildFromRecipePath(jsonFiles[i]) != null)
                    builtCount++;
            }

            Debug.Log($"[UnifiedVFXStudioBuilder] Batch build complete: {builtCount}/{jsonFiles.Length} recipe(s) built into {PrefabsFolder}");
        }

        // =====================================================================
        // CORE RECIPE BUILDER (GUID-SAFE IN-PLACE UPDATE + SMART FALLBACK)
        // =====================================================================

        public static GameObject BuildFromRecipePath(string absoluteJsonPath)
        {
            if (string.IsNullOrEmpty(absoluteJsonPath) || !File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[UnifiedVFXStudioBuilder] Recipe file not found: {absoluteJsonPath}");
                return null;
            }

            EnsureCategorizedFolders();

            string jsonText = File.ReadAllText(absoluteJsonPath);
            VFXStudioRecipe recipe = JsonUtility.FromJson<VFXStudioRecipe>(jsonText);
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.vfxName))
            {
                Debug.LogError($"[UnifiedVFXStudioBuilder] Invalid VFX_Recipe JSON (missing vfxName): {absoluteJsonPath}");
                return null;
            }

            // Optional local asset folder if JSON lives inside Assets/ (supports newly extracted external packs)
            string localAssetFolder = TryConvertToAssetPath(Path.GetDirectoryName(absoluteJsonPath));

            Shader shader = ResolveShader(recipe.shaderName, localAssetFolder);
            if (shader == null)
            {
                Debug.LogError($"[UnifiedVFXStudioBuilder] Shader '{recipe.shaderName}' not found (checked Shader.Find, {localAssetFolder}, and {ShadersFolder}).");
                return null;
            }

            string noiseName = string.IsNullOrWhiteSpace(recipe.noiseTextureName) ? "VFX_Noise" : recipe.noiseTextureName;
            string runeName = string.IsNullOrWhiteSpace(recipe.runeTextureName) ? "VFX_Runes" : recipe.runeTextureName;
            Texture2D noiseTex = ResolveTexture(noiseName, localAssetFolder);
            Texture2D runeTex = ResolveTexture(runeName, localAssetFolder);

            // Materials -> Always stored cleanly in Art/VFX/Materials/ (updated in-place to preserve GUIDs!)
            string matPrefix = MaterialsFolder + "/" + recipe.vfxName;
            Material mainMat = CreateOrUpdateStylizedMat(matPrefix + "_Main.mat", shader, noiseTex, recipe, false);
            Material runeMat = CreateOrUpdateStylizedMat(matPrefix + "_Runes.mat", shader, runeTex, recipe, true);

            // Output Prefab directory (Art/VFX/Prefabs or Art/VFX/Prefabs/<outputSubfolder>)
            string targetPrefabDir = PrefabsFolder;
            if (!string.IsNullOrWhiteSpace(recipe.outputSubfolder))
            {
                string cleanSub = recipe.outputSubfolder.Trim().Replace("\\", "/").Trim('/');
                targetPrefabDir = PrefabsFolder + "/" + cleanSub;
                EnsureFolderPath(targetPrefabDir);
            }

            BakedCurvesData curves = recipe.curves ?? new BakedCurvesData();
            AnimationCurve scaleAnim = BuildAnimCurve(curves.scaleCurve, 1f);
            AnimationCurve velAnim = BuildAnimCurve(curves.velocityCurve, 1f);
            AnimationCurve alphaAnim = BuildAnimCurve(curves.alphaDissolveCurve, 1f);
            AnimationCurve hdrAnim = BuildAnimCurve(curves.hdrGlowCurve, 1f);

            TrackTimingData trCharge = FindTrack(recipe.tracks, "charge", 0f, 0.5f);
            TrackTimingData trCore = FindTrack(recipe.tracks, "core", 0.2f, 1.2f);
            TrackTimingData trTrail = FindTrack(recipe.tracks, "trail", 0.2f, 1.3f);
            TrackTimingData trImpact = FindTrack(recipe.tracks, "impact", 1.2f, 0.9f);

            Mesh slashMesh = ResolveMesh("VFX_Slash", localAssetFolder);
            Mesh headMesh = ResolveMesh("VFX_ProjectileHead", localAssetFolder);
            Mesh trailMesh = ResolveMesh("VFX_TrailRibbon", localAssetFolder);
            Mesh beamMesh = ResolveMesh("VFX_BeamCylinder", localAssetFolder);
            Mesh helixMesh = ResolveMesh("VFX_BeamHelix", localAssetFolder);
            Mesh ringMesh = ResolveMesh("VFX_Ring", localAssetFolder);
            Mesh domeMesh = ResolveMesh("VFX_Dome", localAssetFolder);
            Mesh pillarMesh = ResolveMesh("VFX_Pillar", localAssetFolder);

            bool addPooledComp = !recipe.disableAutoPoolingComponent;
            GameObject primaryResult;

            if (string.Equals(recipe.archetype, "Projectile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recipe.archetype, "Beam", StringComparison.OrdinalIgnoreCase))
            {
                bool isBeam = string.Equals(recipe.archetype, "Beam", StringComparison.OrdinalIgnoreCase);

                // 1. Muzzle / Charge
                SaveSingleRecipePrefab(
                    targetPrefabDir + "/" + recipe.vfxName + "_Muzzle.prefab",
                    recipe.vfxName + "_Muzzle",
                    ringMesh, mainMat, recipe, trCharge, scaleAnim, velAnim, alphaAnim, hdrAnim, 14, addPooledComp);

                // 2. Main Projectile / Beam
                GameObject mainRoot = new GameObject(recipe.vfxName + "_Main");
                mainRoot.transform.localScale = Vector3.one * Mathf.Max(0.2f, recipe.scale);
                if (addPooledComp) mainRoot.AddComponent<PooledVFXInstance>();
                AddMeshLayer(mainRoot, "Core_Shape", isBeam ? beamMesh : headMesh, mainMat, recipe, trCore, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(mainRoot, "Secondary_TrailOrHelix", isBeam ? helixMesh : trailMesh, mainMat, recipe, trTrail, scaleAnim, alphaAnim, hdrAnim);
                primaryResult = PrefabUtility.SaveAsPrefabAsset(mainRoot, targetPrefabDir + "/" + recipe.vfxName + "_Main.prefab");
                UnityEngine.Object.DestroyImmediate(mainRoot);

                // 3. Impact Explosion
                SaveSingleRecipePrefab(
                    targetPrefabDir + "/" + recipe.vfxName + "_Impact.prefab",
                    recipe.vfxName + "_Impact",
                    domeMesh, mainMat, recipe, trImpact, scaleAnim, velAnim, alphaAnim, hdrAnim, recipe.particleCount, addPooledComp);
            }
            else
            {
                Mesh primaryMesh = string.Equals(recipe.archetype, "Slash", StringComparison.OrdinalIgnoreCase)
                    ? slashMesh
                    : (string.Equals(recipe.archetype, "AoE", StringComparison.OrdinalIgnoreCase) ? pillarMesh : domeMesh);

                GameObject root = new GameObject(recipe.vfxName);
                root.transform.localScale = Vector3.one * Mathf.Max(0.2f, recipe.scale);
                if (addPooledComp) root.AddComponent<PooledVFXInstance>();
                AddMeshLayer(root, "Track1_Charge", ringMesh, runeMat, recipe, trCharge, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(root, "Track2_CoreShape", primaryMesh, mainMat, recipe, trCore, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(root, "Track3_SecondaryRing", ringMesh, mainMat, recipe, trTrail, scaleAnim, alphaAnim, hdrAnim);
                if (recipe.particleCount > 0)
                    AddSparkLayer(root, "Track4_ImpactSparks", mainMat, recipe, trImpact, velAnim, alphaAnim, recipe.particleCount);

                primaryResult = PrefabUtility.SaveAsPrefabAsset(root, targetPrefabDir + "/" + recipe.vfxName + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UnifiedVFXStudioBuilder] Built '{recipe.vfxName}' ({recipe.archetype}) -> Materials: {MaterialsFolder}, Prefabs: {targetPrefabDir}");
            return primaryResult;
        }

        // =====================================================================
        // MENU 3: UNIFIED WHITE-BLUE LIGHTNING + 4 SAMPLE PATTERNS (CLEAN PATHS)
        // =====================================================================

        [MenuItem("Tools/MahouShoujo VFX/Build & Auto-Wire Unified Lightning + Patterns (URP)", false, 20)]
        [MenuItem("GameObject/VFX/Build & Auto-Wire Unified Lightning + Patterns (URP)", false, 11)]
        public static void BuildAndWireUnifiedLightningPack()
        {
            EnsureCategorizedFolders();

            Texture2D streakTex = FindOrCreateTexture("T_ProjectileStreak_WhiteBlue", CreateSeamlessLightningStreakTexture);
            Texture2D glowTex = FindOrCreateTexture("T_ElectricGlow_WhiteBlue", CreateRadialGlowTexture);
            Texture2D noiseTex = FindOrCreateTexture("T_LightningDissolveNoise", CreateTileableNoiseTexture);

            Material matInnerTrail = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_InnerTrail_HDR_URP.mat",
                streakTex, noiseTex,
                new Color(3.4f, 4.0f, 4.8f, 1.0f),
                new Color(0.22f, 0.85f, 3.4f, 1.0f),
                new Vector4(-2.8f, 0f, 0f, 0f), 0.08f, 36f, 1.3f);

            Material matOuterTrail = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_OuterTrail_Blue_URP.mat",
                streakTex, noiseTex,
                new Color(0.20f, 0.72f, 2.8f, 0.92f),
                new Color(0.04f, 0.22f, 1.4f, 0.55f),
                new Vector4(-1.8f, 0.1f, 0f, 0f), 0.12f, 26f, 1.6f);

            Material matCoreParticle = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_CoreSpark_HDR_URP.mat",
                glowTex, noiseTex,
                new Color(3.2f, 3.8f, 4.5f, 1.0f),
                new Color(0.18f, 0.75f, 3.2f, 1.0f),
                Vector4.zero, 0.02f, 20f, 0.4f);

            Mesh headMesh = ResolveMesh("VFX_ProjectileHead", null);

            GameObject muzzlePrefab = BuildLightningMuzzlePrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Muzzle.prefab",
                matCoreParticle, matInnerTrail);

            GameObject impactPrefab = BuildLightningImpactPrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Impact.prefab",
                matCoreParticle, matInnerTrail, matOuterTrail);

            GameObject mainProjectileGo = BuildLightningMainProjectilePrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Main.prefab",
                headMesh, matCoreParticle, matInnerTrail, matOuterTrail, muzzlePrefab, impactPrefab);

            MagicProjectile mainProjectileComp = mainProjectileGo != null
                ? mainProjectileGo.GetComponent<MagicProjectile>()
                : null;

            CreateSamplePatterns(mainProjectileComp, muzzlePrefab, impactPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int wiredCount = AutoWireSceneComponents(muzzlePrefab, mainProjectileComp, impactPrefab);
            Selection.activeObject = mainProjectileGo;
            EditorGUIUtility.PingObject(mainProjectileGo);
            Debug.Log($"[UnifiedVFXStudioBuilder] Built 3-Stage Lightning Prefabs in {PrefabsFolder} + 4 Sample Patterns in {PatternsFolder} (Auto-wired {wiredCount} scene components).");
        }

        // =====================================================================
        // SMART ASSET RESOLVERS (LOCAL PACK FIRST -> CENTRAL Art/VFX FALLBACK)
        // =====================================================================

        private static Shader ResolveShader(string shaderName, string localFolder)
        {
            if (!string.IsNullOrWhiteSpace(shaderName))
            {
                Shader found = Shader.Find(shaderName);
                if (found != null) return found;
            }

            if (!string.IsNullOrEmpty(localFolder))
            {
                Shader local = AssetDatabase.LoadAssetAtPath<Shader>(localFolder + "/Shaders/URP_StylizedVFX.shader");
                if (local != null) return local;
            }

            Shader central = AssetDatabase.LoadAssetAtPath<Shader>(ShadersFolder + "/URP_StylizedVFX.shader");
            if (central != null) return central;

            return Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        private static Texture2D ResolveTexture(string texName, string localFolder)
        {
            if (string.IsNullOrWhiteSpace(texName)) return null;
            string clean = Path.GetFileNameWithoutExtension(texName);

            if (!string.IsNullOrEmpty(localFolder))
            {
                Texture2D local = AssetDatabase.LoadAssetAtPath<Texture2D>(localFolder + "/Textures/" + clean + ".png");
                if (local != null) return local;
            }

            Texture2D central = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesFolder + "/" + clean + ".png");
            if (central != null) return central;

            string[] guids = AssetDatabase.FindAssets(clean + " t:Texture2D");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static Mesh ResolveMesh(string meshBaseName, string localFolder)
        {
            if (string.IsNullOrWhiteSpace(meshBaseName)) return null;
            string clean = Path.GetFileNameWithoutExtension(meshBaseName);

            if (!string.IsNullOrEmpty(localFolder))
            {
                Mesh local = AssetDatabase.LoadAssetAtPath<Mesh>(localFolder + "/Meshes/" + clean + ".obj");
                if (local != null) return local;
            }

            Mesh central = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/" + clean + ".obj");
            if (central != null) return central;

            string[] guids = AssetDatabase.FindAssets(clean + " t:Mesh");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guids[0]));

            string[] modelGuids = AssetDatabase.FindAssets(clean + " t:Model");
            for (int i = 0; i < modelGuids.Length; i++)
            {
                UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(modelGuids[i]));
                for (int j = 0; j < subAssets.Length; j++)
                {
                    if (subAssets[j] is Mesh m) return m;
                }
            }

            return null;
        }

        private static string TryConvertToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            string normAbs = Path.GetFullPath(absolutePath).Replace("\\", "/");
            string normData = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
            if (normAbs.StartsWith(normData, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + normAbs.Substring(normData.Length);
            }
            return null;
        }

        // =====================================================================
        // RECIPE PREFAB & MATERIAL HELPERS
        // =====================================================================

        private static Material CreateOrUpdateStylizedMat(string assetPath, Shader shader, Texture2D tex, VFXStudioRecipe r, bool isRuneMat)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, assetPath);
            }
            else
            {
                m.shader = shader;
            }

            if (tex != null && m.HasProperty("_NoiseTex")) m.SetTexture("_NoiseTex", tex);
            if (m.HasProperty("_CoreColor")) m.SetColor("_CoreColor", r.coreColorHDR.ToColor());
            if (m.HasProperty("_EdgeColor")) m.SetColor("_EdgeColor", r.edgeColorHDR.ToColor());
            if (m.HasProperty("_ScrollSpeed")) m.SetVector("_ScrollSpeed", isRuneMat ? Vector4.zero : new Vector4(r.uvScrollX, r.uvScrollY, 0f, 0f));
            if (m.HasProperty("_DistortionStrength")) m.SetFloat("_DistortionStrength", isRuneMat ? 0.02f : r.distortionStrength);
            if (m.HasProperty("_DissolveThreshold")) m.SetFloat("_DissolveThreshold", r.dissolveThreshold);
            if (m.HasProperty("_Sharpness")) m.SetFloat("_Sharpness", r.sharpness);
            if (m.HasProperty("_FresnelPower")) m.SetFloat("_FresnelPower", r.fresnelPower);
            if (m.HasProperty("_FresnelIntensity")) m.SetFloat("_FresnelIntensity", r.fresnelIntensity);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)(r.blendMode == "AlphaBlend" ? BlendMode.OneMinusSrcAlpha : BlendMode.One));

            EditorUtility.SetDirty(m);
            return m;
        }

        private static AnimationCurve BuildAnimCurve(CurveKeyData[] keys, float fallback)
        {
            if (keys == null || keys.Length == 0) return AnimationCurve.Constant(0f, 1f, fallback);
            Keyframe[] kf = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) kf[i] = new Keyframe(keys[i].time, keys[i].value);
            AnimationCurve curve = new AnimationCurve(kf);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0.25f);
            return curve;
        }

        private static TrackTimingData FindTrack(TrackTimingData[] list, string id, float defDelay, float defDur)
        {
            if (list != null)
            {
                for (int i = 0; i < list.Length; i++)
                    if (string.Equals(list[i].id, id, StringComparison.OrdinalIgnoreCase)) return list[i];
            }
            return new TrackTimingData { id = id, startDelay = defDelay, duration = defDur };
        }

        private static void SaveSingleRecipePrefab(string path, string objName, Mesh mesh, Material mat, VFXStudioRecipe r,
            TrackTimingData tr, AnimationCurve scaleAnim, AnimationCurve velAnim, AnimationCurve alphaAnim, AnimationCurve hdrAnim,
            int sparks, bool addPooledComp)
        {
            GameObject root = new GameObject(objName);
            root.transform.localScale = Vector3.one * Mathf.Max(0.2f, r.scale);
            if (addPooledComp) root.AddComponent<PooledVFXInstance>();
            AddMeshLayer(root, "Primary_Mesh", mesh, mat, r, tr, scaleAnim, alphaAnim, hdrAnim);
            if (sparks > 0) AddSparkLayer(root, "Sparks", mat, r, tr, velAnim, alphaAnim, sparks);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void AddMeshLayer(GameObject parent, string childName, Mesh mesh, Material mat, VFXStudioRecipe r,
            TrackTimingData tr, AnimationCurve scaleAnim, AnimationCurve alphaAnim, AnimationCurve hdrAnim)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = Mathf.Max(0.2f, r.totalDuration);
            main.loop = r.loop;
            main.startDelay = tr.startDelay;
            main.startLifetime = Mathf.Max(0.2f, tr.duration);
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var sh = ps.shape;
            sh.enabled = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, scaleAnim);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            GradientAlphaKey[] aKeys = new GradientAlphaKey[6];
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                aKeys[i] = new GradientAlphaKey(Mathf.Clamp01(alphaAnim.Evaluate(t)), t);
            }
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, aKeys);
            col.color = g;

            var cd = ps.customData;
            cd.enabled = true;
            cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, alphaAnim));
            cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f, hdrAnim));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            if (mesh != null) rend.mesh = mesh;
            rend.sharedMaterial = mat;
            rend.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XY
            });
        }

        private static void AddSparkLayer(GameObject parent, string childName, Material mat, VFXStudioRecipe r,
            TrackTimingData tr, AnimationCurve velAnim, AnimationCurve alphaAnim, int count)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = Mathf.Max(0.2f, r.totalDuration);
            main.loop = r.loop;
            main.startDelay = tr.startDelay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(tr.duration * 0.4f, tr.duration);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = r.coreColorHDR.ToColor();
            main.maxParticles = Mathf.Max(32, count * 2);

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.35f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, velAnim);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.4f;
            rend.sharedMaterial = mat;
        }

        // =====================================================================
        // UNIFIED LIGHTNING PREFAB & SAMPLE PATTERN HELPERS
        // =====================================================================

        private static void CreateSamplePatterns(MagicProjectile mainProj, GameObject muzzle, GameObject impact)
        {
            CreateOrUpdatePattern(PatternsFolder + "/PAT_Ranged_LightningChainSnipe.asset", p =>
            {
                p.patternID = "PAT_RANGED_CHAIN_01";
                p.displayName = "Lightning Chain Snipe";
                p.deliveryCategory = SkillDeliveryCategory.Projectile;
                p.patternPreset = CombatPatternPreset.AimedSnipe;
                p.allowedArchetypes = SkillArchetypeMask.RangedCaster | SkillArchetypeMask.Enemy;
                p.count = 1;
                p.speed = 38f;
                p.impactBehavior = ProjectileImpactBehavior.ChainBounce;
                p.maxPierceOrBounceCount = 3;
                p.chainSearchRadius = 6.5f;
                p.customProjectilePrefab = mainProj;
                p.muzzleCastVFXPrefab = muzzle;
                p.impactVFXPrefab = impact;
                p.useWhiteBlueLightningVFX = true;
            });

            CreateOrUpdatePattern(PatternsFolder + "/PAT_Ranged_ItanoHoming5.asset", p =>
            {
                p.patternID = "PAT_RANGED_ITANO_02";
                p.displayName = "Delayed Homing Lightning Barrage (5)";
                p.deliveryCategory = SkillDeliveryCategory.Projectile;
                p.patternPreset = CombatPatternPreset.DelayedHomingItano;
                p.allowedArchetypes = SkillArchetypeMask.RangedCaster;
                p.count = 5;
                p.windupOrDelayDuration = 0.28f;
                p.formationRadius = 1.9f;
                p.speed = 34f;
                p.homingAngularSpeed = 300f;
                p.directDamageMultiplier = 0.75f;
                p.customProjectilePrefab = mainProj;
                p.muzzleCastVFXPrefab = muzzle;
                p.impactVFXPrefab = impact;
                p.useWhiteBlueLightningVFX = true;
            });

            CreateOrUpdatePattern(PatternsFolder + "/PAT_Beam_TelegraphedLaser.asset", p =>
            {
                p.patternID = "PAT_BEAM_TELEGRAPH_03";
                p.displayName = "Telegraphed Lightning Rail Beam";
                p.deliveryCategory = SkillDeliveryCategory.Beam;
                p.patternPreset = CombatPatternPreset.TelegraphedBeam;
                p.allowedArchetypes = SkillArchetypeMask.RangedCaster | SkillArchetypeMask.Enemy;
                p.windupOrDelayDuration = 0.45f;
                p.activeDuration = 1.4f;
                p.damageTickInterval = 0.18f;
                p.beamRange = 20f;
                p.beamWidth = 0.9f;
                p.directDamageMultiplier = 1.6f;
                p.muzzleCastVFXPrefab = muzzle;
                p.impactVFXPrefab = impact;
                p.useWhiteBlueLightningVFX = true;
            });

            CreateOrUpdatePattern(PatternsFolder + "/PAT_Melee_SlashWaveCrescent.asset", p =>
            {
                p.patternID = "PAT_MELEE_SLASHWAVE_04";
                p.displayName = "Crescent Lightning Slash Wave";
                p.deliveryCategory = SkillDeliveryCategory.Projectile;
                p.patternPreset = CombatPatternPreset.SlashWaveCrescent;
                p.allowedArchetypes = SkillArchetypeMask.MeleeStriker | SkillArchetypeMask.HeavyBreaker;
                p.count = 1;
                p.speed = 28f;
                p.lifeTime = 1.2f;
                p.impactBehavior = ProjectileImpactBehavior.PierceTargets;
                p.maxPierceOrBounceCount = 4;
                p.directDamageMultiplier = 1.35f;
                p.customProjectilePrefab = mainProj;
                p.muzzleCastVFXPrefab = muzzle;
                p.impactVFXPrefab = impact;
                p.useWhiteBlueLightningVFX = true;
            });
        }

        private static void CreateOrUpdatePattern(string path, Action<CombatSkillPattern_SO> configure)
        {
            CombatSkillPattern_SO asset = AssetDatabase.LoadAssetAtPath<CombatSkillPattern_SO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CombatSkillPattern_SO>();
                AssetDatabase.CreateAsset(asset, path);
            }
            configure(asset);
            EditorUtility.SetDirty(asset);
        }

        private static int AutoWireSceneComponents(GameObject muzzle, MagicProjectile mainProj, GameObject impact)
        {
            int wired = 0;

            UniversalVFXManager vfxMgr = UnityEngine.Object.FindFirstObjectByType<UniversalVFXManager>(FindObjectsInactive.Include);
            if (vfxMgr != null)
            {
                Undo.RecordObject(vfxMgr, "Auto-Wire Lightning Prefabs to UniversalVFXManager");
                SerializedObject soMgr = new SerializedObject(vfxMgr);
                soMgr.FindProperty("lightningMuzzlePrefab").objectReferenceValue = muzzle;
                soMgr.FindProperty("lightningProjectileMainPrefab").objectReferenceValue = mainProj;
                soMgr.FindProperty("lightningImpactPrefab").objectReferenceValue = impact;
                soMgr.ApplyModifiedProperties();
                EditorUtility.SetDirty(vfxMgr);
                wired++;
            }

            PlayerCombat combat = UnityEngine.Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            if (combat != null && mainProj != null)
            {
                Undo.RecordObject(combat, "Auto-Wire Lightning Projectile to PlayerCombat");
                SerializedObject soCombat = new SerializedObject(combat);
                SerializedProperty projProp = soCombat.FindProperty("magicProjectilePrefab");
                if (projProp != null && projProp.objectReferenceValue == null)
                {
                    projProp.objectReferenceValue = mainProj;
                    soCombat.ApplyModifiedProperties();
                    EditorUtility.SetDirty(combat);
                    wired++;
                }
            }

            return wired;
        }

        private static GameObject BuildLightningMuzzlePrefab(string path, Material matGlow, Material matArc)
        {
            GameObject root = new GameObject("PF_VFX_LightningProjectile_Muzzle");
            root.AddComponent<PooledVFXInstance>();

            GameObject chargeGo = new GameObject("Muzzle_InwardCharge");
            chargeGo.transform.SetParent(root.transform, false);
            ParticleSystem chargePs = chargeGo.AddComponent<ParticleSystem>();
            var cMain = chargePs.main;
            cMain.loop = false;
            cMain.duration = 0.25f;
            cMain.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.16f);
            cMain.startSpeed = new ParticleSystem.MinMaxCurve(-2.8f, -1.6f);
            cMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            cMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.25f, 0.82f, 1f, 1f));
            var cEmit = chargePs.emission;
            cEmit.rateOverTime = 0f;
            cEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var cShape = chargePs.shape;
            cShape.shapeType = ParticleSystemShapeType.Sphere;
            cShape.radius = 0.42f;
            var cRend = chargeGo.GetComponent<ParticleSystemRenderer>();
            cRend.renderMode = ParticleSystemRenderMode.Stretch;
            cRend.lengthScale = 2.0f;
            cRend.sharedMaterial = matGlow;

            GameObject flashGo = new GameObject("Muzzle_CoreFlash");
            flashGo.transform.SetParent(root.transform, false);
            ParticleSystem flashPs = flashGo.AddComponent<ParticleSystem>();
            var fMain = flashPs.main;
            fMain.loop = false;
            fMain.duration = 0.2f;
            fMain.startLifetime = 0.12f;
            fMain.startSpeed = 0f;
            fMain.startSize = 0.95f;
            fMain.startColor = Color.white;
            var fEmit = flashPs.emission;
            fEmit.rateOverTime = 0f;
            fEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var fSize = flashPs.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1.15f), new Keyframe(1f, 0f)));
            flashGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;

            GameObject sparksGo = new GameObject("Muzzle_ReleaseSparks");
            sparksGo.transform.SetParent(root.transform, false);
            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            var sMain = sparksPs.main;
            sMain.loop = false;
            sMain.duration = 0.35f;
            sMain.startDelay = 0.04f;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.30f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.2f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sEmit = sparksPs.emission;
            sEmit.rateOverTime = 0f;
            sEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var sShape = sparksPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 32f;
            sShape.radius = 0.08f;
            var sRend = sparksGo.GetComponent<ParticleSystemRenderer>();
            sRend.renderMode = ParticleSystemRenderMode.Stretch;
            sRend.lengthScale = 2.6f;
            sRend.sharedMaterial = matArc;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildLightningMainProjectilePrefab(
            string path, Mesh spearMesh, Material matCore, Material matInnerTrail, Material matOuterTrail,
            GameObject muzzlePrefab, GameObject impactPrefab)
        {
            GameObject root = new GameObject("PF_VFX_LightningProjectile_Main");

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.32f;

            MagicProjectile proj = root.AddComponent<MagicProjectile>();
            LightningProjectileVisuals visuals = root.AddComponent<LightningProjectileVisuals>();

            GameObject coreRoot = new GameObject("Layer1_PlasmaCoreHead");
            coreRoot.transform.SetParent(root.transform, false);

            GameObject coreMeshObj;
            if (spearMesh != null)
            {
                coreMeshObj = new GameObject("CoreSpearMesh");
                coreMeshObj.transform.SetParent(coreRoot.transform, false);
                coreMeshObj.transform.localScale = new Vector3(0.45f, 0.45f, 0.65f);
                coreMeshObj.AddComponent<MeshFilter>().sharedMesh = spearMesh;
                var mr = coreMeshObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = matCore;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            else
            {
                coreMeshObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                coreMeshObj.name = "CorePlasmaCapsule";
                coreMeshObj.transform.SetParent(coreRoot.transform, false);
                coreMeshObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                coreMeshObj.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
                if (coreMeshObj.TryGetComponent<Collider>(out var c)) UnityEngine.Object.DestroyImmediate(c);
                var mr = coreMeshObj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = matCore;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            TrailRenderer innerTr = CreateTrailChild(root.transform, "Layer2A_InnerBrightTrail", matInnerTrail, 0.25f, 0.32f, 1);
            TrailRenderer outerTr = CreateTrailChild(root.transform, "Layer2B_OuterBlueTrail", matOuterTrail, 0.40f, 0.44f, 0);
            LineRenderer arcA = CreateArcChild(root.transform, "Layer2C_CoiledArc_A", matInnerTrail, 0.08f, 0.015f, Color.white, new Color(0.25f, 0.85f, 1f, 1f));
            LineRenderer arcB = CreateArcChild(root.transform, "Layer2D_CoiledArc_B", matOuterTrail, 0.06f, 0.012f, new Color(0.35f, 0.85f, 1f, 1f), new Color(0.08f, 0.35f, 1f, 0.85f));

            GameObject sparksGo = new GameObject("Layer3_WorldTurbulenceSparks");
            sparksGo.transform.SetParent(root.transform, false);
            ParticleSystem sparkPs = sparksGo.AddComponent<ParticleSystem>();
            var spMain = sparkPs.main;
            spMain.loop = true;
            spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.40f);
            spMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            spMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            spMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.22f, 0.82f, 1f, 1f));
            spMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var spEmit = sparkPs.emission;
            spEmit.rateOverTime = 45f;
            var spShape = sparkPs.shape;
            spShape.shapeType = ParticleSystemShapeType.Sphere;
            spShape.radius = 0.14f;
            var spNoise = sparkPs.noise;
            spNoise.enabled = true;
            spNoise.strength = 2.5f;
            spNoise.frequency = 2.0f;
            spNoise.scrollSpeed = 1.6f;
            var spRend = sparksGo.GetComponent<ParticleSystemRenderer>();
            spRend.renderMode = ParticleSystemRenderMode.Stretch;
            spRend.lengthScale = 2.4f;
            spRend.velocityScale = 0.07f;
            spRend.sharedMaterial = matCore;

            GameObject lightGo = new GameObject("Layer4_ElectricPointLight");
            lightGo.transform.SetParent(root.transform, false);
            Light ptLight = lightGo.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = new Color(0.35f, 0.85f, 1f, 1f);
            ptLight.intensity = 2.6f;
            ptLight.range = 4.8f;
            ptLight.shadows = LightShadows.None;

            visuals.BindReferences(coreRoot, innerTr, outerTr, arcA, arcB, sparkPs, ptLight);

            SerializedObject soProj = new SerializedObject(proj);
            soProj.FindProperty("muzzleCastVFXPrefab").objectReferenceValue = muzzlePrefab;
            soProj.FindProperty("impactVFXPrefab").objectReferenceValue = impactPrefab;
            soProj.FindProperty("useWhiteBlueLightningVFX").boolValue = true;
            soProj.FindProperty("trailDetachDelay").floatValue = 0.45f;
            soProj.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildLightningImpactPrefab(string path, Material matGlow, Material matArc, Material matOuter)
        {
            GameObject root = new GameObject("PF_VFX_LightningProjectile_Impact");
            root.AddComponent<PooledVFXInstance>();

            GameObject flashGo = new GameObject("Impact_CoreFlash");
            flashGo.transform.SetParent(root.transform, false);
            ParticleSystem flashPs = flashGo.AddComponent<ParticleSystem>();
            var fMain = flashPs.main;
            fMain.loop = false;
            fMain.duration = 0.25f;
            fMain.startLifetime = 0.15f;
            fMain.startSpeed = 0f;
            fMain.startSize = 2.2f;
            fMain.startColor = Color.white;
            var fEmit = flashPs.emission;
            fEmit.rateOverTime = 0f;
            fEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var fSize = flashPs.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1.2f), new Keyframe(1f, 0f)));
            flashGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;

            GameObject ringGo = new GameObject("Impact_ShockwaveRing");
            ringGo.transform.SetParent(root.transform, false);
            ParticleSystem ringPs = ringGo.AddComponent<ParticleSystem>();
            var rMain = ringPs.main;
            rMain.loop = false;
            rMain.duration = 0.4f;
            rMain.startDelay = 0.02f;
            rMain.startLifetime = 0.28f;
            rMain.startSpeed = 0f;
            rMain.startSize = 3.2f;
            rMain.startColor = new Color(0.25f, 0.85f, 1f, 0.95f);
            var rEmit = ringPs.emission;
            rEmit.rateOverTime = 0f;
            rEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var rSize = ringPs.sizeOverLifetime;
            rSize.enabled = true;
            rSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(0.55f, 0.95f), new Keyframe(1f, 1.25f)));
            var rCol = ringPs.colorOverLifetime;
            rCol.enabled = true;
            Gradient ringGrad = new Gradient();
            ringGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.12f, 0.55f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            rCol.color = ringGrad;
            ringGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = matOuter;

            GameObject sparksGo = new GameObject("Impact_ElectricSparks");
            sparksGo.transform.SetParent(root.transform, false);
            ParticleSystem sparkPs = sparksGo.AddComponent<ParticleSystem>();
            var sMain = sparkPs.main;
            sMain.loop = false;
            sMain.duration = 0.6f;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            sMain.gravityModifier = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            sMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.22f, 0.80f, 1f, 1f));
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sEmit = sparkPs.emission;
            sEmit.rateOverTime = 0f;
            sEmit.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var sShape = sparkPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.22f;
            var sNoise = sparkPs.noise;
            sNoise.enabled = true;
            sNoise.strength = 2.0f;
            sNoise.frequency = 2.5f;
            var sColl = sparkPs.collision;
            sColl.enabled = true;
            sColl.type = ParticleSystemCollisionType.World;
            sColl.bounce = 0.55f;
            sColl.dampen = 0.25f;
            var sRend = sparksGo.GetComponent<ParticleSystemRenderer>();
            sRend.renderMode = ParticleSystemRenderMode.Stretch;
            sRend.lengthScale = 2.8f;
            sRend.velocityScale = 0.08f;
            sRend.sharedMaterial = matArc;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static TrailRenderer CreateTrailChild(Transform parent, string name, Material mat, float time, float startWidth, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.minVertexDistance = 0.07f;
            tr.startWidth = startWidth;
            tr.endWidth = 0f;
            tr.sortingOrder = sortingOrder;
            tr.sharedMaterial = mat;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            return tr;
        }

        private static LineRenderer CreateArcChild(Transform parent, string name, Material mat, float startW, float endW, Color startCol, Color endCol)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.startColor = startCol;
            lr.endColor = endCol;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static Material CreateOrUpdateLightningMat(
            string path, Texture2D mainTex, Texture2D noiseTex, Color coreHdr, Color edgeHdr,
            Vector4 scrollSpeed, float distortStrength, float dissolveScale, float tailFadePow)
        {
            Shader sh = Shader.Find("MahouShoujo/VFX/LightningTrailURP")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = sh;
            mat.SetTexture("_MainTex", mainTex);
            if (mat.HasProperty("_NoiseTex")) mat.SetTexture("_NoiseTex", noiseTex);
            if (mat.HasProperty("_ColorCore")) mat.SetColor("_ColorCore", coreHdr);
            if (mat.HasProperty("_ColorEdge")) mat.SetColor("_ColorEdge", edgeHdr);
            if (mat.HasProperty("_MainTexSpeed")) mat.SetVector("_MainTexSpeed", scrollSpeed);
            if (mat.HasProperty("_DistortStrength")) mat.SetFloat("_DistortStrength", distortStrength);
            if (mat.HasProperty("_DissolveScale")) mat.SetFloat("_DissolveScale", dissolveScale);
            if (mat.HasProperty("_TailFadePower")) mat.SetFloat("_TailFadePower", tailFadePow);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", coreHdr);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureCategorizedFolders()
        {
            string[] folders =
            {
                "Assets/_Project",
                "Assets/_Project/Art",
                VfxRootFolder,
                PrefabsFolder,
                MaterialsFolder,
                MeshesFolder,
                TexturesFolder,
                ShadersFolder,
                RecipesFolder,
                "Assets/_Project/Data",
                PatternsFolder
            };
            for (int i = 0; i < folders.Length; i++)
                EnsureFolderPath(folders[i]);
        }

        private static void EnsureFolderPath(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Texture2D FindOrCreateTexture(string texName, Func<Texture2D> generator)
        {
            Texture2D existing = ResolveTexture(texName, null);
            if (existing != null) return existing;

            Texture2D tex = generator();
            string outPath = TexturesFolder + "/" + texName + ".png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath);
            if (AssetImporter.GetAtPath(outPath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Texture2D CreateSeamlessLightningStreakTexture()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float wave1 = 0.5f + 0.14f * Mathf.Sin(nx * Mathf.PI * 4f) + 0.06f * Mathf.Sin(nx * Mathf.PI * 10f);
                    float wave2 = 0.5f - 0.18f * Mathf.Sin(nx * Mathf.PI * 6f + 1.2f);
                    float v = Mathf.Clamp01(Mathf.Exp(-Mathf.Abs(ny - wave1) * 28f) + Mathf.Exp(-Mathf.Abs(ny - wave2) * 22f) * 0.65f + Mathf.Exp(-Mathf.Abs(ny - 0.5f) * 6.5f) * 0.45f);
                    tex.SetPixel(x, y, new Color(v, v, v, v));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateRadialGlowTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateTileableNoiseTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float n1 = Mathf.PerlinNoise(nx * 6f, ny * 6f);
                    float n2 = Mathf.PerlinNoise(nx * 14f + 3.1f, ny * 14f + 1.7f) * 0.5f;
                    float v = Mathf.Clamp01((n1 + n2) / 1.5f);
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
#endif
