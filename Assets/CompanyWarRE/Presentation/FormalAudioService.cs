using System;
using CompanyWarRE.Domain;
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
            FormalAudio.Apply(_settings);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            FormalAudio.PlayMusic(clip, loop);
        }

        public void PlayVoice(AudioClip clip)
        {
            FormalAudioRuntime.Instance.PlaySfx(clip, 1f, "Voice");
        }

        public void PlaySound(AudioClip clip, string layer = "SFX", float pitch = 1f)
        {
            if (clip == null || !IsAudible(layer))
            {
                return;
            }

            FormalAudioRuntime.Instance.PlaySfx(clip, pitch, layer);
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

    }
}
