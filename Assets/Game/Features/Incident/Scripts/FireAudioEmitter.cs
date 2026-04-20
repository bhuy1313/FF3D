using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Fire))]
public sealed class FireAudioEmitter : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioId sound = AudioId.FireLoop;

    private Fire fire;
    private AudioSource loopSource;
    private bool isBound;
    private bool cachedBurningState;
    private const float MinLoopVolume = 0.04f;
    private const float MaxLoopVolume = 0.16f;
    private const float MinPitch = 0.95f;
    private const float MaxPitch = 1.08f;
    private const float StopFadeDuration = 0.2f;

    private void Awake()
    {
        Initialize(GetComponent<Fire>());
    }

    private void OnEnable()
    {
        if (!isBound)
        {
            Initialize(GetComponent<Fire>());
            return;
        }

        cachedBurningState = fire != null && fire.IsBurning;
        RefreshLoopState();
    }

    public void Initialize(Fire targetFire)
    {
        if (targetFire == null)
        {
            return;
        }

        if (fire == targetFire && isBound)
        {
            cachedBurningState = fire.IsBurning;
            RefreshLoopState();
            return;
        }

        Unbind();
        fire = targetFire;
        cachedBurningState = fire.IsBurning;

        fire.BurningStateChanged += HandleBurningStateChanged;
        fire.Ignited += HandleIgnited;
        fire.Extinguished += HandleExtinguished;
        isBound = true;

        RefreshLoopState();
    }

    public void HandleFireDisabled()
    {
        StopLoop();
        Unbind();
    }

    private void OnDisable()
    {
        HandleFireDisabled();
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
        float volumeScale = Mathf.Lerp(MinLoopVolume, MaxLoopVolume, intensity01);
        float pitch = Mathf.Lerp(MinPitch, MaxPitch, intensity01);

        AudioService.SetSourceVolumeScale(loopSource, volumeScale);
        AudioService.SetSourcePitch(loopSource, pitch);
    }

    private void EnsureLoopSource()
    {
        if (loopSource != null)
        {
            return;
        }

        loopSource = AudioService.PlayLoop(sound, transform);
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
