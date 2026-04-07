using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class SlantedPanelGraphic : MaskableGraphic
{
    public enum SlantSide
    {
        Left = 0,
        Right = 1
    }

    [SerializeField] private SlantSide slantSide = SlantSide.Right;
    [SerializeField] private float topInset;
    [SerializeField] private float bottomInset;

    public void SetShape(SlantSide side, float top, float bottom)
    {
        slantSide = side;
        topInset = Mathf.Max(0f, top);
        bottomInset = Mathf.Max(0f, bottom);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float left = rect.xMin;
        float right = rect.xMax;
        float bottom = rect.yMin;
        float top = rect.yMax;

        float clampedTopInset = Mathf.Clamp(topInset, 0f, rect.width);
        float clampedBottomInset = Mathf.Clamp(bottomInset, 0f, rect.width);

        Vector2 topLeft = new Vector2(left, top);
        Vector2 bottomLeft = new Vector2(left, bottom);
        Vector2 topRight = new Vector2(right, top);
        Vector2 bottomRight = new Vector2(right, bottom);

        if (slantSide == SlantSide.Left)
        {
            topLeft.x += clampedTopInset;
            bottomLeft.x += clampedBottomInset;
        }
        else
        {
            topRight.x -= clampedTopInset;
            bottomRight.x -= clampedBottomInset;
        }

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = topLeft;
        vh.AddVert(vertex);

        vertex.position = topRight;
        vh.AddVert(vertex);

        vertex.position = bottomRight;
        vh.AddVert(vertex);

        vertex.position = bottomLeft;
        vh.AddVert(vertex);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
