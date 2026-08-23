using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class MigrationBaselineTests
    {
        private const string RequiredEditorVersion = "2022.3.62f3";

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve the Unity project root.");

        [Test]
        public void EditorVersion_IsLockedToApprovedVersion()
        {
            Assert.That(Application.unityVersion, Is.EqualTo(RequiredEditorVersion));

            var versionFile = File.ReadAllText(Path.Combine(ProjectRoot, "ProjectSettings", "ProjectVersion.txt"));
            StringAssert.Contains($"m_EditorVersion: {RequiredEditorVersion}", versionFile);
        }

        [Test]
        public void AssetSerialization_IsForceText()
        {
            var editorSettings = File.ReadAllText(Path.Combine(ProjectRoot, "ProjectSettings", "EditorSettings.asset"));
            StringAssert.Contains("m_SerializationMode: 2", editorSettings);
        }

        [Test]
        public void MetaFiles_AreVisibleAndNotHidden()
        {
            Assert.That(UnityEditor.VersionControlSettings.mode, Is.Not.EqualTo("Hidden Meta Files"));
        }

        [Test]
        public void FirstBatch_DoesNotContainImportedCowAssetRoots()
        {
            var forbiddenRoots = new[]
            {
                Path.Combine(Application.dataPath, "_Game"),
                Path.Combine(Application.dataPath, "_YFanFramework"),
                Path.Combine(Application.dataPath, "AddressableAssetsData")
            };

            Assert.That(forbiddenRoots.Where(Directory.Exists), Is.Empty,
                "First batch must not import Cow scenes, resources, or production scripts.");
        }
    }
}
