// PUAP_TMP.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.
// Cleaned version without dependencies on excluded effects (Glitch, Jitter)

using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace ProceduralUIEffects.Lite
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("Procedural UI Effects Lite/PUAP TMP")]
    public class PUAP_TMP : PUAP_Core
    {
        private TextMeshProUGUI tmproText;
        private string rawText;

        [Header("Effect Database")]
        [Tooltip("Assign the project's EffectDatabase asset here.")]
        public EffectDatabase effectDatabase;

        [Header("Lite Version Info")]
        [Tooltip("🌟 Upgrade to FULL VERSION for 14 additional effects including Glitch, Jitter, Rainbow, Gradients and more!")]
        public string upgradeNotice = "Full version available on Unity Asset Store";

        // Character seeds for effects that need randomness (like ShakeEffect)
        public float[] characterSeeds { get; private set; }

        void Awake()
        {
            tmproText = GetComponent<TextMeshProUGUI>();
            rawText = tmproText.text;
        }

        void OnEnable()
        {
            ProcessText();
        }

        public override void SetText(string newRawText)
        {
            rawText = newRawText;
            if (gameObject.activeInHierarchy)
            {
                ProcessText();
            }
        }

        private void ProcessText()
        {
            if (effectDatabase != null)
            {
                var (cleanText, parsedRegions, parsedActions) = TextParser.Parse(rawText, effectDatabase);
                this.effectRegions = parsedRegions;
                this.Actions = parsedActions;

                tmproText.text = cleanText;
            }
            else
            {
                tmproText.text = rawText;
            }

            tmproText.ForceMeshUpdate();
            CopyMeshFromSource();
        }

        public override void Animate(float customDeltaTime)
        {
            base.Animate(customDeltaTime);
        }

        protected override void CopyMeshFromSource()
        {
            var textInfo = tmproText.textInfo;
            CharactersCount = textInfo.characterCount;

            if (characters == null || characters.Length < CharactersCount)
            {
                characters = new CharacterData[CharactersCount];
            }

            // Initialize character seeds for effects like Shake that need per-character randomness
            if (characterSeeds == null || characterSeeds.Length < CharactersCount)
            {
                characterSeeds = new float[CharactersCount];
                for (int i = 0; i < CharactersCount; i++)
                {
                    characterSeeds[i] = Random.Range(0f, 1000f);
                }
            }

            for (int i = 0; i < CharactersCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                characters[i].Initialize();
                characters[i].index = i;

                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                for (int v = 0; v < 4; v++)
                {
                    characters[i].sourceVertices[v] = textInfo.meshInfo[materialIndex].vertices[vertexIndex + v];
                    characters[i].sourceColors[v] = textInfo.meshInfo[materialIndex].colors32[vertexIndex + v];
                }
            }
        }

        protected override void PasteMeshToSource()
        {
            var textInfo = tmproText.textInfo;

            for (int i = 0; i < CharactersCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                var destinationVertices = textInfo.meshInfo[materialIndex].vertices;
                var destinationColors = textInfo.meshInfo[materialIndex].colors32;

                for (int v = 0; v < 4; v++)
                {
                    destinationVertices[vertexIndex + v] = characters[i].modifiedVertices[v];
                    destinationColors[vertexIndex + v] = characters[i].modifiedColors[v];
                }
            }

            tmproText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

    }
}