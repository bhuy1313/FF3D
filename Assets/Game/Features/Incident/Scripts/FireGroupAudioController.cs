using System.Collections.Generic;
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
    private readonly List<Fire> fireBuffer = new List<Fire>();

    private void Awake()
    {
        fireGroup = GetComponent<FireGroup>();
    }

    private void OnEnable()
    {
        BindIfNeeded();
        RefreshLoopState();
    }

    private void OnDisable()
    {
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

    private void BindIfNeeded()
    {
        if (isBound)
        {
            return;
        }

        if (fireGroup == null)
        {
            fireGroup = GetComponent<FireGroup>();
        }

        isBound = fireGroup != null;
    }

    private void RefreshLoopState()
    {
        if (!isBound || fireGroup == null)
        {
            return;
        }

        int activeFireCount = 0;
        float totalIntensity = 0f;
        RefreshFireBuffer();
        for (int i = 0; i < fireBuffer.Count; i++)
        {
            Fire fire = fireBuffer[i];
            if (fire == null || !fire.IsBurning)
            {
                continue;
            }

            activeFireCount++;
            totalIntensity += fire.NormalizedHp;
        }

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

        float averageIntensity01 = Mathf.Clamp01(totalIntensity / activeFireCount);
        float countContribution = Mathf.Clamp01(activeFireCount * 0.08f);
        float intensityContribution = Mathf.Clamp01(totalIntensity * 0.05f);
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

        fireBuffer.Clear();
        fireGroup = null;
        isBound = false;
    }

    private void RefreshFireBuffer()
    {
        fireBuffer.Clear();
        if (fireGroup == null)
        {
            return;
        }

        Fire[] fires = fireGroup.GetComponentsInChildren<Fire>(true);
        for (int i = 0; i < fires.Length; i++)
        {
            Fire fire = fires[i];
            if (fire != null && !fireBuffer.Contains(fire))
            {
                fireBuffer.Add(fire);
            }
        }
    }
}
