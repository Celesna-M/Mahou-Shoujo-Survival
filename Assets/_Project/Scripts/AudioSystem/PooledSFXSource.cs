using UnityEngine;

/// <summary>
/// Lightweight pooled AudioSource component managed centrally by AudioManager (Zero-GC, no per-source Update).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PooledSFXSource : MonoBehaviour
{
    private AudioSource source;
    private float remainingLifetime;
    private float baseVolume;
    private bool isActiveInPool;

    public bool IsActiveInPool => isActiveInPool;

    private void Awake()
    {
        EnsureSource();
    }

    public void EnsureSource()
    {
        if (source == null)
        {
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = false;
        source.rolloffMode = AudioRolloffMode.Linear;
    }

    public void Play(
        AudioClip clip,
        Vector3 worldPosition,
        float clipVolume,
        float channelGain,
        float pitch,
        float spatialBlend,
        float minDistance,
        float maxDistance)
    {
        EnsureSource();

        transform.position = worldPosition;
        baseVolume = Mathf.Clamp(clipVolume, 0f, 1.5f);
        source.clip = clip;
        source.volume = baseVolume * channelGain;
        source.pitch = pitch;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = Mathf.Max(0.5f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 1f, maxDistance);

        float absPitch = Mathf.Max(0.15f, Mathf.Abs(pitch));
        remainingLifetime = (clip.length / absPitch) + 0.08f;
        isActiveInPool = true;

        source.Play();
    }

    public void UpdateChannelGain(float channelGain)
    {
        if (source != null && isActiveInPool)
        {
            source.volume = baseVolume * channelGain;
        }
    }

    /// <summary>
    /// Advances lifetime using unscaled delta time. Returns true when finished and ready to return to pool.
    /// </summary>
    public bool TickAndCheckFinished(float unscaledDt)
    {
        if (!isActiveInPool) return true;

        remainingLifetime -= unscaledDt;
        if (remainingLifetime <= 0f || (source != null && !source.isPlaying))
        {
            return true;
        }
        return false;
    }

    public void StopAndReset()
    {
        isActiveInPool = false;
        remainingLifetime = 0f;
        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }
    }
}
