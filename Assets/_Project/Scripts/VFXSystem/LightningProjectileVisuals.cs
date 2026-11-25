using UnityEngine;

namespace MahouShoujo.VFX
{
    // 4-Layer Lightning Projectile Visual Controller (Zero-GC Runtime Animation + Graceful Trail Detach)
    // Layer 1: Plasma Core Head (Mesh + Glow)
    // Layer 2: Dual-Layer Stylized TrailRenderer (Inner HDR Order 1 + Outer Wide Royal Blue Order 0) + Coiled Electric Arcs
    // Layer 3: World-Space Turbulence Electric Sparks
    // Layer 4: Dynamic Point Light (Flash + Smooth Fade on Impact)
    public class LightningProjectileVisuals : MonoBehaviour
    {
        [Header("Layer 1: Plasma Core Head")]
        [SerializeField] private GameObject coreVisualRoot;
        [SerializeField] private float coreSpinSpeed = 720f;

        [Header("Layer 2: Dual-Layer Trail & Coiled Arcs")]
        [SerializeField] private TrailRenderer innerBrightTrail;
        [SerializeField] private TrailRenderer outerDarkTrail;
        [SerializeField] private LineRenderer coiledArcA;
        [SerializeField] private LineRenderer coiledArcB;
        [SerializeField, Range(6, 24)] private int arcSegments = 14;
        [SerializeField] private float arcLength = 2.4f;
        [SerializeField] private float arcRadiusA = 0.22f;
        [SerializeField] private float arcRadiusB = 0.30f;
        [SerializeField] private float arcJitter = 0.065f;
        [SerializeField] private float arcRefreshInterval = 0.033f;

        [Header("Layer 3: World-Space Sparks")]
        [SerializeField] private ParticleSystem worldSparksPS;

        [Header("Layer 4: Dynamic Point Light")]
        [SerializeField] private Light projectileLight;
        [SerializeField] private float defaultLightIntensity = 2.6f;

        // Pre-allocated buffers so LineRenderer updates allocate ZERO bytes on GC heap
        private Vector3[] bufferA;
        private Vector3[] bufferB;
        private float refreshTimer;
        private bool isDetached;
        private float detachFadeTimer;
        private float detachFadeDuration = 0.35f;

        private void Awake()
        {
            AllocateArcBuffers();
            EnforceTrailSortingOrder();
        }

        private void OnEnable()
        {
            AllocateArcBuffers();
            ResetForLaunch();
        }

        private void AllocateArcBuffers()
        {
            int count = Mathf.Max(6, arcSegments) + 1;
            if (bufferA == null || bufferA.Length != count) bufferA = new Vector3[count];
            if (bufferB == null || bufferB.Length != count) bufferB = new Vector3[count];
        }

        private void EnforceTrailSortingOrder()
        {
            // Prevent Z-fighting flicker between overlapping Dual Trails (Gabriel Aguiar rule)
            if (innerBrightTrail != null) innerBrightTrail.sortingOrder = 1;
            if (outerDarkTrail != null) outerDarkTrail.sortingOrder = 0;
        }

        public void BindReferences(
            GameObject coreRoot,
            TrailRenderer innerTrail,
            TrailRenderer outerTrail,
            LineRenderer arcA,
            LineRenderer arcB,
            ParticleSystem sparks,
            Light pointLight)
        {
            coreVisualRoot = coreRoot;
            innerBrightTrail = innerTrail;
            outerDarkTrail = outerTrail;
            coiledArcA = arcA;
            coiledArcB = arcB;
            worldSparksPS = sparks;
            projectileLight = pointLight;

            AllocateArcBuffers();
            EnforceTrailSortingOrder();
        }

        public void ResetForLaunch()
        {
            isDetached = false;
            refreshTimer = 0f;

            if (coreVisualRoot != null) coreVisualRoot.SetActive(true);
            if (coiledArcA != null) coiledArcA.enabled = true;
            if (coiledArcB != null) coiledArcB.enabled = true;

            if (innerBrightTrail != null)
            {
                innerBrightTrail.Clear();
                innerBrightTrail.emitting = true;
            }
            if (outerDarkTrail != null)
            {
                outerDarkTrail.Clear();
                outerDarkTrail.emitting = true;
            }
            if (worldSparksPS != null)
            {
                worldSparksPS.Clear();
                worldSparksPS.Play(true);
            }
            if (projectileLight != null)
            {
                projectileLight.enabled = true;
                projectileLight.intensity = defaultLightIntensity;
            }

            UpdateCoiledArcsImmediate();
        }

        // Called by MagicProjectile on impact so trails and world sparks fade out smoothly instead of snapping
        public float BeginGracefulDetach(float minDelay = 0.45f)
        {
            if (isDetached) return minDelay;
            isDetached = true;

            if (coreVisualRoot != null) coreVisualRoot.SetActive(false);
            if (coiledArcA != null) coiledArcA.enabled = false;
            if (coiledArcB != null) coiledArcB.enabled = false;

            float maxTrailTime = minDelay;
            if (innerBrightTrail != null)
            {
                innerBrightTrail.emitting = false;
                maxTrailTime = Mathf.Max(maxTrailTime, innerBrightTrail.time + 0.05f);
            }
            if (outerDarkTrail != null)
            {
                outerDarkTrail.emitting = false;
                maxTrailTime = Mathf.Max(maxTrailTime, outerDarkTrail.time + 0.05f);
            }
            if (worldSparksPS != null)
            {
                worldSparksPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            detachFadeDuration = Mathf.Max(0.15f, maxTrailTime * 0.65f);
            detachFadeTimer = detachFadeDuration;
            return maxTrailTime;
        }

        private void Update()
        {
            if (isDetached)
            {
                if (projectileLight != null && projectileLight.enabled)
                {
                    detachFadeTimer -= Time.deltaTime;
                    float ratio = Mathf.Clamp01(detachFadeTimer / detachFadeDuration);
                    projectileLight.intensity = defaultLightIntensity * ratio;
                    if (ratio <= 0.001f) projectileLight.enabled = false;
                }
                return;
            }

            if (coreVisualRoot != null && coreSpinSpeed > 0.01f)
            {
                coreVisualRoot.transform.Rotate(0f, 0f, coreSpinSpeed * Time.deltaTime, Space.Self);
            }

            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = Mathf.Max(0.016f, arcRefreshInterval);
                UpdateCoiledArcsImmediate();
            }
        }

        private void UpdateCoiledArcsImmediate()
        {
            if (coiledArcA != null && coiledArcA.enabled)
            {
                FillHelixBuffer(bufferA, arcLength, arcRadiusA, Time.time * 18f);
                coiledArcA.positionCount = bufferA.Length;
                coiledArcA.SetPositions(bufferA);
            }

            if (coiledArcB != null && coiledArcB.enabled)
            {
                FillHelixBuffer(bufferB, arcLength * 0.92f, arcRadiusB, -Time.time * 15f + 2.1f);
                coiledArcB.positionCount = bufferB.Length;
                coiledArcB.SetPositions(bufferB);
            }
        }

        private void FillHelixBuffer(Vector3[] buf, float length, float radius, float phase)
        {
            int segs = buf.Length - 1;
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float z = Mathf.Lerp(-length * 0.80f, length * 0.20f, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float angle = t * Mathf.PI * 3.5f + phase;

                float jx = Random.Range(-arcJitter, arcJitter) * envelope;
                float jy = Random.Range(-arcJitter, arcJitter) * envelope;

                buf[i] = new Vector3(
                    Mathf.Cos(angle) * radius * envelope + jx,
                    Mathf.Sin(angle) * radius * envelope + jy,
                    z
                );
            }
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันในไฟล์ LightningProjectileVisuals.cs
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. โครงสร้าง 4 เลเยอร์ของกระสุนสายฟ้า (4-Layer Architecture):
 *    - Layer 1 (coreVisualRoot): หัวพลาสมาสีขาว-ฟ้าที่หมุนรอบแกน Z ตลอดช่วงพุ่ง
 *    - Layer 2 (innerBrightTrail / outerDarkTrail / coiledArcA / coiledArcB):
 *      หางกระสุน 2 ชั้นที่บังคับ sortingOrder (ชั้นใน = 1, ชั้นนอก = 0) เพื่อกันแสงกะพริบ
 *      พร้อมเกลียวสายฟ้า 2 เส้นที่อัปเดตตำแหน่งด้วย Pre-allocated Vector3[] Buffer (Zero GC)
 *    - Layer 3 (worldSparksPS): อนุภาคประกายไฟฟ้าใน World Space ที่ทิ้งรอยตามเส้นทางบิน
 *    - Layer 4 (projectileLight): แสง Point Light ส่องพื้นและกำแพงรอบข้างระหว่างพุ่ง
 *
 * 2. ResetForLaunch():
 *    - รีเซ็ตสถานะทุกเลเยอร์และล้าง Trail เก่าทิ้ง รองรับทั้งการ Instantiate ปกติและการทำ Object Pooling
 *
 * 3. BeginGracefulDetach(float minDelay):
 *    - เรียกเมื่อกระสุนปะทะเป้าหมาย: ซ่อนหัวกระสุนและเกลียวสายฟ้า หยุด Emit ประกายไฟและหาง Trail
 *      แล้วค่อยๆ หรี่แสง Point Light ลงอย่างนุ่มนวล พร้อมคืนเวลาที่ต้องรอก่อนทำลาย GameObject
 *      ทำให้หางกระสุนไม่หายวับกลางอากาศ
 * ============================================================================ */
