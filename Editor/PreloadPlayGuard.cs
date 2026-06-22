namespace Vex.Preload.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using Unity.Scenes;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [InitializeOnLoad]
    public static class PreloadPlayGuard
    {
        public const string TempHostRootName = "[Preload Host]";
        public const string TempHostPath = "Assets/Preload/~PreloadHost.unity";

        private const string TempHostFolder = "Assets/Preload";
        private const string ContentKey = "vex.preload.content";
        private const string ModeKey = "vex.preload.mode";
        private const string ModeWrap = "wrap";
        private const string ModeRepair = "repair";

        public static bool IsBuilding { get; private set; }

        static PreloadPlayGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Self-heal a STRAY temp host. The wrap path discards '~PreloadHost' on the play→edit transition, but a
            // DOMAIN RELOAD (or a crashed/interrupted play) can leave the Editor in EDIT mode with the temp host still
            // open and the real content scene closed — that transition never fired. Discard it on load. Deferred so the
            // scene manager + asset DB are ready; guarded so it never runs while entering/exiting play (that path owns
            // the host) or while the wrap is still mid-build.
            EditorApplication.delayCall += DiscardStrayTempHostInEditMode;
        }

        private static void DiscardStrayTempHostInEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || IsBuilding)
            {
                return;
            }

            if (FindTempHost().IsValid())
            {
                DiscardTempHost();
            }
        }

        public static bool IsTempHost(Scene scene)
        {
            return scene.IsValid() && scene.path == TempHostPath;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    GuardEntry();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    DiscardTempHost();
                    break;
            }
        }

        private static void GuardEntry()
        {
            if (!PreloadState.Enabled)
            {
                return;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (IsTempHost(active) || !active.IsValid() || string.IsNullOrEmpty(active.path))
            {
                return;
            }

            var sceneClass = PreloadClassifier.Classify(active);
            if (sceneClass == SceneClass.Excluded)
            {
                return;
            }

            if (sceneClass == SceneClass.Content && !HostLoadedFor(active))
            {
                Schedule(active.path, ModeWrap);
                return;
            }

            if (NeedsRepair(active, sceneClass))
            {
                Schedule(active.path, ModeRepair);
            }
        }

        // A loaded scene that should host but doesn't, or any loaded subscene whose content is missing, needs a repair.
        private static bool NeedsRepair(Scene active, SceneClass sceneClass)
        {
            var needHost = (sceneClass == SceneClass.Host || sceneClass == SceneClass.Blank) && !PreloadInjector.HasHost(active);
            return needHost || LoadedSubscenesMissingContent().Count > 0;
        }

        private static void Schedule(string path, string mode)
        {
            SessionState.SetString(ContentKey, path);
            SessionState.SetString(ModeKey, mode);
            EditorApplication.ExitPlaymode();
            EditorApplication.update += DeferredBuild;
        }

        private static void DeferredBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= DeferredBuild;

            var mode = SessionState.GetString(ModeKey, string.Empty);
            SessionState.EraseString(ModeKey);

            if (mode == ModeRepair)
            {
                RepairAndPlay();
            }
            else
            {
                BuildHostAndPlay();
            }
        }

        private static void RepairAndPlay()
        {
            var path = SessionState.GetString(ContentKey, string.Empty);
            SessionState.EraseString(ContentKey);

            var active = EditorSceneManager.GetActiveScene();
            if (active.path == path)
            {
                var sceneClass = PreloadClassifier.Classify(active);
                if ((sceneClass == SceneClass.Host || sceneClass == SceneClass.Blank) && PreloadInjector.EnsureHost(active))
                {
                    Debug.Log($"[Preload] '{active.name}' was missing Required In Scene — added it back before play.");
                }
            }

            foreach (var subscene in LoadedSubscenesMissingContent())
            {
                if (PreloadInjector.EnsureContent(subscene))
                {
                    Debug.Log($"[Preload] '{subscene.name}' was missing Required In Subscene — added it back before play.");
                }
            }

            EditorApplication.EnterPlaymode();
        }

        private static List<Scene> LoadedSubscenesMissingContent()
        {
            var result = new List<Scene>();
            var seen = new HashSet<string>();

            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var host = EditorSceneManager.GetSceneAt(i);
                if (!host.isLoaded)
                {
                    continue;
                }

                foreach (var root in host.GetRootGameObjects())
                {
                    foreach (var subScene in root.GetComponentsInChildren<SubScene>(true))
                    {
                        if (TryGetMissingContentScene(subScene, seen, out var scene))
                        {
                            result.Add(scene);
                        }
                    }
                }
            }

            return result;
        }

        // True only the first time we see a non-excluded, loaded subscene that is still missing its required content.
        private static bool TryGetMissingContentScene(SubScene subScene, HashSet<string> seen, out Scene scene)
        {
            scene = default;

            if (subScene.SceneAsset == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(subScene.SceneAsset);
            if (!seen.Add(path) || PreloadSettings.Instance.IsExcluded(path))
            {
                return false;
            }

            scene = EditorSceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded && !PreloadInjector.HasContent(scene);
        }

        private static void BuildHostAndPlay()
        {
            var contentPath = SessionState.GetString(ContentKey, string.Empty);
            if (string.IsNullOrEmpty(contentPath))
            {
                return;
            }

            var contentAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(contentPath);
            if (contentAsset == null)
            {
                SessionState.EraseString(ContentKey);
                return;
            }

            // NewScene(Single) below discards every open scene — including the content scene the user just pressed Play
            // from — without prompting. Give them the chance to save (or cancel the whole wrap) so unsaved edits survive.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.EraseString(ContentKey);
                SessionState.EraseString(ModeKey);
                return;
            }

            IsBuilding = true;
            try
            {
                var host = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject(TempHostRootName);

                var hostPrefab = PreloadSettings.Instance.hostPrefab;
                if (hostPrefab != null)
                {
                    PrefabUtility.InstantiatePrefab(hostPrefab, host);
                }

                var subScene = new GameObject("SubScene").AddComponent<SubScene>();
                subScene.SceneAsset = contentAsset;
                subScene.AutoLoadScene = true;

                EnsureFolder();
                EditorSceneManager.SaveScene(host, TempHostPath);
            }
            finally
            {
                IsBuilding = false;
            }

            Debug.Log($"[Preload] '{Path.GetFileNameWithoutExtension(contentPath)}' is a subscene with no host — wrapped it in a temporary host so it can play. Stop play to return to it.");
            EditorApplication.EnterPlaymode();
        }

        private static void DiscardTempHost()
        {
            if (!FindTempHost().IsValid())
            {
                CleanupAsset();
                return;
            }

            // The NewScene/OpenScene calls below replace the open scene(s) wholesale. Prompt first so any unsaved edits
            // (e.g. work done on the temp host) are not silently lost; on cancel, leave everything intact and bail.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var contentPath = SessionState.GetString(ContentKey, string.Empty);
            SessionState.EraseString(ContentKey);

            if (!string.IsNullOrEmpty(contentPath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(contentPath) != null)
            {
                EditorSceneManager.OpenScene(contentPath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            CleanupAsset();
        }

        private static void CleanupAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TempHostPath) != null)
            {
                AssetDatabase.DeleteAsset(TempHostPath);
            }

            if (AssetDatabase.IsValidFolder(TempHostFolder))
            {
                AssetDatabase.DeleteAsset(TempHostFolder);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempHostFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Preload");
            }
        }

        private static Scene FindTempHost()
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (IsTempHost(scene))
                {
                    return scene;
                }
            }

            return default;
        }

        private static bool HostLoadedFor(Scene content)
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene == content)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var subScene in root.GetComponentsInChildren<SubScene>(true))
                    {
                        if (subScene.SceneAsset != null && AssetDatabase.GetAssetPath(subScene.SceneAsset) == content.path)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
