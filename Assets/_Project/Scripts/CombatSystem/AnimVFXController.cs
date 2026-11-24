using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CombatSystem: Shared Animation-Event VFX Controller for both Player_Root and Enemy Roots.
/// - Supports Key-based lookup (AnimVFXLibrary_SO + local Inspector overrides), Direct Prefab, and full AnimationEvent params.
/// - Resolves exact position, rotation, offsets, follow mode, and ground snapping via cached VFXAnchorRig (zero GetComponent in hot paths).
/// - Spawns via UniversalVFXManager ObjectPool (zero GC spikes) and tracks active handles for instant dodge/hit/stun/death cancellation.
/// </summary>
[DisallowMultipleComponent]
public class AnimVFXController : MonoBehaviour
{
    [Header("1. VFX Data Library & Local Overrides")]
    [Tooltip("Reusable ScriptableObject mapping Animation Event keys (e.g. 'Slash_1') to pooled VFX entries. Auto-injected for Players from CharacterData_SO.")]
    [SerializeField] private AnimVFXLibrary_SO vfxLibrary;

    [Tooltip("Optional actor-specific VFX entries that override matching keys in vfxLibrary.")]
    [SerializeField] private AnimVFXEntry[] localOverrides;

    [Header("2. Defaults for Direct Prefab Events (PlayVFXPrefab)")]
    [Tooltip("Default anchor used when PlayVFXPrefab(prefab) is called without a library entry.")]
    [SerializeField] private VFXSpawnAnchor defaultDirectPrefabAnchor = VFXSpawnAnchor.MeleeSwing;

    [Tooltip("Default rotation mode for direct prefab events.")]
    [SerializeField] private VFXRotationMode defaultDirectPrefabRotationMode = VFXRotationMode.ActorFlatForward;

    [Tooltip("Default follow mode for direct prefab events.")]
    [SerializeField] private VFXFollowMode defaultDirectPrefabFollowMode = VFXFollowMode.WorldSpace;

    [Tooltip("If true, direct prefab VFX are automatically canceled on Dodge / Hurt / Stun / Death.")]
    [SerializeField] private bool defaultDirectPrefabCancelOnInterrupt = true;

    [Header("3. Debug")]
    [Tooltip("Logs warnings in Console if an Animation Event key is not found in localOverrides or vfxLibrary.")]
    [SerializeField] private bool logMissingKeys = true;

    // Cached references (zero GetComponent during gameplay)
    private VFXAnchorRig anchorRig;
    private PlayerHealth playerHealth;
    private readonly Dictionary<string, AnimVFXEntry> localLookup = new Dictionary<string, AnimVFXEntry>(8, StringComparer.OrdinalIgnoreCase);
    private readonly List<PooledVFXInstance> activeInstances = new List<PooledVFXInstance>(12);
    private Action<PooledVFXInstance> cachedReturnHandler;

    // Same-frame blend duplicate guard
    private string lastTriggeredKey;
    private int lastTriggeredFrame = -1;

    public AnimVFXLibrary_SO VfxLibrary => vfxLibrary;

    private void Awake()
    {
        cachedReturnHandler = HandleInstanceReturnedToPool;
        anchorRig = GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>();
        playerHealth = GetComponent<PlayerHealth>();
        RebuildLocalLookup();
    }

    private void OnValidate()
    {
        RebuildLocalLookup();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnPlayerHurt += HandlePlayerInterrupted;
            playerHealth.OnDeath += HandleActorDied;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnPlayerHurt -= HandlePlayerInterrupted;
            playerHealth.OnDeath -= HandleActorDied;
        }

        // Safely detach & return all pooled VFX so disabling/destroying an actor never leaks or destroys pooled objects
        StopAllActiveVFX();
    }

    /// <summary>
    /// Injects or swaps the character's VFX Library at runtime (called by PlayerCharacterLoader).
    /// </summary>
    public void SetLibrary(AnimVFXLibrary_SO library)
    {
        vfxLibrary = library;
        if (vfxLibrary != null)
            vfxLibrary.RebuildLookup();
    }

    /// <summary>
    /// Refreshes cached VFXAnchorRig reference after a dynamic model swap.
    /// </summary>
    public void RefreshAnchorRig(VFXAnchorRig rig = null)
    {
        anchorRig = rig != null ? rig : (GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>());
    }

    private void RebuildLocalLookup()
    {
        localLookup.Clear();
        if (localOverrides == null) return;

        for (int i = 0; i < localOverrides.Length; i++)
        {
            AnimVFXEntry entry = localOverrides[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;

            string trimmed = entry.key.Trim();
            if (!localLookup.ContainsKey(trimmed))
                localLookup.Add(trimmed, entry);
        }
    }

    // =========================================================================
    // PUBLIC ANIMATION EVENT API (3A Key, 3B Direct Prefab, 3C Full Event)
    // =========================================================================

    /// <summary>
    /// Spawns a pooled VFX mapped to 'key' in localOverrides or vfxLibrary.
    /// Gracefully falls back to VFXEventType enum names (e.g. "GroundShockwave") if no entry matches.
    /// </summary>
    public PooledVFXInstance PlayByKey(string key, float scaleOverride = -1f, int anchorEnumOverride = -1)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        string trimmedKey = key.Trim();
        if (IsDuplicateFrameTrigger(trimmedKey)) return null;

        if (TryResolveEntry(trimmedKey, out AnimVFXEntry entry))
        {
            return SpawnEntry(entry, null, scaleOverride, anchorEnumOverride, null);
        }

        // Graceful fallback: allow triggering built-in VFXEventType by name (e.g. "MeleeHit", "GroundShockwave")
        if (Enum.TryParse(trimmedKey, true, out VFXEventType fallbackType))
        {
            VFXSpawnAnchor anchor = (anchorEnumOverride >= 0 && Enum.IsDefined(typeof(VFXSpawnAnchor), anchorEnumOverride))
                ? (VFXSpawnAnchor)anchorEnumOverride
                : (fallbackType == VFXEventType.GroundShockwave ? VFXSpawnAnchor.GroundFeet : defaultDirectPrefabAnchor);

            float scale = scaleOverride > 0.01f ? scaleOverride : 1.0f;
            UniversalVFXManager.SpawnOnOwner(fallbackType, transform, anchor, null, null, scale, false, false);
            return null;
        }

        if (logMissingKeys)
        {
            Debug.LogWarning($"[AnimVFXController] VFX key '{trimmedKey}' not found on '{gameObject.name}'. Assign an AnimVFXLibrary_SO or add it to Local Overrides.", this);
        }
        return null;
    }

    /// <summary>
    /// Spawns a direct VFX Prefab dragged into an Animation Event's Object field.
    /// </summary>
    public PooledVFXInstance PlayDirectPrefab(
        GameObject prefab,
        VFXSpawnAnchor anchor,
        VFXRotationMode rotationMode,
        VFXFollowMode followMode,
        float scale = 1.0f,
        bool cancelOnInterrupt = true,
        string customAnchorName = null,
        string keyTag = null)
    {
        if (prefab == null) return null;
        string guardKey = !string.IsNullOrWhiteSpace(keyTag) ? keyTag : prefab.name;
        if (IsDuplicateFrameTrigger(guardKey)) return null;
        if (anchorRig == null) anchorRig = GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>();

        anchorRig.ResolveWorldPose(
            anchor,
            customAnchorName,
            rotationMode,
            Vector3.zero,
            Vector3.zero,
            anchor == VFXSpawnAnchor.GroundFeet,
            out Vector3 worldPos,
            out Quaternion worldRot,
            out Transform anchorTransform);

        Transform followTarget = ResolveFollowTarget(followMode, anchorTransform);

        PooledVFXInstance inst = UniversalVFXManager.SpawnPooledVFX(
            prefab,
            worldPos,
            worldRot,
            scale > 0.01f ? scale : 1.0f,
            followMode,
            followTarget,
            transform,
            Vector3.zero,
            Quaternion.identity,
            anchor == VFXSpawnAnchor.GroundFeet,
            0f,
            keyTag,
            cancelOnInterrupt);

        RegisterActiveInstance(inst);
        return inst;
    }

    /// <summary>
    /// Convenience overload using Inspector default anchor/rotation/follow settings.
    /// </summary>
    public PooledVFXInstance PlayDirectPrefab(GameObject prefab, float scale = 1.0f)
    {
        return PlayDirectPrefab(
            prefab,
            defaultDirectPrefabAnchor,
            defaultDirectPrefabRotationMode,
            defaultDirectPrefabFollowMode,
            scale,
            defaultDirectPrefabCancelOnInterrupt,
            null,
            prefab != null ? prefab.name : null);
    }

    /// <summary>
    /// Power-user single Animation Event handler supporting ALL 4 Unity AnimationEvent parameters:
    /// - stringParameter: Library Key (or Custom Anchor Name when paired with direct Prefab)
    /// - objectReferenceParameter: Optional Direct VFX Prefab override
    /// - intParameter: Optional VFXSpawnAnchor enum index (0=Muzzle, 1=MeleeSwing, 2=CenterBody, 3=GroundFeet, 5=RightHand, 8=WeaponTip...)
    /// - floatParameter: Optional scale multiplier (>0 overrides scale)
    /// </summary>
    public PooledVFXInstance PlayFromAnimationEvent(AnimationEvent evt)
    {
        if (evt == null) return null;

        string strParam = string.IsNullOrWhiteSpace(evt.stringParameter) ? null : evt.stringParameter.Trim();
        GameObject directPrefab = evt.objectReferenceParameter as GameObject;
        float scaleOverride = evt.floatParameter > 0.01f ? evt.floatParameter : -1f;
        int anchorOverride = (evt.intParameter > 0 && Enum.IsDefined(typeof(VFXSpawnAnchor), evt.intParameter))
            ? evt.intParameter
            : -1;

        // Case 1: Both Key and Direct Prefab provided -> use Key's alignment/follow config with Direct Prefab override
        if (directPrefab != null && strParam != null && TryResolveEntry(strParam, out AnimVFXEntry matchedEntry))
        {
            return SpawnEntry(matchedEntry, directPrefab, scaleOverride, anchorOverride, null);
        }

        // Case 2: Direct Prefab provided without matching key -> treat stringParameter as customAnchorName (e.g. "WeaponTip")
        if (directPrefab != null)
        {
            VFXSpawnAnchor anchor = (evt.intParameter >= 0 && Enum.IsDefined(typeof(VFXSpawnAnchor), evt.intParameter))
                ? (VFXSpawnAnchor)evt.intParameter
                : defaultDirectPrefabAnchor;

            return PlayDirectPrefab(
                directPrefab,
                anchor,
                defaultDirectPrefabRotationMode,
                defaultDirectPrefabFollowMode,
                scaleOverride > 0.01f ? scaleOverride : 1.0f,
                defaultDirectPrefabCancelOnInterrupt,
                strParam,
                directPrefab.name);
        }

        // Case 3: Key-only Animation Event
        if (strParam != null)
        {
            return PlayByKey(strParam, scaleOverride, anchorOverride);
        }

        return null;
    }

    /// <summary>
    /// Stops and returns all active pooled VFX matching the given key (e.g., end of charge/loop).
    /// </summary>
    public void StopByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || activeInstances.Count == 0) return;
        string trimmed = key.Trim();

        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            PooledVFXInstance inst = activeInstances[i];
            if (inst == null || !inst.IsInUse)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            if (string.Equals(inst.ActiveKey, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                inst.ForceStopAndReturn();
            }
        }
    }

    /// <summary>
    /// Stops and returns all active pooled VFX marked cancelOnInterrupt == true.
    /// Called automatically on Player Dodge / Hurt / CancelCombat and Enemy Stun.
    /// </summary>
    public void CancelInterruptibleVFX()
    {
        if (activeInstances.Count == 0) return;

        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            PooledVFXInstance inst = activeInstances[i];
            if (inst == null || !inst.IsInUse)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            if (inst.CancelOnInterrupt)
            {
                inst.ForceStopAndReturn();
            }
        }
    }

    /// <summary>
    /// Immediately stops, detaches, and returns ALL tracked VFX spawned by this actor.
    /// Called on Death / OnDisable so pooled VFX are never orphaned or destroyed with the actor.
    /// </summary>
    public void StopAllActiveVFX()
    {
        if (activeInstances.Count == 0) return;

        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            PooledVFXInstance inst = activeInstances[i];
            if (inst != null && inst.IsInUse)
            {
                inst.ForceStopAndReturn();
            }
        }
        activeInstances.Clear();
    }

    // =========================================================================
    // INTERNAL SPAWN & LIFECYCLE HELPERS
    // =========================================================================

    private bool TryResolveEntry(string key, out AnimVFXEntry entry)
    {
        if (localLookup.TryGetValue(key, out entry) && entry != null)
            return true;

        if (vfxLibrary != null && vfxLibrary.TryGetEntry(key, out entry) && entry != null)
            return true;

        entry = null;
        return false;
    }

    private PooledVFXInstance SpawnEntry(
        AnimVFXEntry entry,
        GameObject prefabOverride,
        float scaleOverride,
        int anchorEnumOverride,
        string customAnchorOverride)
    {
        if (entry == null) return null;
        if (anchorRig == null) anchorRig = GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>();

        if (entry.replaceExistingWithSameKey && !string.IsNullOrWhiteSpace(entry.key))
        {
            StopByKey(entry.key);
        }

        VFXSpawnAnchor resolvedAnchor = (anchorEnumOverride >= 0 && Enum.IsDefined(typeof(VFXSpawnAnchor), anchorEnumOverride))
            ? (VFXSpawnAnchor)anchorEnumOverride
            : entry.anchor;

        string resolvedCustomAnchor = !string.IsNullOrWhiteSpace(customAnchorOverride)
            ? customAnchorOverride
            : entry.customAnchorName;

        bool snapGround = entry.snapToGround || resolvedAnchor == VFXSpawnAnchor.GroundFeet;

        anchorRig.ResolveWorldPose(
            resolvedAnchor,
            resolvedCustomAnchor,
            entry.rotationMode,
            entry.localPositionOffset,
            entry.localEulerOffset,
            snapGround,
            out Vector3 worldPos,
            out Quaternion worldRot,
            out Transform anchorTransform);

        float finalScale = scaleOverride > 0.01f ? scaleOverride : entry.scale;
        GameObject prefabToUse = prefabOverride != null ? prefabOverride : entry.vfxPrefab;

        if (prefabToUse != null)
        {
            Transform followTarget = ResolveFollowTarget(entry.followMode, anchorTransform);
            PooledVFXInstance inst = UniversalVFXManager.SpawnPooledVFX(
                prefabToUse,
                worldPos,
                worldRot,
                finalScale,
                entry.followMode,
                followTarget,
                transform,
                entry.localPositionOffset,
                Quaternion.Euler(entry.localEulerOffset),
                snapGround,
                entry.overrideDuration,
                entry.key,
                entry.cancelOnInterrupt);

            RegisterActiveInstance(inst);
            return inst;
        }

        // Fallback if entry has no prefab assigned -> spawn procedural/global UniversalVFXManager burst
        UniversalVFXManager.SpawnAtPoint(
            entry.fallbackEventType,
            worldPos,
            worldRot,
            null,
            entry.fallbackColor,
            finalScale,
            false,
            snapGround);

        return null;
    }

    private Transform ResolveFollowTarget(VFXFollowMode mode, Transform anchorTransform)
    {
        switch (mode)
        {
            case VFXFollowMode.FollowActorRoot:
                return transform;
            case VFXFollowMode.FollowAnchorTransform:
            case VFXFollowMode.FollowPositionOnly:
                return anchorTransform != null ? anchorTransform : transform;
            default:
                return null;
        }
    }

    private void RegisterActiveInstance(PooledVFXInstance inst)
    {
        if (inst == null) return;
        inst.OnReturnedToPool += cachedReturnHandler;
        activeInstances.Add(inst);
    }

    private void HandleInstanceReturnedToPool(PooledVFXInstance inst)
    {
        activeInstances.Remove(inst);
    }

    private void HandlePlayerInterrupted() => CancelInterruptibleVFX();
    private void HandleActorDied() => StopAllActiveVFX();

    private bool IsDuplicateFrameTrigger(string key)
    {
        int currentFrame = Time.frameCount;
        if (currentFrame == lastTriggeredFrame && string.Equals(lastTriggeredKey, key, StringComparison.OrdinalIgnoreCase))
            return true;

        lastTriggeredFrame = currentFrame;
        lastTriggeredKey = key;
        return false;
    }
}
