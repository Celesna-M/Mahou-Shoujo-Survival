using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DataSystem: Single VFX Animation Event configuration row.
/// Shared via AnimVFXLibrary_SO so multiple characters or enemies sharing an Animation Clip
/// can play completely different themed VFX with exact anchor, rotation, offset, follow, and cancel rules.
/// </summary>
[Serializable]
public class AnimVFXEntry
{
    [Header("1. Event Key & Asset")]
    [Tooltip("Exact string key triggered from Animation Event: PlayVFX(\"Key\"). Case-insensitive.")]
    public string key = "Slash_1";

    [Tooltip("VFX Prefab to spawn from the Object Pool. Leave empty to use fallbackEventType.")]
    public GameObject vfxPrefab;

    [Tooltip("Fallback UniversalVFXManager event type if vfxPrefab is empty.")]
    public VFXEventType fallbackEventType = VFXEventType.MeleeHit;

    [Tooltip("Fallback tint color used only when vfxPrefab is empty.")]
    public Color fallbackColor = new Color(0.95f, 0.35f, 0.80f, 1f);

    [Header("2. Anchor & Alignment (Fixes 1C Position/Rotation)")]
    [Tooltip("Standardized spawn anchor resolved via VFXAnchorRig.")]
    public VFXSpawnAnchor anchor = VFXSpawnAnchor.MeleeSwing;

    [Tooltip("Optional custom socket name (e.g. 'ScytheTip' or child named 'VFX_ScytheTip'). Overrides anchor when found.")]
    public string customAnchorName = "";

    [Tooltip("How initial rotation is calculated (ActorFlatForward prevents floor-tilted slashes; AnchorTransformRotation follows weapon angle).")]
    public VFXRotationMode rotationMode = VFXRotationMode.ActorFlatForward;

    [Tooltip("How VFX moves after spawning (FollowPositionOnly tracks hand/weapon position WITHOUT bone twist).")]
    public VFXFollowMode followMode = VFXFollowMode.WorldSpace;

    [Tooltip("Fine position offset in anchor-rotation space (X=Right, Y=Up, Z=Forward).")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Tooltip("Fine Euler angle offset in degrees (e.g. Z=35 to tilt a horizontal slash arc diagonally).")]
    public Vector3 localEulerOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied on top of the prefab's original scale.")]
    [Min(0.05f)]
    public float scale = 1.0f;

    [Tooltip("Force snap final world position onto the real ground surface (ignores Player/Enemy colliders).")]
    public bool snapToGround = false;

    [Header("3. Pooling & Interrupt Lifecycle")]
    [Tooltip("0 = Auto-detect lifetime from ParticleSystems/TrailRenderers (minimum 0.5s). >0 = explicit lifetime in seconds.")]
    [Min(0f)]
    public float overrideDuration = 0f;

    [Tooltip("If true, automatically stops & returns this VFX to pool if the Player dodges/gets hit/dies or Enemy gets stunned/dies.")]
    public bool cancelOnInterrupt = true;

    [Tooltip("If true, triggering the same key while an older instance is still active stops the older one first (ideal for charge auras).")]
    public bool replaceExistingWithSameKey = false;
}

/// <summary>
/// DataSystem: Reusable ScriptableObject table mapping Animation Event keys to pooled VFX configurations.
/// Create via: Right-click Project > Create > StellaClash > Data > Anim VFX Library.
/// </summary>
[CreateAssetMenu(fileName = "NewAnimVFXLibrary", menuName = "StellaClash/Data/Anim VFX Library", order = 5)]
public class AnimVFXLibrary_SO : ScriptableObject
{
    [Tooltip("List of Animation Event VFX entries. Lookups are cached in an O(1) Dictionary at runtime.")]
    [SerializeField] private AnimVFXEntry[] entries;

    private Dictionary<string, AnimVFXEntry> lookupCache;

    public AnimVFXEntry[] Entries => entries;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    /// <summary>
    /// Builds O(1) case-insensitive key lookup table (zero GC allocation during gameplay).
    /// </summary>
    public void RebuildLookup()
    {
        if (lookupCache == null)
            lookupCache = new Dictionary<string, AnimVFXEntry>(16, StringComparer.OrdinalIgnoreCase);
        else
            lookupCache.Clear();

        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            AnimVFXEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;

            string trimmedKey = entry.key.Trim();
            if (!lookupCache.ContainsKey(trimmedKey))
            {
                lookupCache.Add(trimmedKey, entry);
            }
        }
    }

    /// <summary>
    /// O(1) lookup by key without allocating strings when already trimmed.
    /// </summary>
    public bool TryGetEntry(string key, out AnimVFXEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(key)) return false;

        if (lookupCache == null)
            RebuildLookup();

        if (lookupCache.TryGetValue(key, out entry))
            return true;

        return lookupCache.TryGetValue(key.Trim(), out entry);
    }
}
