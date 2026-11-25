using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ninjazon.Tests.EditMode
{
    public sealed class PixelArtImportTests
    {
        private const string CatalogPath = "Assets/Ninjazon/Content/PixelArtCatalog.asset";

        [Test]
        public void RuntimeAssembly_ExposesPixelArtCatalog()
        {
            var type = Type.GetType("Ninjazon.Runtime.Presentation.PixelArtCatalog, Ninjazon.Runtime");
            Assert.That(type, Is.Not.Null,
                "PixelArtCatalog must be a focused runtime asset contract in Ninjazon.Runtime.Presentation.");
        }

        [Test]
        public void Project_ContainsPixelArtCatalogAsset()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing required catalog at {CatalogPath}.");
        }

        [Test]
        public void EveryPixelTexture_UsesMobileSafeCrispImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Ninjazon/Art/Pixel" });
            Assert.That(guids, Is.Not.Empty, "The pixel-art source folder must contain imported textures.");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(4096), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
            }
        }

        [Test]
        public void GameplayAtlas_SourceHasRealTransparentBackground()
        {
            const string atlasPath = "Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-gameplay-atlas.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(atlasPath)), Is.True);
            Assert.That(texture.GetPixel(0, 0).a, Is.Zero.Within(0.001f));
            Assert.That(texture.GetPixel(texture.width - 1, 0).a, Is.Zero.Within(0.001f));
            Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.Zero.Within(0.001f));
            Assert.That(texture.GetPixel(texture.width - 1, texture.height - 1).a, Is.Zero.Within(0.001f));
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void RunnerAnimationRows_DoNotOverlapAdjacentClips()
        {
            const string sheetPath = "Assets/Ninjazon/Art/Pixel/Character/ninjazon-runner-sheet.png";
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath).OfType<Sprite>().ToArray();
            var idleMin = sprites.Where(sprite => sprite.name.StartsWith("runner_idle_")).Min(sprite => sprite.rect.yMin);
            var runMin = sprites.Where(sprite => sprite.name.StartsWith("runner_run_")).Min(sprite => sprite.rect.yMin);
            var runMax = sprites.Where(sprite => sprite.name.StartsWith("runner_run_")).Max(sprite => sprite.rect.yMax);
            var jumpMin = sprites.Where(sprite => sprite.name.StartsWith("runner_jump_")).Min(sprite => sprite.rect.yMin);
            var jumpMax = sprites.Where(sprite => sprite.name.StartsWith("runner_jump_")).Max(sprite => sprite.rect.yMax);
            var slideMin = sprites.Where(sprite => sprite.name.StartsWith("runner_slide_")).Min(sprite => sprite.rect.yMin);
            var slideMax = sprites.Where(sprite => sprite.name.StartsWith("runner_slide_")).Max(sprite => sprite.rect.yMax);
            var hitMin = sprites.Where(sprite => sprite.name.StartsWith("runner_hit_")).Min(sprite => sprite.rect.yMin);
            var hitMax = sprites.Where(sprite => sprite.name.StartsWith("runner_hit_")).Max(sprite => sprite.rect.yMax);
            var captureMax = sprites.Where(sprite => sprite.name.StartsWith("runner_capture_")).Max(sprite => sprite.rect.yMax);

            Assert.That(captureMax, Is.LessThan(hitMin));
            Assert.That(hitMax, Is.LessThan(slideMin));
            Assert.That(slideMax, Is.LessThan(jumpMin));
            Assert.That(jumpMax, Is.LessThan(runMin));
            Assert.That(runMax, Is.LessThan(idleMin));
        }

        [Test]
        public void GameplayAtlas_ContainsEveryNamedHazardAndPickupSprite()
        {
            const string atlasPath = "Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-gameplay-atlas.png";
            var names = AssetDatabase.LoadAllAssetRepresentationsAtPath(atlasPath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .ToArray();

            CollectionAssert.IsSubsetOf(new[]
            {
                "hazard_barricade", "hazard_laser_gantry", "hazard_blocked_route", "hazard_scanner_drone",
                "pickup_data_chip", "pickup_credit", "pickup_jammer"
            }, names, "The gameplay atlas must expose all seven individually editable sprite regions.");
        }
    }
}
