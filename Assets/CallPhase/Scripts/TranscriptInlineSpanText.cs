using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TranscriptInlineSpanText : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text targetText;

    private TranscriptLogItem owner;
    private Camera eventCamera;
    private int hoveredLinkIndex = -1;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(TranscriptLogItem transcriptLogItem, TMP_Text textTarget)
    {
        owner = transcriptLogItem;
        targetText = textTarget;
        hoveredLinkIndex = -1;
        ResolveReferences();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || targetText == null)
        {
            return;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(targetText, eventData.position, ResolveEventCamera(eventData));
        if (linkIndex >= 0)
        {
            owner.HandleInlineSpanClicked(linkIndex);
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner == null || targetText == null)
        {
            return;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(targetText, eventData.position, ResolveEventCamera(eventData));
        if (linkIndex == hoveredLinkIndex)
        {
            return;
        }

        hoveredLinkIndex = linkIndex;
        owner.HandleInlineSpanHovered(hoveredLinkIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoveredLinkIndex < 0 || owner == null)
        {
            return;
        }

        hoveredLinkIndex = -1;
        owner.HandleInlineSpanHovered(-1);
    }

    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (eventCamera == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }
    }

    private Camera ResolveEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera;
        }

        if (eventData != null && eventData.enterEventCamera != null)
        {
            return eventData.enterEventCamera;
        }

        return eventCamera;
    }
}
