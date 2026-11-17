using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Builds simple world-space UI that works with XR rays and poke (TrackedDeviceGraphicRaycaster).
    /// Positions are in canvas pixels from the canvas centre, y up.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new(0.07f, 0.07f, 0.09f, 0.94f);
        public static readonly Color SubPanelColor = new(0.12f, 0.12f, 0.15f, 1f);
        public static readonly Color ButtonColor = new(0.22f, 0.22f, 0.27f, 1f);
        public static readonly Color AccentColor = new(0.89f, 0.34f, 0.18f, 1f);
        public static readonly Color TextColor = new(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color QuietTextColor = new(0.68f, 0.68f, 0.72f, 1f);

        public static RectTransform Canvas(string name, Transform parent, Vector2 sizePx, float widthMeters,
            Vector3 localPosition, Quaternion localRotation, bool background = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizePx;
            rect.localPosition = localPosition;
            rect.localRotation = localRotation;
            rect.localScale = Vector3.one * (widthMeters / sizePx.x);

            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            if (background) Stretch(Image(rect, "Background", PanelColor, Vector2.zero, sizePx));
            return rect;
        }

        public static RectTransform Panel(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var image = Image(parent, name, color, position, size);
            return (RectTransform)image.transform;
        }

        public static Image Image(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Text(RectTransform parent, string name, string text, float fontSize,
            Vector2 position, Vector2 size, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
            FontStyles style = FontStyles.Normal, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.color = color ?? TextColor;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button Button(RectTransform parent, string name, string label, Vector2 position, Vector2 size,
            out TextMeshProUGUI labelText, Color? color = null, float fontSize = 30f)
        {
            var image = Image(parent, name, color ?? ButtonColor, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            labelText = Text((RectTransform)image.transform, "Label", label, fontSize, Vector2.zero, size,
                TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        public static Button Button(RectTransform parent, string name, string label, Vector2 position, Vector2 size,
            Color? color = null, float fontSize = 30f) =>
            Button(parent, name, label, position, size, out _, color, fontSize);

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(Graphic graphic)
        {
            var rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
