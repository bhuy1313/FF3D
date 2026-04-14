// EffectDatabase.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;
using System.Collections.Generic;

namespace ProceduralUIEffects.Lite
{
    [CreateAssetMenu(fileName = "Effect Database", menuName = "Procedural UI Effects Lite/Effect Database")]
    public class EffectDatabase : ScriptableObject
    {
        [Header("Effects")]
        [Tooltip("Add all the effect ScriptableObjects you want to be available in your project here.")]
        public List<AnimationEffectBase> effects;

        [Header("Lite Version Info")]
        [Tooltip("🌟 UPGRADE TO FULL VERSION for 14 additional effects!\n\nFull version includes: Breathe, Bounce, Wobble, Swing, Shear, Jitter, Glitch, Rainbow, Gradients, Drop In, Scale Pop, Rotate Flip, and more!")]
        [TextArea(3, 5)]
        public string upgradeInfo = "Lite includes 4 effects. Upgrade to Full version for 18 total effects + advanced features!\n\nVisit Unity Asset Store to upgrade.";

        private Dictionary<string, AnimationEffectBase> effectDictionary;
        private bool isInitialized = false;

        public void Initialize()
        {
            if (effectDictionary != null && isInitialized) return;

            effectDictionary = new Dictionary<string, AnimationEffectBase>();
            foreach (var effect in effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.EffectTag))
                {
                    Debug.LogWarning("Found an effect in the database with no EffectTag. It will be ignored.", this);
                    continue;
                }

                if (effectDictionary.ContainsKey(effect.EffectTag))
                {
                    Debug.LogWarning($"Duplicate EffectTag '{effect.EffectTag}' found in the database. Only the first one will be used.", this);
                    continue;
                }

                effectDictionary.Add(effect.EffectTag, effect);
            }
            isInitialized = true;
        }

        public AnimationEffectBase GetEffect(string tag)
        {
            Initialize();

            effectDictionary.TryGetValue(tag, out AnimationEffectBase effect);
            return effect;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Procedural Text Animator/Upgrade to Full Version 🌟")]
        private static void ShowUpgradeInfo()
        {
            bool upgrade = UnityEditor.EditorUtility.DisplayDialog(
                "Upgrade to Full Version",
                "Get 14 additional professional effects!\n\n" +
                "BEHAVIOR EFFECTS:\n" +
                "• Breathe - Scale pulsing\n" +
                "• Bounce - Bouncing motion\n" +
                "• Wobble - Jelly-like wobble\n" +
                "• Swing - Pendulum swing\n" +
                "• Shear - Slanting effect\n" +
                "• Jitter - Erratic jumps\n" +
                "• Glitch - RGB chromatic split\n" +
                "• Rainbow - Color cycling\n" +
                "• Gradient - Static gradients\n" +
                "• Animated Gradient - Scrolling colors\n\n" +
                "APPEARANCE EFFECTS:\n" +
                "• Drop In - Fall from above\n" +
                "• Scale Pop - Bounce reveal\n" +
                "• Rotate Flip - 3D flip reveal\n\n" +
                "BONUS FEATURES:\n" +
                "• Advanced Typewriter (<speed> tag)\n" +
                "• UI Panel Shake component\n" +
                "• Priority email support\n" +
                "• Complete PDF documentation",
                "Visit Asset Store",
                "Maybe Later"
            );

            if (upgrade)
            {

                UnityEngine.Application.OpenURL("https://assetstore.unity.com/packages/tools/gui/procedural-text-animator-327131");
            }
        }
#endif
    }
}