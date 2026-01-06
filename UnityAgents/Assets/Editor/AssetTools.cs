using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityAgents.Editor
{
    /// <summary>
    /// Tool that lists assets in the project, optionally filtered by type.
    /// </summary>
    public class ListAssetsTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "list_assets",
                description = "Lists assets in the Unity project. Can filter by type (e.g., 'Material', 'Prefab', 'Texture2D', 'Script', 'ScriptableObject'). Returns asset paths and names."
            }
            .AddParameter("asset_type", "string", "Filter by asset type (optional). Examples: 'Material', 'Prefab', 'Texture2D', 'MonoScript', 'ScriptableObject'. Leave empty for all assets.", required: false)
            .AddParameter("search_path", "string", "Search within specific folder path (e.g., 'Assets/Materials'). Leave empty to search all assets.", required: false)
            .AddParameter("max_results", "number", "Maximum number of results to return (default: 50)", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string assetType = json["asset_type"]?.Value ?? "";
                string searchPath = json["search_path"]?.Value ?? "Assets";
                int maxResults = json["max_results"]?.AsInt ?? 50;

                string[] guids;
                
                if (!string.IsNullOrEmpty(assetType))
                {
                    // Search for specific type
                    string filter = $"t:{assetType}";
                    guids = AssetDatabase.FindAssets(filter, new[] { searchPath });
                }
                else
                {
                    // Search for all assets
                    guids = AssetDatabase.FindAssets("", new[] { searchPath });
                }

                if (guids.Length == 0)
                {
                    return $"No assets found{(!string.IsNullOrEmpty(assetType) ? $" of type '{assetType}'" : "")} in '{searchPath}'";
                }

                string result = $"Found {guids.Length} asset(s){(!string.IsNullOrEmpty(assetType) ? $" of type '{assetType}'" : "")}:\n";
                
                int count = 0;
                foreach (string guid in guids)
                {
                    if (count >= maxResults)
                    {
                        result += $"\n... and {guids.Length - count} more (limited to {maxResults} results)";
                        break;
                    }

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    
                    if (asset != null)
                    {
                        result += $"\n- {asset.name} ({asset.GetType().Name})";
                        result += $"\n  Path: {path}";
                        count++;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Failed to list assets: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tool that creates or updates C# script assets.
    /// </summary>
    public class CreateScriptAssetTool : IAgentTool
    {
        public static List<string> GetCurrentErrors()
        {
            var logEntries = new List<string>();
            
            // Use reflection to access internal Unity console API
            var consoleWindowType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
            var logEntriesType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            
            if (logEntriesType != null)
            {
                var getCountMethod = logEntriesType.GetMethod("GetCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (getCountMethod != null && startGettingEntriesMethod != null && getEntryMethod != null)
                {
                    int count = (int)getCountMethod.Invoke(null, null);
                    startGettingEntriesMethod.Invoke(null, null);
                    
                    var logEntry = System.Activator.CreateInstance(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.LogEntry"));
                    
                    for (int i = 0; i < count; i++)
                    {
                        if ((bool)getEntryMethod.Invoke(null, new object[] { i, logEntry }))
                        {
                            var messageField = logEntry.GetType().GetField("message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var fileField = logEntry.GetType().GetField("file", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var lineField = logEntry.GetType().GetField("line", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var modeField = logEntry.GetType().GetField("mode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            
                            if (messageField != null && fileField != null)
                            {
                                string message = messageField.GetValue(logEntry) as string;
                                string file = fileField.GetValue(logEntry) as string;
                                int line = lineField != null ? (int)lineField.GetValue(logEntry) : 0;
                                int mode = modeField != null ? (int)modeField.GetValue(logEntry) : 0;
                                
                                // Mode: 1 = Error, 2 = Warning
                                if (mode == 1 || mode == 2)
                                {
                                    logEntries.Add($"{file}({line}): {message}");
                                }
                            }
                        }
                    }
                    
                    if (endGettingEntriesMethod != null)
                    {
                        endGettingEntriesMethod.Invoke(null, null);
                    }
                }
            }    

            return logEntries;      
        }

        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "create_script_asset",
                description = "Creates or updates a C# script file in the Unity project. Provide the script name and C# code content. The tool will compile the script and return any compilation errors. Scripts should not be added to complete one-off requests but only if the user asks to add logic to the project."
            }
            .AddParameter("script_name", "string", "Name of the script (without .cs extension)", required: true)
            .AddParameter("code", "string", "The C# code content for the script", required: true)
            .AddParameter("folder_path", "string", "Folder path within Assets (e.g., 'Scripts' or 'Scripts/Tools'). Default: 'Assets'", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string scriptName = json["script_name"]?.Value;
                string code = json["code"]?.Value;
                string folderPath = json["folder_path"]?.Value ?? "Assets";

                if (string.IsNullOrEmpty(scriptName))
                {
                    return "Error: script_name is required";
                }

                if (string.IsNullOrEmpty(code))
                {
                    return "Error: code is required";
                }

                // Ensure script name ends with .cs
                if (!scriptName.EndsWith(".cs"))
                {
                    scriptName += ".cs";
                }

                // Ensure folder path starts with Assets
                if (!folderPath.StartsWith("Assets"))
                {
                    folderPath = "Assets/" + folderPath;
                }

                // Create directory if it doesn't exist
                string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Substring(7)); // Remove "Assets/" prefix
                if (!Directory.Exists(fullFolderPath))
                {
                    Directory.CreateDirectory(fullFolderPath);
                }

                // Write the script file
                string scriptPath = Path.Combine(folderPath, scriptName);
                string fullPath = Path.Combine(Application.dataPath, scriptPath.Substring(7));
                
                File.WriteAllText(fullPath, code);     
                
                // Refresh the asset database (triggers compilation)
                AssetDatabase.Refresh();
                
                // Build response
                string response = $"Successfully created script '{scriptName}' at '{scriptPath}'";     
                return response;
            }
            catch (Exception ex)
            {
                return $"Failed to create script: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tool that creates or updates material assets.
    /// </summary>
    public class CreateMaterialAssetTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "create_material_asset",
                description = "Creates or updates a Material asset. Provide material name and properties as key-value pairs. Common properties: _Color (color as 'r,g,b,a'), _MainTex (texture name), _Metallic (0-1), _Glossiness (0-1)."
            }
            .AddParameter("material_name", "string", "Name of the material", required: true)
            .AddParameter("shader_name", "string", "Shader name (e.g., 'Standard', 'Unlit/Color'). Default: 'Standard'", required: false)
            .AddParameter("properties", "string", "JSON string of property key-value pairs (e.g., '{\"_Color\":\"1,0,0,1\",\"_Metallic\":\"0.5\"}')", required: false)
            .AddParameter("folder_path", "string", "Folder path within Assets (default: 'Assets/Materials')", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string materialName = json["material_name"]?.Value;
                string shaderName = json["shader_name"]?.Value ?? "Standard";
                string propertiesJson = json["properties"]?.Value ?? "{}";
                string folderPath = json["folder_path"]?.Value ?? "Assets/Materials";

                if (string.IsNullOrEmpty(materialName))
                {
                    return "Error: material_name is required";
                }

                // Ensure folder path starts with Assets
                if (!folderPath.StartsWith("Assets"))
                {
                    folderPath = "Assets/" + folderPath;
                }

                // Create directory if it doesn't exist
                string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Substring(7));
                if (!Directory.Exists(fullFolderPath))
                {
                    Directory.CreateDirectory(fullFolderPath);
                }

                string materialPath = Path.Combine(folderPath, materialName + ".mat");
                
                // Check if material already exists
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                bool isUpdate = material != null;

                if (material == null)
                {
                    // Create new material
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        return $"Error: Shader '{shaderName}' not found";
                    }
                    material = new Material(shader);
                }

                // Parse and apply properties
                var propertiesNode = SimpleJSON.JSONNode.Parse(propertiesJson);
                var propertiesClass = propertiesNode as SimpleJSON.JSONClass;
                
                if (propertiesClass != null)
                {
                    foreach (System.Collections.Generic.KeyValuePair<string, SimpleJSON.JSONNode> kvp in propertiesClass)
                    {
                        string propName = kvp.Key;
                        string propValue = kvp.Value?.Value;

                        if (string.IsNullOrEmpty(propValue))
                            continue;

                        // Try to determine property type and set accordingly
                        if (propName.Contains("Color") || propValue.Contains(","))
                        {
                            // Parse as color (r,g,b,a)
                            string[] parts = propValue.Split(',');
                            if (parts.Length >= 3)
                            {
                                float r = float.Parse(parts[0]);
                                float g = float.Parse(parts[1]);
                                float b = float.Parse(parts[2]);
                                float a = parts.Length > 3 ? float.Parse(parts[3]) : 1f;
                                material.SetColor(propName, new Color(r, g, b, a));
                            }
                        }
                        else if (propName.Contains("Tex") || propName.Contains("Map"))
                        {
                            // Try to load texture
                            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"Assets/{propValue}");
                            if (texture != null)
                            {
                                material.SetTexture(propName, texture);
                            }
                        }
                        else if (float.TryParse(propValue, out float floatValue))
                        {
                            // Set as float
                            material.SetFloat(propName, floatValue);
                        }
                    }
                }

                if (!isUpdate)
                {
                    // Create new asset
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else
                {
                    // Mark existing asset as dirty
                    EditorUtility.SetDirty(material);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"Successfully {(isUpdate ? "updated" : "created")} material '{materialName}' at '{materialPath}'";
            }
            catch (Exception ex)
            {
                return $"Failed to create/update material: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tool that creates texture assets from base64-encoded PNG data.
    /// </summary>
    public class CreateTextureAssetTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "create_texture_asset",
                description = "Creates a Texture2D asset from base64-encoded PNG data. The texture will be saved as a .png file in the project."
            }
            .AddParameter("texture_name", "string", "Name of the texture (without extension)", required: true)
            .AddParameter("base64_data", "string", "Base64-encoded PNG image data", required: true)
            .AddParameter("folder_path", "string", "Folder path within Assets (default: 'Assets/Textures')", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string textureName = json["texture_name"]?.Value;
                string base64Data = json["base64_data"]?.Value;
                string folderPath = json["folder_path"]?.Value ?? "Assets/Textures";

                if (string.IsNullOrEmpty(textureName))
                {
                    return "Error: texture_name is required";
                }

                if (string.IsNullOrEmpty(base64Data))
                {
                    return "Error: base64_data is required";
                }

                // Ensure folder path starts with Assets
                if (!folderPath.StartsWith("Assets"))
                {
                    folderPath = "Assets/" + folderPath;
                }

                // Create directory if it doesn't exist
                string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Substring(7));
                if (!Directory.Exists(fullFolderPath))
                {
                    Directory.CreateDirectory(fullFolderPath);
                }

                // Decode base64 data
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                // Write PNG file
                string texturePath = Path.Combine(folderPath, textureName + ".png");
                string fullPath = Path.Combine(Application.dataPath, texturePath.Substring(7));
                File.WriteAllBytes(fullPath, imageBytes);

                // Refresh asset database and import texture
                AssetDatabase.Refresh();

                // Set texture import settings (optional)
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.SaveAndReimport();
                }

                return $"Successfully created texture '{textureName}' at '{texturePath}'";
            }
            catch (Exception ex)
            {
                return $"Failed to create texture: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tool that creates prefab assets from GameObjects in the scene.
    /// </summary>
    public class CreatePrefabAssetTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "create_prefab_asset",
                description = "Creates a prefab asset from a GameObject in the current scene. The GameObject must exist in the scene."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject in the scene to convert to prefab", required: true)
            .AddParameter("prefab_name", "string", "Name for the prefab (optional, uses GameObject name if not provided)", required: false)
            .AddParameter("folder_path", "string", "Folder path within Assets (default: 'Assets/Prefabs')", required: false)
            .AddParameter("replace_existing", "string", "Whether to replace existing prefab ('true' or 'false', default: 'false')", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                string prefabName = json["prefab_name"]?.Value;
                string folderPath = json["folder_path"]?.Value ?? "Assets/Prefabs";
                bool replaceExisting = json["replace_existing"]?.Value?.ToLower() == "true";

                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return "Error: game_object_name is required";
                }

                // Find the GameObject in the scene
                GameObject sourceObject = GameObject.Find(gameObjectName);
                if (sourceObject == null)
                {
                    return $"Error: GameObject '{gameObjectName}' not found in the scene";
                }

                // Use GameObject name if prefab name not provided
                if (string.IsNullOrEmpty(prefabName))
                {
                    prefabName = sourceObject.name;
                }

                // Ensure folder path starts with Assets
                if (!folderPath.StartsWith("Assets"))
                {
                    folderPath = "Assets/" + folderPath;
                }

                // Create directory if it doesn't exist
                string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Substring(7));
                if (!Directory.Exists(fullFolderPath))
                {
                    Directory.CreateDirectory(fullFolderPath);
                }

                string prefabPath = Path.Combine(folderPath, prefabName + ".prefab");

                // Check if prefab already exists
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    if (!replaceExisting)
                    {
                        return $"Error: Prefab already exists at '{prefabPath}'. Set replace_existing to 'true' to overwrite.";
                    }
                }

                // Create or replace the prefab
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sourceObject, prefabPath);

                if (prefab == null)
                {
                    return $"Failed to create prefab at '{prefabPath}'";
                }

                return $"Successfully created prefab '{prefabName}' at '{prefabPath}'";
            }
            catch (Exception ex)
            {
                return $"Failed to create prefab: {ex.Message}";
            }
        }
    }
}
