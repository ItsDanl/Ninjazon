using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ninjazon.Game
{
    /// <summary>Entry point. The Bootstrap scene calls Launch() and everything else is built in code.</summary>
    public static class GameBoot
    {
        public static void Launch()
        {
            if (Object.FindAnyObjectByType<GameDirector>() != null) return;
            var go = new GameObject("Ninjazon");
            go.AddComponent<GameDirector>();
        }
    }

    /// <summary>
    /// Runs the whole game: builds the scene, owns the state machine
    /// (Menu -> Running -> Dying -> Continue -> Results), scoring, combos,
    /// power-ups, the tutorial and missions.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        private enum State { Menu, Running, Paused, Dying, Continue, Results }

        private const float BaseOrtho = 5.4f;
        private const float StartTop = -3f;

        public Profile Profile { get; private set; }
        public GameAudio Audio { get; private set; }

        private Camera cam;
        private Fx fx;
        private World world;
        private Runner runner;
        private readonly GameInput input = new();

        private Hud hud;
        private MenuScreen menu;
        private ShopScreen shop;
        private MissionsScreen missions;
        private PauseScreen pause;
        private SettingsScreen settings;
        private ContinueScreen continueScreen;
        private ResultsScreen results;
        private TutorialOverlay tutorial;
        private readonly Stack<UIScreen> overlays = new();

        private State state;

        // --- run state
        private float distance, scoreF, speed, timeScale = 1f, targetTimeScale = 1f, hitstop;
        private int runCredits, pips, maxPips, comboCount, mult = 1, bestMult = 1;
        private int slices, laneSwitches, doubleJumps, powerUps, closeCalls, continuesUsed, coinStreak;
        private float cleanDistance, bestClean, coinStreakTimer;
        private float shieldTime, magnetTime, overclockTime;
        private bool tutorialRun;
        private System.Func<float, float, float> groundFn;
        private int promptLane;

        /// <summary>The runner's feet on screen (physics FeetY is in front-lane space).</summary>
        private float RunnerY => runner.transform.position.y;
        private Prompt activePrompt;
        private float dyingTimer, continueTimer;
        private float camY, camZoom;
        private float speedLineClock;
        private float hintsTimer;
        private float missionCheckClock;
        private int district;
        private readonly bool[] announced = new bool[3];

        // ============================================================ setup ==
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Profile = new Profile();
            Audio = gameObject.AddComponent<GameAudio>();
            Audio.Init(Profile.Data.musicVolume, Profile.Data.sfxVolume);
            UI.Audio = Audio;
            Tw.Ensure(gameObject);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = BaseOrtho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Night;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            fx = new GameObject("Fx").AddComponent<Fx>();
            fx.transform.SetParent(transform, false);
            fx.Init(cam);
            fx.ShakeEnabled = Profile.Data.screenShake;

            world = new GameObject("World").AddComponent<World>();
            world.transform.SetParent(transform, false);
            world.Init(cam, fx);

            runner = new GameObject("Runner").AddComponent<Runner>();
            runner.transform.SetParent(transform, false);
            runner.Init(fx, Art.RunnerSheet(Profile.TrailColor, Profile.Data.trail == 0));
            runner.TrailColor = Profile.TrailColor;
            runner.OnMove += OnRunnerMove;
            groundFn = (x, maxY) => world.GroundAt(x, maxY, runner.Lane);

            BuildUI();
            EnterMenu(true);
        }

        private void BuildUI()
        {
            UI.EnsureEventSystem(transform);
            var canvas = UI.CreateCanvas(transform, "UI", 10);
            var safe = UI.Stretch(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<Runtime.UI.SafeAreaFitter>();
            hud = new Hud(safe, Pause);
            menu = new MenuScreen(safe, this);
            results = new ResultsScreen(safe, this);
            continueScreen = new ContinueScreen(safe, this);
            pause = new PauseScreen(safe, this);
            tutorial = new TutorialOverlay(safe);
            missions = new MissionsScreen(safe, this);
            shop = new ShopScreen(safe, this);
            settings = new SettingsScreen(safe, this);
        }

        // ======================================================= menu flow ==
        private void EnterMenu(bool instant)
        {
            state = State.Menu;
            input.Enabled = false;
            input.Clear();
            HideAllOverlays();
            hud.HideInstant();
            results.Hide();
            continueScreen.Hide();
            pause.Hide();
            tutorial.HideInstant();
            Profile.RefreshMissions();
            world.SetHazards(false);
            world.Speed = 8f;
            world.Difficulty = 0f;
            world.ResetWorld(false, StartTop);
            world.SetDistrict(0, false);
            fx.ClearAll();
            runner.ResetTo(StartTop);
            speed = 0f;
            timeScale = targetTimeScale = 1f;
            camY = 0f;
            camZoom = BaseOrtho;
            cam.orthographicSize = BaseOrtho;
            fx.SetCameraBase(new Vector3(0f, 0f, -10f));
            Audio.SetMuffled(true);
            Audio.SetMusicPitch(1f);
            menu.Refresh(Profile);
            if (instant) { menu.Show(); }
            else Tw.Delay(0.15f, menu.Show);
        }

        public void StartRun()
        {
            if (state == State.Running) return;
            HideAllOverlays();
            menu.Hide();
            results.Hide();
            pause.Hide();
            continueScreen.Hide();
            Profile.RefreshMissions();

            tutorialRun = !Profile.Data.tutorialDone;
            world.SetHazards(true);
            world.FortuneMultiplier = Upgrades.FortuneMultiplier(Profile.Level(Upgrades.Fortune));
            world.Difficulty = 0f;
            world.Speed = 8.5f;
            world.ResetWorld(tutorialRun, StartTop);
            fx.ClearAll();
            district = 0;
            world.SetDistrict(0, false);

            distance = scoreF = 0f;
            runCredits = comboCount = slices = laneSwitches = doubleJumps = powerUps = closeCalls = continuesUsed = coinStreak = 0;
            mult = bestMult = 1;
            cleanDistance = bestClean = 0f;
            shieldTime = magnetTime = overclockTime = 0f;
            maxPips = Upgrades.MaxPips(Profile.Level(Upgrades.Armor));
            pips = maxPips;
            activePrompt = Prompt.None;
            timeScale = targetTimeScale = 1f;
            hitstop = 0f;
            speed = 0f;
            for (var i = 0; i < 3; i++) announced[i] = false;

            runner.ResetTo(StartTop);
            runner.DashCooldown = Upgrades.DashCooldown(Profile.Level(Upgrades.Dash));
            ApplyTrail();

            hud.Show();
            hud.SetPips(pips, maxPips);
            hud.SetMultiplier(1, 0f);
            hud.SetCredits(0);
            var showHints = Profile.Data.controlHints && (tutorialRun || Profile.Data.totalRuns < 4);
            hud.SetHintsVisible(showHints, true);
            hintsTimer = showHints ? 22f : 0f;
            for (var i = 0; i < 3; i++) hud.SetPower(i, false, PowerKind.Shield, 0f);
            hud.Toast(tutorialRun ? "TRAINING" : "GO!", Palette.Cyan, 0.5f);

            Audio.SetMuffled(false);
            input.Clear();
            input.Enabled = true;
            state = State.Running;
        }

        public void StartRunFromResults()
        {
            state = State.Results; // allow StartRun
            StartRun();
        }

        public void Pause()
        {
            if (state != State.Running) return;
            state = State.Paused;
            input.Enabled = false;
            input.Clear();
            Audio.SetMuffled(true);
            hud.Hide(); // the pause panel is the only thing on screen
            pause.Show();
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            HideAllOverlays();
            pause.Hide();
            hud.Show();
            state = State.Running;
            Audio.SetMuffled(false);
            // Ease back in from slow-motion so resuming never feels like an ambush.
            timeScale = 0.15f;
            input.Clear();
            input.Enabled = true;
        }

        public void Restart()
        {
            BankRunCredits();
            state = State.Results;
            StartRun();
        }

        public void GoToMenu()
        {
            if (state == State.Paused || state == State.Running) BankRunCredits();
            EnterMenu(false);
        }

        public void OpenShop() => PushOverlay(shop);
        public void OpenMissions() => PushOverlay(missions);
        public void OpenSettings() => PushOverlay(settings);

        // The screen an overlay (shop, contracts, settings) opens on top of. It is hidden
        // while the overlay is up so the two never show through each other.
        private UIScreen BaseScreen => state switch
        {
            State.Menu => menu,
            State.Paused => pause,
            State.Results => results,
            _ => null,
        };

        private void PushOverlay(UIScreen screen)
        {
            if (overlays.Count > 0 && overlays.Peek() == screen) return;
            if (overlays.Count == 0) BaseScreen?.Hide();
            overlays.Push(screen);
            screen.Show();
        }

        public void Back()
        {
            if (overlays.Count == 0) return;
            var top = overlays.Pop();
            top.Hide();
            if (top == settings) Profile.Save();
            if (state == State.Menu) menu.Refresh(Profile);
            if (overlays.Count == 0) BaseScreen?.Show();
        }

        private void HideAllOverlays()
        {
            while (overlays.Count > 0) overlays.Pop().Hide();
        }

        public void ApplySettings(bool save)
        {
            var d = Profile.Data;
            Audio.SetVolumes(d.musicVolume, d.sfxVolume);
            fx.ShakeEnabled = d.screenShake;
            if (save) Profile.Save();
        }

        public void ApplyTrail()
        {
            runner.SetSheet(Art.RunnerSheet(Profile.TrailColor, Profile.Data.trail == 0));
            runner.TrailColor = Profile.TrailColor;
        }

        public void ReplayTutorial()
        {
            Profile.Data.tutorialDone = false;
            Profile.Save();
            HideAllOverlays();
            if (state == State.Paused) BankRunCredits();
            state = State.Results;
            StartRun();
        }

        // ============================================================ loop ==
        private void Update()
        {
            var udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            input.Poll();
            while (input.TryNext(out var action))
            {
                if (action == PlayerAction.Pause) { HandleBack(); continue; }
                if (state == State.Running) HandleAction(action);
            }

            // Desktop shortcut: Space starts a run from the menu.
            var kb = Keyboard.current;
            if (state == State.Menu && overlays.Count == 0 && kb != null && kb.spaceKey.wasPressedThisFrame) StartRun();
            if (state == State.Results && overlays.Count == 0 && kb != null && kb.spaceKey.wasPressedThisFrame) StartRunFromResults();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev only: F12 saves a full-resolution screenshot to 2DPLATFORMER/Screenshots (used for art reviews).
            if (kb != null && kb.f12Key.wasPressedThisFrame)
            {
                var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "Screenshots"));
                System.IO.Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"nz_{System.DateTime.Now:HHmmss_fff}.png"));
            }
#endif

            menu.Tick();
            tutorial.Tick();
            UpdateCameraLayout();
            fx.Danger = state == State.Running && pips == 1; // red pulsing screen edges on last health
            world.HighlightLane(runner.LaneVisual);

            switch (state)
            {
                case State.Running: Simulate(udt); break;
                case State.Dying: TickDying(udt); break;
                case State.Continue: TickContinue(udt); break;
                case State.Paused: fx.Tick(0f, udt); break;
                default:
                    world.Advance(speed * udt, udt);
                    runner.Simulate(udt, speed, groundFn);
                    runner.Animate(udt);
                    runner.UpdateShadow(groundFn(runner.X, runner.FeetY + 0.01f));
                    fx.Tick(udt, udt);
                    break;
            }
        }

        private void HandleBack()
        {
            if (overlays.Count > 0) { Back(); return; }
            switch (state)
            {
                case State.Running: Pause(); break;
                case State.Paused: Resume(); break;
                case State.Results: GoToMenu(); break;
                case State.Continue: DeclineContinue(); break;
            }
        }

        private void UpdateCameraLayout()
        {
            // Keep the runner at ~22% from the left edge on any screen shape.
            var halfWidth = BaseOrtho * cam.aspect;
            runner.SetHome(-halfWidth * 0.56f);
        }

        private void Simulate(float udt)
        {
            // --- time: hit-stop freezes, slow-motion for tutorial prompts.
            targetTimeScale = activePrompt != Prompt.None ? 0.06f : 1f;
            timeScale = Mathf.MoveTowards(timeScale, targetTimeScale, udt * (targetTimeScale < timeScale ? 8f : 2.5f));
            var dt = udt * timeScale;
            if (hitstop > 0f) { hitstop -= udt; dt = 0f; }

            // --- speed ramps up over the run (and launches from 0 at the start).
            var target = tutorialRun ? 8f : 8.5f + 7.5f * (1f - Mathf.Exp(-distance / 1500f));
            speed = Mathf.MoveTowards(speed, target, dt * 9f);
            var effective = speed * (runner.IsDashing ? 1.9f : 1f);
            var dx = effective * dt;

            world.Speed = target;
            // Eased ramp: gentle for the first ~500m, full difficulty around 3000m.
            world.Difficulty = tutorialRun ? 0f : Mathf.Pow(Mathf.Clamp01(distance / 3000f), 0.8f);
            world.Advance(dx, dt);
            distance += dx;
            cleanDistance += dx;
            bestClean = Mathf.Max(bestClean, cleanDistance);
            scoreF += dx * mult;

            runner.Simulate(dt, effective, groundFn);
            runner.UpdateShadow(groundFn(runner.X, runner.FeetY + 0.01f));

            Collide(dt);
            if (state != State.Running) return;
            CheckPrompts();
            if (runner.FeetY < -9f) OnFall();
            if (state != State.Running) return;

            TickPowers(dt);
            coinStreakTimer -= dt;
            if (coinStreakTimer <= 0f) coinStreak = 0;

            // Speed lines sell velocity at high speed and during dashes.
            speedLineClock -= dt;
            if ((speed > 11.5f || runner.IsDashing) && speedLineClock <= 0f)
            {
                speedLineClock = runner.IsDashing ? 0.015f : Mathf.Lerp(0.12f, 0.04f, Mathf.InverseLerp(11.5f, 16f, speed));
                var y = Random.Range(-4.5f, 4.5f) + cam.transform.position.y;
                fx.Spawn("FX/line", new Vector3(world.RightEdge + 1f, y), new Vector3(-effective * 3.2f, 0f), 0.45f,
                    Random.Range(2.5f, 5f), Random.Range(2.5f, 5f), (runner.IsDashing ? runner.TrailColor : Palette.Text).WithAlpha(0.18f), 0f, 0f, 44);
            }

            if (hintsTimer > 0f)
            {
                hintsTimer -= dt;
                if (hintsTimer <= 0f) hud.SetHintsVisible(false);
            }

            // New district every 650m: colour mood shift + title card.
            var nextDistrict = tutorialRun ? 0 : Mathf.FloorToInt(distance / 650f);
            if (nextDistrict != district)
            {
                district = nextDistrict;
                world.SetDistrict(district, false);
                hud.Toast(World.DistrictNames[district % World.DistrictNames.Length], Palette.Cyan, 1.3f);
                Audio.Play("whoosh", 0.7f, 0.8f);
                fx.Flash(0.2f);
            }

            missionCheckClock -= udt;
            if (missionCheckClock <= 0f) { missionCheckClock = 0.5f; AnnounceMissions(); }

            // --- camera: gentle follow when high, zoom out with speed, punch in on dash.
            var follow = Mathf.Max(0f, (RunnerY + 0.6f) * 0.3f);
            camY = Mathf.Lerp(camY, follow, 1f - Mathf.Exp(-3f * udt));
            var zoom = BaseOrtho + Mathf.InverseLerp(8.5f, 16f, speed) * 0.35f - (runner.IsDashing ? 0.25f : 0f);
            camZoom = Mathf.Lerp(camZoom <= 0f ? BaseOrtho : camZoom, zoom, 1f - Mathf.Exp(-6f * udt));
            cam.orthographicSize = camZoom;
            fx.SetCameraBase(new Vector3(0f, camY, -10f));
            Audio.SetMusicPitch(1f + Mathf.InverseLerp(8.5f, 16f, speed) * 0.05f);

            runner.Animate(dt);
            fx.Tick(dt, udt);
            UpdateHud();
        }

        // ======================================================= actions ==
        private void HandleAction(PlayerAction action)
        {
            if (activePrompt != Prompt.None)
            {
                // During a tutorial prompt only the requested move is accepted.
                var wanted = activePrompt switch
                {
                    Prompt.Lane => runner.Lane > promptLane ? PlayerAction.LaneDown : PlayerAction.LaneUp,
                    Prompt.Dash => PlayerAction.Dash,
                    _ => PlayerAction.Jump
                };
                if (action != wanted) return;
                var finished = activePrompt != Prompt.Lane || runner.Lane + (action == PlayerAction.LaneUp ? 1 : -1) == promptLane;
                if (finished)
                {
                    activePrompt = Prompt.None;
                    tutorial.Hide();
                    fx.Popup(new Vector3(runner.X, RunnerY + 2.4f), "NICE!", Palette.Mint, 7f);
                }
            }

            switch (action)
            {
                case PlayerAction.Jump: runner.TryJump(); break;
                case PlayerAction.LaneUp: runner.TryLane(1); break;
                case PlayerAction.LaneDown: runner.TryLane(-1); break;
                case PlayerAction.Dash:
                    if (!runner.TryDash()) Audio.Play("ui_deny", 0.4f);
                    break;
            }
        }

        private void OnRunnerMove(string move)
        {
            switch (move)
            {
                case "jump": Audio.PlayVaried("jump", 0.8f); break;
                case "double_jump": Audio.PlayVaried("double_jump", 0.85f); if (state == State.Running) doubleJumps++; break;
                case "slide": Audio.PlayVaried("slide", 0.7f); break;
                case "lane":
                    Audio.Play("whoosh", 0.35f, 1.6f + Random.Range(-0.1f, 0.1f));
                    if (state == State.Running) laneSwitches++;
                    break;
                case "dive": Audio.Play("whoosh", 0.6f, 1.3f); break;
                case "land": Audio.PlayVaried("land", 0.6f); break;
                case "dash":
                    Audio.PlayVaried("dash", 0.9f);
                    fx.ChromaPulse(0.6f);
                    fx.Shake(0.15f);
                    fx.Ring(new Vector3(runner.X, RunnerY + 0.8f), runner.TrailColor, 3f, 0.3f);
                    break;
            }
        }

        // ===================================================== collisions ==
        private void Collide(float dt)
        {
            var box = runner.Hitbox;
            var centre = box.center;
            var magnet = magnetTime > 0f;
            var ents = world.Ents;
            for (var i = 0; i < ents.Count; i++)
            {
                var e = ents[i];
                if (e.dead || e.kind == EntKind.Marker) continue;

                if (e.IsPickup)
                {
                    if (magnet && e.kind != EntKind.Power && !e.magnetized)
                    {
                        var d = new Vector2(e.x, e.y + e.h * 0.5f) - centre;
                        if (d.sqrMagnitude < 40f)
                        {
                            e.magnetized = true;
                            world.MoveToLane(e, runner.Lane); // pull it into our lane
                        }
                    }
                    if (e.magnetized)
                    {
                        if (e.lane != runner.Lane) world.MoveToLane(e, runner.Lane);
                        var k = Mathf.Min(1f, dt * 14f);
                        e.x = Mathf.Lerp(e.x, centre.x, k);
                        e.y = e.baseY = Mathf.Lerp(e.y, centre.y - e.h * 0.5f, k);
                    }
                    if (e.lane != runner.Lane) continue;
                    var grab = new Rect(e.x - e.w * 0.5f - 0.15f, e.y - 0.15f, e.w + 0.3f, e.h + 0.3f);
                    if (grab.Overlaps(box)) Collect(e);
                    continue;
                }

                // --- hazards (only things in our lane can hit us)
                var hb = e.Box;
                var sameLane = e.lane == runner.Lane;
                if (sameLane && hb.Overlaps(box))
                {
                    if (runner.IsDashing && e.Sliceable) { Slice(e); continue; }
                    if (e.kind == EntKind.Crate && runner.FeetY >= e.y + e.h - 0.2f) continue; // standing on it
                    if (runner.Invulnerable > 0f) continue;
                    Hit(e);
                    if (state != State.Running) return;
                    continue;
                }

                if (!e.passed && hb.xMax < box.xMin)
                {
                    e.passed = true;
                    if (runner.Invulnerable > 0f) continue;
                    // Lane dodge: you left this obstacle's lane just before it reached you.
                    var dodged = !sameLane && runner.PreviousLane == e.lane && Time.time - runner.LastLaneChange < 0.5f;
                    // Jump-over close call: cleared it in our own lane with little room to spare.
                    var gap = Mathf.Min(Mathf.Abs(box.yMin - hb.yMax), Mathf.Abs(hb.yMin - box.yMax));
                    var vaulted = sameLane && gap < 0.45f && e.kind != EntKind.Crate;
                    if (sameLane || dodged) AddCombo(1);
                    if (dodged || vaulted)
                    {
                        closeCalls++;
                        var bonus = 25 * mult;
                        scoreF += bonus;
                        fx.Popup(new Vector3(runner.X + 0.5f, RunnerY + 2.2f), (dodged ? "DODGE +" : "CLOSE +") + bonus, Palette.Cyan, 5.5f);
                        Audio.Play("combo", 0.5f, 1.4f);
                    }
                }
            }
        }

        private void Collect(Ent e)
        {
            world.Kill(e);
            var at = new Vector3(e.x, e.y + e.h * 0.5f + World.LaneY(e.lane));
            switch (e.kind)
            {
                case EntKind.Coin:
                    var value = overclockTime > 0f ? 2 : 1;
                    runCredits += value;
                    scoreF += 5 * mult;
                    coinStreak = Mathf.Min(coinStreak + 1, 16);
                    coinStreakTimer = 0.45f;
                    Audio.Play("coin", 0.55f, 1f + coinStreak * 0.035f);
                    fx.Burst(at, Palette.Amber, 4, 3f, 0.12f, 0.3f);
                    break;
                case EntKind.Chip:
                    runCredits += overclockTime > 0f ? 30 : 15;
                    scoreF += 250 * mult;
                    Audio.Play("chip", 0.8f);
                    fx.Burst(at, Palette.Cyan, 14, 6f);
                    fx.Ring(at, Palette.Cyan, 3f);
                    fx.Popup(at + Vector3.up, "DATA +" + (250 * mult), Palette.Cyan, 6f);
                    break;
                case EntKind.Power:
                    ApplyPower(e.power, at);
                    break;
            }
        }

        private void ApplyPower(PowerKind kind, Vector3 at)
        {
            powerUps++;
            AddCombo(1);
            Audio.Play("powerup", 0.85f);
            var c = World.PowerColor(kind);
            fx.Burst(at, c, 16, 7f);
            fx.Ring(at, c, 4f, 0.45f);
            fx.Flash(0.25f);
            switch (kind)
            {
                case PowerKind.Shield:
                    shieldTime = 14f;
                    runner.Shielded = true;
                    hud.Toast("SHIELD UP", c);
                    break;
                case PowerKind.Magnet:
                    magnetTime = Upgrades.MagnetTime(Profile.Level(Upgrades.Magnet));
                    hud.Toast("MAGNET", c);
                    break;
                case PowerKind.Overclock:
                    overclockTime = Upgrades.OverclockTime(Profile.Level(Upgrades.Overclock));
                    hud.Toast("2X CREDITS", c);
                    break;
                default:
                    if (pips < maxPips)
                    {
                        pips++;
                        hud.Toast("+1 HEALTH", c);
                    }
                    else
                    {
                        runCredits += 25;
                        hud.Toast("+25 CREDITS", Palette.Amber);
                    }
                    break;
            }
        }

        private void TickPowers(float dt)
        {
            if (shieldTime > 0f)
            {
                shieldTime -= dt;
                if (shieldTime <= 0f) runner.Shielded = false;
            }
            magnetTime = Mathf.Max(0f, magnetTime - dt);
            overclockTime = Mathf.Max(0f, overclockTime - dt);
            fx.SetBloomBoost(overclockTime > 0f ? 0.35f : 0f);
        }

        private void Slice(Ent e)
        {
            world.Kill(e);
            slices++;
            var bonus = 100 * mult;
            scoreF += bonus;
            AddCombo(2);
            var at = new Vector3(e.x, e.y + e.h * 0.5f + World.LaneY(e.lane));
            var c = e.kind == EntKind.Drone ? Palette.Magenta : Palette.Amber;
            fx.Sparks(at, Vector3.right, Color.white, 10, 14f);
            fx.Burst(at, c, 16, 8f, 0.2f);
            fx.Ring(at, runner.TrailColor, 3.5f, 0.3f);
            fx.Popup(at + Vector3.up * 1.2f, "SLICE +" + bonus, runner.TrailColor, 7f);
            fx.Shake(0.28f);
            hitstop = 0.05f;
            Audio.PlayVaried("slice", 0.9f);
        }

        private void Hit(Ent e)
        {
            var at = new Vector3(runner.X, RunnerY + 0.9f);
            if (e.Sliceable || e.kind == EntKind.Crate) world.Kill(e);

            if (tutorialRun)
            {
                runner.Invulnerable = 1.2f;
                runner.PlayHit();
                fx.Burst(at, Palette.Red, 10, 5f);
                Audio.Play("hit", 0.5f);
                return;
            }

            if (runner.Shielded)
            {
                runner.Shielded = false;
                shieldTime = 0f;
                runner.Invulnerable = 1.0f;
                Audio.Play("shield_break", 0.9f);
                fx.Ring(at, Palette.Cyan, 5f, 0.4f);
                fx.Burst(at, Palette.Cyan, 20, 8f);
                fx.Shake(0.3f);
                hitstop = 0.06f;
                fx.Popup(at + Vector3.up, "BLOCKED!", Palette.Cyan, 7f);
                return;
            }

            pips--;
            comboCount = 0;
            mult = 1;
            cleanDistance = 0f;
            runner.PlayHit();
            runner.Invulnerable = 1.5f;
            hitstop = 0.09f;
            fx.Shake(0.6f);
            fx.ChromaPulse(1f);
            fx.Flash(0.35f);
            fx.Burst(at, Palette.Red, 18, 7f, 0.22f);
            Audio.Play("hit", 0.95f);
            if (Profile.Data.vibration) Vibrate();
            hud.SetPips(pips, maxPips);
            if (pips <= 0) Die();
            else if (pips == 1) hud.Toast("LAST CHANCE", Palette.Red, 1.2f);
        }

        private static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void OnFall()
        {
            Audio.Play("fall", 0.8f);
            Rescue();
            if (tutorialRun)
            {
                hud.Toast("CAREFUL!", Palette.Amber);
                return;
            }
            if (runner.Shielded)
            {
                runner.Shielded = false;
                shieldTime = 0f;
                Audio.Play("shield_break", 0.8f);
                return;
            }
            pips--;
            comboCount = 0;
            mult = 1;
            cleanDistance = 0f;
            fx.Shake(0.4f);
            fx.ChromaPulse(0.8f);
            hud.SetPips(pips, maxPips);
            if (pips <= 0) Die();
            else hud.Toast("GRAPPLE SAVE", Palette.Magenta);
        }

        /// <summary>Ninja-teleport the runner onto the next rooftop and clear the landing zone.</summary>
        private void Rescue()
        {
            var next = world.NextRoofAfter(runner.X - 0.5f) ?? world.RoofUnder(runner.X);
            if (next != null && next.x0 > runner.X - 0.5f)
            {
                var shift = next.x0 - runner.X + 2.5f;
                world.Shift(shift);
            }
            var roof = world.RoofUnder(runner.X);
            var top = roof != null ? roof.top : StartTop;
            world.ClearHazards(runner.X - 3f, runner.X + 12f);
            runner.Teleport(top);
            runner.Invulnerable = 2f;
            fx.Puff(new Vector3(runner.X, top + World.LaneY(runner.Lane) + 0.6f), new Color(0.8f, 0.7f, 1f, 0.7f), 12);
            fx.Ring(new Vector3(runner.X, top + World.LaneY(runner.Lane) + 0.8f), runner.TrailColor, 4f, 0.4f);
            Audio.Play("whoosh", 0.8f);
        }

        private void AddCombo(int amount)
        {
            comboCount += amount;
            var newMult = Mathf.Min(5, 1 + comboCount / 5);
            if (newMult > mult)
            {
                mult = newMult;
                bestMult = Mathf.Max(bestMult, mult);
                Audio.Play("combo", 0.8f, 0.9f + mult * 0.1f);
                fx.Popup(new Vector3(runner.X + 1f, RunnerY + 2.6f), "x" + mult + "!", Palette.Magenta, 8f, 1f);
                if (mult == 5) hud.Toast("MAX MULTIPLIER", Palette.Amber);
            }
        }

        // ======================================================= tutorial ==
        private void CheckPrompts()
        {
            if (!tutorialRun || activePrompt != Prompt.None) return;
            foreach (var e in world.Ents)
            {
                if (e.prompt == Prompt.None || e.promptShown) continue;
                var ahead = e.x - runner.X;
                // Already destroyed or passed (e.g. the player acted early): no prompt needed.
                if (e.dead || (e.kind != EntKind.Marker && ahead < -e.w * 0.5f))
                {
                    e.promptShown = true;
                    continue;
                }
                // The player already did the right thing on their own: skip the prompt.
                if (e.prompt == Prompt.Jump && e.kind != EntKind.Marker && !runner.Grounded && ahead < 4f) { e.promptShown = true; continue; }
                if (e.prompt == Prompt.Lane && runner.Lane == e.openLane) { e.promptShown = true; continue; }
                if (e.prompt == Prompt.Dash && runner.IsDashing) { e.promptShown = true; continue; }
                var trigger = e.prompt switch
                {
                    Prompt.DoubleJump => !runner.Grounded && runner.JumpsUsed == 1 && runner.VelocityY < 3f && ahead < 3f,
                    Prompt.Done => ahead < 0f,
                    _ => e.kind == EntKind.Marker ? ahead < 0.4f : ahead < 3.0f + e.w * 0.5f
                };
                if (!trigger) continue;
                e.promptShown = true;
                if (e.prompt == Prompt.Done)
                {
                    Profile.Data.tutorialDone = true;
                    Profile.Save();
                    hud.Toast("TRAINING COMPLETE", Palette.Mint, 1.6f);
                    Audio.Play("mission");
                    tutorialRun = false;
                    distance = 0f;
                    scoreF = 0f;
                    return;
                }
                // Skip the jump prompt for a gap if the player is already airborne.
                if (e.prompt == Prompt.Jump && e.kind == EntKind.Marker && !runner.Grounded) continue;
                activePrompt = e.prompt;
                promptLane = e.openLane;
                tutorial.Present(e.prompt, runner.Lane > promptLane);
                Audio.Play("tick", 0.8f, 0.8f);
                return;
            }
        }

        // ========================================================== death ==
        private void Die()
        {
            state = State.Dying;
            dyingTimer = 0f;
            input.Enabled = false;
            input.Clear();
            activePrompt = Prompt.None;
            tutorial.Hide();
            runner.PlayCapture();
            runner.Shielded = false;
            Audio.Play("capture", 0.9f);
            Audio.Duck(0.6f);
            fx.Flash(0.5f);
            hud.SetHintsVisible(false);
        }

        private void TickDying(float udt)
        {
            dyingTimer += udt;
            var dt = udt * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(dyingTimer / 1.2f));
            speed = Mathf.MoveTowards(speed, 0f, udt * 18f);
            world.Advance(speed * dt, dt);
            runner.Animate(dt);
            fx.Tick(dt, udt);
            UpdateHud();
            if (dyingTimer < 1.9f) return;

            var cost = ContinueCost;
            if (Profile.Data.credits + runCredits >= cost && continuesUsed < 3)
            {
                state = State.Continue;
                continueTimer = 5f;
                continueScreen.Setup(cost, Profile.Data.credits + runCredits);
                continueScreen.Show();
                Audio.SetMuffled(true);
            }
            else ShowResults();
        }

        private int ContinueCost => 150 << continuesUsed; // 150, 300, 600

        private void TickContinue(float udt)
        {
            continueTimer -= udt;
            continueScreen.SetTimer(Mathf.Clamp01(continueTimer / 5f));
            fx.Tick(udt, udt);
            if (continueTimer <= 0f) DeclineContinue();
        }

        public void AcceptContinue()
        {
            if (state != State.Continue) return;
            var cost = ContinueCost;
            // Pay from this run's credits first, then the bank.
            var fromRun = Mathf.Min(runCredits, cost);
            runCredits -= fromRun;
            if (cost - fromRun > 0 && !Profile.TrySpend(cost - fromRun)) { runCredits += fromRun; return; }
            continuesUsed++;
            continueScreen.Hide();
            pips = maxPips;
            hud.SetPips(pips, maxPips);
            runner.ResetTo(runner.FeetY);
            runner.DashCooldown = Upgrades.DashCooldown(Profile.Level(Upgrades.Dash));
            world.ClearHazards(runner.X - 3f, runner.X + 20f);
            runner.Invulnerable = 2.5f;
            fx.Ring(new Vector3(runner.X, RunnerY + 0.8f), Palette.Mint, 6f, 0.5f);
            fx.Burst(new Vector3(runner.X, RunnerY + 0.8f), Palette.Mint, 24, 9f);
            hud.Toast("BACK ON THE ROOFS", Palette.Mint);
            Audio.SetMuffled(false);
            timeScale = 0.2f;
            input.Clear();
            input.Enabled = true;
            state = State.Running;
        }

        public void DeclineContinue()
        {
            if (state != State.Continue) return;
            continueScreen.Hide();
            ShowResults();
        }

        // ======================================================== results ==
        private void ShowResults()
        {
            state = State.Results;
            hud.Hide();
            Audio.SetMuffled(true);
            var data = Profile.Data;
            var finalScore = Mathf.RoundToInt(scoreF);
            var isBest = finalScore > data.bestScore && finalScore > 0;
            data.bestScore = Mathf.Max(data.bestScore, finalScore);
            data.bestDistance = Mathf.Max(data.bestDistance, distance);
            data.totalRuns++;
            data.lifetimeDistance += distance;
            data.lifetimeSlices += slices;
            data.lifetimeCredits += runCredits;

            var justCompleted = new bool[3];
            var missionRewards = 0;
            for (var i = 0; i < 3; i++)
            {
                var m = data.missions[i];
                if (m.complete) continue;
                m.progress = LiveMissionProgress(m, true);
                if (m.progress >= m.target)
                {
                    m.complete = true;
                    justCompleted[i] = true;
                    data.stars++;
                    data.missionsCompleted++;
                    missionRewards += m.reward;
                }
            }
            var earned = runCredits;
            data.credits += earned + missionRewards;
            runCredits = 0;
            Profile.Save();

            results.Present(finalScore, isBest, earned + missionRewards, data.missions, justCompleted);
        }

        private void BankRunCredits()
        {
            if (runCredits <= 0) return;
            Profile.AddCredits(runCredits);
            runCredits = 0;
        }

        /// <summary>Mission progress including the current run (used by pause + results).</summary>
        public int LiveMissionProgress(MissionSlot m) => LiveMissionProgress(m, false);

        private int LiveMissionProgress(MissionSlot m, bool runEnded)
        {
            var kind = Missions.Kind(m);
            var value = kind switch
            {
                MissionKind.RunDistance => Mathf.FloorToInt(distance),
                MissionKind.CollectCredits => runCredits,
                MissionKind.Slices => slices,
                MissionKind.LaneSwitches => laneSwitches,
                MissionKind.DoubleJumps => doubleJumps,
                MissionKind.ReachMultiplier => bestMult,
                MissionKind.PowerUps => powerUps,
                MissionKind.TotalDistance => Mathf.FloorToInt(distance),
                MissionKind.CleanDistance => Mathf.FloorToInt(bestClean),
                MissionKind.PlayRuns => runEnded ? 1 : 0,
                MissionKind.CloseCalls => closeCalls,
                _ => 0
            };
            return Missions.PerRun(m) ? Mathf.Max(m.progress, value) : m.progress + value;
        }

        private void AnnounceMissions()
        {
            if (tutorialRun) return;
            var ms = Profile.Data.missions;
            for (var i = 0; i < 3; i++)
            {
                if (announced[i] || ms[i].complete) continue;
                if (Missions.Kind(ms[i]) == MissionKind.PlayRuns) continue;
                if (LiveMissionProgress(ms[i]) < ms[i].target) continue;
                announced[i] = true;
                hud.Toast("CONTRACT COMPLETE", Palette.Mint, 1.4f);
                Audio.Play("mission", 0.9f);
            }
        }

        // ============================================================ HUD ==
        private void UpdateHud()
        {
            hud.SetScore(Mathf.RoundToInt(scoreF), distance);
            hud.SetPips(Mathf.Max(0, pips), maxPips);
            hud.SetMultiplier(mult, mult >= 5 ? 1f : (comboCount % 5) / 5f);
            hud.SetCredits(runCredits);
            hud.SetDash(runner.Dash01);
            var slot = 0;
            if (shieldTime > 0f) hud.SetPower(slot++, true, PowerKind.Shield, shieldTime / 14f);
            if (magnetTime > 0f) hud.SetPower(slot++, true, PowerKind.Magnet, magnetTime / Upgrades.MagnetTime(Profile.Level(Upgrades.Magnet)));
            if (overclockTime > 0f) hud.SetPower(slot++, true, PowerKind.Overclock, overclockTime / Upgrades.OverclockTime(Profile.Level(Upgrades.Overclock)));
            for (; slot < 3; slot++) hud.SetPower(slot, false, PowerKind.Shield, 0f);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && state == State.Running) Pause();
        }
    }
}
