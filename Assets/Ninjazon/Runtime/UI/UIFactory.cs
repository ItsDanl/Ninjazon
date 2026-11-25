using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ninjazon.Runtime.UI
{
    public static class UITheme
    {
        public static readonly Color Night = new(0.025f, 0.035f, 0.09f, 1f);
        public static readonly Color Panel = new(0.04f, 0.07f, 0.14f, 0.94f);
        public static readonly Color Cyan = new(0.05f, 0.95f, 1f, 1f);
        public static readonly Color Magenta = new(1f, 0.08f, 0.62f, 1f);
        public static readonly Color Amber = new(1f, 0.68f, 0.08f, 1f);
        public static readonly Color Text = new(0.9f, 0.98f, 1f, 1f);
        public static readonly Color Muted = new(0.5f, 0.67f, 0.76f, 1f);
    }

    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (lastArea != Screen.safeArea || lastSize.x != Screen.width || lastSize.y != Screen.height) Apply();
        }

        private void Apply()
        {
            var rect = transform as RectTransform;
            if (rect == null || Screen.width <= 0 || Screen.height <= 0) return;
            lastArea = Screen.safeArea;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            rect.anchorMin = new Vector2(lastArea.xMin / Screen.width, lastArea.yMin / Screen.height);
            rect.anchorMax = new Vector2(lastArea.xMax / Screen.width, lastArea.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    public static class UIFactory
    {
        public static Canvas CreateCanvas(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.55f;

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safe.transform.SetParent(root.transform, false);
            Stretch(safe.GetComponent<RectTransform>());
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static CanvasGroup CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<CanvasGroup>();
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color color, Vector2 anchoredPosition, Vector2 size, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.85f);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1.25f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            CreateText(go.transform, "Label", label, 40f, UITheme.Night, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        public static Button CreateBackButton(Transform parent, string name, UnityAction action)
        {
            var button = CreateButton(parent, name, "‹  BACK", UITheme.Cyan, Vector2.zero, new Vector2(280f, 92f), action);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(150f, -34f);
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, string label, float value, Vector2 position, UnityAction<float> changed)
        {
            CreateText(parent, name + "Label", label, 30f, UITheme.Text, TextAlignmentOptions.Left, new Vector2(0.15f, 0.5f), new Vector2(0.48f, 0.5f), new Vector2(0f, position.y - 35f), new Vector2(0f, position.y + 35f));
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.68f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, position.y);
            rect.sizeDelta = new Vector2(360f, 56f);

            var background = CreateImage(root.transform, "Background", new Color(0.1f, 0.18f, 0.28f, 1f));
            Stretch(background.rectTransform);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 12f);
            var fill = CreateImage(fillArea.transform, "Fill", UITheme.Cyan);
            Stretch(fill.rectTransform);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 12f);
            var handle = CreateImage(handleArea.transform, "Handle", UITheme.Text);
            handle.rectTransform.sizeDelta = new Vector2(44f, 72f);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.value = value;
            if (changed != null) slider.onValueChanged.AddListener(changed);
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, string label, bool value, float y, UnityAction<bool> changed)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(760f, 70f);
            var box = CreateImage(root.transform, "Box", new Color(0.1f, 0.18f, 0.28f, 1f));
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            box.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            box.rectTransform.sizeDelta = new Vector2(58f, 58f);
            var check = CreateImage(box.transform, "Checkmark", UITheme.Cyan);
            Stretch(check.rectTransform, 10f);
            CreateText(root.transform, "Label", label, 32f, UITheme.Text, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(100f, 0f), Vector2.zero);
            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            if (changed != null) toggle.onValueChanged.AddListener(changed);
            return toggle;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
