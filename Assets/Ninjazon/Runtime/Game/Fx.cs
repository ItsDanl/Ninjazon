using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ninjazon.Game
{
    /// <summary>
    /// "Juice": pooled sprite particles, floating text, camera shake and
    /// post-processing (bloom = the neon glow). Everything runs on our own
    /// delta time so it keeps animating during slow-motion and hit-stop.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        private sealed class Particle
        {
            public SpriteRenderer sr;
            public Vector3 velocity;
            public float life, duration, size0, size1, gravity, drag, spin;
            public Color color;
            public bool fadeIn;
            public Sprite[] frames; // set for flipbook effects: the sprite steps through these
        }

        private sealed class FloatingText
        {
            public TextMeshPro text;
            public float life, duration;
            public Vector3 velocity;
            public float scale;
        }

        private readonly List<Particle> live = new();
        private readonly Stack<Particle> pool = new();
        private readonly List<FloatingText> popups = new();
        private readonly Stack<FloatingText> popupPool = new();
        private readonly Dictionary<string, Sprite[]> flipbooks = new();

        private Camera cam;
        private Vector3 camBase;
        private float trauma;
        private float kick;
        private Vector2 kickDir;
        private Volume volume;
        private Bloom bloom;
        private ChromaticAberration chroma;
        private Vignette vignette;
        private ColorAdjustments color;
        private float chromaPulse;
        private float flash;
        private float baseBloom = 0.9f;

        public bool ShakeEnabled { get; set; } = true;
        public bool Danger { get; set; } // last health: the vignette pulses red

        public void Init(Camera camera)
        {
            cam = camera;
            camBase = cam.transform.localPosition;
            SetupPost();
        }

        public void SetCameraBase(Vector3 basePosition) => camBase = basePosition;

        private void SetupPost()
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.72f);
            bloom.intensity.Override(baseBloom);
            bloom.scatter.Override(0.68f);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0f, 0.12f));
            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);
            color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(10f);
            color.saturation.Override(12f);
            color.postExposure.Override(0f);
            volume.profile = profile;
        }

        // ----------------------------------------------------------- shake --
        /// <summary>Adds screen shake. Shake strength = trauma squared, so small hits stay subtle.</summary>
        public void Shake(float amount)
        {
            if (!ShakeEnabled) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>A quick directional camera nudge (e.g. landing pushes down).</summary>
        public void Kick(Vector2 direction, float strength)
        {
            if (!ShakeEnabled) return;
            kickDir = direction.normalized;
            kick = Mathf.Max(kick, strength);
        }

        public void ChromaPulse(float amount) => chromaPulse = Mathf.Max(chromaPulse, amount);
        public void Flash(float amount) => flash = Mathf.Max(flash, amount);
        public void SetBloomBoost(float extra) => baseBloom = 0.9f + extra;

        // ------------------------------------------------------- particles --
        public void Spawn(string sprite, Vector3 position, Vector3 velocity, float life, float size0, float size1,
            Color c, float gravity = 0f, float drag = 0f, int order = 40, float spin = 0f, bool smooth = true, bool fadeIn = false)
        {
            // The 4px "pixel" sprite uses a tiny pixels-per-unit so scale 1 = 1 world unit.
            Emit(Art.Get(sprite, sprite == "FX/pixel" ? 4f : 100f), position, velocity, life, size0, size1, c, gravity, drag, order, spin, fadeIn);
        }

        private Particle Emit(Sprite sprite, Vector3 position, Vector3 velocity, float life, float size0, float size1,
            Color c, float gravity, float drag, int order, float spin, bool fadeIn)
        {
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.sr.sprite = sprite;
            p.frames = null;
            p.sr.sortingOrder = order;
            p.sr.transform.position = position;
            p.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            p.velocity = velocity;
            p.life = p.duration = Mathf.Max(0.01f, life);
            p.size0 = size0;
            p.size1 = size1;
            p.color = c;
            p.gravity = gravity;
            p.drag = drag;
            p.spin = spin;
            p.fadeIn = fadeIn;
            p.sr.color = c;
            p.sr.gameObject.SetActive(true);
            live.Add(p);
            return p;
        }

        /// <summary>
        /// Plays a frame-by-frame pixel effect from the FX pack (Resources FX/name_0..4,
        /// made by gen_assets.py), tinted. size = world units; grounded = anchored at its bottom.
        /// </summary>
        public void Flipbook(string name, Vector3 at, Color c, float size, float life, int order = 43, bool grounded = false,
            Vector3 velocity = default)
        {
            const int frameCount = 5;
            var key = grounded ? name + "|g" : name;
            if (!flipbooks.TryGetValue(key, out var frames))
            {
                frames = new Sprite[frameCount];
                for (var i = 0; i < frameCount; i++)
                    frames[i] = Art.Get($"FX/{name}_{i}", 32f, new Vector2(0.5f, grounded ? 0f : 0.5f));
                flipbooks[key] = frames;
            }
            Emit(frames[0], at, velocity, life, size, size, c, 0f, 0f, order, 0f, false).frames = frames;
        }

        private Particle Create()
        {
            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            return new Particle { sr = go.AddComponent<SpriteRenderer>() };
        }

        /// <summary>Radial burst of glowing dots + pixel shards.</summary>
        public void Burst(Vector3 at, Color c, int count, float speed, float size = 0.18f, float life = 0.45f)
        {
            for (var i = 0; i < count; i++)
            {
                var a = Random.value * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.4f, 1f);
                if (i % 2 == 0)
                    Spawn("FX/glow", at, v, life * Random.Range(0.7f, 1.1f), size * 2.2f, 0f, c, 0f, 3f);
                else
                    Spawn("FX/pixel", at, v + Vector3.up * 2f, life * 1.3f, size * 0.6f, size * 0.2f, Color.Lerp(c, Color.white, 0.4f), -18f, 1.5f, 41, Random.Range(-600f, 600f), false);
            }
        }

        /// <summary>Directional spark spray (e.g. slicing).</summary>
        public void Sparks(Vector3 at, Vector3 direction, Color c, int count, float speed)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = Quaternion.Euler(0, 0, Random.Range(-40f, 40f)) * direction.normalized;
                Spawn("FX/spark", at, dir * speed * Random.Range(0.5f, 1.2f), Random.Range(0.2f, 0.4f), 0.55f, 0.1f, c, -6f, 4f, 42);
            }
        }

        /// <summary>Expanding pixel shock ring.</summary>
        public void Ring(Vector3 at, Color c, float size, float life = 0.35f)
            => Flipbook("pop", at, c, size, life);

        /// <summary>
        /// Splash on the wet roof plus a few flicked droplets. groundSpeed = how fast the
        /// roof scrolls left, so the splash stays where it hit instead of following the runner.
        /// </summary>
        public void Puff(Vector3 at, Color c, int count = 6, float groundSpeed = 0f)
        {
            var splash = c.WithAlpha(0.85f);
            Flipbook("splash", at, splash, 0.9f + count * 0.12f, 0.3f, 39, true, Vector3.left * groundSpeed);
            for (var i = 0; i < count / 2; i++)
                Spawn("FX/pixel", at, new Vector3(Random.Range(-2.5f, 1f), Random.Range(2f, 4f)), Random.Range(0.3f, 0.45f),
                    0.09f, 0.05f, splash, -22f, 0f, 41, 0f, false);
        }

        /// <summary>Ghost copy of a sprite that fades out (dash / double-jump afterimages).</summary>
        public void Afterimage(SpriteRenderer source, Color tint, float life = 0.25f)
        {
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.sr.sprite = source.sprite;
            p.sr.flipX = source.flipX;
            p.sr.sortingOrder = source.sortingOrder - 1;
            p.sr.transform.position = source.transform.position;
            p.sr.transform.rotation = source.transform.rotation;
            p.velocity = Vector3.left * 2f;
            p.life = p.duration = life;
            var s = source.transform.lossyScale.x;
            p.size0 = s;
            p.size1 = s;
            p.color = tint.WithAlpha(0.55f);
            p.gravity = 0;
            p.drag = 0;
            p.spin = 0;
            p.fadeIn = false;
            p.sr.color = p.color;
            p.sr.gameObject.SetActive(true);
            live.Add(p);
        }

        // ----------------------------------------------------------- popups --
        public void Popup(Vector3 at, string text, Color c, float size = 4.5f, float life = 0.9f)
        {
            var p = popupPool.Count > 0 ? popupPool.Pop() : CreatePopup();
            p.text.text = text;
            p.text.color = c;
            p.text.fontSize = size;
            p.text.transform.position = at;
            p.velocity = new Vector3(-1.2f, 2.6f, 0f);
            p.life = p.duration = life;
            p.scale = 1f;
            p.text.gameObject.SetActive(true);
            popups.Add(p);
        }

        private FloatingText CreatePopup()
        {
            var go = new GameObject("popup");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = Art.Font(true);
            var mat = Art.ShadowMaterial(true);
            if (mat != null) t.fontSharedMaterial = mat;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.sortingOrder = 60;
            t.rectTransform.sizeDelta = new Vector2(8f, 2f);
            return new FloatingText { text = t };
        }

        // ----------------------------------------------------------- update --
        public void Tick(float dt, float unscaledDt)
        {
            for (var i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.sr.gameObject.SetActive(false);
                    pool.Push(p);
                    live.RemoveAt(i);
                    continue;
                }
                var t = 1f - p.life / p.duration;
                p.velocity.y += p.gravity * dt;
                p.velocity *= Mathf.Exp(-p.drag * dt);
                var tr = p.sr.transform;
                tr.position += p.velocity * dt;
                if (p.spin != 0f) tr.Rotate(0, 0, p.spin * dt);
                var s = Mathf.Lerp(p.size0, p.size1, t);
                tr.localScale = new Vector3(s, s, 1f);
                if (p.frames != null) p.sr.sprite = p.frames[Mathf.Min(p.frames.Length - 1, (int)(t * p.frames.Length))];
                // Flipbooks fade through their own frames; plain particles fade out over life.
                var a = p.frames != null ? 1f : p.fadeIn ? Mathf.Sin(t * Mathf.PI) : 1f - t * t;
                p.sr.color = p.color.WithAlpha(p.color.a * a);
            }

            for (var i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.life -= unscaledDt;
                if (p.life <= 0f)
                {
                    p.text.gameObject.SetActive(false);
                    popupPool.Push(p);
                    popups.RemoveAt(i);
                    continue;
                }
                var t = 1f - p.life / p.duration;
                p.velocity *= Mathf.Exp(-3.5f * unscaledDt);
                p.text.transform.position += p.velocity * unscaledDt;
                // Pop in (overshoot) then shrink away.
                var pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
                var scale = pop * (t > 0.75f ? Mathf.Lerp(1f, 0.6f, (t - 0.75f) / 0.25f) : 1f);
                p.text.transform.localScale = Vector3.one * scale;
                p.text.alpha = t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
            }

            // Camera shake (trauma model) + directional kick.
            trauma = Mathf.Max(0f, trauma - unscaledDt * 1.6f);
            kick = Mathf.MoveTowards(kick, 0f, unscaledDt * 2.5f);
            var shake = trauma * trauma;
            var time = Time.unscaledTime * 28f;
            var offset = new Vector3(
                (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 1.1f * shake,
                (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 0.9f * shake,
                0f) + (Vector3)(kickDir * kick * 0.35f);
            cam.transform.localPosition = camBase + offset;
            cam.transform.localRotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(time * 0.7f, 5f) - 0.5f) * 3f * shake);

            chromaPulse = Mathf.MoveTowards(chromaPulse, 0f, unscaledDt * 2.2f);
            flash = Mathf.MoveTowards(flash, 0f, unscaledDt * 4f);
            if (chroma != null) chroma.intensity.value = Mathf.Clamp01(chromaPulse);
            if (bloom != null) bloom.intensity.value = baseBloom + flash * 3f;
            if (color != null) color.postExposure.value = flash * 0.8f;
            if (vignette != null)
            {
                var pulse = Danger ? 0.5f + Mathf.Sin(Time.unscaledTime * 6f) * 0.5f : 0f;
                vignette.color.value = Color.Lerp(new Color(0.05f, 0f, 0.12f), new Color(0.75f, 0.02f, 0.1f), Danger ? 0.6f + pulse * 0.4f : 0f);
                vignette.intensity.value = Danger ? 0.36f + pulse * 0.08f : 0.3f;
            }
        }

        public void ClearAll()
        {
            foreach (var p in live)
            {
                p.sr.gameObject.SetActive(false);
                pool.Push(p);
            }
            live.Clear();
            foreach (var p in popups)
            {
                p.text.gameObject.SetActive(false);
                popupPool.Push(p);
            }
            popups.Clear();
        }
    }
}
