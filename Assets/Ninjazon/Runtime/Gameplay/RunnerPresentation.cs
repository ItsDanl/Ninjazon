using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class RunnerPresentation : MonoBehaviour
    {
        private sealed class Particle
        {
            public GameObject view;
            public Vector3 velocity;
            public float life;
            public float duration;
        }

        [SerializeField] private Camera runnerCamera;
        [SerializeField] private Transform particleRoot;
        [SerializeField] private Transform captureDrone;

        private readonly List<Particle> particles = new();
        private readonly Queue<GameObject> particlePool = new();
        private Vector3 cameraOrigin;
        private float shakeTime;
        private float shakeAmplitude;
        private bool initialized;

        public float CameraHalfWidth => runnerCamera == null
            ? 5.4f
            : RunnerPresentationLayout.CalculateCameraHalfWidth(runnerCamera);

        public bool ValidateReferences(out string error)
        {
            if (runnerCamera == null)
            {
                error = "Runner presentation requires a serialized camera.";
                return false;
            }

            if (particleRoot == null)
            {
                error = "Runner presentation requires a serialized particle root.";
                return false;
            }

            if (captureDrone == null)
            {
                error = "Runner presentation requires a serialized capture drone.";
                return false;
            }

            if (!runnerCamera.orthographic)
            {
                error = "Runner presentation camera must be orthographic.";
                return false;
            }

            error = null;
            return true;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (!ValidateReferences(out var error)) throw new InvalidOperationException(error);
            initialized = true;
            cameraOrigin = runnerCamera.transform.localPosition;
        }

        public float GetSpawnX(float entityHalfWidth, float buffer = 0.8f)
        {
            return RunnerPresentationLayout.CalculateSpawnX(CameraHalfWidth, entityHalfWidth, buffer);
        }

        public SpriteRenderer ConfigureAura(Transform entity, Color color, Vector3 entityScale)
        {
            EnsureInitialized();
            var auraTransform = entity.Find("EntityAura");
            var aura = auraTransform == null ? CreateSprite("EntityAura", color) : auraTransform.gameObject;
            aura.transform.SetParent(entity, false);
            aura.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            aura.transform.localRotation = Quaternion.identity;
            aura.transform.localScale = new Vector3(1.45f, 1.45f, 1f);
            var renderer = aura.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = -1;
            renderer.color = new Color(color.r, color.g, color.b, 0.16f);
            // The authored sprites already contain shaped neon glows. A scaled 1x1
            // aura reads as a rectangular placeholder behind detailed pixel art.
            renderer.enabled = false;
            aura.SetActive(true);
            return renderer;
        }

        public void Tick(float delta)
        {
            EnsureInitialized();
            if (delta <= 0f) return;
            TickParticles(delta);
            TickCamera(delta);
        }

        public void SetAlert(float alert, bool captured)
        {
            EnsureInitialized();
            var pressure = Mathf.Clamp01((alert - 35f) / 65f);
            captureDrone.gameObject.SetActive(pressure > 0.01f || captured);
            var target = new Vector3(Mathf.Lerp(CameraHalfWidth + 2f, 2.8f, captured ? 1f : pressure),
                4.8f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.22f, 0f);
            captureDrone.position = Vector3.Lerp(captureDrone.position, target, 1f - Mathf.Exp(-(captured ? 8f : 2.8f) * Time.unscaledDeltaTime));
            var renderer = captureDrone.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                var color = renderer.color;
                color.a = captured ? 1f : Mathf.Lerp(0.25f, 0.9f, pressure);
                renderer.color = color;
            }
        }

        public void Burst(Vector3 position, Color color, bool impact, bool shakeEnabled)
        {
            EnsureInitialized();
            var count = impact ? 14 : 9;
            for (var i = 0; i < count; i++)
            {
                var view = particlePool.Count > 0 ? particlePool.Dequeue() : CreateSprite("JuiceParticle", Color.white);
                view.name = "JuiceParticle";
                view.transform.SetParent(particleRoot, false);
                view.transform.position = position;
                view.transform.localScale = Vector3.one * (impact ? 0.16f : 0.11f);
                var angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.18f, 0.18f);
                var magnitude = Random.Range(1.6f, impact ? 4.8f : 3.2f);
                var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * magnitude + Vector3.left * 0.8f;
                var renderer = view.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = 12;
                renderer.color = color;
                view.SetActive(true);
                particles.Add(new Particle { view = view, velocity = velocity, life = impact ? 0.42f : 0.34f, duration = impact ? 0.42f : 0.34f });
            }

            if (impact)
            {
                shakeTime = 0.18f;
                shakeAmplitude = RunnerPresentationLayout.ResolveShakeAmplitude(shakeEnabled, 0.3f);
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized) Initialize();
        }

        private void TickParticles(float delta)
        {
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.life -= delta;
                particle.view.transform.position += particle.velocity * delta;
                particle.velocity *= Mathf.Exp(-4f * delta);
                var normalized = Mathf.Clamp01(particle.life / particle.duration);
                particle.view.transform.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.14f, normalized);
                var renderer = particle.view.GetComponent<SpriteRenderer>();
                var color = renderer.color;
                color.a = normalized;
                renderer.color = color;
                if (particle.life > 0f) continue;
                particle.view.SetActive(false);
                particlePool.Enqueue(particle.view);
                particles.RemoveAt(i);
            }
        }

        private void TickCamera(float delta)
        {
            if (shakeTime > 0f && shakeAmplitude > 0f)
            {
                shakeTime -= delta;
                runnerCamera.transform.localPosition = cameraOrigin + (Vector3)Random.insideUnitCircle * shakeAmplitude * Mathf.Clamp01(shakeTime / 0.18f);
            }
            else
            {
                shakeTime = 0f;
                runnerCamera.transform.localPosition = Vector3.Lerp(runnerCamera.transform.localPosition, cameraOrigin, 1f - Mathf.Exp(-24f * delta));
            }
        }

        private static Sprite sharedSprite;

        private static GameObject CreateSprite(string name, Color color)
        {
            if (sharedSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "NinjazonPresentationPixel", filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            var go = new GameObject(name, typeof(SpriteRenderer));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = color;
            return go;
        }
    }
}
