using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityAgents.Editor
{
    /// <summary>
    /// Discovers and manages all tools implementing IAgentTool in the current domain.
    /// </summary>
    public static class AgentToolRegistry
    {
        private static Dictionary<string, IAgentTool> toolInstances;
        private static bool isInitialized = false;

        /// <summary>
        /// Gets all discovered tools.
        /// </summary>
        public static Dictionary<string, IAgentTool> GetAllTools()
        {
            if (!isInitialized)
            {
                DiscoverTools();
            }
            return toolInstances;
        }

        /// <summary>
        /// Gets a specific tool by name.
        /// </summary>
        public static IAgentTool GetTool(string toolName)
        {
            if (!isInitialized)
            {
                DiscoverTools();
            }
            
            return toolInstances.TryGetValue(toolName, out var tool) ? tool : null;
        }

        /// <summary>
        /// Gets all tool definitions for OpenAI API.
        /// </summary>
        public static ToolDefinition[] GetToolDefinitions()
        {
            if (!isInitialized)
            {
                DiscoverTools();
            }

            return toolInstances.Values
                .Select(tool => tool.GetToolSpec().ToToolDefinition())
                .ToArray();
        }

        /// <summary>
        /// Executes a tool by name with the provided arguments.
        /// </summary>
        public static string ExecuteTool(string toolName, string arguments)
        {
            var tool = GetTool(toolName);
            
            if (tool == null)
            {
                throw new Exception($"Tool '{toolName}' not found in registry");
            }

            // Capture logs during tool execution
            var capturedLogs = new List<string>();
            var capturedWarnings = new List<string>();
            var capturedErrors = new List<string>();
            
            Application.LogCallback logCallback = (logString, stackTrace, type) =>
            {
                switch (type)
                {
                    case LogType.Error:
                    case LogType.Exception:
                        capturedErrors.Add($"[{type}] {logString}");
                        if (!string.IsNullOrEmpty(stackTrace))
                        {
                            capturedErrors.Add($"Stack trace: {stackTrace}");
                        }
                        break;
                    case LogType.Warning:
                        capturedWarnings.Add($"[Warning] {logString}");
                        break;
                    case LogType.Log:
                        // Normal logs are ignored for now
                        break;
                }
            };

            try
            {
                // Register log callback
                Application.logMessageReceived += logCallback;
                
                Debug.Log($"Executing tool: {toolName} with arguments: {arguments}");
                string result = tool.Execute(arguments);
                Debug.Log($"Tool execution result: {result}");
                
                // Append captured logs to result if any
                if (capturedErrors.Count > 0 || capturedWarnings.Count > 0)
                {
                    result += "\n\n--- Captured Logs During Execution ---";
                    
                    if (capturedErrors.Count > 0)
                    {
                        result += "\n\n⚠️ Errors:\n" + string.Join("\n", capturedErrors);
                    }
                    
                    if (capturedWarnings.Count > 0)
                    {
                        result += "\n\n⚠️ Warnings:\n" + string.Join("\n", capturedWarnings);
                    }
                    
                    if (capturedLogs.Count > 0)
                    {
                        result += "\n\nInfo:\n" + string.Join("\n", capturedLogs);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error executing tool '{toolName}': {ex.Message}\n{ex.StackTrace}";
                Debug.LogError(errorMessage);
                
                // Include any captured logs in the error response
                if (capturedErrors.Count > 0 || capturedWarnings.Count > 0 || capturedLogs.Count > 0)
                {
                    errorMessage += "\n\n--- Captured Logs During Execution ---";
                    
                    if (capturedErrors.Count > 0)
                    {
                        errorMessage += "\n\n⚠️ Errors:\n" + string.Join("\n", capturedErrors);
                    }
                    
                    if (capturedWarnings.Count > 0)
                    {
                        errorMessage += "\n\n⚠️ Warnings:\n" + string.Join("\n", capturedWarnings);
                    }
                    
                    if (capturedLogs.Count > 0)
                    {
                        errorMessage += "\n\nInfo:\n" + string.Join("\n", capturedLogs);
                    }
                }
                
                return errorMessage;
            }
            finally
            {
                // Always unregister the callback
                Application.logMessageReceived -= logCallback;
            }
        }

        /// <summary>
        /// Forces a refresh of the tool registry.
        /// </summary>
        public static void RefreshTools()
        {
            isInitialized = false;
            DiscoverTools();
        }

        /// <summary>
        /// Discovers all types implementing IAgentTool in the current domain.
        /// </summary>
        private static void DiscoverTools()
        {
            toolInstances = new Dictionary<string, IAgentTool>();

            try
            {
                // Get all assemblies in the current domain
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Find all types implementing IAgentTool
                        var toolTypes = assembly.GetTypes()
                            .Where(t => typeof(IAgentTool).IsAssignableFrom(t) 
                                     && !t.IsInterface 
                                     && !t.IsAbstract);

                        foreach (var toolType in toolTypes)
                        {
                            try
                            {
                                // Create instance of the tool
                                var toolInstance = (IAgentTool)Activator.CreateInstance(toolType);
                                var spec = toolInstance.GetToolSpec();

                                if (string.IsNullOrEmpty(spec.name))
                                {
                                    Debug.LogWarning($"Tool '{toolType.Name}' has no name specified. Skipping.");
                                    continue;
                                }

                                // Register the tool
                                if (toolInstances.ContainsKey(spec.name))
                                {
                                    Debug.LogWarning($"Duplicate tool name '{spec.name}' found in {toolType.Name}. Skipping.");
                                    continue;
                                }

                                toolInstances[spec.name] = toolInstance;
                                Debug.Log($"Registered tool: {spec.name} ({toolType.Name})");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Failed to instantiate tool '{toolType.Name}': {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Some assemblies might not be accessible, skip them
                        Debug.LogWarning($"Could not scan assembly '{assembly.FullName}': {ex.Message}");
                    }
                }

                isInitialized = true;
                Debug.Log($"Tool discovery complete. Found {toolInstances.Count} tools.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during tool discovery: {ex.Message}");
                isInitialized = true; // Mark as initialized to prevent infinite retry
            }
        }

        /// <summary>
        /// Gets a summary of all registered tools.
        /// </summary>
        public static string GetToolsSummary()
        {
            if (!isInitialized)
            {
                DiscoverTools();
            }

            if (toolInstances.Count == 0)
            {
                return "No tools registered.";
            }

            var summary = $"Registered Tools ({toolInstances.Count}):\n";
            foreach (var tool in toolInstances.Values)
            {
                var spec = tool.GetToolSpec();
                summary += $"\n• {spec.name}: {spec.description}";
                if (spec.parameters.Count > 0)
                {
                    summary += $"\n  Parameters: {string.Join(", ", spec.parameters.Keys)}";
                }
            }

            return summary;
        }
    }
}
