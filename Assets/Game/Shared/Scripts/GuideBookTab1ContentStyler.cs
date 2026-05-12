using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class GuideBookTab1ContentStyler : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform contentRoot;

    [Header("Palette")]
    [SerializeField] private Color titleColor = new Color(0.33f, 0.15f, 0.08f, 1f);
    [SerializeField] private Color bodyColor = new Color(0.18f, 0.14f, 0.11f, 1f);
    [SerializeField] private Color accentColor = new Color(0.62f, 0.25f, 0.12f, 1f);

    [Header("Typography")]
    [SerializeField] private float titleFontSize = 34f;
    [SerializeField] private float bodyFontSize = 24f;
    [SerializeField] private float bulletFontSize = 23f;
    [SerializeField] private FontStyles titleFontStyle = FontStyles.Bold;
    [SerializeField] private FontStyles bodyFontStyle = FontStyles.Normal;
    [SerializeField] private FontStyles bulletFontStyle = FontStyles.Bold;

    [ContextMenu("Apply Style")]
    public void ApplyStyle()
    {
        if (contentRoot == null)
        {
            contentRoot = transform as RectTransform;
        }

        if (contentRoot == null)
        {
            return;
        }

        TMP_Text[] texts = contentRoot.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string normalized = (text.text ?? string.Empty).Trim();
            bool isTitle = i == 0 || LooksLikeTitle(text, normalized);
            bool isBullet = LooksLikeBullet(normalized);

            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;

            if (isTitle)
            {
                StyleTitle(text, normalized);
                continue;
            }

            if (isBullet)
            {
                StyleBullet(text, normalized);
                continue;
            }

            StyleBody(text, normalized);
        }
    }

    private void Awake()
    {
        ApplyStyle();
    }

    private void OnEnable()
    {
        ApplyStyle();
    }

    private void StyleTitle(TMP_Text text, string content)
    {
        text.fontSize = titleFontSize;
        text.fontStyle = titleFontStyle;
        text.color = titleColor;
        text.lineSpacing = 4f;
        text.characterSpacing = 1.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.text = $"<b>{content}</b>";
    }

    private void StyleBody(TMP_Text text, string content)
    {
        text.fontSize = bodyFontSize;
        text.fontStyle = bodyFontStyle;
        text.color = bodyColor;
        text.lineSpacing = 10f;
        text.paragraphSpacing = 14f;
        text.characterSpacing = 0.4f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = HighlightKeywords(content);
    }

    private void StyleBullet(TMP_Text text, string content)
    {
        text.fontSize = bulletFontSize;
        text.fontStyle = bulletFontStyle;
        text.color = bodyColor;
        text.lineSpacing = 6f;
        text.paragraphSpacing = 8f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}><b>{content}</b></color>";
    }

    private bool LooksLikeTitle(TMP_Text text, string content)
    {
        if (text.name.ToLowerInvariant().Contains("title"))
        {
            return true;
        }

        return content.Length > 0 && content.Length <= 28;
    }

    private static bool LooksLikeBullet(string content)
    {
        return content.StartsWith("-") ||
               content.StartsWith("•") ||
               content.StartsWith("*") ||
               content.StartsWith("1.") ||
               content.StartsWith("2.") ||
               content.StartsWith("3.") ||
               content.StartsWith("4.") ||
               content.StartsWith("5.");
    }

    private string HighlightKeywords(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        string result = content;
        result = BoldWord(result, "người chơi");
        result = BoldWord(result, "hiện trường");
        result = BoldWord(result, "mục tiêu");
        result = BoldWord(result, "nhiệm vụ");
        result = BoldWord(result, "an toàn");
        result = BoldWord(result, "tool");
        result = BoldWord(result, "objective");
        result = BoldWord(result, "safety");
        return result;
    }

    private string BoldWord(string source, string word)
    {
        return source.Replace(word, $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}><b>{word}</b></color>");
    }
}
