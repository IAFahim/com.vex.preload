namespace Vex.Preload.Editor.Tests
{
    using NUnit.Framework;
    using UnityEngine.SceneManagement;
    using Vex.Preload;

    [TestFixture]
    internal sealed class PreloadInjectorTests : PreloadTestBase
    {
        [Test]
        public void Ensure_EmptyScene_Injects()
        {
            var scene = this.World.NewLooseScene();
            var prefab = this.World.NewMarkerPrefab("HostPrefab", PreloadKind.Host);

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host));
            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Host));
        }

        [Test]
        public void Ensure_Twice_IsIdempotentViaMarker()
        {
            var scene = this.World.NewLooseScene();
            var prefab = this.World.NewMarkerPrefab("HostPrefab2", PreloadKind.Host);

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host), "first injects");
            Assert.IsFalse(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host), "second is a no-op");

            var hostCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var marker in root.GetComponentsInChildren<PreloadMarker>(true))
                {
                    if (marker.Kind == PreloadKind.Host)
                    {
                        hostCount++;
                    }
                }
            }

            Assert.AreEqual(1, hostCount, "exactly one host marker after two Ensure calls");
        }

        [Test]
        public void Ensure_Twice_IsIdempotentViaPrefabInstance()
        {
            // A prefab with no marker still must not be injected twice — the prefab-instance check catches it.
            var scene = this.World.NewLooseScene();
            var prefab = this.World.NewPlainPrefab("PlainPrefab");

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Content), "first injects");
            Assert.IsFalse(PreloadInjector.Ensure(scene, prefab, PreloadKind.Content), "second is a no-op");
        }

        [Test]
        public void Ensure_NullPrefab_IsNoOp()
        {
            var scene = this.World.NewLooseScene();
            Assert.IsFalse(PreloadInjector.Ensure(scene, null, PreloadKind.Host));
        }

        [Test]
        public void Ensure_InvalidScene_IsNoOp()
        {
            var prefab = this.World.NewMarkerPrefab("HostPrefab3", PreloadKind.Host);
            Assert.IsFalse(PreloadInjector.Ensure(default(Scene), prefab, PreloadKind.Host));
        }
    }
}
