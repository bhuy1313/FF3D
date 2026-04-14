// ShakeEffect.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    [CreateAssetMenu(fileName = "New Shake Effect", menuName = "Procedural UI Effects Lite/Behaviors/Shake Effect")]
    public class ShakeEffect : BehaviorEffect
    {
        [Header("Shake Settings")]
        [Tooltip("The maximum distance each character can shake on the X and Y axes.")]
        [Range(0f, 50f)]
        public float shakeAmount = 2.0f;

        [Tooltip("How fast the characters will shake. Higher values are more frantic.")]
        [Range(0f, 50f)]
        public float shakeSpeed = 15.0f;

        public override void ApplyEffect(ref CharacterData character, PUAP_Core animator)
        {
            var puapTmpAnimator = (PUAP_TMP)animator;
            if (puapTmpAnimator.characterSeeds == null || character.index >= puapTmpAnimator.characterSeeds.Length)
                return;

            float seed = puapTmpAnimator.characterSeeds[character.index];

            float noiseX = (Mathf.PerlinNoise(seed, animator.timeSinceStart * shakeSpeed) * 2f) - 1f;
            float noiseY = (Mathf.PerlinNoise(seed + 10f, animator.timeSinceStart * shakeSpeed) * 2f) - 1f;

            Vector3 offset = new Vector3(noiseX, noiseY, 0) * shakeAmount;

            for (int i = 0; i < 4; i++)
            {
                character.modifiedVertices[i] += offset;
            }
        }
    }
}