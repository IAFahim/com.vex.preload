using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Vex.Preload.Editor
{
    /// <summary>
    /// One-call creation of a fully bootstrapped, play-clean scene for programmatic builders
    /// (showcases, tooling, AI). Instances the project's Required In Scene host (camera + CinemachineBrain
    /// + light + inputs) and a paired Required In Subscene (which bakes the ~11 settings singletons —
    /// NavMesh, Audio, Essence, Physics… — that, when missing, make DOTS systems throw every frame).
    ///
    /// The host OWNS rendering: it already provides a Main Camera, an AudioListener and a CinemachineBrain.
    /// Callers MUST NOT add their own camera / AudioListener / Brain — add gameplay content into the open
    /// subscene (<see cref="BootstrapScene.Sub"/>) and point the camera with <see cref="PointCameraAtTarget"/>.
    ///
    /// ponytail: thin wrapper over the existing PreloadInjector — it reuses EnsureHost/EnsureContent rather
    /// than re-deriving the bootstrap, so it can't drift from the auto-injection path.
    /// </summary>
    public static class PreloadSceneBuilder
    {
        /// <summary>The two open scenes a bootstrapped build produces. Add content into <see cref="Sub"/>, then call <see cref="Save"/>.</summary>
        public readonly struct BootstrapScene
        {
            public readonly Scene Host;
            public readonly Scene Sub;
            public readonly SubScene SubSceneComponent;

            /// <summary>The instanced "Required In Subscene" root. Its child holds the Cinemachine vcam.</summary>
            public readonly GameObject ContentRoot;

            public BootstrapScene(Scene host, Scene sub, SubScene subScene, GameObject contentRoot)
            {
                this.Host = host;
                this.Sub = sub;
                this.SubSceneComponent = subScene;
                this.ContentRoot = contentRoot;
            }

            public bool IsValid => this.Host.IsValid() && this.Sub.IsValid();
        }

        /// <summary>
        /// Builds a host scene + paired content subscene, both left OPEN (host single, sub additive) so the
        /// caller can add gameplay objects into <see cref="BootstrapScene.Sub"/>. Works under any folder,
        /// including the auto-injection-excluded Assets/Samples, because it injects explicitly.
        /// </summary>
        /// <param name="hostScenePath">Asset path for the host scene, e.g. "Assets/Samples/Foo/Foo.unity".</param>
        /// <param name="subScenePath">Asset path for the content subscene, e.g. "Assets/Samples/Foo/Foo_Sub.unity".</param>
        /// <param name="target">Optional object ALREADY in the subscene to aim the camera at; usually null here — add content first, then call <see cref="PointCameraAtTarget"/>.</param>
        public static BootstrapScene CreateBootstrappedScene(string hostScenePath, string subScenePath, GameObject target = null)
        {
            var settings = PreloadSettings.Instance;
            if (settings.hostPrefab == null || settings.contentPrefab == null)
            {
                Debug.LogError("[Preload] hostPrefab/contentPrefab not assigned in Project Settings > Vex > Preload — cannot build a bootstrapped scene.");
                return default;
            }

            // Suppress the InitializeOnLoad scene hooks for the whole build so they can't race the explicit
            // injection (e.g. dumping a host prefab into the content subscene). Set BEFORE the first NewScene.
            var wasEnabled = settings.enabled;
            settings.enabled = false;
            try
            {
                // Host: fresh single scene, inject the managed bootstrap, save so it owns an asset path.
                var host = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PreloadInjector.EnsureHost(host);
                if (!PreloadInjector.HasHost(host))
                {
                    Debug.LogError("[Preload] Failed to inject the host bootstrap.");
                    return default;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(hostScenePath)));
                EditorSceneManager.SaveScene(host, hostScenePath);

                // Subscene: inject the baked-settings content prefab; capture its instanced root.
                var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                PreloadInjector.EnsureContent(sub);
                if (!PreloadInjector.HasContent(sub))
                {
                    Debug.LogError("[Preload] Failed to inject the subscene content (settings singletons).");
                    return default;
                }

                var contentRoot = FindContentRoot(sub, settings.contentPrefab);
                EditorSceneManager.SaveScene(sub, subScenePath);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);

                // Link host -> subscene via the PUBLIC setters (never FindProperty("_SceneAsset")).
                var go = new GameObject(Path.GetFileNameWithoutExtension(subScenePath));
                SceneManager.MoveGameObjectToScene(go, host);
                var subScene = go.AddComponent<SubScene>();
                subScene.SceneAsset = asset;
                subScene.AutoLoadScene = true;
                EditorSceneManager.MarkSceneDirty(host);

                var result = new BootstrapScene(host, sub, subScene, contentRoot);

                if (target != null && target.scene == sub)
                {
                    PointCameraAtTarget(result, target);
                }

                return result;
            }
            finally
            {
                settings.enabled = wasEnabled;
            }
        }

        /// <summary>
        /// Aims the bootstrap Cinemachine camera at <paramref name="target"/> (a GameObject in the subscene).
        /// No-op returning false when Cinemachine is absent. The target must bake to an entity with
        /// LocalToWorld (i.e. be renderable / dynamic) — a bare empty GameObject bakes transform-less and the
        /// camera will sit at the origin.
        /// </summary>
        public static bool PointCameraAtTarget(in BootstrapScene scene, GameObject target)
        {
            if (target == null || scene.ContentRoot == null) return false;

#if UNITY_CINEMACHINE
            var vcam = scene.ContentRoot.GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                Debug.LogWarning("[Preload] No CinemachineCamera found under the content root — camera not aimed.");
                return false;
            }

            if (target.GetComponentInChildren<Renderer>() == null && target.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"[Preload] Camera target '{target.name}' has no Renderer/Collider; it may bake without LocalToWorld and the camera won't follow it. Give it a visible mesh or a physics body.");
            }

            // CinemachineCamera.Target is a value-type field — read, modify, write back.
            var t = vcam.Target;
            t.TrackingTarget = target.transform;
            vcam.Target = t;
            EditorUtility.SetDirty(vcam);
            return true;
#else
            Debug.LogWarning("[Preload] Cinemachine not installed (UNITY_CINEMACHINE undefined) — camera not aimed.");
            return false;
#endif
        }

        /// <summary>
        /// Saves both scenes and forces the subscene to re-import/re-bake so caller-added content is picked up.
        /// Also warns if the caller mistakenly added a second camera/AudioListener (the host owns those).
        /// </summary>
        public static void Save(in BootstrapScene scene)
        {
            if (!scene.IsValid) return;

            WarnOnDuplicateRendering(scene);

            EditorSceneManager.SaveScene(scene.Sub);
            EditorSceneManager.SaveScene(scene.Host);
            AssetDatabase.SaveAssets();
            // ponytail: programmatic content edits don't re-bake an open subscene without an explicit import.
            AssetDatabase.ImportAsset(scene.Sub.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static GameObject FindContentRoot(Scene sub, GameObject contentPrefab)
        {
            var contentPath = AssetDatabase.GetAssetPath(contentPrefab);
            foreach (var root in sub.GetRootGameObjects())
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (src != null && AssetDatabase.GetAssetPath(src) == contentPath) return root;
            }

            return null;
        }

        private static void WarnOnDuplicateRendering(in BootstrapScene scene)
        {
            int cameras = 0, listeners = 0;
            foreach (var s in new[] { scene.Host, scene.Sub })
            {
                foreach (var root in s.GetRootGameObjects())
                {
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                    listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                }
            }

            if (cameras > 1)
                Debug.LogWarning($"[Preload] {cameras} cameras in the bootstrapped scene — the host already provides one. Remove extra cameras (Camera.main becomes ambiguous).");
            if (listeners > 1)
                Debug.LogWarning($"[Preload] {listeners} AudioListeners — the host already provides one. Remove the extras to silence Unity's multiple-listener warning.");
        }
    }
}
