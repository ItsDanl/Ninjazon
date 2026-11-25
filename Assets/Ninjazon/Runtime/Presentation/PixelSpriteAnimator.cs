using UnityEngine;

namespace Ninjazon.Runtime.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;

        private PixelAnimationClip currentClip;
        private float elapsed;
        private int currentFrameIndex = -1;

        public int CurrentFrameIndex => currentFrameIndex;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        }

        public void Play(PixelAnimationClip clip, bool restart = false)
        {
            if (!restart && ReferenceEquals(currentClip, clip)) return;
            currentClip = clip;
            elapsed = 0f;
            currentFrameIndex = -1;
            ApplyFrame(0);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f) return;
            if (currentClip?.Frames == null || currentClip.Frames.Length == 0)
            {
                ApplyFrame(-1);
                return;
            }

            elapsed += deltaTime;
            var frame = Mathf.FloorToInt(elapsed * currentClip.FramesPerSecond);
            frame = currentClip.Loop
                ? frame % currentClip.Frames.Length
                : Mathf.Min(frame, currentClip.Frames.Length - 1);
            ApplyFrame(frame);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            currentFrameIndex = frameIndex;
            targetRenderer.sprite = frameIndex >= 0 && currentClip?.Frames != null && frameIndex < currentClip.Frames.Length
                ? currentClip.Frames[frameIndex]
                : null;
        }
    }
}
