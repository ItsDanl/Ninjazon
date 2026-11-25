using System.Collections.Generic;
using UnityEngine;

namespace Ninjazon.Runtime.Presentation
{
    public enum RunnerVisualState { Idle, Run, Jump, Slide, Hit, Capture }

    public sealed class RunnerArtController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private PixelSpriteAnimator animator;
        [SerializeField] private Texture2D runnerSheet;

        private readonly Dictionary<RunnerVisualState, PixelAnimationClip> clips = new();
        private readonly List<Sprite> generatedFrames = new();
        private RunnerVisualState currentState;
        private bool initialized;

        public bool ValidateReferences(out string error)
        {
            if (targetRenderer == null) { error = "Runner art requires a serialized target renderer."; return false; }
            if (animator == null) { error = "Runner art requires a serialized pixel animator."; return false; }
            if (runnerSheet == null) { error = "Runner art requires a serialized animation sheet."; return false; }
            error = null;
            return true;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (!ValidateReferences(out var error)) throw new System.InvalidOperationException(error);
            initialized = true;

            clips[RunnerVisualState.Idle] = Clip(5f, true,
                R(18, 858, 180, 228), R(225, 858, 180, 228), R(430, 858, 180, 228));
            clips[RunnerVisualState.Run] = ClipAnchored(12f, true, 102.5f,
                R(18, 662, 190, 195), R(210, 662, 190, 195), R(400, 662, 190, 195),
                R(590, 662, 200, 195), R(800, 662, 200, 195), R(1010, 662, 200, 195), R(1225, 662, 205, 195));
            clips[RunnerVisualState.Jump] = Clip(9f, false,
                R(18, 481, 200, 180), R(215, 481, 200, 180), R(415, 481, 205, 180));
            clips[RunnerVisualState.Slide] = Clip(12f, false,
                R(18, 356, 205, 124), R(225, 356, 225, 124), R(445, 356, 225, 124), R(665, 356, 225, 124));
            clips[RunnerVisualState.Hit] = Clip(10f, false,
                R(18, 226, 205, 129), R(225, 226, 205, 129), R(430, 226, 205, 129));
            clips[RunnerVisualState.Capture] = Clip(7f, false,
                R(18, 0, 205, 225), R(225, 0, 205, 225), R(430, 0, 205, 225), R(625, 0, 205, 225));
            SetState(RunnerVisualState.Idle, true);
        }

        public void SetState(RunnerVisualState state, bool restart = false)
        {
            if (!initialized) Initialize();
            if (!restart && state == currentState) return;
            currentState = state;
            animator.Play(clips[state], restart);
        }

        public void Tick(float deltaTime)
        {
            if (!initialized) Initialize();
            animator.Tick(deltaTime);
        }

        private PixelAnimationClip Clip(float fps, bool loop, params Rect[] sourceRects)
            => ClipAnchored(fps, loop, -1f, sourceRects);

        private PixelAnimationClip ClipAnchored(float fps, bool loop, float horizontalAnchorPixels, params Rect[] sourceRects)
        {
            var frames = new Sprite[sourceRects.Length];
            for (var i = 0; i < sourceRects.Length; i++)
            {
                var rect = Clamp(sourceRects[i]);
                var pivotX = horizontalAnchorPixels < 0f ? 0.5f : Mathf.Clamp01(horizontalAnchorPixels / rect.width);
                frames[i] = Sprite.Create(runnerSheet, rect, new Vector2(pivotX, 0f), 32f, 0, SpriteMeshType.FullRect);
                frames[i].name = $"runner_{generatedFrames.Count:00}";
                generatedFrames.Add(frames[i]);
            }
            return new PixelAnimationClip(frames, fps, loop);
        }

        private Rect Clamp(Rect source)
        {
            source.x = Mathf.Clamp(source.x, 0f, runnerSheet.width - 1f);
            source.y = Mathf.Clamp(source.y, 0f, runnerSheet.height - 1f);
            source.width = Mathf.Min(source.width, runnerSheet.width - source.x);
            source.height = Mathf.Min(source.height, runnerSheet.height - source.y);
            return source;
        }

        private static Rect R(float x, float y, float width, float height) => new(x, y, width, height);

        private void OnDestroy()
        {
            foreach (var frame in generatedFrames)
                if (frame != null) Destroy(frame);
            generatedFrames.Clear();
        }
    }
}
