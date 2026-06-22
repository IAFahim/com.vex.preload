using NUnit.Framework;

namespace Vex.Preload.Editor.Tests
{
    [TestFixture]
    internal sealed class PreloadInjectorTests : PreloadTestBase
    {
        [Test]
        public void Ensure_EmptyScene_Injects()
        {
            var scene = World.NewLooseScene();
            var prefab = World.NewMarkerPrefab("HostPrefab", PreloadKind.Host);

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host));
            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Host));
        }

        [Test]
        public void Ensure_Twice_IsIdempotentViaMarker()
        {
            var scene = World.NewLooseScene();
            var prefab = World.NewMarkerPrefab("HostPrefab2", PreloadKind.Host);

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host), "first injects");
            Assert.IsFalse(PreloadInjector.Ensure(scene, prefab, PreloadKind.Host), "second is a no-op");

            var hostCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var marker in root.GetComponentsInChildren<PreloadMarker>(true))
                if (marker.Kind == PreloadKind.Host)
                    hostCount++;

            Assert.AreEqual(1, hostCount, "exactly one host marker after two Ensure calls");
        }

        [Test]
        public void Ensure_Twice_IsIdempotentViaPrefabInstance()
        {
            var scene = World.NewLooseScene();
            var prefab = World.NewPlainPrefab("PlainPrefab");

            Assert.IsTrue(PreloadInjector.Ensure(scene, prefab, PreloadKind.Content), "first injects");
            Assert.IsFalse(PreloadInjector.Ensure(scene, prefab, PreloadKind.Content), "second is a no-op");
        }

        [Test]
        public void Ensure_NullPrefab_IsNoOp()
        {
            var scene = World.NewLooseScene();
            Assert.IsFalse(PreloadInjector.Ensure(scene, null, PreloadKind.Host));
        }

        [Test]
        public void Ensure_InvalidScene_IsNoOp()
        {
            var prefab = World.NewMarkerPrefab("HostPrefab3", PreloadKind.Host);
            Assert.IsFalse(PreloadInjector.Ensure(default, prefab, PreloadKind.Host));
        }
    }
}