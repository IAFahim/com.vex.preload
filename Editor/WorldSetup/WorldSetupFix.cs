// <copyright file="WorldSetupFix.cs" company="Vex">
//     Copyright (c) Vex. All rights reserved.
// </copyright>

namespace Vex.WorldSetup.Editor
{
    using BovineLabs.Core.Editor.Settings;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// One-shot repair for the two defects the builder left behind (confirmed by the SubScenes audit):
    ///   SL-3: Start.unity had no 'Required In Scene' host (EnsureHost skipped because Camera.main resolved from
    ///         another open scene) -> Play rendered black/silent. Fix: open Start ALONE (Camera.main == null) and
    ///         inject the host prefab.
    ///   SL-1: 'Required In Subscene' was injected into Service World.unity too, giving the ServiceWorld a redundant
    ///         CameraMain/PhysicsStep/AudioSources. Fix: strip it; ServiceWorld content stays empty until a service
    ///         system needs authoring. (Game World keeps the full content prefab.)
    ///   SL-6: EditorSettings.startupScene was null on disk. Fix: re-point it at Start.
    /// </summary>
    public static class WorldSetupFix
    {
        private const string Dir = "Assets/Scenes";
        private const string StartPath = Dir + "/Start.unity";
        private const string ServicePath = Dir + "/Service World.unity";
        private const string HostGuid = "4a8e5ea86a1ae36e48a49b00e6737b80"; // Required In Scene
        private const string SubGuid = "8c402a07a40def99f9bfdf9ef67b3712";  // Required In Subscene

        [MenuItem("BovineLabs/Vex/World Setup/4 - Fix Audit Issues")]
        public static void FixAuditIssues()
        {
            var openSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // SL-3: host into Start (single mode => Camera.main is null => guaranteed inject).
                var start = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Single);
                if (!HasInstanceOf(start, HostGuid))
                {
                    var host = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(HostGuid));
                    PrefabUtility.InstantiatePrefab(host, start);
                    EditorSceneManager.MarkSceneDirty(start);
                    EditorSceneManager.SaveScene(start);
                    Debug.Log("[WorldSetupFix] SL-3: injected 'Required In Scene' host into Start.unity.");
                }

                // SL-1: strip the content prefab from Service World.
                var svc = EditorSceneManager.OpenScene(ServicePath, OpenSceneMode.Single);
                var removed = 0;
                foreach (var go in svc.GetRootGameObjects())
                {
                    var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.AssetPathToGUID(path) == SubGuid)
                    {
                        Object.DestroyImmediate(go);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(svc);
                    EditorSceneManager.SaveScene(svc);
                    Debug.Log($"[WorldSetupFix] SL-1: removed {removed} 'Required In Subscene' instance(s) from Service World.unity.");
                }

                // (SL-6 removed: setting EditorSettings.startupScene hijacks Play in every scene. Leave it empty.)
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(openSetup);
            }
        }

        private static bool HasInstanceOf(UnityEngine.SceneManagement.Scene scene, string guid)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.AssetPathToGUID(path) == guid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
