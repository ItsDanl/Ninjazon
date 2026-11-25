using System;
using System.Collections.Generic;
using Ninjazon.Runtime.App;
using Ninjazon.Runtime.Content;
using Ninjazon.Runtime.UI;
using Ninjazon.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class GameplayController : MonoBehaviour
    {
        private enum EntityKind { Barricade, Laser, Blocked, Scanner, Chip, Credit, Jammer }

        private sealed class Entity
        {
            public GameObject view;
            public EntityKind kind;
            public int route;
            public bool resolved;
            public Vector3 baseScale;
            public Color baseColor;
            public SpriteRenderer aura;
            public float pulseOffset;
        }

        private readonly List<Entity> entities = new();
        private readonly Queue<GameObject> entityPool = new();
        private readonly List<RaycastResult> uiRaycastResults = new();
        private readonly PlayerMotionModel motion = new();
        private readonly RunState state = new();

        [SerializeField] private GameplayView view;
        [SerializeField] private UIRouter router;
        [SerializeField] private RunnerPresentation presentation;
        [SerializeField] private GameplayEnvironmentController environment;
        [SerializeField] private Transform world;
        [SerializeField] private Transform player;
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Transform entitySpawnAnchor;
        [SerializeField] private Transform[] laneAnchors;
        [SerializeField] private Transform scarf;
        [SerializeField] private Transform entityPoolRoot;
        [SerializeField] private DifficultyProfile difficultyProfile;
        [SerializeField] private Texture2D gameplayAtlas;
        [SerializeField] private RunnerArtController runnerArt;
        [Header("Encounter Patterns")]
        [SerializeField, Range(0f, 1f)] private float fullLaneActionHazardChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float twoLaneBlockerChance = 0.6f;
        [Header("Spawn Rhythm")]
        [SerializeField] private float minimumSpawnInterval = 0.48f;
        [SerializeField] private float spawnIntervalMinimumMultiplier = 0.72f;
        [SerializeField] private float spawnIntervalMaximumMultiplier = 1.32f;
        [SerializeField, Range(0f, 1f)] private float extendedGapChance = 0.14f;
        [Header("Mobile Controls")]
        [SerializeField, Min(20f)] private float swipeThresholdPixels = 90f;

        private readonly Sprite[] gameplaySprites = new Sprite[7];

        private NinjazonApp app;
        private TutorialSequence tutorial;
        private Action pendingConfirmation;
        private float spawnTimer = 1.2f;
        private float hitGrace;
        private float targetRouteY;
        private Vector2 pointerStart;
        private bool pointerTracking;
        private bool forceJammer;
        private bool initialized;
        private float currentSpeed = 6f;
        private float runnerHitTimer;

        public bool ValidateReferences(out string error)
        {
            if (view == null) { error = "Gameplay controller requires a serialized view."; return false; }
            if (router == null) { error = "Gameplay controller requires a serialized router."; return false; }
            if (presentation == null) { error = "Gameplay controller requires a serialized presentation."; return false; }
            if (environment == null) { error = "Gameplay controller requires a serialized environment controller."; return false; }
            if (world == null) { error = "Gameplay controller requires a serialized world root."; return false; }
            if (player == null) { error = "Gameplay controller requires a serialized player."; return false; }
            if (playerAnchor == null) { error = "Gameplay controller requires a serialized player anchor."; return false; }
            if (entitySpawnAnchor == null) { error = "Gameplay controller requires a serialized entity spawn anchor."; return false; }
            if (laneAnchors == null || laneAnchors.Length != 3) { error = "Gameplay controller requires exactly three serialized lane anchors."; return false; }
            for (var i = 0; i < laneAnchors.Length; i++)
            {
                if (laneAnchors[i] == null) { error = $"Gameplay controller lane anchor {i} is required."; return false; }
            }
            if (scarf == null) { error = "Gameplay controller requires a serialized scarf."; return false; }
            if (entityPoolRoot == null) { error = "Gameplay controller requires a serialized entity pool root."; return false; }
            if (difficultyProfile == null) { error = "Gameplay controller requires a serialized difficulty profile."; return false; }
            if (gameplayAtlas == null) { error = "Gameplay controller requires a serialized gameplay-art atlas."; return false; }
            if (runnerArt == null) { error = "Gameplay controller requires a serialized runner art controller."; return false; }
            if (!runnerArt.ValidateReferences(out error)) return false;
            if (!view.ValidateReferences(out error)) return false;
            if (!presentation.ValidateReferences(out error)) return false;
            if (!environment.ValidateReferences(out error)) return false;
            error = null;
            return true;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (!ValidateReferences(out var error))
            {
                Debug.LogError(error, this);
                return;
            }

            initialized = true;
            app = NinjazonApp.Ensure();
            Time.timeScale = 1f;
            app.Audio.SetPaused(false);
            BuildGameplaySprites();
            runnerArt.Initialize();
            presentation.Initialize();
            router.Configure(view.ScreenBindings, view.ModalBindings);
            BindControls();
            RefreshSettings();
            router.PushScreen("HUD");
            state.Changed += RefreshHud;
            player.position = new Vector3(PlayerX, LaneY(motion.Route), player.position.z);
            targetRouteY = LaneY(motion.Route);
            RefreshHud();

            if (!app.Data.tutorialComplete)
            {
                tutorial = new TutorialSequence();
                state.SetPhase(RunPhase.Tutorial);
                Time.timeScale = 0f;
                router.ShowModal("Tutorial");
                RefreshTutorial();
            }
        }

        private void BindControls()
        {
            view.PauseButton.onClick.AddListener(Pause);
            view.JumpButton.onClick.AddListener(Jump);
            view.SlideButton.onClick.AddListener(Slide);
            view.ResumeButton.onClick.AddListener(Resume);
            view.RestartButton.onClick.AddListener(() => Confirm("Restart this run?", SceneFlow.Gameplay));
            view.PauseOptionsButton.onClick.AddListener(() => router.PushScreen("Options"));
            view.MainMenuButton.onClick.AddListener(() => Confirm("Abandon this run?", GoMainMenu));
            view.OptionsBackButton.onClick.AddListener(() => router.PopScreen());
            view.ResetTutorialButton.onClick.AddListener(app.ResetTutorial);
            view.RetryButton.onClick.AddListener(SceneFlow.Gameplay);
            view.ResultsMainMenuButton.onClick.AddListener(GoMainMenu);
            view.ConfirmButton.onClick.AddListener(ApplyConfirmation);
            view.CancelButton.onClick.AddListener(() => router.CloseModal());
            view.MusicSlider.onValueChanged.AddListener(SetMusicVolume);
            view.SfxSlider.onValueChanged.AddListener(SetSfxVolume);
            view.VibrationToggle.onValueChanged.AddListener(SetVibration);
            view.ScreenShakeToggle.onValueChanged.AddListener(SetScreenShake);
            view.ControlHintsToggle.onValueChanged.AddListener(SetControlHints);
        }

        private void RefreshSettings()
        {
            view.MusicSlider.SetValueWithoutNotify(app.Data.musicVolume);
            view.SfxSlider.SetValueWithoutNotify(app.Data.sfxVolume);
            view.VibrationToggle.SetIsOnWithoutNotify(app.Data.vibration);
            view.ScreenShakeToggle.SetIsOnWithoutNotify(app.Data.screenShake);
            view.ControlHintsToggle.SetIsOnWithoutNotify(app.Data.controlHints);
        }

        private void ApplyConfirmation()
        {
            var action = pendingConfirmation;
            router.CloseModal();
            action?.Invoke();
        }

        private void SetMusicVolume(float value) { app.Data.musicVolume = value; app.Commit(); }
        private void SetSfxVolume(float value) { app.Data.sfxVolume = value; app.Commit(); }
        private void SetVibration(bool value) { app.Data.vibration = value; app.Commit(); }
        private void SetScreenShake(bool value) { app.Data.screenShake = value; app.Commit(); }
        private void SetControlHints(bool value) { app.Data.controlHints = value; app.Commit(); }

        private void Update()
        {
            HandleInput();
            if (state.Phase == RunPhase.Running)
            {
                var delta = Time.deltaTime;
                motion.Tick(delta);
                hitGrace = Mathf.Max(0f, hitGrace - delta);
                runnerHitTimer = Mathf.Max(0f, runnerHitTimer - delta);
                currentSpeed = difficultyProfile.EvaluateSpeed(state.Distance, state.Alert);
                state.Advance(currentSpeed * delta, 1.2f);
                spawnTimer -= delta;
                if (spawnTimer <= 0f)
                {
                    SpawnNext();
                    var baseline = difficultyProfile.EvaluateSpawnInterval(state.Distance, state.Alert);
                    spawnTimer = EncounterPlanner.CalculateSpawnDelay(
                        baseline,
                        Random.value,
                        Random.value,
                        Random.value,
                        spawnIntervalMinimumMultiplier,
                        spawnIntervalMaximumMultiplier,
                        extendedGapChance,
                        minimumSpawnInterval);
                }
                TickEntities(currentSpeed, delta);
            }
            environment.Advance(currentSpeed, state.Alert, state.Phase == RunPhase.Running, Time.unscaledDeltaTime);
            presentation.Tick(Time.unscaledDeltaTime);
            presentation.SetAlert(state.Alert, state.IsCaptured);
            UpdatePlayer(Time.unscaledDeltaTime);
            var runnerState = state.IsCaptured ? RunnerVisualState.Capture
                : runnerHitTimer > 0f ? RunnerVisualState.Hit
                : motion.IsSliding ? RunnerVisualState.Slide
                : motion.IsJumping ? RunnerVisualState.Jump
                : state.Phase == RunPhase.Running ? RunnerVisualState.Run
                : RunnerVisualState.Idle;
            runnerArt.SetState(runnerState);
            runnerArt.Tick(Time.unscaledDeltaTime);
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) HandleBack();
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) MoveRoute(-1);
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) MoveRoute(1);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) Jump();
                if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) Slide();
            }

            var pointer = Pointer.current;
            if (pointer == null) return;
            if (pointer.press.wasPressedThisFrame)
            {
                pointerStart = pointer.position.ReadValue();
                pointerTracking = !StartedOnInteractiveUi(pointerStart);
            }
            if (pointerTracking && pointer.press.wasReleasedThisFrame)
            {
                var delta = pointer.position.ReadValue() - pointerStart;
                pointerTracking = false;
                switch (SwipeGesture.Classify(delta, swipeThresholdPixels))
                {
                    case SwipeAction.LaneUp: MoveRoute(1); break;
                    case SwipeAction.LaneDown: MoveRoute(-1); break;
                    case SwipeAction.Jump: Jump(); break;
                    case SwipeAction.Slide: Slide(); break;
                }
            }
        }

        private bool StartedOnInteractiveUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiRaycastResults);
            for (var i = 0; i < uiRaycastResults.Count; i++)
                if (uiRaycastResults[i].gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }

        public void MoveRoute(int direction)
        {
            if (state.IsCaptured || router.CurrentScreen != "HUD") return;
            if (motion.MoveRoute(direction))
            {
                targetRouteY = LaneY(motion.Route);
                AcceptTutorial("Switch");
                app.Audio.PlayUi();
                presentation.Burst(player.position + Vector3.left * 0.35f, UITheme.Cyan, false, false);
            }
        }

        public void Jump()
        {
            if (state.IsCaptured || router.CurrentScreen != "HUD") return;
            if (motion.Jump())
            {
                AcceptTutorial("Jump");
                app.Audio.PlayJump();
                presentation.Burst(player.position + Vector3.down * 0.55f, UITheme.Cyan, false, false);
            }
        }

        public void Slide()
        {
            if (state.IsCaptured || router.CurrentScreen != "HUD") return;
            if (motion.Slide())
            {
                AcceptTutorial("Slide");
                app.Audio.PlayUi();
                presentation.Burst(player.position + Vector3.left * 0.45f, UITheme.Magenta, false, false);
            }
        }

        private void AcceptTutorial(string action)
        {
            if (tutorial == null || !tutorial.Accept(action)) return;
            if (tutorial.CurrentAction == "Chip")
            {
                router.CloseModal();
                state.SetPhase(RunPhase.Running);
                Time.timeScale = 1f;
                SpawnEntity(EntityKind.Chip, motion.Route);
            }
            else if (tutorial.IsComplete)
            {
                app.Data.tutorialComplete = true;
                app.Commit();
            }
            else
            {
                RefreshTutorial();
            }
        }

        private void RefreshTutorial()
        {
            view.TutorialText.text = tutorial?.CurrentAction switch
            {
                "Switch" => "SWIPE LEFT TO MOVE UP\nRIGHT TO MOVE DOWN",
                "Jump" => "SWIPE UP OR TAP JUMP\nto clear barricades",
                "Slide" => "SWIPE DOWN OR TAP SLIDE\nto pass beneath lasers",
                "Chip" => "COLLECT THE DATA CHIP",
                "Jammer" => "COLLECT A JAMMER\nto reduce Alert",
                _ => "TRAINING COMPLETE"
            };
        }

        private void UpdatePlayer(float delta)
        {
            var position = player.position;
            position.x = PlayerX;
            position.y = Mathf.Lerp(position.y, targetRouteY + motion.JumpHeight, 1f - Mathf.Exp(-14f * delta));
            player.position = position;
            var routeDelta = targetRouteY - position.y;
            // The authored sprite animation already contains the slide and jump poses.
            // Scaling the parent here deformed every child (including the pixel art),
            // which made sliding look like the runner was being physically squashed.
            player.localScale = Vector3.Lerp(player.localScale, Vector3.one, 1f - Mathf.Exp(-18f * delta));
            player.localRotation = Quaternion.Lerp(player.localRotation, Quaternion.Euler(0f, 0f, Mathf.Clamp(-routeDelta * 4.5f, -12f, 12f)), 1f - Mathf.Exp(-16f * delta));
            var scarfWave = Mathf.Sin(Time.unscaledTime * (8f + currentSpeed * 0.35f));
            scarf.localPosition = Vector3.Lerp(scarf.localPosition, new Vector3(-0.7f, 0.34f + scarfWave * 0.07f, 0.1f), 1f - Mathf.Exp(-14f * delta));
            scarf.localScale = Vector3.Lerp(scarf.localScale, new Vector3(0.78f + currentSpeed * 0.025f, 0.13f + Mathf.Abs(scarfWave) * 0.035f, 1f), 1f - Mathf.Exp(-12f * delta));
        }

        private void SpawnNext()
        {
            if (forceJammer)
            {
                forceJammer = false;
                SpawnEntity(EntityKind.Jammer, motion.Route);
                return;
            }
            var roll = Random.value;
            if (roll < 0.2f) { SpawnEntity(EntityKind.Chip, Random.Range(0, 3)); return; }
            if (roll < 0.3f) { SpawnEntity(EntityKind.Credit, Random.Range(0, 3)); return; }
            if (roll < 0.36f) { SpawnEntity(EntityKind.Jammer, Random.Range(0, 3)); return; }

            var kind = (EntityKind)Random.Range(0, 4);
            var primaryLane = Random.Range(0, 3);
            var laneMask = EncounterPlanner.BuildLaneMask(
                kind.ToString(),
                primaryLane,
                Random.value,
                Random.value,
                fullLaneActionHazardChance,
                twoLaneBlockerChance);
            for (var lane = 0; lane < 3; lane++)
            {
                if ((laneMask & (1 << lane)) != 0) SpawnEntity(kind, lane);
            }
        }

        private void SpawnEntity(EntityKind kind, int route)
        {
            var view = entityPool.Count > 0 ? entityPool.Dequeue() : CreateSprite("PooledEntity", Color.white);
            view.SetActive(true);
            view.name = kind + "Entity";
            view.transform.SetParent(entityPoolRoot, false);
            view.transform.rotation = Quaternion.identity;
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = GetGameplaySprite(kind);
            var desiredSize = Vector2.one;
            var color = UITheme.Magenta;
            var yOffset = 0f;
            switch (kind)
            {
                case EntityKind.Barricade: desiredSize = new Vector2(1.15f, 1.25f); color = UITheme.Amber; break;
                case EntityKind.Laser: desiredSize = new Vector2(1.65f, 2.2f); color = UITheme.Magenta; break;
                case EntityKind.Blocked: desiredSize = new Vector2(1.5f, 2.05f); color = new Color(0.9f, 0.22f, 0.32f); break;
                case EntityKind.Scanner: desiredSize = new Vector2(1.35f, 2.6f); color = new Color(1f, 0.08f, 0.62f); break;
                case EntityKind.Chip: desiredSize = new Vector2(0.78f, 0.78f); color = UITheme.Cyan; break;
                case EntityKind.Credit: desiredSize = new Vector2(0.72f, 0.72f); color = UITheme.Amber; break;
                case EntityKind.Jammer: desiredSize = new Vector2(0.82f, 0.92f); color = new Color(0.1f, 1f, 0.48f); break;
            }
            renderer.color = Color.white;
            var spriteSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.one;
            var scale = new Vector3(desiredSize.x / Mathf.Max(0.01f, spriteSize.x), desiredSize.y / Mathf.Max(0.01f, spriteSize.y), 1f);
            view.transform.localScale = scale;
            view.transform.position = new Vector3(entitySpawnAnchor.position.x, LaneY(route) + yOffset, 0f);
            var aura = presentation.ConfigureAura(view.transform, color, scale);
            entities.Add(new Entity { view = view, kind = kind, route = route, baseScale = scale, baseColor = color, aura = aura, pulseOffset = Random.value * 6f });
        }

        private Sprite GetGameplaySprite(EntityKind kind)
        {
            var index = (int)kind;
            if (gameplaySprites[index] == null) BuildGameplaySprites();
            return gameplaySprites[index];
        }

        private void BuildGameplaySprites()
        {
            if (gameplayAtlas == null || gameplaySprites[0] != null) return;
            var textureWidth = gameplayAtlas.width;
            var textureHeight = gameplayAtlas.height;
            var definitions = new[]
            {
                new Rect(0f, 210f, 315f, 280f),
                new Rect(330f, 210f, 330f, 330f),
                new Rect(670f, 210f, 380f, 280f),
                new Rect(1050f, 200f, 360f, 420f),
                new Rect(1390f, 210f, 220f, 280f),
                new Rect(1600f, 220f, 230f, 250f),
                new Rect(1840f, 210f, 200f, 390f)
            };

            for (var i = 0; i < definitions.Length; i++)
            {
                var source = definitions[i];
                source.x = Mathf.Clamp(source.x, 0f, textureWidth - 1f);
                source.y = Mathf.Clamp(source.y, 0f, textureHeight - 1f);
                source.width = Mathf.Min(source.width, textureWidth - source.x);
                source.height = Mathf.Min(source.height, textureHeight - source.y);
                gameplaySprites[i] = Sprite.Create(gameplayAtlas, source, new Vector2(0.5f, 0f), 32f, 0, SpriteMeshType.FullRect);
                gameplaySprites[i].name = ((EntityKind)i).ToString();
            }
        }

        private void OnDestroy()
        {
            for (var i = 0; i < gameplaySprites.Length; i++)
            {
                if (gameplaySprites[i] != null) Destroy(gameplaySprites[i]);
            }
        }

        private void TickEntities(float speed, float delta)
        {
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];
                // World-mounted props and pickups track the rooftop exactly. The
                // scanner drone has a small closing-speed advantage so it feels
                // airborne without visibly detaching from the runner's world speed.
                var movementMultiplier = entity.kind == EntityKind.Scanner ? 1.08f : 1f;
                entity.view.transform.position += Vector3.left * speed * movementMultiplier * delta;
                var x = entity.view.transform.position.x;
                var pulse = 0.5f + Mathf.Sin(Time.time * 7f + entity.pulseOffset) * 0.5f;
                if (entity.kind == EntityKind.Chip || entity.kind == EntityKind.Credit || entity.kind == EntityKind.Jammer)
                {
                    entity.view.transform.Rotate(0f, 0f, 110f * delta);
                    entity.view.transform.localScale = entity.baseScale * Mathf.Lerp(0.92f, 1.12f, pulse);
                }
                var auraColor = entity.aura.color;
                var approach = Mathf.InverseLerp(presentation.CameraHalfWidth, PlayerX, x);
                auraColor.a = Mathf.Lerp(0.1f, 0.38f, Mathf.Clamp01(approach) * Mathf.Lerp(0.55f, 1f, pulse));
                entity.aura.color = auraColor;
                if (!entity.resolved && x <= PlayerX + 0.25f)
                {
                    ResolveEntity(entity);
                    entity.resolved = true;
                }
                if (x < -presentation.CameraHalfWidth - 1.5f)
                {
                    entity.view.SetActive(false);
                    entity.view.transform.rotation = Quaternion.identity;
                    entityPool.Enqueue(entity.view);
                    entities.RemoveAt(i);
                }
            }
        }

        private float PlayerX => playerAnchor.position.x;

        private float LaneY(int route)
        {
            return laneAnchors[Mathf.Clamp(route, 0, laneAnchors.Length - 1)].position.y;
        }

        private void ResolveEntity(Entity entity)
        {
            if (entity.route != motion.Route) return;
            switch (entity.kind)
            {
                case EntityKind.Chip:
                    state.AddScore(75);
                    app.Audio.PlayPickup();
                    HapticsService.Pulse(app.Data.vibration, false);
                    presentation.Burst(entity.view.transform.position, UITheme.Cyan, false, false);
                    AcceptTutorial("Chip");
                    if (tutorial?.CurrentAction == "Jammer") forceJammer = true;
                    entity.view.SetActive(false);
                    break;
                case EntityKind.Credit:
                    state.AddCredits(1);
                    app.Audio.PlayPickup();
                    HapticsService.Pulse(app.Data.vibration, false);
                    presentation.Burst(entity.view.transform.position, UITheme.Amber, false, false);
                    entity.view.SetActive(false);
                    break;
                case EntityKind.Jammer:
                    state.ReduceAlert(25f);
                    app.Audio.PlayPickup();
                    HapticsService.Pulse(app.Data.vibration, false);
                    presentation.Burst(entity.view.transform.position, new Color(0.1f, 1f, 0.48f), false, false);
                    AcceptTutorial("Jammer");
                    entity.view.SetActive(false);
                    break;
                default:
                    var hit = CollisionRules.ShouldHit(entity.kind.ToString(), entity.route, motion.Route, motion.IsJumping, motion.IsSliding);
                    if (hit && hitGrace <= 0f)
                    {
                        state.AddAlert(entity.kind == EntityKind.Scanner ? 18f : 24f);
                        hitGrace = 1.05f;
                        runnerHitTimer = 0.42f;
                        app.Audio.PlayHit();
                        HapticsService.Pulse(app.Data.vibration, true);
                        presentation.Burst(player.position, new Color(1f, 0.06f, 0.22f), true, app.Data.screenShake);
                        if (state.IsCaptured) EndRun();
                    }
                    else if (!hit)
                    {
                        state.AddScore(25);
                    }
                    break;
            }
        }

        private void Pause()
        {
            if (state.IsCaptured || router.CurrentScreen != "HUD" || router.CurrentModal == "Tutorial") return;
            state.SetPhase(RunPhase.Paused);
            Time.timeScale = 0f;
            app.Audio.SetPaused(true);
            router.PushScreen("Pause");
        }

        private void Resume()
        {
            if (router.CurrentScreen == "Pause") router.PopScreen();
            state.SetPhase(RunPhase.Running);
            Time.timeScale = 1f;
            app.Audio.SetPaused(false);
        }

        private void HandleBack()
        {
            if (router.CurrentModal == "Tutorial") return;
            if (router.CurrentModal != null) { router.CloseModal(); return; }
            if (router.CurrentScreen == "Options") { router.PopScreen(); return; }
            if (router.CurrentScreen == "Pause") { Resume(); return; }
            if (router.CurrentScreen == "Results") { Confirm("Return to main menu?", GoMainMenu); return; }
            Pause();
        }

        private void Confirm(string prompt, Action action)
        {
            pendingConfirmation = action;
            view.ConfirmText.text = prompt;
            router.ShowModal("Confirm");
        }

        private void EndRun()
        {
            state.SetPhase(RunPhase.Captured);
            Time.timeScale = 0f;
            app.Audio.SetPaused(true);
            app.RecordRun(state.Score, state.Distance, state.Credits);
            view.ResultsText.text = $"SCORE  {state.Score:N0}\nDISTANCE  {state.Distance:0} m\nCREDITS  +{state.Credits}\n\nBEST  {app.Data.bestScore:N0}";
            router.PushScreen("Results");
        }

        private void GoMainMenu()
        {
            Time.timeScale = 1f;
            app.Audio.SetPaused(false);
            SceneFlow.MainMenu();
        }

        private void RefreshHud()
        {
            view.ScoreText.text = $"SCORE {state.Score:N0}\n{state.Distance:0} m";
            view.CreditsText.text = $"CREDITS {state.Credits}";
            var speed = difficultyProfile.EvaluateSpeed(state.Distance, state.Alert);
            view.SpeedText.text = $"SPEED {speed:0.0}\nBAND {DifficultyModel.DifficultyBand(state.Distance, state.Alert) + 1}";
            var normalized = state.Alert / 100f;
            view.AlertFill.anchorMax = new Vector2(normalized, 1f);
            view.AlertText.text = $"ALERT {state.Alert:0}%";
            app.Audio.SetAlert(state.Alert);
        }

        private static Sprite sharedSprite;

        private static GameObject CreateSprite(string name, Color color)
        {
            if (sharedSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "NinjazonPixel", filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            var go = new GameObject(name, typeof(SpriteRenderer));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = color;
            return go;
        }
    }

    public enum SwipeAction { None, LaneUp, LaneDown, Jump, Slide }

    public static class SwipeGesture
    {
        public static SwipeAction Classify(Vector2 delta, float thresholdPixels)
        {
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < Mathf.Max(0f, thresholdPixels))
                return SwipeAction.None;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x < 0f ? SwipeAction.LaneUp : SwipeAction.LaneDown;
            return delta.y > 0f ? SwipeAction.Jump : SwipeAction.Slide;
        }
    }
}
