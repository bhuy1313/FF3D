// AnimationEffectBase.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    public abstract class AnimationEffectBase : ScriptableObject
    {
        [Header("Effect Identification")]
        [Tooltip("The tag used in the text to trigger this effect, e.g., 'wave'. Must be unique and lowercase.")]
        public string EffectTag;

        public abstract void ApplyEffect(ref CharacterData character, PUAP_Core animator);
    }
}