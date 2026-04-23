using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    private const string SettingsResourcePath = "GameAudioSettings";
    private const string CatalogResourcePath = "GameAudioCatalog";
    private const float MinDecibel = -80f;

    private static AudioService instance;
    private static bool isCreating;
    private static bool isShuttingDown;

    [SerializeField]
    private GameAudioSettingsAsset settingsAsset;

    [SerializeField]
    private AudioCatalog fallbackCatalog;

    private readonly List<AudioSource> oneShotPool = new List<AudioSource>();
    private readonly Dictionary<AudioSource, SourceState> sourceStates =
        new Dictionary<AudioSource, SourceState>();
    private readonly List<AudioSource> managedLoopSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, float> externalSourceBaseVolumes =
        new Dictionary<AudioSource, float>();

    private Transform oneShotRoot;
    private Transform loopRoot;
    private Transform musicRoot;
    private AudioSource musicPrimarySource;
    private AudioSource musicSecondarySource;
    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;
    private Coroutine musicFadeCoroutine;

    private sealed class SourceState
    {
        public AudioBus Bus;
        public float BaseVolume = 1f;
        public float FadeMultiplier = 1f;
        public bool IsPooled;
        public bool IsMusic;
        public int PlaybackVersion;
    }

    public static bool IsReady => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isCreating = false;
        isShuttingDown = false;
    }

    public static void EnsureCreated()
    {
        if (instance != null || isCreating || isShuttingDown)
        {
            return;
        }

        instance = FindAnyObjectByType<AudioService>(FindObjectsInactive.Include);
        if (instance != null)
        {
            return;
        }

        isCreating = true;
        try
        {
            GameObject hostObject = AudioManager.EnsureHostObject();
            if (hostObject != null)
            {
                instance = hostObject.GetComponent<AudioService>();
                if (instance == null)
                {
                    instance = hostObject.AddComponent<AudioService>();
                }
            }
        }
        finally
        {
            isCreating = false;
        }
    }

    public static void ApplySavedVolumes()
    {
        EnsureCreated();
        if (instance != null)
        {
            instance.ApplySavedVolumesInternal();
        }
    }

    public static void SetBusVolume(AudioBus bus, float volume, bool save = true)
    {
        EnsureCreated();
        if (instance != null)
        {
            instance.SetBusVolumeInternal(bus, volume, save);
        }
    }

    public static AudioSource Play(AudioId id)
    {
        EnsureCreated();
        return instance != null ? instance.PlayByIdInternal(id, false, null, null) : null;
    }

    public static AudioSource PlayAtPoint(AudioId id, Vector3 worldPosition)
    {
        EnsureCreated();
        return instance != null ? instance.PlayByIdInternal(id, false, null, worldPosition) : null;
    }

    public static AudioSource PlayLoop(AudioId id, Transform followTarget = null)
    {
        EnsureCreated();
        return instance != null ? instance.PlayByIdInternal(id, true, followTarget, null) : null;
    }

    public static void PlayMusic(AudioId id, float fadeDuration = 0.5f)
    {
        EnsureCreated();
        if (instance != null)
        {
            instance.PlayMusicByIdInternal(id, fadeDuration);
        }
    }

    public static void StopMusic(float fadeDuration = 0.35f)
    {
        EnsureCreated();
        if (instance != null)
        {
            instance.StopMusicInternal(fadeDuration);
        }
    }

    public static AudioSource PlayClip2D(
        AudioClip clip,
        AudioBus bus = AudioBus.Ui,
        float volumeScale = 1f,
        float pitch = 1f
    )
    {
        EnsureCreated();
        return instance != null
            ? instance.PlayClipInternal(clip, bus, volumeScale, pitch, 0f, false, null, null, null)
            : null;
    }

    public static AudioSource PlayClipAtPoint(
        AudioClip clip,
        Vector3 worldPosition,
        AudioBus bus = AudioBus.Sfx,
        float volumeScale = 1f,
        float pitch = 1f,
        float spatialBlend = 1f
    )
    {
        EnsureCreated();
        return instance != null
            ? instance.PlayClipInternal(
                clip,
                bus,
                volumeScale,
                pitch,
                spatialBlend,
                false,
                null,
                null,
                worldPosition
            )
            : null;
    }

    public static void Stop(AudioSource source, float fadeDuration = 0f)
    {
        if (instance == null || source == null)
        {
            return;
        }

        instance.StopSourceInternal(source, fadeDuration);
    }

    public static void SetSourceVolumeScale(AudioSource source, float volumeScale)
    {
        if (source == null)
        {
            return;
        }

        EnsureCreated();
        if (instance == null)
        {
            return;
        }

        instance.SetSourceVolumeScaleInternal(source, volumeScale);
    }

    public static void SetSourcePitch(AudioSource source, float pitch)
    {
        if (source == null)
        {
            return;
        }

        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfigurationIfNeeded();
        EnsureRuntimeHierarchy();
        WarmOneShotPool();
        ApplySavedVolumesInternal();
    }

    private void Start()
    {
        ApplySavedVolumesInternal();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        RefreshExternalSourceVolumes();
    }

    private void LoadConfigurationIfNeeded()
    {
        if (settingsAsset == null)
        {
            settingsAsset = Resources.Load<GameAudioSettingsAsset>(SettingsResourcePath);
        }

        if (fallbackCatalog == null)
        {
            fallbackCatalog = Resources.Load<AudioCatalog>(CatalogResourcePath);
        }
    }

    private void EnsureRuntimeHierarchy()
    {
        if (oneShotRoot == null)
        {
            oneShotRoot = CreateChild("OneShots");
        }

        if (loopRoot == null)
        {
            loopRoot = CreateChild("Loops");
        }

        if (musicRoot == null)
        {
            musicRoot = CreateChild("Music");
        }

        if (musicPrimarySource == null)
        {
            musicPrimarySource = CreateMusicSource("MusicA");
        }

        if (musicSecondarySource == null)
        {
            musicSecondarySource = CreateMusicSource("MusicB");
        }

        if (activeMusicSource == null)
        {
            activeMusicSource = musicPrimarySource;
            inactiveMusicSource = musicSecondarySource;
        }
    }

    private void WarmOneShotPool()
    {
        int desiredPoolSize = settingsAsset != null ? settingsAsset.InitialOneShotPoolSize : 8;
        while (oneShotPool.Count < desiredPoolSize)
        {
            oneShotPool.Add(CreatePooledSource("OneShot_" + oneShotPool.Count));
        }
    }

    private AudioCatalog ResolveCatalog()
    {
        return settingsAsset != null && settingsAsset.Catalog != null
            ? settingsAsset.Catalog
            : fallbackCatalog;
    }

    private AudioSource PlayByIdInternal(
        AudioId id,
        bool forceLoop,
        Transform followTarget,
        Vector3? worldPosition
    )
    {
        if (id == AudioId.None)
        {
            return null;
        }

        AudioCatalog catalog = ResolveCatalog();
        if (catalog == null || !catalog.TryGetCue(id, out AudioCatalog.AudioCueDefinition cue))
        {
            return null;
        }

        if (cue.bus == AudioBus.Music)
        {
            PlayMusicCueInternal(cue, 0.5f);
            return activeMusicSource;
        }

        AudioClip clip = cue.GetRandomClip();
        if (clip == null)
        {
            return null;
        }

        return PlayClipInternal(
            clip,
            cue.bus,
            cue.GetResolvedVolume(),
            cue.GetResolvedPitch(),
            cue.spatialBlend,
            forceLoop || cue.loop,
            cue.outputMixerGroup,
            followTarget,
            worldPosition
        );
    }

    private void PlayMusicByIdInternal(AudioId id, float fadeDuration)
    {
        if (id == AudioId.None)
        {
            return;
        }

        AudioCatalog catalog = ResolveCatalog();
        if (catalog == null || !catalog.TryGetCue(id, out AudioCatalog.AudioCueDefinition cue))
        {
            return;
        }

        PlayMusicCueInternal(cue, fadeDuration);
    }

    private void PlayMusicCueInternal(AudioCatalog.AudioCueDefinition cue, float fadeDuration)
    {
        if (cue == null)
        {
            return;
        }

        AudioClip clip = cue.GetRandomClip();
        if (clip == null)
        {
            return;
        }

        EnsureRuntimeHierarchy();

        AudioSource nextMusicSource = inactiveMusicSource ?? musicSecondarySource;
        if (nextMusicSource == null)
        {
            return;
        }

        float resolvedVolume = cue.GetResolvedVolume();
        float resolvedPitch = cue.GetResolvedPitch();

        ConfigureSource(
            nextMusicSource,
            cue.bus,
            clip,
            resolvedVolume,
            resolvedPitch,
            0f,
            true,
            cue.outputMixerGroup,
            musicRoot,
            null
        );

        RegisterState(nextMusicSource, cue.bus, resolvedVolume, false, true);
        SetFadeMultiplier(nextMusicSource, 0f);
        nextMusicSource.Play();

        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
        }

        musicFadeCoroutine = StartCoroutine(
            CrossFadeMusic(activeMusicSource, nextMusicSource, Mathf.Max(0f, fadeDuration))
        );
    }

    private void StopMusicInternal(float fadeDuration)
    {
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = null;
        }

        if (activeMusicSource == null)
        {
            return;
        }

        if (fadeDuration <= 0f)
        {
            activeMusicSource.Stop();
            SetFadeMultiplier(activeMusicSource, 1f);
            return;
        }

        musicFadeCoroutine = StartCoroutine(FadeOutAndStop(activeMusicSource, fadeDuration));
    }

    private AudioSource PlayClipInternal(
        AudioClip clip,
        AudioBus bus,
        float volumeScale,
        float pitch,
        float spatialBlend,
        bool loop,
        AudioMixerGroup overrideMixerGroup,
        Transform followTarget,
        Vector3? worldPosition
    )
    {
        if (clip == null)
        {
            return null;
        }

        AudioSource source = loop ? CreateLoopSource() : GetAvailableOneShotSource();
        if (source == null)
        {
            return null;
        }

        Transform parent = followTarget != null ? followTarget : (loop ? loopRoot : oneShotRoot);
        ConfigureSource(
            source,
            bus,
            clip,
            volumeScale,
            pitch,
            spatialBlend,
            loop,
            overrideMixerGroup,
            parent,
            worldPosition
        );

        int playbackVersion = RegisterState(source, bus, volumeScale, !loop, false);
        source.Play();

        if (!loop)
        {
            StartCoroutine(ReleaseWhenPlaybackEnds(source, playbackVersion));
        }
        else if (!managedLoopSources.Contains(source))
        {
            managedLoopSources.Add(source);
        }

        return source;
    }

    private AudioSource CreateLoopSource()
    {
        GameObject sourceObject = new GameObject("LoopSource");
        sourceObject.transform.SetParent(loopRoot, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        ConfigureBaseSource(source);
        return source;
    }

    private AudioSource GetAvailableOneShotSource()
    {
        for (int i = 0; i < oneShotPool.Count; i++)
        {
            AudioSource pooledSource = oneShotPool[i];
            if (pooledSource == null)
            {
                continue;
            }

            if (pooledSource.isPlaying)
            {
                continue;
            }

            CleanupSourceState(pooledSource, true);
            return pooledSource;
        }

        int maxPoolSize = settingsAsset != null ? settingsAsset.MaxOneShotPoolSize : 24;
        if (oneShotPool.Count >= maxPoolSize)
        {
            return null;
        }

        AudioSource source = CreatePooledSource("OneShot_" + oneShotPool.Count);
        oneShotPool.Add(source);
        return source;
    }

    private AudioSource CreatePooledSource(string objectName)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(oneShotRoot, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        ConfigureBaseSource(source);
        return source;
    }

    private AudioSource CreateMusicSource(string objectName)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(musicRoot, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        ConfigureBaseSource(source);
        source.loop = true;
        return source;
    }

    private void ConfigureBaseSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 30f;
    }

    private void ConfigureSource(
        AudioSource source,
        AudioBus bus,
        AudioClip clip,
        float volumeScale,
        float pitch,
        float spatialBlend,
        bool loop,
        AudioMixerGroup overrideMixerGroup,
        Transform parent,
        Vector3? worldPosition
    )
    {
        if (source == null)
        {
            return;
        }

        source.transform.SetParent(parent, false);
        if (worldPosition.HasValue)
        {
            source.transform.position = worldPosition.Value;
        }
        else
        {
            source.transform.localPosition = Vector3.zero;
        }

        source.clip = clip;
        source.loop = loop;
        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.outputAudioMixerGroup =
            overrideMixerGroup != null ? overrideMixerGroup : GetMixerGroup(bus);
        source.time = 0f;
    }

    private int RegisterState(
        AudioSource source,
        AudioBus bus,
        float baseVolume,
        bool isPooled,
        bool isMusic
    )
    {
        if (source == null)
        {
            return 0;
        }

        SourceState state;
        if (!sourceStates.TryGetValue(source, out state))
        {
            state = new SourceState();
            sourceStates.Add(source, state);
        }

        state.Bus = bus;
        state.BaseVolume = Mathf.Max(0f, baseVolume);
        state.FadeMultiplier = 1f;
        state.IsPooled = isPooled;
        state.IsMusic = isMusic;
        state.PlaybackVersion++;
        ApplyStateVolume(source, state);
        return state.PlaybackVersion;
    }

    private void ApplySavedVolumesInternal()
    {
        LoadConfigurationIfNeeded();

        Array buses = Enum.GetValues(typeof(AudioBus));
        for (int i = 0; i < buses.Length; i++)
        {
            AudioBus bus = (AudioBus)buses.GetValue(i);
            float volume = AudioVolumeSettings.GetSavedOrDefaultVolume(bus);
            ApplyMixerVolume(bus, volume);
        }

        RefreshAllManagedVolumes();
        RefreshExternalSourceVolumes();
    }

    private void SetBusVolumeInternal(AudioBus bus, float volume, bool save)
    {
        float clampedVolume = AudioVolumeSettings.ClampVolume(volume);
        if (save)
        {
            AudioVolumeSettings.SaveVolume(bus, clampedVolume);
        }

        ApplyMixerVolume(bus, clampedVolume);
        if (bus == AudioBus.Master)
        {
            RefreshAllManagedVolumes();
            RefreshExternalSourceVolumes();
            return;
        }

        if (!UsesMixerVolume(bus) || !UsesMixerVolume(AudioBus.Master))
        {
            RefreshAllManagedVolumes();
        }

        RefreshExternalSourceVolumes();
    }

    private void RefreshAllManagedVolumes()
    {
        foreach (KeyValuePair<AudioSource, SourceState> pair in sourceStates)
        {
            if (pair.Key == null || pair.Value == null)
            {
                continue;
            }

            ApplyStateVolume(pair.Key, pair.Value);
        }
    }

    private void ApplyStateVolume(AudioSource source, SourceState state)
    {
        if (source == null || state == null)
        {
            return;
        }

        float effectiveVolume = state.BaseVolume * Mathf.Clamp01(state.FadeMultiplier);
        effectiveVolume *= GetFallbackBusScalar(state.Bus);
        source.volume = Mathf.Clamp01(effectiveVolume);
    }

    private void RefreshExternalSourceVolumes()
    {
        if (settingsAsset == null)
        {
            return;
        }

        CleanupExternalSourceCache();

        AudioSource[] sceneSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneSources.Length; i++)
        {
            AudioSource source = sceneSources[i];
            if (source == null || IsManagedSource(source))
            {
                continue;
            }

            if (!TryResolveExternalSourceBus(source, out AudioBus bus))
            {
                continue;
            }

            if (!externalSourceBaseVolumes.TryGetValue(source, out float baseVolume))
            {
                baseVolume = source.volume;
                externalSourceBaseVolumes.Add(source, baseVolume);
            }

            source.volume = Mathf.Clamp01(baseVolume * GetFallbackBusScalar(bus));
        }
    }

    private void CleanupExternalSourceCache()
    {
        if (externalSourceBaseVolumes.Count == 0)
        {
            return;
        }

        List<AudioSource> staleSources = null;
        foreach (KeyValuePair<AudioSource, float> pair in externalSourceBaseVolumes)
        {
            if (pair.Key != null)
            {
                continue;
            }

            staleSources ??= new List<AudioSource>();
            staleSources.Add(pair.Key);
        }

        if (staleSources == null)
        {
            return;
        }

        for (int i = 0; i < staleSources.Count; i++)
        {
            externalSourceBaseVolumes.Remove(staleSources[i]);
        }
    }

    private bool IsManagedSource(AudioSource source)
    {
        return source != null && source.transform.IsChildOf(transform);
    }

    private bool TryResolveExternalSourceBus(AudioSource source, out AudioBus bus)
    {
        bus = AudioBus.Master;
        if (source == null || source.outputAudioMixerGroup == null || settingsAsset == null)
        {
            return false;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Music))
        {
            bus = AudioBus.Music;
            return true;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Sfx))
        {
            bus = AudioBus.Sfx;
            return true;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Ui))
        {
            bus = AudioBus.Ui;
            return true;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Ambience))
        {
            bus = AudioBus.Ambience;
            return true;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Voice))
        {
            bus = AudioBus.Voice;
            return true;
        }

        if (source.outputAudioMixerGroup == settingsAsset.GetMixerGroup(AudioBus.Alert))
        {
            bus = AudioBus.Alert;
            return true;
        }

        return false;
    }

    private float GetFallbackBusScalar(AudioBus bus)
    {
        float scalar = UsesMixerVolume(AudioBus.Master)
            ? 1f
            : AudioVolumeSettings.GetSavedOrDefaultVolume(AudioBus.Master);

        if (bus != AudioBus.Master && !UsesMixerVolume(bus))
        {
            scalar *= AudioVolumeSettings.GetSavedOrDefaultVolume(bus);
        }

        return scalar;
    }

    private bool UsesMixerVolume(AudioBus bus)
    {
        if (
            settingsAsset == null
            || settingsAsset.MasterMixer == null
            || !settingsAsset.TryGetVolumeParameter(bus, out string parameterName)
            || string.IsNullOrWhiteSpace(parameterName)
        )
        {
            return false;
        }

        return settingsAsset.MasterMixer.GetFloat(parameterName, out _);
    }

    private void ApplyMixerVolume(AudioBus bus, float normalizedVolume)
    {
        if (
            settingsAsset == null
            || settingsAsset.MasterMixer == null
            || !settingsAsset.TryGetVolumeParameter(bus, out string parameterName)
            || string.IsNullOrWhiteSpace(parameterName)
        )
        {
            return;
        }

        settingsAsset.MasterMixer.SetFloat(parameterName, LinearToDecibel(normalizedVolume));
    }

    private AudioMixerGroup GetMixerGroup(AudioBus bus)
    {
        return settingsAsset != null ? settingsAsset.GetMixerGroup(bus) : null;
    }

    private IEnumerator ReleaseWhenPlaybackEnds(AudioSource source, int playbackVersion)
    {
        if (source == null)
        {
            yield break;
        }

        while (
            source != null
            && sourceStates.TryGetValue(source, out SourceState state)
            && state.PlaybackVersion == playbackVersion
            && source.isPlaying
        )
        {
            yield return null;
        }

        if (
            source == null
            || !sourceStates.TryGetValue(source, out SourceState finalState)
            || finalState.PlaybackVersion != playbackVersion
            || source.isPlaying
        )
        {
            yield break;
        }

        CleanupSourceState(source, true);
    }

    private IEnumerator CrossFadeMusic(
        AudioSource outgoingSource,
        AudioSource incomingSource,
        float fadeDuration
    )
    {
        if (incomingSource == null)
        {
            yield break;
        }

        if (fadeDuration <= 0f)
        {
            SetFadeMultiplier(incomingSource, 1f);
            if (outgoingSource != null)
            {
                outgoingSource.Stop();
                SetFadeMultiplier(outgoingSource, 1f);
            }

            activeMusicSource = incomingSource;
            inactiveMusicSource =
                incomingSource == musicPrimarySource ? musicSecondarySource : musicPrimarySource;
            musicFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            SetFadeMultiplier(incomingSource, Mathf.SmoothStep(0f, 1f, t));

            if (outgoingSource != null)
            {
                SetFadeMultiplier(outgoingSource, Mathf.SmoothStep(1f, 0f, t));
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetFadeMultiplier(incomingSource, 1f);
        if (outgoingSource != null)
        {
            outgoingSource.Stop();
            SetFadeMultiplier(outgoingSource, 1f);
        }

        activeMusicSource = incomingSource;
        inactiveMusicSource =
            incomingSource == musicPrimarySource ? musicSecondarySource : musicPrimarySource;
        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float fadeDuration, int playbackVersion = -1)
    {
        if (source == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration && source != null && source.isPlaying)
        {
            if (playbackVersion != -1 && sourceStates.TryGetValue(source, out SourceState state) && state.PlaybackVersion != playbackVersion)
            {
                yield break;
            }

            float t = elapsed / fadeDuration;
            SetFadeMultiplier(source, 1f - t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (source != null && (playbackVersion == -1 || !sourceStates.TryGetValue(source, out SourceState finalState) || finalState.PlaybackVersion == playbackVersion))
        {
            source.Stop();
            SetFadeMultiplier(source, 1f);
        }
    }

    private void StopSourceInternal(AudioSource source, float fadeDuration)
    {
        if (source == null)
        {
            return;
        }

        sourceStates.TryGetValue(source, out SourceState state);
        int version = state != null ? state.PlaybackVersion : -1;

        if (source == musicPrimarySource || source == musicSecondarySource)
        {
            if (fadeDuration <= 0f)
            {
                source.Stop();
                SetFadeMultiplier(source, 1f);
                return;
            }

            StartCoroutine(FadeOutAndStop(source, fadeDuration, version));
            return;
        }

        if (fadeDuration <= 0f)
        {
            source.Stop();
            CleanupSourceState(source, true);
            return;
        }

        StartCoroutine(FadeOutLoopAndCleanup(source, fadeDuration, version));
    }

    private void SetSourceVolumeScaleInternal(AudioSource source, float volumeScale)
    {
        if (source == null)
        {
            return;
        }

        float clampedVolume = Mathf.Clamp01(volumeScale);
        if (sourceStates.TryGetValue(source, out SourceState state))
        {
            state.BaseVolume = clampedVolume;
            ApplyStateVolume(source, state);
            return;
        }

        if (TryResolveExternalSourceBus(source, out AudioBus bus))
        {
            externalSourceBaseVolumes[source] = clampedVolume;
            source.volume = Mathf.Clamp01(clampedVolume * GetFallbackBusScalar(bus));
            return;
        }

        source.volume = clampedVolume;
    }

    private IEnumerator FadeOutLoopAndCleanup(AudioSource source, float fadeDuration, int playbackVersion = -1)
    {
        if (source == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration && source != null)
        {
            if (playbackVersion != -1 && sourceStates.TryGetValue(source, out SourceState state) && state.PlaybackVersion != playbackVersion)
            {
                yield break;
            }

            float t = elapsed / fadeDuration;
            SetFadeMultiplier(source, 1f - t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (source != null && (playbackVersion == -1 || !sourceStates.TryGetValue(source, out SourceState finalState) || finalState.PlaybackVersion == playbackVersion))
        {
            source.Stop();
            CleanupSourceState(source, true);
        }
    }

    private void SetFadeMultiplier(AudioSource source, float fadeMultiplier)
    {
        if (source == null || !sourceStates.TryGetValue(source, out SourceState state))
        {
            return;
        }

        state.FadeMultiplier = Mathf.Clamp01(fadeMultiplier);
        ApplyStateVolume(source, state);
    }

    private void CleanupSourceState(AudioSource source, bool resetTransform)
    {
        if (source == null)
        {
            return;
        }

        sourceStates.Remove(source);

        if (source == musicPrimarySource || source == musicSecondarySource)
        {
            source.clip = null;
            source.loop = true;
            source.transform.SetParent(musicRoot, false);
            source.transform.localPosition = Vector3.zero;
            source.volume = 0f;
            return;
        }

        if (oneShotPool.Contains(source))
        {
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = null;
            source.volume = 0f;
            if (resetTransform)
            {
                source.transform.SetParent(oneShotRoot, false);
                source.transform.localPosition = Vector3.zero;
            }

            return;
        }

        managedLoopSources.Remove(source);
        if (source.gameObject != null)
        {
            Destroy(source.gameObject);
        }
    }

    private Transform CreateChild(string childName)
    {
        Transform existingChild = transform.Find(childName);
        if (existingChild != null)
        {
            return existingChild;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(transform, false);
        return childObject.transform;
    }

    private static float LinearToDecibel(float normalizedVolume)
    {
        return normalizedVolume <= 0.0001f
            ? MinDecibel
            : Mathf.Log10(Mathf.Clamp01(normalizedVolume)) * 20f;
    }
}
