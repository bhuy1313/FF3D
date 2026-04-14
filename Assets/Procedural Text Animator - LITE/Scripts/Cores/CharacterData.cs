// CharacterData.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    public struct CharacterData
    {
        // --- Source Data---
        public Vector3[] sourceVertices;
        public Color32[] sourceColors;

        // --- Modified Data ---
        public Vector3[] modifiedVertices;
        public Color32[] modifiedColors;

        // --- State Information ---
        public int index;
        public bool isVisible;
        public float passedTime;

        public void Initialize()
        {
            sourceVertices = new Vector3[4];
            sourceColors = new Color32[4];
            modifiedVertices = new Vector3[4];
            modifiedColors = new Color32[4];
        }
    }
}