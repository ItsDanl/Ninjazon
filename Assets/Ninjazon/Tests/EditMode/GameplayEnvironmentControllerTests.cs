using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ninjazon.Runtime.Gameplay;
using System.Collections.Generic;

namespace Ninjazon.Tests.EditMode
{
    public sealed class GameplayEnvironmentControllerTests
    {
        private readonly List<GameObject> objects = new();

        [TearDown]
        public void TearDown()
        {
            ScrollingLayerFixture.DestroyAll();
            foreach (var gameObject in objects) UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ValidateReferences_RejectsPerspectiveCameras()
        {
            var controller = CreateConfiguredController(out var camera);
            camera.orthographic = false;

            Assert.That(controller.ValidateReferences(out var error), Is.False);
            Assert.That(error, Does.Contain("orthographic"));
        }

        [Test]
        public void ValidateReferences_AllowsAnOptionalBackdropThatCannotCoverTheViewport()
        {
            var controller = CreateConfiguredController(out _);
            var invalidBackdrop = ScrollingLayerFixture.Create(new[] { -2f, 2f }, tileWidth: 4f, speedMultiplier: 1f);
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("pixelBackdropLayer").objectReferenceValue = invalidBackdrop;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(controller.ValidateReferences(out var error), Is.True, error);
            Assert.DoesNotThrow(() => controller.Advance(6f, 0f, false, 0.016f));
        }

        [TestCase("farLayer", "FarLayer")]
        [TestCase("nearLayer", "NearLayer")]
        [TestCase("backgroundLayer", "BackgroundLayer")]
        [TestCase("routeLayer", "RouteLayer")]
        [TestCase("rainLayer", "RainLayer")]
        [TestCase("streakLayer", "StreakLayer")]
        public void ValidateReferences_RejectsCoverageFailuresFromEachSerializedLayer(string propertyName, string fieldName)
        {
            var controller = CreateConfiguredController(out _);
            var serializedController = new SerializedObject(controller);
            var invalid = ScrollingLayerFixture.Create(new[] { -2f, 2f }, tileWidth: 4f, speedMultiplier: 1f);

            serializedController.FindProperty(propertyName).objectReferenceValue = invalid;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(controller.ValidateReferences(out var error), Is.False);
            Assert.That(error, Does.Contain(fieldName));
            Assert.That(error, Does.Contain("coverage"));
        }

        private GameplayEnvironmentController CreateConfiguredController(out Camera camera)
        {
            var host = CreateObject("EnvironmentController");
            var controller = host.AddComponent<GameplayEnvironmentController>();
            var cameraObject = CreateObject("RunnerCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            var vignette = CreateObject("AlertVignette").AddComponent<SpriteRenderer>();
            var layer = ScrollingLayerFixture.Create(new[] { -8f, -4f, 0f, 4f }, tileWidth: 4f, speedMultiplier: 1f);
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("runnerCamera").objectReferenceValue = camera;
            serializedController.FindProperty("farLayer").objectReferenceValue = layer;
            serializedController.FindProperty("nearLayer").objectReferenceValue = layer;
            serializedController.FindProperty("backgroundLayer").objectReferenceValue = layer;
            serializedController.FindProperty("pixelBackdropLayer").objectReferenceValue = layer;
            serializedController.FindProperty("routeLayer").objectReferenceValue = layer;
            serializedController.FindProperty("rainLayer").objectReferenceValue = layer;
            serializedController.FindProperty("streakLayer").objectReferenceValue = layer;
            serializedController.FindProperty("alertVignette").objectReferenceValue = vignette;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }
    }
}
