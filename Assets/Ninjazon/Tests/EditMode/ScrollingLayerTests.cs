using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ninjazon.Runtime.Gameplay;

namespace Ninjazon.Tests.EditMode
{
    public sealed class ScrollingLayerTests
    {
        [TearDown]
        public void TearDown()
        {
            ScrollingLayerFixture.DestroyAll();
        }

        [Test]
        public void Advance_RecyclesLeftTileAfterRightmostWithoutGap()
        {
            var layer = ScrollingLayerFixture.Create(tileCenters: new[] { -8f, -4f, 0f, 4f }, tileWidth: 4f, speedMultiplier: 1f);

            layer.Advance(worldSpeed: 8f, deltaTime: 1f, leftBoundary: -6f);

            Assert.That(layer.OrderedCenters, Is.EqualTo(new[] { -4f, 0f, 4f, 8f }).AsCollection
                .Using<float>((a, b) => Mathf.Abs(a - b) < 0.001f ? 0 : a.CompareTo(b)));
            Assert.That(layer.HasGap, Is.False);
            Assert.That(layer.ValidateCoverage(cameraHalfWidth: 5f, out var error), Is.True, error);
        }

        [Test]
        public void ValidateCoverage_RequiresCameraWidthAndBothEdgeBleeds()
        {
            var layer = ScrollingLayerFixture.Create(tileCenters: new[] { -8f, -4f, 0f, 4f }, tileWidth: 4f, speedMultiplier: 1f);

            Assert.That(layer.ValidateCoverage(cameraHalfWidth: 5f, out _), Is.True);
            Assert.That(layer.ValidateCoverage(cameraHalfWidth: 6f, out var error), Is.False);
            Assert.That(error, Does.Contain("coverage"));
        }

        [Test]
        public void ValidateCoverage_UsesTheCameraPositionRatherThanWorldZero()
        {
            var layer = ScrollingLayerFixture.Create(tileCenters: new[] { 2f, 6f, 10f, 14f }, tileWidth: 4f, speedMultiplier: 1f);

            Assert.That(layer.ValidateCoverage(cameraCenterX: 10f, cameraHalfWidth: 5f, out var error), Is.True, error);
            Assert.That(layer.ValidateCoverage(cameraCenterX: 0f, cameraHalfWidth: 5f, out error), Is.False);
            Assert.That(error, Does.Contain("coverage"));
        }

        [Test]
        public void ValidateCoverage_IgnoresSubPixelFloatingPointSeams()
        {
            var layer = ScrollingLayerFixture.Create(
                tileCenters: new[] { -6f, -1.9995f, 2.001f, 6.0015f },
                tileWidth: 4f,
                speedMultiplier: 1f);

            Assert.That(layer.HasGap, Is.False);
            Assert.That(layer.ValidateCoverage(cameraHalfWidth: 4.5f, out var error), Is.True, error);
        }

        [Test]
        public void ValidateCoverage_IgnoresSubPixelFloatingPointDriftAtCameraEdges()
        {
            var layer = ScrollingLayerFixture.Create(
                tileCenters: new[] { -3.9995f, 0.0005f, 4.0005f, 8.0005f },
                tileWidth: 4f,
                speedMultiplier: 1f);

            Assert.That(layer.ValidateCoverage(cameraHalfWidth: 5f, out var error), Is.True, error);
        }
    }

    internal static class ScrollingLayerFixture
    {
        private static Sprite sprite;
        private static readonly List<GameObject> objects = new();

        public static ScrollingLayer Create(float[] tileCenters, float tileWidth, float speedMultiplier)
        {
            var host = CreateObject("ScrollingLayer");
            var layer = host.AddComponent<ScrollingLayer>();
            var tiles = new SpriteRenderer[tileCenters.Length];
            for (var i = 0; i < tileCenters.Length; i++)
            {
                var tile = CreateObject($"Tile{i}");
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSprite();
                tile.transform.position = new Vector3(tileCenters[i], 0f, 0f);
                tile.transform.localScale = new Vector3(tileWidth, 1f, 1f);
                tiles[i] = renderer;
            }

            var serializedLayer = new SerializedObject(layer);
            var serializedTiles = serializedLayer.FindProperty("tiles");
            serializedTiles.arraySize = tiles.Length;
            for (var i = 0; i < tiles.Length; i++)
            {
                serializedTiles.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            }

            serializedLayer.FindProperty("speedMultiplier").floatValue = speedMultiplier;
            serializedLayer.FindProperty("edgeBleed").floatValue = 1f;
            serializedLayer.ApplyModifiedPropertiesWithoutUndo();
            return layer;
        }

        public static void DestroyAll()
        {
            foreach (var gameObject in objects)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            objects.Clear();
        }

        private static GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }

        private static Sprite GetSprite()
        {
            if (sprite != null) return sprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return sprite;
        }
    }
}
