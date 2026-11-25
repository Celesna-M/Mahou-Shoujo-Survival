using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized string key constants for AudioLibrary_SO lookups (prevents magic-string typos).
/// </summary>
public static class AudioKeys
{
    // BGM Track IDs
    public const string BGM_MainMenu = "BGM_MainMenu";
    public const string BGM_Lobby = "BGM_Lobby";
    public const string BGM_StageMap = "BGM_StageMap";
    public const string BGM_Stage_Action = "BGM_Stage_Action";
    public const string BGM_Stage_Shibuya = "BGM_Stage_Shibuya";
    public const string BGM_Boss_01 = "BGM_Boss_01";
    public const string BGM_Boss_02 = "BGM_Boss_02";

    // UI SFX Keys
    public const string UI_Click = "UI_Click";
    public const string UI_Confirm = "UI_Confirm";
    public const string UI_Back = "UI_Back";
    public const string UI_Upgrade = "UI_Upgrade";
    public const string UI_Error = "UI_Error";

    // Combat & Movement SFX Keys
    public const string Combat_Swing = "Combat_Swing";
    public const string Combat_HitImpact = "Combat_HitImpact";
    public const string Combat_HeavyHit = "Combat_HeavyHit";
    public const string Combat_RangedCast = "Combat_RangedCast";
    public const string Combat_SkillCast = "Combat_SkillCast";
    public const string Combat_Dodge = "Combat_Dodge";
    public const string Combat_Footstep = "Combat_Footstep";
    public const string Combat_LockOn = "Combat_LockOn";
    public const string Combat_LockSwitch = "Combat_LockSwitch";
    public const string Combat_PlayerHurt = "Combat_PlayerHurt";
    public const string Combat_PlayerDeath = "Combat_PlayerDeath";
    public const string Combat_EnemyAttack = "Combat_EnemyAttack";
    public const string Combat_EnemyHurt = "Combat_EnemyHurt";
    public const string Combat_BossStun = "Combat_BossStun";
    public const string Combat_BossEnrage = "Combat_BossEnrage";

    // Reward & Stage State SFX Keys
    public const string Reward_StarDust = "Reward_StarDust";
    public const string Reward_Core = "Reward_Core";
    public const string Stage_Victory = "Stage_Victory";
    public const string Stage_Defeat = "Stage_Defeat";
}

[Serializable]
public class BGMTrackEntry
{
    [Tooltip("Unique BGM track ID referenced by Scene BGM mappings or Boss triggers (e.g., BGM_MainMenu).")]
    public string trackId = "BGM_Track";

    [Tooltip("Optional intro clip played once before seamlessly transitioning into loopClip via AudioSettings.dspTime.")]
    public AudioClip introClip;

    [Tooltip("Main looping music clip (plays continuously after introClip finishes, or immediately if introClip is empty).")]
    public AudioClip loopClip;

    [Range(0f, 1.5f)]
    [Tooltip("Per-track volume multiplier.")]
    public float volume = 0.72f;
}

[Serializable]
public class SceneBGMConfig
{
    [Tooltip("Exact Unity Scene name (e.g., MainMenu, The Lobby, StageMap, The Action, Shibuya).")]
    public string sceneName = "MainMenu";

    [Tooltip("Default BGM Track ID played automatically when this scene loads.")]
    public string defaultTrackId = AudioKeys.BGM_MainMenu;

    [Tooltip("Optional Boss Battle Track ID crossfaded in when an Elite/Boss spawns in this scene.")]
    public string bossTrackId = AudioKeys.BGM_Boss_01;
}

[Serializable]
public class SFXCueEntry
{
    [Tooltip("Lookup key used by AudioManager.PlaySFX / PlayUI and Animation Events (e.g., Combat_Swing, UI_Click).")]
    public string key = "SFX_Key";

    [Tooltip("One or more AudioClips chosen at random (avoids repeating the same clip twice in a row).")]
    public AudioClip[] clips;

    [Range(0f, 1.5f)]
    [Tooltip("Base volume multiplier for this sound cue.")]
    public float volume = 0.85f;

    [Range(0.5f, 1.5f)]
    [Tooltip("Minimum randomized playback pitch.")]
    public float minPitch = 0.95f;

    [Range(0.5f, 1.5f)]
    [Tooltip("Maximum randomized playback pitch.")]
    public float maxPitch = 1.05f;

    [Tooltip("Play as non-spatial 2D sound (ideal for UI, Lock-On, Rewards, and Stingers).")]
    public bool is2D = false;

    [Range(0f, 1f)]
    [Tooltip("3D spatial blend used when is2D is false (0 = 2D, 1 = full 3D). ~0.65 keeps combat hits punchy at 7.5m camera distance.")]
    public float spatialBlend = 0.65f;

    [Tooltip("Minimum distance before 3D volume rolloff begins.")]
    public float minDistance = 4f;

    [Tooltip("Maximum audible distance for 3D spatial sounds.")]
    public float maxDistance = 35f;

    [Tooltip("Minimum seconds between plays of this same key (prevents audio clipping on multi-target AoE hits).")]
    public float minCooldown = 0.04f;
}

/// <summary>
/// DataSystem / AudioSystem: Central ScriptableObject holding all Scene BGM routes,
/// JRPG Intro+Loop music tracks, and keyed SFX cues with variations and pitch jitter.
/// Place as Assets/_Project/Resources/DefaultAudioLibrary.asset for automatic startup loading.
/// </summary>
[CreateAssetMenu(fileName = "DefaultAudioLibrary", menuName = "StellaClash/Data/Audio Library", order = 6)]
public class AudioLibrary_SO : ScriptableObject
{
    [Header("1. Scene -> BGM Mapping")]
    [Tooltip("Maps each Unity scene to its default background music and optional Boss battle track.")]
    public List<SceneBGMConfig> sceneMusicConfigs = new List<SceneBGMConfig>();

    [Header("2. BGM Tracks (Supports Seamless Intro + Loop)")]
    [Tooltip("Library of all BGM tracks referenced by Track ID.")]
    public List<BGMTrackEntry> bgmTracks = new List<BGMTrackEntry>();

    [Header("3. SFX Cues (Variations, Pitch Jitter & Spam Guard)")]
    [Tooltip("Library of all UI, Combat, Movement, and Reward sound cues.")]
    public List<SFXCueEntry> sfxCues = new List<SFXCueEntry>();
}
