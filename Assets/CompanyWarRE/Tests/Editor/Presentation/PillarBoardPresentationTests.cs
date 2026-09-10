using CompanyWarRE.Domain;
using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class PillarBoardPresentationTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PillarBoardPresentationTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Mapper_InsertsHalfWidthGapWithoutChangingLogicalBlockTopology()
        {
            var mapper = CreateMapper(6, 6, 8f);

            Assert.That(mapper.ControlBlockPitch, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(mapper.PillarGap, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(mapper.VisualWidth, Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(
                mapper.GetControlBlockCenterLocalPosition(new GridPosition(2, 1)).x -
                mapper.GetControlBlockCenterLocalPosition(new GridPosition(1, 1)).x,
                Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(
                mapper.GetCellLocalPosition(new GridPosition(4, 1)).x -
                mapper.GetCellLocalPosition(new GridPosition(3, 1)).x,
                Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void Mapper_MapsCellsToPillarTopsAndCanResolveAHitBackToLogic()
        {
            var mapper = CreateMapper(6, 6, 8f);
            var target = new GridPosition(5, 6);
            var localPoint = mapper.GetCellLocalPosition(target);

            Assert.That(localPoint.y, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(
                mapper.TryGetCellAtLocalPoint(
                    localPoint,
                    mapper.GetControlBlockForCell(target),
                    out var resolved),
                Is.True);
            Assert.That(resolved, Is.EqualTo(target));
        }

        [Test]
        public void Mapper_InterpolatesMovingUnitsAcrossTheVisualGap()
        {
            var mapper = CreateMapper(3, 6, 8f);
            var beforeGap = mapper.GetMovingUnitLocalPosition(2, 3d);
            var middleOfGap = mapper.GetMovingUnitLocalPosition(2, 3.5d);
            var afterGap = mapper.GetMovingUnitLocalPosition(2, 4d);

            Assert.That(
                middleOfGap.z,
                Is.EqualTo((beforeGap.z + afterGap.z) * 0.5f).Within(0.0001f));
            Assert.That(middleOfGap.y, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void Board_BuildsOnePillarAndBuildAnchorPerControlBlock()
        {
            var board = _root.AddComponent<FormalBattleBoardView>();
            board.Prepare(6, 6);

            Assert.That(board.PillarGenerator, Is.Not.Null);
            Assert.That(board.PillarGenerator.Pillars.Count, Is.EqualTo(4));
            Assert.That(board.GlobalVisualScale, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(board.CoordinateMapper.PillarWidth, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(board.CoordinateMapper.PillarGap, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(
                board.PillarGenerator.TryGetPillar(new GridPosition(2, 2), out var pillar),
                Is.True);
            Assert.That(pillar.TopAnchor, Is.Not.Null);
            Assert.That(pillar.BuildAnchor, Is.Not.Null);
            Assert.That(
                board.transform.InverseTransformPoint(pillar.BuildAnchor.position).y,
                Is.InRange(104f, 152f));
        }

        [Test]
        public void Board_AcceptsAReplaceablePerBlockHeightRule()
        {
            var board = _root.AddComponent<FormalBattleBoardView>();
            board.Prepare(6, 3, new ColumnHeightProvider());

            board.PillarGenerator.TryGetPillar(new GridPosition(1, 1), out var first);
            board.PillarGenerator.TryGetPillar(new GridPosition(2, 1), out var second);

            Assert.That(first.Height, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(second.Height, Is.EqualTo(28f).Within(0.0001f));
            Assert.That(first.BuildAnchor.position.y, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(second.BuildAnchor.position.y, Is.EqualTo(28f).Within(0.0001f));
        }

        [Test]
        public void SeededRandomHeightRule_IsStableAndStaysInsideConfiguredRange()
        {
            var first = new SeededRandomBattlePillarHeightProvider(26f, 38f, 1977);
            var second = new SeededRandomBattlePillarHeightProvider(26f, 38f, 1977);
            var firstHeight = first.GetHeight(new GridPosition(2, 3));
            var neighbouringHeight = first.GetHeight(new GridPosition(2, 4));

            Assert.That(firstHeight, Is.InRange(26f, 38f));
            Assert.That(neighbouringHeight, Is.InRange(26f, 38f));
            Assert.That(
                second.GetHeight(new GridPosition(2, 3)),
                Is.EqualTo(firstHeight).Within(0.0001f));
            Assert.That(neighbouringHeight, Is.Not.EqualTo(firstHeight).Within(0.0001f));
        }

        [Test]
        public void UniformFlightStep_UsesTheSameWorldDistanceInsideAndBetweenPillars()
        {
            var current = Vector3.zero;
            var insideTarget = new Vector3(0f, 0f, 1f);
            var gapTarget = new Vector3(0f, 6f, 12f);
            var insideStep = BattleSliceCombatantView.AdvanceUniformPosition(
                current,
                insideTarget,
                2f,
                0.1f);
            var gapStep = BattleSliceCombatantView.AdvanceUniformPosition(
                current,
                gapTarget,
                2f,
                0.1f);

            Assert.That(Vector3.Distance(current, insideStep), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(Vector3.Distance(current, gapStep), Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void CombatantVisualScale_UniformlyScalesUnitAndBuildingRoots()
        {
            var combatantObject = new GameObject("ScaledCombatant");
            combatantObject.transform.SetParent(_root.transform, false);
            var view = combatantObject.AddComponent<BattleSliceCombatantView>();

            view.ConfigureVisualScale(4f);

            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one * 4f));
        }

        [Test]
        public void CameraViewDistance_NormalizesCloseAndFullBoardZoom()
        {
            Assert.That(
                BattleSliceCameraRig.CalculateNormalizedViewDistance(5f, 5f, 100f),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                BattleSliceCameraRig.CalculateNormalizedViewDistance(52.5f, 5f, 100f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                BattleSliceCameraRig.CalculateNormalizedViewDistance(180f, 5f, 100f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PillarCloudAbyss_ExtendsBodyDownWithoutMovingBuildAnchor()
        {
            var board = _root.AddComponent<FormalBattleBoardView>();
            board.Prepare(3, 3, new UniformBattlePillarHeightProvider(8f));
            board.PillarGenerator.TryGetPillar(new GridPosition(1, 1), out var pillar);
            var originalAnchor = pillar.BuildAnchor.position;

            board.PillarGenerator.ConfigureCloudAbyss(
                -40f,
                8f,
                -24f,
                new Color(0.56f, 0.66f, 0.72f),
                0.96f,
                1.35f);

            Assert.That(pillar.BottomY, Is.EqualTo(-40f).Within(0.0001f));
            Assert.That(pillar.BuildAnchor.position, Is.EqualTo(originalAnchor));
            Assert.That(pillar.Height, Is.EqualTo(32f).Within(0.0001f));
        }

        [Test]
        public void PillarTopSurface_DoesNotOverlapTheBodyTopFace()
        {
            var board = _root.AddComponent<FormalBattleBoardView>();
            board.Prepare(3, 3, new UniformBattlePillarHeightProvider(8f));
            board.PillarGenerator.TryGetPillar(new GridPosition(1, 1), out var pillar);
            var body = pillar.transform.Find("Body");
            var top = pillar.transform.Find("TopSurface");
            var bodyTopY = body.localPosition.y + body.localScale.y * 0.5f;
            var capBottomY = top.localPosition.y - top.localScale.y * 0.5f;
            var capTopY = top.localPosition.y + top.localScale.y * 0.5f;

            Assert.That(bodyTopY, Is.EqualTo(capBottomY).Within(0.0001f));
            Assert.That(bodyTopY, Is.LessThan(pillar.Height));
            Assert.That(capTopY, Is.EqualTo(pillar.Height).Within(0.0001f));
        }

        [Test]
        public void CloudAbyss_BuildsLayeredHierarchyAndHidesLegacyVisuals()
        {
            var environment = new GameObject("EnvironmentRoot");
            environment.transform.SetParent(_root.transform, false);
            var legacyGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legacyGround.name = "LegacyGround";
            legacyGround.transform.SetParent(environment.transform, false);
            var cloudAbyss = environment.AddComponent<CloudAbyssEnvironmentView>();

            cloudAbyss.Build(environment.transform, _root.transform, 102f, 174f);

            Assert.That(legacyGround.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(
                environment.transform.Find(
                    "CloudAbyssEnvironment/CloudSea/CloudLayer_High"),
                Is.Not.Null);
            Assert.That(
                environment.transform.Find(
                    "CloudAbyssEnvironment/CloudSea/CloudLayer_Main"),
                Is.Not.Null);
            Assert.That(
                environment.transform.Find(
                    "CloudAbyssEnvironment/CloudSea/CloudLayer_Low"),
                Is.Not.Null);
            var cloudRing = environment.transform.Find(
                "CloudAbyssEnvironment/AbyssEnvironment/HorizonCloudRing");
            Assert.That(cloudRing, Is.Not.Null);
            Assert.That(cloudRing.childCount, Is.EqualTo(16));
            Assert.That(
                environment.transform.Find(
                    "CloudAbyssEnvironment/FogController/UnifiedColorGrading"),
                Is.Not.Null);
            Assert.That(
                cloudAbyss.MinimumPillarBottomY,
                Is.LessThanOrEqualTo(cloudAbyss.LowestCloudHeight - 20f));
        }

        private static BattleBoardCoordinateMapper CreateMapper(int columns, int rows, float height)
        {
            return new BattleBoardCoordinateMapper(
                columns,
                rows,
                3f,
                0.5f,
                0f,
                new UniformBattlePillarHeightProvider(height));
        }

        private sealed class ColumnHeightProvider : IBattlePillarHeightProvider
        {
            public float GetHeight(GridPosition controlBlockPosition)
            {
                return 3f + controlBlockPosition.Column * 2f;
            }
        }
    }
}
