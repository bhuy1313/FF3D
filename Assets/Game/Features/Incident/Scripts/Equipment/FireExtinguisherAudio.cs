using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireExtinguisher))]
public sealed class FireExtinguisherAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireExtinguisher extinguisher;
    [SerializeField] private Transform audioAnchor;
    [SerializeField] private ParticleSystem sprayParticles;

    [Header("Audio")]
    [SerializeField] private AudioId startAudioId = AudioId.ExtinguisherSprayStart;
    [SerializeField] private AudioId loopAudioId = AudioId.ExtinguisherSprayLoop;
    [SerializeField] private AudioId endAudioId = AudioId.ExtinguisherSprayEnd;
    [SerializeField, Min(0f)] private float stopFadeDuration = 0.08f;

    private AudioSource loopSource;
    private bool wasSpraying;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        SyncState(forceRefresh: true);
    }

    private void Update()
    {
        SyncState(forceRefresh: false);
    }

    private void OnDisable()
    {
        if (wasSpraying)
        {
            StopAudio(playEndShot: true);
            wasSpraying = false;
            return;
        }

        StopAudio(playEndShot: false);
    }

    private void OnDestroy()
    {
        StopAudio(playEndShot: false);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        stopFadeDuration = Mathf.Max(0f, stopFadeDuration);
    }

    private void SyncState(bool forceRefresh)
    {
        if (extinguisher == null)
        {
            return;
        }

        bool isSpraying = extinguisher.IsSpraying;
        if (!forceRefresh && wasSpraying == isSpraying)
        {
            return;
        }

        if (isSpraying)
        {
            StartAudio();
        }
        else
        {
            StopAudio(playEndShot: wasSpraying);
        }

        wasSpraying = isSpraying;
    }

    private void StartAudio()
    {
        Transform anchor = ResolveAudioAnchor();
        if (anchor == null)
        {
            return;
        }

        if (loopSource != null)
        {
            return;
        }

        if (startAudioId != AudioId.None)
        {
            AudioService.PlayAtPoint(startAudioId, anchor.position);
        }

        if (loopAudioId != AudioId.None)
        {
            loopSource = AudioService.PlayLoop(loopAudioId, anchor);
        }
    }

    private void StopAudio(bool playEndShot)
    {
        Transform anchor = ResolveAudioAnchor();

        if (loopSource != null)
        {
            AudioService.Stop(loopSource, stopFadeDuration);
            loopSource = null;
        }

        if (playEndShot && endAudioId != AudioId.None && anchor != null)
        {
            AudioService.PlayAtPoint(endAudioId, anchor.position);
        }
    }

    private void AutoAssignReferences()
    {
        extinguisher ??= GetComponent<FireExtinguisher>();
        sprayParticles ??= GetComponentInChildren<ParticleSystem>(true);
        if (audioAnchor == null && sprayParticles != null)
        {
            audioAnchor = sprayParticles.transform;
        }
    }

    private Transform ResolveAudioAnchor()
    {
        if (audioAnchor != null)
        {
            return audioAnchor;
        }

        if (sprayParticles != null)
        {
            return sprayParticles.transform;
        }

        return transform;
    }
}
