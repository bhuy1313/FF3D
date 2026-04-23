using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "FF3D/Audio/Audio Settings", fileName = "GameAudioSettings")]
public sealed class GameAudioSettingsAsset : ScriptableObject
{
    [Header("Catalog")]
    [SerializeField]
    private AudioCatalog audioCatalog;

    [Header("Mixer")]
    [SerializeField]
    private AudioMixer masterMixer;

    [SerializeField]
    private AudioMixerGroup musicMixerGroup;

    [SerializeField]
    private AudioMixerGroup sfxMixerGroup;

    [SerializeField]
    private AudioMixerGroup uiMixerGroup;

    [SerializeField]
    private AudioMixerGroup ambienceMixerGroup;

    [SerializeField]
    private AudioMixerGroup voiceMixerGroup;

    [SerializeField]
    private AudioMixerGroup alertMixerGroup;

    [Header("Exposed Volume Parameters")]
    [SerializeField]
    private string masterVolumeParameter = "MasterVolume";

    [SerializeField]
    private string musicVolumeParameter = "MusicVolume";

    [SerializeField]
    private string sfxVolumeParameter = "SfxVolume";

    [SerializeField]
    private string uiVolumeParameter = "UiVolume";

    [SerializeField]
    private string ambienceVolumeParameter = "AmbienceVolume";

    [SerializeField]
    private string voiceVolumeParameter = "VoiceVolume";

    [SerializeField]
    private string alertVolumeParameter = string.Empty;

    [Header("Runtime")]
    [SerializeField, Min(1)]
    private int initialOneShotPoolSize = 8;

    [SerializeField, Min(1)]
    private int maxOneShotPoolSize = 24;

    public AudioCatalog Catalog => audioCatalog;
    public AudioMixer MasterMixer => masterMixer;
    public int InitialOneShotPoolSize => Mathf.Max(1, initialOneShotPoolSize);
    public int MaxOneShotPoolSize => Mathf.Max(InitialOneShotPoolSize, maxOneShotPoolSize);

    public AudioMixerGroup GetMixerGroup(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Music:
                return musicMixerGroup;
            case AudioBus.Sfx:
                return sfxMixerGroup;
            case AudioBus.Ui:
                return uiMixerGroup;
            case AudioBus.Ambience:
                return ambienceMixerGroup;
            case AudioBus.Voice:
                return voiceMixerGroup;
            case AudioBus.Alert:
                return alertMixerGroup;
            default:
                return null;
        }
    }

    public bool TryGetVolumeParameter(AudioBus bus, out string parameterName)
    {
        switch (bus)
        {
            case AudioBus.Master:
                parameterName = masterVolumeParameter;
                break;
            case AudioBus.Music:
                parameterName = musicVolumeParameter;
                break;
            case AudioBus.Sfx:
                parameterName = sfxVolumeParameter;
                break;
            case AudioBus.Ui:
                parameterName = uiVolumeParameter;
                break;
            case AudioBus.Ambience:
                parameterName = ambienceVolumeParameter;
                break;
            case AudioBus.Voice:
                parameterName = voiceVolumeParameter;
                break;
            case AudioBus.Alert:
                parameterName = alertVolumeParameter;
                break;
            default:
                parameterName = string.Empty;
                break;
        }

        return !string.IsNullOrWhiteSpace(parameterName);
    }
}
