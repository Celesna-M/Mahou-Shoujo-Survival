using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MahouShoujo.VFX
{
    // Debugged & Zero-GC White-Blue 3D Lightning Fallback Controller (Unified with UniversalVFXManager)
    // - Fixes GC Allocation bug: removed `new WaitForSeconds(arcRefreshRate)` inside while loops
    // - Fixes Duration bug: accumulates real elapsed Time.deltaTime with yield return null instead of mixing WaitForSeconds + deltaTime
    // - Fixes URP Black-Box bug: uses UniversalVFXManager Shared URP Transparent/Additive Materials
    // - Uses pre-allocated Vector3[] buffer for LineRenderer.SetPositions (Zero-GC per refresh)
    [ExecuteAlways]
    public class LightningBoltVFXController : MonoBehaviour
    {
        [Header("1. White & Blue Lightning Color Palette (HDR Emission)")]
        [ColorUsage(true, true)]
        public Color coreWhiteColor = new Color(3.2f, 3.6f, 4.0f, 1.0f);
        [ColorUsage(true, true)]
        public Color electricCyanBlueColor = new Color(0.35f, 1.85f, 3.8f, 1.0f);
        [ColorUsage(true, true)]
        public Color deepRoyalBlueColor = new Color(0.05f, 0.48f, 2.4f, 0.85f);

        [Header("2. Single Bolt Sequence Settings (Cast -> Line Bolt -> Impact Sphere)")]
        public float projectileSpeed = 18.0f;
        public float travelDistance = 12.0f;
        public float repeatInterval = 1.60f;
        public bool autoLoopInPlayMode = true;

        [Header("3. Lightning Arcs & Impact Sphere Parameters")]
        [Range(6, 32)]
        public int arcSegments = 16;
        public float arcJitterAmount = 0.24f;
        public float arcRefreshRate = 0.035f;
        public float impactSphereRadius = 1.40f;
        public float impactDuration = 0.65f;

        private Vector3[] arcPointBuffer;
        private float nextFireTimer;

        private void EnsureArcBuffer(int segments)
        {
            int needed = Mathf.Max(6, segments) + 1;
            if (arcPointBuffer == null || arcPointBuffer.Length != needed)
                arcPointBuffer = new Vector3[needed];
        }

        private void OnEnable()
        {
            EnsureArcBuffer(arcSegments);
            if (Application.isPlaying && autoLoopInPlayMode)
            {
                nextFireTimer = 0.1f;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (autoLoopInPlayMode)
            {
                nextFireTimer -= Time.deltaTime;
                if (nextFireTimer <= 0f)
                {
                    nextFireTimer = repeatInterval;
                    FireSingleWhiteBlueLightningBolt();
                }
            }
        }

        [ContextMenu("Fire Single White-Blue Lightning Bolt (Cast -> Bolt -> Impact)")]
        public void FireSingleWhiteBlueLightningBolt()
        {
            EnsureArcBuffer(arcSegments);
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;

            SpawnMuzzleChargeBurst(origin, forward);
            StartCoroutine(SimulateSingleLightningProjectileRoutine(origin, forward));
        }

        public static GameObject CreateMuzzleVFXInstance(Vector3 position, Quaternion rotation)
        {
            GameObject root = new GameObject("VFX_Muzzle_Lightning_WhiteBlue");
            root.transform.SetPositionAndRotation(position, rotation);
            var runner = root.AddComponent<LightningBoltVFXController>();
            runner.autoLoopInPlayMode = false;
            runner.SpawnMuzzleChargeBurst(position, rotation * Vector3.forward);
            Destroy(root, 0.9f);
            return root;
        }

        public static GameObject CreateImpactVFXInstance(Vector3 position, Vector3 normal, float radius = 1.40f)
        {
            GameObject root = new GameObject("VFX_Impact_LightningSphere_WhiteBlue");
            root.transform.position = position;
            if (normal.sqrMagnitude > 0.001f)
                root.transform.rotation = Quaternion.LookRotation(normal);

            var runner = root.AddComponent<LightningBoltVFXController>();
            runner.autoLoopInPlayMode = false;
            runner.impactSphereRadius = radius;
            runner.SpawnImpactLightningSphere(position, normal);
            Destroy(root, runner.impactDuration + 0.35f);
            return root;
        }

        public void SpawnMuzzleChargeBurst(Vector3 origin, Vector3 forward)
        {
            EnsureArcBuffer(10);
            GameObject muzzleObj = new GameObject("Stage1_ReleaseElectricBurst_WhiteBlue");
            muzzleObj.transform.SetParent(transform, true);
            muzzleObj.transform.position = origin;
            muzzleObj.transform.rotation = Quaternion.LookRotation(forward);

            // Reduced from 9 to 6 arcs for better performance while preserving silhouette
            for (int a = 0; a < 6; a++)
            {
                GameObject arcGo = new GameObject($"ReleaseArc_{a}");
                arcGo.transform.SetParent(muzzleObj.transform, false);
                LineRenderer lr = SetupElectricLineRenderer(arcGo, 0.085f, 0.015f, coreWhiteColor, electricCyanBlueColor);
                BuildJaggedArcOnSphere(lr, 0.78f, 10, 0.16f);
            }

            CreateSparkParticleBurst(muzzleObj.transform, 22, 4.8f, 0.38f);
            StartCoroutine(AnimateMuzzleBurstRoutine(muzzleObj, 0.32f));
        }

        private IEnumerator SimulateSingleLightningProjectileRoutine(Vector3 startPos, Vector3 dir)
        {
            GameObject projRoot = new GameObject("Stage2_SingleLightningBolt_WhiteBlue");
            projRoot.transform.position = startPos;
            projRoot.transform.rotation = Quaternion.LookRotation(dir);

            Material innerMat = UniversalVFXManager.GetSharedInnerTrailMaterial();
            Material outerMat = UniversalVFXManager.GetSharedOuterTrailMaterial();

            GameObject innerCore = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            innerCore.name = "InnerWhiteCoreSpear";
            innerCore.transform.SetParent(projRoot.transform, false);
            innerCore.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            innerCore.transform.localScale = new Vector3(0.20f, 1.25f, 0.20f);
            if (innerCore.TryGetComponent<Collider>(out var c1)) Destroy(c1);
            innerCore.GetComponent<Renderer>().sharedMaterial = innerMat;

            TrailRenderer trail = projRoot.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.minVertexDistance = 0.08f;
            trail.startWidth = 0.38f;
            trail.endWidth = 0.0f;
            trail.sharedMaterial = innerMat;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            LineRenderer coilArcA = SetupElectricLineRenderer(new GameObject("CoiledArc_A"), 0.08f, 0.02f, coreWhiteColor, electricCyanBlueColor);
            coilArcA.transform.SetParent(projRoot.transform, false);
            LineRenderer coilArcB = SetupElectricLineRenderer(new GameObject("CoiledArc_B"), 0.06f, 0.015f, electricCyanBlueColor, deepRoyalBlueColor);
            coilArcB.transform.SetParent(projRoot.transform, false);
            coilArcB.sharedMaterial = outerMat;

            CreateSparkParticleBurst(projRoot.transform, 28, 2.2f, 0.45f, true);

            float traveled = 0f;
            float jitterTimer = 0f;
            Vector3 currentPos = startPos;

            while (traveled < travelDistance && projRoot != null)
            {
                float step = projectileSpeed * Time.deltaTime;
                if (Physics.Raycast(currentPos, dir, out RaycastHit hit, step + 0.35f))
                {
                    currentPos = hit.point;
                    projRoot.transform.position = currentPos;
                    SpawnImpactLightningSphere(hit.point, hit.normal);
                    trail.emitting = false;
                    innerCore.SetActive(false);
                    coilArcA.enabled = false;
                    coilArcB.enabled = false;
                    Destroy(projRoot, trail.time + 0.05f);
                    yield break;
                }

                currentPos += dir * step;
                traveled += step;
                projRoot.transform.position = currentPos;

                jitterTimer -= Time.deltaTime;
                if (jitterTimer <= 0f)
                {
                    jitterTimer = Mathf.Max(0.016f, arcRefreshRate);
                    UpdateCoiledProjectileArc(coilArcA, 2.4f, 0.25f, 14, Time.time * 18f);
                    UpdateCoiledProjectileArc(coilArcB, 2.2f, 0.32f, 14, -Time.time * 15f + 2.1f);
                }

                yield return null;
            }

            if (projRoot != null)
            {
                SpawnImpactLightningSphere(currentPos, -dir);
                trail.emitting = false;
                innerCore.SetActive(false);
                coilArcA.enabled = false;
                coilArcB.enabled = false;
                Destroy(projRoot, trail.time + 0.05f);
            }
        }

        public void SpawnImpactLightningSphere(Vector3 hitPos, Vector3 hitNormal)
        {
            EnsureArcBuffer(arcSegments);
            GameObject impactRoot = new GameObject("Stage3_ImpactLightningSphere_WhiteBlue");
            impactRoot.transform.position = hitPos;

            GameObject domeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            domeSphere.name = "TranslucentBlueEnergySphere";
            domeSphere.transform.SetParent(impactRoot.transform, false);
            domeSphere.transform.localScale = Vector3.one * (impactSphereRadius * 2.0f);
            if (domeSphere.TryGetComponent<Collider>(out var col)) Destroy(col);
            var domeRend = domeSphere.GetComponent<Renderer>();
            domeRend.sharedMaterial = UniversalVFXManager.GetSharedTranslucentDomeMaterial();
            domeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRend.receiveShadows = false;

            // Reduced from 12 to 8 surface arcs for better frame time while preserving look
            List<LineRenderer> surfaceArcs = new List<LineRenderer>(8);
            for (int i = 0; i < 8; i++)
            {
                GameObject arcObj = new GameObject($"SphereLightningArc_{i}");
                arcObj.transform.SetParent(impactRoot.transform, false);
                bool isPrimaryWhite = (i % 2 == 0);
                LineRenderer lr = SetupElectricLineRenderer(
                    arcObj,
                    isPrimaryWhite ? 0.10f : 0.07f,
                    0.015f,
                    isPrimaryWhite ? coreWhiteColor : electricCyanBlueColor,
                    isPrimaryWhite ? electricCyanBlueColor : deepRoyalBlueColor
                );
                BuildJaggedArcOnSphere(lr, impactSphereRadius * 1.03f, arcSegments, arcJitterAmount);
                surfaceArcs.Add(lr);
            }

            CreateSparkParticleBurst(impactRoot.transform, 32, 5.5f, 0.50f);
            StartCoroutine(AnimateImpactSphereRoutine(impactRoot, domeSphere.transform, surfaceArcs, impactDuration));
        }

        // FIXED: Zero-GC coroutine (no `new WaitForSeconds`) and accurate wall-clock elapsed timer
        private IEnumerator AnimateMuzzleBurstRoutine(GameObject muzzleObj, float duration)
        {
            float elapsed = 0f;
            float refreshAccum = 0f;
            float stepInterval = Mathf.Max(0.016f, arcRefreshRate);
            LineRenderer[] arcs = muzzleObj.GetComponentsInChildren<LineRenderer>();

            while (elapsed < duration && muzzleObj != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                refreshAccum += dt;

                if (refreshAccum >= stepInterval)
                {
                    refreshAccum -= stepInterval;
                    float radius = Mathf.Lerp(0.38f, 0.98f, Mathf.Clamp01(elapsed / duration));
                    for (int i = 0; i < arcs.Length; i++)
                    {
                        if (arcs[i] != null)
                            BuildJaggedArcOnSphere(arcs[i], radius, 10, 0.16f);
                    }
                }
                yield return null;
            }
            if (muzzleObj != null) Destroy(muzzleObj);
        }

        // FIXED: Zero-GC coroutine + smooth per-frame dome scaling + accurate duration
        private IEnumerator AnimateImpactSphereRoutine(GameObject impactRoot, Transform domeTransform, List<LineRenderer> arcs, float duration)
        {
            float elapsed = 0f;
            float refreshAccum = 0f;
            float stepInterval = Mathf.Max(0.016f, arcRefreshRate);
            float targetDiameter = impactSphereRadius * 2.0f;

            while (elapsed < duration && impactRoot != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                refreshAccum += dt;

                float t = Mathf.Clamp01(elapsed / duration);
                float scaleFactor = t < 0.18f
                    ? Mathf.Lerp(0.25f, 1.05f, t / 0.18f)
                    : Mathf.Lerp(1.05f, 0.85f, (t - 0.18f) / 0.82f);

                if (domeTransform != null)
                {
                    domeTransform.localScale = Vector3.one * (targetDiameter * scaleFactor);
                }

                if (refreshAccum >= stepInterval)
                {
                    refreshAccum -= stepInterval;
                    for (int i = 0; i < arcs.Count; i++)
                    {
                        if (arcs[i] != null)
                        {
                            BuildJaggedArcOnSphere(arcs[i], impactSphereRadius * scaleFactor * 1.03f, arcSegments, arcJitterAmount);
                        }
                    }
                }

                yield return null;
            }

            if (impactRoot != null) Destroy(impactRoot);
        }

        private static LineRenderer SetupElectricLineRenderer(GameObject target, float startW, float endW, Color startCol, Color endCol)
        {
            LineRenderer lr = target.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = UniversalVFXManager.GetSharedInnerTrailMaterial();
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

        private void UpdateCoiledProjectileArc(LineRenderer lr, float length, float radius, int segments, float phase)
        {
            EnsureArcBuffer(segments);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float z = Mathf.Lerp(-length * 0.78f, length * 0.22f, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float angle = t * Mathf.PI * 3.5f + phase;
                arcPointBuffer[i] = new Vector3(
                    Mathf.Cos(angle) * radius * envelope + Random.Range(-0.07f, 0.07f),
                    Mathf.Sin(angle) * radius * envelope + Random.Range(-0.07f, 0.07f),
                    z
                );
            }
            lr.positionCount = segments + 1;
            lr.SetPositions(arcPointBuffer);
        }

        private void BuildJaggedArcOnSphere(LineRenderer lr, float radius, int segments, float jitter)
        {
            EnsureArcBuffer(segments);
            Vector3 startDir = Random.onUnitSphere;
            Vector3 endDir = Vector3.Slerp(startDir, -startDir + Random.insideUnitSphere * 0.6f, 0.75f).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 dir = Vector3.Slerp(startDir, endDir, t).normalized;
                float midWeight = Mathf.Sin(t * Mathf.PI);
                Vector3 noise = Random.insideUnitSphere * (jitter * midWeight);
                Vector3 pt = (dir * radius) + noise;
                if (pt.magnitude < radius * 0.92f)
                    pt = pt.normalized * (radius * 1.02f);
                arcPointBuffer[i] = pt;
            }
            lr.positionCount = segments + 1;
            lr.SetPositions(arcPointBuffer);
        }

        private static void CreateSparkParticleBurst(Transform parent, int count, float speed, float lifetime, bool continuous = false)
        {
            GameObject psObj = new GameObject("WhiteBlue_ElectricSparks");
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

            var psRend = psObj.GetComponent<ParticleSystemRenderer>();
            psRend.renderMode = ParticleSystemRenderMode.Stretch;
            psRend.lengthScale = 2.4f;
            psRend.velocityScale = 0.08f;
            psRend.sharedMaterial = UniversalVFXManager.GetSharedParticleMaterial();
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/VFX/Create Single White-Blue 3D Lightning Sequence", false, 10)]
        public static void CreateCompleteDemoInScene()
        {
            GameObject root = new GameObject("VFX_WhiteBlue_SingleLightning_Sequence");
            var ctrl = root.AddComponent<LightningBoltVFXController>();
            Selection.activeGameObject = root;
            ctrl.FireSingleWhiteBlueLightningBolt();
        }
#endif
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันในไฟล์ LightningBoltVFXController.cs (Zero-GC Unified)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. บั๊กที่แก้ไขจากเวอร์ชันเดิม (#debug Fixes):
 *    - ตัด `new WaitForSeconds(arcRefreshRate)` ใน Coroutine ออกทั้งหมด เปลี่ยนเป็นตัวสะสมเวลา
 *      `refreshAccum += Time.deltaTime` คู่กับ `yield return null` ทำให้ Zero-GC และเวลาทำงานตรงตาม
 *      `duration` เป๊ะ (ไม่เพี้ยนยาวกว่าปกติ 2-3 เท่าเหมือนเดิม)
 *    - เปลี่ยนการตั้งค่าจุดสายฟ้าเป็น `arcPointBuffer` ที่จองไว้ครั้งเดียวแล้วส่งด้วย `SetPositions()`
 *    - ดึง Shared URP Transparent/Additive Material จาก `UniversalVFXManager` โดยตรง แก้บั๊กกล่องดำทึบใน Unity 6 URP
 *
 * 2. บทบาทในระบบรวม (Unified Architecture):
 *    - ทำหน้าที่เป็น Procedural Fallback อัตโนมัติให้ `UniversalVFXManager` เมื่อเปิดโหมดสายฟ้า
 *      แต่ยังไม่ได้ผูก URP Prefab ทั้ง 3 ระยะ
 * ============================================================================ */
