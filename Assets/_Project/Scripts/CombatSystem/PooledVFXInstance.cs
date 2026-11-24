using System;
using UnityEngine;

/// <summary>
/// CombatSystem: Lightweight zero-GC runtime wrapper attached automatically to pooled VFX instances.
/// - Caches ParticleSystems, TrailRenderers, and original prefab scale once in Awake().
/// - Prevents stretched TrailRenderers by clearing AFTER setting world position.
/// - Supports all 4 VFXFollowMode behaviors including FollowPositionOnly (follows bone position without bone twist).
/// - Supports instant cancellation & clean return to UniversalVFXManager's ObjectPool.
/// </summary>
public class PooledVFXInstance : MonoBehaviour
{
    private ParticleSystem[] cachedParticles;
    private TrailRenderer[] cachedTrails;
    private Vector3 baseLocalScale = Vector3.one;
    private bool isInitialized;

    private Action<PooledVFXInstance> returnToPoolCallback;
    private Transform poolContainer;

    // Active runtime state
    private bool isInUse;
    private float remainingLifetime;
    private VFXFollowMode activeFollowMode;
    private Transform positionFollowTarget;
    private Transform rotationReferenceActor;
    private Vector3 positionFollowLocalOffset;
    private Quaternion baseRotationOffset;
    private bool snapFollowToGround;

    public bool IsInUse => isInUse;
    public string ActiveKey { get; private set; }
    public bool CancelOnInterrupt { get; private set; }
    public Vector3 BaseLocalScale => baseLocalScale;

    /// <summary>
    /// Invoked right before this instance returns to the pool so AnimVFXController can drop its reference cleanly.
    /// </summary>
    public event Action<PooledVFXInstance> OnReturnedToPool;

    private void Awake()
    {
        EnsureCachedComponents();
    }

    public void EnsureCachedComponents()
    {
        if (isInitialized) return;
        isInitialized = true;
        baseLocalScale = transform.localScale;
        cachedParticles = GetComponentsInChildren<ParticleSystem>(true);
        cachedTrails = GetComponentsInChildren<TrailRenderer>(true);
    }

    /// <summary>
    /// Binds this instance to its owning UniversalVFXManager pool.
    /// </summary>
    public void BindPool(Transform container, Action<PooledVFXInstance> releaseCallback)
    {
        EnsureCachedComponents();
        poolContainer = container;
        returnToPoolCallback = releaseCallback;
    }

    /// <summary>
    /// Activates the pooled VFX at an exact world pose, applies follow mode, clears trails, and restarts all particles.
    /// </summary>
    public void Activate(
        Vector3 worldPos,
        Quaternion worldRot,
        float scaleMultiplier,
        VFXFollowMode followMode,
        Transform followTarget,
        Transform actorRoot,
        Vector3 localPosOffset,
        Quaternion eulerOffset,
        bool snapToGround,
        float overrideDuration,
        float fallbackAutoDuration,
        string key,
        bool cancelOnInterrupt)
    {
        EnsureCachedComponents();

        isInUse = true;
        ActiveKey = key;
        CancelOnInterrupt = cancelOnInterrupt;
        activeFollowMode = followMode;
        positionFollowTarget = null;
        rotationReferenceActor = actorRoot;
        positionFollowLocalOffset = localPosOffset;
        baseRotationOffset = eulerOffset;
        snapFollowToGround = snapToGround;

        // 1. Always detach first, set exact world pose & clean scale BEFORE playing/clearing trails
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(worldPos, worldRot);
        float safeScale = Mathf.Max(0.05f, scaleMultiplier);
        transform.localScale = baseLocalScale * safeScale;

        // 2. Apply Follow Mode
        if (followMode == VFXFollowMode.FollowActorRoot && actorRoot != null)
        {
            transform.SetParent(actorRoot, true);
        }
        else if (followMode == VFXFollowMode.FollowAnchorTransform && followTarget != null)
        {
            transform.SetParent(followTarget, true);
        }
        else if (followMode == VFXFollowMode.FollowPositionOnly && followTarget != null)
        {
            positionFollowTarget = followTarget;
        }

        // 3. Activate GameObject & cleanly reset all Trails and ParticleSystems
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (cachedTrails != null)
        {
            for (int i = 0; i < cachedTrails.Length; i++)
            {
                if (cachedTrails[i] != null)
                {
                    cachedTrails[i].Clear();
                    cachedTrails[i].emitting = true;
                }
            }
        }

        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                ParticleSystem ps = cachedParticles[i];
                if (ps == null) continue;
                if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        // 4. Compute lifetime (0 = auto-detect from ParticleSystems / Trails)
        remainingLifetime = overrideDuration > 0.01f
            ? overrideDuration
            : ComputeAutoLifetime(fallbackAutoDuration);
    }

    /// <summary>
    /// Advances lifetime timer from UniversalVFXManager's centralized Update loop (Zero Coroutine GC).
    /// Returns true if the VFX has finished and was returned to pool.
    /// </summary>
    public bool TickLifetime(float deltaTime)
    {
        if (!isInUse) return true;

        remainingLifetime -= deltaTime;
        if (remainingLifetime <= 0f)
        {
            ForceStopAndReturn();
            return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (!isInUse || activeFollowMode != VFXFollowMode.FollowPositionOnly) return;

        if (positionFollowTarget == null || !positionFollowTarget.gameObject.activeInHierarchy)
        {
            positionFollowTarget = null;
            return;
        }

        Quaternion refRot = rotationReferenceActor != null
            ? GetFlatRotation(rotationReferenceActor)
            : Quaternion.identity;

        Vector3 targetPos = positionFollowTarget.position + (refRot * positionFollowLocalOffset);
        if (snapFollowToGround)
        {
            targetPos = VFXAnchorRig.SnapToGroundSurface(targetPos, 0.05f, default, rotationReferenceActor != null ? rotationReferenceActor.root : null);
        }

        transform.position = targetPos;
        transform.rotation = refRot * baseRotationOffset;
    }

    /// <summary>
    /// Immediately halts all particles/trails, detaches from any actor/bone, and returns to the ObjectPool.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public void ForceStopAndReturn()
    {
        if (!isInUse) return;
        isInUse = false;

        positionFollowTarget = null;
        rotationReferenceActor = null;
        activeFollowMode = VFXFollowMode.WorldSpace;

        if (cachedTrails != null)
        {
            for (int i = 0; i < cachedTrails.Length; i++)
            {
                if (cachedTrails[i] != null)
                {
                    cachedTrails[i].emitting = false;
                    cachedTrails[i].Clear();
                }
            }
        }

        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                if (cachedParticles[i] != null)
                    cachedParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // Detach BEFORE returning to pool so destroying the actor never destroys pooled VFX!
        if (poolContainer != null)
        {
            transform.SetParent(poolContainer, false);
        }
        else
        {
            transform.SetParent(null, false);
        }

        transform.localScale = baseLocalScale;

        Action<PooledVFXInstance> notify = OnReturnedToPool;
        OnReturnedToPool = null;
        ActiveKey = null;
        CancelOnInterrupt = false;
        notify?.Invoke(this);

        if (returnToPoolCallback != null)
        {
            returnToPoolCallback.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float ComputeAutoLifetime(float fallbackDuration)
    {
        float maxDuration = 0f;
        bool hasLooping = false;

        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                ParticleSystem ps = cachedParticles[i];
                if (ps == null) continue;

                ParticleSystem.MainModule main = ps.main;
                if (main.loop)
                {
                    hasLooping = true;
                    continue;
                }

                float startDelay = main.startDelay.constantMax;
                float startLife = main.startLifetime.constantMax;
                float total = main.duration + startDelay + startLife;
                if (total > maxDuration) maxDuration = total;
            }
        }

        if (cachedTrails != null)
        {
            for (int i = 0; i < cachedTrails.Length; i++)
            {
                if (cachedTrails[i] != null && cachedTrails[i].time > maxDuration)
                    maxDuration = cachedTrails[i].time + 0.15f;
            }
        }

        if (maxDuration > 0.05f)
            return Mathf.Clamp(maxDuration + 0.1f, 0.35f, 12.0f);

        return hasLooping ? Mathf.Max(1.0f, fallbackDuration) : Mathf.Max(0.5f, fallbackDuration);
    }

    private static Quaternion GetFlatRotation(Transform tf)
    {
        Vector3 flatFwd = tf.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        return Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
    }

    private void OnDestroy()
    {
        isInUse = false;
        OnReturnedToPool = null;
        returnToPoolCallback = null;
    }
}
