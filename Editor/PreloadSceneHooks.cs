using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vex.Preload.Editor
{
    [InitializeOnLoad]
    public static class PreloadSceneHooks
    {
        static PreloadSceneHooks()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            MaybeInject(scene);
        }

        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            MaybeInject(scene);
        }

        private static void MaybeInject(Scene scene)
        {
            if (!ShouldConsider(scene)) return;

            var sceneClass = PreloadClassifier.Classify(scene);
            if (sceneClass != SceneClass.Host && sceneClass != SceneClass.Blank) return;

            if (!PreloadInjector.EnsureHost(scene)) return;

            var hint = sceneClass == SceneClass.Blank
                ? " New scene — link a SubScene next (Vex > Preload > Create paired subscene)."
                : string.Empty;
            Debug.Log($"[Preload] '{SceneName(scene)}' was missing Required In Scene — added it for you.{hint}");
        }

        private static bool ShouldConsider(Scene scene)
        {
            if (!PreloadState.Enabled || EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid())
                return false;

            return !PreloadPlayGuard.IsBuilding && !PreloadPlayGuard.IsTempHost(scene);
        }

        private static string SceneName(Scene scene)
        {
            return string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
        }
    }
}