namespace Vex.Preload.Editor.Tests
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Vex.Preload;

    /// <summary>
    /// Throwaway scenes, prefabs, and SceneAssets for the Preload editor tests. Everything created here is either an
    /// additive scene closed on teardown or an asset under <see cref="TempFolder"/> deleted on teardown, so a run
    /// never leaves residue and never touches the designer's real scenes.
    ///
    /// Scene rules that shape this helper:
    ///  - Unity refuses to create an *additive* scene while an untitled-unsaved scene is open, and only ONE untitled
    ///    additive scene may exist at a time. So the fixture first stands up a *saved* sandbox scene (see
    ///    <see cref="NewSandboxScene"/>) to unblock additive creation, prefab sources are built in that active sandbox
    ///    rather than in their own scene, and tests that need a second loaded scene save the first.
    /// </summary>
    internal sealed class PreloadTestWorld
    {
        public const string TempFolder = "Assets/__PreloadTests";

        private readonly List<Scene> additiveScenes = new();
        private readonly List<string> tempAssets = new();

        /// <summary>Replace whatever is open with a single saved sandbox scene — unblocks additive creation.</summary>
        public Scene NewSandboxScene()
        {
            EnsureFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var path = AssetPath("_Sandbox.unity");
            EditorSceneManager.SaveScene(scene, path);
            this.tempAssets.Add(path);
            return scene;
        }

        /// <summary>An unsaved in-memory additive scene (empty path). At most one of these may be open at once.</summary>
        public Scene NewLooseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            this.additiveScenes.Add(scene);
            return scene;
        }

        /// <summary>An additive scene saved to a temp path, so it owns a real <see cref="SceneAsset"/> for SubScene refs.</summary>
        public Scene NewSavedScene(string name)
        {
            EnsureFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var path = AssetPath(name + ".unity");
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            this.additiveScenes.Add(scene);
            this.tempAssets.Add(path);
            return scene;
        }

        public GameObject AddMarker(Scene scene, PreloadKind kind, string name = null)
        {
            var go = new GameObject(name ?? "Marker_" + kind);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PreloadMarker>().Kind = kind;
            return go;
        }

        public GameObject AddChild(GameObject parent)
        {
            var go = new GameObject("Child");
            go.transform.SetParent(parent.transform);
            return go;
        }

        public GameObject AddSubScene(Scene host, SceneAsset target)
        {
            var go = new GameObject("SubScene");
            SceneManager.MoveGameObjectToScene(go, host);
            var sub = go.AddComponent<Unity.Scenes.SubScene>();
            sub.SceneAsset = target;
            sub.AutoLoadScene = false;
            return go;
        }

        /// <summary>A temp prefab asset whose root carries a <see cref="PreloadMarker"/> of the given kind.</summary>
        public GameObject NewMarkerPrefab(string name, PreloadKind kind)
        {
            return this.BuildPrefab(name, go => go.AddComponent<PreloadMarker>().Kind = kind);
        }

        /// <summary>A temp prefab asset with no marker — exercises the prefab-instance detection path.</summary>
        public GameObject NewPlainPrefab(string name)
        {
            return this.BuildPrefab(name, _ => { });
        }

        public static SceneAsset AssetFor(Scene scene)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
        }

        public void Dispose()
        {
            // Close additive scenes first; the sandbox stays open (you cannot close the last scene).
            foreach (var scene in this.additiveScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            foreach (var path in this.tempAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private GameObject BuildPrefab(string name, System.Action<GameObject> decorate)
        {
            EnsureFolder();

            // Built in the active sandbox scene (not its own additive scene, which would collide with a test's loose
            // scene), then immediately removed — only the saved prefab asset survives.
            var src = new GameObject(name);
            decorate(src);
            var path = AssetPath(name + ".prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(src, path);
            Object.DestroyImmediate(src);
            this.tempAssets.Add(path);
            return prefab;
        }

        private static string AssetPath(string fileName)
        {
            return TempFolder + "/" + fileName;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "__PreloadTests");
            }
        }
    }
}
