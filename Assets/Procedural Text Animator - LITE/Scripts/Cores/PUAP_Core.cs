// PUAP_Core.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using System.Collections.Generic;
using UnityEngine;

namespace ProceduralUIEffects.Lite
{
    public enum AnimationLoop { Update, LateUpdate, Manual }

    public abstract class PUAP_Core : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Controls when the animation updates. Use Manual if you want to call Animate() from your own script.")]
        public AnimationLoop animationLoop = AnimationLoop.Update;

        public float timeSinceStart { get; protected set; }
        public float deltaTime { get; protected set; }

        [HideInInspector]
        public int maxVisibleCharacters = int.MaxValue;

        public CharacterData[] characters { get; protected set; }
        public int CharactersCount { get; protected set; }
        public List<EffectRegion> effectRegions { get; protected set; }
        public List<ActionMarker> Actions { get; protected set; }
        public Vector3 textMinBounds { get; private set; }
        public Vector3 textMaxBounds { get; private set; }

        #region Unity Lifecycle
        protected virtual void Update()
        {
            if (animationLoop == AnimationLoop.Update)
            {
                Animate(Time.deltaTime);
            }
        }

        protected virtual void LateUpdate()
        {
            if (animationLoop == AnimationLoop.LateUpdate)
            {
                Animate(Time.deltaTime);
            }
        }
        #endregion

        #region Abstract Methods (To be implemented by the 'Bridge')

        public abstract void SetText(string text);

        protected abstract void CopyMeshFromSource();

        protected abstract void PasteMeshToSource();
        #endregion

        public virtual void Animate(float customDeltaTime)
        {
            deltaTime = customDeltaTime;
            timeSinceStart += deltaTime;

            UpdateBehaviorStates();
            if (CharactersCount == 0) return;

            CalculateTextBounds();

            for (int i = 0; i < CharactersCount; i++)
            {
                if (i >= maxVisibleCharacters)
                {
                    characters[i].passedTime = 0;
                    for (int v = 0; v < 4; v++)
                    {
                        characters[i].modifiedVertices[v] = Vector3.zero;
                    }
                    continue;
                }
                else
                {
                    characters[i].passedTime += deltaTime;
                }

                // Copy source data to modified arrays
                characters[i].modifiedVertices = (Vector3[])characters[i].sourceVertices.Clone();
                characters[i].modifiedColors = (Color32[])characters[i].sourceColors.Clone();

                // Apply all effects that affect this character
                if (effectRegions != null)
                {
                    foreach (var region in effectRegions)
                    {
                        if (i >= region.startIndex && i < region.endIndex)
                        {
                            region.effectAsset.ApplyEffect(ref characters[i], this);
                        }
                    }
                }
            }

            PasteMeshToSource();
        }

        private void CalculateTextBounds()
        {
            if (CharactersCount == 0) return;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0);

            bool hasVisibleChars = false;

            for (int i = 0; i < CharactersCount; i++)
            {
                if (i >= maxVisibleCharacters) continue;

                hasVisibleChars = true;

                Vector3[] vertices = characters[i].sourceVertices;

                min.x = Mathf.Min(min.x, vertices[0].x);
                min.y = Mathf.Min(min.y, vertices[3].y);
                max.x = Mathf.Max(max.x, vertices[2].x);
                max.y = Mathf.Max(max.y, vertices[1].y);
            }

            if (hasVisibleChars)
            {
                textMinBounds = min;
                textMaxBounds = max;
            }
        }

        protected virtual void UpdateBehaviorStates() { }
    }
}