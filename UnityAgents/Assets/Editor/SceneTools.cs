using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityAgents.Editor
{
    /// <summary>
    /// Helper utilities for scene manipulation tools.
    /// </summary>
    internal static class SceneToolHelpers
    {
        /// <summary>
        /// Finds a GameObject by name and throws an exception if not found.
        /// </summary>
        public static GameObject FindGameObject(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject == null)
            {
                throw new ArgumentException($"GameObject '{name}' not found");
            }
            return gameObject;
        }

        /// <summary>
        /// Resolves a component type by name, trying both with and without UnityEngine namespace.
        /// </summary>
        public static Type ResolveComponentType(string typeName)
        {
            var componentType = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name.Contains(typeName));

            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"'{typeName}' is not a valid Component type");
            }

            return componentType;
        }

        /// <summary>
        /// Gets a component from a GameObject and throws an exception if not found.
        /// </summary>
        public static Component GetComponent(GameObject gameObject, Type componentType)
        {
            Component component = gameObject.GetComponent(componentType);
            if (component == null)
            {
                throw new ArgumentException($"Component '{componentType.Name}' not found on GameObject '{gameObject.name}'");
            }
            return component;
        }

        /// <summary>
        /// Formats GameObject information as a string (used by multiple tools).
        /// </summary>
        public static string FormatGameObjectInfo(GameObject obj)
        {
            string info = $"- {obj.name} at position {obj.transform.position}";
            var components = obj.GetComponents<Component>();
            info += $"\n  Components: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}";
            return info;
        }

        /// <summary>
        /// Wraps tool execution with try-catch and returns formatted error messages.
        /// </summary>
        public static string SafeExecute(Func<string> action, string operationName)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return $"Failed to {operationName}: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets a serialized property value as a string.
        /// </summary>
        public static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString();
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum:
                    return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.ToString();
                default:
                    return prop.propertyType.ToString();
            }
        }

        /// <summary>
        /// Parses a Vector3 from a comma-separated string.
        /// </summary>
        public static Vector3 ParseVector3(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length != 3)
            {
                throw new FormatException($"Vector3 must have 3 comma-separated values, got {parts.Length}");
            }

            Vector3 result;
            if (!float.TryParse(parts[0], out result.x) ||
                !float.TryParse(parts[1], out result.y) ||
                !float.TryParse(parts[2], out result.z))
            {
                throw new FormatException("Failed to parse Vector3 components as floats");
            }

            return result;
        }

        /// <summary>
        /// Parses a Color from a comma-separated string.
        /// </summary>
        public static Color ParseColor(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 3 || parts.Length > 4)
            {
                throw new FormatException($"Color must have 3 or 4 comma-separated values, got {parts.Length}");
            }

            Color result;
            if (!float.TryParse(parts[0], out result.r) ||
                !float.TryParse(parts[1], out result.g) ||
                !float.TryParse(parts[2], out result.b))
            {
                throw new FormatException("Failed to parse Color components as floats");
            }

            result.a = parts.Length > 3 && float.TryParse(parts[3], out float a) ? a : 1f;
            return result;
        }
    }

    /// <summary>
    /// Tool that creates a GameObject in the scene.
    /// </summary>
    public class CreateGameObjectTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "create_game_object",
                description = "Creates a new GameObject in the Unity scene with the specified name and optional position."
            }
            .AddParameter("name", "string", "The name for the new GameObject", required: true)
            .AddParameter("x", "number", "X position (default: 0)", required: false)
            .AddParameter("y", "number", "Y position (default: 0)", required: false)
            .AddParameter("z", "number", "Z position (default: 0)", required: false);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string name = json["name"]?.Value;
                float x = json["x"]?.AsFloat ?? 0f;
                float y = json["y"]?.AsFloat ?? 0f;
                float z = json["z"]?.AsFloat ?? 0f;
                
                if (string.IsNullOrEmpty(name))
                {
                    return "Error: GameObject name is required";
                }

                GameObject newObj = new GameObject(name);
                newObj.transform.position = new Vector3(x, y, z);
                
                Undo.RegisterCreatedObjectUndo(newObj, "Create GameObject via AI");
                Selection.activeGameObject = newObj;
                
                return $"Successfully created GameObject '{name}' at position ({x}, {y}, {z})";
            }, "create GameObject");
        }
    }

    /// <summary>
    /// Tool that finds GameObjects in the scene.
    /// </summary>
    public class FindGameObjectsTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "find_game_objects",
                description = "Finds GameObjects in the scene by name or tag. Returns information about matching objects."
            }
            .AddParameter("search_by", "string", "Search method: 'name' or 'tag'", required: true)
            .AddParameter("search_value", "string", "The name or tag to search for", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string searchBy = json["search_by"]?.Value;
                string searchValue = json["search_value"]?.Value;
                
                GameObject[] foundObjects = null;
                
                if (searchBy == "tag")
                {
                    foundObjects = GameObject.FindGameObjectsWithTag(searchValue);
                }
                else if (searchBy == "name")
                {
                    var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    foundObjects = System.Array.FindAll(allObjects, obj => obj.name.Contains(searchValue));
                }
                else
                {
                    return "Error: search_by must be either 'name' or 'tag'";
                }

                if (foundObjects.Length == 0)
                {
                    return $"No GameObjects found with {searchBy} '{searchValue}'";
                }

                string result = $"Found {foundObjects.Length} GameObject(s):\n";
                foreach (var obj in foundObjects)
                {
                    result += "\n" + SceneToolHelpers.FormatGameObjectInfo(obj);
                }

                return result;
            }, "find GameObjects");
        }
    }

    /// <summary>
    /// Tool that adds a component to a GameObject.
    /// </summary>
    public class AddComponentTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "add_component",
                description = "Adds a component to a GameObject by name. Supports common Unity components like Rigidbody, BoxCollider, etc."
            }
            .AddParameter("game_object_name", "string", "The name of the GameObject to add the component to", required: true)
            .AddParameter("component_type", "string", "The type of component to add (e.g., 'Rigidbody', 'BoxCollider', 'MeshRenderer')", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                string componentType = json["component_type"]?.Value;
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);
                Type componentTypeObj = SceneToolHelpers.ResolveComponentType(componentType);

                var component = targetObj.AddComponent(componentTypeObj);
                Undo.RegisterCreatedObjectUndo(component, "Add Component via AI");

                return $"Successfully added {componentType} to '{gameObjectName}'";
            }, "add component");
        }
    }

    /// <summary>
    /// Tool that gets scene information.
    /// </summary>
    public class GetSceneInfoTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "get_scene_info",
                description = "Gets information about the current Unity scene including object count, active scene name, and list of root objects."
            };
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                
                string result = $"Scene Information:\n";
                result += $"Name: {scene.name}\n";
                result += $"Path: {scene.path}\n";
                result += $"Is Loaded: {scene.isLoaded}\n";
                result += $"Root Object Count: {scene.rootCount}\n\n";
                
                result += "Root GameObjects:\n";
                var rootObjects = scene.GetRootGameObjects();
                
                foreach (var obj in rootObjects)
                {
                    result += $"\n- {obj.name}";
                    result += $"\n  Active: {obj.activeSelf}";
                    result += $"\n  Children: {obj.transform.childCount}";
                    var components = obj.GetComponents<Component>();
                    result += $"\n  Components: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}";
                }

                return result;
            }, "get scene info");
        }
    }

    /// <summary>
    /// Tool that updates GameObject transforms (position, rotation, scale).
    /// </summary>
    public class UpdateTransformTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "update_transform",
                description = "Updates the transform (position, rotation, or scale) of a GameObject in the scene."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject to update", required: true)
            .AddParameter("position_x", "number", "X position (leave empty to keep current)", required: false)
            .AddParameter("position_y", "number", "Y position (leave empty to keep current)", required: false)
            .AddParameter("position_z", "number", "Z position (leave empty to keep current)", required: false)
            .AddParameter("rotation_x", "number", "X rotation in degrees (leave empty to keep current)", required: false)
            .AddParameter("rotation_y", "number", "Y rotation in degrees (leave empty to keep current)", required: false)
            .AddParameter("rotation_z", "number", "Z rotation in degrees (leave empty to keep current)", required: false)
            .AddParameter("scale_x", "number", "X scale (leave empty to keep current)", required: false)
            .AddParameter("scale_y", "number", "Y scale (leave empty to keep current)", required: false)
            .AddParameter("scale_z", "number", "Z scale (leave empty to keep current)", required: false);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);

                Undo.RecordObject(targetObj.transform, "Update Transform via AI");

                List<string> changes = new List<string>();

                // Update position
                if (json["position_x"] != null || json["position_y"] != null || json["position_z"] != null)
                {
                    Vector3 newPos = targetObj.transform.position;
                    if (json["position_x"] != null) newPos.x = json["position_x"].AsFloat;
                    if (json["position_y"] != null) newPos.y = json["position_y"].AsFloat;
                    if (json["position_z"] != null) newPos.z = json["position_z"].AsFloat;
                    targetObj.transform.position = newPos;
                    changes.Add($"position to {newPos}");
                }

                // Update rotation
                if (json["rotation_x"] != null || json["rotation_y"] != null || json["rotation_z"] != null)
                {
                    Vector3 newRot = targetObj.transform.eulerAngles;
                    if (json["rotation_x"] != null) newRot.x = json["rotation_x"].AsFloat;
                    if (json["rotation_y"] != null) newRot.y = json["rotation_y"].AsFloat;
                    if (json["rotation_z"] != null) newRot.z = json["rotation_z"].AsFloat;
                    targetObj.transform.eulerAngles = newRot;
                    changes.Add($"rotation to {newRot}");
                }

                // Update scale
                if (json["scale_x"] != null || json["scale_y"] != null || json["scale_z"] != null)
                {
                    Vector3 newScale = targetObj.transform.localScale;
                    if (json["scale_x"] != null) newScale.x = json["scale_x"].AsFloat;
                    if (json["scale_y"] != null) newScale.y = json["scale_y"].AsFloat;
                    if (json["scale_z"] != null) newScale.z = json["scale_z"].AsFloat;
                    targetObj.transform.localScale = newScale;
                    changes.Add($"scale to {newScale}");
                }

                if (changes.Count == 0)
                {
                    return "No transform properties were specified to update";
                }

                return $"Successfully updated '{gameObjectName}': {string.Join(", ", changes)}";
            }, "update transform");
        }
    }

    /// <summary>
    /// Tool that deletes GameObjects from the scene.
    /// </summary>
    public class DeleteGameObjectTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "delete_game_object",
                description = "Deletes a GameObject from the scene by name."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject to delete", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);

                Undo.DestroyObjectImmediate(targetObj);
                
                return $"Successfully deleted GameObject '{gameObjectName}'";
            }, "delete GameObject");
        }
    }

    /// <summary>
    /// Tool that removes components from GameObjects.
    /// </summary>
    public class DeleteComponentTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "delete_component",
                description = "Removes a component from a GameObject. Specify the component type to remove."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject", required: true)
            .AddParameter("component_type", "string", "Type of component to remove (e.g., 'Rigidbody', 'BoxCollider')", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                string componentType = json["component_type"]?.Value;
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);
                Type compType = SceneToolHelpers.ResolveComponentType(componentType);
                Component component = SceneToolHelpers.GetComponent(targetObj, compType);

                Undo.DestroyObjectImmediate(component);
                
                return $"Successfully removed {componentType} from '{gameObjectName}'";
            }, "delete component");
        }
    }

    /// <summary>
    /// Tool that gets detailed information about a GameObject including all its components and properties.
    /// </summary>
    public class GetGameObjectInfoTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "get_game_object_info",
                description = "Gets detailed information about a GameObject including all components and their serialized properties in JSON format."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject to inspect", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);

                string result = $"GameObject: {targetObj.name}\n";
                result += $"Active: {targetObj.activeSelf}\n";
                result += $"Tag: {targetObj.tag}\n";
                result += $"Layer: {LayerMask.LayerToName(targetObj.layer)}\n";
                result += $"Position: {targetObj.transform.position}\n";
                result += $"Rotation: {targetObj.transform.eulerAngles}\n";
                result += $"Scale: {targetObj.transform.localScale}\n\n";

                var components = targetObj.GetComponents<Component>();
                result += $"Components ({components.Length}):\n";

                foreach (var component in components)
                {
                    if (component == null) continue;

                    result += $"\n--- {component.GetType().Name} ---\n";
                    
                    try
                    {
                        string componentJson = JsonUtility.ToJson(component, true);
                        result += componentJson + "\n";
                    }
                    catch
                    {
                        // If JsonUtility fails, use SerializedObject instead
                        SerializedObject so = new SerializedObject(component);
                        SerializedProperty prop = so.GetIterator();
                        
                        while (prop.NextVisible(true))
                        {
                            result += $"{prop.propertyPath}: {SceneToolHelpers.GetPropertyValue(prop)}\n";
                        }
                    }
                }

                return result;
            }, "get GameObject info");
        }
    }

    /// <summary>
    /// Tool that updates GameObject or Component properties using SerializedObject API.
    /// </summary>
    public class UpdateObjectPropertyTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "update_object_property",
                description = "Updates properties on a GameObject or Component using the SerializedObject API. Provide the property path and new value."
            }
            .AddParameter("game_object_name", "string", "Name of the GameObject", required: true)
            .AddParameter("component_type", "string", "Type of component to update (e.g., 'Rigidbody', 'BoxCollider'). Use 'GameObject' for GameObject properties.", required: true)
            .AddParameter("property_path", "string", "Property path (e.g., 'm_Mass', 'm_IsActive', 'm_Size.x')", required: true)
            .AddParameter("value", "string", "New value for the property", required: true)
            .AddParameter("value_type", "string", "Type of value: 'int', 'float', 'bool', 'string', 'vector3' (format: 'x,y,z'), 'color' (format: 'r,g,b,a')", required: true);
        }

        public string Execute(string arguments)
        {
            return SceneToolHelpers.SafeExecute(() =>
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string gameObjectName = json["game_object_name"]?.Value;
                string componentType = json["component_type"]?.Value;
                string propertyPath = json["property_path"]?.Value;
                string value = json["value"]?.Value;
                string valueType = json["value_type"]?.Value?.ToLower();
                
                GameObject targetObj = SceneToolHelpers.FindGameObject(gameObjectName);

                UnityEngine.Object targetComponent;
                if (componentType == "GameObject")
                {
                    targetComponent = targetObj;
                }
                else
                {
                    Type compType = SceneToolHelpers.ResolveComponentType(componentType);
                    targetComponent = SceneToolHelpers.GetComponent(targetObj, compType);
                }

                SerializedObject so = new SerializedObject(targetComponent);
                SerializedProperty prop = so.FindProperty(propertyPath);

                if (prop == null)
                {
                    throw new ArgumentException($"Property '{propertyPath}' not found on {componentType}");
                }

                // Set the value based on type
                bool success = false;
                switch (valueType)
                {
                    case "int":
                        if (int.TryParse(value, out int intVal))
                        {
                            prop.intValue = intVal;
                            success = true;
                        }
                        break;
                    case "float":
                        if (float.TryParse(value, out float floatVal))
                        {
                            prop.floatValue = floatVal;
                            success = true;
                        }
                        break;
                    case "bool":
                        if (bool.TryParse(value, out bool boolVal))
                        {
                            prop.boolValue = boolVal;
                            success = true;
                        }
                        break;
                    case "string":
                        prop.stringValue = value;
                        success = true;
                        break;
                    case "vector3":
                        prop.vector3Value = SceneToolHelpers.ParseVector3(value);
                        success = true;
                        break;
                    case "color":
                        prop.colorValue = SceneToolHelpers.ParseColor(value);
                        success = true;
                        break;
                }

                if (!success)
                {
                    throw new FormatException($"Failed to parse value '{value}' as type '{valueType}'");
                }

                so.ApplyModifiedProperties();
                
                return $"Successfully updated '{propertyPath}' on {componentType} of '{gameObjectName}' to '{value}'";
            }, "update property");
        }
    }
}
