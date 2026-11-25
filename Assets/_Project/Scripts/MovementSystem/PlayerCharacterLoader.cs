using System.Reflection;
using UnityEngine;

/// <summary>
/// MovementSystem: ระบบโหลดและสวมโมเดลตัวละครลงบน Player_Root ตัวเดียว (Unified Character Shell Architecture)
/// - แก้ปัญหาการต้องสร้างและแปะ Component (PlayerMovement, PlayerCombat, PlayerHealth, CharacterController ฯลฯ) ซ้ำทุกตัวละคร!
/// - วิธีใช้: แปะสคริปต์นี้ไว้บน Player_Root เพียงตัวเดียวในฉาก ส่วน Prefab ตัวละครใหม่ให้เป็นแค่โมเดล 3D เปล่าๆ + Animator เท่านั้น
/// - เมื่อเริ่มฉาก ระบบจะเสกโมเดลจาก CharacterData_SO เข้าใต้ ModelSocket แล้วเชื่อม Animator, Renderer, AnimationEvents,
///   ค่าสเตตัส และ VFX (รวมถึง LightningBoltVFXController) ให้ทุกสคริปต์บน Player_Root อัตโนมัติ!
/// </summary>
[DefaultExecutionOrder(-150)]
public class PlayerCharacterLoader : MonoBehaviour
{
    [Header("1. Character Data Resolution (Lobby Selection -> Roster Lookup -> Editor Fallback)")]
    [Tooltip("Optional list of CharacterData_SO ordered by Lobby slot index (0=Hikari, 1=Akane, 2=Shiro/Haru) for automatic model & skill matching when Lobby slots don't assign an SO directly.")]
    [SerializeField] private CharacterData_SO[] characterRosterLookup;
    [Tooltip("Fallback CharacterData_SO used ONLY when pressing Play directly inside a combat scene without coming from The Lobby (or as visual model fallback).")]
    [SerializeField] private CharacterData_SO fallbackCharacterData;

    [Header("2. Visual Model Mount Point")]
    [Tooltip("Transform ลูกบน Player_Root ที่จะใช้สวมโมเดล 3D (หากเว้นว่างจะสร้างชื่อ ModelSocket ให้อัตโนมัติ)")]
    [SerializeField] private Transform modelSocket;
    [Tooltip("ซ่อนโมเดล Placeholder เดิมที่อยู่ใน Player_Root เมื่อโหลดโมเดลจาก CharacterData_SO สำเร็จ")]
    [SerializeField] private GameObject placeholderVisualToHide;

    private GameObject spawnedVisualInstance;
    private const BindingFlags PrivateOrPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public GameObject SpawnedVisualInstance => spawnedVisualInstance;

    private void Awake()
    {
        CharacterData_SO dataToLoad = ResolveActiveCharacterData(out bool preserveLobbyStats);
        if (dataToLoad == null) return;

        ApplyCharacterDataToShell(dataToLoad, preserveLobbyStats);
    }

    private CharacterData_SO ResolveActiveCharacterData(out bool preserveLobbyStats)
    {
        preserveLobbyStats = false;
        GameProgressionManager prog = GameProgressionManager.Instance;

        if (prog != null)
        {
            // 1. Exact CharacterData_SO chosen in The Lobby
            if (prog.ActiveCharacterData != null)
            {
                return prog.ActiveCharacterData;
            }

            // 2. Player selected a character slot in The Lobby without an SO assigned
            if (prog.HasSelectedCharacter)
            {
                int idx = prog.SelectedCharacterIndex;
                if (characterRosterLookup != null && idx >= 0 && idx < characterRosterLookup.Length && characterRosterLookup[idx] != null)
                {
                    prog.SetActiveCharacterData(characterRosterLookup[idx], idx);
                    return characterRosterLookup[idx];
                }

                // Borrow fallback 3D visual model ONLY — strictly preserve Lobby HP/ATK/Speed/Archetype!
                preserveLobbyStats = true;
                return fallbackCharacterData;
            }
        }

        // 3. Direct Play inside combat scene in Editor (no Lobby selection yet)
        if (fallbackCharacterData != null)
        {
            if (prog != null)
            {
                prog.SetActiveCharacterData(fallbackCharacterData);
            }
            return fallbackCharacterData;
        }

        return null;
    }

    public void ApplyCharacterDataToShell(CharacterData_SO data, bool preserveLobbyStats = false)
    {
        if (data == null) return;

        // 1. Spawn Pure 3D Visual Model into ModelSocket (Zero gameplay components needed on the model!)
        Animator activeAnimator = null;
        Renderer activeRenderer = null;

        if (data.characterPrefab != null)
        {
            if (modelSocket == null)
            {
                Transform existing = transform.Find("ModelSocket");
                if (existing != null)
                {
                    modelSocket = existing;
                }
                else
                {
                    GameObject sockObj = new GameObject("ModelSocket");
                    sockObj.transform.SetParent(transform, false);
                    sockObj.transform.localPosition = Vector3.zero;
                    sockObj.transform.localRotation = Quaternion.identity;
                    modelSocket = sockObj.transform;
                }
            }

            if (placeholderVisualToHide != null)
            {
                placeholderVisualToHide.SetActive(false);
            }

            if (spawnedVisualInstance != null)
            {
                Destroy(spawnedVisualInstance);
            }

            spawnedVisualInstance = Instantiate(data.characterPrefab, modelSocket);
            spawnedVisualInstance.name = $"Visual_{data.characterDisplayName}";
            spawnedVisualInstance.transform.localPosition = data.modelLocalOffset;
            spawnedVisualInstance.transform.localRotation = Quaternion.identity;
            spawnedVisualInstance.transform.localScale = data.modelLocalScale;

            // Remove any accidental duplicate CharacterController on the visual prefab so it never fights Player_Root
            if (spawnedVisualInstance.TryGetComponent<CharacterController>(out var dupCC))
            {
                Destroy(dupCC);
            }

            activeAnimator = spawnedVisualInstance.GetComponentInChildren<Animator>(true);
            activeRenderer = spawnedVisualInstance.GetComponentInChildren<Renderer>(true);

            if (activeAnimator != null)
            {
                if (data.animatorControllerOverride != null)
                {
                    activeAnimator.runtimeAnimatorController = data.animatorControllerOverride;
                }
                activeAnimator.applyRootMotion = false;

                // Automatically attach PlayerAnimationEvents to the spawned model's Animator object
                if (activeAnimator.GetComponent<PlayerAnimationEvents>() == null)
                {
                    activeAnimator.gameObject.AddComponent<PlayerAnimationEvents>();
                }
            }
        }

        // 2. Auto-attach & refresh Universal VFXAnchorRig + AnimVFXController on Player_Root so all VFX spawn at exact positions
        var vfxRig = GetComponent<VFXAnchorRig>() ?? gameObject.AddComponent<VFXAnchorRig>();
        vfxRig.RefreshBoneAnchors(activeAnimator);

        var animVfx = GetComponent<AnimVFXController>() ?? gameObject.AddComponent<AnimVFXController>();
        animVfx.RefreshAnchorRig(vfxRig);
        if (data.animVFXLibrary != null)
        {
            animVfx.SetLibrary(data.animVFXLibrary);
        }

        // 3. Wire Animator, Renderer, Stats & VFX into all shared shell components on Player_Root
        GameProgressionManager prog = GameProgressionManager.Instance;
        float resolvedMoveSpeed = (preserveLobbyStats && prog != null) ? prog.SelectedCharacterMoveSpeed : data.moveSpeed;
        float resolvedBaseHp = (preserveLobbyStats && prog != null) ? prog.SelectedCharacterBaseHp : data.baseMaxHP;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            if (activeAnimator != null) SetField(movement, "animator", activeAnimator);
            SetField(movement, "moveSpeed", resolvedMoveSpeed);
        }

        var health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            if (activeAnimator != null) SetField(health, "animator", activeAnimator);
            if (activeRenderer != null) SetField(health, "playerRenderer", activeRenderer);
            SetField(health, "baseMaxHealth", resolvedBaseHp);
        }

        var combat = GetComponent<PlayerCombat>();
        if (combat != null)
        {
            if (activeAnimator != null) SetField(combat, "animator", activeAnimator);
            if (!preserveLobbyStats)
            {
                combat.ApplyCharacterData(data);
            }
            else if (prog != null)
            {
                SetField(combat, "damage", prog.SelectedCharacterBaseAtk);
            }

            // Auto-wire a fire point on the right hand bone if available
            if (activeAnimator != null && activeAnimator.isHuman)
            {
                Transform rightHand = activeAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null)
                {
                    SetField(combat, "rangedFirePoint", rightHand);
                }
            }
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        if (target == null) return;
        FieldInfo fi = target.GetType().GetField(fieldName, PrivateOrPublicInstance);
        if (fi != null)
        {
            fi.SetValue(target, value);
        }
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน PlayerCharacterLoader.cs (MovementSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. Awake() & ResolveActiveCharacterData()
 *    - ทำงานก่อนสคริปต์ตัวละครทั้งหมด ([DefaultExecutionOrder(-150)])
 *    - ดึง CharacterData_SO ของตัวละครที่ผู้เล่นเลือกจากหน้า Hub (หรือใช้ fallbackCharacterData ตอนเทสต์ตรงในฉาก)
 *
 * 2. ApplyCharacterDataToShell(CharacterData_SO data)
 *    - เสกโมเดล 3D เปล่าๆ (data.characterPrefab) เข้าไปสวมไว้ใต้ ModelSocket ของ Player_Root ตัวเดียว
 *    - ติดตั้ง PlayerAnimationEvents บน Animator ของโมเดลใหม่ให้เองอัตโนมัติ (ไม่ต้องแปะ Component ซ้ำบน Prefab ตัวละคร)
 *    - เชื่อม Animator, Renderer, กระดูกมือขวา (rangedFirePoint), ค่าสเตตัส และสวิตช์ VFX สายฟ้า (useWhiteBlueLightningVFX)
 *      ส่งเข้าสู่ PlayerMovement, PlayerCombat, และ PlayerHealth ทั้งหมดในครั้งเดียว
 * ============================================================================ */
