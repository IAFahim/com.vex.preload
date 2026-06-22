using NUnit.Framework;

namespace Vex.Preload.Editor.Tests
{
    [TestFixture]
    internal sealed class PreloadClassifierTests : PreloadTestBase
    {
        [Test]
        public void Classify_EmptyScene_IsBlank()
        {
            var scene = World.NewLooseScene();
            Assert.AreEqual(SceneClass.Blank, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_HostMarker_IsHost()
        {
            var scene = World.NewLooseScene();
            World.AddMarker(scene, PreloadKind.Host);
            Assert.AreEqual(SceneClass.Host, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_ContentMarker_IsContent()
        {
            var scene = World.NewLooseScene();
            World.AddMarker(scene, PreloadKind.Content);
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_BothMarkers_ContentWins()
        {
            var scene = World.NewLooseScene();
            World.AddMarker(scene, PreloadKind.Host);
            World.AddMarker(scene, PreloadKind.Content);
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_SubSceneComponent_IsHost()
        {
            var content = World.NewSavedScene("Content_SubComp");
            var host = World.NewLooseScene();
            World.AddSubScene(host, PreloadTestWorld.AssetFor(content));
            Assert.AreEqual(SceneClass.Host, PreloadClassifier.Classify(host));
        }

        [Test]
        public void Classify_InvalidScene_IsExcluded()
        {
            Assert.AreEqual(SceneClass.Excluded, PreloadClassifier.Classify(default));
        }

        [Test]
        public void Classify_ReferencedSubscene_IsContent()
        {
            var content = World.NewSavedScene("Content_Ref");
            var host = World.NewLooseScene();
            World.AddSubScene(host, PreloadTestWorld.AssetFor(content));

            Assert.IsTrue(PreloadClassifier.IsReferencedSubscene(content), "host should reference content");
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(content));
        }

        [Test]
        public void IsReferencedSubscene_NoHost_IsFalse()
        {
            var lonely = World.NewSavedScene("Content_Lonely");
            Assert.IsFalse(PreloadClassifier.IsReferencedSubscene(lonely));
            Assert.AreEqual(SceneClass.Blank, PreloadClassifier.Classify(lonely));
        }

        [Test]
        public void ContainsKind_NestedMarker_IsFound()
        {
            var scene = World.NewLooseScene();
            var parent = World.AddMarker(scene, PreloadKind.Host, "Parent");
            var child = World.AddChild(parent);
            child.AddComponent<PreloadMarker>().Kind = PreloadKind.Content;

            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Content));
            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Host));
        }

        [Test]
        public void ContainsKind_WrongKind_IsFalse()
        {
            var scene = World.NewLooseScene();
            World.AddMarker(scene, PreloadKind.Host);
            Assert.IsFalse(PreloadClassifier.ContainsKind(scene, PreloadKind.Content));
        }

        [Test]
        public void ContainsPrefab_NullPrefab_IsFalse()
        {
            var scene = World.NewLooseScene();
            Assert.IsFalse(PreloadClassifier.ContainsPrefab(scene, null));
        }

        [Test]
        public void HasSubScene_MatchesPresence()
        {
            var content = World.NewSavedScene("Content_Has");
            var host = World.NewLooseScene();
            Assert.IsFalse(PreloadClassifier.HasSubScene(host));
            World.AddSubScene(host, PreloadTestWorld.AssetFor(content));
            Assert.IsTrue(PreloadClassifier.HasSubScene(host));
        }
    }
}