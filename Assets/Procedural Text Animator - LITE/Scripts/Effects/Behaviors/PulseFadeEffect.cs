// PulseFadeEffect.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    [CreateAssetMenu(fileName = "New Pulse Effect", menuName = "Procedural UI Effects Lite/Behaviors/Pulse Effect")]
    public class PulseFadeEffect : BehaviorEffect
    {
        [Header("Pulse Settings")]
        [Tooltip("Minimum alpha value (0-1).")]
        [Range(0f, 1f)]
        public float minAlpha = 0.3f;

        [Tooltip("Speed of the pulsing effect.")]
        [Range(0f, 20f)]
        public float pulseSpeed = 3f;

        [Header("Wave Settings")]
        [Tooltip("Controls the animation offset between characters, creating a wave-like ripple.")]
        [Range(0f, 5f)]
        public float waveFrequency = 0.5f;

        public override void ApplyEffect(ref CharacterData character, PUAP_Core animator)
        {
            // Calculate wave-based time offset for this character
            float time = animator.timeSinceStart * pulseSpeed + character.index * waveFrequency;

            // Calculate alpha using sine wave (oscillates between -1 and 1, then normalized to 0-1)
            float normalizedSin = (Mathf.Sin(time) + 1f) * 0.5f;

            // Lerp between min and max alpha
            float targetAlpha = Mathf.Lerp(minAlpha, 1f, normalizedSin);

            // Apply to all 4 vertices
            for (int i = 0; i < 4; i++)
            {
                Color32 color = character.modifiedColors[i];
                color.a = (byte)(targetAlpha * 255);
                character.modifiedColors[i] = color;
            }
        }
    }
}