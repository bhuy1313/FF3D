using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireExtinguisher))]
public sealed class FireExtinguisherAudioController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioId startSound = AudioId.ExtinguisherSprayStart;
    [SerializeField] private AudioId loopSound = AudioId.ExtinguisherSprayLoop;
    [SerializeField] private AudioId endSound = AudioId.ExtinguisherSprayEnd;

    private FireExtinguisher extinguisher;
    private AudioSource loopSource;
    private bool isBound;
    private bool wasSpraying;
    private const float StopFadeDuration = 0.1f;

    private void Awake()
    {
        Initialize(GetComponent<FireExtinguisher>());
    }

    private void OnEnable()
    {
        if (!isBound)
        {
            Initialize(GetComponent<FireExtinguisher>());
            return;
        }

        wasSpraying = extinguisher != null && extinguisher.IsSpraying;
        RefreshState(false);
    }

    public void Initialize(FireExtinguisher targetExtinguisher)
    {
        if (targetExtinguisher == null)
        {
            return;
        }

        extinguisher = targetExtinguisher;
        isBound = true;
        wasSpraying = extinguisher.IsSpraying;
        RefreshState(false);
    }

    public void RefreshState(bool playTransitionAudio = true)
    {
        if (!isBound || extinguisher == null)
        {
            return;
        }

        bool isSpraying = extinguisher.IsSpraying;
        if (isSpraying == wasSpraying)
        {
            if (isSpraying)
            {
                EnsureLoop();
            }
            else
            {
                StopLoop();
            }

            return;
        }

        if (isSpraying)
        {
            if (playTransitionAudio)
            {
                AudioService.PlayAtPoint(startSound, transform.position);
            }

            EnsureLoop();
        }
        else
        {
            StopLoop();
            if (playTransitionAudio)
            {
                AudioService.PlayAtPoint(endSound, transform.position);
            }
        }

        wasSpraying = isSpraying;
    }

    private void Update()
    {
        RefreshState();
    }

    private void OnDisable()
    {
        StopLoop();
        wasSpraying = false;
    }

    private void EnsureLoop()
    {
        if (loopSource != null)
        {
            return;
        }

        loopSource = AudioService.PlayLoop(loopSound, transform);
    }

    private void StopLoop()
    {
        if (loopSource == null)
        {
            return;
        }

        AudioService.Stop(loopSource, StopFadeDuration);
        loopSource = null;
    }
}
