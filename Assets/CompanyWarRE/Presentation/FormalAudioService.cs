using System;
using CompanyWarRE.Domain;
using QFramework;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class FormalAudioService
    {
        private GameSettingsSave _settings = GameSettingsSave.Default;

        public GameSettingsSave Settings => _settings;

        public void Apply(GameSettingsSave settings)
        {
            _settings = settings ?? GameSettingsSave.Default;
            var master = Resolve("Master");
            var music = Resolve("BGM");
            var voice = Resolve("Voice");
            var sfx = Resolve("SFX");
            var ui = Resolve("UI");

            AudioKit.Settings.MusicVolume.Value = EffectiveVolume(master, music);
            AudioKit.Settings.VoiceVolume.Value = EffectiveVolume(master, voice);
            AudioKit.Settings.SoundVolume.Value = master.Muted ? 0f : (float)master.Volume;
            AudioKit.Settings.IsMusicOn.Value = !master.Muted && !music.Muted;
            AudioKit.Settings.IsVoiceOn.Value = !master.Muted && !voice.Muted;
            AudioKit.Settings.IsSoundOn.Value = !master.Muted && (!sfx.Muted || !ui.Muted);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip != null && IsAudible("BGM"))
            {
                AudioKit.PlayMusic(clip, loop);
            }
        }

        public void PlayVoice(AudioClip clip)
        {
            if (clip != null && IsAudible("Voice"))
            {
                AudioKit.PlayVoice(clip);
            }
        }

        public void PlaySound(AudioClip clip, string layer = "SFX", float pitch = 1f)
        {
            if (clip == null || !IsAudible(layer))
            {
                return;
            }

            var setting = Resolve(layer);
            AudioKit.PlaySound(
                clip,
                volume: Mathf.Clamp01((float)setting.Volume),
                pitch: pitch,
                playSoundMode: AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames);
        }

        private bool IsAudible(string layer)
        {
            var master = Resolve("Master");
            var target = Resolve(layer);
            return !master.Muted && !target.Muted && master.Volume > 0d && target.Volume > 0d;
        }

        private AudioLayerSaveSettings Resolve(string layer)
        {
            return _settings.FindAudioLayer(layer) ?? new AudioLayerSaveSettings(layer, 1d, false);
        }

        private static float EffectiveVolume(AudioLayerSaveSettings master, AudioLayerSaveSettings layer)
        {
            if (master.Muted || layer.Muted)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(master.Volume * layer.Volume));
        }
    }
}
