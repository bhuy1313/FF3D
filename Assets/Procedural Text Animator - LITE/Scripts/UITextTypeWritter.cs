// UITextTypewriter.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.
// Upgrade to FULL version for <speed> tag support and advanced controls!

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace ProceduralUIEffects.Lite
{
    [RequireComponent(typeof(PUAP_Core))]
    [AddComponentMenu("Procedural UI Effects Lite/UI Text Typewriter")]
    public class UITextTypewriter : MonoBehaviour
    {
        private PUAP_Core animationEngine;

        [Header("Typing Settings")]
        [Tooltip("Characters revealed per second.")]
        public float charsPerSecond = 20f;

        [Tooltip("Start typing automatically when enabled.")]
        public bool playOnEnable = true;

        private Coroutine typingCoroutine;

        [Header("Events")]
        [Tooltip("This event is fired when the typewriter has finished revealing all characters.")]
        public UnityEvent onTypingComplete;

        [Header("Lite Version Info")]
        [Tooltip("🌟 UPGRADE TO FULL VERSION for <speed> tag support!\nDynamically control typing speed throughout your text.")]
        [TextArea(2, 3)]
        public string upgradeInfo = "Full version includes <speed> tag for dynamic speed control during typing!";

        void Awake()
        {
            animationEngine = GetComponent<PUAP_Core>();
        }

        void OnEnable()
        {
            if (playOnEnable)
            {
                StartTyping();
            }
        }

        public void StartTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            animationEngine.maxVisibleCharacters = 0;
            typingCoroutine = StartCoroutine(TypeText());
        }

        public void StopTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
        }

        public void CompleteInstantly()
        {
            StopTyping();
            animationEngine.maxVisibleCharacters = animationEngine.CharactersCount;
        }

        private IEnumerator TypeText()
        {
            // Wait for text to be processed
            while (animationEngine.CharactersCount == 0)
            {
                yield return null;
            }

            int characterIndex = 0;
            int actionIndex = 0;
            List<ActionMarker> actions = animationEngine.Actions;

            while (characterIndex < animationEngine.CharactersCount)
            {
                // Process action tags at current position
                while (actionIndex < actions.Count && actions[actionIndex].Index == characterIndex)
                {
                    ActionMarker currentAction = actions[actionIndex];

                    switch (currentAction.Name)
                    {
                        case "wait":
                            yield return new WaitForSeconds(currentAction.Value);
                            break;

                            // 🌟 UPGRADE TO FULL VERSION 🌟
                            // The <speed> tag is available in the full version!
                            // Dynamically control typing speed throughout your text.
                            // Example: Normal text <speed=2>faster text</speed> <speed=0.5>slower text</speed>
                            // 
                            // case "speed":
                            //     currentCharsPerSecond = charsPerSecond * currentAction.Value;
                            //     break;
                    }
                    actionIndex++;
                }

                // Reveal one more character
                characterIndex++;
                animationEngine.maxVisibleCharacters = characterIndex;

                // Wait before revealing the next character
                if (charsPerSecond > 0)
                {
                    yield return new WaitForSeconds(1.0f / charsPerSecond);
                }
            }

            typingCoroutine = null;
            onTypingComplete?.Invoke();
        }
    }
}