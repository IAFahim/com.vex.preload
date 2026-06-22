namespace Vex.Preload.Editor.Tests
{
    using NUnit.Framework;
    using UnityEditor.SceneManagement;

    /// <summary>
    /// Shared fixture: snapshots the editor's open-scene setup, swaps to a fresh saved sandbox scene for the test, and
    /// restores the original setup afterwards. The sandbox is the active scene during the test, so object creation and
    /// prefab building never touch — or even dirty — the designer's real scenes, and additive scene creation is
    /// unblocked (Unity forbids it while an untitled-unsaved scene is open).
    ///
    /// It also turns the live feature OFF for the duration of the test. <see cref="PreloadSceneHooks"/> reacts to
    /// <c>newSceneCreated</c> by auto-injecting the host prefab — which would pollute the very scenes these tests build
    /// to assert against. We want to exercise the pure classifier/injector units, not the editor event plumbing, so we
    /// suppress the hooks and put the toggle back exactly as we found it.
    /// </summary>
    internal abstract class PreloadTestBase
    {
        protected PreloadTestWorld World { get; private set; }

        private SceneSetup[] originalSetup;
        private bool originalEnabled;
        private bool enabledToggled;

        [SetUp]
        public void SetUp()
        {
            // Capture the real scene setup so we can put it back. Empty when the runner is on an untitled scene.
            this.originalSetup = EditorSceneManager.GetSceneManagerSetup();
            this.World = new PreloadTestWorld();
            this.World.NewSandboxScene();

            // Toggle the shared on-disk settings asset only after the throwing scene I/O above has succeeded, so a
            // failure there never leaves the live feature disabled for the rest of the editor session.
            this.originalEnabled = PreloadSettings.Instance.enabled;
            PreloadSettings.Instance.enabled = false;
            this.enabledToggled = true;
        }

        [TearDown]
        public void TearDown()
        {
            // Restore the shared settings asset first: it mutates global state, and World.Dispose /
            // RestoreSceneManagerSetup below can throw and skip the rest of TearDown. Only restore if we actually
            // toggled it, so a SetUp that failed before the toggle does not clobber the asset to a default false.
            if (this.enabledToggled)
            {
                PreloadSettings.Instance.enabled = this.originalEnabled;
                this.enabledToggled = false;
            }

            if (this.originalSetup is { Length: > 0 })
            {
                EditorSceneManager.RestoreSceneManagerSetup(this.originalSetup);
            }

            if (this.World != null)
            {
                this.World.Dispose();
                this.World = null;
            }
        }
    }
}
