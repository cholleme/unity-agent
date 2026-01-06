using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAgents.Editor
{
    [Serializable]
    public class ChatSession
    {
        public string chatId;
        public string chatName;
        public DateTime createdTime;
        public List<ChatMessage> messages = new List<ChatMessage>();

        public string GetDisplayName()
        {
            if (messages.Count == 0)
                return chatName;
            
            return $"{chatName}\n({messages.Count} msgs)";
        }

        public List<OpenAIMessage> GetOpenAIMessages()
        {
            var openAIMessages = new List<OpenAIMessage>();
            
            foreach (var message in messages)
            {
                var openAIMessage = new OpenAIMessage
                {
                    role = message.role
                };
                
                if (message.role == "tool")
                {
                    // Tool result message
                    openAIMessage.tool_call_id = message.toolCallId;
                    openAIMessage.name = message.toolName;
                    openAIMessage.content = message.content;
                }
                else
                {
                    // Regular message (user, assistant, system)
                    string content = message.content;
                    
                    // Add attached object information for user messages
                    if (message.role == "user" && message.attachedObjects != null && message.attachedObjects.Count > 0)
                    {
                        content += "\n\n[Attached Unity Objects]:\n";
                        
                        foreach (var obj in message.attachedObjects)
                        {
                            if (obj != null)
                            {
                                string serialized = UnityObjectSerializer.SerializeObject(obj);
                                content += $"\n{serialized}";
                            }
                        }
                    }
                    
                    openAIMessage.content = content;
                    
                    // Add tool calls if present (for assistant messages)
                    if (message.toolCalls != null && message.toolCalls.Count > 0)
                    {
                        openAIMessage.tool_calls = new ToolCall[message.toolCalls.Count];
                        for (int i = 0; i < message.toolCalls.Count; i++)
                        {
                            var tc = message.toolCalls[i];
                            openAIMessage.tool_calls[i] = new ToolCall
                            {
                                id = tc.id,
                                type = tc.type,
                                function = new FunctionCall
                                {
                                    name = tc.functionName,
                                    arguments = tc.arguments
                                }
                            };
                        }
                    }
                }
                
                openAIMessages.Add(openAIMessage);
            }
            
            return openAIMessages;
        }
    }

    [Serializable]
    public class ChatMessage
    {
        public string role; // "user", "assistant", "system", "tool"
        public string content;
        public string reasoningContent; // AI reasoning for assistant messages
        public List<UnityEngine.Object> attachedObjects;
        public DateTime timestamp = DateTime.Now;
        
        // For tool messages
        public string toolCallId;
        public string toolName;
        
        // For assistant messages with tool calls
        public List<ToolCallInfo> toolCalls;
    }
    
    [Serializable]
    public class ToolCallInfo
    {
        public string id;
        public string type;
        public string functionName;
        public string arguments;
    }
    
    [Serializable]
    public class ChatStatistics
    {
        public int promptTokens;
        public int completionTokens;
        public int totalTokens;
        public float predictedMs;
        public int predictedN;
    }
}
