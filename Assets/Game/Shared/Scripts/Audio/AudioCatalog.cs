using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "FF3D/Audio/Audio Catalog", fileName = "GameAudioCatalog")]
public sealed class AudioCatalog : ScriptableObject
{
    [Serializable]
    public sealed class AudioCueDefinition
    {
        public AudioId id = AudioId.None;
        public AudioBus bus = AudioBus.Sfx;
        public AudioClip[] clips;
        [Min(0f)]
        public float volume = 1f;
        [Min(0.01f)]
        public float pitch = 1f;
        [Min(0f)]
        public float randomVolume = 0f;
        [Min(0f)]
        public float randomPitch = 0f;
        [Range(0f, 1f)]
        public float spatialBlend = 0f;
        public bool loop;
        public AudioMixerGroup outputMixerGroup;

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            if (clips.Length == 1)
            {
                return clips[0];
            }

            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        public float GetResolvedVolume()
        {
            if (randomVolume <= 0f)
            {
                return Mathf.Max(0f, volume);
            }

            return Mathf.Max(0f, volume + UnityEngine.Random.Range(-randomVolume, randomVolume));
        }

        public float GetResolvedPitch()
        {
            if (randomPitch <= 0f)
            {
                return Mathf.Clamp(pitch, 0.01f, 3f);
            }

            return Mathf.Clamp(
                pitch + UnityEngine.Random.Range(-randomPitch, randomPitch),
                0.01f,
                3f
            );
        }
    }

    [SerializeField]
    private List<AudioCueDefinition> cues = new List<AudioCueDefinition>();

    private readonly Dictionary<AudioId, AudioCueDefinition> lookup =
        new Dictionary<AudioId, AudioCueDefinition>();

    public bool TryGetCue(AudioId id, out AudioCueDefinition cue)
    {
        EnsureLookup();
        return lookup.TryGetValue(id, out cue);
    }

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    private void EnsureLookup()
    {
        if (lookup.Count == 0 && cues != null && cues.Count > 0)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        if (cues == null)
        {
            return;
        }

        for (int i = 0; i < cues.Count; i++)
        {
            AudioCueDefinition cue = cues[i];
            if (cue == null || cue.id == AudioId.None || lookup.ContainsKey(cue.id))
            {
                continue;
            }

            lookup.Add(cue.id, cue);
        }
    }
}
