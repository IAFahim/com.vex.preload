// <copyright file="WorldSetupBuilder.cs" company="Vex">
//     Copyright (c) Vex. All rights reserved.
// </copyright>

namespace Vex.WorldSetup.Editor
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Core.Authoring.Settings;
    using BovineLabs.Core.Authoring.SubScenes;
    using BovineLabs.Core.Editor.Settings;
    using BovineLabs.Core.SubScenes;
    using Unity.Scenes;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Vex.Preload.Editor;

    /// <summary>
    /// Builds the FULL BovineLabs SubScene world setup in Assets/Scenes, cooperating with the Vex Preload system.
    /// Demonstrates every part of SubSceneSettings: SceneSets (world-targeted streaming), EditorSceneSets (the dev-only
    /// "Scene Override" tool) and AssetSets (GameObjects instantiated into a world).
    ///
    /// Run the two menu items IN ORDER (a scene saved via SaveScene is not importable until the creating call returns,
    /// so creation and wiring must be separate editor ticks):
    ///   1 - Create Scene Files : creates Start (host) + Boot/Game/Service/Editor content subscenes.
    ///   2 - Wire Settings, Sets and Host : populates SubSceneSettings and links the host SubScene.
    ///
    /// Layout produced:
    ///   Start.unity          HOST scene you press Play on  ('Required In Scene' + Camera + SubScene -> Boot, AutoLoad).
    ///   Boot.unity           CONTENT subscene             ('Required In Subscene' + SubSceneLoadAuthoring -> settings).
    ///   Game World.unity     CONTENT, SceneSet TargetWorld=Game     (streams into GameWorld).
    ///   Service World.unity  CONTENT, SceneSet TargetWorld=Service  (streams into ServiceWorld).
    ///   Editor Override.unity CONTENT, EditorSceneSet                (Scene Override dev tool).
    ///   Service Asset.prefab  AssetSet member instantiated into the ServiceWorld at runtime.
    /// </summary>
    public static class WorldSetupBuilder
    {
        private const string Dir = "Assets/Scenes";
        private const string StartPath = Dir + "/Start.unity";
        private const string BootPath = Dir + "/Boot.unity";
        private const string GamePath = Dir + "/Game World.unity";
        private const string ServicePath = Dir + "/Service World.unity";
        private const string EditorPath = Dir + "/Editor Override.unity";
        private const string AssetPath = Dir + "/Service Asset.prefab";

        [MenuItem("BovineLabs/Vex/World Setup/1 - Create Scene Files")]
        public static void Step1CreateScenes()
        {
            var openSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Content subscenes (created the Preload way: host prefab suppressed, content prefab injected).
                EnsureContentSubscene(GamePath, null);
                EnsureContentSubscene(ServicePath, null);
                EnsureContentSubscene(EditorPath, null);

                var settings = LoadOrCreateSettings();
                EnsureContentSubscene(BootPath, scene =>
                {
                    var go = FindOrCreate(scene, "SubScene Loader");
                    var auth = go.GetComponent<SubSceneLoadAuthoring>() ? go.GetComponent<SubSceneLoadAuthoring>() : go.AddComponent<SubSceneLoadAuthoring>();
                    var so = new SerializedObject(auth);
                    so.FindProperty("settings").objectReferenceValue = settings;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

                AssetDatabase.SaveAssets();
                Debug.Log("[WorldSetup] Step 1 done — scene files created. Now run '2 - Wire Settings, Sets and Host'.");
            }
            finally
            {
                if (openSetup is { Length: > 0 })
                {
                    EditorSceneManager.RestoreSceneManagerSetup(openSetup);
                }
            }
        }

        [MenuItem("BovineLabs/Vex/World Setup/2 - Wire Settings, Sets and Host")]
        public static void Step2Wire()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath) == null)
            {
                Debug.LogError("[WorldSetup] Run '1 - Create Scene Files' first.");
                return;
            }

            var openSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var settings = LoadOrCreateSettings();

                // --- SceneSets: world-targeted streaming ---
                var gameSet = EnsureSceneSet($"{Dir}/Set - Game.asset", 0, SubSceneLoadFlags.Game, required: true, GamePath);
                var serviceSet = EnsureSceneSet($"{Dir}/Set - Service.asset", 1, SubSceneLoadFlags.Service, required: false, ServicePath);
                settings.SceneSets = new List<SubSceneSet> { gameSet, serviceSet };

                // --- EditorSceneSets: the dev-only "Scene Override" tool ---
                var editorSet = EnsureEditorSet($"{Dir}/Set - Editor Override.asset", SubSceneLoadFlags.Game, EditorPath);
                settings.EditorSceneSets = new List<SubSceneEditorSet> { editorSet };

                // --- AssetSets: GameObjects instantiated into a world at runtime ---
                var assetSet = EnsureAssetSet($"{Dir}/Set - Service Assets.asset", SubSceneLoadFlags.Service, EnsureSampleAsset());
                settings.AssetSets = new List<AssetSet> { assetSet };

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                // Re-bake Boot now that settings.SceneSets is final (the baker reads it off disk, no DependsOn).
                AssetDatabase.ImportAsset(BootPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                // --- Start (host) scene: Camera + SubScene(AutoLoad) -> Boot ---
                EnsureHostScene(StartPath, BootPath);
                AssetDatabase.SaveAssets();
                AddToBuildSettingsFirst(StartPath);

                Debug.Log("[WorldSetup] Step 2 done. SubSceneSettings now has " +
                    $"{settings.SceneSets.Count} SceneSets, {settings.EditorSceneSets.Count} EditorSceneSets, {settings.AssetSets.Count} AssetSets. " +
                    "Open 'Assets/Scenes/Start' and press Play.");
            }
            finally
            {
                if (openSetup is { Length: > 0 })
                {
                    EditorSceneManager.RestoreSceneManagerSetup(openSetup);
                }
            }
        }

        // NOTE: an earlier "Step 3 - Wire Editor Settings" was removed. It set EditorSettings.defaultSettingsAuthoring,
        // settingAuthoring and prebakeScenes, which inject/force-bake a settings entity into the EDITOR world. This
        // project's pattern is that every subscene carries its OWN SettingsAuthoring (via 'Required In Subscene'), so a
        // global editor default duplicates it -> "found 2 instances of <singleton>" crashes when opening any scene.
        // startupScene was also a trap: it hijacks Play in EVERY scene to boot Start. Leave all of these empty.

        private static void EnsureContentSubscene(string path, Action<Scene> extra)
        {
            var settings = PreloadSettings.Instance;
            var wasEnabled = settings.enabled;
            settings.enabled = false; // stop the OnNewSceneCreated hook injecting the HOST prefab
            try
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null
                    ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                PreloadInjector.EnsureContent(scene); // 'Required In Subscene'
                extra?.Invoke(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
            }
            finally
            {
                settings.enabled = wasEnabled;
            }
        }

        private static void EnsureHostScene(string path, string subscenePath)
        {
            var sub = AssetDatabase.LoadAssetAtPath<SceneAsset>(subscenePath);
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PreloadInjector.EnsureHost(scene); // 'Required In Scene'

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                cam.transform.position = new Vector3(0f, 1f, -10f);
                SceneManager.MoveGameObjectToScene(cam, scene);
            }

            var go = FindOrCreate(scene, "Boot");
            var ss = go.GetComponent<SubScene>() ? go.GetComponent<SubScene>() : go.AddComponent<SubScene>();
            ss.SceneAsset = sub;
            ss.AutoLoadScene = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static GameObject FindOrCreate(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static SubSceneSettings LoadOrCreateSettings()
        {
            var guids = AssetDatabase.FindAssets("t:SubSceneSettings");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<SubSceneSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            var settings = ScriptableObject.CreateInstance<SubSceneSettings>();
            AssetDatabase.CreateAsset(settings, $"{Dir}/SubSceneSettings.asset");
            return settings;
        }

        private static SubSceneSet EnsureSceneSet(string path, int id, SubSceneLoadFlags world, bool required, string contentPath)
        {
            var set = AssetDatabase.LoadAssetAtPath<SubSceneSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SubSceneSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.ID = id;
            set.TargetWorld = world;
            set.AutoLoad = true;
            set.IsRequired = required;
            set.WaitForLoad = true;
            set.Scenes = new List<SceneAsset> { LoadScene(contentPath, path) };
            EditorUtility.SetDirty(set);
            return set;
        }

        private static SubSceneEditorSet EnsureEditorSet(string path, SubSceneLoadFlags world, string contentPath)
        {
            var set = AssetDatabase.LoadAssetAtPath<SubSceneEditorSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SubSceneEditorSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.TargetWorld = world;
            set.Scenes = new List<SceneAsset> { LoadScene(contentPath, path) };
            EditorUtility.SetDirty(set);
            return set;
        }

        private static AssetSet EnsureAssetSet(string path, SubSceneLoadFlags world, GameObject asset)
        {
            var set = AssetDatabase.LoadAssetAtPath<AssetSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AssetSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.TargetWorld = world;
            set.Assets = new List<GameObject> { asset };
            EditorUtility.SetDirty(set);
            return set;
        }

        private static GameObject EnsureSampleAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            var temp = new GameObject("Service Asset");
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, AssetPath);
            UnityEngine.Object.DestroyImmediate(temp);
            return prefab;
        }

        private static SceneAsset LoadScene(string contentPath, string ownerSet)
        {
            var s = AssetDatabase.LoadAssetAtPath<SceneAsset>(contentPath);
            if (s == null)
            {
                Debug.LogError($"[WorldSetup] Content scene '{contentPath}' missing — set '{ownerSet}' will be empty. Run step 1 first.");
            }

            return s;
        }

        private static void AddToBuildSettingsFirst(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
