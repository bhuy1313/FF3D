using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public class SwitchToggleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform handle;

    [Header("Colors")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.75f, 0.35f, 1f);
    [SerializeField] private Color offColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Animation")]
    [SerializeField, Min(0f)] private float duration = 0.12f;
    [SerializeField, Min(0f)] private float padding = 4f;
    [SerializeField] private bool applyOnStart = true;

    private Coroutine animateCoroutine;

    private void Awake()
    {
        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }
    }

    private void OnEnable()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyVisual(toggle != null && toggle.isOn, true);
        }
    }

    private void OnDisable()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        StopAnimation();
    }

    private void OnValidate()
    {
        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }

        if (!Application.isPlaying && applyOnStart)
        {
            ApplyVisual(toggle != null && toggle.isOn, true);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (applyOnStart)
        {
            ApplyVisual(toggle != null && toggle.isOn, true);
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        ApplyVisual(isOn, false);
    }

    private void ApplyVisual(bool isOn, bool instant)
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        float offX;
        float onX;
        CalculateHandlePositions(out offX, out onX);

        float targetX = isOn ? onX : offX;
        Color targetColor = isOn ? onColor : offColor;

        if (instant || duration <= 0f)
        {
            SetHandleX(targetX);
            background.color = targetColor;
            StopAnimation();
            return;
        }

        StopAnimation();
        animateCoroutine = StartCoroutine(AnimateTo(targetX, targetColor));
    }

    private IEnumerator AnimateTo(float targetX, Color targetColor)
    {
        float startX = handle.anchoredPosition.x;
        Color startColor = background.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            SetHandleX(Mathf.Lerp(startX, targetX, t));
            background.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        SetHandleX(targetX);
        background.color = targetColor;
        animateCoroutine = null;
    }

    private void CalculateHandlePositions(out float offX, out float onX)
    {
        RectTransform backgroundRect = background.rectTransform;

        float backgroundWidth = backgroundRect.rect.width;
        float handleWidth = handle.rect.width;

        float travel = Mathf.Max(0f, backgroundWidth - handleWidth - (padding * 2f));
        float halfTravel = travel * 0.5f;

        offX = -halfTravel;
        onX = halfTravel;
    }

    private void SetHandleX(float x)
    {
        Vector2 anchoredPos = handle.anchoredPosition;
        anchoredPos.x = x;
        handle.anchoredPosition = anchoredPos;
    }

    private bool HasRequiredReferences()
    {
        return toggle != null && background != null && handle != null;
    }

    private void StopAnimation()
    {
        if (animateCoroutine == null)
        {
            return;
        }

        StopCoroutine(animateCoroutine);
        animateCoroutine = null;
    }
}
