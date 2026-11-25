using System;
using Ninjazon.Runtime.App;
using Ninjazon.Runtime.Gameplay;
using Ninjazon.Runtime.Presentation;
using Ninjazon.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ninjazon.Tests.EditMode
{
    public sealed class PersistentSceneContractTests
    {
        private const string SceneFolder = "Assets/Ninjazon/Scenes";

        [Test]
        public void MainMenuScene_ContainsACompleteSerializedAuthoredHierarchy()
        {
            var scene = OpenScene("MainMenu");

            Assert.That(FindInScene<Camera>(scene), Is.Not.Null);
            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null);
            Assert.That(FindNamed(scene, "SafeArea"), Is.Not.Null);
            Assert.That(FindNamed(scene, "NeonCityBackground"), Is.Not.Null);
            Assert.That(FindNamed(scene, "MainScreen"), Is.Not.Null);
            Assert.That(FindNamed(scene, "OptionsScreen"), Is.Not.Null);
            Assert.That(FindNamed(scene, "CreditsScreen"), Is.Not.Null);
            Assert.That(FindNamed(scene, "QuitConfirmation"), Is.Not.Null);

            var entry = FindInScene<MainMenuSceneController>(scene);
            var controller = FindInScene<MainMenuController>(scene);
            var view = FindInScene<MainMenuView>(scene);
            Assert.That(entry, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(FindInScene<UIRouter>(scene), Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(new SerializedObject(entry).FindProperty("controller").objectReferenceValue, Is.SameAs(controller));
            Assert.That(view.ValidateReferences(out var error), Is.True, error);
        }

        [Test]
        public void GameplayScene_ContainsACompleteSerializedAuthoredHierarchy()
        {
            var scene = OpenScene("Gameplay");

            foreach (var name in new[]
                     {
                         "SafeArea", "GameplayHUD", "PauseScreen", "OptionsScreen", "GameOverScreen",
                         "ConfirmModal", "TutorialModal", "RunnerWorld", "Player", "RunnerBody", "NeonScarf",
                         "SkylineFar", "SkylineMid", "SkylineNear", "RooftopForeground", "RouteReference", "RainField",
                         "SpeedStreaks", "AlertVignette", "EntityPool", "JuiceParticles"
                     })
            {
                Assert.That(FindNamed(scene, name), Is.Not.Null, $"Gameplay scene requires authored object '{name}'.");
            }

            var entry = FindInScene<GameplaySceneController>(scene);
            var controller = FindInScene<GameplayController>(scene);
            var environment = FindInScene<GameplayEnvironmentController>(scene);
            var presentation = FindInScene<RunnerPresentation>(scene);
            var viewType = Type.GetType("Ninjazon.Runtime.Gameplay.GameplayView, Ninjazon.Runtime");
            Assert.That(entry, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(viewType, Is.Not.Null, "Gameplay requires a serialized GameplayView contract.");
            Assert.That(FindInScene(scene, viewType), Is.Not.Null);
            Assert.That(new SerializedObject(entry).FindProperty("controller").objectReferenceValue, Is.SameAs(controller));
            Assert.That(environment.ValidateReferences(out var environmentError), Is.True, environmentError);

            var view = FindInScene(scene, viewType);
            var arguments = new object[] { null };
            Assert.That((bool)viewType.GetMethod("ValidateReferences")!.Invoke(view, arguments), Is.True, arguments[0] as string);
        }

        [Test]
        public void GameplayScene_UsesAuthoredPixelArtForPlayerAndBackdrop()
        {
            var scene = OpenScene("Gameplay");
            var playerArt = FindNamed(scene, "RunnerPixelArt");
            var backdrop = FindNamed(scene, "RooftopForegroundTile");

            Assert.That(playerArt, Is.Not.Null, "Player pixel art must be a persistent hierarchy object.");
            Assert.That(backdrop, Is.Not.Null, "Rooftop art must be a persistent hierarchy object.");
            Assert.That(playerArt.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("runner_"));
            Assert.That(backdrop.GetComponent<SpriteRenderer>().sprite.name, Does.Contain("ninjazon-rooftop-foreground"));
            Assert.That(FindNamed(scene, "LegacyPlaceholders"), Is.Not.Null,
                "Replaced geometry must be preserved in an explicitly named legacy group.");

            var controller = FindInScene<GameplayController>(scene);
            var atlas = new SerializedObject(controller).FindProperty("gameplayAtlas");
            Assert.That(atlas, Is.Not.Null, "GameplayController requires a serialized gameplay-art atlas binding.");
            Assert.That(atlas.objectReferenceValue, Is.Not.Null, "Gameplay atlas must remain editable and serialized in the scene.");

            var runnerArt = FindInScene<RunnerArtController>(scene);
            var animator = playerArt.GetComponent<PixelSpriteAnimator>();
            Assert.That(runnerArt, Is.Not.Null, "Runner art controller must be persistent on the Player object.");
            Assert.That(animator, Is.Not.Null, "Pixel animator must be persistent on RunnerPixelArt.");
            Assert.That(runnerArt.ValidateReferences(out var runnerError), Is.True, runnerError);
            var runnerBinding = new SerializedObject(controller).FindProperty("runnerArt");
            Assert.That(runnerBinding.objectReferenceValue, Is.SameAs(runnerArt));
        }

        private static Scene OpenScene(string sceneName)
        {
            var path = $"{SceneFolder}/{sceneName}.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"Missing scene asset {path}.");
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return FindInScene(scene, typeof(T)) as T;
        }

        private static Component FindInScene(Scene scene, Type componentType)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren(componentType, true);
                if (component != null) return component;
            }

            return null;
        }

        private static GameObject FindNamed(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var candidate in transforms)
                {
                    if (candidate.name == objectName) return candidate.gameObject;
                }
            }

            return null;
        }
    }
}
