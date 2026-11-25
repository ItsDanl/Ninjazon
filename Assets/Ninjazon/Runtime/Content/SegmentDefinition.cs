using UnityEngine;

namespace Ninjazon.Runtime.Content
{
    [CreateAssetMenu(menuName = "Ninjazon/Segment Definition", fileName = "SegmentDefinition")]
    public sealed class SegmentDefinition : ScriptableObject
    {
        public GameObject prefab;
        [Range(0, 5)] public int minimumDifficulty;
        [Range(0, 5)] public int maximumDifficulty = 5;
        [Min(0.01f)] public float weight = 1f;
        [Range(1, 7)] public int entryOpenRoutes = 0b111;
        [Range(1, 7)] public int exitOpenRoutes = 0b111;

        public bool SupportsDifficulty(int band) => band >= minimumDifficulty && band <= maximumDifficulty;
        public bool CanFollow(SegmentDefinition previous) => previous == null || SegmentRules.AreCompatible(previous.exitOpenRoutes, entryOpenRoutes);
    }
}

