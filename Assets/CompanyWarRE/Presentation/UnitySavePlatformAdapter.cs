using System;
using System.Collections.Generic;
using System.IO;
using CompanyWarRE.Application;
using CompanyWarRE.Infrastructure.Saves;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class UnityPlayerPrefsReader : ILegacyPreferenceReader
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
    }

    public sealed class UnitySavePathSet
    {
        public UnitySavePathSet(string persistentRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentRoot))
            {
                throw new ArgumentException("A platform persistence root is required.", nameof(persistentRoot));
            }

            var root = Path.GetFullPath(persistentRoot);
            SavePath = Path.Combine(root, "CompanyWarRE", "save.json");
            BackupDirectory = Path.Combine(root, "CompanyWarRE", "Backups");
        }

        public string SavePath { get; }
        public string BackupDirectory { get; }

        public static UnitySavePathSet ForCurrentPlatform()
        {
            return new UnitySavePathSet(UnityEngine.Application.persistentDataPath);
        }
    }

    public static class UnitySaveCompatibilityFactory
    {
        public static SaveGameUseCases CreateForCurrentPlayerPrefs(IEnumerable<string> targetLevelIds)
        {
            return new SaveGameUseCases(
                new DataContractSaveDocumentCodec(),
                new AtomicLocalSaveDocumentStore(),
                new CowLegacyPreferenceSaveSource(new UnityPlayerPrefsReader(), targetLevelIds));
        }

        public static SaveGameUseCases CreateForLegacySnapshot(
            CowLegacyPreferenceSnapshotDto snapshot,
            IEnumerable<string> targetLevelIds)
        {
            return new SaveGameUseCases(
                new DataContractSaveDocumentCodec(),
                new AtomicLocalSaveDocumentStore(),
                new CowLegacyPreferenceSaveSource(
                    new CowLegacySnapshotPreferenceReader(snapshot),
                    targetLevelIds));
        }
    }
}
