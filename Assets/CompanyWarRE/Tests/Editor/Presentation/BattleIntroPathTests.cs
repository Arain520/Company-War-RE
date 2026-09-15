using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class BattleIntroPathTests
    {
        [Test]
        public void EditedPath_AlwaysReturnsExactlyToGameplayCamera()
        {
            var settings = ScriptableObject.CreateInstance<BattleIntroPathSettings>();
            try
            {
                settings.startOffset = new Vector3(0.5f, 0.2f, 0.1f);
                settings.lookAtOffset = Vector3.right;
                settings.motion = AnimationCurve.Linear(0f, 0.3f, 1f, 0.8f);
                var home = new Vector3(10f, 150f, -50f);
                var rotation = Quaternion.Euler(48f, -32f, 0f);
                settings.Evaluate(home, rotation, new Vector3(0f, 128f, 0f), 1f,
                    out var position, out var orientation, out var size);
                Assert.That(Vector3.Distance(position, home), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(orientation, rotation), Is.LessThan(0.01f));
                Assert.That(size, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void ControlPoints_ScaleWithMapAndPreserveAuthoredOffset()
        {
            var settings = ScriptableObject.CreateInstance<BattleIntroPathSettings>();
            try
            {
                var home = new Vector3(0f, 40f, -40f);
                var rotation = Quaternion.Euler(45f, 0f, 0f);
                settings.GetControlPoints(home, rotation, Vector3.zero, out var original, out _, out _, out _);
                settings.startOffset = new Vector3(0.2f, 0f, 0f);
                settings.GetControlPoints(home, rotation, Vector3.zero, out var moved, out var a, out var b, out _);
                Assert.That(Vector3.Distance(moved - original, rotation * settings.startOffset * home.magnitude), Is.LessThan(0.001f));
                settings.GetControlPoints(home * 2f, rotation, Vector3.zero, out var doubled, out var aa, out var bb, out _);
                Assert.That(Vector3.Distance(doubled, moved * 2f), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(aa, a * 2f), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(bb, b * 2f), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void ResourcePath_IsAvailableToRuntime()
        {
            Assert.That(Resources.Load<BattleIntroPathSettings>(BattleIntroPathSettings.ResourcePath), Is.Not.Null);
        }
    }
}
