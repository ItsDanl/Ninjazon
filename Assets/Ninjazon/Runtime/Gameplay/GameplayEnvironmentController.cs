using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class GameplayEnvironmentController : MonoBehaviour
    {
        [SerializeField] private Camera runnerCamera;
        [SerializeField] private ScrollingLayer farLayer;
        [SerializeField] private ScrollingLayer nearLayer;
        [SerializeField] private ScrollingLayer backgroundLayer;
        [SerializeField] private ScrollingLayer pixelBackdropLayer;
        [SerializeField] private ScrollingLayer routeLayer;
        [SerializeField] private ScrollingLayer rainLayer;
        [SerializeField] private ScrollingLayer streakLayer;
        [SerializeField] private SpriteRenderer alertVignette;
        [Header("Rain")]
        [SerializeField, Min(0f)] private float rainFallSpeed = 8f;
        [SerializeField, Min(0f)] private float rainWindSpeed = 1.2f;
        [SerializeField, Range(0f, 1f)] private float rainOpacity = 0.11f;

        private readonly List<Transform> rainDrops = new();
        private readonly List<float> rainSpeedFactors = new();
        private bool rainInitialized;
        private bool validated;
        private bool pixelBackdropUsable;
        private bool warnedAboutBackdrop;

        private float clock;

        public bool ValidateReferences(out string error)
        {
            if (runnerCamera == null) { error = "RunnerCamera is required."; return false; }
            if (farLayer == null) { error = "FarLayer is required."; return false; }
            if (nearLayer == null) { error = "NearLayer is required."; return false; }
            if (backgroundLayer == null) { error = "BackgroundLayer is required."; return false; }
            if (pixelBackdropLayer == null) { error = "PixelBackdropLayer is required."; return false; }
            if (routeLayer == null) { error = "RouteLayer is required."; return false; }
            if (rainLayer == null) { error = "RainLayer is required."; return false; }
            if (streakLayer == null) { error = "StreakLayer is required."; return false; }
            if (alertVignette == null) { error = "AlertVignette is required."; return false; }
            if (!runnerCamera.orthographic) { error = "RunnerCamera must be orthographic for 2D environment coverage."; return false; }

            var cameraHalfWidth = RunnerPresentationLayout.CalculateCameraHalfWidth(runnerCamera);
            var cameraCenterX = runnerCamera.transform.position.x;
            if (!ValidateLayerCoverage(farLayer, "FarLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            if (!ValidateLayerCoverage(nearLayer, "NearLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            if (!ValidateLayerCoverage(backgroundLayer, "BackgroundLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            var backdropError = "No pixel backdrop assigned";
            pixelBackdropUsable = pixelBackdropLayer != null &&
                pixelBackdropLayer.ValidateCoverage(cameraCenterX, cameraHalfWidth, out backdropError);
            if (!pixelBackdropUsable && Application.isPlaying && !warnedAboutBackdrop)
            {
                Debug.LogWarning($"Pixel backdrop is unavailable at this aspect ratio: {backdropError}. The main background remains active.", this);
                warnedAboutBackdrop = true;
            }
            if (!ValidateLayerCoverage(routeLayer, "RouteLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            if (!ValidateLayerCoverage(rainLayer, "RainLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            if (!ValidateLayerCoverage(streakLayer, "StreakLayer", cameraCenterX, cameraHalfWidth, out error)) return false;
            error = null;
            return true;
        }

        public void Advance(float worldSpeed, float alert, bool simulationRunning, float deltaTime)
        {
            if (!validated)
            {
                if (!ValidateReferences(out var error)) throw new InvalidOperationException(error);
                validated = true;
            }
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            if (simulationRunning)
            {
                var cameraHalfWidth = RunnerPresentationLayout.CalculateCameraHalfWidth(runnerCamera);
                var leftBoundary = RunnerPresentationLayout.CalculateCameraLeftBoundary(runnerCamera.transform.position.x, cameraHalfWidth);
                var rightBoundary = runnerCamera.transform.position.x + cameraHalfWidth;
                farLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                nearLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                backgroundLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                if (pixelBackdropUsable) pixelBackdropLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                routeLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                rainLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
                AdvanceRain(deltaTime);
                streakLayer.Advance(worldSpeed, deltaTime, leftBoundary, rightBoundary);
            }

            clock += deltaTime;
            var alert01 = Mathf.Clamp01(alert / 100f);
            var pulse = 0.72f + Mathf.Sin(clock * Mathf.Lerp(2f, 9f, alert01)) * 0.28f;
            var color = alertVignette.color;
            color.a = alert01 * Mathf.Lerp(0.08f, 0.24f, pulse);
            alertVignette.color = color;
        }

        private void AdvanceRain(float deltaTime)
        {
            if (!rainInitialized) InitializeRain();
            for (var i = 0; i < rainDrops.Count; i++)
            {
                var drop = rainDrops[i];
                var position = drop.localPosition;
                var speed = rainSpeedFactors[i];
                var tileScale = drop.parent.lossyScale;
                position.x -= rainWindSpeed * speed * deltaTime / Mathf.Max(0.001f, Mathf.Abs(tileScale.x));
                position.y -= rainFallSpeed * speed * deltaTime / Mathf.Max(0.001f, Mathf.Abs(tileScale.y));
                if (position.y < -0.5f)
                {
                    position.y += 1f;
                    position.x = -0.45f + Hash01(i * 47 + Mathf.FloorToInt(clock * 13f)) * 0.9f;
                }
                if (position.x < -0.5f) position.x += 1f;
                drop.localPosition = position;
            }
        }

        private void InitializeRain()
        {
            rainInitialized = true;
            var renderers = rainLayer.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer.name.StartsWith("RainDrop", StringComparison.Ordinal)) continue;
                var drop = renderer.transform;
                var length = Mathf.Lerp(0.2f, 0.5f, Hash01(i * 31 + 7));
                var tileScale = drop.parent.lossyScale;
                drop.localScale = new Vector3(
                    0.025f / Mathf.Max(0.001f, Mathf.Abs(tileScale.x)),
                    length / Mathf.Max(0.001f, Mathf.Abs(tileScale.y)), 1f);
                drop.localRotation = Quaternion.Euler(0f, 0f, -12f);
                drop.localPosition = new Vector3(
                    -0.45f + Hash01(i * 71 + 13) * 0.9f,
                    -0.45f + Hash01(i * 53 + 19) * 0.9f,
                    drop.localPosition.z);
                var color = renderer.color;
                color.a = rainOpacity * Mathf.Lerp(0.55f, 1f, Hash01(i * 23 + 3));
                renderer.color = color;
                rainDrops.Add(drop);
                rainSpeedFactors.Add(Mathf.Lerp(0.75f, 1.35f, Hash01(i * 37 + 11)));
            }
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint hash = (uint)value * 747796405u + 2891336453u;
                hash = (hash >> ((int)(hash >> 28) + 4)) ^ hash;
                hash *= 277803737u;
                return ((hash >> 22) ^ hash) / (float)uint.MaxValue;
            }
        }

        private static bool ValidateLayerCoverage(ScrollingLayer layer, string fieldName, float cameraCenterX, float cameraHalfWidth, out string error)
        {
            if (layer.ValidateCoverage(cameraCenterX, cameraHalfWidth, out var layerError))
            {
                error = null;
                return true;
            }

            error = $"{fieldName} has invalid coverage: {layerError}";
            return false;
        }
    }
}
