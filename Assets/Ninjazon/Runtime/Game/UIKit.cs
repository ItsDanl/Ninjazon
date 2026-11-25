using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ninjazon.Game
{
    // ================================================================ TWEENS ==
    public enum Ease { Linear, OutCubic, InCubic, InOutSine, OutBack, OutElastic }

    /// <summary>
    /// Tiny tween engine. A "tween" animates a value from 0 to 1 over time and
    /// calls your function with the eased value. Uses unscaled time so menus
    /// still animate while the game is paused.
    /// </summary>
    public sealed class Tw : MonoBehaviour
    {
        private sealed class Tween
        {
            public float duration, elapsed, delay;
            public Ease ease;
            public Action<float> apply;
            public Action done;
            public object key;
            public bool killed;
        }

        private static Tw instance;
        private readonly List<Tween> tweens = new();
        private readonly List<Tween> adding = new();

        public static void Ensure(GameObject host)
        {
            if (instance == null) instance = host.AddComponent<Tw>();
        }

        public static float Evaluate(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InCubic: return t * t * t;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                case Ease.OutElastic:
                    if (t <= 0f || t >= 1f) return t;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                default: return t;
            }
        }

        public static void Value(float duration, Action<float> apply, Ease ease = Ease.OutCubic, float delay = 0f, Action done = null, object key = null)
        {
            if (instance == null) { apply(1f); done?.Invoke(); return; }
            if (key != null) Kill(key);
            instance.adding.Add(new Tween { duration = Mathf.Max(0.0001f, duration), delay = delay, ease = ease, apply = apply, done = done, key = key });
        }

        public static void Kill(object key)
        {
            if (instance == null || key == null) return;
            foreach (var t in instance.tweens) if (Equals(t.key, key)) t.killed = true;
            instance.adding.RemoveAll(t => Equals(t.key, key));
        }

        public static void Delay(float seconds, Action action) => Value(seconds, _ => { }, Ease.Linear, 0f, action);

        public static void Scale(Transform t, Vector3 to, float duration, Ease ease = Ease.OutBack, float delay = 0f)
        {
            var from = t.localScale;
            Value(duration, v => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, v); }, ease, delay, null, (t, "scale"));
        }

        public static void Move(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Action done = null)
        {
            var from = rt.anchoredPosition;
            Value(duration, v => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, v); }, ease, delay, done, (rt, "move"));
        }

        public static void Fade(CanvasGroup g, float to, float duration, float delay = 0f, Action done = null)
        {
            var from = g.alpha;
            Value(duration, v => { if (g) g.alpha = Mathf.Lerp(from, to, v); }, Ease.OutCubic, delay, done, (g, "fade"));
        }

        /// <summary>Quick "boing" scale bump, great for buttons and numbers changing.</summary>
        public static void Punch(Transform t, float amount = 0.18f, float duration = 0.35f)
        {
            Value(duration, v =>
            {
                if (!t) return;
                var s = 1f + amount * Mathf.Sin(v * Mathf.PI) * (1f - v);
                t.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear, 0f, () => { if (t) t.localScale = Vector3.one; }, (t, "scale"));
        }

        private void Update()
        {
            if (adding.Count > 0) { tweens.AddRange(adding); adding.Clear(); }
            var dt = Time.unscaledDeltaTime;
            for (var i = tweens.Count - 1; i >= 0; i--)
            {
                var t = tweens[i];
                if (t.killed) { tweens.RemoveAt(i); continue; }
                if (t.delay > 0f) { t.delay -= dt; continue; }
                t.elapsed += dt;
                var p = Mathf.Clamp01(t.elapsed / t.duration);
                try { t.apply(Evaluate(t.ease, p)); }
                catch (Exception e) { Debug.LogException(e); t.killed = true; }
                if (p >= 1f && !t.killed)
                {
                    tweens.RemoveAt(i);
                    t.done?.Invoke();
                }
            }
        }
    }

    // ============================================================ PRESS FX ==
    /// <summary>
    /// Makes a button feel physical: it pops a little on hover, and when pressed
    /// its face sinks onto the darker "lip" under it, like a real key.
    /// </summary>
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public float pressed = 0.97f;
        public float hover = 1.04f;
        public RectTransform face; // optional: the part that sinks
        public float sink;         // how far it sinks, in canvas units
        private bool down, over;

        public void OnPointerDown(PointerEventData e) { down = true; Apply(); }
        public void OnPointerUp(PointerEventData e) { down = false; Apply(); }
        public void OnPointerEnter(PointerEventData e) { over = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; Apply(); }
        private void OnDisable() { down = over = false; transform.localScale = Vector3.one; Sink(); }

        private void Apply()
        {
            var s = down ? pressed : over ? hover : 1f;
            Tw.Scale(transform, new Vector3(s, s, 1f), 0.12f, Ease.OutCubic);
            Sink();
        }

        // Snaps instantly (no tween): a crisp click reads better than a slide.
        private void Sink()
        {
            if (face != null) face.anchoredPosition = new Vector2(0f, down ? -sink : 0f);
        }
    }

    // ============================================================== FACTORY ==
    public static class UI
    {
        public static GameAudio Audio;

        public static Canvas CreateCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand = the canvas is always at least 1920x1080 reference units, so
            // nothing designed for that size can overflow or overlap, whatever the
            // screen shape (wide phones, tablets, a small editor Game view).
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(parent, false);
        }

        // ------------------------------------------------------------ rects --
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Fill(rt, inset);
            return rt;
        }

        public static void Fill(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ----------------------------------------------------------- images --
        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) { img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            return img;
        }

        public static Image Icon(Transform parent, string icon, Color color, float size)
        {
            var img = Image(parent, "Icon_" + icon, Art.Get("UI/icon_" + icon), color);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            return img;
        }

        // ------------------------------------------------------------- text --
        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, bool bold = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool shadow = true)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = Art.Font(bold);
            if (shadow)
            {
                var m = Art.ShadowMaterial(bold);
                if (m != null) t.fontSharedMaterial = m;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.characterSpacing = bold ? 2f : 0.5f; // the body font is already well spaced
            return t;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, bool bold,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box)
        {
            var t = Label(parent, text, size, color, bold, align);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            return t;
        }

        // ----------------------------------------------------------- panels --
        /// <summary>Pixel-art panel: dark fill + two-tone coloured frame (+ a hard drop shadow).</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            Color frame, Color? fill = null, bool shadow = true)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            Decorate(rt, frame, fill ?? Palette.Panel, shadow);
            return rt;
        }

        public static void Decorate(RectTransform rt, Color frame, Color fill, bool shadow)
        {
            if (shadow)
            {
                // A solid offset shadow instead of a blurry glow: it matches the pixel-art
                // world and separates the panel from the busy city behind it.
                var sh = Image(rt, "Shadow", Art.Sliced("UI/chamfer_fill", 20f), Palette.Shadow.WithAlpha(0.7f), true);
                Fill(sh.rectTransform);
                Offset(sh.rectTransform, new Vector2(14f, -14f));
            }
            var f = Image(rt, "Fill", Art.Sliced("UI/chamfer_fill", 20f), fill, true);
            Fill(f.rectTransform);
            var sheen = Image(rt, "Sheen", Art.Sliced("UI/chamfer_sheen", 20f), Color.white.WithAlpha(0.12f), true);
            Fill(sheen.rectTransform);
            var fr = Image(rt, "Frame", Art.Sliced("UI/chamfer_frame", 20f), frame, true);
            Fill(fr.rectTransform);
        }

        private static void Offset(RectTransform rt, Vector2 by)
        {
            rt.offsetMin += by;
            rt.offsetMax += by;
        }

        // ---------------------------------------------------------- buttons --
        public sealed class NeonButton
        {
            public Button button;
            public RectTransform rect;
            public TextMeshProUGUI label;
            public TextMeshProUGUI sub;
            public Image icon;
            public Image fill;
            public Image frame;
            public Image lip; // darker strip under the face: makes the button look pressable
            public float labelInset; // left space the label leaves for the icon

            /// <summary>Shows/hides the icon; the label re-centres to use the freed space.</summary>
            public void ShowIcon(bool show)
            {
                icon.gameObject.SetActive(show);
                label.rectTransform.offsetMin = new Vector2(show ? labelInset : 0f, 0f);
            }

            /// <summary>Recolours the button. solid = filled with colour (primary), else dark with a coloured frame.</summary>
            public void Paint(Color color, bool solid)
            {
                // Secondary buttons get a dark fill tinted toward their colour, so they read as
                // live buttons rather than disabled grey slabs.
                fill.color = solid ? color : Color.Lerp(Palette.Panel, color, 0.22f).WithAlpha(1f);
                frame.color = solid ? Color.Lerp(color, Color.white, 0.5f) : color;
                lip.color = Color.Lerp(color, Palette.Ink, solid ? 0.55f : 0.72f).WithAlpha(1f);
                if (label != null) label.color = solid ? Palette.Ink : color;
                // Icons have a black outline, so on a filled button they stay white
                // (an ink-tinted icon would turn into a black blob).
                if (icon != null) icon.color = solid ? Color.white : color;
            }
        }

        private const float Lip = 12f; // canvas units of lip visible under a button face

        /// <summary>
        /// Neon button. solid = filled with colour (primary action); otherwise a
        /// dark button with a coloured frame (secondary action).
        /// </summary>
        public static NeonButton Button(Transform parent, string text, string icon, Color color, Vector2 anchor, Vector2 pos, Vector2 size,
            Action onClick, bool solid = true, float fontSize = 40f, string sound = "ui_tap")
        {
            var rt = Rect(parent, "Btn_" + text, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var nb = new NeonButton { rect = rt };
            // The lip fills the whole rect; the face sits on top of it, raised by Lip.
            nb.lip = Image(rt, "Lip", Art.Sliced("UI/chamfer_fill", 20f), Color.white, true);
            Fill(nb.lip.rectTransform);
            var face = Stretch(rt, "Face");
            face.offsetMin = new Vector2(0f, Lip);
            nb.fill = Image(face, "Fill", Art.Sliced("UI/chamfer_fill", 20f), Color.white, true);
            Fill(nb.fill.rectTransform);
            nb.fill.raycastTarget = true;
            var sheen = Image(face, "Sheen", Art.Sliced("UI/chamfer_sheen", 20f), Color.white.WithAlpha(solid ? 0.55f : 0.2f), true);
            Fill(sheen.rectTransform);
            nb.frame = Image(face, "Frame", Art.Sliced("UI/chamfer_frame", 20f), Color.white, true);
            Fill(nb.frame.rectTransform);

            var hasText = !string.IsNullOrEmpty(text);
            var faceHeight = size.y - Lip;
            if (!string.IsNullOrEmpty(icon))
            {
                var iconSize = Mathf.Min(faceHeight * 0.6f, 72f);
                nb.icon = Icon(face, icon, Color.white, iconSize);
                var irt = nb.icon.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(hasText ? 0f : 0.5f, 0.5f);
                irt.anchoredPosition = hasText ? new Vector2(faceHeight * 0.5f + 6f, 0f) : Vector2.zero;
            }
            if (hasText)
            {
                nb.label = Label(face, text, fontSize, Color.white, true, TextAlignmentOptions.Center, false);
                Fill(nb.label.rectTransform);
                nb.label.rectTransform.offsetMax = new Vector2(-12f, 0f);
                // Shrink (never grow) long labels so they always fit inside the button.
                nb.label.enableAutoSizing = true;
                nb.label.fontSizeMax = fontSize;
                nb.label.fontSizeMin = fontSize * 0.6f;
                if (nb.icon != null)
                {
                    // Start the label just past the icon's right edge so long words never overlap it.
                    var icn = nb.icon.rectTransform;
                    nb.labelInset = icn.anchoredPosition.x + icn.sizeDelta.x * 0.5f + 6f;
                    nb.label.rectTransform.offsetMin = new Vector2(nb.labelInset, 0f);
                }
            }
            nb.Paint(color, solid);

            nb.button = rt.gameObject.AddComponent<Button>();
            nb.button.targetGraphic = nb.fill;
            nb.button.transition = Selectable.Transition.ColorTint;
            var colors = nb.button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.55f, 0.8f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            nb.button.colors = colors;
            var nav = nb.button.navigation;
            nav.mode = Navigation.Mode.None;
            nb.button.navigation = nav;
            nb.button.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(sound)) Audio?.Play(sound);
                onClick?.Invoke();
            });
            var press = rt.gameObject.AddComponent<PressFx>();
            press.face = face;
            press.sink = Lip * 0.75f;
            return nb;
        }

        /// <summary>Square icon-only button (pause, back).</summary>
        public static NeonButton IconButton(Transform parent, string icon, Color color, Vector2 anchor, Vector2 pos, float size, Action onClick, string sound = "ui_tap")
            => Button(parent, null, icon, color, anchor, pos, new Vector2(size, size), onClick, false, 40f, sound);

        // ------------------------------------------------------------- bars --
        /// <summary>Horizontal progress bar. Returns the fill Image (set fillAmount 0..1).</summary>
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(parent, "Bar", anchor, pivot, pos, size);
            var back = Image(rt, "Back", Art.Sliced("UI/chamfer_fill", 20f), new Color(0.03f, 0.02f, 0.09f, 0.9f), true);
            Fill(back.rectTransform);
            back.pixelsPerUnitMultiplier = 2.5f; // smaller frame pixels for thin bars
            var fill = Image(rt, "Fill", Art.Get("FX/pixel", 4f), color);
            Fill(fill.rectTransform, 5f);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            var frame = Image(rt, "Frame", Art.Sliced("UI/chamfer_frame", 20f), color.WithAlpha(0.8f), true);
            Fill(frame.rectTransform);
            frame.pixelsPerUnitMultiplier = 2.5f;
            return fill;
        }

        /// <summary>Row of hexagon "pips" (used for upgrade levels and health).</summary>
        public static Image[] Pips(Transform parent, int count, float size, float gap, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            var width = count * size + (count - 1) * gap;
            var row = Rect(parent, "Pips", anchor, pivot, pos, new Vector2(width, size));
            var fills = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var frame = Image(row, "PipFrame", Art.Get("UI/hex_frame"), color.WithAlpha(0.7f));
                var r = frame.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(i * (size + gap), 0f);
                r.sizeDelta = new Vector2(size, size * 14f / 13f);
                fills[i] = Image(frame.transform, "PipFill", Art.Get("UI/hex_fill"), color);
                Fill(fills[i].rectTransform, size * 0.12f);
            }
            return fills;
        }

        /// <summary>A labelled ON/OFF switch built from a button.</summary>
        public static Action<bool> Switch(Transform parent, string text, Vector2 pos, bool initial, Action<bool> changed)
        {
            var row = Rect(parent, "Switch_" + text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(760f, 84f));
            // Label right-aligned against its control, so the eye doesn't cross a wide gutter.
            var label = Label(row, text, 34f, Palette.Text, false, TextAlignmentOptions.Right);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-280f, 0f);
            // Two-part ON | OFF switch: the active half is lit, so the current state is always visible.
            var state = initial;
            NeonButton on = null, off = null;
            void Set(bool v)
            {
                if (v == state) return;
                state = v;
                Paint();
                changed?.Invoke(state);
            }
            void Paint()
            {
                on.Paint(state ? Palette.Cyan : Palette.Muted, state);
                off.Paint(state ? Palette.Muted : Palette.Cyan, !state);
            }
            on = Button(row, "ON", null, Palette.Cyan, new Vector2(1f, 0.5f), new Vector2(-185f, 0f), new Vector2(120f, 76f), () => Set(true), true, 30f);
            off = Button(row, "OFF", null, Palette.Cyan, new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(120f, 76f), () => Set(false), true, 30f);
            Paint();
            return v => { state = v; Paint(); };
        }

        /// <summary>Volume slider (0..1) with neon styling.</summary>
        public static Slider Slider(Transform parent, string text, Vector2 pos, float value, Action<float> changed)
        {
            var row = Rect(parent, "Slider_" + text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(760f, 84f));
            var label = Label(row, text, 34f, Palette.Text, false, TextAlignmentOptions.Right);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-460f, 0f);

            var area = Rect(row, "Track", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(420f, 34f));
            var back = Image(area, "Back", Art.Sliced("UI/chamfer_fill", 20f), new Color(0.05f, 0.04f, 0.14f), true);
            Fill(back.rectTransform);
            back.pixelsPerUnitMultiplier = 2.5f; // smaller frame pixels for thin bars
            back.raycastTarget = true;
            var fillArea = Stretch(area, "FillArea", 6f);
            var fill = Image(fillArea, "Fill", Art.Get("FX/pixel", 4f), Palette.Cyan);
            Fill(fill.rectTransform);
            var handleArea = Stretch(area, "HandleArea", 0f);
            var handle = Image(handleArea, "Handle", Art.Get("UI/hex_fill"), Palette.Text);
            handle.rectTransform.sizeDelta = new Vector2(48f, 52f);
            var frame = Image(area, "Frame", Art.Sliced("UI/chamfer_frame", 20f), Palette.Cyan.WithAlpha(0.8f), true);
            Fill(frame.rectTransform);
            frame.pixelsPerUnitMultiplier = 2.5f;

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.onValueChanged.AddListener(v => changed?.Invoke(v));
            return slider;
        }

        /// <summary>Credits display: coin icon + number inside a pill.</summary>
        public static TextMeshProUGUI CreditPill(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width = 300f)
        {
            var rt = Panel(parent, "Credits", anchor, pivot, pos, new Vector2(width, 84f), Palette.Amber, new Color(0.08f, 0.05f, 0.12f, 0.92f), false);
            var icon = Icon(rt, "coin", Palette.Amber, 54f);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(44f, 0f);
            // Number sits right next to the coin, not pushed to the far end of the pill.
            var t = Label(rt, "0", 40f, Palette.Amber, true, TextAlignmentOptions.Left);
            Fill(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(84f, 0f);
            t.enableAutoSizing = true; // big balances shrink instead of spilling out
            t.fontSizeMax = 40f;
            t.fontSizeMin = 24f;
            t.rectTransform.offsetMax = new Vector2(-24f, 0f);
            return t;
        }
    }

    // =============================================================== SCREEN ==
    /// <summary>A full-screen layer that can fade/slide in and out.</summary>
    public class UIScreen
    {
        public readonly RectTransform Root;
        public readonly CanvasGroup Group;
        public bool Visible { get; private set; }

        public UIScreen(Transform parent, string name)
        {
            Root = UI.Stretch(parent, name);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Root.gameObject.SetActive(false);
            Group.alpha = 0f;
        }

        public virtual void Show()
        {
            Visible = true;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Group.interactable = true;
            Group.blocksRaycasts = true;
            Root.localScale = new Vector3(1.04f, 1.04f, 1f);
            Tw.Scale(Root, Vector3.one, 0.3f, Ease.OutCubic);
            Tw.Fade(Group, 1f, 0.22f);
        }

        public virtual void Hide()
        {
            Visible = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Tw.Fade(Group, 0f, 0.16f, 0f, () => { if (!Visible) Root.gameObject.SetActive(false); });
        }

        public void HideInstant()
        {
            Visible = false;
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Root.gameObject.SetActive(false);
        }
    }
}
