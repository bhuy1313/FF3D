// ParsingData.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

namespace ProceduralUIEffects.Lite
{
    public struct ModifierInfo
    {
        public string name;
        public float value;
    }

    public struct ActionMarker
    {
        public string Name;
        public int Index;
        public float Value;
    }

    public class EffectRegion
    {
        public AnimationEffectBase effectAsset;
        public int startIndex;
        public int endIndex;
        public ModifierInfo[] modifiers;
    }
}