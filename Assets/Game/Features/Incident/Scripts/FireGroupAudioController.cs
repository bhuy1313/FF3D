using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireGroup))]
public sealed class FireGroupAudioController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioId loopAudioId = AudioId.FireLoop;
    [SerializeField, Range(0f, 1f)] private float minBedVolume = 0.03f;
    [SerializeField, Range(0f, 1f)] private float maxBedVolume = 0.28f;
    [SerializeField, Range(0.5f, 1.5f)] private float minPitch = 0.94f;
    [SerializeField, Range(0.5f, 1.5f)] private float maxPitch = 1.02f;
    [SerializeField, Min(0f)] private float stopFadeDuration = 0.35f;

    private FireGroup fireGroup;
    private AudioSource loopSource;
    private bool isBound;

    public void Initialize(FireGroup targetGroup)
    {
        if (targetGroup == null)
        {
            return;
        }

        if (fireGroup == targetGroup && isBound)
        {
            RefreshLoopState();
            return;
        }

        Unbind();
        fireGroup = targetGroup;
        isBound = true;
        RefreshLoopState();
    }

    private void OnDisable()
    {
        StopLoop();
        Unbind();
    }

    private void Update()
    {
        if (!isBound || fireGroup == null)
        {
            return;
        }

        RefreshLoopState();
    }

    private void RefreshLoopState()
    {
        if (!isBound || fireGroup == null)
        {
            return;
        }

        fireGroup.GetActiveFireMetrics(out int activeFireCount, out float averageIntensity01, out float totalIntensity01);

        if (activeFireCount <= 0)
        {
            StopLoop();
            return;
        }

        EnsureLoopSource();
        if (loopSource == null)
        {
            return;
        }

        float countContribution = Mathf.Clamp01(activeFireCount * 0.08f);
        float intensityContribution = Mathf.Clamp01(totalIntensity01 * 0.05f);
        float volumeScale = Mathf.Clamp01(minBedVolume + (countContribution * maxBedVolume) + intensityContribution);
        float pitch = Mathf.Lerp(minPitch, maxPitch, averageIntensity01);

        AudioService.SetSourceVolumeScale(loopSource, volumeScale);
        AudioService.SetSourcePitch(loopSource, pitch);
    }

    private void EnsureLoopSource()
    {
        if (loopSource != null)
        {
            return;
        }

        loopSource = AudioService.PlayLoop(loopAudioId, transform);
    }

    private void StopLoop()
    {
        if (loopSource == null)
        {
            return;
        }

        AudioService.Stop(loopSource, stopFadeDuration);
        loopSource = null;
    }

    private void Unbind()
    {
        StopLoop();

        fireGroup = null;
        isBound = false;
    }
}
