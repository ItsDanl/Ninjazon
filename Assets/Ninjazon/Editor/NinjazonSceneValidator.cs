using System.Linq;
using Ninjazon.Runtime.Gameplay;
using Ninjazon.Runtime.Presentation;
using UnityEditor;
using UnityEngine;

namespace Ninjazon.Editor
{
    public static class NinjazonSceneValidator
    {
        [MenuItem("Ninjazon/Validate Loaded Hierarchy")]
        public static void ValidateLoadedHierarchy()
        {
            var missingCount = 0;
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!gameObject.scene.IsValid()) continue;

                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (count == 0) continue;

                missingCount += count;
                Debug.LogError($"NINJAZON_MISSING_SCRIPT: {GetPath(gameObject.transform)} ({count})", gameObject);
            }

            missingCount += ValidateComponents(Resources.FindObjectsOfTypeAll<GameplayController>(),
                component => component.ValidateReferences(out var error) ? null : error);
            missingCount += ValidateComponents(Resources.FindObjectsOfTypeAll<GameplayEnvironmentController>(),
                component => component.ValidateReferences(out var error) ? null : error);
            missingCount += ValidateComponents(Resources.FindObjectsOfTypeAll<RunnerPresentation>(),
                component => component.ValidateReferences(out var error) ? null : error);
            missingCount += ValidateComponents(Resources.FindObjectsOfTypeAll<RunnerArtController>(),
                component => component.ValidateReferences(out var error) ? null : error);

            var loadedGameplay = Resources.FindObjectsOfTypeAll<GameplayController>()
                .Any(component => component.gameObject.scene.IsValid());
            foreach (var requiredName in loadedGameplay ? new[] { "RunnerPixelArt", "RooftopPixelLayer" } : System.Array.Empty<string>())
            {
                var authored = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(candidate => candidate.scene.IsValid() && candidate.name == requiredName);
                if (authored != null && authored.activeInHierarchy) continue;
                missingCount++;
                Debug.LogError($"NINJAZON_ART_ROOT_INVALID: {requiredName} is missing or inactive.", authored);
            }

            const string atlasPath = "Assets/Ninjazon/Art/Pixel/Hazards/ninjazon-gameplay-atlas.png";
            var requiredSprites = new[] { "hazard_barricade", "hazard_laser_gantry", "hazard_blocked_route", "hazard_scanner_drone", "pickup_data_chip", "pickup_credit", "pickup_jammer" };
            var atlasSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(atlasPath).OfType<Sprite>().Select(sprite => sprite.name).ToArray();
            foreach (var requiredSprite in requiredSprites.Where(requiredSprite => !atlasSprites.Contains(requiredSprite)))
            {
                missingCount++;
                Debug.LogError($"NINJAZON_ATLAS_INVALID: Missing sliced sprite {requiredSprite}.");
            }

            if (missingCount == 0)
                Debug.Log("NINJAZON_HIERARCHY_VALID: No missing scripts in loaded scenes.");
        }

        private static int ValidateComponents<T>(T[] components, System.Func<T, string> validate) where T : Component
        {
            var errors = 0;
            foreach (var component in components.Where(component => component.gameObject.scene.IsValid()))
            {
                var error = validate(component);
                if (string.IsNullOrEmpty(error)) continue;
                errors++;
                Debug.LogError($"NINJAZON_REFERENCE_INVALID: {GetPath(component.transform)}: {error}", component);
            }
            return errors;
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return transform.gameObject.scene.name + ":" + path;
        }
    }
}
