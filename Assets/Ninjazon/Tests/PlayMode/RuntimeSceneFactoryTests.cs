using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ninjazon.Tests.PlayMode
{
    public sealed class PersistentScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuScene_InitializesItsExistingAuthoredHierarchy()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            var authoredRoot = GameObject.Find("NinjazonRoot");
            Assert.That(authoredRoot, Is.Not.Null);
            var authoredRootId = authoredRoot.GetInstanceID();
            yield return null;

            Assert.That(GameObject.Find("NinjazonRoot").GetInstanceID(), Is.EqualTo(authoredRootId),
                "Scene startup must initialize the serialized hierarchy rather than replacing it.");
            Assert.That(GameObject.Find("PlayButton"), Is.Not.Null);
            Assert.That(GameObject.Find("OptionsButton"), Is.Not.Null);
            Assert.That(GameObject.Find("CreditsButton"), Is.Not.Null);
            Assert.That(GameObject.Find("QuitButton"), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null, "The menu needs a camera so Unity does not overlay 'No cameras rendering'.");
            var safeArea = GameObject.Find("SafeArea").transform;
            Assert.That(safeArea.GetSiblingIndex(), Is.EqualTo(safeArea.parent.childCount - 1), "Interactive UI must render above the backdrop.");
            var router = FindRouter();
            router.GetType().GetMethod("PushScreen")!.Invoke(router, new object[] { "Options" });
            var back = GameObject.Find("OptionsBackButton");
            AssertTopLeftBack(back);
            back.GetComponent<Button>().onClick.Invoke();
            Assert.That(router.GetType().GetProperty("CurrentScreen")!.GetValue(router), Is.EqualTo("Main"));

            router.GetType().GetMethod("PushScreen")!.Invoke(router, new object[] { "Credits" });
            var creditsBack = GameObject.Find("CreditsBackButton");
            AssertTopLeftBack(creditsBack);
            creditsBack.GetComponent<Button>().onClick.Invoke();
            Assert.That(router.GetType().GetProperty("CurrentScreen")!.GetValue(router), Is.EqualTo("Main"));
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GameplayScene_InitializesItsExistingAuthoredHierarchy()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            var authoredRoot = GameObject.Find("NinjazonRoot");
            Assert.That(authoredRoot, Is.Not.Null);
            var authoredRootId = authoredRoot.GetInstanceID();
            yield return null;

            Assert.That(GameObject.Find("NinjazonRoot").GetInstanceID(), Is.EqualTo(authoredRootId),
                "Scene startup must initialize the serialized hierarchy rather than replacing it.");
            Assert.That(GameObject.Find("GameplayHUD"), Is.Not.Null);
            Assert.That(GameObject.Find("AlertFill"), Is.Not.Null);
            Assert.That(GameObject.Find("JumpButton"), Is.Not.Null);
            Assert.That(GameObject.Find("SlideButton"), Is.Not.Null);
            Assert.That(GameObject.Find("PauseButton"), Is.Not.Null);
            Assert.That(GameObject.Find("SkylineFar"), Is.Not.Null, "Gameplay needs a moving background layer for depth.");
            Assert.That(GameObject.Find("SkylineNear"), Is.Not.Null, "Gameplay needs a second parallax layer for readable motion.");
            Assert.That(GameObject.Find("SpeedStreaks"), Is.Not.Null, "Gameplay needs speed feedback independent of obstacles.");
            Assert.That(GameObject.Find("RainField"), Is.Not.Null, "The cyberpunk city needs a lightweight animated rain layer.");
            Assert.That(GameObject.Find("AlertVignette"), Is.Not.Null, "Alert escalation needs an in-world presentation layer.");
            Assert.That(GameObject.Find("JuiceParticles"), Is.Not.Null, "Pickup and impact feedback must use a reusable particle layer.");
            Assert.That(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Count(renderer => renderer.gameObject.name.StartsWith("RouteReferenceTile")), Is.GreaterThanOrEqualTo(6),
                "The route reference layer must remain a persistent scrolling hierarchy.");
            var router = FindRouter();
            router.GetType().GetMethod("CloseModal")!.Invoke(router, null);
            router.GetType().GetMethod("PushScreen")!.Invoke(router, new object[] { "Options" });
            var back = GameObject.Find("OptionsBackButton");
            AssertTopLeftBack(back);
            back.GetComponent<Button>().onClick.Invoke();
            Assert.That(router.GetType().GetProperty("CurrentScreen")!.GetValue(router), Is.EqualTo("HUD"));
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            yield return null;
        }

        private static void AssertTopLeftBack(GameObject back)
        {
            Assert.That(back, Is.Not.Null, "Layered screens must always expose a touch-accessible Back control.");
            var rect = back.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "Back must remain pinned to the visible top-left corner.");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)), "Back must remain pinned to the visible top-left corner.");
            Assert.That(rect.anchoredPosition.x, Is.GreaterThanOrEqualTo(120f), "Back needs enough safe inset to keep the full touch target on-screen.");
        }

        private static UnityEngine.Object FindRouter()
        {
            var type = Type.GetType("Ninjazon.Runtime.UI.UIRouter, Ninjazon.Runtime");
            Assert.That(type, Is.Not.Null);
            var router = UnityEngine.Object.FindFirstObjectByType(type!);
            Assert.That(router, Is.Not.Null);
            return router!;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var name in new[] { "NinjazonRoot", "NinjazonApp", "EventSystem" })
            {
                var created = GameObject.Find(name);
                if (created != null) UnityEngine.Object.Destroy(created);
            }
            yield return null;
        }
    }
}
