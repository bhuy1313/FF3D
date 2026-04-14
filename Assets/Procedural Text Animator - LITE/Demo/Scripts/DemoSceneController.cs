// In file: DemoSceneController.cs
// Copyright (c) 2025 Spanky
//
// This script manages the UI navigation for the Procedural Text Animator demo scene
// by enabling and disabling "page" GameObjects from a list.

using UnityEngine;
using System.Collections.Generic;

namespace ProceduralUIEffects
{
    public class DemoSceneController : MonoBehaviour
    {
        [Header("Page Management")]
        [Tooltip("Add all of your effect 'page' GameObjects to this list. The order determines the Next/Previous sequence.")]
        public List<GameObject> effectPages;

        [Tooltip("The page index to display when the scene first starts.")]
        public int startingPageIndex = 0;

        private int currentPageIndex;

        void Start()
        {
            for (int i = 0; i < effectPages.Count; i++)
            {
                if (effectPages[i] != null) effectPages[i].SetActive(false);
            }

            currentPageIndex = Mathf.Clamp(startingPageIndex, 0, effectPages.Count - 1);
            ShowPageByIndex(currentPageIndex);
        }

        public void ShowPageByIndex(int pageIndex)
        {
            if (effectPages[currentPageIndex] != null)
            {
                effectPages[currentPageIndex].SetActive(false);
            }

            currentPageIndex = pageIndex;
            if (effectPages[currentPageIndex] != null)
            {
                effectPages[currentPageIndex].SetActive(true);
            }
        }

        public void OnClick_ShowNextPage()
        {
            int nextPageIndex = (currentPageIndex + 1) % effectPages.Count;
            ShowPageByIndex(nextPageIndex);
        }

        public void OnClick_ShowPreviousPage()
        {
            int previousPageIndex = currentPageIndex - 1;
            if (previousPageIndex < 0)
            {
                previousPageIndex = effectPages.Count - 1;
            }
            ShowPageByIndex(previousPageIndex);
        }
    }
}