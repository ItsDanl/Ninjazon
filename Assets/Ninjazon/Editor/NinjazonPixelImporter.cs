using Ninjazon.Runtime.Presentation;
using UnityEditor;
using UnityEngine;

namespace Ninjazon.Editor
{
    public sealed class NinjazonPixelImporter : AssetPostprocessor
    {
        public const float PixelsPerUnit = 32f;
        public const string PixelRoot = "Assets/Ninjazon/Art/Pixel/";
        public const string CatalogPath = "Assets/Ninjazon/Content/PixelArtCatalog.asset";

        [InitializeOnLoadMethod]
        private static void EnsureOnEditorLoad()
        {
            const string sessionKey = "Ninjazon.PixelArtStructureReady.v2";
            if (SessionState.GetBool(sessionKey, false)) return;
            SessionState.SetBool(sessionKey, true);
            EditorApplication.delayCall += EnsureStructure;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(PixelRoot)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

#pragma warning disable 0618
            if (assetPath.EndsWith("ninjazon-runner-sheet.png"))
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = RunnerFrames();
            }
            else if (assetPath.EndsWith("ninjazon-gameplay-atlas.png"))
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = GameplaySprites();
            }
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }
#pragma warning restore 0618
        }

#pragma warning disable 0618
        private static SpriteMetaData[] RunnerFrames()
        {
            return new[]
            {
                Frame("runner_idle_00", 18, 858, 180, 228), Frame("runner_idle_01", 225, 858, 180, 228), Frame("runner_idle_02", 430, 858, 180, 228),
                Frame("runner_run_00", 18, 662, 190, 195), Frame("runner_run_01", 210, 662, 190, 195), Frame("runner_run_02", 400, 662, 190, 195), Frame("runner_run_03", 590, 662, 200, 195), Frame("runner_run_04", 800, 662, 200, 195), Frame("runner_run_05", 1010, 662, 200, 195), Frame("runner_run_06", 1225, 662, 205, 195),
                Frame("runner_jump_00", 18, 481, 200, 180), Frame("runner_jump_01", 215, 481, 200, 180), Frame("runner_jump_02", 415, 481, 205, 180),
                Frame("runner_slide_00", 18, 356, 205, 124), Frame("runner_slide_01", 225, 356, 225, 124), Frame("runner_slide_02", 445, 356, 225, 124), Frame("runner_slide_03", 665, 356, 225, 124),
                Frame("runner_hit_00", 18, 226, 205, 129), Frame("runner_hit_01", 225, 226, 205, 129), Frame("runner_hit_02", 430, 226, 205, 129),
                Frame("runner_capture_00", 18, 0, 205, 225), Frame("runner_capture_01", 225, 0, 205, 225), Frame("runner_capture_02", 430, 0, 205, 225), Frame("runner_capture_03", 625, 0, 205, 225)
            };
        }

        private static SpriteMetaData[] GameplaySprites()
        {
            return new[]
            {
                Frame("hazard_barricade", 0, 210, 315, 280, new Vector2(0.5f, 0f)),
                Frame("hazard_laser_gantry", 330, 210, 330, 330, new Vector2(0.5f, 0f)),
                Frame("hazard_blocked_route", 670, 210, 380, 280, new Vector2(0.5f, 0f)),
                Frame("hazard_scanner_drone", 1050, 200, 360, 420, new Vector2(0.5f, 0f)),
                Frame("pickup_data_chip", 1390, 210, 220, 280, new Vector2(0.5f, 0f)),
                Frame("pickup_credit", 1600, 220, 230, 250, new Vector2(0.5f, 0f)),
                Frame("pickup_jammer", 1840, 210, 200, 390, new Vector2(0.5f, 0f))
            };
        }

        private static SpriteMetaData Frame(string name, float x, float y, float width, float height, Vector2? pivot = null)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, y, width, height),
                alignment = (int)SpriteAlignment.Custom,
                pivot = pivot ?? new Vector2(0.5f, 0.5f)
            };
        }
#pragma warning restore 0618

        [MenuItem("Ninjazon/Art/Ensure Pixel Art Structure")]
        public static void EnsureStructure()
        {
            EnsureFolder("Assets/Ninjazon", "Art");
            EnsureFolder("Assets/Ninjazon", "Resources");
            EnsureFolder("Assets/Ninjazon/Art", "Pixel");
            foreach (var folder in new[] { "Character", "Environment", "Hazards", "Pickups", "VFX", "UI" })
                EnsureFolder("Assets/Ninjazon/Art/Pixel", folder);
            if (AssetDatabase.LoadAssetAtPath<PixelArtCatalog>(CatalogPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PixelArtCatalog>(), CatalogPath);
            foreach (var path in new[]
                     {
                         "Assets/Ninjazon/Art/Pixel/Character/ninjazon-runner-sheet.png",
                         "Assets/Ninjazon/Art/Pixel/Character/runner_run_00.png",
                         "Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-gameplay-atlas.png",
                         "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-rooftop-panorama.png",
                         "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-far.png",
                         "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-mid.png",
                         "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-near.png",
                         "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-rooftop-foreground.png"
                     })
            {
                if (System.IO.File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
