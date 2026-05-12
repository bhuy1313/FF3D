using UnityEngine;
using UnityEngine.UI;

public static class CallPhaseFunctionButtonVisuals
{
    public static readonly Color32 ActiveColor = new Color32(0x00, 0x00, 0xFF, 0xFF);
    public static readonly Color32 AssessRiskActiveColor = new Color32(0xFF, 0x45, 0x00, 0xFF);
    public static readonly Color32 InactiveColor = new Color32(0x8A, 0x8A, 0x8A, 0xFF);

    public static void Apply(Button button, bool isActive)
    {
        Apply(button, isActive, ActiveColor);
    }

    public static void Apply(Button button, bool isActive, Color32 activeColor)
    {
        if (button == null)
        {
            return;
        }

        Color targetColor = isActive ? activeColor : InactiveColor;
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.r = targetColor.r;
            color.g = targetColor.g;
            color.b = targetColor.b;
            graphic.color = color;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.selectedColor = targetColor;
        colors.pressedColor = targetColor;
        colors.disabledColor = InactiveColor;
        button.colors = colors;
    }
}
