using CompanyWarRE.Domain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class SkyBattlefieldPresentationTests
    {
        [Test]
        public void AuthoredPillars_KeepModelDeckAndBuildAnchorAlignedAfterCloudExtension()
        {
            var root = new GameObject("SkyPillarTest");
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx");
                Assert.That(model, Is.Not.Null);
                var board = root.AddComponent<FormalBattleBoardView>();
                board.ConfigureSkyPillars(model, 3f, 0.5f, 29f, 35f, 1977, 4f);
                var randomState = Random.state;
                board.Prepare(6, 6);
                foreach (var pair in board.PillarGenerator.Pillars)
                {
                    var pillar = pair.Value;
                    var authored = pillar.transform.Find("SkyPillarModel");
                    Assert.That(authored, Is.Not.Null);
                    var bounds = authored.GetComponentInChildren<Renderer>().bounds;
                    Assert.That(bounds.size.y, Is.GreaterThan(bounds.size.x * 3f));
                    Assert.That(bounds.size.y, Is.GreaterThan(bounds.size.z * 3f));
                    var expected = board.CoordinateMapper.GetControlBlockTopCenterLocalPosition(pair.Key);
                    Assert.That(Vector3.Distance(pillar.BuildAnchor.position, expected), Is.LessThan(0.001f));
                    pillar.ExtendBodyToBottom(-46f);
                    Assert.That(authored.TransformPoint(new Vector3(0f, 16f, 0f)).y,
                        Is.EqualTo(expected.y).Within(0.001f));
                    Assert.That(pillar.BuildAnchor.position.y, Is.EqualTo(expected.y).Within(0.001f));
                    Assert.That(pillar.transform.Find("Body").GetComponent<Collider>().enabled, Is.True);
                    Assert.That(pillar.transform.Find("Body").GetComponent<Renderer>().enabled, Is.False);
                }
                Assert.That(Random.state, Is.EqualTo(randomState));
                var firstHeight = board.CoordinateMapper.GetPillarHeight(new GridPosition(1, 1));
                board.Prepare(3, 6);
                Assert.That(board.PillarRoot.childCount, Is.EqualTo(2), "Rebuild must remove old pillars.");
                Assert.That(board.CoordinateMapper.GetPillarHeight(new GridPosition(1, 1)), Is.EqualTo(firstHeight));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapSettings_OverridePresentationButPreserveLevelDimensions()
        {
            var root = new GameObject("SkyMapSettingsTest");
            try
            {
                var settings = root.AddComponent<SkyBattlefieldSettings>();
                settings.Configure(null, 4f, 0.25f, 18f, 22f, 7, 2f);
                var board = root.AddComponent<FormalBattleBoardView>();
                settings.ApplyTo(board);
                board.Prepare(7, 13);
                Assert.That(board.CoordinateMapper.Columns, Is.EqualTo(7));
                Assert.That(board.CoordinateMapper.Rows, Is.EqualTo(13));
                Assert.That(board.PillarGenerator.Pillars.Count, Is.EqualTo(15));
                Assert.That(board.CoordinateMapper.PillarWidth, Is.EqualTo(8f));
                Assert.That(board.CoordinateMapper.MinimumPillarTopY, Is.GreaterThanOrEqualTo(36f));
                Assert.That(board.CoordinateMapper.MaximumPillarTopY, Is.LessThanOrEqualTo(44f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
