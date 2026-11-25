using System;
using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public enum RunPhase
    {
        Tutorial,
        Running,
        Paused,
        Captured
    }

    public sealed class RunState
    {
        public event Action Changed;

        public float Distance { get; private set; }
        public int Score { get; private set; }
        public int Credits { get; private set; }
        public float Alert { get; private set; }
        public bool IsCaptured => Alert >= 100f || Phase == RunPhase.Captured;
        public RunPhase Phase { get; private set; } = RunPhase.Running;

        public void AddAlert(float amount)
        {
            Alert = Mathf.Clamp(Alert + Mathf.Max(0f, amount), 0f, 100f);
            if (Alert >= 100f)
            {
                Phase = RunPhase.Captured;
            }
            Changed?.Invoke();
        }

        public void ReduceAlert(float amount)
        {
            Alert = Mathf.Clamp(Alert - Mathf.Max(0f, amount), 0f, 100f);
            Changed?.Invoke();
        }

        public void Advance(float distance, float scorePerUnit)
        {
            var safeDistance = Mathf.Max(0f, distance);
            Distance += safeDistance;
            Score += Mathf.RoundToInt(safeDistance * Mathf.Max(0f, scorePerUnit));
            Changed?.Invoke();
        }

        public void AddScore(int amount)
        {
            Score += Mathf.Max(0, amount);
            Changed?.Invoke();
        }

        public void AddCredits(int amount)
        {
            Credits += Mathf.Max(0, amount);
            Changed?.Invoke();
        }

        public void SetPhase(RunPhase phase)
        {
            Phase = phase;
            Changed?.Invoke();
        }
    }
}

