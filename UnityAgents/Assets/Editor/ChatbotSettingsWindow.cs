using UnityEditor;
using UnityEngine;

namespace UnityAgents.Editor
{
    public class ChatbotSettingsWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private ChatbotSettings settings;
        private bool showApiKey = false;

        public static void ShowWindow()
        {
            var window = GetWindow<ChatbotSettingsWindow>("Chatbot Settings");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            settings = ChatbotSettings.Instance;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("OpenAI Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key", GUILayout.Width(100));
            
            if (showApiKey)
            {
                settings.apiKey = EditorGUILayout.TextField(settings.apiKey);
            }
            else
            {
                EditorGUILayout.TextField(new string('*', settings.apiKey.Length));
            }
            
            showApiKey = GUILayout.Toggle(showApiKey, showApiKey ? "Hide" : "Show", "Button", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Get your API key from: https://platform.openai.com/api-keys", MessageType.Info);

            EditorGUILayout.Space(10);

            settings.apiEndpoint = EditorGUILayout.TextField("API Endpoint", settings.apiEndpoint);
            settings.model = EditorGUILayout.TextField("Model", settings.model);
            
            EditorGUILayout.Space(10);
            
            settings.temperature = EditorGUILayout.Slider("Temperature", settings.temperature, 0f, 2f);
            EditorGUILayout.HelpBox("Lower values make output more focused and deterministic. Higher values make it more creative.", MessageType.None);
            
            EditorGUILayout.Space(5);
            
            settings.maxTokens = EditorGUILayout.IntSlider("Max Tokens", settings.maxTokens, 100, 4000);
            EditorGUILayout.HelpBox("Maximum number of tokens in the response. Higher values allow longer responses but cost more.", MessageType.None);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Tool Calling", EditorStyles.boldLabel);
            
            settings.enableTools = EditorGUILayout.Toggle("Enable Tool Calling", settings.enableTools);
            
            if (settings.enableTools)
            {
                EditorGUILayout.HelpBox("Tool calling allows the chatbot to execute Unity functions. Implement IAgentTool interface to create custom tools.", MessageType.Info);
                
                EditorGUILayout.Space(5);
                
                settings.autoExecuteTools = EditorGUILayout.Toggle("Auto Execute Tools", settings.autoExecuteTools);
                EditorGUILayout.HelpBox("When enabled, tools are automatically executed and results are sent back to the AI.", MessageType.None);
                
                EditorGUILayout.Space(10);
                
                // Show discovered tools
                var tools = AgentToolRegistry.GetAllTools();
                
                EditorGUILayout.LabelField($"Discovered Tools: {tools.Count}", EditorStyles.boldLabel);
                
                if (tools.Count > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foreach (var tool in tools.Values)
                    {
                        var spec = tool.GetToolSpec();
                        EditorGUILayout.LabelField($"• {spec.name}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"  {spec.description}", EditorStyles.wordWrappedMiniLabel);
                        if (spec.parameters.Count > 0)
                        {
                            EditorGUILayout.LabelField($"  Parameters: {string.Join(", ", spec.parameters.Keys)}", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.Space(5);
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("No tools found. Implement IAgentTool in your code to create tools.", MessageType.Warning);
                }
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Refresh Tool List"))
                {
                    AgentToolRegistry.RefreshTools();
                    Repaint();
                }
                
                if (GUILayout.Button("Show Example Tool"))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Editor/ExampleTools.cs");
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
            {
                settings.SaveToEditorPrefs();
                EditorUtility.DisplayDialog("Settings Saved", "Chatbot settings have been saved successfully.", "OK");
            }
            
            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings", 
                    "Are you sure you want to reset all settings to defaults?", 
                    "Reset", "Cancel"))
                {
                    settings.apiEndpoint = "https://api.openai.com/v1/chat/completions";
                    settings.model = "gpt-4";
                    settings.temperature = 0.7f;
                    settings.maxTokens = 2000;
                    settings.enableTools = false;
                    settings.SaveToEditorPrefs();
                }
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }
    }
}
