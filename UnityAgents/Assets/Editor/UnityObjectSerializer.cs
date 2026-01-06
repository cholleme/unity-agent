using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;
using SimpleJSON;

namespace UnityAgents.Editor
{
    public static class UnityObjectSerializer
    {
        public static string SerializeObject(Object obj)
        {
            if (obj == null)
                return "null";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Object: {obj.name} (Type: {obj.GetType().Name})");
            sb.AppendLine("{");

            // Handle different types of Unity objects
            if (obj is GameObject gameObject)
            {
                SerializeGameObject(gameObject, sb);
            }
            else if (obj is Component component)
            {
                SerializeComponent(component, sb);
            }
            else if (obj is ScriptableObject scriptableObject)
            {
                SerializeScriptableObject(scriptableObject, sb);
            }
            else
            {
                // Fallback - just show the object type
                sb.AppendLine($"  Type: {obj.GetType().FullName}");
                sb.AppendLine("  (Use SerializedObject for detailed properties)");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void SerializeGameObject(GameObject go, StringBuilder sb)
        {
            sb.AppendLine($"  Name: {go.name}");
            sb.AppendLine($"  Active: {go.activeSelf}");
            sb.AppendLine($"  Tag: {go.tag}");
            sb.AppendLine($"  Layer: {LayerMask.LayerToName(go.layer)}");
            
            if (go.transform != null)
            {
                sb.AppendLine($"  Position: {go.transform.position}");
                sb.AppendLine($"  Rotation: {go.transform.rotation.eulerAngles}");
                sb.AppendLine($"  Scale: {go.transform.localScale}");
            }

            var components = go.GetComponents<Component>();
            if (components.Length > 0)
            {
                sb.AppendLine("  Components:");
                foreach (var component in components)
                {
                    if (component != null)
                    {
                        sb.AppendLine($"    - {component.GetType().Name}");
                    }
                }
            }

            // Include child count
            if (go.transform.childCount > 0)
            {
                sb.AppendLine($"  Children: {go.transform.childCount}");
            }
        }

        private static void SerializeComponent(Component component, StringBuilder sb)
        {
            sb.AppendLine($"  Component Type: {component.GetType().Name}");
            sb.AppendLine($"  GameObject: {component.gameObject.name}");
            
            // Get all serialized fields
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            
            sb.AppendLine("  Properties:");
            
            if (property.NextVisible(true))
            {
                do
                {
                    if (property.name == "m_Script")
                        continue;

                    string value = GetPropertyValue(property);
                    sb.AppendLine($"    {property.name}: {value}");
                }
                while (property.NextVisible(false));
            }
        }

        private static void SerializeScriptableObject(ScriptableObject scriptableObject, StringBuilder sb)
        {
            SerializedObject serializedObject = new SerializedObject(scriptableObject);
            SerializedProperty property = serializedObject.GetIterator();
            
            sb.AppendLine("  Properties:");
            
            if (property.NextVisible(true))
            {
                do
                {
                    if (property.name == "m_Script")
                        continue;

                    string value = GetPropertyValue(property);
                    sb.AppendLine($"    {property.name}: {value}");
                }
                while (property.NextVisible(false));
            }
        }

        private static string GetPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString("F3");
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum:
                    return property.enumNames[property.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return property.arraySize.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return "AnimationCurve";
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.ToString();
                default:
                    return property.propertyType.ToString();
            }
        }
    }
}
