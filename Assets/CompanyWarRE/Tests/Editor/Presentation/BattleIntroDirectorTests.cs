using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class BattleIntroDirectorTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Intro_HidesUiAndRestoresCameraAfterCompletionOrSkip(bool skip)
        {
            var root = new GameObject("IntroTest");
            try
            {
                var cameraRoot = new GameObject("Camera", typeof(Camera), typeof(BattleSliceCameraRig));
                cameraRoot.transform.SetParent(root.transform);
                var camera = cameraRoot.GetComponent<Camera>();
                var rig = cameraRoot.GetComponent<BattleSliceCameraRig>();
                rig.ConfigureVisualSize(80f, 100f, 128f, null);
                var position = camera.transform.position;
                var rotation = camera.transform.rotation;
                var size = camera.orthographicSize;
                var ui = new GameObject("HUD", typeof(Canvas), typeof(GraphicRaycaster));
                ui.transform.SetParent(root.transform);
                var hiddenUi = new GameObject("InitiallyDisabled", typeof(Canvas));
                hiddenUi.transform.SetParent(root.transform);
                hiddenUi.GetComponent<Canvas>().enabled = false;
                var director = root.AddComponent<BattleIntroDirector>();
                director.Play(camera, new Vector3(0, 128, 0), 6.5f);
                Assert.That(director.IsPlaying, Is.True);
                Assert.That(rig.enabled, Is.False);
                Assert.That(ui.GetComponent<Canvas>().enabled, Is.False);
                Assert.That(ui.GetComponent<GraphicRaycaster>().enabled, Is.False);
                director.Advance(2f);
                Assert.That(Vector3.Distance(camera.transform.position, position), Is.GreaterThan(1f));
                var lateUi = new GameObject("LateHUD", typeof(Canvas));
                lateUi.transform.SetParent(root.transform);
                director.Advance(0.1f);
                Assert.That(lateUi.GetComponent<Canvas>().enabled, Is.False);
                if (skip)
                {
                    director.Skip();
                    director.Advance(0.1f);
                    Assert.That(director.IsPlaying, Is.True, "Skip must blend before restoring UI.");
                    director.Advance(0.4f);
                }
                else director.Advance(5f);
                Assert.That(director.IsPlaying, Is.False);
                Assert.That(rig.enabled, Is.True);
                Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
                Assert.That(ui.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(ui.GetComponent<GraphicRaycaster>().enabled, Is.True);
                Assert.That(lateUi.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(hiddenUi.GetComponent<Canvas>().enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Intro_CancellationRestoresUiAndAllowsReplay()
        {
            var root = new GameObject("CancelIntroTest");
            try
            {
                var cameraRoot = new GameObject("Camera", typeof(Camera));
                cameraRoot.transform.SetParent(root.transform);
                var camera = cameraRoot.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 30, -30);
                var ui = new GameObject("HUD", typeof(Canvas));
                ui.transform.SetParent(root.transform);
                var director = root.AddComponent<BattleIntroDirector>();
                director.Play(camera, Vector3.zero, 6.5f);
                director.Cancel();
                Assert.That(director.IsPlaying, Is.False);
                Assert.That(ui.GetComponent<Canvas>().enabled, Is.True);
                director.Play(camera, Vector3.zero, 6.5f);
                director.Cancel();
                director.Play(camera, Vector3.zero, 6.5f);
                director.Advance(7f);
                Assert.That(director.IsPlaying, Is.False);
                Assert.That(ui.GetComponent<Canvas>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
