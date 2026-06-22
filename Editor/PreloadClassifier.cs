using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vex.Preload.Editor
{
    public enum SceneClass
    {
        Host,
        Content,
        Blank,
        Excluded
    }

    public static class PreloadClassifier
    {
        public static SceneClass Classify(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return SceneClass.Excluded;

            if (PreloadSettings.Instance.IsExcluded(scene.path)) return SceneClass.Excluded;

            if (ContainsKind(scene, PreloadKind.Content) ||
                ContainsPrefab(scene, PreloadSettings.Instance.contentPrefab) ||
                IsReferencedSubscene(scene))
                return SceneClass.Content;

            if (ContainsKind(scene, PreloadKind.Host) ||
                ContainsPrefab(scene, PreloadSettings.Instance.hostPrefab) ||
                HasSubScene(scene))
                return SceneClass.Host;

            return SceneClass.Blank;
        }

        public static bool ContainsKind(Scene scene, PreloadKind kind)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var marker in root.GetComponentsInChildren<PreloadMarker>(true))
                if (marker.Kind == kind)
                    return true;

            return false;
        }

        public static bool ContainsPrefab(Scene scene, GameObject prefab)
        {
            if (prefab == null || !scene.IsValid() || !scene.isLoaded) return false;

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath)) return false;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(transform.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == prefabPath) return true;
            }

            return false;
        }

        public static bool HasSubScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<SubScene>(true) != null)
                    return true;

            return false;
        }

        public static bool IsReferencedSubscene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return false;

            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var host = EditorSceneManager.GetSceneAt(i);
                if (!host.isLoaded || host == scene) continue;

                foreach (var root in host.GetRootGameObjects())
                foreach (var subScene in root.GetComponentsInChildren<SubScene>(true))
                    if (subScene.SceneAsset != null && AssetDatabase.GetAssetPath(subScene.SceneAsset) == scene.path)
                        return true;
            }

            return false;
        }
    }
}