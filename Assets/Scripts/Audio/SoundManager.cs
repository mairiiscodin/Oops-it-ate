using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Audio
{
    public enum Sfx
    {
        Step,
        Bump,
        Pickup,
        Eat,
        Grow,
        Push,
        Burp,
        BorderPush,
        Reveal,
        DoorEnter,
        Complete,
        Reset,
        Nope
    }

    /// <summary>
    /// Plays sound effects and background music across scenes.
    /// Custom clips override the generated placeholders:
    ///   Resources/Audio/Sfx/&lt;Sfx name&gt;  (e.g. Resources/Audio/Sfx/Eat.wav)
    ///   Resources/Audio/Music                (looping background music)
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        private const string SfxResourceFolder = "Audio/Sfx/";
        private const string MusicResource = "Audio/Music";
        private const string SfxVolumeKey = "OopsItAte.SfxVolume";
        private const string MusicVolumeKey = "OopsItAte.MusicVolume";
        private const int VoiceCount = 10;

        private struct SfxSettings
        {
            public float Volume;
            public float PitchJitter;
            public float MinInterval;

            public SfxSettings(float volume, float pitchJitter, float minInterval = 0.03f)
            {
                Volume = volume;
                PitchJitter = pitchJitter;
                MinInterval = minInterval;
            }
        }

        // Per-sound mix. Pitch jitter keeps repeated sounds (steps, chomps) from sounding robotic.
        private static readonly Dictionary<Sfx, SfxSettings> mix = new Dictionary<Sfx, SfxSettings>
        {
            [Sfx.Step] = new SfxSettings(0.45f, 0.12f, 0.05f),
            [Sfx.Bump] = new SfxSettings(0.55f, 0.08f, 0.1f),
            [Sfx.Pickup] = new SfxSettings(0.7f, 0.03f),
            [Sfx.Eat] = new SfxSettings(0.9f, 0.08f),
            [Sfx.Grow] = new SfxSettings(0.9f, 0.04f),
            [Sfx.Push] = new SfxSettings(0.7f, 0.1f, 0.06f),
            [Sfx.Burp] = new SfxSettings(0.9f, 0.1f, 0.15f),
            [Sfx.BorderPush] = new SfxSettings(0.9f, 0.05f),
            [Sfx.Reveal] = new SfxSettings(0.8f, 0f),
            [Sfx.DoorEnter] = new SfxSettings(0.7f, 0.05f),
            [Sfx.Complete] = new SfxSettings(0.85f, 0f),
            [Sfx.Reset] = new SfxSettings(0.6f, 0f),
            [Sfx.Nope] = new SfxSettings(0.5f, 0.03f, 0.15f),
        };

        private static SoundManager instance;

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<Sfx, float> lastPlayTime = new Dictionary<Sfx, float>();
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicSource;
        private AudioListener fallbackListener;
        private float sfxVolume = 1f;
        private float musicVolume = 0.6f;

        public static float SfxVolume
        {
            get => Instance.sfxVolume;
            set
            {
                Instance.sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxVolumeKey, Instance.sfxVolume);
            }
        }

        public static float MusicVolume
        {
            get => Instance.musicVolume;
            set
            {
                Instance.musicVolume = Mathf.Clamp01(value);
                Instance.musicSource.volume = Instance.musicVolume;
                PlayerPrefs.SetFloat(MusicVolumeKey, Instance.musicVolume);
            }
        }

        private static SoundManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var root = new GameObject("Sound Manager");
                    DontDestroyOnLoad(root);
                    instance = root.AddComponent<SoundManager>();
                    instance.Build();
                }

                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnGameStart()
        {
            _ = Instance;
        }

        /// <param name="pitch">Base pitch; random jitter from the mix table is applied on top.</param>
        /// <param name="delay">Seconds of real time to wait, e.g. to let a chomp finish before a pop.</param>
        public static void Play(Sfx sfx, float pitch = 1f, float volume = 1f, float delay = 0f)
        {
            if (delay > 0f)
            {
                Instance.StartCoroutine(Instance.PlayDelayed(sfx, pitch, volume, delay));
                return;
            }

            Instance.PlayInternal(sfx, pitch, volume);
        }

        private IEnumerator PlayDelayed(Sfx sfx, float pitch, float volume, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayInternal(sfx, pitch, volume);
        }

        private void Build()
        {
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);

            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = CreateSource();
            }

            musicSource = CreateSource();
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            AudioClip music = Resources.Load<AudioClip>(MusicResource);
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }

            fallbackListener = gameObject.AddComponent<AudioListener>();
            RefreshFallbackListener();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshFallbackListener();
        }

        /// <summary>Menus have no camera listener; keep exactly one listener active.</summary>
        private void RefreshFallbackListener()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>();
            bool hasSceneListener = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != fallbackListener && listeners[i].enabled)
                {
                    hasSceneListener = true;
                    break;
                }
            }

            fallbackListener.enabled = !hasSceneListener;
        }

        private AudioSource CreateSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            // Keeps playing while the completion animation pauses gameplay with timeScale = 0.
            source.ignoreListenerPause = true;
            return source;
        }

        private void PlayInternal(Sfx sfx, float pitch, float volume)
        {
            SfxSettings settings = mix.TryGetValue(sfx, out SfxSettings found)
                ? found
                : new SfxSettings(1f, 0f);

            float now = Time.unscaledTime;
            if (lastPlayTime.TryGetValue(sfx, out float last) && now - last < settings.MinInterval)
            {
                return;
            }

            lastPlayTime[sfx] = now;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch * (1f + Random.Range(-settings.PitchJitter, settings.PitchJitter));
            voice.PlayOneShot(GetClip(sfx), settings.Volume * volume * sfxVolume);
        }

        private AudioClip GetClip(Sfx sfx)
        {
            if (clips.TryGetValue(sfx, out AudioClip clip) && clip != null)
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(SfxResourceFolder + sfx);
            if (clip == null)
            {
                clip = ProceduralSfx.Create(sfx);
            }

            clips[sfx] = clip;
            return clip;
        }
    }
}
