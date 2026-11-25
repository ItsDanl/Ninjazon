using System.Collections.Generic;
using UnityEngine;

namespace Ninjazon.Game
{
    /// <summary>
    /// Music + sound effects. SFX are short WAVs from Resources/NZ/Audio.
    /// A low-pass filter muffles the music in menus so the run feels "louder".
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new();
        private readonly List<AudioSource> voices = new();
        private AudioSource music;
        private AudioLowPassFilter musicFilter;
        private float musicVolume = 0.7f;
        private float sfxVolume = 0.85f;
        private float targetCutoff = 22000f;
        private float duck = 1f;
        private int nextVoice;

        public void Init(float musicLevel, float sfxLevel)
        {
            // Unity filters affect the AudioSource on the same GameObject, so the
            // music gets its own child object; SFX stay unfiltered.
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform, false);
            music = musicGo.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.clip = Resources.Load<AudioClip>("Audio/BackgroundMusic");
            musicFilter = musicGo.AddComponent<AudioLowPassFilter>();
            musicFilter.cutoffFrequency = 1400f;
            var sfxGo = new GameObject("Sfx");
            sfxGo.transform.SetParent(transform, false);
            for (var i = 0; i < 12; i++)
            {
                var v = sfxGo.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = 0f;
                voices.Add(v);
            }
            SetVolumes(musicLevel, sfxLevel);
            if (music.clip != null) music.Play();
        }

        public void SetVolumes(float musicLevel, float sfxLevel)
        {
            musicVolume = Mathf.Clamp01(musicLevel);
            sfxVolume = Mathf.Clamp01(sfxLevel);
        }

        /// <summary>true = muffled (menus/pause), false = full bandwidth (running).</summary>
        public void SetMuffled(bool muffled) => targetCutoff = muffled ? 1400f : 22000f;

        /// <summary>Briefly lowers the music under big moments (death, new best).</summary>
        public void Duck(float amount) => duck = Mathf.Min(duck, 1f - Mathf.Clamp01(amount));

        public void SetMusicPitch(float pitch)
        {
            if (music != null) music.pitch = pitch;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            duck = Mathf.MoveTowards(duck, 1f, dt * 0.6f);
            if (music != null) music.volume = musicVolume * duck * 0.8f;
            if (musicFilter != null)
            {
                // Lerp in log space so the sweep sounds even.
                var current = Mathf.Log(musicFilter.cutoffFrequency);
                var target = Mathf.Log(targetCutoff);
                musicFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(current, target, 1f - Mathf.Exp(-5f * dt)));
            }
        }

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (sfxVolume <= 0.001f) return;
            if (!clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("NZ/Audio/" + name);
                clips[name] = clip;
            }
            if (clip == null) return;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            v.pitch = pitch;
            v.PlayOneShot(clip, volume * sfxVolume);
        }

        /// <summary>Small random pitch change so repeated sounds don't feel robotic.</summary>
        public void PlayVaried(string name, float volume = 1f, float spread = 0.06f)
            => Play(name, volume, 1f + Random.Range(-spread, spread));
    }
}
