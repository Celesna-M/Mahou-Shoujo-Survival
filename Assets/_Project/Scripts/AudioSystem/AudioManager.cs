using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

/// <summary>
/// AudioSystem: Persistent DontDestroyOnLoad AudioManager (Zero-GC Hot Path Edition)
/// - Dual-Deck BGM Crossfader with sample-accurate JRPG Intro + Loop scheduling (AudioSettings.dspTime).
/// - Automatic Scene BGM routing + Dynamic Boss Battle BGM crossfade + Smooth Pause & End-Screen volume ducking.
/// - Zero-GC ObjectPool<PooledSFXSource> for 3D spatial combat SFX + dedicated 2D UI AudioSource channel.
/// - Random clip variation (no back-to-back repeat), pitch jitter, and per-key cooldown spam guard.
/// - Live Master / BGM / SFX volume sync with GameProgressionManager.OnProgressionChanged.
/// </summary>
[DefaultExecutionOrder(-180)]
public class AudioManager : MonoBehaviour
{
    private const string DefaultLibraryResourcePath = "DefaultAudioLibrary";

    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<AudioManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("[AudioManager]");
                    instance = go.AddComponent<AudioManager>();
                }
            }
            return instance;
        }
    }

    [Header("1. Audio Library (Auto-loaded from Resources/DefaultAudioLibrary if empty)")]
    [Tooltip("Central ScriptableObject defining Scene BGM, Boss BGM, and all keyed SFX cues.")]
    [SerializeField] private AudioLibrary_SO audioLibrary;

    [Header("2. Dynamic BGM Crossfade & Ducking")]
    [Tooltip("Crossfade duration in seconds when switching scenes or entering Boss battle music.")]
    [SerializeField] private float bgmCrossfadeDuration = 1.0f;

    [Tooltip("BGM volume multiplier while the Pause / Settings panel is open.")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseDuckMultiplier = 0.32f;

    [Tooltip("BGM volume multiplier during Victory / Defeat summary screens.")]
    [Range(0f, 1f)]
    [SerializeField] private float endScreenDuckMultiplier = 0.22f;

    [Tooltip("Smoothing speed for BGM volume ducking transitions.")]
    [SerializeField] private float duckSmoothSpeed = 6f;

    [Header("3. Pooled SFX Settings")]
    [Tooltip("Initial pre-warmed pooled 3D AudioSources.")]
    [SerializeField] private int sfxPoolDefaultCapacity = 20;

    [Tooltip("Maximum simultaneous pooled 3D AudioSources.")]
    [SerializeField] private int sfxPoolMaxSize = 48;

    private sealed class BGMDeck
    {
        public AudioSource introSource;
        public AudioSource loopSource;
        public string trackId;
        public float trackBaseVolume = 0.72f;
        public float fadeWeight = 0f;

        public bool IsPlaying => (introSource != null && introSource.isPlaying) || (loopSource != null && loopSource.isPlaying);

        public void ApplyVolume(float globalBgmGain)
        {
            float v = Mathf.Clamp01(trackBaseVolume * fadeWeight * globalBgmGain);
            if (introSource != null) introSource.volume = v;
            if (loopSource != null) loopSource.volume = v;
        }

        public void Stop()
        {
            if (introSource != null)
            {
                introSource.Stop();
                introSource.clip = null;
            }
            if (loopSource != null)
            {
                loopSource.Stop();
                loopSource.clip = null;
            }
            trackId = null;
            fadeWeight = 0f;
        }
    }

    private BGMDeck deckA;
    private BGMDeck deckB;
    private BGMDeck activeDeck;
    private BGMDeck fadingOutDeck;
    private bool isCrossfading;
    private float crossfadeElapsed;
    private float activeCrossfadeDuration = 1.0f;

    private float currentBgmVolume = 0.75f;
    private float currentSfxVolume = 1.0f;
    private float currentDuckFactor = 1.0f;
    private bool isEndScreenDucked;
    private bool isBossMusicActive;
    private string currentSceneName;

    private AudioSource uiAudioSource;
    private Transform sfxPoolRoot;
    private ObjectPool<PooledSFXSource> sfxPool;
    private readonly List<PooledSFXSource> activePooledSources = new List<PooledSFXSource>(32);

    private readonly Dictionary<string, BGMTrackEntry> bgmLookup = new Dictionary<string, BGMTrackEntry>(16, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SceneBGMConfig> sceneBgmLookup = new Dictionary<string, SceneBGMConfig>(8, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SFXCueEntry> sfxLookup = new Dictionary<string, SFXCueEntry>(32, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> lastPlayTimeByKey = new Dictionary<string, float>(32, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastClipIndexByKey = new Dictionary<string, int>(32, StringComparer.OrdinalIgnoreCase);

    private GameProgressionManager subscribedProgression;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        // Ensures AudioManager boots cleanly regardless of which scene Play Mode starts in.
        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent != null)
            transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        if (audioLibrary == null)
        {
            audioLibrary = Resources.Load<AudioLibrary_SO>(DefaultLibraryResourcePath);
        }

        BuildLookupTables();
        InitializeAudioChannels();
        InitializeSfxPool();

        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeToProgression();
        SyncVolumesFromProgression();

        HandleSceneAudio(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (subscribedProgression != null)
        {
            subscribedProgression.OnProgressionChanged -= SyncVolumesFromProgression;
            subscribedProgression = null;
        }

        instance = null;
    }

    private void Update()
    {
        float unscaledDt = Time.unscaledDeltaTime;

        UpdatePauseAndEndScreenDucking(unscaledDt);
        UpdateBgmCrossfade(unscaledDt);
        TickActivePooledSfx(unscaledDt);
    }

    // =========================================================================
    // 1. INITIALIZATION & LIBRARY INDEXING
    // =========================================================================

    public void SetAudioLibrary(AudioLibrary_SO newLibrary)
    {
        audioLibrary = newLibrary;
        BuildLookupTables();
    }

    private void BuildLookupTables()
    {
        bgmLookup.Clear();
        sceneBgmLookup.Clear();
        sfxLookup.Clear();

        if (audioLibrary == null) return;

        if (audioLibrary.bgmTracks != null)
        {
            for (int i = 0; i < audioLibrary.bgmTracks.Count; i++)
            {
                var track = audioLibrary.bgmTracks[i];
                if (track != null && !string.IsNullOrEmpty(track.trackId))
                {
                    bgmLookup[track.trackId.Trim()] = track;
                }
            }
        }

        if (audioLibrary.sceneMusicConfigs != null)
        {
            for (int i = 0; i < audioLibrary.sceneMusicConfigs.Count; i++)
            {
                var cfg = audioLibrary.sceneMusicConfigs[i];
                if (cfg != null && !string.IsNullOrEmpty(cfg.sceneName))
                {
                    sceneBgmLookup[cfg.sceneName.Trim()] = cfg;
                }
            }
        }

        if (audioLibrary.sfxCues != null)
        {
            for (int i = 0; i < audioLibrary.sfxCues.Count; i++)
            {
                var cue = audioLibrary.sfxCues[i];
                if (cue != null && !string.IsNullOrEmpty(cue.key))
                {
                    sfxLookup[cue.key.Trim()] = cue;
                }
            }
        }
    }

    private void InitializeAudioChannels()
    {
        deckA = CreateDeck("BGM_DeckA");
        deckB = CreateDeck("BGM_DeckB");
        activeDeck = deckA;
        fadingOutDeck = deckB;

        GameObject uiGo = new GameObject("UI_2D_AudioSource");
        uiGo.transform.SetParent(transform, false);
        uiAudioSource = uiGo.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;
    }

    private BGMDeck CreateDeck(string deckName)
    {
        GameObject deckGo = new GameObject(deckName);
        deckGo.transform.SetParent(transform, false);

        AudioSource intro = deckGo.AddComponent<AudioSource>();
        intro.playOnAwake = false;
        intro.loop = false;
        intro.spatialBlend = 0f;
        intro.priority = 0;

        AudioSource loop = deckGo.AddComponent<AudioSource>();
        loop.playOnAwake = false;
        loop.loop = true;
        loop.spatialBlend = 0f;
        loop.priority = 0;

        return new BGMDeck
        {
            introSource = intro,
            loopSource = loop
        };
    }

    private void InitializeSfxPool()
    {
        GameObject rootGo = new GameObject("Pooled_SFX_Root");
        rootGo.transform.SetParent(transform, false);
        sfxPoolRoot = rootGo.transform;

        sfxPool = new ObjectPool<PooledSFXSource>(
            createFunc: () =>
            {
                GameObject go = new GameObject("PooledSFX");
                go.transform.SetParent(sfxPoolRoot, false);
                var pooled = go.AddComponent<PooledSFXSource>();
                pooled.EnsureSource();
                go.SetActive(false);
                return pooled;
            },
            actionOnGet: src =>
            {
                if (src != null) src.gameObject.SetActive(true);
            },
            actionOnRelease: src =>
            {
                if (src != null)
                {
                    src.StopAndReset();
                    src.gameObject.SetActive(false);
                }
            },
            actionOnDestroy: src =>
            {
                if (src != null) Destroy(src.gameObject);
            },
            collectionCheck: false,
            defaultCapacity: Mathf.Max(8, sfxPoolDefaultCapacity),
            maxSize: Mathf.Max(16, sfxPoolMaxSize));
    }

    // =========================================================================
    // 2. VOLUME SYNC (MASTER / BGM / SFX)
    // =========================================================================

    private void SubscribeToProgression()
    {
        var prog = GameProgressionManager.Instance;
        if (prog == null || prog == subscribedProgression) return;

        if (subscribedProgression != null)
            subscribedProgression.OnProgressionChanged -= SyncVolumesFromProgression;

        subscribedProgression = prog;
        subscribedProgression.OnProgressionChanged += SyncVolumesFromProgression;
    }

    private void SyncVolumesFromProgression()
    {
        var prog = subscribedProgression != null ? subscribedProgression : GameProgressionManager.Instance;
        if (prog != null)
        {
            AudioListener.volume = prog.MasterVolume;
            currentBgmVolume = prog.BgmVolume;
            currentSfxVolume = prog.SfxVolume;
        }

        ApplyCurrentBgmGains();

        for (int i = 0; i < activePooledSources.Count; i++)
        {
            if (activePooledSources[i] != null)
                activePooledSources[i].UpdateChannelGain(currentSfxVolume);
        }
    }

    // =========================================================================
    // 3. DYNAMIC BGM (SCENE AUTO-MUSIC, INTRO+LOOP, BOSS CROSSFADE, DUCKING)
    // =========================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        SubscribeToProgression();
        SyncVolumesFromProgression();
        HandleSceneAudio(scene.name);
    }

    private void HandleSceneAudio(string sceneName)
    {
        currentSceneName = sceneName;
        isBossMusicActive = false;
        isEndScreenDucked = false;

        if (string.IsNullOrEmpty(sceneName)) return;

        if (sceneBgmLookup.TryGetValue(sceneName.Trim(), out SceneBGMConfig cfg) && !string.IsNullOrEmpty(cfg.defaultTrackId))
        {
            PlayBGM(cfg.defaultTrackId, bgmCrossfadeDuration);
        }
    }

    public static void EnterBossMusic(string overrideTrackId = null)
    {
        if (Instance != null)
            Instance.PlayBossMusicInternal(overrideTrackId);
    }

    public static void ExitBossMusic()
    {
        if (Instance != null)
            Instance.ExitBossMusicInternal();
    }

    public static void PlayEndScreenStinger(bool isVictory)
    {
        if (Instance == null) return;
        Instance.isEndScreenDucked = true;
        Instance.PlaySFXInternal(isVictory ? AudioKeys.Stage_Victory : AudioKeys.Stage_Defeat, Vector3.zero, 1f, force2D: true);
    }

    private void PlayBossMusicInternal(string overrideTrackId)
    {
        if (isBossMusicActive && string.IsNullOrEmpty(overrideTrackId)) return;

        string trackId = overrideTrackId;
        if (string.IsNullOrEmpty(trackId) && !string.IsNullOrEmpty(currentSceneName) &&
            sceneBgmLookup.TryGetValue(currentSceneName.Trim(), out SceneBGMConfig cfg) &&
            !string.IsNullOrEmpty(cfg.bossTrackId))
        {
            trackId = cfg.bossTrackId;
        }

        if (string.IsNullOrEmpty(trackId))
            trackId = AudioKeys.BGM_Boss_01;

        isBossMusicActive = true;
        PlayBGM(trackId, 0.85f);
    }

    private void ExitBossMusicInternal()
    {
        if (!isBossMusicActive) return;
        isBossMusicActive = false;

        if (isEndScreenDucked) return;

        if (!string.IsNullOrEmpty(currentSceneName) &&
            sceneBgmLookup.TryGetValue(currentSceneName.Trim(), out SceneBGMConfig cfg) &&
            !string.IsNullOrEmpty(cfg.defaultTrackId))
        {
            PlayBGM(cfg.defaultTrackId, 1.2f);
        }
    }

    public void PlayBGM(string trackId, float fadeDuration = -1f)
    {
        if (string.IsNullOrEmpty(trackId)) return;
        if (!bgmLookup.TryGetValue(trackId.Trim(), out BGMTrackEntry track)) return;
        if (track.loopClip == null && track.introClip == null) return;

        // Already playing this exact track on the active deck
        if (activeDeck != null && activeDeck.IsPlaying &&
            string.Equals(activeDeck.trackId, track.trackId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Swap decks: current active fades out, standby becomes new active
        if (fadingOutDeck != null)
            fadingOutDeck.Stop();

        BGMDeck oldActive = activeDeck;
        activeDeck = (oldActive == deckA) ? deckB : deckA;
        fadingOutDeck = oldActive;

        StartTrackOnDeck(activeDeck, track);

        activeCrossfadeDuration = fadeDuration >= 0f ? fadeDuration : bgmCrossfadeDuration;
        if (activeCrossfadeDuration <= 0.02f || fadingOutDeck == null || !fadingOutDeck.IsPlaying)
        {
            if (fadingOutDeck != null) fadingOutDeck.Stop();
            activeDeck.fadeWeight = 1f;
            isCrossfading = false;
        }
        else
        {
            activeDeck.fadeWeight = 0f;
            crossfadeElapsed = 0f;
            isCrossfading = true;
        }

        ApplyCurrentBgmGains();
    }

    private static void StartTrackOnDeck(BGMDeck deck, BGMTrackEntry track)
    {
        deck.Stop();
        deck.trackId = track.trackId;
        deck.trackBaseVolume = track.volume;

        if (track.introClip != null && track.loopClip != null)
        {
            // Sample-accurate JRPG Intro -> Seamless Loop scheduling
            double startDsp = AudioSettings.dspTime + 0.05;
            double introDuration = (double)track.introClip.samples / track.introClip.frequency;

            deck.introSource.clip = track.introClip;
            deck.introSource.loop = false;
            deck.introSource.PlayScheduled(startDsp);

            deck.loopSource.clip = track.loopClip;
            deck.loopSource.loop = true;
            deck.loopSource.PlayScheduled(startDsp + introDuration);
        }
        else
        {
            AudioClip singleClip = track.loopClip != null ? track.loopClip : track.introClip;
            deck.loopSource.clip = singleClip;
            deck.loopSource.loop = true;
            deck.loopSource.Play();
        }
    }

    private void UpdateBgmCrossfade(float unscaledDt)
    {
        if (!isCrossfading) return;

        crossfadeElapsed += unscaledDt;
        float t = Mathf.Clamp01(crossfadeElapsed / Mathf.Max(0.05f, activeCrossfadeDuration));

        if (activeDeck != null) activeDeck.fadeWeight = t;
        if (fadingOutDeck != null) fadingOutDeck.fadeWeight = 1f - t;

        ApplyCurrentBgmGains();

        if (t >= 1f)
        {
            isCrossfading = false;
            if (fadingOutDeck != null) fadingOutDeck.Stop();
        }
    }

    private void UpdatePauseAndEndScreenDucking(float unscaledDt)
    {
        bool isPaused = BattleHUD.Instance != null && BattleHUD.Instance.IsSettingsOpen;
        float targetDuck = 1.0f;

        if (isEndScreenDucked)
            targetDuck = endScreenDuckMultiplier;
        else if (isPaused)
            targetDuck = pauseDuckMultiplier;

        if (Mathf.Abs(currentDuckFactor - targetDuck) > 0.002f)
        {
            float lerpT = 1f - Mathf.Exp(-duckSmoothSpeed * unscaledDt);
            currentDuckFactor = Mathf.Lerp(currentDuckFactor, targetDuck, lerpT);
            ApplyCurrentBgmGains();
        }
    }

    private void ApplyCurrentBgmGains()
    {
        float globalBgmGain = currentBgmVolume * currentDuckFactor;
        if (deckA != null) deckA.ApplyVolume(globalBgmGain);
        if (deckB != null) deckB.ApplyVolume(globalBgmGain);
    }

    // =========================================================================
    // 4. POOLED 3D & 2D SFX ENGINE (ZERO-GC)
    // =========================================================================

    public static void PlayUI(string key, float volumeScale = 1f)
    {
        if (Instance != null)
            Instance.PlaySFXInternal(key, Vector3.zero, volumeScale, force2D: true);
    }

    public static void PlaySFX(string key, Vector3 worldPos, float volumeScale = 1f)
    {
        if (Instance != null)
            Instance.PlaySFXInternal(key, worldPos, volumeScale, force2D: false);
    }

    public static void PlayClip(AudioClip clip, Vector3 worldPos, float volumeScale = 1f, bool is2D = false, float pitchJitter = 0.05f)
    {
        if (clip == null || Instance == null) return;
        Instance.PlayDirectClipInternal(clip, worldPos, volumeScale, is2D, pitchJitter);
    }

    private void PlaySFXInternal(string key, Vector3 worldPos, float volumeScale, bool force2D)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!sfxLookup.TryGetValue(key.Trim(), out SFXCueEntry cue)) return;
        if (cue.clips == null || cue.clips.Length == 0) return;

        // Per-key cooldown spam guard
        float now = Time.unscaledTime;
        if (cue.minCooldown > 0f && lastPlayTimeByKey.TryGetValue(cue.key, out float lastTime))
        {
            if (now - lastTime < cue.minCooldown) return;
        }
        lastPlayTimeByKey[cue.key] = now;

        AudioClip clip = PickRandomClipAvoidingRepeat(cue.key, cue.clips);
        if (clip == null) return;

        float vol = cue.volume * Mathf.Max(0f, volumeScale);
        float minP = Mathf.Min(cue.minPitch, cue.maxPitch);
        float maxP = Mathf.Max(cue.minPitch, cue.maxPitch);
        float pitch = (maxP - minP > 0.001f) ? UnityEngine.Random.Range(minP, maxP) : minP;

        if (force2D || cue.is2D)
        {
            if (uiAudioSource != null)
            {
                uiAudioSource.pitch = pitch;
                uiAudioSource.PlayOneShot(clip, vol * currentSfxVolume);
            }
            return;
        }

        SpawnPooledSfx(clip, worldPos, vol, pitch, cue.spatialBlend, cue.minDistance, cue.maxDistance);
    }

    private void PlayDirectClipInternal(AudioClip clip, Vector3 worldPos, float volumeScale, bool is2D, float pitchJitter)
    {
        float pitch = pitchJitter > 0.001f
            ? UnityEngine.Random.Range(1f - pitchJitter, 1f + pitchJitter)
            : 1f;

        if (is2D)
        {
            if (uiAudioSource != null)
            {
                uiAudioSource.pitch = pitch;
                uiAudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1.5f) * currentSfxVolume);
            }
            return;
        }

        SpawnPooledSfx(clip, worldPos, Mathf.Clamp(volumeScale, 0f, 1.5f), pitch, 0.65f, 4f, 35f);
    }

    private void SpawnPooledSfx(
        AudioClip clip,
        Vector3 worldPos,
        float clipVolume,
        float pitch,
        float spatialBlend,
        float minDist,
        float maxDist)
    {
        if (sfxPool == null) return;

        PooledSFXSource pooled = sfxPool.Get();
        if (pooled == null) return;

        pooled.Play(clip, worldPos, clipVolume, currentSfxVolume, pitch, spatialBlend, minDist, maxDist);
        activePooledSources.Add(pooled);
    }

    private void TickActivePooledSfx(float unscaledDt)
    {
        for (int i = activePooledSources.Count - 1; i >= 0; i--)
        {
            PooledSFXSource src = activePooledSources[i];
            if (src == null || src.TickAndCheckFinished(unscaledDt))
            {
                activePooledSources.RemoveAt(i);
                if (src != null)
                {
                    sfxPool.Release(src);
                }
            }
        }
    }

    private AudioClip PickRandomClipAvoidingRepeat(string key, AudioClip[] clips)
    {
        if (clips.Length == 1) return clips[0];

        int lastIdx = lastClipIndexByKey.TryGetValue(key, out int prev) ? prev : -1;
        int chosen = UnityEngine.Random.Range(0, clips.Length);
        if (chosen == lastIdx)
        {
            chosen = (chosen + 1) % clips.Length;
        }
        lastClipIndexByKey[key] = chosen;
        return clips[chosen];
    }
}
