using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class FormalAudioCatalogTests
    {
        [Test]
        public void ProjectCatalog_IsAvailableAtTheRuntimeResourcesPath()
        {
            var catalog = Resources.Load<FormalAudioCatalog>(FormalAudioCatalog.ResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.LevelMusic.Count, Is.EqualTo(4));
            Assert.That(catalog.LevelMusic[0].LevelId, Is.EqualTo("L01"));
            Assert.That(catalog.LevelMusic[3].LevelId, Is.EqualTo("L04"));
        }

        [Test]
        public void EmptyCatalog_TreatsMissingMusicAsSilence()
        {
            var catalog = ScriptableObject.CreateInstance<FormalAudioCatalog>();
            try
            {
                Assert.That(catalog.MenuMusic, Is.Null);
                Assert.That(catalog.FindLevelMusic("L01"), Is.Null);
                Assert.That(catalog.FindLevelMusic(null), Is.Null);
                Assert.That(catalog.CrossFadeSeconds, Is.GreaterThanOrEqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
