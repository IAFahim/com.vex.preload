using System;
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vex.Preload.Editor.Tests
{
    internal sealed class PreloadTestWorld
    {
        public const string TempFolder = "Assets/__PreloadTests";

        private readonly List<Scene> additiveScenes = new();
        private readonly List<string> tempAssets = new();

        public Scene NewSandboxScene()
        {
            EnsureFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var path = AssetPath("_Sandbox.unity");
            EditorSceneManager.SaveScene(scene, path);
            tempAssets.Add(path);
            return scene;
        }

        public Scene NewLooseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            additiveScenes.Add(scene);
            return scene;
        }

        public Scene NewSavedScene(string name)
        {
            EnsureFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var path = AssetPath(name + ".unity");
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            additiveScenes.Add(scene);
            tempAssets.Add(path);
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
            var sub = go.AddComponent<SubScene>();
            sub.SceneAsset = target;
            sub.AutoLoadScene = false;
            return go;
        }

        public GameObject NewMarkerPrefab(string name, PreloadKind kind)
        {
            return BuildPrefab(name, go => go.AddComponent<PreloadMarker>().Kind = kind);
        }

        public GameObject NewPlainPrefab(string name)
        {
            return BuildPrefab(name, _ => { });
        }

        public static SceneAsset AssetFor(Scene scene)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
        }

        public void Dispose()
        {
            foreach (var scene in additiveScenes)
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);

            foreach (var path in tempAssets)
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);

            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        private GameObject BuildPrefab(string name, Action<GameObject> decorate)
        {
            EnsureFolder();

            var src = new GameObject(name);
            decorate(src);
            var path = AssetPath(name + ".prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(src, path);
            Object.DestroyImmediate(src);
            tempAssets.Add(path);
            return prefab;
        }

        private static string AssetPath(string fileName)
        {
            return TempFolder + "/" + fileName;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "__PreloadTests");
        }
    }
}