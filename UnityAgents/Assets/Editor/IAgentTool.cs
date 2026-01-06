using System;
using System.Collections.Generic;

namespace UnityAgents.Editor
{
    /// <summary>
    /// Interface for implementing custom tools that the AI chatbot can call.
    /// Implement this interface to create tools that can be discovered and executed automatically.
    /// </summary>
    public interface IAgentTool
    {
        /// <summary>
        /// Gets the specification for this tool that will be sent to the AI.
        /// </summary>
        ToolSpec GetToolSpec();

        /// <summary>
        /// Executes the tool with the provided arguments.
        /// </summary>
        /// <param name="arguments">JSON string containing the tool arguments</param>
        /// <returns>Result of the tool execution as a string</returns>
        string Execute(string arguments);
    }

    /// <summary>
    /// Specification for a tool that can be called by the AI.
    /// </summary>
    [Serializable]
    public class ToolSpec
    {
        public string name;
        public string description;
        public Dictionary<string, ParameterSpec> parameters;
        public List<string> requiredParameters;

        public ToolSpec()
        {
            parameters = new Dictionary<string, ParameterSpec>();
            requiredParameters = new List<string>();
        }

        /// <summary>
        /// Adds a parameter to this tool specification.
        /// </summary>
        public ToolSpec AddParameter(string name, string type, string description, bool required = false)
        {
            parameters[name] = new ParameterSpec
            {
                type = type,
                description = description
            };

            if (required)
            {
                requiredParameters.Add(name);
            }

            return this;
        }

        /// <summary>
        /// Converts this spec to the OpenAI API format.
        /// </summary>
        public ToolDefinition ToToolDefinition()
        {
            var properties = new Dictionary<string, PropertyDefinition>();
            
            foreach (var param in parameters)
            {
                properties[param.Key] = new PropertyDefinition
                {
                    type = param.Value.type,
                    description = param.Value.description
                };
            }

            return new ToolDefinition
            {
                type = "function",
                function = new FunctionDefinition
                {
                    name = name,
                    description = description,
                    parameters = new ParametersDefinition
                    {
                        type = "object",
                        properties = properties,
                        required = requiredParameters.ToArray()
                    }
                }
            };
        }
    }

    /// <summary>
    /// Parameter specification for a tool.
    /// </summary>
    [Serializable]
    public class ParameterSpec
    {
        public string type;
        public string description;
    }
}
