// FadeInEffect.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    [CreateAssetMenu(fileName = "New FadeIn Effect", menuName = "Procedural UI Effects Lite/Appearances/FadeIn Effect")]
    public class FadeInEffect : AppearanceEffect
    {
        public override void ApplyEffect(ref CharacterData character, PUAP_Core animator)
        {
            float progress = Mathf.Clamp01(character.passedTime / duration);

            byte alpha = (byte)(progress * 255);

            for (int i = 0; i < 4; i++)
            {
                character.modifiedColors[i].a = alpha;
            }
        }
    }
}