using System.Collections.Generic;
using CompanyWarRE.Domain;
using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class BattleIntroSequenceTests
    {
        [TestCase(18, 30)]
        [TestCase(15, 27)]
        [TestCase(3, 9)]
        public void Tunnel_StaysBelowEveryTopAndOutsidePillarFootprints(int columns, int rows)
        {
            var settings = ScriptableObject.CreateInstance<BattleIntroPathSettings>();
            try
            {
                var board = new BattleBoardCoordinateMapper(columns, rows, 12f, 0.5f, 0f,
                    new SeededRandomBattlePillarHeightProvider(126f, 130f, 1977));
                var sequence = new BattleIntroSequence(board, Matrix4x4.identity, Quaternion.identity, new Vector3(0, 128, 60));
                sequence.GetTunnel(settings, out var start, out var end);
                for (var sample = 0; sample <= 100; sample++)
                {
                    var point = Vector3.Lerp(start, end, sample / 100f);
                    Assert.That(point.y, Is.LessThan(board.MinimumPillarTopY));
                    for (var x = 1; x <= board.ControlBlockColumns; x++)
                        for (var z = 1; z <= board.ControlBlockRows; z++)
                        {
                            var center = board.GetControlBlockCenterLocalPosition(new GridPosition(x, z));
                            Assert.That(Mathf.Abs(point.x - center.x) > board.PillarWidth * 0.5f + 0.1f ||
                                        Mathf.Abs(point.z - center.z) > board.PillarWidth * 0.5f + 0.1f, Is.True);
                        }
                }
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Shots_CutToEnemyRearThenJoinOrbitAndReturnExactlyHome()
        {
            var settings = ScriptableObject.CreateInstance<BattleIntroPathSettings>();
            try
            {
                var board = new BattleBoardCoordinateMapper(18, 30, 12f, 0.5f, 0f, new UniformBattlePillarHeightProvider(128f));
                var matrix = Matrix4x4.TRS(new Vector3(30, 5, 10), Quaternion.Euler(0, 35, 0), Vector3.one * 2);
                var sequence = new BattleIntroSequence(board, matrix, Quaternion.Euler(0, 35, 0), new Vector3(0,128,60));
                var home = matrix.MultiplyPoint3x4(new Vector3(70, 260, -150));
                var rotation = Quaternion.Euler(48,-32,0);
                var center = matrix.MultiplyPoint3x4(new Vector3(0,128,0));
                sequence.Evaluate(settings, home, rotation, center, 6f/11.5f, out var rear, out var aim, out _, out var perspective, out var shot);
                Assert.That(shot, Is.EqualTo(2));
                Assert.That(perspective, Is.False);
                Assert.That(matrix.inverse.MultiplyPoint3x4(rear).z, Is.GreaterThan(board.VisualLength*0.5f));
                Assert.That(matrix.inverse.MultiplyVector(aim * Vector3.forward).z, Is.LessThan(0f));
                sequence.Evaluate(settings, home, rotation, center, 8f/11.5f, out var orbit, out _, out _, out _, out shot);
                Assert.That(shot, Is.EqualTo(3));
                Assert.That(Vector3.Distance(rear, orbit), Is.LessThan(0.01f));
                sequence.Evaluate(settings, home, rotation, center, 1f, out var finish, out var finalRotation, out var size, out _, out _);
                Assert.That(Vector3.Distance(finish, home), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(finalRotation, rotation), Is.LessThan(0.01f));
                Assert.That(size, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void CloudFlight_DetoursAroundBuildingInsteadOfPassingThrough()
        {
            var obstacle = new Bounds(new Vector3(0, 5, 0), new Vector3(6, 20, 6));
            var path = BattleIntroSequence.PlanFlight(new Vector3(-10,5,0), new Vector3(10,8,0), new List<Bounds>{obstacle});
            Assert.That(path.Count, Is.GreaterThan(2));
            for (var segment=1; segment<path.Count; segment++)
                for (var sample=0; sample<=50; sample++)
                    Assert.That(obstacle.Contains(Vector3.Lerp(path[segment-1],path[segment],sample/50f)), Is.False);
        }
    }
}
