using System;
using System.IO;
using System.Linq;
using Ninjazon.Runtime.App;
using Ninjazon.Runtime.Content;
using Ninjazon.Runtime.Gameplay;
using Ninjazon.Runtime.Presentation;
using Ninjazon.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ninjazon.Editor
{
    public static class NinjazonProjectBuilder
    {
        private const string SceneFolder = "Assets/Ninjazon/Scenes";
        private const string ContentFolder = "Assets/Ninjazon/Content";
        private const string PresentationSpritePath = ContentFolder + "/NinjazonPixel.asset";
        private static readonly float[] RouteY = { -4.2f, -1.2f, 1.8f };

        [MenuItem("Ninjazon/Build Prototype Scenes")]
        public static void Build()
        {
            ImportTextMeshProEssentials();
            NinjazonPixelImporter.EnsureStructure();
            Directory.CreateDirectory(SceneFolder);
            Directory.CreateDirectory(ContentFolder);
            CreateDifficultyProfile();
            EnsurePresentationSprite();
            CreateBootstrapScene();
            CreateMainMenuScene();
            CreateGameplayScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{SceneFolder}/Bootstrap.unity", true),
                new EditorBuildSettingsScene($"{SceneFolder}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{SceneFolder}/Gameplay.unity", true)
            };
            PlayerSettings.productName = "Ninjazon";
            PlayerSettings.companyName = "Neon Courier Studio";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene($"{SceneFolder}/Bootstrap.unity", OpenSceneMode.Single);
            Debug.Log("NINJAZON_BUILD_COMPLETE: Bootstrap, MainMenu, Gameplay scenes and content defaults created.");
        }

        [MenuItem("Ninjazon/Play Prototype", priority = 1)]
        public static void PlayPrototype()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene($"{SceneFolder}/Bootstrap.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Ninjazon/Build Android Test APK")]
        public static void BuildAndroid()
        {
            var arguments = Environment.GetCommandLineArgs();
            var output = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Ninjazon-android-test.apk");
            for (var i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == "-buildOutput") output = arguments[i + 1];

            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("APK output directory is missing."));
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled Ninjazon scenes are in Build Settings.");

            var previousBundleSetting = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
                Debug.Log($"NINJAZON_ANDROID_APK: {output}");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundleSetting;
            }
        }

        private static void ImportTextMeshProEssentials()
        {
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(settingsPath) != null) return;

            var packagePath = Path.Combine(
                EditorApplication.applicationContentsPath,
                "Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning($"NINJAZON_TMP_IMPORT_SKIPPED: TMP essentials package was not found at {packagePath}");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var controller = new GameObject("BootstrapEntryPoint").AddComponent<BootstrapSceneController>();
            // Force the serialized MonoScript to the filename-matched asset. This
            // also repairs editor sessions that cached the legacy SceneEntryPoints
            // association before BootstrapSceneController was split into its own file.
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Ninjazon/Runtime/App/BootstrapSceneController.cs");
            if (script != null)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("m_Script").objectReferenceValue = script;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/Bootstrap.unity");
        }

        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("NinjazonRoot");
            CreateCamera(root.transform, 9.6f);
            CreateEventSystem(root.transform);

            var canvas = UIFactory.CreateCanvas("MainMenuCanvas");
            canvas.transform.SetParent(root.transform, false);
            var background = UIFactory.CreateImage(canvas.transform, "NeonCityBackground", UITheme.Night);
            UIFactory.Stretch(background.rectTransform);
            AddNeonBackdrop(canvas.transform);
            var safeArea = canvas.transform.Find("SafeArea");
            safeArea.SetAsLastSibling();

            var main = UIFactory.CreatePanel(safeArea, "MainScreen", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.CreateText(main.transform, "Title", "NINJAZON", 96f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(main.transform, "Tagline", "NEON COURIER // ZERO TRACE", 30f, UITheme.Magenta, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.7f), Vector2.zero, Vector2.zero);
            var record = UIFactory.CreateText(main.transform, "RecordText", "BEST  0   //   0 m\nCREDITS  0", 30f, UITheme.Text, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.65f), Vector2.zero, Vector2.zero);
            var play = UIFactory.CreateButton(main.transform, "PlayButton", "RUN", UITheme.Cyan, new Vector2(0f, 20f), new Vector2(650f, 125f), null);
            var mainOptions = UIFactory.CreateButton(main.transform, "OptionsButton", "OPTIONS", UITheme.Magenta, new Vector2(0f, -145f), new Vector2(650f, 110f), null);
            var credits = UIFactory.CreateButton(main.transform, "CreditsButton", "CREDITS", UITheme.Amber, new Vector2(0f, -295f), new Vector2(650f, 105f), null);
            var quit = UIFactory.CreateButton(main.transform, "QuitButton", "QUIT", UITheme.Muted, new Vector2(0f, -440f), new Vector2(650f, 100f), null);

            var options = UIFactory.CreatePanel(safeArea, "OptionsScreen", UITheme.Panel, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(options.transform, "Heading", "OPTIONS", 58f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0f, 0.82f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);
            var music = UIFactory.CreateSlider(options.transform, "MusicSlider", "MUSIC", 0.8f, new Vector2(0f, 250f), null);
            var sfx = UIFactory.CreateSlider(options.transform, "SfxSlider", "SFX", 0.8f, new Vector2(0f, 125f), null);
            var vibration = UIFactory.CreateToggle(options.transform, "VibrationToggle", "Vibration", true, -20f, null);
            var shake = UIFactory.CreateToggle(options.transform, "ShakeToggle", "Screen shake", true, -115f, null);
            var hints = UIFactory.CreateToggle(options.transform, "HintsToggle", "Control hints", true, -210f, null);
            var resetTutorial = UIFactory.CreateButton(options.transform, "ResetTutorialButton", "RESET TUTORIAL", UITheme.Amber,
                new Vector2(0f, -360f), new Vector2(620f, 95f), null);
            var optionsBack = UIFactory.CreateBackButton(options.transform, "OptionsBackButton", null);

            var creditsScreen = UIFactory.CreatePanel(safeArea, "CreditsScreen", UITheme.Panel, new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(creditsScreen.transform, "Heading", "CREDITS", 58f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0f, 0.78f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);
            var creditsBody = UIFactory.CreateText(creditsScreen.transform, "CreditsBody", "DESIGN + CODE\nNEON COURIER STUDIO\n\nBUILT WITH UNITY",
                34f, UITheme.Text, TextAlignmentOptions.Center, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            var creditsBack = UIFactory.CreateBackButton(creditsScreen.transform, "CreditsBackButton", null);

            var quitModal = UIFactory.CreatePanel(safeArea, "QuitConfirmation", new Color(0.02f, 0.03f, 0.08f, 0.98f),
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.65f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(quitModal.transform, "Prompt", "EXIT NINJAZON?", 42f, UITheme.Text, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero);
            var confirmQuit = UIFactory.CreateButton(quitModal.transform, "ConfirmQuitButton", "CONFIRM", UITheme.Magenta,
                new Vector2(-180f, -110f), new Vector2(300f, 95f), null);
            var cancelQuit = UIFactory.CreateButton(quitModal.transform, "CancelQuitButton", "CANCEL", UITheme.Cyan,
                new Vector2(180f, -110f), new Vector2(300f, 95f), null);

            var router = root.AddComponent<UIRouter>();
            var view = root.AddComponent<MainMenuView>();
            var controller = root.AddComponent<MainMenuController>();
            var entry = root.AddComponent<MainMenuSceneController>();

            SetReference(view, "mainScreen", main);
            SetReference(view, "optionsScreen", options);
            SetReference(view, "creditsScreen", creditsScreen);
            SetReference(view, "quitConfirmation", quitModal);
            SetReference(view, "playButton", play);
            SetReference(view, "optionsButton", mainOptions);
            SetReference(view, "creditsButton", credits);
            SetReference(view, "quitButton", quit);
            SetReference(view, "optionsBackButton", optionsBack);
            SetReference(view, "creditsBackButton", creditsBack);
            SetReference(view, "confirmQuitButton", confirmQuit);
            SetReference(view, "cancelQuitButton", cancelQuit);
            SetReference(view, "musicSlider", music);
            SetReference(view, "sfxSlider", sfx);
            SetReference(view, "vibrationToggle", vibration);
            SetReference(view, "screenShakeToggle", shake);
            SetReference(view, "controlHintsToggle", hints);
            SetReference(view, "resetTutorialButton", resetTutorial);
            SetReference(view, "recordText", record);
            SetReference(view, "creditsBodyText", creditsBody);
            SetBindings(view, "screenBindings",
                ("Main", main, play.gameObject),
                ("Options", options, music.gameObject),
                ("Credits", creditsScreen, creditsBack.gameObject));
            SetBindings(view, "modalBindings", ("Quit", quitModal, cancelQuit.gameObject));
            SetReference(controller, "view", view);
            SetReference(controller, "router", router);
            SetReference(entry, "controller", controller);

            options.gameObject.SetActive(false);
            creditsScreen.gameObject.SetActive(false);
            quitModal.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/MainMenu.unity");
        }

        private static void CreateGameplayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("NinjazonRoot");
            var runnerCamera = CreateCamera(root.transform, 9.6f);
            CreateEventSystem(root.transform);

            var world = new GameObject("RunnerWorld").transform;
            world.SetParent(root.transform, false);
            var layout = new GameObject("GameplayLayout_EDIT_ME").transform;
            var playerAnchor = CreateAnchor(layout, "PlayerAnchor", new Vector3(-2.65f, -1.2f, 0f));
            var entitySpawnAnchor = CreateAnchor(layout, "EntitySpawnAnchor", new Vector3(8.5f, 0f, 0f));
            var laneAnchors = new[]
            {
                CreateAnchor(layout, "LaneBottom", new Vector3(0f, RouteY[0], 0f)),
                CreateAnchor(layout, "LaneMiddle", new Vector3(0f, RouteY[1], 0f)),
                CreateAnchor(layout, "LaneTop", new Vector3(0f, RouteY[2], 0f))
            };
            var layoutGuide = layout.gameObject.AddComponent<GameplayLayoutGuide>();
            SetReference(layoutGuide, "playerAnchor", playerAnchor);
            SetReference(layoutGuide, "entitySpawnAnchor", entitySpawnAnchor);
            SetReferences(layoutGuide, "laneAnchors", laneAnchors);
            // Each depth is a real, independently editable sprite layer. The old
            // procedural rectangles sat over a flattened panorama and could never
            // produce convincing parallax.
            var backgroundLayer = CreatePixelLayer(world, "SkylineFar", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-far.png", 0.18f, -40);
            var farLayer = CreatePixelLayer(world, "SkylineMid", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-mid.png", 0.34f, -30);
            var nearLayer = CreatePixelLayer(world, "SkylineNear", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-near.png", 0.58f, -20);
            var pixelBackdropLayer = CreatePixelLayer(world, "RooftopForeground", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-rooftop-foreground.png", 1f, -5);

            // Retain a serialized route reference for tooling, but let the painted
            // rooftop provide the visible surface instead of neon guide bars.
            var routeLayer = CreateScrollingLayer(world, "RouteReference", Color.clear, 1f, -1.2f, 9f, -6, out _);
            var rainLayer = CreateScrollingLayer(world, "RainField", new Color(0.25f, 0.75f, 1f, 0.005f), 0.22f, 0f, 20f, 3, out var rainTiles);
            AddRain(rainTiles);
            var streakLayer = CreateScrollingLayer(world, "SpeedStreaks", new Color(0.05f, 0.95f, 1f, 0.005f), 1.65f, 0f, 20f, 4, out var streakTiles);
            AddSpeedStreaks(streakTiles);

            var vignette = CreateWorldSprite("AlertVignette", world, new Color(1f, 0.02f, 0.18f, 0f), Vector3.zero,
                new Vector3(40f, 22f, 1f), 20);
            var player = new GameObject("Player").transform;
            player.SetParent(world, false);
            player.localPosition = new Vector3(-2.65f, RouteY[1], 0f);
            var legacy = new GameObject("LegacyPlaceholders").transform;
            legacy.SetParent(player, false);
            CreateWorldSprite("RunnerBody", legacy, UITheme.Cyan, Vector3.zero, new Vector3(0.85f, 1.35f, 1f), 5);
            var scarf = CreateWorldSprite("NeonScarf", player, UITheme.Magenta, new Vector3(-0.65f, 0.35f, 0.1f),
                new Vector3(0.75f, 0.16f, 1f), 4).transform;
            scarf.SetParent(legacy, true);
            var runnerArt = CreateWorldSprite("RunnerPixelArt", player, Color.white,
                Vector3.zero, new Vector3(0.25f, 0.25f, 1f), 6);
            runnerArt.sprite = LoadPixelSprite("Assets/Ninjazon/Art/Pixel/Character/runner_run_00.png", "runner_run_00");
            var pixelAnimator = runnerArt.gameObject.AddComponent<PixelSpriteAnimator>();
            var runnerArtController = player.gameObject.AddComponent<RunnerArtController>();
            SetReference(runnerArtController, "targetRenderer", runnerArt);
            SetReference(runnerArtController, "animator", pixelAnimator);
            SetReference(runnerArtController, "runnerSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Ninjazon/Art/Pixel/Character/ninjazon-runner-sheet.png"));
            legacy.gameObject.SetActive(false);
            var entityPoolRoot = new GameObject("EntityPool").transform;
            entityPoolRoot.SetParent(world, false);
            var particleRoot = new GameObject("JuiceParticles").transform;
            particleRoot.SetParent(world, false);
            var captureDrone = CreateWorldSprite("CaptureDrone", world, UITheme.Magenta,
                new Vector3(12f, 4.8f, 0f), new Vector3(1.5f, 0.65f, 1f), 10).transform;
            CreateWorldSprite("DroneEye", captureDrone, UITheme.Amber, new Vector3(-0.15f, -0.05f, -0.1f),
                new Vector3(0.18f, 0.18f, 1f), 11);
            captureDrone.gameObject.SetActive(false);

            var canvas = UIFactory.CreateCanvas("GameplayCanvas");
            canvas.transform.SetParent(root.transform, false);
            var safeArea = canvas.transform.Find("SafeArea");
            var hud = UIFactory.CreatePanel(safeArea, "GameplayHUD", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var score = UIFactory.CreateText(hud.transform, "ScoreText", "SCORE 0\n0 m", 38f, UITheme.Text, TextAlignmentOptions.TopLeft,
                new Vector2(0.04f, 0.87f), new Vector2(0.62f, 0.97f), Vector2.zero, Vector2.zero);
            var credits = UIFactory.CreateText(hud.transform, "CreditsText", "CREDITS 0", 31f, UITheme.Amber, TextAlignmentOptions.TopLeft,
                new Vector2(0.04f, 0.81f), new Vector2(0.5f, 0.88f), Vector2.zero, Vector2.zero);
            var speed = UIFactory.CreateText(hud.transform, "SpeedText", "SPEED 6.0\nBAND 1", 25f, UITheme.Muted, TextAlignmentOptions.TopRight,
                new Vector2(0.55f, 0.86f), new Vector2(0.84f, 0.93f), Vector2.zero, Vector2.zero);
            var pauseButton = UIFactory.CreateButton(hud.transform, "PauseButton", "II", UITheme.Magenta, new Vector2(420f, 790f), new Vector2(120f, 100f), null);
            var alertBack = UIFactory.CreateImage(hud.transform, "AlertBar", new Color(0.08f, 0.11f, 0.2f, 0.96f));
            alertBack.rectTransform.anchorMin = new Vector2(0.08f, 0.76f);
            alertBack.rectTransform.anchorMax = new Vector2(0.92f, 0.8f);
            alertBack.rectTransform.offsetMin = alertBack.rectTransform.offsetMax = Vector2.zero;
            var alertFill = UIFactory.CreateImage(alertBack.transform, "AlertFill", UITheme.Magenta).rectTransform;
            alertFill.anchorMin = Vector2.zero;
            alertFill.anchorMax = Vector2.one;
            alertFill.pivot = new Vector2(0f, 0.5f);
            alertFill.offsetMin = alertFill.offsetMax = Vector2.zero;
            var alertText = UIFactory.CreateText(alertBack.transform, "AlertText", "ALERT 0%", 25f, UITheme.Text, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var jump = UIFactory.CreateButton(hud.transform, "JumpButton", "JUMP", UITheme.Cyan, new Vector2(-235f, -760f), new Vector2(390f, 155f), null);
            var slide = UIFactory.CreateButton(hud.transform, "SlideButton", "SLIDE", UITheme.Magenta, new Vector2(235f, -760f), new Vector2(390f, 155f), null);

            var pause = UIFactory.CreatePanel(safeArea, "PauseScreen", UITheme.Panel, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(pause.transform, "Heading", "RUN PAUSED", 60f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0f, 0.75f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);
            var resume = UIFactory.CreateButton(pause.transform, "ResumeButton", "RESUME", UITheme.Cyan, new Vector2(0f, 210f), new Vector2(620f, 110f), null);
            var restart = UIFactory.CreateButton(pause.transform, "RestartButton", "RESTART", UITheme.Magenta, new Vector2(0f, 55f), new Vector2(620f, 105f), null);
            var pauseOptions = UIFactory.CreateButton(pause.transform, "PauseOptionsButton", "OPTIONS", UITheme.Amber, new Vector2(0f, -95f), new Vector2(620f, 105f), null);
            var mainMenu = UIFactory.CreateButton(pause.transform, "MainMenuButton", "MAIN MENU", UITheme.Muted, new Vector2(0f, -245f), new Vector2(620f, 105f), null);

            var options = UIFactory.CreatePanel(safeArea, "OptionsScreen", UITheme.Panel, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(options.transform, "Heading", "OPTIONS", 58f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0f, 0.82f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);
            var music = UIFactory.CreateSlider(options.transform, "MusicSlider", "MUSIC", 0.8f, new Vector2(0f, 250f), null);
            var sfx = UIFactory.CreateSlider(options.transform, "SfxSlider", "SFX", 0.8f, new Vector2(0f, 125f), null);
            var vibration = UIFactory.CreateToggle(options.transform, "VibrationToggle", "Vibration", true, -20f, null);
            var shake = UIFactory.CreateToggle(options.transform, "ShakeToggle", "Screen shake", true, -115f, null);
            var hints = UIFactory.CreateToggle(options.transform, "HintsToggle", "Control hints", true, -210f, null);
            var resetTutorial = UIFactory.CreateButton(options.transform, "ResetTutorialButton", "RESET TUTORIAL", UITheme.Amber,
                new Vector2(0f, -360f), new Vector2(620f, 95f), null);
            var optionsBack = UIFactory.CreateBackButton(options.transform, "OptionsBackButton", null);

            var results = UIFactory.CreatePanel(safeArea, "GameOverScreen", UITheme.Panel, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.84f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(results.transform, "Heading", "TRACE LOCKED", 60f, UITheme.Magenta, TextAlignmentOptions.Center,
                new Vector2(0f, 0.76f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);
            var resultsText = UIFactory.CreateText(results.transform, "RunResults", string.Empty, 36f, UITheme.Text, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            var retry = UIFactory.CreateButton(results.transform, "RetryButton", "RETRY", UITheme.Cyan, new Vector2(0f, -210f), new Vector2(620f, 110f), null);
            var resultsMenu = UIFactory.CreateButton(results.transform, "ResultsMainMenuButton", "MAIN MENU", UITheme.Magenta, new Vector2(0f, -365f), new Vector2(620f, 105f), null);

            var confirm = UIFactory.CreatePanel(safeArea, "ConfirmModal", new Color(0.02f, 0.03f, 0.08f, 0.98f),
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.65f), Vector2.zero, Vector2.zero);
            var confirmText = UIFactory.CreateText(confirm.transform, "Prompt", string.Empty, 42f, UITheme.Text, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero);
            var confirmButton = UIFactory.CreateButton(confirm.transform, "ConfirmButton", "CONFIRM", UITheme.Magenta, new Vector2(-180f, -110f), new Vector2(300f, 95f), null);
            var cancelButton = UIFactory.CreateButton(confirm.transform, "CancelButton", "CANCEL", UITheme.Cyan, new Vector2(180f, -110f), new Vector2(300f, 95f), null);

            var tutorial = UIFactory.CreatePanel(safeArea, "TutorialModal", new Color(0.02f, 0.04f, 0.1f, 0.97f),
                new Vector2(0.07f, 0.35f), new Vector2(0.93f, 0.65f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(tutorial.transform, "TutorialHeading", "COURIER TRAINING", 36f, UITheme.Cyan, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero);
            var tutorialText = UIFactory.CreateText(tutorial.transform, "TutorialPrompt", string.Empty, 38f, UITheme.Text, TextAlignmentOptions.Center,
                new Vector2(0.07f, 0.15f), new Vector2(0.93f, 0.68f), Vector2.zero, Vector2.zero);

            var router = root.AddComponent<UIRouter>();
            var view = root.AddComponent<GameplayView>();
            var presentation = root.AddComponent<RunnerPresentation>();
            var environment = root.AddComponent<GameplayEnvironmentController>();
            var controller = root.AddComponent<GameplayController>();
            var entry = root.AddComponent<GameplaySceneController>();

            SetReference(view, "hudScreen", hud);
            SetReference(view, "pauseScreen", pause);
            SetReference(view, "optionsScreen", options);
            SetReference(view, "resultsScreen", results);
            SetReference(view, "confirmModal", confirm);
            SetReference(view, "tutorialModal", tutorial);
            SetReference(view, "scoreText", score);
            SetReference(view, "creditsText", credits);
            SetReference(view, "speedText", speed);
            SetReference(view, "alertFill", alertFill);
            SetReference(view, "alertText", alertText);
            SetReference(view, "pauseButton", pauseButton);
            SetReference(view, "jumpButton", jump);
            SetReference(view, "slideButton", slide);
            SetReference(view, "resumeButton", resume);
            SetReference(view, "restartButton", restart);
            SetReference(view, "pauseOptionsButton", pauseOptions);
            SetReference(view, "mainMenuButton", mainMenu);
            SetReference(view, "musicSlider", music);
            SetReference(view, "sfxSlider", sfx);
            SetReference(view, "vibrationToggle", vibration);
            SetReference(view, "screenShakeToggle", shake);
            SetReference(view, "controlHintsToggle", hints);
            SetReference(view, "resetTutorialButton", resetTutorial);
            SetReference(view, "optionsBackButton", optionsBack);
            SetReference(view, "resultsText", resultsText);
            SetReference(view, "retryButton", retry);
            SetReference(view, "resultsMainMenuButton", resultsMenu);
            SetReference(view, "confirmText", confirmText);
            SetReference(view, "confirmButton", confirmButton);
            SetReference(view, "cancelButton", cancelButton);
            SetReference(view, "tutorialText", tutorialText);
            SetBindings(view, "screenBindings",
                ("HUD", hud, jump.gameObject),
                ("Pause", pause, resume.gameObject),
                ("Options", options, music.gameObject),
                ("Results", results, retry.gameObject));
            SetBindings(view, "modalBindings",
                ("Confirm", confirm, cancelButton.gameObject),
                ("Tutorial", tutorial, tutorial.gameObject));

            SetReference(presentation, "runnerCamera", runnerCamera);
            SetReference(presentation, "particleRoot", particleRoot);
            SetReference(presentation, "captureDrone", captureDrone);
            SetReference(environment, "runnerCamera", runnerCamera);
            SetReference(environment, "farLayer", farLayer);
            SetReference(environment, "nearLayer", nearLayer);
            SetReference(environment, "backgroundLayer", backgroundLayer);
            SetReference(environment, "pixelBackdropLayer", pixelBackdropLayer);
            SetReference(environment, "routeLayer", routeLayer);
            SetReference(environment, "rainLayer", rainLayer);
            SetReference(environment, "streakLayer", streakLayer);
            SetReference(environment, "alertVignette", vignette);
            SetReference(controller, "view", view);
            SetReference(controller, "router", router);
            SetReference(controller, "presentation", presentation);
            SetReference(controller, "environment", environment);
            SetReference(controller, "world", world);
            SetReference(controller, "player", player);
            SetReference(controller, "playerAnchor", playerAnchor);
            SetReference(controller, "entitySpawnAnchor", entitySpawnAnchor);
            SetReferences(controller, "laneAnchors", laneAnchors);
            SetReference(controller, "scarf", scarf);
            SetReference(controller, "entityPoolRoot", entityPoolRoot);
            SetReference(controller, "difficultyProfile", AssetDatabase.LoadAssetAtPath<DifficultyProfile>($"{ContentFolder}/DefaultDifficulty.asset"));
            SetReference(controller, "gameplayAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-gameplay-atlas.png"));
            SetReference(controller, "runnerArt", runnerArtController);
            SetReference(entry, "controller", controller);

            pause.gameObject.SetActive(false);
            options.gameObject.SetActive(false);
            results.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
            tutorial.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/Gameplay.unity");
        }

        private static Camera CreateCamera(Transform parent, float orthographicSize)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UITheme.Night;
            return camera;
        }

        private static void CreateEventSystem(Transform parent)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private static void AddNeonBackdrop(Transform canvas)
        {
            for (var i = 0; i < 14; i++)
            {
                var building = UIFactory.CreateImage(canvas, "Building" + i,
                    new Color(0.035f, 0.07f + (i % 3) * 0.012f, 0.14f, 1f));
                var rect = building.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(i / 13f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(90f + (i % 4) * 35f, 280f + (i % 5) * 95f);
                rect.anchoredPosition = Vector2.zero;
            }

            for (var i = 0; i < 7; i++)
            {
                var line = UIFactory.CreateImage(canvas, "NeonRain" + i,
                    i % 2 == 0 ? new Color(0.05f, 0.95f, 1f, 0.16f) : new Color(1f, 0.08f, 0.62f, 0.13f));
                var rect = line.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.08f + i * 0.145f, 0.5f);
                rect.sizeDelta = new Vector2(3f, 1850f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }
        }

        internal static ScrollingLayer CreateScrollingLayer(
            Transform parent,
            string name,
            Color color,
            float speedMultiplier,
            float y,
            float height,
            int sortingOrder,
            out SpriteRenderer[] tiles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var layer = root.AddComponent<ScrollingLayer>();
            tiles = new SpriteRenderer[6];
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i] = CreateWorldSprite(name + "Tile" + i, root.transform, color,
                    new Vector3((i - 2.5f) * 10f, y, 0f), new Vector3(10.05f, height, 1f), sortingOrder);
            }

            var serialized = new SerializedObject(layer);
            var tileProperty = serialized.FindProperty("tiles");
            tileProperty.arraySize = tiles.Length;
            for (var i = 0; i < tiles.Length; i++) tileProperty.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            serialized.FindProperty("speedMultiplier").floatValue = speedMultiplier;
            serialized.FindProperty("edgeBleed").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return layer;
        }

        internal static ScrollingLayer CreatePixelLayer(Transform parent, string name, string path, float speedMultiplier, int sortingOrder)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) return CreateScrollingLayer(parent, name, Color.clear, speedMultiplier, 0f, 20f, sortingOrder, out _);

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var layer = root.AddComponent<ScrollingLayer>();
            var scale = 0.66f;
            var width = sprite.bounds.size.x * scale;
            var tiles = new SpriteRenderer[3];
            for (var i = 0; i < tiles.Length; i++)
            {
                var go = new GameObject(i == 0 ? name + "Tile" : $"{name}Tile{i + 1}", typeof(SpriteRenderer));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((i - 1) * width, 0f, 0f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                tiles[i] = go.GetComponent<SpriteRenderer>();
                tiles[i].sprite = sprite;
                tiles[i].sortingOrder = sortingOrder;
            }

            var serialized = new SerializedObject(layer);
            var tileProperty = serialized.FindProperty("tiles");
            tileProperty.arraySize = tiles.Length;
            for (var i = 0; i < tiles.Length; i++) tileProperty.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            serialized.FindProperty("speedMultiplier").floatValue = speedMultiplier;
            serialized.FindProperty("edgeBleed").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return layer;
        }

        private static Sprite LoadPixelSprite(string path, string spriteName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == spriteName)
                   ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
                   ?? EnsurePresentationSprite();
        }

        private static void AddRain(SpriteRenderer[] tiles)
        {
            foreach (var tile in tiles)
            {
                for (var i = 0; i < 5; i++)
                {
                    var drop = CreateWorldSprite($"RainDrop{tile.transform.GetSiblingIndex()}_{i}", tile.transform,
                        new Color(0.25f, 0.75f, 1f, 0.08f + (i % 3) * 0.015f),
                        new Vector3(-0.42f + ((i * 7 + tile.transform.GetSiblingIndex() * 3) % 10) * 0.085f,
                            -0.4f + ((i * 11 + tile.transform.GetSiblingIndex() * 7) % 17) * 0.05f, 0f),
                        new Vector3(0.0025f, 0.01f + (i % 4) * 0.004f, 1f), 3);
                    drop.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                }
            }
        }

        private static void AddSpeedStreaks(SpriteRenderer[] tiles)
        {
            foreach (var tile in tiles)
            {
                for (var i = 0; i < 4; i++)
                {
                    CreateWorldSprite($"SpeedStreak{tile.transform.GetSiblingIndex()}_{i}", tile.transform,
                        i % 2 == 0 ? new Color(1f, 0.08f, 0.62f, 0.2f) : new Color(0.05f, 0.95f, 1f, 0.17f),
                        new Vector3(-4f + i * 2.5f, -6.5f + i * 4f, 0f),
                        new Vector3(0.7f + i * 0.2f, 0.035f, 1f), 4);
                }
            }
        }

        private static SpriteRenderer CreateWorldSprite(
            string name,
            Transform parent,
            Color color,
            Vector3 localPosition,
            Vector3 localScale,
            int sortingOrder)
        {
            var gameObject = new GameObject(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = localScale;
            var renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = EnsurePresentationSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite EnsurePresentationSprite()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(PresentationSpritePath).OfType<Sprite>().FirstOrDefault();
            if (sprite != null) return sprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "NinjazonPixelTexture",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "NinjazonPixel";
            AssetDatabase.CreateAsset(texture, PresentationSpritePath);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(PresentationSpritePath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAllAssetsAtPath(PresentationSpritePath).OfType<Sprite>().First();
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private static void SetReference(Component target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(target.GetType().Name, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(Component target, string propertyName, Transform[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(target.GetType().Name, propertyName);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBindings(
            Component target,
            string propertyName,
            params (string Id, CanvasGroup View, GameObject FirstSelection)[] bindings)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(target.GetType().Name, propertyName);
            property.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = property.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("id").stringValue = bindings[i].Id;
                binding.FindPropertyRelative("view").objectReferenceValue = bindings[i].View;
                binding.FindPropertyRelative("firstSelection").objectReferenceValue = bindings[i].FirstSelection;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDifficultyProfile()
        {
            var path = $"{ContentFolder}/DefaultDifficulty.asset";
            if (AssetDatabase.LoadAssetAtPath<DifficultyProfile>(path) != null) return;
            var profile = ScriptableObject.CreateInstance<DifficultyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
    }
}
