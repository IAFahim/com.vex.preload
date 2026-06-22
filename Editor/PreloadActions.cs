namespace Vex.Preload.Editor
{
    using System.IO;
    using Unity.Scenes;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static class PreloadActions
    {
        [MenuItem("Vex/Preload/Stamp Required Markers On Prefabs")]
        public static void StampMarkers()
        {
            PreloadInjector.StampMarkers();
        }

        /// <summary>
        /// Create a new subscene pre-populated with the Content prefab ("Required In Subscene") and link it into the
        /// active host scene via a SubScene component. This is the creation counterpart to the on-play repair: instead
        /// of waiting for Play to inject the content, you get a ready-to-author subscene with the required stage
        /// components already in it. Referenced by the "new scene" hint in PreloadSceneHooks.
        /// </summary>
        [MenuItem("Vex/Preload/Create Paired Subscene")]
        public static void CreatePairedSubscene()
        {
            var host = EditorSceneManager.GetActiveScene();
            if (!host.IsValid() || string.IsNullOrEmpty(host.path))
            {
                Debug.LogWarning("[Preload] Save the active host scene first, then create a paired subscene.");
                return;
            }

            // Make sure the host carries Required In Scene before we hang a subscene off it.
            PreloadInjector.EnsureHost(host);

            var defaultDir = Path.GetDirectoryName(host.path);
            var defaultName = Path.GetFileNameWithoutExtension(host.path) + " Subscene";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Paired Subscene", defaultName, "unity", "Where to save the new subscene.", defaultDir);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Build the subscene's scene additively, drop the Content prefab in, and save it as the new asset.
            // Suppress the scene hooks while we author this scene: the additive NewScene synchronously raises
            // EditorSceneManager.newSceneCreated, and the brand-new empty scene classifies as Blank — without this
            // PreloadSceneHooks would inject the Required-In-Scene HOST prefab into what is about to become subscene
            // CONTENT, producing a paired subscene that carries both a host stage and the content stage.
            var content = PreloadSettings.Instance.contentPrefab;
            SceneAsset asset;
            var settings = PreloadSettings.Instance;
            var wasEnabled = settings.enabled;
            settings.enabled = false;
            try
            {
                var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (content != null)
                {
                    PrefabUtility.InstantiatePrefab(content, sub);
                }

                EditorSceneManager.SaveScene(sub, path);
                asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

                // Close the loose scene — the SubScene component owns its load/edit lifecycle from here.
                EditorSceneManager.CloseScene(sub, true);
            }
            finally
            {
                settings.enabled = wasEnabled;
            }

            // Link it into the host as an auto-loading SubScene.
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            SceneManager.MoveGameObjectToScene(go, host);
            var subScene = go.AddComponent<SubScene>();
            subScene.SceneAsset = asset;
            subScene.AutoLoadScene = true;

            EditorSceneManager.MarkSceneDirty(host);
            Selection.activeGameObject = go;
            Debug.Log($"[Preload] Created paired subscene '{Path.GetFileNameWithoutExtension(path)}'" +
                      (content != null ? " with Required In Subscene" : string.Empty) + $" and linked it into '{host.name}'.");
        }
    }
}
