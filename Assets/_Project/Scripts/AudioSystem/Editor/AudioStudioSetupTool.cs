#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor Studio Tool: Synthesizes clean, lightweight 16-bit PCM placeholder .wav SFX for missing slots
/// and auto-wires ALL existing project BGM (with JRPG Intro+Loop pairs) and SFX clips into
/// Assets/_Project/Resources/DefaultAudioLibrary.asset (GUID-safe in-place updates).
/// </summary>
public static class AudioStudioSetupTool
{
    private const string GeneratedSfxDir = "Assets/_Project/Audio/GeneratedSFX";
    private const string LibraryAssetPath = "Assets/_Project/Resources/DefaultAudioLibrary.asset";
    private const int SampleRate = 44100;

    [MenuItem("Tools/StellaClash/Audio Studio/Build Default Audio Library & Placeholder SFX", priority = 10)]
    public static void BuildDefaultAudioLibraryAndPlaceholders()
    {
        EnsureFolderExists("Assets/_Project/Audio");
        EnsureFolderExists(GeneratedSfxDir);
        EnsureFolderExists("Assets/_Project/Resources");

        GenerateAllPlaceholderWavs();
        AssetDatabase.Refresh();

        AudioLibrary_SO lib = AssetDatabase.LoadAssetAtPath<AudioLibrary_SO>(LibraryAssetPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<AudioLibrary_SO>();
            AssetDatabase.CreateAsset(lib, LibraryAssetPath);
        }

        PopulateDefaultAudioLibrary(lib);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
        Debug.Log($"<color=#40FF90>[AudioStudio]</color> Built & wired {LibraryAssetPath} ({lib.bgmTracks.Count} BGM tracks, {lib.sceneMusicConfigs.Count} scenes, {lib.sfxCues.Count} SFX cues)!");
    }

    [MenuItem("Tools/StellaClash/Audio Studio/Select Default Audio Library", priority = 11)]
    public static void SelectDefaultAudioLibrary()
    {
        AudioLibrary_SO lib = AssetDatabase.LoadAssetAtPath<AudioLibrary_SO>(LibraryAssetPath);
        if (lib != null)
        {
            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
        }
        else
        {
            Debug.LogWarning($"[AudioStudio] '{LibraryAssetPath}' not found yet. Run 'Build Default Audio Library & Placeholder SFX' first.");
        }
    }

    // =========================================================================
    // 1. AUTO-WIRE ALL EXISTING + GENERATED CLIPS INTO DEFAULT AUDIO LIBRARY
    // =========================================================================

    private static void PopulateDefaultAudioLibrary(AudioLibrary_SO lib)
    {
        lib.sceneMusicConfigs = new List<SceneBGMConfig>
        {
            new SceneBGMConfig { sceneName = "MainMenu",   defaultTrackId = AudioKeys.BGM_MainMenu,      bossTrackId = "" },
            new SceneBGMConfig { sceneName = "The Lobby",  defaultTrackId = AudioKeys.BGM_Lobby,         bossTrackId = "" },
            new SceneBGMConfig { sceneName = "StageMap",   defaultTrackId = AudioKeys.BGM_StageMap,      bossTrackId = "" },
            new SceneBGMConfig { sceneName = "The Action", defaultTrackId = AudioKeys.BGM_Stage_Action,  bossTrackId = AudioKeys.BGM_Boss_01 },
            new SceneBGMConfig { sceneName = "Shibuya",    defaultTrackId = AudioKeys.BGM_Stage_Shibuya, bossTrackId = AudioKeys.BGM_Boss_02 }
        };

        const string coldDir = "Assets/_Project/Audio/Alex Coldfire 5 tracks Wav/Alex Coldfire 5 tracks Wav";
        const string jrpgDir = "Assets/_Project/Audio/JRPG Battle BGM Pack 1 ogg&m4a/ogg&m4a";

        lib.bgmTracks = new List<BGMTrackEntry>
        {
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_MainMenu,
                introClip = null,
                loopClip = LoadClip($"{coldDir}/Aurora.wav"),
                volume = 0.65f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_Lobby,
                introClip = null,
                loopClip = LoadClip($"{coldDir}/Garden Street.wav"),
                volume = 0.62f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_StageMap,
                introClip = null,
                loopClip = LoadClip($"{coldDir}/Broken Reality.wav"),
                volume = 0.64f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_Stage_Action,
                introClip = LoadClip($"{jrpgDir}/Normal Battle/rpg_bt001/unityloop/rpg_bt001-intro.ogg"),
                loopClip  = LoadClip($"{jrpgDir}/Normal Battle/rpg_bt001/unityloop/rpg_bt001-loop.ogg"),
                volume = 0.68f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_Stage_Shibuya,
                introClip = LoadClip($"{jrpgDir}/Normal Battle/rpg_bt003/unityloop/rpg_bt003-intro.ogg"),
                loopClip  = LoadClip($"{jrpgDir}/Normal Battle/rpg_bt003/unityloop/rpg_bt003-loop.ogg"),
                volume = 0.68f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_Boss_01,
                introClip = LoadClip($"{jrpgDir}/Boss Battle/rpg_bs001/unityloop/rpg_bs001-intro.ogg"),
                loopClip  = LoadClip($"{jrpgDir}/Boss Battle/rpg_bs001/unityloop/rpg_bs001-loop.ogg"),
                volume = 0.76f
            },
            new BGMTrackEntry
            {
                trackId = AudioKeys.BGM_Boss_02,
                introClip = LoadClip($"{jrpgDir}/Boss Battle/rpg_bs002/unityloop/rpg_bs002-intro.ogg"),
                loopClip  = LoadClip($"{jrpgDir}/Boss Battle/rpg_bs002/unityloop/rpg_bs002-loop.ogg"),
                volume = 0.76f
            }
        };

        const string scytheDir = "Assets/ThirdParty/Scythe girl pack/audio";
        const string vefectsDir = "Assets/ThirdParty/Vefects/Trails/Sounds/WAV";

        lib.sfxCues = new List<SFXCueEntry>
        {
            // UI (2D)
            MakeCue(AudioKeys.UI_Click,   is2D: true,  vol: 0.65f, minP: 0.98f, maxP: 1.03f, cd: 0.03f, LoadClip($"{GeneratedSfxDir}/UI_Click.wav")),
            MakeCue(AudioKeys.UI_Confirm, is2D: true,  vol: 0.75f, minP: 0.99f, maxP: 1.02f, cd: 0.05f, LoadClip($"{GeneratedSfxDir}/UI_Confirm.wav")),
            MakeCue(AudioKeys.UI_Back,    is2D: true,  vol: 0.65f, minP: 0.98f, maxP: 1.02f, cd: 0.05f, LoadClip($"{GeneratedSfxDir}/UI_Back.wav")),
            MakeCue(AudioKeys.UI_Upgrade, is2D: true,  vol: 0.85f, minP: 0.98f, maxP: 1.04f, cd: 0.08f, LoadClip($"{GeneratedSfxDir}/UI_Upgrade.wav")),
            MakeCue(AudioKeys.UI_Error,   is2D: true,  vol: 0.72f, minP: 0.96f, maxP: 1.00f, cd: 0.08f, LoadClip($"{GeneratedSfxDir}/UI_Error.wav")),

            // Combat & Movement (3D / 2D)
            MakeCue(AudioKeys.Combat_Swing,      is2D: false, vol: 0.75f, minP: 0.92f, maxP: 1.10f, cd: 0.05f,
                LoadClip($"{scytheDir}/swoosh1.mp3"), LoadClip($"{scytheDir}/swoosh2.mp3")),
            MakeCue(AudioKeys.Combat_HitImpact,  is2D: false, vol: 0.85f, minP: 0.92f, maxP: 1.12f, cd: 0.035f,
                LoadClip($"{GeneratedSfxDir}/Combat_HitImpact.wav")),
            MakeCue(AudioKeys.Combat_HeavyHit,   is2D: false, vol: 0.95f, minP: 0.94f, maxP: 1.06f, cd: 0.05f,
                LoadClip($"{GeneratedSfxDir}/Combat_HeavyHit.wav")),
            MakeCue(AudioKeys.Combat_RangedCast, is2D: false, vol: 0.72f, minP: 0.95f, maxP: 1.12f, cd: 0.05f,
                LoadClip($"{vefectsDir}/Vefects_SFX_Trail_Electric.wav")),
            MakeCue(AudioKeys.Combat_SkillCast,  is2D: false, vol: 0.88f, minP: 0.96f, maxP: 1.05f, cd: 0.12f,
                LoadClip($"{vefectsDir}/Vefects_SFX_Trail_Cosmos.wav"), LoadClip($"{vefectsDir}/Vefects_SFX_Trail_Electric.wav")),
            MakeCue(AudioKeys.Combat_Dodge,      is2D: false, vol: 0.72f, minP: 0.94f, maxP: 1.08f, cd: 0.08f,
                LoadClip($"{GeneratedSfxDir}/Combat_Dodge.wav")),
            MakeCue(AudioKeys.Combat_Footstep,   is2D: false, vol: 0.42f, minP: 0.90f, maxP: 1.12f, cd: 0.12f,
                LoadClip($"{scytheDir}/footstep1.mp3"),
                LoadClip($"{scytheDir}/footstep2.mp3"),
                LoadClip($"{scytheDir}/footstep3.mp3"),
                LoadClip($"{scytheDir}/footstep4.mp3"),
                LoadClip($"{scytheDir}/footstep5.mp3")),
            MakeCue(AudioKeys.Combat_LockOn,     is2D: true,  vol: 0.65f, minP: 0.98f, maxP: 1.03f, cd: 0.05f,
                LoadClip($"{GeneratedSfxDir}/Combat_LockOn.wav")),
            MakeCue(AudioKeys.Combat_LockSwitch, is2D: true,  vol: 0.55f, minP: 1.02f, maxP: 1.12f, cd: 0.05f,
                LoadClip($"{GeneratedSfxDir}/Combat_LockSwitch.wav")),
            MakeCue(AudioKeys.Combat_PlayerHurt, is2D: true,  vol: 0.85f, minP: 0.94f, maxP: 1.06f, cd: 0.12f,
                LoadClip($"{GeneratedSfxDir}/Combat_PlayerHurt.wav")),
            MakeCue(AudioKeys.Combat_PlayerDeath,is2D: true,  vol: 0.92f, minP: 1.00f, maxP: 1.00f, cd: 0.50f,
                LoadClip($"{GeneratedSfxDir}/Combat_PlayerDeath.wav")),
            MakeCue(AudioKeys.Combat_EnemyAttack,is2D: false, vol: 0.72f, minP: 0.88f, maxP: 1.05f, cd: 0.08f,
                LoadClip($"{vefectsDir}/Vefects_SFX_Trail_Dark.wav")),
            MakeCue(AudioKeys.Combat_EnemyHurt,  is2D: false, vol: 0.70f, minP: 0.90f, maxP: 1.12f, cd: 0.04f,
                LoadClip($"{GeneratedSfxDir}/Combat_EnemyHurt.wav")),
            MakeCue(AudioKeys.Combat_BossStun,   is2D: true,  vol: 0.95f, minP: 0.98f, maxP: 1.02f, cd: 0.25f,
                LoadClip($"{GeneratedSfxDir}/Combat_BossStun.wav")),
            MakeCue(AudioKeys.Combat_BossEnrage, is2D: true,  vol: 0.92f, minP: 0.95f, maxP: 1.02f, cd: 0.30f,
                LoadClip($"{vefectsDir}/Vefects_SFX_Trail_Void.wav")),

            // Rewards & Stage Stingers (2D)
            MakeCue(AudioKeys.Reward_StarDust, is2D: true, vol: 0.58f, minP: 0.95f, maxP: 1.18f, cd: 0.045f,
                LoadClip($"{GeneratedSfxDir}/Reward_StarDust.wav")),
            MakeCue(AudioKeys.Reward_Core,     is2D: true, vol: 0.82f, minP: 0.98f, maxP: 1.05f, cd: 0.08f,
                LoadClip($"{GeneratedSfxDir}/Reward_Core.wav")),
            MakeCue(AudioKeys.Stage_Victory,   is2D: true, vol: 0.92f, minP: 1.00f, maxP: 1.00f, cd: 0.50f,
                LoadClip($"{GeneratedSfxDir}/Stage_Victory.wav")),
            MakeCue(AudioKeys.Stage_Defeat,    is2D: true, vol: 0.92f, minP: 1.00f, maxP: 1.00f, cd: 0.50f,
                LoadClip($"{GeneratedSfxDir}/Stage_Defeat.wav"))
        };
    }

    private static SFXCueEntry MakeCue(string key, bool is2D, float vol, float minP, float maxP, float cd, params AudioClip[] rawClips)
    {
        var valid = new List<AudioClip>();
        if (rawClips != null)
        {
            for (int i = 0; i < rawClips.Length; i++)
            {
                if (rawClips[i] != null) valid.Add(rawClips[i]);
            }
        }
        return new SFXCueEntry
        {
            key = key,
            clips = valid.ToArray(),
            volume = vol,
            minPitch = minP,
            maxPitch = maxP,
            is2D = is2D,
            spatialBlend = is2D ? 0f : 0.65f,
            minDistance = 4f,
            maxDistance = 35f,
            minCooldown = cd
        };
    }

    private static AudioClip LoadClip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

    // =========================================================================
    // 2. PROCEDURAL 16-BIT PCM .WAV PLACEHOLDER GENERATOR (CLEAN & MUSICAL)
    // =========================================================================

    private static void GenerateAllPlaceholderWavs()
    {
        WriteWav("UI_Click.wav", 0.065f, (t, d, rng) =>
            Env(t, d, 0.004f) * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(980f, 1380f, t / d) * t) * 0.45f);

        WriteWav("UI_Confirm.wav", 0.16f, (t, d, rng) =>
        {
            float f = t < 0.06f ? 784f : 1174.66f; // G5 -> D6 chime
            return Env(t, d, 0.006f) * (Mathf.Sin(2f * Mathf.PI * f * t) * 0.40f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.14f);
        });

        WriteWav("UI_Back.wav", 0.12f, (t, d, rng) =>
            Env(t, d, 0.005f) * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(620f, 340f, t / d) * t) * 0.42f);

        WriteWav("UI_Upgrade.wav", 0.42f, (t, d, rng) =>
        {
            float f = t < 0.10f ? 523.25f : (t < 0.20f ? 659.25f : 1046.5f); // C5-E5-C6 sparkle
            return Env(t, d, 0.01f) * (Mathf.Sin(2f * Mathf.PI * f * t) * 0.38f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.18f);
        });

        WriteWav("UI_Error.wav", 0.18f, (t, d, rng) =>
            Env(t, d, 0.006f) * (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 190f * t)) * 0.22f + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.25f));

        WriteWav("Combat_HitImpact.wav", 0.14f, (t, d, rng) =>
        {
            float punch = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(210f, 58f, t / d) * t) * Mathf.Exp(-t * 28f);
            float crack = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 48f) * 0.55f;
            return Mathf.Clamp(punch * 0.78f + crack, -0.95f, 0.95f);
        });

        WriteWav("Combat_HeavyHit.wav", 0.28f, (t, d, rng) =>
        {
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 36f, t / d) * t) * Mathf.Exp(-t * 14f);
            float crunch = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 24f) * 0.65f;
            float ring = Mathf.Sin(2f * Mathf.PI * 640f * t) * Mathf.Exp(-t * 18f) * 0.25f;
            return Mathf.Clamp(sub * 0.85f + crunch + ring, -0.95f, 0.95f);
        });

        WriteWav("Combat_Dodge.wav", 0.19f, (t, d, rng) =>
        {
            float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 180f, t / d) * t) * 0.22f;
            float whoosh = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Sin(Mathf.PI * (t / d)) * 0.45f;
            return Env(t, d, 0.02f) * (sweep + whoosh);
        });

        WriteWav("Combat_LockOn.wav", 0.13f, (t, d, rng) =>
        {
            float f = t < 0.055f ? 920f : 1480f;
            return Env(t, d, 0.004f) * (Mathf.Sin(2f * Mathf.PI * f * t) * 0.42f + Mathf.Sin(6f * Mathf.PI * f * t) * 0.10f);
        });

        WriteWav("Combat_LockSwitch.wav", 0.08f, (t, d, rng) =>
            Env(t, d, 0.003f) * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1100f, 1520f, t / d) * t) * 0.36f);

        WriteWav("Combat_PlayerHurt.wav", 0.24f, (t, d, rng) =>
        {
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(190f, 65f, t / d) * t) * Mathf.Exp(-t * 16f);
            float grit = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 25f) * 0.45f;
            return Mathf.Clamp(thud * 0.72f + grit, -0.95f, 0.95f);
        });

        WriteWav("Combat_PlayerDeath.wav", 0.75f, (t, d, rng) =>
        {
            float drop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 48f, t / d) * t) * Env(t, d, 0.015f);
            float dark = Mathf.Sin(2f * Mathf.PI * 92f * t) * Mathf.Exp(-t * 4f) * 0.35f;
            return Mathf.Clamp(drop * 0.55f + dark, -0.95f, 0.95f);
        });

        WriteWav("Combat_EnemyHurt.wav", 0.11f, (t, d, rng) =>
        {
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(340f, 120f, t / d) * t) * Mathf.Exp(-t * 26f);
            float snap = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 42f) * 0.35f;
            return Mathf.Clamp(tone * 0.55f + snap, -0.95f, 0.95f);
        });

        WriteWav("Combat_BossStun.wav", 0.55f, (t, d, rng) =>
        {
            // Crystal shatter + low boom
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 38f, t / d) * t) * Mathf.Exp(-t * 8f);
            float chime = (Mathf.Sin(2f * Mathf.PI * 1318.5f * t) + Mathf.Sin(2f * Mathf.PI * 1760f * t)) * Mathf.Exp(-t * 9f) * 0.28f;
            float shatter = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 16f) * 0.42f;
            return Mathf.Clamp(boom * 0.65f + chime + shatter, -0.95f, 0.95f);
        });

        WriteWav("Reward_StarDust.wav", 0.14f, (t, d, rng) =>
            Env(t, d, 0.005f) * (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1046f, 1568f, t / d) * t) * 0.35f + Mathf.Sin(4f * Mathf.PI * 1568f * t) * 0.12f));

        WriteWav("Reward_Core.wav", 0.38f, (t, d, rng) =>
        {
            float chord = Mathf.Sin(2f * Mathf.PI * 587.33f * t) + Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1174.66f * t);
            return Env(t, d, 0.01f) * chord * 0.22f;
        });

        WriteWav("Stage_Victory.wav", 1.10f, (t, d, rng) =>
        {
            // Rising Magical Girl victory arpeggio: C5 -> E5 -> G5 -> C6 chord
            float f = t < 0.16f ? 523.25f : (t < 0.32f ? 659.25f : (t < 0.48f ? 783.99f : 1046.50f));
            float main = Mathf.Sin(2f * Mathf.PI * f * t) * 0.36f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.14f;
            float pad = t >= 0.48f ? (Mathf.Sin(2f * Mathf.PI * 523.25f * t) + Mathf.Sin(2f * Mathf.PI * 659.25f * t) + Mathf.Sin(2f * Mathf.PI * 783.99f * t)) * 0.14f : 0f;
            return Env(t, d, 0.015f) * Mathf.Clamp(main + pad, -0.95f, 0.95f);
        });

        WriteWav("Stage_Defeat.wav", 1.05f, (t, d, rng) =>
        {
            // Melancholic descending minor cadence: E4 -> D4 -> C4 -> A3
            float f = t < 0.22f ? 329.63f : (t < 0.44f ? 293.66f : (t < 0.66f ? 261.63f : 220.00f));
            float tone = Mathf.Sin(2f * Mathf.PI * f * t) * 0.38f + Mathf.Sin(2f * Mathf.PI * (f * 0.5f) * t) * 0.18f;
            return Env(t, d, 0.02f) * tone;
        });
    }

    private static float Env(float t, float duration, float attack)
    {
        float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
        float r = Mathf.Clamp01((duration - t) / Mathf.Max(0.02f, duration * 0.55f));
        return a * r;
    }

    private static void WriteWav(string fileName, float durationSec, Func<float, float, System.Random, float> sampleFunc)
    {
        string fullPath = Path.Combine(GeneratedSfxDir, fileName);
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSec));
        var rng = new System.Random(fileName.GetHashCode());

        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            int byteRate = SampleRate * 2;
            int dataSize = sampleCount * 2;

            // RIFF header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            // data chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float val = Mathf.Clamp(sampleFunc(t, durationSec, rng), -1f, 1f);
                bw.Write((short)Mathf.RoundToInt(val * short.MaxValue));
            }
        }
    }

    private static void EnsureFolderExists(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
        string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(assetFolderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
