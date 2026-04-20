using UnityEngine;
using UnityEngine.EventSystems;

namespace FF3D.UI
{
    /// <summary>
    /// Attach this script to a UI Button or any UI element to play a sound on hover.
    /// </summary>
    public class UIHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        [Tooltip("The sound to play when the mouse hovers over this UI element.")]
        [SerializeField] private AudioClip hoverSound;

        [SerializeField]
        private AudioId hoverAudioId = AudioId.UiHover;

        [SerializeField]
        private bool useAudioService = true;

        [SerializeField, Range(0f, 1f)]
        private float volumeScale = 1f;

        private AudioSource audioSource;

        private void Awake()
        {
            if (hoverAudioId == AudioId.None)
            {
                hoverAudioId = AudioId.UiHover;
            }

            if (volumeScale <= 0f)
            {
                volumeScale = 1f;
            }

            // Try to get an existing AudioSource on this GameObject
            audioSource = GetComponent<AudioSource>();

            // If none exists, automatically add one
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Configure the AudioSource for UI sounds (2D, no play on awake)
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // Ensure it's a 2D sound
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverAudioId != AudioId.None && AudioService.Play(hoverAudioId) != null)
            {
                return;
            }

            if (hoverSound != null && AudioService.PlayClip2D(hoverSound, AudioBus.Ui, volumeScale) != null)
            {
                return;
            }

            if (!useAudioService && hoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound, volumeScale);
            }
        }
    }
}
