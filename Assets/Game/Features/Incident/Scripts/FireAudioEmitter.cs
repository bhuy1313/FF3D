using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Fire))]
public sealed class FireAudioEmitter : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioId loopAudioId = AudioId.FireLoop;
    [SerializeField, Range(0f, 1f)] private float minLoopVolume = 0.04f;
    [SerializeField, Range(0f, 1f)] private float maxLoopVolume = 0.16f;
    [SerializeField, Range(0.5f, 1.5f)] private float minPitch = 0.95f;
    [SerializeField, Range(0.5f, 1.5f)] private float maxPitch = 1.08f;
    [SerializeField, Min(0f)] private float stopFadeDuration = 0.2f;

    private Fire fire;
    private AudioSource loopSource;
    private bool isBound;
    private bool cachedBurningState;

    private void Awake()
    {
        fire = GetComponent<Fire>();
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
        if (!isBound || fire == null || (!cachedBurningState && loopSource == null))
        {
            return;
        }

        if (cachedBurningState != fire.IsBurning)
        {
            cachedBurningState = fire.IsBurning;
        }

        RefreshLoopState();
    }

    private void BindIfNeeded()
    {
        if (isBound)
        {
            return;
        }

        if (fire == null)
        {
            fire = GetComponent<Fire>();
        }

        if (fire == null)
        {
            return;
        }

        cachedBurningState = fire.IsBurning;
        fire.BurningStateChanged += HandleBurningStateChanged;
        fire.Ignited += HandleIgnited;
        fire.Extinguished += HandleExtinguished;
        isBound = true;
    }

    private void HandleIgnited()
    {
        cachedBurningState = true;
        RefreshLoopState();
    }

    private void HandleExtinguished()
    {
        cachedBurningState = false;
        RefreshLoopState();
    }

    private void HandleBurningStateChanged(bool isBurning)
    {
        cachedBurningState = isBurning;
        RefreshLoopState();
    }

    private void RefreshLoopState()
    {
        if (!isBound || fire == null)
        {
            return;
        }

        if (!cachedBurningState)
        {
            StopLoop();
            return;
        }

        EnsureLoopSource();
        if (loopSource == null)
        {
            return;
        }

        float intensity01 = fire.NormalizedHp;
        float volumeScale = Mathf.Lerp(minLoopVolume, maxLoopVolume, intensity01);
        float pitch = Mathf.Lerp(minPitch, maxPitch, intensity01);

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

        if (!isBound || fire == null)
        {
            fire = null;
            isBound = false;
            return;
        }

        fire.BurningStateChanged -= HandleBurningStateChanged;
        fire.Ignited -= HandleIgnited;
        fire.Extinguished -= HandleExtinguished;
        fire = null;
        isBound = false;
    }
}
