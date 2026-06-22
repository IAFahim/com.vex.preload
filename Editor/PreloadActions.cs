using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vex.Preload.Editor
{
    public static class PreloadActions
    {
        [MenuItem("Vex/Preload/Stamp Required Markers On Prefabs")]
        public static void StampMarkers()
        {
            PreloadInjector.StampMarkers();
        }

        [MenuItem("Vex/Preload/Create Paired Subscene")]
        public static void CreatePairedSubscene()
        {
            var host = EditorSceneManager.GetActiveScene();
            if (!host.IsValid() || string.IsNullOrEmpty(host.path))
            {
                Debug.LogWarning("[Preload] Save the active host scene first, then create a paired subscene.");
                return;
            }

            PreloadInjector.EnsureHost(host);

            var defaultDir = Path.GetDirectoryName(host.path);
            var defaultName = Path.GetFileNameWithoutExtension(host.path) + " Subscene";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Paired Subscene", defaultName, "unity", "Where to save the new subscene.", defaultDir);
            if (string.IsNullOrEmpty(path)) return;

            var content = PreloadSettings.Instance.contentPrefab;
            SceneAsset asset;
            var settings = PreloadSettings.Instance;
            var wasEnabled = settings.enabled;
            settings.enabled = false;
            try
            {
                var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (content != null) PrefabUtility.InstantiatePrefab(content, sub);

                EditorSceneManager.SaveScene(sub, path);
                asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

                EditorSceneManager.CloseScene(sub, true);
            }
            finally
            {
                settings.enabled = wasEnabled;
            }

            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            SceneManager.MoveGameObjectToScene(go, host);
            var subScene = go.AddComponent<SubScene>();
            subScene.SceneAsset = asset;
            subScene.AutoLoadScene = true;

            EditorSceneManager.MarkSceneDirty(host);
            Selection.activeGameObject = go;
            Debug.Log($"[Preload] Created paired subscene '{Path.GetFileNameWithoutExtension(path)}'" +
                      (content != null ? " with Required In Subscene" : string.Empty) +
                      $" and linked it into '{host.name}'.");
        }
    }
}