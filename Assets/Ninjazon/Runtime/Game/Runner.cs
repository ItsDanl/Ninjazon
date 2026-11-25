using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ninjazon.Game
{
    /// <summary>
    /// The player character. The runner stays at a fixed screen X and the world
    /// scrolls past (a "treadmill"), so this class only simulates vertical motion
    /// plus the dash, and picks which animation frames to show.
    ///
    /// Feel tricks used here (common in good platformers):
    ///  - Coyote time: you can still jump for a moment after running off an edge.
    ///  - Jump buffer: pressing jump just before landing still jumps on landing.
    ///  - Faster falling than rising so jumps feel snappy, not floaty.
    /// </summary>
    public sealed class Runner : MonoBehaviour
    {
        public enum Pose { Idle, Run, Jump, Slide, Hit, Capture }

        // Tuning (world units, seconds). Jump apex ~2.5 units, air time ~0.55s.
        private const float Gravity = 62f;
        private const float FallMultiplier = 1.45f;
        private const float JumpVelocity = 17.5f;
        private const float DoubleJumpVelocity = 15.5f;
        private const float MaxFall = 34f;
        private const float SlideTime = 0.62f;
        private const float DashTime = 0.3f;
        private const float CoyoteTime = 0.1f;
        private const float BufferTime = 0.13f;
        private const float Ppu = 88f; // runner art scale: lower = bigger hero (he must read clearly at speed)

        public float HomeX { get; private set; } = -5.5f;

        /// <summary>Which depth lane the runner is in (0 = front, 2 = back).</summary>
        public int Lane { get; private set; } = 1;
        /// <summary>Smoothly animated lane value used for drawing (e.g. 1.4 while moving 1 -> 2).</summary>
        public float LaneVisual { get; private set; } = 1f;
        public float LastLaneChange { get; private set; } = -10f;
        public int PreviousLane { get; private set; } = 1;
        private SortingGroup sortingGroup;
        private float laneTilt;

        /// <summary>Where on screen (world X) the runner stands when not dashing.</summary>
        public void SetHome(float x)
        {
            HomeX = x;
            ApplyTransform();
        }
        public float X => HomeX + dashOffset;
        public float FeetY { get; private set; }
        public float VelocityY { get; private set; }
        public bool Grounded { get; private set; }
        public bool IsSliding => slideTimer > 0f;
        public bool IsDashing => dashTimer > 0f;
        public int JumpsUsed { get; private set; }
        public float DashCooldownLeft { get; private set; }
        public float DashCooldown { get; set; } = 3f;
        public float Dash01 => DashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(DashCooldownLeft / DashCooldown);
        public float Invulnerable { get; set; }
        public bool Shielded { get; set; }
        public Color TrailColor { get; set; } = Palette.Cyan;
        public SpriteRenderer Body => body;

        /// <summary>Fired with (kind) so the director can play sounds / count missions.</summary>
        public event Action<string> OnMove;

        private SpriteRenderer body;
        private SpriteRenderer shieldGlow;
        private SpriteRenderer shadow;
        private Fx fx;
        private Sprite[] idle, run, jump, slide, hit, capture;
        private Pose pose = Pose.Idle;
        private float frameClock;
        private float slideTimer;
        private float dashTimer;
        private float dashOffset;
        private float coyote;
        private float jumpBuffer;
        private bool slideQueued;
        private float spin;
        private float squash = 1f;
        private float dustClock;
        private float ghostClock;
        private float runSpeed;
        private float hitPoseTimer;

        public void Init(Fx effects, Texture2D sheet)
        {
            fx = effects;
            sortingGroup = gameObject.AddComponent<SortingGroup>();
            shadow = MakeChild("Shadow", Art.Get("FX/soft_dot", 100f), 18);
            shadow.color = new Color(0, 0, 0.05f, 0.45f);
            body = MakeChild("Body", null, 20);
            shieldGlow = MakeChild("Shield", Art.Get("FX/ring", 100f), 21);
            shieldGlow.transform.localScale = Vector3.one * 3.2f;
            shieldGlow.transform.localPosition = new Vector3(0, 0.85f, 0);
            shieldGlow.gameObject.SetActive(false);
            SetSheet(sheet);
        }

        private SpriteRenderer MakeChild(string childName, Sprite sprite, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Cuts the animation frames out of the runner sprite sheet.</summary>
        public void SetSheet(Texture2D sheet)
        {
            Sprite F(float x, float y, float w, float h, float pivotX = -1f)
            {
                var px = pivotX < 0 ? 0.5f : pivotX / w;
                return Art.Slice(sheet, new Rect(x, y, w, h), new Vector2(px, 0f), Ppu, "runner");
            }
            // The frames sit at uneven offsets inside their cells, so each frame's pivot is the
            // x of the character's HEAD in that cell (measured from the sheet). Anchoring on the
            // head keeps him steady; a shared pivot made him lurch ~20px back and forth.
            idle = new[] { F(18, 858, 180, 228, 111f), F(225, 858, 180, 228, 99f), F(430, 858, 180, 228, 81f) };
            run = new[]
            {
                F(18, 662, 190, 195, 120f), F(210, 662, 190, 195, 109f), F(400, 662, 190, 195, 115f),
                F(590, 662, 200, 195, 123f), F(800, 662, 200, 195, 122f), F(1010, 662, 200, 195, 129f),
                F(1225, 662, 205, 195, 114f)
            };
            jump = new[] { F(18, 481, 200, 180, 120f), F(215, 481, 200, 180, 97f), F(415, 481, 205, 180, 85f) };
            slide = new[] { F(18, 356, 205, 124, 110f), F(225, 356, 225, 124, 110f), F(445, 356, 225, 124, 113f), F(665, 356, 225, 124, 82f) };
            hit = new[] { F(18, 226, 205, 129, 110f), F(225, 226, 205, 129, 91f), F(430, 226, 205, 129, 137f) };
            capture = new[] { F(18, 0, 205, 225), F(225, 0, 205, 225), F(430, 0, 205, 225), F(625, 0, 205, 225) };
            body.sprite = idle[0];
        }

        public void ResetTo(float feetY)
        {
            FeetY = feetY;
            VelocityY = 0f;
            Grounded = true;
            JumpsUsed = 0;
            slideTimer = dashTimer = dashOffset = coyote = jumpBuffer = 0f;
            DashCooldownLeft = 0f;
            Invulnerable = 0f;
            Shielded = false;
            slideQueued = false;
            spin = 0f;
            hitPoseTimer = 0f;
            pose = Pose.Idle;
            Lane = PreviousLane = 1;
            LaneVisual = 1f;
            body.transform.localRotation = Quaternion.identity;
            ApplyTransform();
        }

        /// <summary>Place the runner on a surface instantly (used by the rescue teleport).</summary>
        public void Teleport(float feetY)
        {
            FeetY = feetY;
            VelocityY = 0f;
            Grounded = true;
            JumpsUsed = 0;
            spin = 0f;
            ApplyTransform();
        }

        // ------------------------------------------------------------ actions --
        public bool TryJump()
        {
            if (pose == Pose.Capture) return false;
            if (Grounded || coyote > 0f)
            {
                DoJump(JumpVelocity, false);
                return true;
            }
            if (JumpsUsed < 2)
            {
                DoJump(DoubleJumpVelocity, true);
                return true;
            }
            jumpBuffer = BufferTime; // remember the press; we'll jump when we land
            return false;
        }

        private void DoJump(float velocity, bool isDouble)
        {
            slideTimer = 0f;
            slideQueued = false;
            VelocityY = velocity;
            Grounded = false;
            coyote = 0f;
            JumpsUsed = isDouble ? 2 : 1;
            squash = isDouble ? 1.0f : 1.06f; // subtle: big squash smears pixel art
            if (isDouble)
            {
                spin = 360f;
                fx.Ring(new Vector3(X, FeetY + 0.2f), TrailColor, 2.2f, 0.3f);
                OnMove?.Invoke("double_jump");
            }
            else
            {
                fx.Puff(new Vector3(X - 0.2f, FeetY + 0.1f), new Color(0.45f, 0.4f, 0.75f, 0.35f), 4, runSpeed);
                OnMove?.Invoke("jump");
            }
        }

        /// <summary>Move one lane back (+1) or forward (-1). Works on the ground and in the air.</summary>
        public bool TryLane(int direction)
        {
            if (pose == Pose.Capture) return false;
            var next = Mathf.Clamp(Lane + direction, 0, 2);
            if (next == Lane) return false;
            PreviousLane = Lane;
            Lane = next;
            LastLaneChange = Time.time;
            laneTilt = direction;
            fx.Afterimage(body, TrailColor, 0.18f);
            OnMove?.Invoke("lane");
            return true;
        }

        public bool TrySlide()
        {
            if (pose == Pose.Capture) return false;
            if (Grounded)
            {
                if (slideTimer <= 0f) OnMove?.Invoke("slide");
                slideTimer = SlideTime;
                return true;
            }
            // In the air, "slide" slams you down fast and slides on landing.
            VelocityY = Mathf.Min(VelocityY, -26f);
            slideQueued = true;
            spin = 0f;
            OnMove?.Invoke("dive");
            return true;
        }

        public bool TryDash()
        {
            if (pose == Pose.Capture || DashCooldownLeft > 0f) return false;
            dashTimer = DashTime;
            DashCooldownLeft = DashCooldown;
            slideTimer = 0f;
            if (!Grounded && VelocityY < 0f) VelocityY = 0f;
            OnMove?.Invoke("dash");
            return true;
        }

        public void PlayHit()
        {
            hitPoseTimer = 0.4f;
            slideTimer = 0f;
        }

        public void PlayCapture()
        {
            pose = Pose.Capture;
            frameClock = 0f;
            spin = 0f;
            dashTimer = 0f;
            slideTimer = 0f;
        }

        // ------------------------------------------------------------ physics --
        /// <param name="groundAt">Returns the highest walkable surface top under x that is not above maxY (or -999).</param>
        public void Simulate(float dt, float worldSpeed, Func<float, float, float> groundAt)
        {
            runSpeed = worldSpeed;
            DashCooldownLeft = Mathf.Max(0f, DashCooldownLeft - dt);
            Invulnerable = Mathf.Max(0f, Invulnerable - dt);
            jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);
            hitPoseTimer = Mathf.Max(0f, hitPoseTimer - dt);
            if (pose == Pose.Capture) return;

            // Quick, eased lane slide (~0.12s per lane).
            LaneVisual = Mathf.MoveTowards(LaneVisual, Lane, dt * 9f);
            laneTilt = Mathf.MoveTowards(laneTilt, 0f, dt * 6f);

            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                dashOffset = Mathf.MoveTowards(dashOffset, 1.3f, dt * 14f);
            }
            else
            {
                dashOffset = Mathf.MoveTowards(dashOffset, 0f, dt * 3f);
            }

            if (slideTimer > 0f) slideTimer -= dt;

            var prevY = FeetY;
            if (Grounded)
            {
                var g = groundAt(X, FeetY + 0.05f);
                if (g < FeetY - 0.05f)
                {
                    Grounded = false;      // ran off an edge
                    coyote = CoyoteTime;
                    JumpsUsed = 0;
                }
                else
                {
                    FeetY = g;
                    VelocityY = 0f;
                }
            }

            if (!Grounded)
            {
                coyote = Mathf.Max(0f, coyote - dt);
                var gravityScale = dashTimer > 0f ? 0f : (VelocityY < 0f ? FallMultiplier : 1f);
                VelocityY = Mathf.Max(-MaxFall, VelocityY - Gravity * gravityScale * dt);
                FeetY += VelocityY * dt;

                if (VelocityY <= 0f)
                {
                    var g = groundAt(X, prevY + 0.02f);
                    if (FeetY <= g)
                    {
                        Land(g);
                    }
                }
            }

            ApplyTransform();
        }

        private void Land(float surface)
        {
            var impact = -VelocityY;
            FeetY = surface;
            VelocityY = 0f;
            Grounded = true;
            JumpsUsed = 0;
            spin = 0f;
            squash = impact > 20f ? 0.9f : 0.95f;
            fx.Puff(new Vector3(X, FeetY + 0.05f), new Color(0.5f, 0.45f, 0.8f, 0.4f), impact > 20f ? 7 : 4, runSpeed);
            if (impact > 22f)
            {
                fx.Kick(Vector2.down, 0.4f);
                fx.Flipbook("burst", new Vector3(X, FeetY), TrailColor, 2.2f, 0.3f, 42, true, Vector3.left * runSpeed);
            }
            OnMove?.Invoke("land");
            if (slideQueued)
            {
                slideQueued = false;
                slideTimer = SlideTime;
                OnMove?.Invoke("slide");
            }
            if (jumpBuffer > 0f)
            {
                jumpBuffer = 0f;
                DoJump(JumpVelocity, false);
            }
        }

        private void ApplyTransform()
        {
            transform.position = new Vector3(X, FeetY + World.LaneY(LaneVisual), 0f);
            var s = World.LaneScale(LaneVisual);
            transform.localScale = new Vector3(s, s, 1f);
            // Draw in front of everything in our lane (and behind lanes closer to the camera).
            if (sortingGroup != null) sortingGroup.sortingOrder = World.LaneOrder(Mathf.RoundToInt(LaneVisual)) + 5;
        }

        // ----------------------------------------------------------- visuals --
        public void Animate(float dt)
        {
            frameClock += dt;
            Sprite[] clip;
            float fps;
            var loop = true;
            var target = pose == Pose.Capture ? Pose.Capture
                : hitPoseTimer > 0f ? Pose.Hit
                : IsSliding ? Pose.Slide
                : !Grounded ? Pose.Jump
                : runSpeed > 0.1f ? Pose.Run : Pose.Idle;
            if (target != pose)
            {
                pose = target;
                frameClock = 0f;
            }

            switch (pose)
            {
                case Pose.Run: clip = run; fps = Mathf.Lerp(11f, 19f, Mathf.InverseLerp(8f, 16f, runSpeed)); break;
                case Pose.Jump: clip = jump; fps = 0f; break;
                case Pose.Slide: clip = slide; fps = 14f; loop = false; break;
                case Pose.Hit: clip = hit; fps = 10f; loop = false; break;
                case Pose.Capture: clip = capture; fps = 4.5f; loop = false; break;
                default: clip = idle; fps = 4f; break;
            }

            int frame;
            if (pose == Pose.Jump)
                frame = VelocityY > 6f ? 0 : VelocityY > -4f ? 1 : 2;
            else
            {
                frame = Mathf.FloorToInt(frameClock * fps);
                frame = loop ? frame % clip.Length : Mathf.Min(frame, clip.Length - 1);
            }
            body.sprite = clip[frame];

            // Double-jump front flip. The angle counts down 360 -> 0, which turns
            // clockwise (forwards for a runner facing right).
            if (spin > 0f)
            {
                spin = Mathf.Max(0f, spin - dt * 1150f);
                var r = Quaternion.Euler(0, 0, spin);
                body.transform.localRotation = r;
                // Rotate around the body's middle (about 1.1 units up at the current art scale) instead of the feet.
                var centre = new Vector3(0f, 1.1f, 0f);
                body.transform.localPosition = centre - r * centre;
            }
            else
            {
                // Slight lean while switching lanes.
                body.transform.localRotation = Quaternion.Euler(0f, 0f, laneTilt * 7f);
                body.transform.localPosition = Vector3.zero;
            }

            // Squash & stretch: brief, subtle, eases back to 1.
            squash = Mathf.Lerp(squash, 1f, 1f - Mathf.Exp(-14f * dt));
            body.transform.localScale = new Vector3(2f - squash, squash, 1f);

            // Invulnerability blink.
            var visible = Invulnerable <= 0f || Mathf.Repeat(Time.unscaledTime * 14f, 1f) > 0.35f;
            body.color = visible ? Color.white : new Color(1f, 0.6f, 0.7f, 0.35f);

            // Ground shadow shrinks as you go higher.
            shadow.gameObject.SetActive(pose != Pose.Capture);

            shieldGlow.gameObject.SetActive(Shielded && pose != Pose.Capture);
            if (Shielded)
            {
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f;
                shieldGlow.transform.localScale = Vector3.one * 3.1f * pulse;
                shieldGlow.color = Palette.Cyan.WithAlpha(0.75f);
            }

            // Trail effects.
            if (pose == Pose.Run)
            {
                dustClock -= dt;
                if (dustClock <= 0f)
                {
                    // One small splash per footfall on the wet roof.
                    dustClock = 0.2f;
                    fx.Flipbook("splash", new Vector3(X - 0.15f, FeetY + 0.02f), new Color(0.6f, 0.6f, 0.95f, 0.55f), 0.7f, 0.25f, 17, true,
                        Vector3.left * runSpeed);
                }
            }
            if (IsDashing || spin > 0f)
            {
                ghostClock -= dt;
                if (ghostClock <= 0f)
                {
                    ghostClock = 0.035f;
                    fx.Afterimage(body, TrailColor, 0.22f);
                }
            }
        }

        public void UpdateShadow(float groundY)
        {
            var height = Mathf.Max(0f, FeetY - groundY);
            var s = Mathf.Lerp(1.6f, 0.6f, Mathf.Clamp01(height / 3f));
            shadow.transform.position = new Vector3(X, groundY + World.LaneY(LaneVisual) + 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s * 1.4f, s * 0.35f, 1f);
            shadow.color = new Color(0, 0, 0.05f, groundY < -50f ? 0f : 0.45f * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(height / 3f)));
        }

        /// <summary>Collision box in world space (smaller than the art so near-misses feel fair).</summary>
        public Rect Hitbox
        {
            get
            {
                if (IsSliding) return new Rect(X - 0.5f, FeetY, 1.0f, 0.62f);
                return new Rect(X - 0.28f, FeetY + 0.05f, 0.56f, 1.45f);
            }
        }
    }
}
