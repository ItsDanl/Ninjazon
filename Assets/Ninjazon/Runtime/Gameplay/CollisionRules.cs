namespace Ninjazon.Runtime.Gameplay
{
    public static class CollisionRules
    {
        public static bool ShouldHit(string kind, int hazardRoute, int playerRoute, bool jumping, bool sliding)
        {
            if (hazardRoute != playerRoute) return false;

            return kind switch
            {
                "Barricade" => !jumping,
                "Laser" => !sliding,
                "Blocked" => true,
                "Scanner" => true,
                _ => false
            };
        }
    }

    public static class EncounterPlanner
    {
        private const int AllLanesMask = 0b111;

        public static int BuildLaneMask(
            string kind,
            int primaryLane,
            float multiLaneRoll,
            float secondaryLaneRoll,
            float fullLaneChance,
            float twoLaneChance)
        {
            primaryLane = UnityEngine.Mathf.Clamp(primaryLane, 0, 2);
            if ((kind == "Barricade" || kind == "Laser") && multiLaneRoll < UnityEngine.Mathf.Clamp01(fullLaneChance))
                return AllLanesMask;

            if ((kind == "Blocked" || kind == "Scanner") && multiLaneRoll < UnityEngine.Mathf.Clamp01(twoLaneChance))
            {
                var direction = secondaryLaneRoll < 0.5f ? 1 : 2;
                var secondLane = (primaryLane + direction) % 3;
                return (1 << primaryLane) | (1 << secondLane);
            }

            return 1 << primaryLane;
        }

        public static float CalculateSpawnDelay(
            float baseline,
            float jitterRoll,
            float extendedGapRoll,
            float extendedGapScaleRoll,
            float minimumMultiplier,
            float maximumMultiplier,
            float extendedGapChance,
            float minimumDelay)
        {
            var low = UnityEngine.Mathf.Max(0.05f, minimumMultiplier);
            var high = UnityEngine.Mathf.Max(low, maximumMultiplier);
            var delay = UnityEngine.Mathf.Max(0f, baseline) * UnityEngine.Mathf.Lerp(low, high, UnityEngine.Mathf.Clamp01(jitterRoll));
            if (extendedGapRoll < UnityEngine.Mathf.Clamp01(extendedGapChance))
                delay *= UnityEngine.Mathf.Lerp(1.3f, 1.75f, UnityEngine.Mathf.Clamp01(extendedGapScaleRoll));
            return UnityEngine.Mathf.Max(UnityEngine.Mathf.Max(0.05f, minimumDelay), delay);
        }
    }
}
