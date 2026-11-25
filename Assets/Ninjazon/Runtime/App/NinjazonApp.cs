using System;
using System.Collections.Generic;
using System.IO;
using Ninjazon.Runtime.Persistence;
using UnityEngine;

namespace Ninjazon.Runtime.App
{
    public sealed class NinjazonApp : MonoBehaviour
    {
        public static NinjazonApp Instance { get; private set; }

        public SaveDataV1 Data { get; private set; }
        public AudioService Audio { get; private set; }
        private SaveRepository repository;

        public static NinjazonApp Ensure()
        {
            if (Instance != null) return Instance;
            var root = new GameObject("NinjazonApp");
            return root.AddComponent<NinjazonApp>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_ANDROID
            Screen.orientation = ScreenOrientation.Portrait;
#endif
            Application.targetFrameRate = 60;
            repository = new SaveRepository(Path.Combine(Application.persistentDataPath, "ninjazon-save-v1.json"));
            Data = repository.Load();
            gameObject.AddComponent<AudioListener>();
            Audio = gameObject.AddComponent<AudioService>();
            Audio.Apply(Data.musicVolume, Data.sfxVolume);
        }

        public void Commit()
        {
            repository.Save(Data);
            Audio.Apply(Data.musicVolume, Data.sfxVolume);
        }

        public void RecordRun(int score, float distance, int credits)
        {
            Data.bestScore = Mathf.Max(Data.bestScore, score);
            Data.bestDistance = Mathf.Max(Data.bestDistance, distance);
            Data.lifetimeCredits += Mathf.Max(0, credits);
            Commit();
        }

        public void ResetTutorial()
        {
            Data.tutorialComplete = false;
            Commit();
        }
    }

    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource music;
        private AudioSource sfx;
        private readonly Dictionary<int, AudioClip> toneCache = new();
        private float musicVolume = 0.7f;
        private float sfxVolume = 0.8f;
        private AudioClip collectionClip;
        private AudioClip hitClip;

        private void Awake()
        {
            var backgroundMusic = Resources.Load<AudioClip>("Audio/BackgroundMusic");
            collectionClip = Resources.Load<AudioClip>("Audio/CollectionSfx");
            hitClip = Resources.Load<AudioClip>("Audio/HitSfx");
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.clip = backgroundMusic != null
                ? backgroundMusic
                : MakeTone("Neon Pulse", 110f, 4f, 0.035f, true);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            music.Play();
        }

        public void Apply(float musicLevel, float sfxLevel)
        {
            musicVolume = Mathf.Clamp01(musicLevel);
            sfxVolume = Mathf.Clamp01(sfxLevel);
            if (music != null) music.volume = musicVolume;
            if (sfx != null) sfx.volume = sfxVolume;
        }

        public void SetAlert(float alert)
        {
            if (music == null) return;
            music.pitch = Mathf.Lerp(0.95f, 1.18f, Mathf.Clamp01(alert / 100f));
        }

        public void SetPaused(bool paused)
        {
            if (music == null) return;
            music.volume = paused ? musicVolume * 0.3f : musicVolume;
        }

        public void PlayUi() => PlayTone(520f, 0.07f);
        public void PlayJump() => PlayTone(720f, 0.09f);
        public void PlayPickup() => PlayClipOrTone(collectionClip, 960f, 0.08f);
        public void PlayHit() => PlayClipOrTone(hitClip, 180f, 0.16f);
        public void PlayAlert() => PlayHit();

        private void PlayClipOrTone(AudioClip clip, float fallbackFrequency, float fallbackDuration)
        {
            if (sfx == null || sfxVolume <= 0f) return;
            if (clip != null) sfx.PlayOneShot(clip, sfxVolume);
            else PlayTone(fallbackFrequency, fallbackDuration);
        }

        private void PlayTone(float frequency, float duration)
        {
            if (sfx == null || sfxVolume <= 0f) return;
            var key = (Mathf.RoundToInt(frequency) * 397) ^ Mathf.RoundToInt(duration * 1000f);
            if (!toneCache.TryGetValue(key, out var clip))
            {
                clip = MakeTone($"SFX {frequency:0}", frequency, duration, 0.18f, false);
                toneCache.Add(key, clip);
            }
            sfx.PlayOneShot(clip, sfxVolume);
        }

        private static AudioClip MakeTone(string name, float frequency, float seconds, float amplitude, bool pulse)
        {
            const int rate = 22050;
            var samples = Mathf.Max(1, Mathf.RoundToInt(rate * seconds));
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)rate;
                var envelope = pulse ? 0.45f + 0.55f * Mathf.Sin(t * Mathf.PI * 2f * 2f) : Mathf.Min(1f, (samples - i) / (rate * 0.02f));
                data[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
