using UnityEngine;
using System;

namespace UnityAgents.Editor.Examples
{
    /// <summary>
    /// Example tool that logs a message to the Unity console.
    /// </summary>
    public class LogMessageTool : IAgentTool
    {
        public ToolSpec GetToolSpec()
        {
            return new ToolSpec
            {
                name = "log_message",
                description = "Logs a message to the Unity console. Useful for debugging or providing feedback."
            }
            .AddParameter("message", "string", "The message to log", required: true)
            .AddParameter("type", "string", "Log type: 'info', 'warning', or 'error' (default: 'info')", required: false);
        }

        public string Execute(string arguments)
        {
            try
            {
                var json = SimpleJSON.JSONNode.Parse(arguments);
                string message = json["message"].Value;
                string type = json["type"].Value;
                if (string.IsNullOrEmpty(type)) type = "info";
                
                switch (type.ToLower())
                {
                    case "warning":
                        Debug.LogWarning(message);
                        break;
                    case "error":
                        Debug.LogError(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }

                return $"Logged {type} message: {message}";
            }
            catch (Exception ex)
            {
                return $"Failed to log message: {ex.Message}";
            }
        }
    }
}
