using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ninjazon.Game
{
    public enum EntKind { Barricade, Laser, Crate, Drone, Coin, Chip, Power, Marker }
    public enum PowerKind { Shield, Magnet, Overclock, Heal }
    public enum Prompt { None, Jump, Lane, Dash, DoubleJump, Done }

    /// <summary>
    /// One obstacle or pickup. x = centre, y = bottom of its hitbox measured in
    /// "lane space" (as if it stood in the front lane); lane says which of the
    /// three depth lanes it is in. Its on-screen height adds World.LaneY(lane).
    /// </summary>
    public sealed class Ent
    {
        public EntKind kind;
        public PowerKind power;
        public Prompt prompt;
        public int lane;
        public int openLane = -1;   // tutorial: the lane that is safe
        public float x, y, w, h;
        public float baseY, phase;
        public bool passed, dead, magnetized, promptShown;
        public Transform root;
        public SortingGroup group;
        public SpriteRenderer main;
        public SpriteRenderer shadow;
        public SpriteRenderer glow;
        public SpriteRenderer icon;
        public SpriteRenderer[] beams;

        public bool IsHazard => kind is EntKind.Barricade or EntKind.Laser or EntKind.Crate or EntKind.Drone;
        public bool IsPickup => kind is EntKind.Coin or EntKind.Chip or EntKind.Power;
        public bool Sliceable => kind is EntKind.Barricade or EntKind.Drone;
        public Rect Box => new(x - w * 0.5f, y, w, h);
    }

    public sealed class Roof
    {
        public float x0, x1, top;
        public Transform root;
        public SpriteRenderer floor, facade, capLeft, capRight;
        public SpriteRenderer[] laneLines;
        public SpriteRenderer[] laneBands; // one floor strip per lane; the runner's lane glows
    }

    /// <summary>
    /// Builds and scrolls the level. Everything moves left by the run speed
    /// each frame; new rooftops and obstacles are generated off the right edge
    /// and recycled when they leave the left edge (object pooling = no garbage).
    ///
    /// The rooftop has 3 depth lanes: 0 = front (lowest on screen), 2 = back.
    /// Back lanes are drawn a little higher, smaller and behind front lanes.
    /// </summary>
    public sealed class World : MonoBehaviour
    {
        private const float RoofArtHeight = 645f / Art.WorldPpu;    // 5.86 units
        private const float RoofFeetPivot = 0.392f;                 // front-lane feet row inside the roof art
        public const float LaneSpacing = 1.1f;
        public const int LaneCount = 3;

        public static float LaneY(float lane) => lane * LaneSpacing;
        public static float LaneScale(float lane) => 1f - 0.07f * lane;
        public static int LaneOrder(int lane) => 30 - Mathf.Clamp(lane, 0, 2) * 10;

        /// <summary>
        /// Lights the floor strip of the lane the runner is in, so you can see at a glance
        /// which obstacles share your lane. laneVisual may be between lanes mid-switch.
        /// </summary>
        public void HighlightLane(float laneVisual)
        {
            foreach (var r in Roofs)
                for (var i = 0; i < r.laneBands.Length; i++)
                    r.laneBands[i].color = Palette.Cyan.WithAlpha(0.13f * Mathf.Clamp01(1f - Mathf.Abs(laneVisual - i)));
        }

        public readonly List<Roof> Roofs = new();
        public readonly List<Ent> Ents = new();

        private readonly Stack<Roof> roofPool = new();
        private readonly Dictionary<EntKind, Stack<Ent>> entPool = new();
        private readonly List<Transform>[] parallax = { new(), new(), new() };
        private readonly float[] parallaxWidth = new float[3];
        private readonly float[] parallaxFactor = { 0.035f, 0.13f, 0.3f };
        private readonly List<Transform> rain = new();
        private readonly List<float> rainSpeed = new();

        private Camera cam;
        private Fx fx;
        private Transform roofRoot, entRoot, fogRoot;
        private SpriteRenderer fogBottom;
        private float cursor;          // where the next roof will start
        private float lastTop = -3f;
        private int tutorialStep = -1; // -1 = no tutorial
        private bool spawnHazards = true;

        // ---- districts: each has its own fog / skyline / rain colour mood.
        public static readonly string[] DistrictNames = { "NEON DOCKS", "CHROME HEIGHTS", "RED LIGHT RISE", "ULTRAVIOLET SPIRE", "GOLDEN HOUR" };
        private static readonly Color[] DistrictFog =
        {
            new(0.55f, 0.15f, 0.65f, 0.55f), new(0.1f, 0.5f, 0.62f, 0.55f), new(0.8f, 0.1f, 0.3f, 0.55f),
            new(0.42f, 0.16f, 0.95f, 0.6f), new(0.85f, 0.45f, 0.15f, 0.5f)
        };
        private static readonly Color[] DistrictSky =
        {
            new(0.85f, 0.85f, 1f), new(0.72f, 0.95f, 1f), new(1f, 0.78f, 0.88f), new(0.82f, 0.72f, 1f), new(1f, 0.88f, 0.76f)
        };
        private static readonly Color[] DistrictRain =
        {
            new(0.6f, 0.85f, 1f), new(0.6f, 1f, 0.95f), new(1f, 0.7f, 0.8f), new(0.8f, 0.7f, 1f), new(1f, 0.85f, 0.6f)
        };
        private int district;
        private float moodBlend = 1f;
        private Color fogFrom, fogTo, skyFrom, skyTo, rainFrom, rainTo;
        private readonly List<SpriteRenderer> skyRenderers = new();
        private readonly List<SpriteRenderer> rainRenderers = new();

        public int District => district;

        /// <summary>Starts a smooth colour transition to a district's mood.</summary>
        public void SetDistrict(int index, bool instant)
        {
            district = index;
            var i = index % DistrictNames.Length;
            fogFrom = fogBottom.color;
            skyFrom = skyRenderers.Count > 0 ? skyRenderers[0].color : Color.white;
            rainFrom = rainTo;
            fogTo = DistrictFog[i];
            skyTo = DistrictSky[i];
            rainTo = DistrictRain[i];
            moodBlend = instant ? 1f : 0f;
            if (instant) ApplyMood(1f);
        }

        private void ApplyMood(float t)
        {
            fogBottom.color = Color.Lerp(fogFrom, fogTo, t);
            var sky = Color.Lerp(skyFrom, skyTo, t);
            foreach (var r in skyRenderers) r.color = sky;
            var rainColor = Color.Lerp(rainFrom, rainTo, t);
            for (var k = 0; k < rainRenderers.Count; k++)
            {
                var near = k >= 80;
                rainRenderers[k].color = rainColor.WithAlpha(near ? 0.22f : 0.13f);
            }
        }

        public float Difficulty { get; set; }
        public float Speed { get; set; } = 8f;
        public float FortuneMultiplier { get; set; } = 1f;
        public bool TutorialActive => tutorialStep >= 0;
        public float HalfWidth => cam.orthographicSize * cam.aspect;
        public float LeftEdge => cam.transform.position.x - HalfWidth;
        public float RightEdge => cam.transform.position.x + HalfWidth;

        public void Init(Camera camera, Fx effects)
        {
            cam = camera;
            fx = effects;
            roofRoot = new GameObject("Roofs").transform;
            roofRoot.SetParent(transform, false);
            entRoot = new GameObject("Entities").transform;
            entRoot.SetParent(transform, false);
            BuildParallax();
            BuildRain();
            rainTo = DistrictRain[0];
            SetDistrict(0, true);
        }

        // ---------------------------------------------------------- backdrop --
        private void BuildParallax()
        {
            var layers = new[] { "Art/sky_far", "Art/sky_mid", "Art/sky_near" };
            var ppus = new[] { 88f, 110f, 110f };
            var bottoms = new[] { -5.8f, -4.2f, -4.6f };
            var orders = new[] { -100, -90, -80 };
            for (var l = 0; l < 3; l++)
            {
                var sprite = Art.Get(layers[l], ppus[l], new Vector2(0f, 0f));
                parallaxWidth[l] = sprite.bounds.size.x;
                for (var i = 0; i < 4; i++)
                {
                    var go = new GameObject($"Parallax{l}_{i}");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = orders[l];
                    // Distant layers are darker/bluer = atmospheric depth.
                    sr.color = l == 0 ? new Color(0.85f, 0.85f, 1f) : l == 1 ? new Color(0.8f, 0.78f, 0.95f) : new Color(0.92f, 0.9f, 1f);
                    go.transform.position = new Vector3(-14f + i * parallaxWidth[l], bottoms[l], 0f);
                    parallax[l].Add(go.transform);
                    if (l < 2) skyRenderers.Add(sr);
                }
            }

            fogRoot = new GameObject("Fog").transform;
            fogRoot.SetParent(transform, false);
            fogBottom = new GameObject("FogBottom").AddComponent<SpriteRenderer>();
            fogBottom.transform.SetParent(fogRoot, false);
            fogBottom.sprite = Art.Get("FX/fog", 100f, new Vector2(0.5f, 1f));
            fogBottom.color = new Color(0.55f, 0.15f, 0.65f, 0.55f);
            fogBottom.sortingOrder = -70;
            var midFog = new GameObject("FogMid").AddComponent<SpriteRenderer>();
            midFog.transform.SetParent(fogRoot, false);
            midFog.sprite = Art.Get("FX/fog", 100f, new Vector2(0.5f, 1f));
            midFog.color = new Color(0.2f, 0.12f, 0.45f, 0.45f);
            midFog.sortingOrder = -85;
            midFog.transform.localPosition = new Vector3(0f, -1.0f, 0f);
            midFog.transform.localScale = new Vector3(40f, 0.03f, 1f);
        }

        private void BuildRain()
        {
            var sprite = Art.Get("FX/line", 100f);
            for (var i = 0; i < 110; i++)
            {
                var near = i >= 80;
                var go = new GameObject("Rain");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = near ? 45 : -60;
                sr.color = near ? new Color(0.6f, 0.85f, 1f, 0.22f) : new Color(0.55f, 0.6f, 1f, 0.13f);
                var len = near ? Random.Range(0.6f, 1.0f) : Random.Range(0.25f, 0.5f);
                go.transform.localScale = new Vector3(len, near ? 0.6f : 0.4f, 1f);
                go.transform.rotation = Quaternion.Euler(0, 0, -72f);
                go.transform.position = new Vector3(Random.Range(-14f, 14f), Random.Range(-6f, 6f), 0f);
                rain.Add(go.transform);
                rainRenderers.Add(sr);
                rainSpeed.Add(near ? Random.Range(22f, 28f) : Random.Range(11f, 15f));
            }
        }

        private void TickBackdrop(float dx, float dt)
        {
            if (moodBlend < 1f)
            {
                moodBlend = Mathf.Min(1f, moodBlend + dt / 2.5f);
                ApplyMood(Mathf.SmoothStep(0f, 1f, moodBlend));
            }
            var left = LeftEdge;
            for (var l = 0; l < 3; l++)
            {
                var w = parallaxWidth[l];
                var list = parallax[l];
                var maxX = float.MinValue;
                foreach (var t in list) maxX = Mathf.Max(maxX, t.position.x);
                foreach (var t in list)
                {
                    var p = t.position;
                    p.x -= dx * parallaxFactor[l];
                    if (p.x + w < left - 1f)
                    {
                        p.x = maxX + w - 0.01f;
                        maxX = p.x;
                    }
                    t.position = p;
                }
            }

            var camPos = cam.transform.position;
            fogBottom.transform.position = new Vector3(camPos.x, camPos.y - cam.orthographicSize + 3.2f, 0f);
            fogBottom.transform.localScale = new Vector3(HalfWidth * 0.32f, 0.018f, 1f);
            fogRoot.position = new Vector3(camPos.x, 0f, 0f);

            var h = cam.orthographicSize + 1f;
            for (var i = 0; i < rain.Count; i++)
            {
                var t = rain[i];
                var p = t.position;
                var s = rainSpeed[i];
                p += new Vector3(-0.31f * s - dx / Mathf.Max(dt, 0.0001f) * (i >= 80 ? 0.6f : 0.15f), -0.95f * s, 0f) * dt;
                if (p.y < camPos.y - h) { p.y += 2 * h; p.x = Random.Range(LeftEdge, RightEdge + 6f); }
                while (p.x < LeftEdge - 2f) p.x += HalfWidth * 2f + 4f;
                t.position = p;
            }
        }


        // ------------------------------------------------------------ reset --
        public void ResetWorld(bool tutorial, float startTop)
        {
            foreach (var r in Roofs) RecycleRoof(r);
            Roofs.Clear();
            foreach (var e in Ents) Recycle(e);
            Ents.Clear();
            lastTop = startTop;
            tutorialStep = tutorial ? 0 : -1;
            // A long, safe starting rooftop that also hosts the main menu.
            var start = AddRoof(LeftEdge - 4f, LeftEdge - 4f + 46f, startTop);
            cursor = tutorial ? start.x1 : start.x1 + 3.2f;
            if (!tutorial && spawnHazards)
            {
                CoinLine(start.x1 - 20f, startTop + 0.5f, 6, 1.1f, 1);
                CoinLine(start.x1 - 12f, startTop + 0.5f, 6, 1.1f, Random.Range(0, 3));
            }
            Fill();
        }

        /// <summary>Menu mode: keep generating empty rooftops (no hazards).</summary>
        public void SetHazards(bool on) => spawnHazards = on;

        // -------------------------------------------------------- per frame --
        public void Advance(float dx, float dt)
        {
            TickBackdrop(dx, dt);
            for (var i = Roofs.Count - 1; i >= 0; i--)
            {
                var r = Roofs[i];
                r.x0 -= dx;
                r.x1 -= dx;
                r.root.position = new Vector3(r.x0, r.top, 0f);
                if (r.x1 < LeftEdge - 3f)
                {
                    RecycleRoof(r);
                    Roofs.RemoveAt(i);
                }
            }
            cursor -= dx;

            var time = Time.time;
            for (var i = Ents.Count - 1; i >= 0; i--)
            {
                var e = Ents[i];
                e.x -= dx;
                if (e.kind == EntKind.Drone)
                {
                    e.phase += dt;
                    e.y = e.baseY + Mathf.Sin(e.phase * 3.2f) * 0.15f;
                }
                else if (e.kind == EntKind.Coin && !e.dead)
                {
                    // Fake 3D spin by squishing the coin horizontally.
                    var sx = Mathf.Abs(Mathf.Cos((time * 4f) + e.x * 0.6f));
                    e.main.transform.localScale = new Vector3(Mathf.Max(0.15f, sx), 1f, 1f);
                }
                else if (e.kind == EntKind.Power || e.kind == EntKind.Chip)
                {
                    e.phase += dt;
                    if (!e.magnetized) e.y = e.baseY + Mathf.Sin(e.phase * 3f) * 0.12f;
                    if (e.glow != null) e.glow.transform.localScale = Vector3.one * (2.4f + Mathf.Sin(e.phase * 5f) * 0.25f);
                }
                else if (e.kind == EntKind.Laser && e.beams != null)
                {
                    e.phase += dt;
                    for (var b = 0; b < e.beams.Length; b++)
                    {
                        var flicker = 0.75f + 0.25f * Mathf.Sin(e.phase * 30f + b * 2f) * Mathf.Sin(e.phase * 7f + b);
                        e.beams[b].color = Palette.Magenta.WithAlpha(flicker);
                    }
                }
                if (!e.dead) PlaceEnt(e);
                if (e.x < LeftEdge - 4f)
                {
                    Recycle(e);
                    Ents.RemoveAt(i);
                }
            }
            Fill();
        }

        public void PlaceEnt(Ent e)
        {
            var roof = RoofUnder(e.x);
            var groundY = roof != null ? roof.top : e.y;
            e.root.position = new Vector3(e.x, e.y + LaneY(e.lane), 0f);
            if (e.shadow != null)
            {
                // Shadow stays on the floor of the entity's lane (sells the depth).
                e.shadow.gameObject.SetActive(roof != null);
                e.shadow.transform.position = new Vector3(e.x, groundY + LaneY(e.lane) + 0.02f, 0f);
            }
        }

        /// <summary>
        /// Highest surface (roof or crate top) under x in the given lane that is
        /// not above maxY. Roofs span every lane; crates only block their own lane.
        /// </summary>
        public float GroundAt(float x, float maxY, int lane)
        {
            var best = -999f;
            const float foot = 0.22f;
            foreach (var r in Roofs)
                if (x + foot > r.x0 && x - foot < r.x1 && r.top <= maxY && r.top > best) best = r.top;
            foreach (var e in Ents)
            {
                if (e.kind != EntKind.Crate || e.dead || e.lane != lane) continue;
                var top = e.y + e.h;
                if (x + foot > e.x - e.w * 0.5f && x - foot < e.x + e.w * 0.5f && top <= maxY && top > best) best = top;
            }
            return best;
        }

        public Roof RoofUnder(float x)
        {
            foreach (var r in Roofs) if (x >= r.x0 && x <= r.x1) return r;
            return null;
        }

        public Roof NextRoofAfter(float x)
        {
            Roof best = null;
            foreach (var r in Roofs) if (r.x0 > x && (best == null || r.x0 < best.x0)) best = r;
            return best;
        }

        /// <summary>Shifts the whole level left (used by the rescue teleport).</summary>
        public void Shift(float dx) => Advance(dx, 0.0001f);

        public void ClearHazards(float fromX, float toX)
        {
            foreach (var e in Ents)
                if (e.IsHazard && !e.dead && e.x > fromX && e.x < toX) Kill(e);
        }

        public void Kill(Ent e)
        {
            e.dead = true;
            if (e.root != null) e.root.gameObject.SetActive(false);
        }

        /// <summary>Moves a pickup into another lane without visibly jumping (magnet).</summary>
        public void MoveToLane(Ent e, int lane)
        {
            if (e.lane == lane) return;
            e.y += LaneY(e.lane) - LaneY(lane);
            e.baseY = e.y;
            e.lane = lane;
            ApplyLaneLook(e);
        }

        // -------------------------------------------------------- generation --
        private void Fill()
        {
            while (cursor < RightEdge + 30f)
            {
                if (tutorialStep >= 0) BuildTutorialRoof();
                else BuildRandomRoof();
            }
        }

        // Time between obstacle patterns: ~1.6s at the start, ~0.7s at top difficulty.
        private float Spacing() => Speed * Mathf.Lerp(1.6f, 0.7f, Difficulty) + Random.Range(0f, 2f);

        private void BuildRandomRoof()
        {
            var len = Random.Range(Mathf.Lerp(30f, 20f, Difficulty), Mathf.Lerp(48f, 32f, Difficulty));
            var top = Mathf.Clamp(lastTop + new[] { -0.4f, 0f, 0f, 0.4f }[Random.Range(0, 4)], -3.4f, -2.6f);
            var roof = AddRoof(cursor, cursor + len, top);

            if (spawnHazards)
            {
                var x = roof.x0 + Mathf.Max(5f, Speed * 0.6f);
                while (x < roof.x1 - 4f)
                {
                    var end = PlaceBeat(x, top, roof.x1 - 3f);
                    x = end + Spacing();
                }
            }

            // Gap to the next building. Wider gaps need a double jump (or a dash).
            var needsDouble = spawnHazards && Difficulty > 0.12f && Random.value < Mathf.Lerp(0.15f, 0.4f, Difficulty);
            var gap = needsDouble
                ? Mathf.Clamp(Speed * 0.68f, 5f, 9.5f)
                : Mathf.Clamp(Speed * Random.Range(0.28f, 0.42f), 2.2f, 5.2f);
            if (!spawnHazards) gap = 3f;
            if (spawnHazards) GapArc(roof.x1, gap, top, needsDouble, Random.Range(0, 3));
            cursor = roof.x1 + gap;
        }

        private static int OtherLane(int a) => (a + Random.Range(1, 3)) % 3;

        /// <summary>Places one "beat" (a lane pattern) and returns where it ends.</summary>
        private float PlaceBeat(float x, float top, float limit)
        {
            var d = Difficulty;
            var roll = Random.value;

            // Power-ups: rarer early, more frequent with the Lucky Charm upgrade.
            if (Random.value < 0.07f * FortuneMultiplier)
            {
                AddPower(x, top + 0.9f, (PowerKind)Random.Range(0, 4), Random.Range(0, 3));
                return x + 1f;
            }

            if (roll < 0.12f)
            {
                // Coin trail that weaves between lanes: rewards lane switching.
                var lane = Random.Range(0, 3);
                for (var i = 0; i < 9; i++)
                {
                    if (i == 3 || i == 6) lane = Mathf.Clamp(lane + (Random.value < 0.5f ? -1 : 1), 0, 2);
                    AddCoin(x + i * 1.1f, top + 0.5f, lane);
                }
                if (Random.value < 0.25f) AddChip(x + 10.5f, top + 0.6f, lane);
                return x + 10f;
            }
            if (roll < 0.34f)
            {
                // Laser walls: block one or two lanes; coins mark the open lane.
                var open = Random.Range(0, 3);
                var blockBoth = d > 0.15f && Random.value < Mathf.Lerp(0.4f, 0.85f, d);
                for (var l = 0; l < 3; l++)
                {
                    if (l == open) continue;
                    if (!blockBoth && l != OtherLaneFixed(open)) continue;
                    AddLaser(x, top, l);
                }
                CoinLine(x - 2.2f, top + 0.5f, 4, 1.1f, open);
                return x + 1f;
            }
            if (roll < 0.5f)
            {
                // Barricades: across every lane later on (must jump or slice).
                var all = d > 0.2f && Random.value < 0.6f;
                var first = Random.Range(0, 3);
                for (var l = 0; l < 3; l++)
                    if (all || l == first || (d > 0.1f && l == (first + 1) % 3)) AddBarricade(x, top, l);
                JumpArc(x, top, all ? Random.Range(0, 3) : first);
                return x + 1f;
            }
            if (roll < 0.64f)
            {
                // Drone guarding a lane: dodge it or dash-slice it for points.
                var lane = Random.Range(0, 3);
                AddDrone(x, top + 0.75f, lane);
                if (d > 0.3f && Random.value < 0.5f) AddDrone(x + 1.5f, top + 0.75f, OtherLane(lane));
                CoinLine(x - 2.5f, top + 0.5f, 3, 1.1f, lane);
                return x + 1.5f;
            }
            if (roll < 0.78f && x + 4f < limit)
            {
                // Crates: run into the side = hit, jump on top = coin route.
                var lane = Random.Range(0, 3);
                AddCrate(x, top, lane);
                CoinLine(x - 0.6f, top + 1.35f + 0.5f, 3, 0.6f, lane);
                if (d > 0.3f && Random.value < 0.5f)
                {
                    AddCrate(x + 2f, top + 1.35f, lane);   // stacked: a two-step climb
                    CoinLine(x + 1.4f, top + 2.7f + 0.5f, 3, 0.6f, lane);
                    if (Random.value < 0.3f) AddChip(x + 3.6f, top + 3.1f, lane);
                }
                if (d > 0.4f && Random.value < 0.5f) AddLaser(x + 1f, top, OtherLane(lane));
                return x + 3f;
            }
            if (d > 0.3f && roll < 0.9f)
            {
                // Mixed wall: every lane has a different problem. Pick your answer.
                var order = new List<int> { 0, 1, 2 };
                for (var i = 0; i < 3; i++) { var j = Random.Range(i, 3); (order[i], order[j]) = (order[j], order[i]); }
                AddLaser(x, top, order[0]);
                AddBarricade(x, top, order[1]);
                AddDrone(x, top + 0.75f, order[2]);
                JumpArc(x, top, order[1]);
                return x + 1.5f;
            }
            if (d > 0.45f)
            {
                // Zig-zag: two laser walls in a row, each leaving a different lane open.
                var open1 = Random.Range(0, 3);
                var open2 = OtherLane(open1);
                var x2 = x + Mathf.Max(5.5f, Speed * 0.5f);
                for (var l = 0; l < 3; l++)
                {
                    if (l != open1) AddLaser(x, top, l);
                    if (l != open2 && x2 < limit) AddLaser(x2, top, l);
                }
                CoinLine(x - 1.5f, top + 0.5f, 2, 1.1f, open1);
                CoinLine(x2 - 1.5f, top + 0.5f, 2, 1.1f, open2);
                return x2 + 1f;
            }
            AddDrone(x, top + 0.75f, Random.Range(0, 3));
            return x + 1f;
        }

        private static int OtherLaneFixed(int open) => open == 0 ? 1 : open - 1;

        // --------------------------------------------------------- tutorial --
        private void BuildTutorialRoof()
        {
            const float top = -3f;
            switch (tutorialStep)
            {
                case 0:
                {
                    // Extend the starting rooftop so training begins on solid ground.
                    var r = Roofs[Roofs.Count - 1];
                    var o = r.x1;
                    ExtendRoof(r, 66f);
                    CoinLine(o + 4f, top + 0.5f, 6, 1.1f, 1);
                    // 1) Jump: barricades in every lane.
                    for (var l = 0; l < 3; l++)
                    {
                        var b = AddBarricade(o + 20f, top, l);
                        if (l == 1) b.prompt = Prompt.Jump;
                    }
                    JumpArc(o + 20f, top, 1);
                    // 2) Change lanes: lasers in lanes 1 and 2, lane 0 is open.
                    AddLaser(o + 38f, top, 2);
                    var laser = AddLaser(o + 38f, top, 1);
                    laser.prompt = Prompt.Lane;
                    laser.openLane = 0;
                    CoinLine(o + 32f, top + 0.5f, 6, 1.1f, 0);
                    // 3) Dash: a drone in every lane.
                    for (var l = 0; l < 3; l++)
                    {
                        var dr = AddDrone(o + 56f, top + 0.75f, l);
                        if (l == 0) dr.prompt = Prompt.Dash;
                    }
                    AddMarker(r.x1 - 2.4f, top, Prompt.Jump);
                    GapArc(r.x1, 3.6f, top, false, 0);
                    cursor = r.x1 + 3.6f;
                    break;
                }
                case 1:
                {
                    var r = AddRoof(cursor, cursor + 26f, top);
                    CoinLine(r.x0 + 4f, top + 0.5f, 6, 1.0f, 0);
                    AddMarker(r.x1 - 2.4f, top, Prompt.Jump);
                    AddMarker(r.x1 + 2.2f, top, Prompt.DoubleJump);
                    for (var l = 0; l < 3; l++) GapArc(r.x1, 6.6f, top, true, l);
                    cursor = r.x1 + 6.6f;
                    break;
                }
                case 2:
                {
                    var r = AddRoof(cursor, cursor + 40f, top);
                    AddMarker(r.x0 + 6f, top, Prompt.Done);
                    for (var l = 0; l < 3; l++) CoinLine(r.x0 + 8f + l * 3f, top + 0.5f, 5, 1.0f, l);
                    cursor = r.x1 + 3.2f;
                    tutorialStep = -2; // finished; switch to random after this roof
                    break;
                }
            }
            if (tutorialStep >= 0) tutorialStep++;
            else tutorialStep = -1;
        }

        // ----------------------------------------------------------- builders --
        private Roof AddRoof(float x0, float x1, float top)
        {
            var r = roofPool.Count > 0 ? roofPool.Pop() : CreateRoof();
            r.x0 = x0;
            r.x1 = x1;
            r.top = top;
            lastTop = top;
            r.root.gameObject.SetActive(true);
            r.root.position = new Vector3(x0, top, 0f);
            var below = RoofArtHeight * RoofFeetPivot;
            r.facade.transform.localPosition = new Vector3(0f, -below + 0.02f, 0f);
            var capTop = RoofArtHeight * (1f - RoofFeetPivot) - 0.15f;
            r.capLeft.transform.localPosition = new Vector3(0f, capTop, 0f);
            r.capLeft.size = new Vector2(0.3f, 18f);
            r.capRight.size = new Vector2(0.3f, 18f);
            SizeRoof(r);
            Roofs.Add(r);
            return r;
        }

        private void SizeRoof(Roof r)
        {
            var width = r.x1 - r.x0;
            r.floor.size = new Vector2(width, RoofArtHeight);
            r.facade.size = new Vector2(width, 14f);
            var capTop = RoofArtHeight * (1f - RoofFeetPivot) - 0.15f;
            r.capRight.transform.localPosition = new Vector3(width, capTop, 0f);
            for (var i = 0; i < r.laneLines.Length; i++)
            {
                r.laneLines[i].size = new Vector2(Mathf.Max(0f, width - 0.6f), 0.12f);
                r.laneLines[i].transform.localPosition = new Vector3(0.3f, LaneY(i + 0.5f) - 0.06f, 0f);
            }
            for (var i = 0; i < r.laneBands.Length; i++)
            {
                // Each band covers its lane, from the line below it to the line above it.
                r.laneBands[i].size = new Vector2(Mathf.Max(0f, width - 0.6f), LaneSpacing);
                r.laneBands[i].transform.localPosition = new Vector3(0.3f, LaneY(i - 0.5f), 0f);
            }
        }

        private void ExtendRoof(Roof r, float extra)
        {
            r.x1 += extra;
            SizeRoof(r);
        }

        private Roof CreateRoof()
        {
            var r = new Roof { root = new GameObject("Roof").transform };
            r.root.SetParent(roofRoot, false);
            r.floor = TiledChild(r.root, "Floor", Art.Get("Art/roof_lanes", Art.WorldPpu, new Vector2(0f, RoofFeetPivot)), -20);
            r.facade = TiledChild(r.root, "Facade", Art.Get("Art/facade", 64f, new Vector2(0f, 1f)), -22);
            r.facade.color = new Color(0.8f, 0.8f, 0.95f);
            r.capLeft = TiledChild(r.root, "CapL", Art.Get("Art/edge_cap", 32f, new Vector2(0f, 1f)), -19);
            r.capRight = TiledChild(r.root, "CapR", Art.Get("Art/edge_cap", 32f, new Vector2(1f, 1f)), -19);
            r.capRight.flipX = true;
            // Dashed neon lines painted between the lanes.
            r.laneLines = new SpriteRenderer[2];
            for (var i = 0; i < 2; i++)
            {
                r.laneLines[i] = TiledChild(r.root, "LaneLine" + i, Art.Get("Art/lane_dash", 16f, new Vector2(0f, 0f)), -18);
                r.laneLines[i].color = Palette.Cyan.WithAlpha(0.3f);
            }
            r.laneBands = new SpriteRenderer[LaneCount];
            for (var i = 0; i < LaneCount; i++)
            {
                r.laneBands[i] = TiledChild(r.root, "LaneBand" + i, Art.Get("FX/pixel", 4f, Vector2.zero), -19);
                r.laneBands[i].color = Color.clear;
            }
            return r;
        }

        private static SpriteRenderer TiledChild(Transform parent, string childName, Sprite sprite, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            return sr;
        }

        private void RecycleRoof(Roof r)
        {
            r.root.gameObject.SetActive(false);
            roofPool.Push(r);
        }

        private Ent Take(EntKind kind)
        {
            if (entPool.TryGetValue(kind, out var stack) && stack.Count > 0)
            {
                var e = stack.Pop();
                e.root.gameObject.SetActive(true);
                return e;
            }
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(entRoot, false);
            var ent = new Ent { kind = kind, root = go.transform };
            // A SortingGroup lets us draw the whole object (all its parts) in its lane's layer.
            ent.group = go.AddComponent<SortingGroup>();
            ent.main = AddSprite(go.transform, "Main", null, 5);
            if (kind != EntKind.Marker)
            {
                // The shadow is a separate object so it can stay on the floor while
                // the entity bobs or floats.
                var shadowGo = new GameObject(kind + "Shadow");
                shadowGo.transform.SetParent(entRoot, false);
                ent.shadow = shadowGo.AddComponent<SpriteRenderer>();
                // Hazards get a coloured "danger plate" glowing on their lane's floor,
                // so you can tell at a glance which lane they block. Pickups get a soft shadow.
                var danger = kind is EntKind.Barricade or EntKind.Laser or EntKind.Crate or EntKind.Drone;
                ent.shadow.sprite = Art.Get(danger ? "FX/glow" : "FX/soft_dot", 100f);
                ent.shadow.color = danger
                    ? (kind == EntKind.Laser || kind == EntKind.Drone ? Palette.Magenta : Palette.Amber).WithAlpha(0.55f)
                    : new Color(0f, 0f, 0.05f, kind is EntKind.Coin ? 0.25f : 0.5f);
                var size = kind switch { EntKind.Crate => 3.4f, EntKind.Laser => 3.0f, EntKind.Coin => 0.9f, _ => 2.2f };
                ent.shadow.transform.localScale = new Vector3(size, size * (danger ? 0.3f : 0.22f), 1f);
            }
            return ent;
        }

        private static SpriteRenderer AddSprite(Transform parent, string childName, Sprite sprite, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private void ApplyLaneLook(Ent e)
        {
            e.group.sortingOrder = LaneOrder(e.lane) + (e.IsPickup ? 2 : 0);
            var s = LaneScale(e.lane);
            e.root.localScale = new Vector3(s, s, 1f);
            if (e.shadow != null) e.shadow.sortingOrder = LaneOrder(e.lane) - 1;
        }

        private Ent Register(Ent e, float x, float y, float w, float h, int lane)
        {
            e.x = x;
            e.y = e.baseY = y;
            e.w = w;
            e.h = h;
            e.lane = Mathf.Clamp(lane, 0, 2);
            e.openLane = -1;
            e.dead = e.passed = e.magnetized = e.promptShown = false;
            e.prompt = Prompt.None;
            e.phase = Random.value * 6f;
            if (e.shadow != null) e.shadow.gameObject.SetActive(true);
            ApplyLaneLook(e);
            PlaceEnt(e);
            Ents.Add(e);
            return e;
        }

        private void Recycle(Ent e)
        {
            if (e.root == null) return;
            e.root.gameObject.SetActive(false);
            if (e.shadow != null) e.shadow.gameObject.SetActive(false);
            if (!entPool.TryGetValue(e.kind, out var stack)) entPool[e.kind] = stack = new Stack<Ent>();
            stack.Push(e);
        }

        public Ent AddBarricade(float x, float top, int lane)
        {
            var e = Take(EntKind.Barricade);
            e.main.sprite = Art.Atlas(Art.AtlasItem.Barricade, new Vector2(0.5f, 0.02f), 262f);
            e.main.color = Color.white;
            return Register(e, x, top, 0.9f, 0.82f, lane);
        }

        /// <summary>
        /// Laser wall: blocks its lane up to ~1.9 units, about one lane-spacing
        /// tall, so walls in neighbouring lanes don't merge into one tower.
        /// It can only be cleared by switching lanes (or a perfectly timed jump).
        /// </summary>
        public Ent AddLaser(float x, float top, int lane)
        {
            var e = Take(EntKind.Laser);
            if (e.beams == null)
            {
                // Built once per pooled object: two posts, four beams and a soft glow.
                e.main.sprite = Art.Get("Art/laser_post", 110f, new Vector2(0.5f, 0f));
                e.main.transform.localPosition = new Vector3(-0.62f, 0f, 0f);
                var post2 = AddSprite(e.root, "Post2", e.main.sprite, 6);
                post2.transform.localPosition = new Vector3(0.62f, 0f, 0f);
                e.glow = AddSprite(e.root, "Glow", Art.Get("FX/glow", 100f), 3);
                e.glow.transform.localPosition = new Vector3(0f, 1f, 0f);
                e.glow.transform.localScale = new Vector3(3.2f, 3.6f, 1f);
                e.glow.color = Palette.Magenta.WithAlpha(0.32f);
                e.beams = new SpriteRenderer[3];
                for (var b = 0; b < 3; b++)
                {
                    e.beams[b] = AddSprite(e.root, "Beam" + b, Art.Get("FX/beam", 100f), 4);
                    e.beams[b].transform.localPosition = new Vector3(0f, 0.35f + b * 0.6f, 0f);
                    e.beams[b].transform.localScale = new Vector3(0.98f, 1.6f, 1f);
                    e.beams[b].color = Palette.Magenta;
                }
            }
            return Register(e, x, top, 1.2f, 1.9f, lane);
        }

        public Ent AddCrate(float x, float bottom, int lane)
        {
            var e = Take(EntKind.Crate);
            e.main.sprite = Art.Atlas(Art.AtlasItem.Crate, new Vector2(0.5f, 0.03f), 190f);
            return Register(e, x, bottom, 1.9f, 1.35f, lane);
        }

        public Ent AddDrone(float x, float bottom, int lane)
        {
            var e = Take(EntKind.Drone);
            e.main.sprite = Art.Atlas(Art.AtlasItem.Drone, new Vector2(0.5f, 0.6f), 270f);
            e.main.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            return Register(e, x, bottom, 1.05f, 0.75f, lane);
        }

        public Ent AddCoin(float x, float y, int lane)
        {
            var e = Take(EntKind.Coin);
            // Big enough (and bright enough) to pick out from the amber roof lamps at speed.
            e.main.sprite = Art.Atlas(Art.AtlasItem.Credit, new Vector2(0.5f, 0.5f), 290f);
            e.main.color = Color.white;
            e.main.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            return Register(e, x, y, 0.75f, 0.75f, lane);
        }

        public Ent AddChip(float x, float y, int lane)
        {
            var e = Take(EntKind.Chip);
            e.main.sprite = Art.Atlas(Art.AtlasItem.Chip, new Vector2(0.5f, 0.5f), 330f);
            e.main.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            if (e.glow == null)
            {
                e.glow = AddSprite(e.root, "Glow", Art.Get("FX/glow", 100f), 4);
                e.glow.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            }
            e.glow.color = Palette.Cyan.WithAlpha(0.45f);
            return Register(e, x, y, 0.8f, 0.9f, lane);
        }

        public Ent AddPower(float x, float y, PowerKind kind, int lane)
        {
            var e = Take(EntKind.Power);
            e.power = kind;
            var c = PowerColor(kind);
            e.main.sprite = Art.Get("Art/capsule", 110f);
            e.main.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            e.main.color = c;
            if (e.icon == null)
            {
                e.icon = AddSprite(e.root, "Icon", null, 7);
                e.icon.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                e.glow = AddSprite(e.root, "Glow", Art.Get("FX/glow", 100f), 4);
                e.glow.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            }
            e.icon.sprite = Art.Get("UI/icon_" + PowerIcon(kind), 230f);
            e.icon.color = c;
            e.glow.color = c.WithAlpha(0.5f);
            return Register(e, x, y, 0.9f, 0.9f, lane);
        }

        private void AddMarker(float x, float top, Prompt prompt)
        {
            var e = Take(EntKind.Marker);
            e.main.sprite = null;
            Register(e, x, top, 0.1f, 0.1f, 0).prompt = prompt;
        }

        public static Color PowerColor(PowerKind k) => k switch
        {
            PowerKind.Shield => Palette.Cyan,
            PowerKind.Magnet => Palette.Magenta,
            PowerKind.Overclock => Palette.Amber,
            _ => Palette.Mint
        };

        public static string PowerIcon(PowerKind k) => k switch
        {
            PowerKind.Shield => "shield",
            PowerKind.Magnet => "magnet",
            PowerKind.Overclock => "bolt",
            _ => "heart"
        };

        // ------------------------------------------------------- coin paths --
        private void CoinLine(float x, float y, int count, float spacing, int lane)
        {
            for (var i = 0; i < count; i++) AddCoin(x + i * spacing, y, lane);
        }

        /// <summary>Coins along the arc a jump over an obstacle at x would follow.</summary>
        private void JumpArc(float x, float top, int lane)
        {
            var span = Mathf.Clamp(Speed * 0.5f, 3.5f, 7f);
            const int n = 6;
            for (var i = 0; i < n; i++)
            {
                var u = i / (float)(n - 1);
                var y = top + 0.6f + 1.9f * (1f - Mathf.Pow(u * 2f - 1f, 2f));
                AddCoin(x - span * 0.5f + u * span, y, lane);
            }
        }

        /// <summary>Coins over a gap. A taller hump hints "double jump here".</summary>
        private void GapArc(float edge, float gap, float top, bool needsDouble, int lane)
        {
            var n = needsDouble ? 9 : 6;
            var span = gap + 2.4f;
            var x0 = edge - 1.4f;
            for (var i = 0; i < n; i++)
            {
                var u = i / (float)(n - 1);
                var h = needsDouble ? 3.4f : 2f;
                var y = top + 0.7f + h * (1f - Mathf.Pow(u * 2f - 1f, 2f));
                AddCoin(x0 + u * span, y, lane);
            }
        }
    }
}
