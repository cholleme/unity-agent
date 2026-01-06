using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityAgents.Editor
{
    public static class OpenAIService
    {
        private static string apiKey => ChatbotSettings.Instance.apiKey;
        private static string apiUrl => ChatbotSettings.Instance.apiEndpoint;
        private static string model => ChatbotSettings.Instance.model;

        public static async Task<ChatStatistics> SendChatRequest(ChatSession session, Action<int> SaveToolState = null, Func<bool> cancelRequested = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("API Key is not set. Please configure it in Settings.");
            }

            int maxIterations = 50; // Prevent infinite loops
            int iteration = 0;

            // Accumulate statistics across all iterations
            int totalPromptTokens = 0;
            int totalCompletionTokens = 0;
            int totalTokens = 0;
            float totalPredictedMs = 0;
            int totalPredictedN = 0;

            while (iteration < maxIterations && (cancelRequested == null || !cancelRequested()))
            {
                iteration++;
                
                // Get current messages from session
                var messages = session.GetOpenAIMessages();
                
                // Call OpenAI API
                var response = await CallOpenAIAPI(messages.ToArray());
                
                if (response.choices == null || response.choices.Length == 0)
                {
                    throw new Exception("Invalid response format from OpenAI");
                }

                var choice = response.choices[0];
                var finishReason = choice.finish_reason;
                
                Debug.Log($"Iteration {iteration}: finish_reason = {finishReason}");

                // Accumulate statistics
                if (response.usage != null)
                {
                    totalPromptTokens += response.usage.prompt_tokens;
                    totalCompletionTokens += response.usage.completion_tokens;
                    totalTokens += response.usage.total_tokens;
                }
                if (response.timings != null)
                {
                    totalPredictedMs += response.timings.predicted_ms;
                    totalPredictedN += response.timings.predicted_n;
                }

                // If there are no tool calls this means we have the final response
                if (finishReason != "tool_calls" || 
                    choice.message.tool_calls == null || 
                    choice.message.tool_calls.Length == 0)
                {
                    // Add final assistant message to session
                    session.messages.Add(new ChatMessage
                    {
                        role = "assistant",
                        content = choice.message.content ?? "",
                        reasoningContent = choice.message.reasoning_content
                    });
                    
                    return new ChatStatistics
                    {
                        promptTokens = totalPromptTokens,
                        completionTokens = totalCompletionTokens,
                        totalTokens = totalTokens,
                        predictedMs = totalPredictedMs,
                        predictedN = totalPredictedN
                    };
                }

                //
                // There is a tool to call so we call it, gather results and pass them back to AI
                //
                
                // Add the assistant's message with tool calls to session
                var assistantMessage = new ChatMessage
                {
                    role = "assistant",
                    content = choice.message.content,
                    reasoningContent = choice.message.reasoning_content,
                    toolCalls = new List<ToolCallInfo>()
                };
                
                foreach (var tc in choice.message.tool_calls)
                {
                    assistantMessage.toolCalls.Add(new ToolCallInfo
                    {
                        id = tc.id,
                        type = tc.type,
                        functionName = tc.function.name,
                        arguments = tc.function.arguments
                    });
                }
                
                session.messages.Add(assistantMessage);

                // Execute each tool and add results to session
                foreach (var toolCall in choice.message.tool_calls)
                {
                    SaveToolState?.Invoke(iteration);
                    string toolResult;
                    try
                    {
                        toolResult = AgentToolRegistry.ExecuteTool(toolCall.function.name, toolCall.function.arguments);
                    }
                    catch (Exception ex)
                    {
                        toolResult = $"Error: {ex.Message}";
                    }

                    // Add tool result message to session
                    session.messages.Add(new ChatMessage
                    {
                        role = "tool",
                        toolCallId = toolCall.id,
                        toolName = toolCall.function.name,
                        content = toolResult
                    });
                }
                
                // Loop continues to get AI's response to the tool results
            }

            throw new Exception($"Maximum iteration limit ({maxIterations}) reached. Possible infinite loop in tool calls.");
        }

        private static async Task<OpenAIResponse> CallOpenAIAPI(OpenAIMessage[] messages)
        {
            var requestData = new OpenAIRequest
            {
                model = model,
                messages = messages,
                temperature = ChatbotSettings.Instance.temperature,
                max_tokens = ChatbotSettings.Instance.maxTokens
            };

            // Add tool definitions if tools are enabled
            if (ChatbotSettings.Instance.enableTools)
            {
                var toolDefinitions = AgentToolRegistry.GetToolDefinitions();
                if (toolDefinitions.Length > 0)
                {
                    requestData.tools = toolDefinitions;
                }
            }

            string jsonRequest = SerializeOpenAIRequest(requestData);
            Debug.Log($"Sending request to OpenAI: {jsonRequest}");

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    Debug.Log($"Received response: {response}");
                    return DeserializeOpenAIResponse(response);
                }
                else
                {
                    string errorMessage = $"Request failed: {request.error}\n{request.downloadHandler.text}";
                    Debug.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }
            }
        }



        // SimpleJSON Serialization Helpers
        private static string SerializeOpenAIRequest(OpenAIRequest request)
        {
            var json = new SimpleJSON.JSONClass();
            json["model"] = request.model;
            json["temperature"] = request.temperature.ToString();
            json["max_tokens"] = request.max_tokens.ToString();

            var messagesArray = new SimpleJSON.JSONArray();
            foreach (var message in request.messages)
            {
                var msgObj = new SimpleJSON.JSONClass();
                msgObj["role"] = message.role;
                
                // Handle different message types
                if (message.role == "tool")
                {
                    // Tool result message
                    msgObj["tool_call_id"] = message.tool_call_id;
                    msgObj["name"] = message.name;
                    msgObj["content"] = message.content;
                }
                else
                {
                    // Regular message
                    msgObj["content"] = message.content ?? "";
                    
                    // Add tool_calls if present (for assistant messages)
                    if (message.tool_calls != null && message.tool_calls.Length > 0)
                    {
                        var toolCallsArray = new SimpleJSON.JSONArray();
                        foreach (var toolCall in message.tool_calls)
                        {
                            var tcObj = new SimpleJSON.JSONClass();
                            tcObj["id"] = toolCall.id;
                            tcObj["type"] = toolCall.type;
                            
                            var funcObj = new SimpleJSON.JSONClass();
                            funcObj["name"] = toolCall.function.name;
                            funcObj["arguments"] = toolCall.function.arguments;
                            tcObj["function"] = funcObj;
                            
                            toolCallsArray[toolCallsArray.Count.ToString()] = tcObj;
                        }
                        msgObj["tool_calls"] = toolCallsArray;
                    }
                }
                
                messagesArray[messagesArray.Count.ToString()] = msgObj;
            }
            json["messages"] = messagesArray;

            if (request.tools != null && request.tools.Length > 0)
            {
                var toolsArray = new SimpleJSON.JSONArray();
                foreach (var tool in request.tools)
                {
                    toolsArray[toolsArray.Count.ToString()] = SerializeToolDefinition(tool);
                }
                json["tools"] = toolsArray;
            }

            return json.ToString();
        }

        private static SimpleJSON.JSONNode SerializeToolDefinition(ToolDefinition tool)
        {
            var json = new SimpleJSON.JSONClass();
            json["type"] = tool.type;

            var functionObj = new SimpleJSON.JSONClass();
            functionObj["name"] = tool.function.name;
            functionObj["description"] = tool.function.description;

            if (tool.function.parameters != null)
            {
                var parametersObj = new SimpleJSON.JSONClass();
                parametersObj["type"] = tool.function.parameters.type;

                if (tool.function.parameters.properties != null)
                {
                    var propertiesObj = new SimpleJSON.JSONClass();
                    foreach (var prop in tool.function.parameters.properties)
                    {
                        var propObj = new SimpleJSON.JSONClass();
                        propObj["type"] = prop.Value.type;
                        propObj["description"] = prop.Value.description;
                        propertiesObj[prop.Key] = propObj;
                    }
                    parametersObj["properties"] = propertiesObj;
                }

                if (tool.function.parameters.required != null && tool.function.parameters.required.Length > 0)
                {
                    var requiredArray = new SimpleJSON.JSONArray();
                    foreach (var req in tool.function.parameters.required)
                    {
                        requiredArray[requiredArray.Count.ToString()] = req;
                    }
                    parametersObj["required"] = requiredArray;
                }

                functionObj["parameters"] = parametersObj;
            }

            json["function"] = functionObj;
            return json;
        }

        private static OpenAIResponse DeserializeOpenAIResponse(string jsonString)
        {
            var json = SimpleJSON.JSONNode.Parse(jsonString);
            var response = new OpenAIResponse();

            response.id = json["id"].Value;
            response.@object = json["object"].Value;
            response.created = json["created"].AsLong;
            response.model = json["model"].Value;

            var choicesArray = json["choices"].AsArray;
            if (choicesArray != null)
            {
                response.choices = new Choice[choicesArray.Count];
                for (int i = 0; i < choicesArray.Count; i++)
                {
                    response.choices[i] = DeserializeChoice(choicesArray[i]);
                }
            }

            if (json["usage"] != null)
            {
                response.usage = new Usage
                {
                    prompt_tokens = json["usage"]["prompt_tokens"].AsInt,
                    completion_tokens = json["usage"]["completion_tokens"].AsInt,
                    total_tokens = json["usage"]["total_tokens"].AsInt
                };
            }

            if (json["timings"] != null)
            {
                response.timings = new Timings
                {
                    cache_n = json["timings"]["cache_n"].AsInt,
                    prompt_n = json["timings"]["prompt_n"].AsInt,
                    prompt_ms = json["timings"]["prompt_ms"].AsFloat,
                    prompt_per_token_ms = json["timings"]["prompt_per_token_ms"].AsFloat,
                    prompt_per_second = json["timings"]["prompt_per_second"].AsFloat,
                    predicted_n = json["timings"]["predicted_n"].AsInt,
                    predicted_ms = json["timings"]["predicted_ms"].AsFloat,
                    predicted_per_token_ms = json["timings"]["predicted_per_token_ms"].AsFloat,
                    predicted_per_second = json["timings"]["predicted_per_second"].AsFloat
                };
            }

            return response;
        }

        private static Choice DeserializeChoice(SimpleJSON.JSONNode choiceNode)
        {
            var choice = new Choice();
            choice.index = choiceNode["index"].AsInt;
            choice.finish_reason = choiceNode["finish_reason"].Value;

            if (choiceNode["message"] != null)
            {
                choice.message = new Message();
                choice.message.role = choiceNode["message"]["role"].Value;
                
                // Handle null/missing content properly
                var contentNode = choiceNode["message"]["content"];
                choice.message.content = (contentNode != null && !contentNode.IsNull) ? contentNode.Value : null;
                
                // Handle null/missing reasoning_content properly
                var reasoningNode = choiceNode["message"]["reasoning_content"];
                choice.message.reasoning_content = (reasoningNode != null && !reasoningNode.IsNull) ? reasoningNode.Value : null;

                var toolCallsArray = choiceNode["message"]["tool_calls"].AsArray;
                if (toolCallsArray != null && toolCallsArray.Count > 0)
                {
                    choice.message.tool_calls = new ToolCall[toolCallsArray.Count];
                    for (int i = 0; i < toolCallsArray.Count; i++)
                    {
                        var toolCallNode = toolCallsArray[i];
                        choice.message.tool_calls[i] = new ToolCall
                        {
                            id = toolCallNode["id"].Value,
                            type = toolCallNode["type"].Value,
                            function = new FunctionCall
                            {
                                name = toolCallNode["function"]["name"].Value,
                                arguments = toolCallNode["function"]["arguments"].Value
                            }
                        };
                    }
                }
            }

            return choice;
        }
    }

    public class OpenAIRequest
    {
        public string model;
        public OpenAIMessage[] messages;
        public float temperature = 0.7f;
        public int max_tokens = 2000;
        public ToolDefinition[] tools;
    }

    public class OpenAIMessage
    {
        public string role;
        public string content;
        public ToolCall[] tool_calls;  // For assistant messages with tool calls
        public string tool_call_id;    // For tool role messages
        public string name;            // For tool role messages
    }

    public class OpenAIResponse
    {
        public string id;
        public string @object;
        public long created;
        public string model;
        public Choice[] choices;
        public Usage usage;
        public Timings timings;
    }

    public class Choice
    {
        public int index;
        public Message message;
        public string finish_reason;
    }

    public class Message
    {
        public string role;
        public string content;
        public string reasoning_content;
        public ToolCall[] tool_calls;
    }

    public class ToolCall
    {
        public string id;
        public string type;
        public FunctionCall function;
    }

    public class FunctionCall
    {
        public string name;
        public string arguments;
    }

    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    public class Timings
    {
        public int cache_n;
        public int prompt_n;
        public float prompt_ms;
        public float prompt_per_token_ms;
        public float prompt_per_second;
        public int predicted_n;
        public float predicted_ms;
        public float predicted_per_token_ms;
        public float predicted_per_second;
    }

    public class ToolDefinition
    {
        public string type = "function";
        public FunctionDefinition function;
    }

    public class FunctionDefinition
    {
        public string name;
        public string description;
        public ParametersDefinition parameters;
    }

    public class ParametersDefinition
    {
        public string type = "object";
        public Dictionary<string, PropertyDefinition> properties;
        public string[] required;
    }

    public class PropertyDefinition
    {
        public string type;
        public string description;
    }
}
