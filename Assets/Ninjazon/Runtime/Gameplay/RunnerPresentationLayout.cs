using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public static class RunnerPresentationLayout
    {
        public static float CalculateSpawnX(float cameraHalfWidth, float entityHalfWidth, float buffer)
        {
            return Mathf.Max(0f, cameraHalfWidth) + Mathf.Max(0f, entityHalfWidth) + Mathf.Max(0f, buffer);
        }

        public static float CalculateTrackWidth(float cameraHalfWidth, float edgeBleed)
        {
            return (Mathf.Max(0f, cameraHalfWidth) + Mathf.Max(0f, edgeBleed)) * 2f;
        }

        public static float CalculateCameraHalfWidth(Camera camera)
        {
            if (camera == null) throw new System.ArgumentNullException(nameof(camera));
            if (!camera.orthographic) throw new System.ArgumentException("Camera must be orthographic.", nameof(camera));
            return camera.orthographicSize * camera.aspect;
        }

        public static float CalculateCameraLeftBoundary(float cameraX, float cameraHalfWidth)
        {
            return cameraX - Mathf.Max(0f, cameraHalfWidth);
        }

        public static float ResolveShakeAmplitude(bool enabled, float requestedAmplitude)
        {
            return enabled ? Mathf.Max(0f, requestedAmplitude) : 0f;
        }
    }
}
