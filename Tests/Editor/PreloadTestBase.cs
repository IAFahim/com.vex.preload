using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace Vex.Preload.Editor.Tests
{
    internal abstract class PreloadTestBase
    {
        private bool enabledToggled;
        private bool originalEnabled;

        private SceneSetup[] originalSetup;
        protected PreloadTestWorld World { get; private set; }

        [SetUp]
        public void SetUp()
        {
            originalSetup = EditorSceneManager.GetSceneManagerSetup();
            World = new PreloadTestWorld();
            World.NewSandboxScene();

            originalEnabled = PreloadSettings.Instance.enabled;
            PreloadSettings.Instance.enabled = false;
            enabledToggled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (enabledToggled)
            {
                PreloadSettings.Instance.enabled = originalEnabled;
                enabledToggled = false;
            }

            if (originalSetup is { Length: > 0 }) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);

            if (World != null)
            {
                World.Dispose();
                World = null;
            }
        }
    }
}