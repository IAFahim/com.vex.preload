using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vex.Preload.Editor
{
    public static class PreloadInjector
    {
        public static bool EnsureHost(Scene scene)
        {
            return Ensure(scene, PreloadSettings.Instance.hostPrefab, PreloadKind.Host);
        }

        public static bool HasHost(Scene scene)
        {
            return PreloadClassifier.ContainsKind(scene, PreloadKind.Host) ||
                   PreloadClassifier.ContainsPrefab(scene, PreloadSettings.Instance.hostPrefab);
        }

        public static bool EnsureContent(Scene scene)
        {
            return Ensure(scene, PreloadSettings.Instance.contentPrefab, PreloadKind.Content);
        }

        public static bool HasContent(Scene scene)
        {
            return PreloadClassifier.ContainsKind(scene, PreloadKind.Content) ||
                   PreloadClassifier.ContainsPrefab(scene, PreloadSettings.Instance.contentPrefab);
        }

        public static void StampMarkers()
        {
            Stamp(PreloadSettings.Instance.hostPrefab, PreloadKind.Host);
            Stamp(PreloadSettings.Instance.contentPrefab, PreloadKind.Content);
        }

        internal static bool Ensure(Scene scene, GameObject prefab, PreloadKind kind)
        {
            if (prefab == null || !scene.IsValid() || !scene.isLoaded) return false;

            if (PreloadClassifier.ContainsKind(scene, kind) || PreloadClassifier.ContainsPrefab(scene, prefab))
                return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) return false;

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void Stamp(GameObject prefab, PreloadKind kind)
        {
            if (prefab == null) return;

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path)) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var marker = root.GetComponent<PreloadMarker>();
                if (marker == null) marker = root.AddComponent<PreloadMarker>();

                marker.Kind = kind;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}