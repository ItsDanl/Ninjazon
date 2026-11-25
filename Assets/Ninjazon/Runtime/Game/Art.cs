using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ninjazon.Game
{
    /// <summary>
    /// The game's colour palette. Every screen and effect pulls colours from
    /// here so the whole game stays visually consistent.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Night = Hex(0x0B0820);
        public static readonly Color Ink = Hex(0x07051A);
        public static readonly Color Panel = new(0.07f, 0.055f, 0.16f, 1f);
        public static readonly Color PanelLight = new(0.13f, 0.1f, 0.27f, 1f);
        public static readonly Color Cyan = Hex(0x19F0FF);
        public static readonly Color Magenta = Hex(0xFF2A8A);
        public static readonly Color Amber = Hex(0xFFB020);
        public static readonly Color Mint = Hex(0x3DFFA0);
        public static readonly Color Violet = Hex(0x9B6BFF);
        public static readonly Color Red = Hex(0xFF3B4E);
        public static readonly Color Text = Hex(0xEAF6FF);
        public static readonly Color Muted = Hex(0xBCC2EE); // light lavender: secondary text must stay readable over the city
        public static readonly Color Shadow = new(0.02f, 0.01f, 0.07f, 0.9f);

        public static Color Hex(int rgb, float alpha = 1f)
            => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }

    /// <summary>
    /// Loads textures from Resources/NZ and turns them into sprites at runtime.
    /// Sprites are cached, so asking for the same one twice is free.
    /// </summary>
    public static class Art
    {
        // Pixels-per-unit for world art: 110 pixels of art = 1 Unity unit.
        public const float WorldPpu = 110f;

        private static readonly Dictionary<string, Sprite> Cache = new();
        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static TMP_FontAsset regular;
        private static TMP_FontAsset bold;
        private static Material shadowMaterial;
        private static Material boldShadowMaterial;

        public static Texture2D Texture(string path)
        {
            if (Textures.TryGetValue(path, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("NZ/" + path);
            if (t == null) Debug.LogError($"[Ninjazon] Missing texture Resources/NZ/{path}");
            Textures[path] = t;
            return t;
        }

        /// <summary>Whole texture as a sprite. pivot (0..1), ppu = pixels per unit.</summary>
        public static Sprite Get(string path, float ppu = 100f, Vector2? pivot = null)
        {
            var p = pivot ?? new Vector2(0.5f, 0.5f);
            var key = $"{path}|{ppu}|{p.x:0.###}|{p.y:0.###}";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(path);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), p, ppu, 0, SpriteMeshType.FullRect);
            s.name = path;
            Cache[key] = s;
            return s;
        }

        /// <summary>9-sliced UI sprite. border = edge size in texture pixels that should not stretch.</summary>
        public static Sprite Sliced(string path, float border)
        {
            var key = $"{path}|sliced|{border}";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(path);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = path + "_sliced";
            Cache[key] = s;
            return s;
        }

        /// <summary>A rectangle cut out of a texture (rect uses Unity's bottom-left origin).</summary>
        public static Sprite Slice(Texture2D tex, Rect rect, Vector2 pivot, float ppu, string name)
        {
            rect.x = Mathf.Clamp(rect.x, 0, tex.width - 1);
            rect.y = Mathf.Clamp(rect.y, 0, tex.height - 1);
            rect.width = Mathf.Min(rect.width, tex.width - rect.x);
            rect.height = Mathf.Min(rect.height, tex.height - rect.y);
            var s = Sprite.Create(tex, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }

        // ------------------------------------------------------------ atlas --
        public enum AtlasItem { Barricade, Laser, Crate, Drone, Chip, Credit, Jammer }

        private static readonly Rect[] AtlasRects =
        {
            new(0f, 210f, 315f, 280f),
            new(330f, 210f, 330f, 330f),
            new(670f, 210f, 380f, 280f),
            new(1050f, 200f, 360f, 420f),
            new(1390f, 210f, 220f, 280f),
            new(1600f, 220f, 230f, 250f),
            new(1840f, 210f, 200f, 390f),
        };

        public static Sprite Atlas(AtlasItem item, Vector2 pivot, float ppu)
        {
            var key = $"atlas|{item}|{pivot.x:0.##}|{pivot.y:0.##}|{ppu}";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture("Art/atlas");
            s = Slice(tex, AtlasRects[(int)item], pivot, ppu, "atlas_" + item);
            Cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------ fonts --
        public static TMP_FontAsset Font(bool isBold)
        {
            if (isBold && bold != null) return bold;
            if (!isBold && regular != null) return regular;
            // Two type tiers: chunky Silkscreen for titles, numbers and buttons (bold);
            // the narrower Pixelify Sans for body text, descriptions and stats.
            var source = Resources.Load<Font>(isBold ? "NZ/Fonts/Silkscreen-Bold" : "NZ/Fonts/PixelifySans");
            TMP_FontAsset asset = null;
            if (source != null)
            {
                // Dynamic SDF font asset: glyphs are rendered on demand, and SDF lets
                // us add outlines/shadows through the material.
                asset = TMP_FontAsset.CreateFontAsset(source, 72, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
            }
            if (asset == null)
            {
                Debug.LogWarning("[Ninjazon] Custom font unavailable, using TMP default.");
                asset = TMP_Settings.defaultFontAsset;
            }
            if (isBold) bold = asset; else regular = asset;
            return asset;
        }

        /// <summary>Font material with a chunky drop shadow (TMP "underlay").</summary>
        public static Material ShadowMaterial(bool isBold)
        {
            if (isBold && boldShadowMaterial != null) return boldShadowMaterial;
            if (!isBold && shadowMaterial != null) return shadowMaterial;
            var font = Font(isBold);
            if (font == null || font.material == null) return null;
            var m = new Material(font.material) { name = "NZ_TextShadow" };
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.03f, 0.0f, 0.1f, 0.95f));
            m.SetFloat("_UnderlayOffsetX", 0.55f);
            m.SetFloat("_UnderlayOffsetY", -0.55f);
            m.SetFloat("_UnderlayDilate", 0.35f);
            m.SetFloat("_UnderlaySoftness", 0.0f);
            // Thin dark outline on the chunky display font so big callouts stay readable over
            // the busy city. Not on the body font: its small counters (5, B, C) would fill in.
            if (isBold)
            {
                m.EnableKeyword("OUTLINE_ON");
                m.SetColor("_OutlineColor", new Color(0.03f, 0.0f, 0.1f, 1f));
                m.SetFloat("_OutlineWidth", 0.12f);
            }
            else m.SetFloat("_UnderlayDilate", 0f);
            if (isBold) boldShadowMaterial = m; else shadowMaterial = m;
            return m;
        }

        // --------------------------------------------------------- recolour --
        private static readonly Dictionary<int, Texture2D> RunnerVariants = new();

        /// <summary>
        /// Returns the runner sheet with the cyan scarf/trim recoloured to
        /// `target`. Used by the cosmetic trails in the shop.
        /// </summary>
        public static Texture2D RunnerSheet(Color target, bool isDefault)
        {
            var source = Texture("Art/runner");
            if (isDefault || source == null || !source.isReadable) return source;
            var key = ColorKey(target);
            if (RunnerVariants.TryGetValue(key, out var cached) && cached != null) return cached;

            Color.RGBToHSV(target, out var targetHue, out var targetSat, out _);
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a < 10) continue;
                Color.RGBToHSV(p, out var h, out var s, out var v);
                // The scarf and neon trims are bright cyan/blue (hue ~170-220 degrees).
                if (h < 0.47f || h > 0.61f || s < 0.35f || v < 0.45f) continue;
                var outColor = Color.HSVToRGB(targetHue, Mathf.Clamp01(s * Mathf.Lerp(0.4f, 1.1f, targetSat)), v);
                pixels[i] = new Color32((byte)(outColor.r * 255), (byte)(outColor.g * 255), (byte)(outColor.b * 255), p.a);
            }
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "runner_" + key
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            RunnerVariants[key] = tex;
            return tex;
        }

        private static int ColorKey(Color c) => ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);
    }
}
