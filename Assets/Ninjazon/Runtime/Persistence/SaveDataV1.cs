using System;

namespace Ninjazon.Runtime.Persistence
{
    [Serializable]
    public sealed class SaveDataV1
    {
        public int schemaVersion = 1;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public bool vibration = true;
        public bool screenShake = true;
        public bool controlHints = true;
        public bool tutorialComplete;
        public int bestScore;
        public float bestDistance;
        public int lifetimeCredits;
    }
}

