using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    [Serializable]
    public sealed class FormalLevelMusicEntry
    {
        [SerializeField] private string levelId = string.Empty;
        [SerializeField] private AudioClip music;

        public string LevelId => levelId ?? string.Empty;
        public AudioClip Music => music;
    }

    [CreateAssetMenu(
        fileName = "FormalAudioCatalog",
        menuName = "Company War/Audio/Audio Catalog",
        order = 40)]
    public sealed class FormalAudioCatalog : ScriptableObject
    {
        public const string ResourcesPath = "CompanyWarRE/Presentation/FormalAudioCatalog";

        [Header("主界面 / 选关")]
        [SerializeField] private AudioClip menuMusic;
        [Header("关卡音乐")]
        [SerializeField] private List<FormalLevelMusicEntry> levelMusic = new List<FormalLevelMusicEntry>();
        [Header("切换")]
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.5f;

        public AudioClip MenuMusic => menuMusic;
        public IReadOnlyList<FormalLevelMusicEntry> LevelMusic => levelMusic;
        public float CrossFadeSeconds => Mathf.Max(0f, crossFadeSeconds);

        public AudioClip FindLevelMusic(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId) || levelMusic == null)
            {
                return null;
            }

            foreach (var entry in levelMusic)
            {
                if (entry != null && string.Equals(
                        entry.LevelId,
                        levelId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Music;
                }
            }

            return null;
        }
    }
}
