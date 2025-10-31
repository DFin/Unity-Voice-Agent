using DFIN.VoiceAgent.Configuration;
using UnityEditor;
using UnityEngine;

namespace DFIN.VoiceAgent.Editor
{
    public class VoiceAgentSettingsWindow : EditorWindow
    {
        private const string WindowTitle = "Voice Agent Settings";

        private VoiceAgentSettings settings;
        private SerializedObject serializedSettings;

        private Vector2 scrollPosition;

        [MenuItem("Voice Agent/Settings", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<VoiceAgentSettingsWindow>(false, WindowTitle, true);
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            ReloadAssetReference();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voice Agent Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("API keys are stored in plain text for rapid prototyping. Do not ship production secrets.", MessageType.Warning);

            DrawAssetControls();

            if (settings == null)
            {
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            serializedSettings.Update();

            var openAiProperty = serializedSettings.FindProperty("openAi");
            var elevenLabsProperty = serializedSettings.FindProperty("elevenLabs");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(openAiProperty, new GUIContent("OpenAI Realtime"), true);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(elevenLabsProperty, new GUIContent("ElevenLabs (Phase 2)"), true);
            }

            var applied = serializedSettings.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();

            if (applied)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawAssetControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Settings Asset", GUILayout.Width(100f));

                if (settings != null)
                {
                    EditorGUILayout.ObjectField(settings, typeof(VoiceAgentSettings), false);
                }
                else
                {
                    EditorGUILayout.LabelField("Not found", EditorStyles.label);
                }

                if (GUILayout.Button("Locate"))
                {
                    ReloadAssetReference();
                    if (settings != null)
                    {
                        EditorGUIUtility.PingObject(settings);
                    }
                    else
                    {
                        ShowNotification(new GUIContent("Settings asset not found. Create one below."));
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create / Replace", GUILayout.Width(160f)))
                {
                    var created = VoiceAgentSettingsEditorUtility.CreateSettingsAsset();
                    if (created != null)
                    {
                        settings = created;
                        serializedSettings = new SerializedObject(settings);
                        EditorGUIUtility.PingObject(settings);
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void ReloadAssetReference()
        {
            settings = VoiceAgentSettingsEditorUtility.FindSettingsAsset();
            serializedSettings = settings != null ? new SerializedObject(settings) : null;
        }
    }
}
