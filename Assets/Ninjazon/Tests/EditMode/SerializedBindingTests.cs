using System;
using System.Collections.Generic;
using Ninjazon.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Ninjazon.Tests.EditMode
{
    public sealed class SerializedBindingTests
    {
        private readonly List<GameObject> gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MainMenuView_ValidateReferences_RequiresPlayButtonAndAcceptsCompleteSerializedFixture()
        {
            var empty = CreateGameObject("EmptyMenuView").AddComponent<MainMenuView>();

            Assert.That(empty.ValidateReferences(out var error), Is.False);
            Assert.That(error, Does.Contain("PlayButton"));

            var complete = CreateGameObject("CompleteMenuView").AddComponent<MainMenuView>();
            var serializedView = new SerializedObject(complete);
            SetObjectReference(serializedView, "mainScreen", CreateGameObject("Main", typeof(CanvasGroup)).GetComponent<CanvasGroup>());
            SetObjectReference(serializedView, "optionsScreen", CreateGameObject("Options", typeof(CanvasGroup)).GetComponent<CanvasGroup>());
            SetObjectReference(serializedView, "creditsScreen", CreateGameObject("Credits", typeof(CanvasGroup)).GetComponent<CanvasGroup>());
            SetObjectReference(serializedView, "quitConfirmation", CreateGameObject("Quit", typeof(CanvasGroup)).GetComponent<CanvasGroup>());
            SetObjectReference(serializedView, "playButton", CreateGameObject("Play", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "optionsButton", CreateGameObject("Options", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "creditsButton", CreateGameObject("Credits", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "quitButton", CreateGameObject("Quit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "optionsBackButton", CreateGameObject("OptionsBack", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "creditsBackButton", CreateGameObject("CreditsBack", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "confirmQuitButton", CreateGameObject("ConfirmQuit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "cancelQuitButton", CreateGameObject("CancelQuit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "musicSlider", CreateGameObject("Music", typeof(Slider)).GetComponent<Slider>());
            SetObjectReference(serializedView, "sfxSlider", CreateGameObject("Sfx", typeof(Slider)).GetComponent<Slider>());
            SetObjectReference(serializedView, "vibrationToggle", CreateGameObject("Vibration", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "screenShakeToggle", CreateGameObject("ScreenShake", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "controlHintsToggle", CreateGameObject("ControlHints", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "resetTutorialButton", CreateGameObject("ResetTutorial", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "recordText", CreateGameObject("Record", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
            SetObjectReference(serializedView, "creditsBodyText", CreateGameObject("CreditsBody", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(complete.ValidateReferences(out error), Is.False);
            Assert.That(error, Does.Contain("ScreenBindings"));

            SetBindings(serializedView, "screenBindings",
                ("Main", complete.MainScreen, complete.PlayButton.gameObject),
                ("Options", complete.OptionsScreen, complete.MusicSlider.gameObject),
                ("Credits", complete.CreditsScreen, complete.CreditsBackButton.gameObject));
            SetBindings(serializedView, "modalBindings",
                ("Quit", complete.QuitConfirmation, complete.CancelQuitButton.gameObject));
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(complete.ValidateReferences(out error), Is.True, error);
        }

        [Test]
        public void MainMenuController_UsesSerializedBindingsForLayeredBackAndSelectionRestoration()
        {
            var host = CreateGameObject("MainMenuHost");
            host.SetActive(false);
            var router = host.AddComponent<UIRouter>();
            var view = host.AddComponent<MainMenuView>();
            var controller = host.AddComponent<MainMenuController>();
            var serializedView = new SerializedObject(view);
            var main = CreateGameObject("Main", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var options = CreateGameObject("Options", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var credits = CreateGameObject("Credits", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var quit = CreateGameObject("Quit", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            SetObjectReference(serializedView, "mainScreen", main);
            SetObjectReference(serializedView, "optionsScreen", options);
            SetObjectReference(serializedView, "creditsScreen", credits);
            SetObjectReference(serializedView, "quitConfirmation", quit);
            SetObjectReference(serializedView, "playButton", CreateGameObject("Play", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "optionsButton", CreateGameObject("Options", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "creditsButton", CreateGameObject("Credits", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "quitButton", CreateGameObject("Quit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "optionsBackButton", CreateGameObject("OptionsBack", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "creditsBackButton", CreateGameObject("CreditsBack", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "confirmQuitButton", CreateGameObject("ConfirmQuit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "cancelQuitButton", CreateGameObject("CancelQuit", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "musicSlider", CreateGameObject("Music", typeof(Slider)).GetComponent<Slider>());
            SetObjectReference(serializedView, "sfxSlider", CreateGameObject("Sfx", typeof(Slider)).GetComponent<Slider>());
            SetObjectReference(serializedView, "vibrationToggle", CreateGameObject("Vibration", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "screenShakeToggle", CreateGameObject("ScreenShake", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "controlHintsToggle", CreateGameObject("ControlHints", typeof(Toggle)).GetComponent<Toggle>());
            SetObjectReference(serializedView, "resetTutorialButton", CreateGameObject("ResetTutorial", typeof(Button)).GetComponent<Button>());
            SetObjectReference(serializedView, "recordText", CreateGameObject("Record", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
            SetObjectReference(serializedView, "creditsBodyText", CreateGameObject("CreditsBody", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            SetBindings(serializedView, "screenBindings",
                ("Main", main, view.PlayButton.gameObject),
                ("Options", options, view.MusicSlider.gameObject),
                ("Credits", credits, view.CreditsBackButton.gameObject));
            SetBindings(serializedView, "modalBindings", ("Quit", quit, view.CancelQuitButton.gameObject));
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var serializedController = new SerializedObject(controller);
            SetObjectReference(serializedController, "view", view);
            SetObjectReference(serializedController, "router", router);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            host.SetActive(true);
            controller.Initialize();

            Assert.That(router.CurrentScreen, Is.EqualTo("Main"));
            view.OptionsButton.onClick.Invoke();
            Assert.That(router.CurrentScreen, Is.EqualTo("Options"));
            view.QuitButton.onClick.Invoke();
            Assert.That(router.CurrentModal, Is.EqualTo("Quit"));
            Assert.That(router.TryHandleBack(), Is.True);
            Assert.That(router.CurrentModal, Is.Null);
            view.OptionsBackButton.onClick.Invoke();
            Assert.That(router.CurrentScreen, Is.EqualTo("Main"));
        }

        [Test]
        public void Configure_RejectsDuplicateIdsAndMissingViews()
        {
            var host = CreateGameObject("RouterHost");
            var router = host.AddComponent<UIRouter>();
            var panel = CreateGameObject("Panel", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var duplicate = new[]
            {
                new SerializedViewBinding("Options", panel, null),
                new SerializedViewBinding("Options", panel, null)
            };

            Assert.That(() => router.Configure(duplicate, Array.Empty<SerializedViewBinding>()),
                Throws.ArgumentException.With.Message.Contains("Options"));
            Assert.That(() => router.Configure(
                new[] { new SerializedViewBinding("Credits", null, null) },
                Array.Empty<SerializedViewBinding>()),
                Throws.ArgumentException.With.Message.Contains("Credits"));
        }

        [Test]
        public void Configure_RejectsBlankIdsAndNullBindings()
        {
            var router = CreateGameObject("RouterHost").AddComponent<UIRouter>();
            var panel = CreateGameObject("Panel", typeof(CanvasGroup)).GetComponent<CanvasGroup>();

            Assert.That(() => router.Configure(
                new[] { new SerializedViewBinding(" ", panel, null) },
                Array.Empty<SerializedViewBinding>()),
                Throws.ArgumentException);
            Assert.That(() => router.Configure(
                new SerializedViewBinding[] { null },
                Array.Empty<SerializedViewBinding>()),
                Throws.ArgumentException);
        }

        [Test]
        public void Configure_RejectsDuplicateIdsAcrossScreensAndModals()
        {
            var router = CreateGameObject("RouterHost").AddComponent<UIRouter>();
            var panel = CreateGameObject("Panel", typeof(CanvasGroup)).GetComponent<CanvasGroup>();

            Assert.That(() => router.Configure(
                new[] { new SerializedViewBinding("Options", panel, null) },
                new[] { new SerializedViewBinding("Options", panel, null) }),
                Throws.ArgumentException.With.Message.Contains("Options"));
        }

        [Test]
        public void Configure_FailedValidationLeavesCurrentConfigurationUsable()
        {
            var router = CreateGameObject("RouterHost").AddComponent<UIRouter>();
            var main = CreateGameObject("Main", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var quit = CreateGameObject("Quit", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var replacement = CreateGameObject("Replacement", typeof(CanvasGroup)).GetComponent<CanvasGroup>();

            router.Configure(
                new[] { new SerializedViewBinding("Main", main, null) },
                new[] { new SerializedViewBinding("Quit", quit, null) });
            router.PushScreen("Main");
            router.ShowModal("Quit");

            Assert.That(() => router.Configure(
                new[]
                {
                    new SerializedViewBinding("Replacement", replacement, null),
                    new SerializedViewBinding("Broken", null, null)
                },
                Array.Empty<SerializedViewBinding>()),
                Throws.ArgumentException.With.Message.Contains("Broken"));

            Assert.That(router.CurrentScreen, Is.EqualTo("Main"));
            Assert.That(router.CurrentModal, Is.EqualTo("Quit"));
            Assert.That(main.gameObject.activeSelf, Is.True);
            Assert.That(quit.gameObject.activeSelf, Is.True);
            Assert.That(router.CloseModal(), Is.True);
            router.ShowModal("Quit");
            Assert.That(router.CurrentModal, Is.EqualTo("Quit"));
        }

        [Test]
        public void Configure_RegistersSerializedScreensAndModals()
        {
            var host = CreateGameObject("RouterHost");
            var router = host.AddComponent<UIRouter>();
            var main = CreateGameObject("Main", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var quit = CreateGameObject("Quit", typeof(CanvasGroup)).GetComponent<CanvasGroup>();

            router.Configure(
                new[] { new SerializedViewBinding("Main", main, null) },
                new[] { new SerializedViewBinding("Quit", quit, null) });
            router.PushScreen("Main");
            router.ShowModal("Quit");

            Assert.That(router.CurrentScreen, Is.EqualTo("Main"));
            Assert.That(router.CurrentModal, Is.EqualTo("Quit"));
        }

        private GameObject CreateGameObject(string name, params Type[] components)
        {
            var gameObject = new GameObject(name, components);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetBindings(
            SerializedObject serializedView,
            string propertyName,
            params (string Id, CanvasGroup View, GameObject FirstSelection)[] bindings)
        {
            var property = serializedView.FindProperty(propertyName);
            property.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = property.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("id").stringValue = bindings[i].Id;
                binding.FindPropertyRelative("view").objectReferenceValue = bindings[i].View;
                binding.FindPropertyRelative("firstSelection").objectReferenceValue = bindings[i].FirstSelection;
            }
        }
    }
}
