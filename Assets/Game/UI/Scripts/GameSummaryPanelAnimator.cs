using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF3D.UI
{
    [System.Serializable]
    public class StatRowElement
    {
        [Tooltip(
            "Optional manual row reference. When auto-discovery is enabled, missing rows are picked up from Performance_Area."
        )]
        public RectTransform rowRect;

        [Tooltip("Optional CanvasGroup for the row. One is added automatically when missing.")]
        public CanvasGroup rowCanvasGroup;
    }

    public class GameSummaryPanelAnimator : MonoBehaviour
    {
        [Header("Manual References")]
        [SerializeField]
        private bool autoDiscoverReferences = true;

        [SerializeField]
        private RectTransform contentContainer;
        public CanvasGroup headerGroup;

        [SerializeField]
        private CanvasGroup performanceGroup;
        public StatRowElement[] statRows;

        [SerializeField]
        private RectTransform objectivesArea;

        [SerializeField]
        private CanvasGroup objectivesGroup;
        public RectTransform rankStamp;
        public TextMeshProUGUI scoreText;
        public CanvasGroup buttonGroup;

        [Header("Animation Settings")]
        [SerializeField]
        private float headerFadeDuration = 0.5f;

        [SerializeField]
        private float rowSlideDuration = 0.4f;

        [SerializeField]
        private float rowInterval = 0.15f;

        [SerializeField]
        private float postRowsDelay = 0.35f;

        [SerializeField]
        private float scoreAnimDuration = 1f;

        [SerializeField]
        private float objectivesFadeDuration = 0.3f;

        [SerializeField]
        private float buttonFadeDuration = 0.3f;

        [SerializeField]
        private float slideStartOffsetX = -50f;

        private const string HeaderAreaName = "Header_Area";
        private const string ContentContainerName = "Content_Container";
        private const string PerformanceAreaName = "Performance_Area";
        private const string ObjectivesAreaName = "Objectives_Area";
        private const string ButtonsAreaName = "Buttons_Area";
        private const string ScoreRowName = "ScoreRow";
        private const string ScoreValueName = "Txt_Value";

        private readonly List<RowRuntimeState> resolvedStatRows = new List<RowRuntimeState>();

        private Coroutine animationCoroutine;
        private RowRuntimeState objectivesState;
        private RowRuntimeState buttonState;
        private string cachedScoreText = string.Empty;

        private sealed class RowRuntimeState
        {
            public RectTransform rootRect;
            public RectTransform animatedRect;
            public CanvasGroup canvasGroup;
            public Vector2 anchoredPosition;
            public Vector3 scale;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            DOTween.Kill(this);
            animationCoroutine = StartCoroutine(PlayAnimations());
        }

        private void OnDisable()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            DOTween.Kill(this);
            RestoreResolvedState();
        }

        private IEnumerator PlayAnimations()
        {
            yield return null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            ResolveReferences();
            CacheState();
            PrepareInitialAnimationState();

            if (headerGroup != null)
            {
                headerGroup
                    .DOFade(1f, headerFadeDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true)
                    .SetTarget(this);
            }

            if (performanceGroup != null)
            {
                performanceGroup
                    .DOFade(1f, headerFadeDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true)
                    .SetTarget(this);
            }

            yield return new WaitForSecondsRealtime(headerFadeDuration);

            for (int i = 0; i < resolvedStatRows.Count; i++)
            {
                AnimateRowIn(resolvedStatRows[i]);
                yield return new WaitForSecondsRealtime(rowInterval);
            }

            if (postRowsDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(postRowsDelay);
            }

            yield return AnimateScore();

            if (objectivesGroup != null && objectivesArea != null)
            {
                AnimateRowIn(objectivesState, objectivesFadeDuration);
                yield return new WaitForSecondsRealtime(objectivesFadeDuration);
            }

            if (buttonState != null && buttonState.animatedRect != null && buttonState.canvasGroup != null)
            {
                if (buttonState.animatedRect != buttonState.rootRect)
                {
                    buttonState.animatedRect.localScale = buttonState.scale * 0.9f;
                    buttonState.animatedRect
                        .DOScale(buttonState.scale, buttonFadeDuration)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true)
                        .SetTarget(this);
                }

                buttonState
                    .canvasGroup
                    .DOFade(1f, buttonFadeDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true)
                    .SetTarget(this);

                yield return new WaitForSecondsRealtime(buttonFadeDuration);
            }

            RestoreResolvedState();

            if (buttonState != null && buttonState.animatedRect != null && buttonState.animatedRect != buttonState.rootRect)
            {
                buttonState.animatedRect
                    .DOScale(buttonState.scale * 1.03f, 1.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetTarget(this);
            }

            animationCoroutine = null;
        }

        private void ResolveReferences()
        {
            if (autoDiscoverReferences)
            {
                RectTransform headerRect = FindDescendantByName(transform, HeaderAreaName) as RectTransform;
                RectTransform performanceRect =
                    FindDescendantByName(transform, PerformanceAreaName) as RectTransform;
                RectTransform buttonsRect =
                    FindDescendantByName(transform, ButtonsAreaName) as RectTransform;

                if (contentContainer == null)
                {
                    contentContainer =
                        FindDescendantByName(transform, ContentContainerName) as RectTransform;
                }

                headerGroup = ResolveCanvasGroup(headerGroup, headerRect);
                performanceGroup = ResolveCanvasGroup(performanceGroup, performanceRect);

                if (objectivesArea == null)
                {
                    objectivesArea =
                        FindDescendantByName(transform, ObjectivesAreaName) as RectTransform;
                }

                if (objectivesArea != null)
                {
                    objectivesGroup = ResolveCanvasGroup(objectivesGroup, objectivesArea);
                }

                buttonGroup = ResolveCanvasGroup(buttonGroup, buttonsRect);

                if (scoreText == null)
                {
                    scoreText = FindTextInNamedRow(transform, ScoreRowName, ScoreValueName);
                }
            }

            ResolveStatRows();
        }

        private void ResolveStatRows()
        {
            resolvedStatRows.Clear();

            if (statRows != null)
            {
                for (int i = 0; i < statRows.Length; i++)
                {
                    AddRowState(statRows[i]?.rowRect, statRows[i]?.rowCanvasGroup);
                }
            }

            RectTransform performanceRoot =
                FindDescendantByName(transform, PerformanceAreaName) as RectTransform;
            if (performanceRoot != null)
            {
                CollectStatRows(performanceRoot);
            }

            resolvedStatRows.Sort(CompareRowOrder);
        }

        private void CacheState()
        {
            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < resolvedStatRows.Count; i++)
            {
                RowRuntimeState row = resolvedStatRows[i];
                if (row.animatedRect == null)
                {
                    continue;
                }

                row.anchoredPosition = row.animatedRect.anchoredPosition;
                row.scale = row.animatedRect.localScale;
            }

            if (objectivesArea != null)
            {
                RectTransform objectivesAnimationRect = ResolveAnimationRect(objectivesArea);
                objectivesState = new RowRuntimeState
                {
                    rootRect = objectivesArea,
                    animatedRect = objectivesAnimationRect,
                    canvasGroup =
                        objectivesGroup != null
                            ? objectivesGroup
                            : GetOrAddCanvasGroup(objectivesAnimationRect),
                    anchoredPosition = objectivesAnimationRect.anchoredPosition,
                    scale = objectivesAnimationRect.localScale,
                };
            }
            else
            {
                objectivesState = null;
            }

            RectTransform buttonRoot = FindDescendantByName(transform, ButtonsAreaName) as RectTransform;
            RectTransform buttonAnimationRect = ResolveAnimationRect(buttonRoot);
            if (buttonAnimationRect != null)
            {
                buttonState = new RowRuntimeState
                {
                    rootRect = buttonRoot,
                    animatedRect = buttonAnimationRect,
                    canvasGroup =
                        buttonGroup != null ? buttonGroup : GetOrAddCanvasGroup(buttonAnimationRect),
                    anchoredPosition = buttonAnimationRect.anchoredPosition,
                    scale = buttonAnimationRect.localScale,
                };
            }
            else
            {
                buttonState = null;
            }

            if (scoreText != null)
            {
                cachedScoreText = scoreText.text;
            }
        }

        private void PrepareInitialAnimationState()
        {
            if (headerGroup != null)
            {
                headerGroup.alpha = 0f;
            }

            if (performanceGroup != null)
            {
                performanceGroup.alpha = 0f;
            }

            for (int i = 0; i < resolvedStatRows.Count; i++)
            {
                RowRuntimeState row = resolvedStatRows[i];
                if (row.animatedRect == null)
                {
                    continue;
                }

                row.animatedRect.anchoredPosition =
                    row.anchoredPosition + new Vector2(slideStartOffsetX, 0f);
                row.animatedRect.localScale = row.scale;

                if (row.canvasGroup != null)
                {
                    row.canvasGroup.alpha = 0f;
                }
            }

            if (objectivesState != null && objectivesState.animatedRect != null)
            {
                objectivesState.animatedRect.anchoredPosition =
                    objectivesState.anchoredPosition + new Vector2(slideStartOffsetX, 0f);
                objectivesState.animatedRect.localScale = objectivesState.scale;

                if (objectivesState.canvasGroup != null)
                {
                    objectivesState.canvasGroup.alpha = 0f;
                }
            }

            if (buttonState != null && buttonState.canvasGroup != null)
            {
                buttonState.canvasGroup.alpha = 0f;
            }

            if (scoreText != null)
            {
                scoreText.text = BuildZeroScoreText(cachedScoreText);
            }
        }

        private void RestoreResolvedState()
        {
            if (headerGroup != null)
            {
                headerGroup.alpha = 1f;
            }

            if (performanceGroup != null)
            {
                performanceGroup.alpha = 1f;
            }

            for (int i = 0; i < resolvedStatRows.Count; i++)
            {
                RowRuntimeState row = resolvedStatRows[i];
                if (row.animatedRect == null)
                {
                    continue;
                }

                row.animatedRect.anchoredPosition = row.anchoredPosition;
                row.animatedRect.localScale = row.scale;

                if (row.canvasGroup != null)
                {
                    row.canvasGroup.alpha = 1f;
                }
            }

            if (objectivesState != null && objectivesState.animatedRect != null)
            {
                objectivesState.animatedRect.anchoredPosition = objectivesState.anchoredPosition;
                objectivesState.animatedRect.localScale = objectivesState.scale;

                if (objectivesState.canvasGroup != null)
                {
                    objectivesState.canvasGroup.alpha = 1f;
                }
            }

            if (buttonState != null && buttonState.animatedRect != null)
            {
                buttonState.animatedRect.anchoredPosition = buttonState.anchoredPosition;
                buttonState.animatedRect.localScale = buttonState.scale;

                if (buttonState.canvasGroup != null)
                {
                    buttonState.canvasGroup.alpha = 1f;
                }
            }

            if (scoreText != null && !string.IsNullOrEmpty(cachedScoreText))
            {
                scoreText.text = cachedScoreText;
            }
        }

        private IEnumerator AnimateScore()
        {
            if (scoreText == null)
            {
                yield break;
            }

            ScoreTarget scoreTarget = ParseScoreTarget(cachedScoreText);
            if (!scoreTarget.hasValue)
            {
                scoreText.text = cachedScoreText;
                yield break;
            }

            if (scoreTarget.value <= 0)
            {
                scoreText.text = "0" + scoreTarget.suffix;
                yield break;
            }

            int currentScore = 0;
            scoreText.text = "0" + scoreTarget.suffix;

            scoreText.transform.DOPunchScale(Vector3.one * 0.2f, scoreAnimDuration, 5, 1f).SetUpdate(true).SetTarget(this);

            DOTween
                .To(
                    () => currentScore,
                    x =>
                    {
                        currentScore = x;
                        scoreText.text = currentScore.ToString() + scoreTarget.suffix;
                    },
                    scoreTarget.value,
                    scoreAnimDuration
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(this);

            yield return new WaitForSecondsRealtime(scoreAnimDuration);
            scoreText.text = cachedScoreText;

            scoreText.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 6, 1f).SetUpdate(true).SetTarget(this);
        }

        private void AnimateRowIn(RowRuntimeState rowState, float duration = -1f)
        {
            if (rowState == null || rowState.animatedRect == null)
            {
                return;
            }

            float resolvedDuration = duration > 0f ? duration : rowSlideDuration;

            rowState
                .animatedRect.DOAnchorPos(rowState.anchoredPosition, resolvedDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetTarget(this);

            if (rowState.canvasGroup != null)
            {
                rowState
                    .canvasGroup.DOFade(1f, resolvedDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true)
                    .SetTarget(this);
            }
        }

        private void AddRowState(RectTransform rowRect, CanvasGroup rowCanvasGroup)
        {
            if (rowRect == null || ContainsRow(rowRect))
            {
                return;
            }

            RectTransform animationRect = ResolveAnimationRect(rowRect);

            resolvedStatRows.Add(
                new RowRuntimeState
                {
                    rootRect = rowRect,
                    animatedRect = animationRect,
                    canvasGroup =
                        rowCanvasGroup != null
                            ? rowCanvasGroup
                            : GetOrAddCanvasGroup(animationRect),
                    anchoredPosition = animationRect.anchoredPosition,
                    scale = animationRect.localScale,
                }
            );
        }

        private bool ContainsRow(RectTransform rowRect)
        {
            for (int i = 0; i < resolvedStatRows.Count; i++)
            {
                if (resolvedStatRows[i].rootRect == rowRect)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareRowOrder(RowRuntimeState left, RowRuntimeState right)
        {
            return CompareHierarchyOrder(left?.rootRect, right?.rootRect);
        }

        private void CollectStatRows(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                RectTransform childRect = child as RectTransform;
                if (childRect != null)
                {
                    string childName = childRect.name;
                    if (childName.StartsWith("StatRow_") || childName == ScoreRowName)
                    {
                        AddRowState(childRect, childRect.GetComponent<CanvasGroup>());
                        continue;
                    }
                }

                CollectStatRows(child);
            }
        }

        private static CanvasGroup GetOrAddCanvasGroup(RectTransform target)
        {
            if (target == null)
            {
                return null;
            }

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private static CanvasGroup ResolveCanvasGroup(CanvasGroup current, RectTransform root)
        {
            RectTransform target = ResolveAnimationRect(root);
            if (target == null)
            {
                return current;
            }

            if (current != null && current.transform == target)
            {
                return current;
            }

            return GetOrAddCanvasGroup(target);
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendantByName(root.GetChild(i), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static RectTransform ResolveAnimationRect(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            RectTransform namedWrapper = FindNamedWrapperChild(root);
            if (namedWrapper != null)
            {
                return namedWrapper;
            }

            return root.childCount == 1 ? root.GetChild(0) as RectTransform ?? root : root;
        }

        private static int CompareHierarchyOrder(Transform left, Transform right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            List<int> leftPath = GetHierarchyPath(left);
            List<int> rightPath = GetHierarchyPath(right);
            int depth = Mathf.Min(leftPath.Count, rightPath.Count);

            for (int i = 0; i < depth; i++)
            {
                int comparison = leftPath[i].CompareTo(rightPath[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftPath.Count.CompareTo(rightPath.Count);
        }

        private static List<int> GetHierarchyPath(Transform current)
        {
            List<int> path = new List<int>();
            while (current != null)
            {
                path.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            path.Reverse();
            return path;
        }

        private static RectTransform FindNamedWrapperChild(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                string childName = child.name;
                if (
                    childName.IndexOf("wrapper", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || childName.IndexOf("visual", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || childName.IndexOf("content", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || childName.IndexOf("body", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || childName.IndexOf("container", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || childName.IndexOf("root", System.StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    return child;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindTextInNamedRow(
            Transform root,
            string rowName,
            string textName
        )
        {
            Transform row = FindDescendantByName(root, rowName);
            if (row == null)
            {
                return null;
            }

            Transform textTransform = FindDescendantByName(row, textName);
            return textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;
        }

        private static string BuildZeroScoreText(string originalText)
        {
            ScoreTarget scoreTarget = ParseScoreTarget(originalText);
            return scoreTarget.hasValue ? "0" + scoreTarget.suffix : originalText;
        }

        private static ScoreTarget ParseScoreTarget(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
            {
                return default(ScoreTarget);
            }

            Match match = Regex.Match(rawText, @"^\s*(\d+)(.*)$");
            if (!match.Success)
            {
                return default(ScoreTarget);
            }

            int value;
            if (!int.TryParse(match.Groups[1].Value, out value))
            {
                return default(ScoreTarget);
            }

            return new ScoreTarget
            {
                hasValue = true,
                value = value,
                suffix = match.Groups[2].Value,
            };
        }

        private struct ScoreTarget
        {
            public bool hasValue;
            public int value;
            public string suffix;
        }
    }
}
