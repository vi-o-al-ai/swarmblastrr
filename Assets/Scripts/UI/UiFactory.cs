using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Swarmblastrr.UI
{
    /// <summary>
    /// Builds legacy uGUI widgets (Canvas, Text, Button, Image) from code so the project runs with
    /// zero prefabs. Every screen in <c>Swarmblastrr.UI</c> is assembled from these helpers.
    /// </summary>
    public static class UiFactory
    {
        private static readonly Vector2 _center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 _referenceResolution = new Vector2(1080f, 1920f);

        private static Font _font;

        /// <summary>
        /// Creates the root <see cref="Canvas"/>: ScreenSpaceOverlay, <see cref="CanvasScaler"/> set to
        /// ScaleWithScreenSize 1080x1920 (match 0.5), a <see cref="GraphicRaycaster"/>, plus an
        /// <see cref="EventSystem"/> with the right input module if the scene has none.
        /// </summary>
        public static Canvas CreateRootCanvas()
        {
            GameObject go = new GameObject("UI Root", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// The built-in runtime font ("LegacyRuntime.ttf"), cached. Falls back to an OS font so a
        /// <see cref="Text"/> never ends up with a null font.
        /// </summary>
        public static Font DefaultFont()
        {
            if (_font != null)
            {
                return _font;
            }

            _font = LoadBuiltinFont("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = LoadBuiltinFont("Arial.ttf");
            }

            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
            }

            return _font;
        }

        /// <summary>
        /// Creates a <see cref="Text"/> under <paramref name="parent"/>. The RectTransform is stretched
        /// to fill the parent; callers reposition it with <see cref="Anchor"/>. Overflow is allowed
        /// in both directions and the text does not block raycasts.
        /// </summary>
        public static Text CreateText(Transform parent, string text, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, Vector2.zero, Vector2.one, _center, Vector2.zero, Vector2.zero);

            Text label = go.AddComponent<Text>();
            label.font = DefaultFont();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Creates a <see cref="Button"/> of the given size, centred in <paramref name="parent"/>, with
        /// a solid <see cref="Image"/> background tinted <paramref name="bg"/> and a bold white label.
        /// <paramref name="onClick"/> may be null.
        /// </summary>
        public static Button CreateButton(Transform parent, string label, Vector2 size, Color bg, UnityAction onClick)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, _center, _center, _center, Vector2.zero, size);

            Image image = go.AddComponent<Image>();
            image.color = bg;
            image.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.42f), 20, 96);
            Text text = CreateText(rt, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            text.name = "Label";
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        /// <summary>
        /// Creates a solid <see cref="Image"/> stretched to fill <paramref name="parent"/>. It blocks
        /// raycasts so it can act as a modal backdrop.
        /// </summary>
        public static Image CreatePanel(Transform parent, Color color)
        {
            GameObject go = new GameObject("Panel", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, Vector2.zero, Vector2.one, _center, Vector2.zero, Vector2.zero);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// Sets anchors, pivot, anchored position and size delta in one call and returns
        /// <paramref name="rt"/> for chaining.
        /// </summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            if (rt == null)
            {
                return null;
            }

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Font LoadBuiltinFont(string name)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(name);
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
