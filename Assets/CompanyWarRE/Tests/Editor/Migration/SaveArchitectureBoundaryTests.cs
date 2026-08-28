using System.IO;
using NUnit.Framework;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class SaveArchitectureBoundaryTests
    {
        [Test]
        public void SaveDomain_RemainsPureCSharpAndEngineIndependent()
        {
            var source = File.ReadAllText("Assets/CompanyWarRE/Domain/SaveGame.cs");
            var assembly = File.ReadAllText("Assets/CompanyWarRE/Domain/CompanyWarRE.Domain.asmdef");

            StringAssert.DoesNotContain("UnityEngine", source);
            StringAssert.DoesNotContain("QFramework", source);
            StringAssert.DoesNotContain("PlayerPrefs", source);
            StringAssert.DoesNotContain("System.IO", source);
            StringAssert.Contains("\"noEngineReferences\": true", assembly);
            StringAssert.Contains("\"references\": []", assembly);
        }

        [Test]
        public void SaveStorage_DoesNotBindToResKitOrStreamingAssets()
        {
            var application = File.ReadAllText("Assets/CompanyWarRE/Application/SaveGameApplication.cs");
            var infrastructure = File.ReadAllText("Assets/CompanyWarRE/Infrastructure/SaveCompatibility.cs");

            StringAssert.DoesNotContain("UnityEngine", application);
            StringAssert.DoesNotContain("PlayerPrefs", application);
            StringAssert.DoesNotContain("StreamingAssets", application);
            StringAssert.DoesNotContain("ResKit", application);
            StringAssert.DoesNotContain("UnityEngine", infrastructure);
            StringAssert.DoesNotContain("PlayerPrefs", infrastructure);
            StringAssert.DoesNotContain("StreamingAssets", infrastructure);
            StringAssert.DoesNotContain("ResKit", infrastructure);
            StringAssert.Contains("ISaveDocumentStore", application);
            StringAssert.Contains("RequireAbsolute", infrastructure);
        }

        [Test]
        public void CowAudit_LabelsFrameworkSaveUtilAsUnusedRatherThanLegacyFormat()
        {
            var evidence = File.ReadAllText("Migration/Inventory/CowSavePersistenceEvidence.csv");
            var mapping = File.ReadAllText("Migration/Inventory/CowSaveFieldMapping.csv");

            StringAssert.Contains("CompanyWar.LastLevel", evidence);
            StringAssert.Contains("CompanyWar.LevelStars.{LevelId}", evidence);
            StringAssert.Contains("Not a real Cow business save format", evidence);
            StringAssert.Contains("Target-new; not legacy compatibility", mapping);
        }
    }
}
