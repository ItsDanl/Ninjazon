using System.Linq;
using Ninjazon.Runtime.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ninjazon.Editor
{
    /// <summary>
    /// Surgically upgrades only the environment children in the persistent Gameplay
    /// scene. Other authored objects and user edits are left intact.
    /// </summary>
    public static class NinjazonParallaxSceneUpgrade
    {
        private const string GameplayScenePath = "Assets/Ninjazon/Scenes/Gameplay.unity";

        [MenuItem("Ninjazon/Upgrade Gameplay Layout Anchors", priority = 3)]
        public static void UpgradeLayoutAnchors()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += UpgradeLayoutAnchors;
                return;
            }

            var scene = SceneManager.GetSceneByPath(GameplayScenePath);
            var openedForUpgrade = !scene.IsValid() || !scene.isLoaded;
            if (openedForUpgrade) scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "NinjazonRoot");
            var world = root != null ? root.transform.Find("RunnerWorld") : null;
            var controller = root != null ? root.GetComponent<GameplayController>() : null;
            if (world == null || controller == null)
            {
                Debug.LogError("NINJAZON_LAYOUT_UPGRADE_FAILED: Gameplay scene is missing its root, world, or controller.");
                if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var serialized = new SerializedObject(controller);
            var laneProperty = serialized.FindProperty("laneAnchors");
            var visibleLayout = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "GameplayLayout_EDIT_ME")?.transform;
            var alreadyConfigured = serialized.FindProperty("playerAnchor").objectReferenceValue != null &&
                                    serialized.FindProperty("entitySpawnAnchor").objectReferenceValue != null &&
                                    laneProperty.arraySize == 3 &&
                                    Enumerable.Range(0, 3).All(i => laneProperty.GetArrayElementAtIndex(i).objectReferenceValue != null) &&
                                    visibleLayout != null && visibleLayout.GetComponent<GameplayLayoutGuide>() != null;
            if (alreadyConfigured)
            {
                if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var layout = visibleLayout ?? root.transform.Find("GameplayLayout_EDIT_ME") ?? world.Find("GameplayLayout") ?? new GameObject("GameplayLayout_EDIT_ME").transform;
            layout.name = "GameplayLayout_EDIT_ME";
            layout.SetParent(null, true);
            var playerAnchor = FindOrCreateAnchor(layout, "PlayerAnchor", new Vector3(-2.65f, -1.2f, 0f));
            var spawnAnchor = FindOrCreateAnchor(layout, "EntitySpawnAnchor", new Vector3(8.5f, 0f, 0f));
            var lanes = new[]
            {
                FindOrCreateAnchor(layout, "LaneBottom", new Vector3(0f, -4.2f, 0f)),
                FindOrCreateAnchor(layout, "LaneMiddle", new Vector3(0f, -1.2f, 0f)),
                FindOrCreateAnchor(layout, "LaneTop", new Vector3(0f, 1.8f, 0f))
            };
            serialized.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            serialized.FindProperty("entitySpawnAnchor").objectReferenceValue = spawnAnchor;
            laneProperty.arraySize = lanes.Length;
            for (var i = 0; i < lanes.Length; i++) laneProperty.GetArrayElementAtIndex(i).objectReferenceValue = lanes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var guide = layout.GetComponent<GameplayLayoutGuide>() ?? layout.gameObject.AddComponent<GameplayLayoutGuide>();
            var serializedGuide = new SerializedObject(guide);
            serializedGuide.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            serializedGuide.FindProperty("entitySpawnAnchor").objectReferenceValue = spawnAnchor;
            var guideLanes = serializedGuide.FindProperty("laneAnchors");
            guideLanes.arraySize = lanes.Length;
            for (var i = 0; i < lanes.Length; i++) guideLanes.GetArrayElementAtIndex(i).objectReferenceValue = lanes[i];
            serializedGuide.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("NINJAZON_LAYOUT_UPGRADE_COMPLETE: editable player, lane, and entity spawn anchors installed.");
        }

        [MenuItem("Ninjazon/Upgrade Gameplay Parallax", priority = 2)]
        public static void TryUpgrade()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryUpgrade;
                return;
            }

            var scene = SceneManager.GetSceneByPath(GameplayScenePath);
            var openedForUpgrade = !scene.IsValid() || !scene.isLoaded;
            if (openedForUpgrade) scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "NinjazonRoot");
            var world = root != null ? root.transform.Find("RunnerWorld") : null;
            var environment = root != null ? root.GetComponent<GameplayEnvironmentController>() : null;
            if (world == null || environment == null)
            {
                Debug.LogError("NINJAZON_PARALLAX_UPGRADE_FAILED: Gameplay scene is missing NinjazonRoot/RunnerWorld or GameplayEnvironmentController.");
                if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            if (world.Find("SkylineFar") != null)
            {
                if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            foreach (var oldName in new[] { "RooftopPixelLayer", "ScrollingBackground", "ParallaxFar", "ParallaxNear", "RouteTracks", "RouteReference" })
            {
                var oldLayer = world.Find(oldName);
                if (oldLayer != null) Object.DestroyImmediate(oldLayer.gameObject);
            }

            var background = NinjazonProjectBuilder.CreatePixelLayer(world, "SkylineFar", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-far.png", 0.18f, -40);
            var far = NinjazonProjectBuilder.CreatePixelLayer(world, "SkylineMid", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-mid.png", 0.34f, -30);
            var near = NinjazonProjectBuilder.CreatePixelLayer(world, "SkylineNear", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-skyline-near.png", 0.58f, -20);
            var rooftop = NinjazonProjectBuilder.CreatePixelLayer(world, "RooftopForeground", "Assets/Ninjazon/Art/Pixel/Environment/ninjazon-rooftop-foreground.png", 1f, -5);
            var route = NinjazonProjectBuilder.CreateScrollingLayer(world, "RouteReference", Color.clear, 1f, -1.2f, 9f, -6, out _);

            var rain = world.Find("RainField")?.GetComponent<ScrollingLayer>();
            if (rain != null)
            {
                var serializedRain = new SerializedObject(rain);
                serializedRain.FindProperty("speedMultiplier").floatValue = 0.22f;
                serializedRain.ApplyModifiedPropertiesWithoutUndo();
            }

            var serializedEnvironment = new SerializedObject(environment);
            serializedEnvironment.FindProperty("backgroundLayer").objectReferenceValue = background;
            serializedEnvironment.FindProperty("farLayer").objectReferenceValue = far;
            serializedEnvironment.FindProperty("nearLayer").objectReferenceValue = near;
            serializedEnvironment.FindProperty("pixelBackdropLayer").objectReferenceValue = rooftop;
            serializedEnvironment.FindProperty("routeLayer").objectReferenceValue = route;
            serializedEnvironment.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedForUpgrade) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("NINJAZON_PARALLAX_UPGRADE_COMPLETE: persistent Far/Mid/Near/Rooftop layers installed without replacing the scene.");
        }

        private static Transform FindOrCreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var anchor = parent.Find(name);
            if (anchor != null) return anchor;
            anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            return anchor;
        }
    }
}
