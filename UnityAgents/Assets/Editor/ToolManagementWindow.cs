using UnityEditor;
using UnityEngine;

namespace UnityAgents.Editor
{
    /// <summary>
    /// Window for browsing and testing discovered agent tools.
    /// </summary>
    public class ToolManagementWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private IAgentTool selectedTool;
        private string testArguments = "{}";

        public static void ShowWindow()
        {
            var window = GetWindow<ToolManagementWindow>("Agent Tools Browser");
            window.minSize = new Vector2(600, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Agent Tools Browser", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Browse and test tools discovered from IAgentTool implementations.", MessageType.Info);
            
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Refresh Tool List", GUILayout.Height(30)))
            {
                AgentToolRegistry.RefreshTools();
                Repaint();
            }

            EditorGUILayout.Space(10);

            var tools = AgentToolRegistry.GetAllTools();

            if (tools.Count == 0)
            {
                EditorGUILayout.HelpBox("No tools found. Implement IAgentTool interface to create tools.\n\nSee ExampleTools.cs for examples.", MessageType.Warning);
                
                if (GUILayout.Button("Open Example Tools", GUILayout.Height(30)))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Editor/ExampleTools.cs");
                    AssetDatabase.OpenAsset(script);
                }
                
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField($"Discovered Tools: {tools.Count}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            foreach (var tool in tools.Values)
            {
                DrawToolInfo(tool);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolInfo(IAgentTool tool)
        {
            var spec = tool.GetToolSpec();
            bool isSelected = selectedTool == tool;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
            if (GUILayout.Button(spec.name, EditorStyles.boldLabel, GUILayout.Height(25)))
            {
                selectedTool = isSelected ? null : tool;
                if (isSelected)
                {
                    testArguments = "{}";
                }
                else
                {
                    // Generate sample arguments
                    testArguments = GenerateSampleArguments(spec);
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();

            // Description
            EditorGUILayout.LabelField(spec.description, EditorStyles.wordWrappedLabel);

            // Parameters
            if (spec.parameters.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Parameters:", EditorStyles.miniBoldLabel);
                
                foreach (var param in spec.parameters)
                {
                    bool isRequired = spec.requiredParameters.Contains(param.Key);
                    string requiredStr = isRequired ? " (required)" : " (optional)";
                    
                    EditorGUILayout.LabelField(
                        $"  • {param.Key} ({param.Value.type}){requiredStr}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"    {param.Value.description}",
                        EditorStyles.wordWrappedMiniLabel
                    );
                }
            }

            // Test section for selected tool
            if (isSelected)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Test Tool", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField("Arguments (JSON):", EditorStyles.miniLabel);
                testArguments = EditorGUILayout.TextArea(testArguments, GUILayout.Height(60));
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Execute", GUILayout.Height(30)))
                {
                    TestTool(tool, testArguments);
                }
                
                if (GUILayout.Button("Reset", GUILayout.Height(30)))
                {
                    testArguments = GenerateSampleArguments(spec);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private string GenerateSampleArguments(ToolSpec spec)
        {
            if (spec.parameters.Count == 0)
            {
                return "{}";
            }

            string json = "{\n";
            int count = 0;
            
            foreach (var param in spec.parameters)
            {
                if (count > 0) json += ",\n";
                
                string sampleValue = param.Value.type switch
                {
                    "string" => "\"sample_value\"",
                    "number" => "0",
                    "boolean" => "true",
                    _ => "\"\""
                };
                
                json += $"  \"{param.Key}\": {sampleValue}";
                count++;
            }
            
            json += "\n}";
            return json;
        }

        private void TestTool(IAgentTool tool, string arguments)
        {
            try
            {
                Debug.Log($"Testing tool: {tool.GetToolSpec().name}");
                Debug.Log($"Arguments: {arguments}");
                
                string result = tool.Execute(arguments);
                
                Debug.Log($"Result: {result}");
                EditorUtility.DisplayDialog("Tool Test Result", result, "OK");
            }
            catch (System.Exception ex)
            {
                string error = $"Error testing tool: {ex.Message}";
                Debug.LogError(error);
                EditorUtility.DisplayDialog("Tool Test Failed", error, "OK");
            }
        }
    }
}
