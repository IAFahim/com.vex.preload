namespace Vex.Preload.Editor.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;

    [TestFixture]
    internal sealed class PreloadSettingsTests
    {
        private PreloadSettings settings;

        [SetUp]
        public void SetUp()
        {
            // A detached instance so the test controls excludeFolders without depending on (or mutating) the real asset.
            this.settings = ScriptableObject.CreateInstance<PreloadSettings>();
            this.settings.excludeFolders = new List<string> { "Packages", "Assets/Samples" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.settings);
        }

        [Test]
        public void IsExcluded_UnderExcludedFolder_IsTrue()
        {
            Assert.IsTrue(this.settings.IsExcluded("Packages/com.foo/Scene.unity"));
            Assert.IsTrue(this.settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
        }

        [Test]
        public void IsExcluded_GameScene_IsFalse()
        {
            Assert.IsFalse(this.settings.IsExcluded("Assets/Scenes/Main Scene.unity"));
        }

        [Test]
        public void IsExcluded_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(this.settings.IsExcluded(null));
            Assert.IsFalse(this.settings.IsExcluded(string.Empty));
        }

        [Test]
        public void IsExcluded_NullEntry_DoesNotThrow()
        {
            this.settings.excludeFolders = new List<string> { null, string.Empty, "Packages" };
            Assert.DoesNotThrow(() => this.settings.IsExcluded("Assets/Scenes/Main Scene.unity"));
            Assert.IsTrue(this.settings.IsExcluded("Packages/x/Scene.unity"));
        }

        [Test]
        public void IsExcluded_MatchesFolderBoundaries_NotBareSubstring()
        {
            // A sibling that merely shares a leading substring with an excluded folder must NOT be excluded.
            Assert.IsFalse(this.settings.IsExcluded("Assets/SamplesOfMine/Scene.unity"));
            Assert.IsFalse(this.settings.IsExcluded("Assets/MySamples/Scene.unity"));

            // The folder itself and its real descendants are excluded.
            Assert.IsTrue(this.settings.IsExcluded("Assets/Samples"));
            Assert.IsTrue(this.settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
        }

        [Test]
        public void IsExcluded_TrailingSlashEntry_StillMatches()
        {
            this.settings.excludeFolders = new List<string> { "Assets/Samples/" };
            Assert.IsTrue(this.settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
            Assert.IsFalse(this.settings.IsExcluded("Assets/SamplesOfMine/Scene.unity"));
        }
    }
}
