using System.IO;
using DFIN.VoiceAgent.Configuration;
using UnityEditor;
using UnityEngine;

namespace DFIN.VoiceAgent.Editor
{
    internal static class VoiceAgentSettingsEditorUtility
    {
        public static VoiceAgentSettings FindSettingsAsset()
        {
            var direct = AssetDatabase.LoadAssetAtPath<VoiceAgentSettings>(VoiceAgentSettings.AssetPath);
            if (direct != null)
            {
                return direct;
            }

            var guids = AssetDatabase.FindAssets("t:VoiceAgentSettings");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<VoiceAgentSettings>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        public static VoiceAgentSettings CreateSettingsAsset()
        {
            EnsureFolders();

            var existing = AssetDatabase.LoadAssetAtPath<VoiceAgentSettings>(VoiceAgentSettings.AssetPath);
            if (existing != null)
            {
                Debug.Log($"VoiceAgentSettings asset already exists at {VoiceAgentSettings.AssetPath}");
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<VoiceAgentSettings>();
            AssetDatabase.CreateAsset(settings, VoiceAgentSettings.AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static void EnsureFolders()
        {
            var resourcesFolder = VoiceAgentSettings.ResourcesFolder;
            var parts = resourcesFolder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Length == 0)
            {
                return;
            }

            var currentPath = parts[0];
            if (!AssetDatabase.IsValidFolder(currentPath))
            {
                Debug.LogError($"Cannot create VoiceAgent folders because base folder '{currentPath}' is missing.");
                return;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                var parent = string.Join("/", parts, 0, i);
                var child = parts[i];
                var target = $"{parent}/{child}";
                if (!AssetDatabase.IsValidFolder(target))
                {
                    AssetDatabase.CreateFolder(parent, child);
                }
            }
        }
    }
}

