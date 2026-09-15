using System;
using System.Collections;
using System.Collections.Generic;
using CompanyWarRE.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Cross-scene audio host. Use <see cref="FormalAudio"/> from gameplay and UI code.
    /// Missing catalog entries and clips are intentionally treated as silence.
    /// </summary>
    public sealed class FormalAudioRuntime : MonoBehaviour
    {
        private const string BootSceneName = "Boot";
        private const string BattleSceneName = "FormalBattle";
        private const string LastLevelKey = "CompanyWar.LastLevel";

        private static FormalAudioRuntime _instance;
        private readonly List<AudioSource> _soundSources = new List<AudioSource>();
        private readonly float[] _musicGains = { 1f, 0f };
        private AudioSource[] _musicSources;
        private int _activeMusicIndex;
        private AudioClip _currentMusic;
        private Coroutine _musicFade;
        private FormalAudioCatalog _catalog;
        private GameSettingsSave _settings;

        public static FormalAudioRuntime Instance => EnsureInstance();
        public FormalAudioCatalog Catalog => _catalog;
        public AudioClip CurrentMusic => _currentMusic;
        public GameSettingsSave Settings => _settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        private static FormalAudioRuntime EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<FormalAudioRuntime>();
            if (_instance != null)
            {
                return _instance;
            }

            var host = new GameObject("FormalAudioRuntime");
            _instance = host.AddComponent<FormalAudioRuntime>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _catalog = Resources.Load<FormalAudioCatalog>(FormalAudioCatalog.ResourcesPath);
            _settings = LoadSettings();
            _musicSources = new[] { CreateSource("Music A"), CreateSource("Music B") };
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            RouteScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void PlayMenuMusic()
        {
            PlayMusic(_catalog != null ? _catalog.MenuMusic : null);
        }

        public void PlayLevelMusic(string levelId)
        {
            PlayMusic(_catalog != null ? _catalog.FindLevelMusic(levelId) : null);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (_musicSources == null)
            {
                return;
            }

            var active = _musicSources[_activeMusicIndex];
            if (_currentMusic == clip && active.clip == clip)
            {
                active.loop = loop;
                if (clip != null && !active.isPlaying)
                {
                    active.Play();
                }
                RefreshMusicVolumes();
                return;
            }

            if (_musicFade != null)
            {
                StopCoroutine(_musicFade);
            }

            var previous = active;
            var nextIndex = 1 - _activeMusicIndex;
            var next = _musicSources[nextIndex];
            next.Stop();
            next.clip = clip;
            next.loop = loop;
            _musicGains[nextIndex] = 0f;
            if (clip != null)
            {
                next.Play();
            }

            _currentMusic = clip;
            _activeMusicIndex = nextIndex;
            var duration = _catalog != null ? _catalog.CrossFadeSeconds : 0f;
            if (duration <= 0f)
            {
                CompleteMusicSwitch(previous, next, nextIndex);
                return;
            }

            _musicFade = StartCoroutine(CrossFade(previous, next, nextIndex, duration));
        }

        public void PlaySfx(AudioClip clip, float pitch = 1f, string layer = "SFX")
        {
            if (clip == null || !IsAudible(layer))
            {
                return;
            }

            var source = FindSoundSource();
            source.clip = clip;
            source.loop = false;
            source.pitch = Mathf.Clamp(pitch, -3f, 3f);
            source.volume = EffectiveVolume(layer);
            source.Play();
        }

        public void ApplySettings(GameSettingsSave settings, bool persist)
        {
            _settings = settings ?? GameSettingsSave.Default;
            RefreshMusicVolumes();
            if (persist)
            {
                PersistSettings();
            }
        }

        public void SetLayer(string layer, float volume, bool muted, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(layer))
            {
                return;
            }

            var normalized = Array.Find(GameSettingsSave.CowAudioLayers,
                item => string.Equals(item, layer.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalized == null)
            {
                return;
            }

            var updated = new List<AudioLayerSaveSettings>();
            foreach (var knownLayer in GameSettingsSave.CowAudioLayers)
            {
                var current = Resolve(knownLayer);
                updated.Add(string.Equals(knownLayer, normalized, StringComparison.OrdinalIgnoreCase)
                    ? new AudioLayerSaveSettings(knownLayer, Mathf.Clamp01(volume), muted)
                    : current);
            }

            ApplySettings(new GameSettingsSave(updated), persist);
        }

        private IEnumerator CrossFade(AudioSource previous, AudioSource next, int nextIndex, float duration)
        {
            var previousIndex = 1 - nextIndex;
            var previousStart = _musicGains[previousIndex];
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                _musicGains[previousIndex] = Mathf.Lerp(previousStart, 0f, progress);
                _musicGains[nextIndex] = progress;
                RefreshMusicVolumes();
                yield return null;
            }

            CompleteMusicSwitch(previous, next, nextIndex);
            _musicFade = null;
        }

        private void CompleteMusicSwitch(AudioSource previous, AudioSource next, int nextIndex)
        {
            previous.Stop();
            previous.clip = null;
            _musicGains[1 - nextIndex] = 0f;
            _musicGains[nextIndex] = 1f;
            next.volume = EffectiveVolume("BGM");
        }

        private void RefreshMusicVolumes()
        {
            if (_musicSources == null)
            {
                return;
            }

            var volume = EffectiveVolume("BGM");
            for (var index = 0; index < _musicSources.Length; index++)
            {
                _musicSources[index].volume = volume * _musicGains[index];
            }
        }

        private AudioSource FindSoundSource()
        {
            foreach (var source in _soundSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            var created = CreateSource("Sound " + (_soundSources.Count + 1));
            _soundSources.Add(created);
            return created;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private bool IsAudible(string layer)
        {
            var master = Resolve("Master");
            var target = Resolve(layer);
            return !master.Muted && !target.Muted && master.Volume > 0d && target.Volume > 0d;
        }

        private float EffectiveVolume(string layer)
        {
            if (!IsAudible(layer))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(Resolve("Master").Volume * Resolve(layer).Volume));
        }

        private AudioLayerSaveSettings Resolve(string layer)
        {
            return _settings?.FindAudioLayer(layer) ?? new AudioLayerSaveSettings(layer, 1d, false);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RouteScene(next);
        }

        private void RouteScene(Scene scene)
        {
            if (string.Equals(scene.name, BootSceneName, StringComparison.OrdinalIgnoreCase))
            {
                PlayMenuMusic();
            }
            else if (string.Equals(scene.name, BattleSceneName, StringComparison.OrdinalIgnoreCase))
            {
                PlayLevelMusic(PlayerPrefs.GetString(LastLevelKey, string.Empty));
            }
        }

        private static GameSettingsSave LoadSettings()
        {
            var layers = new List<AudioLayerSaveSettings>();
            foreach (var layer in GameSettingsSave.CowAudioLayers)
            {
                layers.Add(new AudioLayerSaveSettings(
                    layer,
                    Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey(layer), 1f)),
                    PlayerPrefs.GetInt(MuteKey(layer), 0) == 1));
            }

            return new GameSettingsSave(layers);
        }

        private void PersistSettings()
        {
            foreach (var layer in GameSettingsSave.CowAudioLayers)
            {
                var setting = Resolve(layer);
                PlayerPrefs.SetFloat(VolumeKey(layer), (float)setting.Volume);
                PlayerPrefs.SetInt(MuteKey(layer), setting.Muted ? 1 : 0);
            }
            PlayerPrefs.Save();
        }

        private static string VolumeKey(string layer) => "Audio_" + layer + "_Volume";
        private static string MuteKey(string layer) => "Audio_" + layer + "_Mute";
    }

    /// <summary>Stable public entry point for music, sound effects and persisted volume settings.</summary>
    public static class FormalAudio
    {
        public static float BgmVolume => (float)FormalAudioRuntime.Instance.Settings.FindAudioLayer("BGM").Volume;
        public static float SfxVolume => (float)FormalAudioRuntime.Instance.Settings.FindAudioLayer("SFX").Volume;
        public static bool BgmMuted => FormalAudioRuntime.Instance.Settings.FindAudioLayer("BGM").Muted;
        public static bool SfxMuted => FormalAudioRuntime.Instance.Settings.FindAudioLayer("SFX").Muted;

        public static void PlayMenuMusic() => FormalAudioRuntime.Instance.PlayMenuMusic();
        public static void PlayLevelMusic(string levelId) => FormalAudioRuntime.Instance.PlayLevelMusic(levelId);
        public static void PlayMusic(AudioClip clip, bool loop = true) => FormalAudioRuntime.Instance.PlayMusic(clip, loop);
        public static void PlaySfx(AudioClip clip, float pitch = 1f) =>
            FormalAudioRuntime.Instance.PlaySfx(clip, pitch);
        public static void PlaySFX(AudioClip clip, float pitch = 1f) => PlaySfx(clip, pitch);

        public static void SetBgmVolume(float volume) =>
            FormalAudioRuntime.Instance.SetLayer("BGM", volume, BgmMuted);
        public static void SetSfxVolume(float volume) =>
            FormalAudioRuntime.Instance.SetLayer("SFX", volume, SfxMuted);
        public static void SetBgmMuted(bool muted) =>
            FormalAudioRuntime.Instance.SetLayer("BGM", BgmVolume, muted);
        public static void SetSfxMuted(bool muted) =>
            FormalAudioRuntime.Instance.SetLayer("SFX", SfxVolume, muted);

        internal static void Apply(GameSettingsSave settings, bool persist = false) =>
            FormalAudioRuntime.Instance.ApplySettings(settings, persist);
    }
}
