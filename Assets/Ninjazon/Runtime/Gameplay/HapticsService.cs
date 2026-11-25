using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public static class HapticsService
    {
        public static void Pulse(bool enabled, bool strong)
        {
            if (!enabled) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
