using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Vex.Preload.Editor.Tests
{
    [TestFixture]
    internal sealed class PreloadSettingsTests
    {
        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<PreloadSettings>();
            settings.excludeFolders = new List<string> { "Packages", "Assets/Samples" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        private PreloadSettings settings;

        [Test]
        public void IsExcluded_UnderExcludedFolder_IsTrue()
        {
            Assert.IsTrue(settings.IsExcluded("Packages/com.foo/Scene.unity"));
            Assert.IsTrue(settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
        }

        [Test]
        public void IsExcluded_GameScene_IsFalse()
        {
            Assert.IsFalse(settings.IsExcluded("Assets/Scenes/Main Scene.unity"));
        }

        [Test]
        public void IsExcluded_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(settings.IsExcluded(null));
            Assert.IsFalse(settings.IsExcluded(string.Empty));
        }

        [Test]
        public void IsExcluded_NullEntry_DoesNotThrow()
        {
            settings.excludeFolders = new List<string> { null, string.Empty, "Packages" };
            Assert.DoesNotThrow(() => settings.IsExcluded("Assets/Scenes/Main Scene.unity"));
            Assert.IsTrue(settings.IsExcluded("Packages/x/Scene.unity"));
        }

        [Test]
        public void IsExcluded_MatchesFolderBoundaries_NotBareSubstring()
        {
            Assert.IsFalse(settings.IsExcluded("Assets/SamplesOfMine/Scene.unity"));
            Assert.IsFalse(settings.IsExcluded("Assets/MySamples/Scene.unity"));

            Assert.IsTrue(settings.IsExcluded("Assets/Samples"));
            Assert.IsTrue(settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
        }

        [Test]
        public void IsExcluded_TrailingSlashEntry_StillMatches()
        {
            settings.excludeFolders = new List<string> { "Assets/Samples/" };
            Assert.IsTrue(settings.IsExcluded("Assets/Samples/Demo/Scene.unity"));
            Assert.IsFalse(settings.IsExcluded("Assets/SamplesOfMine/Scene.unity"));
        }
    }
}