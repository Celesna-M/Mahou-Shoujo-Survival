using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standardized spawn anchors shared across all Player characters and Enemies.
/// Numeric values 0-6 are preserved for 100% Inspector serialization compatibility.
/// </summary>
public enum VFXSpawnAnchor
{
    Muzzle = 0,        // Spell cast / projectile origin (stays aligned in front of actor)
    MeleeSwing = 1,    // Melee slash / punch center in front of actor
    CenterBody = 2,    // Chest / torso center (ideal for hit impacts & auras)
    GroundFeet = 3,    // True ground surface beneath feet (Raycast snapped + lift)
    Overhead = 4,      // Above head (Stun / Phase 2 / Buff)
    RightHand = 5,     // Humanoid RightHand bone
    LeftHand = 6,      // Humanoid LeftHand bone
    WeaponRoot = 7,    // Weapon hilt / base socket
    WeaponTip = 8,     // Weapon blade tip / staff orb socket
    RightFoot = 9,     // Humanoid RightFoot bone
    LeftFoot = 10,     // Humanoid LeftFoot bone
    CustomNamed = 11   // Custom named child socket (resolved via customAnchorName)
}

/// <summary>
/// Controls how a spawned VFX calculates its initial rotation.
/// Solves tilted/upside-down VFX caused by twisting animation bones.
/// </summary>
public enum VFXRotationMode
{
    ActorFlatForward = 0,        // Horizontal actor facing (Y-up, never pitches into the floor)
    ActorFullRotation = 1,       // Exact actor root rotation
    AnchorTransformRotation = 2, // Exact rotation of the bone/weapon/custom anchor Transform
    WorldUpIdentity = 3          // Always upright world identity rotation (best for vertical pillars/rings)
}

/// <summary>
/// Controls whether and how a spawned VFX follows the actor or anchor after spawning.
/// </summary>
public enum VFXFollowMode
{
    WorldSpace = 0,            // Detaches immediately in world space (impacts, slash arcs, ground blasts)
    FollowActorRoot = 1,       // Parented to actor root (auras, dash rings)
    FollowAnchorTransform = 2, // Parented to the exact anchor/bone/weapon Transform (weapon glow, palm charge)
    FollowPositionOnly = 3     // Tracks anchor world position each LateUpdate WITHOUT inheriting bone twist
}

[Serializable]
public struct NamedVFXAnchor
{
    [Tooltip("Lookup key referenced by AnimVFXEntry.customAnchorName (case-insensitive).")]
    public string anchorName;

    [Tooltip("Transform socket on the character/weapon rig.")]
    public Transform anchorTransform;
}

/// <summary>
/// CombatSystem: Universal VFX anchor & orientation resolver for both Players and Enemies.
/// Fixes floating ground VFX (ignores actor self-colliders), supports WeaponRoot/WeaponTip/CustomNamed sockets,
/// supports 4 RotationModes + 4 FollowModes, and draws directional Scene View Gizmos.
/// </summary>
public class VFXAnchorRig : MonoBehaviour
{
    [Header("1. Optional Custom Anchor Transforms (Leave empty for auto bone/offset fallback)")]
    [Tooltip("Custom muzzle / spell cast Transform.")]
    [SerializeField] private Transform customMuzzlePoint;
    [Tooltip("Custom melee slash / strike origin Transform.")]
    [SerializeField] private Transform customMeleePoint;
    [Tooltip("Custom torso/chest center Transform.")]
    [SerializeField] private Transform customCenterPoint;
    [Tooltip("Custom feet/ground reference Transform.")]
    [SerializeField] private Transform customGroundPoint;
    [Tooltip("Custom weapon hilt/base Transform (auto-detected if child is named 'WeaponRoot').")]
    [SerializeField] private Transform customWeaponRoot;
    [Tooltip("Custom weapon blade/staff tip Transform (auto-detected if child is named 'WeaponTip').")]
    [SerializeField] private Transform customWeaponTip;

    [Header("2. Custom Named Sockets (e.g. 'ScytheTip', 'StaffOrb', 'BackHalo')")]
    [Tooltip("Extra named Transform sockets usable with VFXSpawnAnchor.CustomNamed.")]
    [SerializeField] private NamedVFXAnchor[] customNamedAnchors;

    [Header("3. Universal Local Offsets (Relative to Actor Root)")]
    [Tooltip("Muzzle / spell cast offset (X=Right, Y=Up, Z=Forward).")]
    [SerializeField] private Vector3 muzzleLocalOffset = new Vector3(0f, 1.25f, 0.85f);
    [Tooltip("Melee swing / front skill offset.")]
    [SerializeField] private Vector3 meleeLocalOffset = new Vector3(0f, 1.05f, 1.15f);
    [Tooltip("Chest / body center offset (for hit impacts & body auras).")]
    [SerializeField] private Vector3 centerBodyLocalOffset = new Vector3(0f, 1.05f, 0f);
    [Tooltip("Overhead offset (for buffs/stuns).")]
    [SerializeField] private Vector3 overheadLocalOffset = new Vector3(0f, 2.15f, 0f);
    [Tooltip("Small vertical lift above ground surface to prevent Z-fighting on Ground AoE VFX.")]
    [SerializeField] private float groundSurfaceLift = 0.05f;
    [Tooltip("Layers considered valid ground. Leave at default (-1) to auto-exclude Player/Enemy/Ignore Raycast & self colliders.")]
    [SerializeField] private LayerMask groundRaycastLayers = -1;

    private Animator cachedAnimator;
    private Transform rightHandBone;
    private Transform leftHandBone;
    private Transform rightFootBone;
    private Transform leftFootBone;
    private Transform runtimeWeaponRoot;
    private Transform runtimeWeaponTip;

    private readonly Dictionary<string, Transform> namedAnchorLookup = new Dictionary<string, Transform>(8, StringComparer.OrdinalIgnoreCase);

    // Pre-allocated NonAlloc Raycast buffer (Zero GC spikes)
    private static readonly RaycastHit[] GroundHitBuffer = new RaycastHit[12];
    private static int cachedDefaultGroundMask = 0;

    private void Awake()
    {
        RefreshBoneAnchors();
    }

    /// <summary>
    /// Refreshes Humanoid bones, auto-discovered weapon sockets, and named anchors.
    /// Call again whenever PlayerCharacterLoader mounts a new 3D character model under Player_Root.
    /// </summary>
    public void RefreshBoneAnchors(Animator overrideAnimator = null)
    {
        cachedAnimator = overrideAnimator != null ? overrideAnimator : GetComponentInChildren<Animator>(true);
        rightHandBone = null;
        leftHandBone = null;
        rightFootBone = null;
        leftFootBone = null;
        runtimeWeaponRoot = customWeaponRoot;
        runtimeWeaponTip = customWeaponTip;

        if (cachedAnimator != null && cachedAnimator.isHuman)
        {
            rightHandBone = cachedAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHandBone = cachedAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightFootBone = cachedAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftFootBone = cachedAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        }

        RebuildNamedAnchorLookup();
        AutoDiscoverChildSockets();
    }

    private void RebuildNamedAnchorLookup()
    {
        namedAnchorLookup.Clear();
        if (customNamedAnchors == null) return;

        for (int i = 0; i < customNamedAnchors.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(customNamedAnchors[i].anchorName) && customNamedAnchors[i].anchorTransform != null)
            {
                namedAnchorLookup[customNamedAnchors[i].anchorName.Trim()] = customNamedAnchors[i].anchorTransform;
            }
        }
    }

    /// <summary>
    /// Scans child transforms once on Awake/model swap to auto-bind sockets named
    /// WeaponRoot, WeaponTip, MuzzlePoint, or prefixed with VFX_.
    /// </summary>
    private void AutoDiscoverChildSockets()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == transform) continue;

            string n = t.name;
            if (runtimeWeaponRoot == null && string.Equals(n, "WeaponRoot", StringComparison.OrdinalIgnoreCase))
            {
                runtimeWeaponRoot = t;
            }
            else if (runtimeWeaponTip == null && string.Equals(n, "WeaponTip", StringComparison.OrdinalIgnoreCase))
            {
                runtimeWeaponTip = t;
            }
            else if (customMuzzlePoint == null && (string.Equals(n, "MuzzlePoint", StringComparison.OrdinalIgnoreCase) || string.Equals(n, "RangedFirePoint", StringComparison.OrdinalIgnoreCase)))
            {
                customMuzzlePoint = t;
            }

            if (n.StartsWith("VFX_", StringComparison.OrdinalIgnoreCase))
            {
                string shortKey = n.Substring(4);
                if (!string.IsNullOrEmpty(shortKey) && !namedAnchorLookup.ContainsKey(shortKey))
                    namedAnchorLookup[shortKey] = t;
                if (!namedAnchorLookup.ContainsKey(n))
                    namedAnchorLookup[n] = t;
            }
        }
    }

    /// <summary>
    /// Resolves the live Transform representing the requested anchor (may return null if offset-based).
    /// </summary>
    public Transform GetAnchorTransform(VFXSpawnAnchor anchor, string customAnchorName = null)
    {
        if (!string.IsNullOrWhiteSpace(customAnchorName) &&
            namedAnchorLookup.TryGetValue(customAnchorName.Trim(), out Transform customMatch) &&
            customMatch != null)
        {
            return customMatch;
        }

        switch (anchor)
        {
            case VFXSpawnAnchor.Muzzle: return customMuzzlePoint != null ? customMuzzlePoint : (runtimeWeaponTip != null ? runtimeWeaponTip : rightHandBone);
            case VFXSpawnAnchor.MeleeSwing: return customMeleePoint;
            case VFXSpawnAnchor.CenterBody: return customCenterPoint;
            case VFXSpawnAnchor.GroundFeet: return customGroundPoint;
            case VFXSpawnAnchor.RightHand: return rightHandBone;
            case VFXSpawnAnchor.LeftHand: return leftHandBone;
            case VFXSpawnAnchor.WeaponRoot: return runtimeWeaponRoot != null ? runtimeWeaponRoot : rightHandBone;
            case VFXSpawnAnchor.WeaponTip: return runtimeWeaponTip != null ? runtimeWeaponTip : (runtimeWeaponRoot != null ? runtimeWeaponRoot : rightHandBone);
            case VFXSpawnAnchor.RightFoot: return rightFootBone;
            case VFXSpawnAnchor.LeftFoot: return leftFootBone;
            case VFXSpawnAnchor.CustomNamed: return null;
            default: return null;
        }
    }

    public Vector3 GetAnchorPosition(VFXSpawnAnchor anchor) => GetAnchorPosition(anchor, null);

    public Vector3 GetAnchorPosition(VFXSpawnAnchor anchor, string customAnchorName)
    {
        if (!string.IsNullOrWhiteSpace(customAnchorName) &&
            namedAnchorLookup.TryGetValue(customAnchorName.Trim(), out Transform named) &&
            named != null)
        {
            return named.position;
        }

        switch (anchor)
        {
            case VFXSpawnAnchor.Muzzle:
                if (customMuzzlePoint != null) return customMuzzlePoint.position;
                if (runtimeWeaponTip != null) return runtimeWeaponTip.position;
                if (rightHandBone != null)
                {
                    Vector3 handPos = rightHandBone.position;
                    Vector3 safeFront = transform.TransformPoint(muzzleLocalOffset);
                    return Vector3.Lerp(handPos, safeFront, 0.45f);
                }
                return transform.TransformPoint(muzzleLocalOffset);

            case VFXSpawnAnchor.MeleeSwing:
                return customMeleePoint != null ? customMeleePoint.position : transform.TransformPoint(meleeLocalOffset);

            case VFXSpawnAnchor.CenterBody:
                return customCenterPoint != null ? customCenterPoint.position : transform.TransformPoint(centerBodyLocalOffset);

            case VFXSpawnAnchor.GroundFeet:
                Vector3 basePos = customGroundPoint != null ? customGroundPoint.position : transform.position;
                return SnapToGroundSurface(basePos, groundSurfaceLift, GetEffectiveGroundMask(), transform.root);

            case VFXSpawnAnchor.Overhead:
                return transform.TransformPoint(overheadLocalOffset);

            case VFXSpawnAnchor.RightHand:
                return rightHandBone != null ? rightHandBone.position : transform.TransformPoint(muzzleLocalOffset);

            case VFXSpawnAnchor.LeftHand:
                return leftHandBone != null ? leftHandBone.position : transform.TransformPoint(new Vector3(-0.35f, muzzleLocalOffset.y, muzzleLocalOffset.z));

            case VFXSpawnAnchor.WeaponRoot:
                if (runtimeWeaponRoot != null) return runtimeWeaponRoot.position;
                return rightHandBone != null ? rightHandBone.position : transform.TransformPoint(meleeLocalOffset);

            case VFXSpawnAnchor.WeaponTip:
                if (runtimeWeaponTip != null) return runtimeWeaponTip.position;
                if (runtimeWeaponRoot != null) return runtimeWeaponRoot.position + runtimeWeaponRoot.forward * 0.8f;
                return transform.TransformPoint(meleeLocalOffset);

            case VFXSpawnAnchor.RightFoot:
                return rightFootBone != null ? rightFootBone.position : transform.position + transform.right * 0.15f;

            case VFXSpawnAnchor.LeftFoot:
                return leftFootBone != null ? leftFootBone.position : transform.position - transform.right * 0.15f;

            default:
                return transform.TransformPoint(centerBodyLocalOffset);
        }
    }

    public Quaternion GetAnchorRotation(VFXSpawnAnchor anchor)
        => GetAnchorRotation(anchor, VFXRotationMode.ActorFlatForward, null);

    public Quaternion GetAnchorRotation(VFXSpawnAnchor anchor, VFXRotationMode rotationMode, string customAnchorName = null)
    {
        switch (rotationMode)
        {
            case VFXRotationMode.WorldUpIdentity:
                return Quaternion.identity;

            case VFXRotationMode.ActorFullRotation:
                return transform.rotation;

            case VFXRotationMode.AnchorTransformRotation:
                Transform anchorTf = GetAnchorTransform(anchor, customAnchorName);
                if (anchorTf != null) return anchorTf.rotation;
                return GetFlatForwardRotation(transform);

            case VFXRotationMode.ActorFlatForward:
            default:
                return GetFlatForwardRotation(transform);
        }
    }

    /// <summary>
    /// Resolves final world position + rotation including per-entry local offsets and optional ground snap.
    /// </summary>
    public void ResolveWorldPose(
        VFXSpawnAnchor anchor,
        string customAnchorName,
        VFXRotationMode rotationMode,
        Vector3 localPositionOffset,
        Vector3 localEulerOffset,
        bool snapToGround,
        out Vector3 worldPosition,
        out Quaternion worldRotation,
        out Transform anchorTransform)
    {
        anchorTransform = GetAnchorTransform(anchor, customAnchorName);
        Vector3 basePos = GetAnchorPosition(anchor, customAnchorName);
        Quaternion baseRot = GetAnchorRotation(anchor, rotationMode, customAnchorName);

        worldPosition = basePos + (baseRot * localPositionOffset);
        worldRotation = baseRot * Quaternion.Euler(localEulerOffset);

        if (snapToGround || anchor == VFXSpawnAnchor.GroundFeet)
        {
            worldPosition = SnapToGroundSurface(worldPosition, groundSurfaceLift, GetEffectiveGroundMask(), transform.root);
        }
    }

    public static Vector3 ResolvePosition(Transform owner, VFXSpawnAnchor anchor)
    {
        if (owner == null) return Vector3.zero;
        VFXAnchorRig rig = owner.GetComponent<VFXAnchorRig>() ?? owner.GetComponentInParent<VFXAnchorRig>();
        if (rig != null) return rig.GetAnchorPosition(anchor);

        switch (anchor)
        {
            case VFXSpawnAnchor.Muzzle: return owner.TransformPoint(new Vector3(0f, 1.25f, 0.85f));
            case VFXSpawnAnchor.MeleeSwing: return owner.TransformPoint(new Vector3(0f, 1.05f, 1.15f));
            case VFXSpawnAnchor.GroundFeet: return SnapToGroundSurface(owner.position, 0.05f, default, owner.root);
            case VFXSpawnAnchor.Overhead: return owner.TransformPoint(new Vector3(0f, 2.15f, 0f));
            default: return owner.TransformPoint(new Vector3(0f, 1.05f, 0f));
        }
    }

    public static Quaternion ResolveRotation(Transform owner, VFXSpawnAnchor anchor)
    {
        if (owner == null) return Quaternion.identity;
        VFXAnchorRig rig = owner.GetComponent<VFXAnchorRig>() ?? owner.GetComponentInParent<VFXAnchorRig>();
        if (rig != null) return rig.GetAnchorRotation(anchor);

        return GetFlatForwardRotation(owner);
    }

    /// <summary>
    /// Zero-GC ground surface snapping that safely excludes Player, Enemy, Ignore Raycast layers
    /// AND any collider belonging to ignoreRoot (prevents ground VFX from floating on actor colliders).
    /// </summary>
    public static Vector3 SnapToGroundSurface(Vector3 worldPos, float liftOffset = 0.05f, LayerMask mask = default, Transform ignoreRoot = null)
    {
        int layerMask = ResolveEffectiveGroundMask(mask);
        Vector3 rayOrigin = worldPos + Vector3.up * 1.4f;

        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, GroundHitBuffer, 6.0f, layerMask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        bool foundValidGround = false;
        Vector3 bestPoint = worldPos;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = GroundHitBuffer[i];
            Collider col = hit.collider;
            if (col == null) continue;
            if (ignoreRoot != null && col.transform.root == ignoreRoot) continue;
            if (col is CharacterController) continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestPoint = hit.point;
                foundValidGround = true;
            }
        }

        if (foundValidGround)
        {
            return bestPoint + Vector3.up * liftOffset;
        }

        return new Vector3(worldPos.x, worldPos.y + liftOffset, worldPos.z);
    }

    private LayerMask GetEffectiveGroundMask()
    {
        // When left at default All (-1) or Nothing (0), exclude actor layers automatically
        if (groundRaycastLayers.value == -1 || groundRaycastLayers.value == 0)
            return GetDefaultGroundMask();
        return groundRaycastLayers;
    }

    private static int ResolveEffectiveGroundMask(LayerMask mask)
    {
        if (mask.value == 0 || mask.value == -1)
            return GetDefaultGroundMask();
        return mask.value;
    }

    private static int GetDefaultGroundMask()
    {
        if (cachedDefaultGroundMask == 0)
        {
            int excluded = LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");
            cachedDefaultGroundMask = ~excluded;
        }
        return cachedDefaultGroundMask;
    }

    private static Quaternion GetFlatForwardRotation(Transform tf)
    {
        if (tf == null) return Quaternion.identity;
        Vector3 flatFwd = tf.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        return Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        DrawAnchorGizmo(customMuzzlePoint != null ? customMuzzlePoint.position : transform.TransformPoint(muzzleLocalOffset), transform.forward, new Color(0.2f, 0.85f, 1f, 0.9f), 0.12f);
        DrawAnchorGizmo(customMeleePoint != null ? customMeleePoint.position : transform.TransformPoint(meleeLocalOffset), transform.forward, new Color(1f, 0.45f, 0.2f, 0.9f), 0.14f);
        DrawAnchorGizmo(customCenterPoint != null ? customCenterPoint.position : transform.TransformPoint(centerBodyLocalOffset), transform.forward, new Color(1f, 0.25f, 0.65f, 0.9f), 0.13f);
        DrawAnchorGizmo(transform.position + Vector3.up * groundSurfaceLift, transform.forward, new Color(0.3f, 1f, 0.45f, 0.9f), 0.16f);

        if (customWeaponRoot != null)
            DrawAnchorGizmo(customWeaponRoot.position, customWeaponRoot.forward, new Color(1f, 0.9f, 0.2f, 0.95f), 0.10f);
        if (customWeaponTip != null)
            DrawAnchorGizmo(customWeaponTip.position, customWeaponTip.forward, new Color(1f, 0.2f, 0.2f, 0.95f), 0.10f);

        if (customNamedAnchors != null)
        {
            for (int i = 0; i < customNamedAnchors.Length; i++)
            {
                Transform t = customNamedAnchors[i].anchorTransform;
                if (t != null)
                    DrawAnchorGizmo(t.position, t.forward, new Color(0.75f, 0.45f, 1f, 0.95f), 0.09f);
            }
        }
    }

    private static void DrawAnchorGizmo(Vector3 pos, Vector3 forward, Color color, float radius)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(pos, radius);
        if (forward.sqrMagnitude > 0.001f)
            Gizmos.DrawRay(pos, forward.normalized * (radius * 3.0f));
    }
}
