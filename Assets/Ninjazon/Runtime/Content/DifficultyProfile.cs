using UnityEngine;

namespace Ninjazon.Runtime.Content
{
    [CreateAssetMenu(menuName = "Ninjazon/Difficulty Profile", fileName = "DifficultyProfile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        public float baseSpeed = 6f;
        public float maximumSpeed = 13f;
        public AnimationCurve distanceRamp = AnimationCurve.EaseInOut(0f, 0f, 1200f, 1f);
        public AnimationCurve alertRamp = AnimationCurve.Linear(0f, 0f, 100f, 1f);
        public AnimationCurve spawnInterval = AnimationCurve.Linear(0f, 1.45f, 1f, 0.68f);

        public float EvaluateSpeed(float distance, float alert)
        {
            var pressure = Mathf.Clamp01(distanceRamp.Evaluate(Mathf.Max(0f, distance)) * 0.72f + alertRamp.Evaluate(Mathf.Clamp(alert, 0f, 100f)) * 0.28f);
            return Mathf.Lerp(baseSpeed, Mathf.Max(baseSpeed, maximumSpeed), pressure);
        }

        public float EvaluateSpawnInterval(float distance, float alert)
        {
            var speed = EvaluateSpeed(distance, alert);
            var normalized = Mathf.InverseLerp(baseSpeed, Mathf.Max(baseSpeed, maximumSpeed), speed);
            return Mathf.Max(0.35f, spawnInterval.Evaluate(normalized));
        }
    }
}

