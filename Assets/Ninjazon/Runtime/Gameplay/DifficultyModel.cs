using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public static class DifficultyModel
    {
        public static float CalculateSpeed(float baseSpeed, float maximumSpeed, float distance, float alert)
        {
            var minimum = Mathf.Max(0f, baseSpeed);
            var maximum = Mathf.Max(minimum, maximumSpeed);
            var distanceBoost = Mathf.Max(0f, distance) * 0.0025f;
            var alertBoost = Mathf.Clamp(alert, 0f, 100f) * 0.03f;
            return Mathf.Clamp(minimum + distanceBoost + alertBoost, minimum, maximum);
        }

        public static int DifficultyBand(float distance, float alert)
        {
            return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(0f, distance) / 400f) + (Mathf.Clamp(alert, 0f, 100f) / 34f)), 0, 5);
        }
    }
}

