using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireAudioEmitter : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioId sound = AudioId.FireLoop;

    private MonoBehaviour fireSourceBehaviour;
    private IFireTarget fireTarget;
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
        Initialize(ResolveFireSourceBehaviour());
    }

    private void OnEnable()
    {
        if (!isBound)
        {
            Initialize(ResolveFireSourceBehaviour());
            return;
        }

        cachedBurningState = fireTarget != null && fireTarget.IsBurning;
        RefreshLoopState();
    }

    public void Initialize(MonoBehaviour targetFireSource)
    {
        if (targetFireSource == null || targetFireSource is not IFireTarget targetFire)
        {
            return;
        }

        if (fireSourceBehaviour == targetFireSource && isBound)
        {
            cachedBurningState = fireTarget != null && fireTarget.IsBurning;
            RefreshLoopState();
            return;
        }

        Unbind();
        fireSourceBehaviour = targetFireSource;
        fireTarget = targetFire;
        cachedBurningState = fireTarget.IsBurning;
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
        if (!isBound || fireTarget == null || (!cachedBurningState && loopSource == null))
        {
            return;
        }

        if (cachedBurningState != fireTarget.IsBurning)
        {
            cachedBurningState = fireTarget.IsBurning;
        }

        RefreshLoopState();
    }

    private void RefreshLoopState()
    {
        if (!isBound || fireTarget == null)
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

        float intensity01 = Mathf.Clamp01(fireTarget.GetWorldRadius());
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

        if (!isBound || fireSourceBehaviour == null)
        {
            fireSourceBehaviour = null;
            fireTarget = null;
            isBound = false;
            return;
        }

        fireSourceBehaviour = null;
        fireTarget = null;
        isBound = false;
    }

    private MonoBehaviour ResolveFireSourceBehaviour()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IFireTarget)
            {
                return behaviours[i];
            }
        }

        return null;
    }
}
