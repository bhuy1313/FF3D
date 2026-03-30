using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform root;
    public RectTransform shine;
    public UnityEngine.UI.Image leftBorder;
    public TMP_Text label;
    public Color normalBorder = new Color(0.33f, 0.33f, 0.33f, 1f);
    public Color hoverBorder = new Color(1f, 0.27f, 0f, 1f);

    Vector2 _rootStart;
    Vector3 _scaleStart;

    void Awake()
    {
        if (!root) root = (RectTransform)transform;
        _rootStart = root.anchoredPosition;
        _scaleStart = root.localScale;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        leftBorder.color = hoverBorder;
        root.anchoredPosition = _rootStart + new Vector2(10f, 0f);
        // shine sweep
        if (shine) StartCoroutine(ShineSweep());
    }

    public void OnPointerExit(PointerEventData e)
    {
        leftBorder.color = normalBorder;
        root.anchoredPosition = _rootStart;
        root.localScale = _scaleStart;
    }

    public void OnPointerDown(PointerEventData e)
    {
        root.localScale = _scaleStart * 0.98f;
    }

    public void OnPointerUp(PointerEventData e)
    {
        root.localScale = _scaleStart;
    }

    System.Collections.IEnumerator ShineSweep()
    {
        shine.gameObject.SetActive(true);
        var rt = shine;
        float w = ((RectTransform)transform).rect.width;
        rt.anchoredPosition = new Vector2(-w, 0);
        float t = 0;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Lerp(-w, w, t / 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            yield return null;
        }
        shine.gameObject.SetActive(false);
    }
}
