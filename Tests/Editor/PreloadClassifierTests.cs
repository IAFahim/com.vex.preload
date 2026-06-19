namespace Vex.Preload.Editor.Tests
{
    using NUnit.Framework;
    using UnityEngine.SceneManagement;
    using Vex.Preload;

    [TestFixture]
    internal sealed class PreloadClassifierTests : PreloadTestBase
    {
        [Test]
        public void Classify_EmptyScene_IsBlank()
        {
            var scene = this.World.NewLooseScene();
            Assert.AreEqual(SceneClass.Blank, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_HostMarker_IsHost()
        {
            var scene = this.World.NewLooseScene();
            this.World.AddMarker(scene, PreloadKind.Host);
            Assert.AreEqual(SceneClass.Host, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_ContentMarker_IsContent()
        {
            var scene = this.World.NewLooseScene();
            this.World.AddMarker(scene, PreloadKind.Content);
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_BothMarkers_ContentWins()
        {
            // A scene tagged as both must be treated as content: we never want to drop a host prefab into something
            // that is actually subscene content.
            var scene = this.World.NewLooseScene();
            this.World.AddMarker(scene, PreloadKind.Host);
            this.World.AddMarker(scene, PreloadKind.Content);
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(scene));
        }

        [Test]
        public void Classify_SubSceneComponent_IsHost()
        {
            var content = this.World.NewSavedScene("Content_SubComp");
            var host = this.World.NewLooseScene();
            this.World.AddSubScene(host, PreloadTestWorld.AssetFor(content));
            Assert.AreEqual(SceneClass.Host, PreloadClassifier.Classify(host));
        }

        [Test]
        public void Classify_InvalidScene_IsExcluded()
        {
            // A default/never-opened scene has no roots to scan; classification must not throw and must say "leave it".
            Assert.AreEqual(SceneClass.Excluded, PreloadClassifier.Classify(default(Scene)));
        }

        [Test]
        public void Classify_ReferencedSubscene_IsContent()
        {
            // The odd case the designer hit: a subscene whose content prefab was deleted. It carries no marker, but a
            // loaded host references it as a SubScene — so it must classify as Content, never Host.
            var content = this.World.NewSavedScene("Content_Ref");
            var host = this.World.NewLooseScene();
            this.World.AddSubScene(host, PreloadTestWorld.AssetFor(content));

            Assert.IsTrue(PreloadClassifier.IsReferencedSubscene(content), "host should reference content");
            Assert.AreEqual(SceneClass.Content, PreloadClassifier.Classify(content));
        }

        [Test]
        public void IsReferencedSubscene_NoHost_IsFalse()
        {
            var lonely = this.World.NewSavedScene("Content_Lonely");
            Assert.IsFalse(PreloadClassifier.IsReferencedSubscene(lonely));
            Assert.AreEqual(SceneClass.Blank, PreloadClassifier.Classify(lonely));
        }

        [Test]
        public void ContainsKind_NestedMarker_IsFound()
        {
            // Markers buried under other objects (designer parented the prefab somewhere) must still be detected.
            var scene = this.World.NewLooseScene();
            var parent = this.World.AddMarker(scene, PreloadKind.Host, "Parent");
            var child = this.World.AddChild(parent);
            child.AddComponent<PreloadMarker>().Kind = PreloadKind.Content;

            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Content));
            Assert.IsTrue(PreloadClassifier.ContainsKind(scene, PreloadKind.Host));
        }

        [Test]
        public void ContainsKind_WrongKind_IsFalse()
        {
            var scene = this.World.NewLooseScene();
            this.World.AddMarker(scene, PreloadKind.Host);
            Assert.IsFalse(PreloadClassifier.ContainsKind(scene, PreloadKind.Content));
        }

        [Test]
        public void ContainsPrefab_NullPrefab_IsFalse()
        {
            var scene = this.World.NewLooseScene();
            Assert.IsFalse(PreloadClassifier.ContainsPrefab(scene, null));
        }

        [Test]
        public void HasSubScene_MatchesPresence()
        {
            var content = this.World.NewSavedScene("Content_Has");
            var host = this.World.NewLooseScene();
            Assert.IsFalse(PreloadClassifier.HasSubScene(host));
            this.World.AddSubScene(host, PreloadTestWorld.AssetFor(content));
            Assert.IsTrue(PreloadClassifier.HasSubScene(host));
        }
    }
}
