// AppearanceEffect.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    /// <summary>
    /// Base class for one-shot appearance effects used with typewriter reveals.
    /// </summary>
    public abstract class AppearanceEffect : AnimationEffectBase
    {
        [Header("Appearance Settings")]
        [Tooltip("How long in seconds this appearance effect takes to complete for a single character.")]
        [Min(0.01f)]
        public float duration = 0.5f;
    }
}