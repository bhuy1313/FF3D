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
        
        private AudioSource audioSource;

        private void Awake()
        {
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
            // Play the sound once each time the pointer enters the UI element's rect
            if (hoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound);
            }
        }
    }
}