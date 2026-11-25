using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ninjazon.Runtime.Gameplay;

namespace Ninjazon.Tests.EditMode
{
    public sealed class GameplayContractTests
    {
        [TestCase(-120f, 10f, SwipeAction.LaneUp)]
        [TestCase(120f, -10f, SwipeAction.LaneDown)]
        [TestCase(8f, 120f, SwipeAction.Jump)]
        [TestCase(-8f, -120f, SwipeAction.Slide)]
        [TestCase(20f, 15f, SwipeAction.None)]
        public void SwipeGesture_MapsMobileDirections(float x, float y, SwipeAction expected)
        {
            Assert.That(SwipeGesture.Classify(new Vector2(x, y), 90f), Is.EqualTo(expected));
        }

        [Test]
        public void PlayerMotion_ClampsRoutesAndCompletesJumpAndSlideTimers()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.PlayerMotionModel, Ninjazon.Runtime");
            var motion = Activator.CreateInstance(type);

            type.GetMethod("MoveRoute")!.Invoke(motion, new object[] { -1 });
            type.GetMethod("MoveRoute")!.Invoke(motion, new object[] { -1 });
            Assert.That(type.GetProperty("Route")!.GetValue(motion), Is.EqualTo(0));

            type.GetMethod("Jump")!.Invoke(motion, null);
            Assert.That(type.GetProperty("IsJumping")!.GetValue(motion), Is.True);
            type.GetMethod("Tick")!.Invoke(motion, new object[] { 1.5f });
            Assert.That(type.GetProperty("IsJumping")!.GetValue(motion), Is.False);

            type.GetMethod("Slide")!.Invoke(motion, null);
            Assert.That(type.GetProperty("IsSliding")!.GetValue(motion), Is.True);
            type.GetMethod("Tick")!.Invoke(motion, new object[] { 1f });
            Assert.That(type.GetProperty("IsSliding")!.GetValue(motion), Is.False);
        }

        [Test]
        public void TutorialSequence_OnlyAdvancesForRequestedAction()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.TutorialSequence, Ninjazon.Runtime");
            var tutorial = Activator.CreateInstance(type);

            Assert.That(type.GetProperty("CurrentAction")!.GetValue(tutorial), Is.EqualTo("Switch"));
            Assert.That(type.GetMethod("Accept")!.Invoke(tutorial, new object[] { "Jump" }), Is.False);
            Assert.That(type.GetMethod("Accept")!.Invoke(tutorial, new object[] { "Switch" }), Is.True);
            Assert.That(type.GetProperty("CurrentAction")!.GetValue(tutorial), Is.EqualTo("Jump"));
        }

        [TestCase("Barricade", false, false, true)]
        [TestCase("Barricade", true, false, false)]
        [TestCase("Laser", false, false, true)]
        [TestCase("Laser", false, true, false)]
        [TestCase("Blocked", true, true, true)]
        [TestCase("Scanner", true, true, true)]
        public void CollisionRules_RequireTheCorrectAvoidance(string kind, bool jumping, bool sliding, bool expected)
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.CollisionRules, Ninjazon.Runtime");
            var method = type.GetMethod("ShouldHit", BindingFlags.Public | BindingFlags.Static)!;

            Assert.That(method.Invoke(null, new object[] { kind, 1, 1, jumping, sliding }), Is.EqualTo(expected));
            Assert.That(method.Invoke(null, new object[] { kind, 2, 1, jumping, sliding }), Is.False);
        }

        [TestCase("Barricade")]
        [TestCase("Laser")]
        public void EncounterPlanner_ActionHazardsCanFillAllThreeLanes(string kind)
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.EncounterPlanner, Ninjazon.Runtime");
            var method = type.GetMethod("BuildLaneMask", BindingFlags.Public | BindingFlags.Static)!;
            var mask = (int)method.Invoke(null, new object[] { kind, 1, 0.1f, 0f, 0.45f, 0.6f });
            Assert.That(mask, Is.EqualTo(0b111));
        }

        [TestCase("Blocked")]
        [TestCase("Scanner")]
        public void EncounterPlanner_LaneHazardsCanBlockExactlyTwoDistinctLanes(string kind)
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.EncounterPlanner, Ninjazon.Runtime");
            var method = type.GetMethod("BuildLaneMask", BindingFlags.Public | BindingFlags.Static)!;
            var mask = (int)method.Invoke(null, new object[] { kind, 1, 0.1f, 0.2f, 0.45f, 0.6f });
            Assert.That(CountBits(mask), Is.EqualTo(2));
            Assert.That(mask & (1 << 1), Is.Not.Zero);
        }

        [Test]
        public void EncounterPlanner_SpawnTimingVariesAndHonorsMinimumDelay()
        {
            var type = RequireType("Ninjazon.Runtime.Gameplay.EncounterPlanner, Ninjazon.Runtime");
            var method = type.GetMethod("CalculateSpawnDelay", BindingFlags.Public | BindingFlags.Static)!;
            var shortDelay = (float)method.Invoke(null, new object[] { 1f, 0f, 1f, 0f, 0.72f, 1.32f, 0.14f, 0.48f });
            var longDelay = (float)method.Invoke(null, new object[] { 1f, 1f, 0f, 1f, 0.72f, 1.32f, 0.14f, 0.48f });
            var clampedDelay = (float)method.Invoke(null, new object[] { 0.1f, 0f, 1f, 0f, 0.72f, 1.32f, 0.14f, 0.48f });
            Assert.That(shortDelay, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(longDelay, Is.EqualTo(2.31f).Within(0.001f));
            Assert.That(clampedDelay, Is.EqualTo(0.48f).Within(0.001f));
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }

        private static Type RequireType(string qualifiedName)
        {
            var type = Type.GetType(qualifiedName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {qualifiedName} to exist.");
            return type!;
        }
    }
}
