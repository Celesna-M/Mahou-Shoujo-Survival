// Drop the extracted folder into Assets/ (keep this file in Editor/)
// Run: Tools > Stylized VFX Studio > Build Prefabs With Baked Curves
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedVFXStudio.Editor
{
    [Serializable]
    public struct HDRColorData
    {
        public float r; public float g; public float b; public float a;
        public Color ToColor() => new Color(r, g, b, a);
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
        public string archetype;
        public string variant;
        public string element;
        public string blendMode;
        public float totalDuration;
        public bool loop;
        public HDRColorData coreColorHDR;
        public HDRColorData edgeColorHDR;
        public float distortionStrength;
        public float dissolveThreshold;
        public float sharpness;
        public float fresnelPower;
        public float fresnelIntensity;
        public float uvScrollX;
        public float uvScrollY;
        public float scale;
        public int particleCount;
        public string shaderName;
        public TrackTimingData[] tracks;
        public BakedCurvesData curves;
    }

    public static class UnityVFXPrefabsBuilder
    {
        [MenuItem("Tools/Stylized VFX Studio/Build Prefabs With Baked Curves")]
        public static void BuildPrefabs()
        {
            string jsonPath = EditorUtility.OpenFilePanel("Select VFX_Recipe.json", Application.dataPath, "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            if (!jsonPath.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Move Into Assets/",
                    "Extract the exported ZIP inside your Unity project's Assets/ directory first.", "OK");
                return;
            }

            string rootFolder = "Assets" + Path.GetDirectoryName(jsonPath).Substring(Application.dataPath.Length).Replace("\\", "/");
            VFXStudioRecipe recipe = JsonUtility.FromJson<VFXStudioRecipe>(File.ReadAllText(jsonPath));
            AssetDatabase.Refresh();

            Shader shader = Shader.Find(recipe.shaderName);
            if (shader == null) shader = AssetDatabase.LoadAssetAtPath<Shader>(rootFolder + "/Shaders/URP_StylizedVFX.shader");
            if (shader == null) { Debug.LogError("[StylizedVFXStudio] Shader not found."); return; }

            Texture2D noiseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(rootFolder + "/Textures/VFX_Noise.png");
            Texture2D runeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(rootFolder + "/Textures/VFX_Runes.png");

            Material mainMat = CreateMat(shader, noiseTex, recipe);
            AssetDatabase.CreateAsset(mainMat, rootFolder + "/" + recipe.vfxName + "_Main.mat");

            Material runeMat = CreateMat(shader, runeTex, recipe);
            runeMat.SetFloat("_DistortionStrength", 0.02f);
            runeMat.SetVector("_ScrollSpeed", Vector4.zero);
            AssetDatabase.CreateAsset(runeMat, rootFolder + "/" + recipe.vfxName + "_Runes.mat");

            string prefabDir = rootFolder + "/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabDir)) AssetDatabase.CreateFolder(rootFolder, "Prefabs");

            // Convert baked JSON keyframe arrays into native Unity AnimationCurves
            AnimationCurve scaleAnim = BuildAnimCurve(recipe.curves.scaleCurve, 1f);
            AnimationCurve velAnim = BuildAnimCurve(recipe.curves.velocityCurve, 1f);
            AnimationCurve alphaAnim = BuildAnimCurve(recipe.curves.alphaDissolveCurve, 1f);
            AnimationCurve hdrAnim = BuildAnimCurve(recipe.curves.hdrGlowCurve, 1f);

            TrackTimingData trCharge = FindTrack(recipe.tracks, "charge", 0f, 0.5f);
            TrackTimingData trCore   = FindTrack(recipe.tracks, "core",   0.2f, 1.2f);
            TrackTimingData trTrail  = FindTrack(recipe.tracks, "trail",  0.2f, 1.3f);
            TrackTimingData trImpact = FindTrack(recipe.tracks, "impact", 1.2f, 0.9f);

            Mesh slashMesh  = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_Slash.obj");
            Mesh headMesh   = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_ProjectileHead.obj");
            Mesh trailMesh  = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_TrailRibbon.obj");
            Mesh beamMesh   = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_BeamCylinder.obj");
            Mesh helixMesh  = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_BeamHelix.obj");
            Mesh ringMesh   = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_Ring.obj");
            Mesh domeMesh   = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_Dome.obj");
            Mesh pillarMesh = AssetDatabase.LoadAssetAtPath<Mesh>(rootFolder + "/Meshes/VFX_Pillar.obj");

            if (recipe.archetype == "Projectile" || recipe.archetype == "Beam")
            {
                // Sub-Prefab 1: Muzzle / Charge
                SaveSinglePrefab(prefabDir + "/" + recipe.vfxName + "_Muzzle.prefab", recipe.vfxName + "_Muzzle",
                    ringMesh, mainMat, recipe, trCharge, scaleAnim, velAnim, alphaAnim, hdrAnim, 14);

                // Sub-Prefab 2: Main Projectile / Beam
                GameObject mainRoot = new GameObject(recipe.vfxName + "_Main");
                mainRoot.transform.localScale = Vector3.one * Mathf.Max(0.2f, recipe.scale);
                AddMeshLayer(mainRoot, "Core_Shape", recipe.archetype == "Beam" ? beamMesh : headMesh, mainMat, recipe, trCore, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(mainRoot, "Secondary_TrailOrHelix", recipe.archetype == "Beam" ? helixMesh : trailMesh, mainMat, recipe, trTrail, scaleAnim, alphaAnim, hdrAnim);
                GameObject savedMain = PrefabUtility.SaveAsPrefabAsset(mainRoot, prefabDir + "/" + recipe.vfxName + "_Main.prefab");
                UnityEngine.Object.DestroyImmediate(mainRoot);

                // Sub-Prefab 3: Impact Explosion
                SaveSinglePrefab(prefabDir + "/" + recipe.vfxName + "_Impact.prefab", recipe.vfxName + "_Impact",
                    domeMesh, mainMat, recipe, trImpact, scaleAnim, velAnim, alphaAnim, hdrAnim, recipe.particleCount);

                Selection.activeObject = savedMain;
                EditorGUIUtility.PingObject(savedMain);
            }
            else
            {
                Mesh primary = recipe.archetype == "Slash" ? slashMesh : (recipe.archetype == "AoE" ? pillarMesh : domeMesh);
                GameObject root = new GameObject(recipe.vfxName);
                root.transform.localScale = Vector3.one * Mathf.Max(0.2f, recipe.scale);
                AddMeshLayer(root, "Track1_Charge", ringMesh, runeMat, recipe, trCharge, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(root, "Track2_CoreShape", primary, mainMat, recipe, trCore, scaleAnim, alphaAnim, hdrAnim);
                AddMeshLayer(root, "Track3_SecondaryRing", ringMesh, mainMat, recipe, trTrail, scaleAnim, alphaAnim, hdrAnim);
                if (recipe.particleCount > 0) AddSparkLayer(root, "Track4_ImpactSparks", mainMat, recipe, trImpact, velAnim, alphaAnim, recipe.particleCount);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabDir + "/" + recipe.vfxName + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StylizedVFXStudio] Successfully built URP Prefabs with baked AnimationCurves in " + prefabDir);
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
                foreach (var t in list) if (t.id == id) return t;
            }
            return new TrackTimingData { id = id, startDelay = defDelay, duration = defDur };
        }

        private static Material CreateMat(Shader shader, Texture2D tex, VFXStudioRecipe r)
        {
            Material m = new Material(shader);
            if (tex != null) m.SetTexture("_NoiseTex", tex);
            m.SetColor("_CoreColor", r.coreColorHDR.ToColor());
            m.SetColor("_EdgeColor", r.edgeColorHDR.ToColor());
            m.SetVector("_ScrollSpeed", new Vector4(r.uvScrollX, r.uvScrollY, 0f, 0f));
            m.SetFloat("_DistortionStrength", r.distortionStrength);
            m.SetFloat("_DissolveThreshold", r.dissolveThreshold);
            m.SetFloat("_Sharpness", r.sharpness);
            m.SetFloat("_FresnelPower", r.fresnelPower);
            m.SetFloat("_FresnelIntensity", r.fresnelIntensity);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(r.blendMode == "AlphaBlend" ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            return m;
        }

        private static void SaveSinglePrefab(string path, string name, Mesh mesh, Material mat, VFXStudioRecipe r,
            TrackTimingData tr, AnimationCurve scaleAnim, AnimationCurve velAnim, AnimationCurve alphaAnim, AnimationCurve hdrAnim, int sparks)
        {
            GameObject root = new GameObject(name);
            root.transform.localScale = Vector3.one * Mathf.Max(0.2f, r.scale);
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

            // Apply Baked Scale Curve to Size Over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, scaleAnim);

            // Apply Baked Alpha/Dissolve Curve to Color Over Lifetime
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

            // Apply Baked Dissolve & HDR Glow Curves to Custom Data Stream (UV.zw in URP_StylizedVFX.shader)
            var cd = ps.customData;
            cd.enabled = true;
            cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, alphaAnim));
            cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f, hdrAnim));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            if (mesh != null) rend.mesh = mesh;
            rend.material = mat;
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

            // Apply Baked Velocity Curve to Velocity Over Lifetime
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, velAnim);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.4f;
            rend.material = mat;
        }
    }
}
#endif
