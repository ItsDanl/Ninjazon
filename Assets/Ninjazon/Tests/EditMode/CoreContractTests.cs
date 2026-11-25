using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Ninjazon.Tests.EditMode
{
    public sealed class CoreContractTests
    {
        [Test]
        public void ScreenStack_PopsInLastInFirstOutOrder()
        {
            var type = RequireType("Ninjazon.Runtime.Navigation.ScreenStack, Ninjazon.Runtime");
            var stack = Activator.CreateInstance(type);
            type.GetMethod("Push")!.Invoke(stack, new object[] { "Main" });
            type.GetMethod("Push")!.Invoke(stack, new object[] { "Options" });

            Assert.That(type.GetMethod("Pop")!.Invoke(stack, null), Is.EqualTo("Options"));
            Assert.That(type.GetProperty("Current")!.GetValue(stack), Is.EqualTo("Main"));
        }

        [Test]
        public void SaveData_DefaultsAreSafeAndVersioned()
        {
            var type = RequireType("Ninjazon.Runtime.Persistence.SaveDataV1, Ninjazon.Runtime");
            var data = Activator.CreateInstance(type);

            Assert.That(type.GetField("schemaVersion")!.GetValue(data), Is.EqualTo(1));
            Assert.That((float)type.GetField("musicVolume")!.GetValue(data), Is.InRange(0f, 1f));
            Assert.That((float)type.GetField("sfxVolume")!.GetValue(data), Is.InRange(0f, 1f));
        }

        [Test]
        public void RunState_AlertClampsAndCaptureOccursAtOneHundred()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.RunState, Ninjazon.Runtime");
            var state = Activator.CreateInstance(type);
            type.GetMethod("AddAlert")!.Invoke(state, new object[] { 125f });

            Assert.That(type.GetProperty("Alert")!.GetValue(state), Is.EqualTo(100f));
            Assert.That(type.GetProperty("IsCaptured")!.GetValue(state), Is.True);

            type.GetMethod("ReduceAlert")!.Invoke(state, new object[] { 30f });
            Assert.That(type.GetProperty("Alert")!.GetValue(state), Is.EqualTo(70f));
        }

        [Test]
        public void SegmentRules_RequiresAtLeastOneSharedOpenRoute()
        {
            var type = RequireType("Ninjazon.Runtime.Content.SegmentRules, Ninjazon.Runtime");
            var method = type.GetMethod("AreCompatible", BindingFlags.Public | BindingFlags.Static)!;

            Assert.That(method.Invoke(null, new object[] { 0b011, 0b110 }), Is.True);
            Assert.That(method.Invoke(null, new object[] { 0b001, 0b100 }), Is.False);
        }

        [Test]
        public void NavigationHistory_BackClosesModalBeforeScreen()
        {
            var type = RequireType("Ninjazon.Runtime.Navigation.NavigationHistory, Ninjazon.Runtime");
            var history = Activator.CreateInstance(type);
            type.GetMethod("PushScreen")!.Invoke(history, new object[] { "Main" });
            type.GetMethod("PushScreen")!.Invoke(history, new object[] { "Options" });
            type.GetMethod("ShowModal")!.Invoke(history, new object[] { "Confirm" });

            Assert.That(type.GetMethod("Back")!.Invoke(history, null), Is.EqualTo("Confirm"));
            Assert.That(type.GetProperty("CurrentScreen")!.GetValue(history), Is.EqualTo("Options"));
            Assert.That(type.GetMethod("Back")!.Invoke(history, null), Is.EqualTo("Options"));
            Assert.That(type.GetProperty("CurrentScreen")!.GetValue(history), Is.EqualTo("Main"));
        }

        [Test]
        public void SaveRepository_MalformedJsonReturnsDefaultsAndRoundTrips()
        {
            var type = RequireType("Ninjazon.Runtime.Persistence.SaveRepository, Ninjazon.Runtime");
            var path = Path.Combine(Path.GetTempPath(), $"ninjazon-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{not-json");

            try
            {
                var repository = Activator.CreateInstance(type, path);
                var loaded = type.GetMethod("Load")!.Invoke(repository, null)!;
                var loadedType = loaded.GetType();
                Assert.That(loadedType.GetField("schemaVersion")!.GetValue(loaded), Is.EqualTo(1));

                loadedType.GetField("bestScore")!.SetValue(loaded, 4321);
                type.GetMethod("Save")!.Invoke(repository, new[] { loaded });
                var reloaded = type.GetMethod("Load")!.Invoke(repository, null)!;
                Assert.That(loadedType.GetField("bestScore")!.GetValue(reloaded), Is.EqualTo(4321));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        [Test]
        public void DifficultyModel_UsesDistanceAndAlertAndClampsAtMaximum()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.DifficultyModel, Ninjazon.Runtime");
            var method = type.GetMethod("CalculateSpeed", BindingFlags.Public | BindingFlags.Static)!;

            Assert.That((float)method.Invoke(null, new object[] { 6f, 13f, 0f, 0f }), Is.EqualTo(6f));
            Assert.That((float)method.Invoke(null, new object[] { 6f, 13f, 300f, 50f }), Is.GreaterThan(6f));
            Assert.That((float)method.Invoke(null, new object[] { 6f, 13f, 99999f, 100f }), Is.EqualTo(13f));
        }

        [Test]
        public void RunnerPresentationLayout_SpawnsFullyBeyondCameraAndBleedsTracksPastEdges()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.RunnerPresentationLayout, Ninjazon.Runtime");
            var spawn = type.GetMethod("CalculateSpawnX", BindingFlags.Public | BindingFlags.Static)!;
            var trackWidth = type.GetMethod("CalculateTrackWidth", BindingFlags.Public | BindingFlags.Static)!;
            var shake = type.GetMethod("ResolveShakeAmplitude", BindingFlags.Public | BindingFlags.Static)!;

            Assert.That((float)spawn.Invoke(null, new object[] { 5.4f, 0.75f, 0.8f }), Is.EqualTo(6.95f).Within(0.001f),
                "The complete entity plus a readability buffer must begin beyond the camera's right edge.");
            Assert.That((float)trackWidth.Invoke(null, new object[] { 5.4f, 1f }), Is.EqualTo(12.8f).Within(0.001f),
                "Route visuals must extend past both camera edges so no gap can become visible.");
            Assert.That((float)shake.Invoke(null, new object[] { false, 0.35f }), Is.Zero,
                "The presentation layer must respect the disabled Screen Shake comfort setting.");
            Assert.That((float)shake.Invoke(null, new object[] { true, 0.35f }), Is.EqualTo(0.35f).Within(0.001f));
        }

        [Test]
        public void RunState_TracksDistanceScoreCreditsAndIgnoresNegativeRewards()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.RunState, Ninjazon.Runtime");
            var state = Activator.CreateInstance(type);
            type.GetMethod("Advance")!.Invoke(state, new object[] { 10f, 2f });
            type.GetMethod("AddScore")!.Invoke(state, new object[] { 50 });
            type.GetMethod("AddCredits")!.Invoke(state, new object[] { 3 });
            type.GetMethod("AddScore")!.Invoke(state, new object[] { -500 });

            Assert.That(type.GetProperty("Distance")!.GetValue(state), Is.EqualTo(10f));
            Assert.That(type.GetProperty("Score")!.GetValue(state), Is.EqualTo(70));
            Assert.That(type.GetProperty("Credits")!.GetValue(state), Is.EqualTo(3));
        }

        private static Type RequireType(string qualifiedName)
        {
            var type = Type.GetType(qualifiedName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {qualifiedName} to exist.");
            return type!;
        }
    }
}
