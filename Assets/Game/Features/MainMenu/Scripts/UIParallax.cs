using UnityEngine;

public class UIParallax : MonoBehaviour
{
    public RectTransform target;
    public float maxOffset = 20f;
    public float smooth = 8f;

    Vector2 _startPos;
    Vector2 _vel;

    void Start()
    {
        if (!target) target = (RectTransform)transform;
        _startPos = target.anchoredPosition;
    }

    void Update()
    {
        Vector2 mouse = Input.mousePosition;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 delta = (mouse - center) / center; // -1..1
        Vector2 desired = _startPos - delta * maxOffset;

        target.anchoredPosition = Vector2.SmoothDamp(target.anchoredPosition, desired, ref _vel, 1f / smooth);
    }
}
