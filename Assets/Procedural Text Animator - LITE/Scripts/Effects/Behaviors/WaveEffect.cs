// WaveEffect.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    [CreateAssetMenu(fileName = "New Wave Effect", menuName = "Procedural UI Effects Lite/Behaviors/Wave Effect")]
    public class WaveEffect : BehaviorEffect
    {
        [Header("Wave Settings")]
        [Tooltip("The vertical height (amplitude) of the wave.")]
        public float waveHeight = 5.0f;

        [Tooltip("The speed at which the wave flows through the text.")]
        public float waveSpeed = 10.0f;

        [Tooltip("The number of full waves visible across the text.")]
        public float waveFrequency = 1.0f;

        public override void ApplyEffect(ref CharacterData character, PUAP_Core animator)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 originalPos = character.sourceVertices[i];

                float yOffset = Mathf.Sin(animator.timeSinceStart * waveSpeed + character.index * waveFrequency) * waveHeight;

                character.modifiedVertices[i] = originalPos + new Vector3(0, yOffset, 0);
            }
        }
    }
}