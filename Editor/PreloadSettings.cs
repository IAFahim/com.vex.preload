using System;
using System.Collections.Generic;
using BovineLabs.Core.Editor.Settings;
using BovineLabs.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Vex.Preload.Editor
{
    [SettingsGroup("Vex")]
    public sealed class PreloadSettings : ScriptableObject, ISettings
    {
        private const string HostPrefabGuid = "4a8e5ea86a1ae36e48a49b00e6737b80";
        private const string ContentPrefabGuid = "8c402a07a40def99f9bfdf9ef67b3712";

        [Tooltip("When off, Preload does nothing: no auto-injection, no play-time wrapping or repair.")]
        public bool enabled = true;

        [Tooltip("Injected into host scenes that are missing it (Required In Scene).")]
        public GameObject hostPrefab;

        [Tooltip("Marks a scene as subscene content (Required In Subscene).")]
        public GameObject contentPrefab;

        [Tooltip("Scenes whose path starts with any of these are ignored.")]
        public List<string> excludeFolders = new() { "Packages", "Assets/Samples" };

        public static PreloadSettings Instance => EditorSettingsUtility.GetSettings<PreloadSettings>();

        private void OnEnable()
        {
            if (hostPrefab == null) hostPrefab = LoadPrefab(HostPrefabGuid);

            if (contentPrefab == null) contentPrefab = LoadPrefab(ContentPrefabGuid);
        }

        public bool IsExcluded(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return false;

            foreach (var folder in excludeFolders)
            {
                if (string.IsNullOrEmpty(folder)) continue;

                var trimmed = folder.TrimEnd('/');
                if (scenePath == trimmed || scenePath.StartsWith(trimmed + "/", StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static GameObject LoadPrefab(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}