#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MahouShoujo.VFX
{
    // 1-Click Unity 6 URP Builder for Unified UniversalVFX + 3-Stage Lightning Prefabs + Sample CombatSkillPattern_SO Assets
    public static class LightningProjectilePrefabBuilder
    {
        private const string BaseFolder = "Assets/_Project/VFX_Lightning_WhiteBlue";
        private const string MaterialsFolder = BaseFolder + "/Materials";
        private const string PrefabsFolder = BaseFolder + "/Prefabs";
        private const string TexturesFolder = BaseFolder + "/Textures";
        private const string PatternsFolder = BaseFolder + "/Patterns";

        [MenuItem("Tools/MahouShoujo VFX/Build & Auto-Wire Unified Lightning + Patterns (Unity 6 URP)", false, 1)]
        [MenuItem("GameObject/VFX/Build & Auto-Wire Unified Lightning + Patterns (Unity 6 URP)", false, 11)]
        public static void BuildAndWireUnifiedPack()
        {
            EnsureFolders();

            Texture2D streakTex = FindOrCreateTexture("T_ProjectileStreak_WhiteBlue", CreateSeamlessLightningStreakTexture);
            Texture2D glowTex = FindOrCreateTexture("T_ElectricGlow_WhiteBlue", CreateRadialGlowTexture);
            Texture2D noiseTex = FindOrCreateTexture("T_LightningDissolveNoise", CreateTileableNoiseTexture);

            Material matInnerTrail = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_InnerTrail_HDR_URP.mat",
                streakTex,
                noiseTex,
                new Color(3.4f, 4.0f, 4.8f, 1.0f),
                new Color(0.22f, 0.85f, 3.4f, 1.0f),
                new Vector4(-2.8f, 0f, 0f, 0f),
                0.08f,
                36f,
                1.3f);

            Material matOuterTrail = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_OuterTrail_Blue_URP.mat",
                streakTex,
                noiseTex,
                new Color(0.20f, 0.72f, 2.8f, 0.92f),
                new Color(0.04f, 0.22f, 1.4f, 0.55f),
                new Vector4(-1.8f, 0.1f, 0f, 0f),
                0.12f,
                26f,
                1.6f);

            Material matCoreParticle = CreateOrUpdateLightningMat(
                MaterialsFolder + "/M_LP_CoreSpark_HDR_URP.mat",
                glowTex,
                noiseTex,
                new Color(3.2f, 3.8f, 4.5f, 1.0f),
                new Color(0.18f, 0.75f, 3.2f, 1.0f),
                Vector4.zero,
                0.02f,
                20f,
                0.4f);

            Mesh spearMesh = FindSubMeshInObj("VFX_Projectile_TaperedSpear");

            GameObject muzzlePrefab = BuildMuzzlePrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Muzzle.prefab",
                matCoreParticle,
                matInnerTrail);

            GameObject impactPrefab = BuildImpactPrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Impact.prefab",
                matCoreParticle,
                matInnerTrail,
                matOuterTrail);

            GameObject mainProjectileGo = BuildMainProjectilePrefab(
                PrefabsFolder + "/PF_VFX_LightningProjectile_Main.prefab",
                spearMesh,
                matCoreParticle,
                matInnerTrail,
                matOuterTrail,
                muzzlePrefab,
                impactPrefab);

            MagicProjectile mainProjectileComp = mainProjectileGo != null
                ? mainProjectileGo.GetComponent<MagicProjectile>()
                : null;

            // Create 4 Ready-to-Test CombatSkillPattern_SO Presets (Projectile / Homing / Beam / Melee SlashWave)
            CreateSamplePatterns(mainProjectileComp, muzzlePrefab, impactPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int wiredCount = AutoWireSceneComponents(muzzlePrefab, mainProjectileComp, impactPrefab);

            Selection.activeObject = mainProjectileGo;
            EditorGUIUtility.PingObject(mainProjectileGo);
            Debug.Log($"[LightningProjectilePrefabBuilder] สร้าง Prefab 3 ระยะ + 4 Sample Patterns เรียบร้อย และเชื่อมเข้า Scene Components {wiredCount} จุด!");
        }

        private static void CreateSamplePatterns(MagicProjectile mainProj, GameObject muzzle, GameObject impact)
        {
            CreateOrUpdatePattern(
                PatternsFolder + "/PAT_Ranged_LightningChainSnipe.asset",
                p =>
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

            CreateOrUpdatePattern(
                PatternsFolder + "/PAT_Ranged_ItanoHoming5.asset",
                p =>
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

            CreateOrUpdatePattern(
                PatternsFolder + "/PAT_Beam_TelegraphedLaser.asset",
                p =>
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

            CreateOrUpdatePattern(
                PatternsFolder + "/PAT_Melee_SlashWaveCrescent.asset",
                p =>
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

        private static void CreateOrUpdatePattern(string path, System.Action<CombatSkillPattern_SO> configure)
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

            UniversalVFXManager vfxMgr = Object.FindFirstObjectByType<UniversalVFXManager>(FindObjectsInactive.Include);
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

            PlayerCombat combat = Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
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

        private static GameObject BuildMuzzlePrefab(string path, Material matGlow, Material matArc)
        {
            GameObject root = new GameObject("PF_VFX_LightningProjectile_Muzzle");

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
            cEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });
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
            fEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });
            var fSize = flashPs.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.25f, 1.15f),
                new Keyframe(1f, 0.0f)));
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
            sEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
            var sShape = sparksPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 32f;
            sShape.radius = 0.08f;
            var sRend = sparksGo.GetComponent<ParticleSystemRenderer>();
            sRend.renderMode = ParticleSystemRenderMode.Stretch;
            sRend.lengthScale = 2.6f;
            sRend.sharedMaterial = matArc;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildMainProjectilePrefab(
            string path,
            Mesh spearMesh,
            Material matCore,
            Material matInnerTrail,
            Material matOuterTrail,
            GameObject muzzlePrefab,
            GameObject impactPrefab)
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
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            else
            {
                coreMeshObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                coreMeshObj.name = "CorePlasmaCapsule";
                coreMeshObj.transform.SetParent(coreRoot.transform, false);
                coreMeshObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                coreMeshObj.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
                if (coreMeshObj.TryGetComponent<Collider>(out var c)) Object.DestroyImmediate(c);
                var mr = coreMeshObj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = matCore;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            GameObject innerTrailGo = new GameObject("Layer2A_InnerBrightTrail");
            innerTrailGo.transform.SetParent(root.transform, false);
            TrailRenderer innerTr = innerTrailGo.AddComponent<TrailRenderer>();
            innerTr.time = 0.25f;
            innerTr.minVertexDistance = 0.07f;
            innerTr.startWidth = 0.32f;
            innerTr.endWidth = 0.0f;
            innerTr.sortingOrder = 1;
            innerTr.sharedMaterial = matInnerTrail;
            innerTr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            innerTr.receiveShadows = false;

            GameObject outerTrailGo = new GameObject("Layer2B_OuterBlueTrail");
            outerTrailGo.transform.SetParent(root.transform, false);
            TrailRenderer outerTr = outerTrailGo.AddComponent<TrailRenderer>();
            outerTr.time = 0.40f;
            outerTr.minVertexDistance = 0.07f;
            outerTr.startWidth = 0.44f;
            outerTr.endWidth = 0.0f;
            outerTr.sortingOrder = 0;
            outerTr.sharedMaterial = matOuterTrail;
            outerTr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outerTr.receiveShadows = false;

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
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildImpactPrefab(string path, Material matGlow, Material matArc, Material matOuter)
        {
            GameObject root = new GameObject("PF_VFX_LightningProjectile_Impact");

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
            fEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });
            var fSize = flashPs.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.2f, 1.2f),
                new Keyframe(1f, 0f)));
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
            rEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 2) });
            var rSize = ringPs.sizeOverLifetime;
            rSize.enabled = true;
            rSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f),
                new Keyframe(0.55f, 0.95f),
                new Keyframe(1f, 1.25f)));
            var rCol = ringPs.colorOverLifetime;
            rCol.enabled = true;
            Gradient ringGrad = new Gradient();
            ringGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.12f, 0.55f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
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
            sEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 36) });
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
            Object.DestroyImmediate(root);
            return saved;
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
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static Material CreateOrUpdateLightningMat(
            string path,
            Texture2D mainTex,
            Texture2D noiseTex,
            Color coreHdr,
            Color edgeHdr,
            Vector4 scrollSpeed,
            float distortStrength,
            float dissolveScale,
            float tailFadePow)
        {
            Shader sh = Shader.Find("MahouShoujo/VFX/LightningTrailURP");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");

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
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", coreHdr);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolders()
        {
            string[] folders = { "Assets/_Project", BaseFolder, MaterialsFolder, PrefabsFolder, TexturesFolder, PatternsFolder };
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
                    if (obj is Mesh m && m.name.Contains(meshName)) return m;
                }
            }
            return null;
        }

        private static Texture2D FindOrCreateTexture(string texName, System.Func<Texture2D> generator)
        {
            string[] guids = AssetDatabase.FindAssets(texName + " t:Texture2D");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));

            Texture2D tex = generator();
            string outPath = TexturesFolder + "/" + texName + ".png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath);
            TextureImporter importer = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (importer != null)
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
                    float d1 = Mathf.Abs(ny - wave1);
                    float d2 = Mathf.Abs(ny - wave2);
                    float v = Mathf.Clamp01(Mathf.Exp(-d1 * 28f) + Mathf.Exp(-d2 * 22f) * 0.65f + Mathf.Exp(-Mathf.Abs(ny - 0.5f) * 6.5f) * 0.45f);
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

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดในไฟล์ LightningProjectilePrefabBuilder.cs (Unified + Patterns)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. เมนู 1-Click ใน Unity 6 Editor:
 *    - Tools -> MahouShoujo VFX -> Build & Auto-Wire Unified Lightning + Patterns (Unity 6 URP)
 *    - สร้าง Prefab สายฟ้า 3 ระยะ (Muzzle / Main / Impact) พร้อมผูกเข้า UniversalVFXManager อัตโนมัติ
 *    - สร้างไฟล์ตัวอย่าง CombatSkillPattern_SO พร้อมทดสอบ 4 ใบในโฟลเดอร์ Patterns/:
 *      (1) PAT_Ranged_LightningChainSnipe (สายฟ้าชิ่ง 3 ตัว)
 *      (2) PAT_Ranged_ItanoHoming5 (มิสไซล์สายฟ้าลอยค้าง 5 นัด)
 *      (3) PAT_Beam_TelegraphedLaser (ลำแสงแจ้งเตือนก่อนยิง)
 *      (4) PAT_Melee_SlashWaveCrescent (คลื่นดาบสายฟ้าทะลวงสำหรับสายใกล้)
 * ============================================================================ */
