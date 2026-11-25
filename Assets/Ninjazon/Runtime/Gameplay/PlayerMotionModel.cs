using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class PlayerMotionModel
    {
        private const float JumpDuration = 0.85f;
        private const float SlideDuration = 0.65f;
        private float jumpRemaining;
        private float slideRemaining;

        public int Route { get; private set; } = 1;
        public bool IsJumping => jumpRemaining > 0f;
        public bool IsSliding => slideRemaining > 0f;
        public float JumpHeight => IsJumping ? Mathf.Sin((1f - jumpRemaining / JumpDuration) * Mathf.PI) * 1.35f : 0f;

        public bool MoveRoute(int direction)
        {
            var next = Mathf.Clamp(Route + direction, 0, 2);
            if (next == Route) return false;
            Route = next;
            return true;
        }

        public bool Jump()
        {
            if (IsJumping) return false;
            jumpRemaining = JumpDuration;
            return true;
        }

        public bool Slide()
        {
            if (IsSliding) return false;
            slideRemaining = SlideDuration;
            return true;
        }

        public void Tick(float deltaTime)
        {
            var delta = Mathf.Max(0f, deltaTime);
            jumpRemaining = Mathf.Max(0f, jumpRemaining - delta);
            slideRemaining = Mathf.Max(0f, slideRemaining - delta);
        }
    }
}

