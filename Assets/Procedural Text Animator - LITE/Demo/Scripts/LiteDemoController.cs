// LiteDemoController.cs
// Copyright (c) 2025 Spanky
//
// Simple demo scene controller for Procedural Text Animator - LITE

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ProceduralUIEffects.Lite
{
    /// <summary>
    /// Simple page-based demo controller for showcasing Lite effects
    /// </summary>
    public class LiteDemoController : MonoBehaviour
    {
        [Header("Demo Pages")]
        [Tooltip("Add your demo page GameObjects here. Only one will be active at a time.")]
        public List<GameObject> demoPages = new List<GameObject>();

        [Header("Navigation UI")]
        public Button nextButton;
        public Button previousButton;
        public Text pageIndicatorText;

        [Header("Settings")]
        [Tooltip("Which page index to show when scene starts (0-based).")]
        public int startingPageIndex = 0;

        private int currentPageIndex = 0;

        void Start()
        {
            // Validate
            if (demoPages.Count == 0)
            {
                Debug.LogWarning("LiteDemoController: No demo pages assigned!");
                return;
            }

            // Wire up buttons
            if (nextButton != null)
                nextButton.onClick.AddListener(ShowNextPage);

            if (previousButton != null)
                previousButton.onClick.AddListener(ShowPreviousPage);

            // Initialize
            currentPageIndex = Mathf.Clamp(startingPageIndex, 0, demoPages.Count - 1);
            ShowPage(currentPageIndex);
        }

        public void ShowNextPage()
        {
            int nextIndex = (currentPageIndex + 1) % demoPages.Count;
            ShowPage(nextIndex);
        }

        public void ShowPreviousPage()
        {
            int prevIndex = currentPageIndex - 1;
            if (prevIndex < 0)
                prevIndex = demoPages.Count - 1;
            ShowPage(prevIndex);
        }

        public void ShowPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= demoPages.Count)
                return;

            // Deactivate current page
            if (currentPageIndex >= 0 && currentPageIndex < demoPages.Count && demoPages[currentPageIndex] != null)
            {
                demoPages[currentPageIndex].SetActive(false);
            }

            // Activate new page
            currentPageIndex = pageIndex;
            if (demoPages[currentPageIndex] != null)
            {
                demoPages[currentPageIndex].SetActive(true);
            }

            // Update page indicator
            UpdatePageIndicator();
        }

        private void UpdatePageIndicator()
        {
            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"Page {currentPageIndex + 1} of {demoPages.Count}";
            }
        }

        #region Public API for Button Events

        // These methods can be called from UI buttons directly via Inspector

        public void ShowWavePage() => ShowPage(0);
        public void ShowShakePage() => ShowPage(1);
        public void ShowPulsePage() => ShowPage(2);
        public void ShowTypewriterPage() => ShowPage(3);

        #endregion
    }
}