using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

namespace UnityAgents.Editor
{
    public class ChatbotEditorWindow : EditorWindow
    {
        private List<ChatSession> chatSessions = new List<ChatSession>();
        private int selectedChatIndex = 0;
        private int iterationCount = 0;
        private Vector2 chatListScrollPosition;
        private Vector2 chatHistoryScrollPosition;
        private Vector2 attachmentsScrollPosition;
        private string currentPrompt = "";
        private List<Object> attachedObjects = new List<Object>();
        [System.NonSerialized]
        private bool isWaitingForResponse = false;
        
        private GUIStyle messageBoxStyle;
        private GUIStyle userMessageStyle;
        private GUIStyle assistantMessageStyle;
        private GUIStyle systemMessageStyle;
        private GUIStyle toolMessageStyle;
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;
        
        private Dictionary<string, bool> reasoningFoldouts = new Dictionary<string, bool>();

        [MenuItem("Window/AI Chatbot")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChatbotEditorWindow>("AI Chatbot");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            LoadSessions();
            if (chatSessions.Count == 0)
            {
                CreateNewChat();
            }
        }

        private void OnDisable()
        {
            SaveSessions();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 5, 5)
            };

            messageBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5),
                wordWrap = true,
                richText = true
            };

            userMessageStyle = new GUIStyle(messageBoxStyle);
            userMessageStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.5f, 0.8f, 0.3f));

            assistantMessageStyle = new GUIStyle(messageBoxStyle);
            assistantMessageStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.8f, 0.5f, 0.3f));

            systemMessageStyle = new GUIStyle(messageBoxStyle);
            systemMessageStyle.normal.background = CreateColorTexture(new Color(0.8f, 0.8f, 0.3f, 0.3f));

            toolMessageStyle = new GUIStyle(messageBoxStyle);
            toolMessageStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.8f, 0.5f, 0.2f));

            stylesInitialized = true;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - Chat list
            DrawChatListPanel();
            
            // Right panel - Chat interface
            DrawChatPanel();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChatListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            
            EditorGUILayout.LabelField("Chat Sessions", headerStyle);
            
            if (GUILayout.Button("New Chat", GUILayout.Height(30)))
            {
                CreateNewChat();
            }
            
            EditorGUILayout.Space(5);
            
            chatListScrollPosition = EditorGUILayout.BeginScrollView(chatListScrollPosition);
            
            for (int i = 0; i < chatSessions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = selectedChatIndex == i;
                GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                
                if (GUILayout.Button(chatSessions[i].GetDisplayName(), GUILayout.Height(40)))
                {
                    selectedChatIndex = i;
                }
                
                GUI.backgroundColor = Color.white;
                
                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(40)))
                {
                    DeleteChat(i);
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawChatPanel()
        {
            if (chatSessions.Count == 0) return;
            if (selectedChatIndex >= chatSessions.Count) selectedChatIndex = chatSessions.Count - 1;

            var currentChat = chatSessions[selectedChatIndex];

            EditorGUILayout.BeginVertical();
            
            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"Chat: {currentChat.chatName}", headerStyle);
            
            if (GUILayout.Button("Rename", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RenameChat(selectedChatIndex);
            }
            
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ClearChat(selectedChatIndex);
            }
            
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ChatbotSettingsWindow.ShowWindow();
            }
            
            if (GUILayout.Button("Tools", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ToolManagementWindow.ShowWindow();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Chat history
            EditorGUILayout.LabelField("Chat History", EditorStyles.boldLabel);
            chatHistoryScrollPosition = EditorGUILayout.BeginScrollView(chatHistoryScrollPosition, GUILayout.ExpandHeight(true));
            
            foreach (var message in currentChat.messages)
            {
                DrawMessage(message);
            }
            
            // Display statistics after the last message if available
            if (lastStatistics != null && currentChat.messages.Count > 0)
            {
                DrawStatistics(lastStatistics);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // Attachments section
            DrawAttachmentsSection();
            
            EditorGUILayout.Space(5);
            
            // Input area
            DrawInputArea();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawMessage(ChatMessage message)
        {
            if (message.role == "tool")
            {
                // Tool result messages - render with special formatting
                DrawToolResultMessage(message);
                return;
            }
            
            GUIStyle style = message.role == "user" ? userMessageStyle : 
                           message.role == "assistant" ? assistantMessageStyle : 
                           systemMessageStyle;

            EditorGUILayout.BeginVertical(style);
            
            EditorGUILayout.LabelField($"<b>{message.role.ToUpper()}</b>", new GUIStyle(EditorStyles.label) { richText = true });
                        
            // Render markdown content if present
            if (!string.IsNullOrEmpty(message.content))
            {
                MarkdownRenderer.RenderMarkdown(message.content);
            }
            
            // Display tool calls for assistant messages
            if (message.toolCalls != null && message.toolCalls.Count > 0)
            {
                foreach (var toolCall in message.toolCalls)
                {
                    string foldoutKey = message.timestamp.ToBinary().ToString() + "_" + toolCall.id;
                    if (!reasoningFoldouts.ContainsKey(foldoutKey))
                    {
                        reasoningFoldouts[foldoutKey] = false;
                    }
                    
                    reasoningFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                        reasoningFoldouts[foldoutKey], 
                        $"🔧 Tool Call: {toolCall.functionName}",
                        true,
                        new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Italic }
                    );
                    
                    if (reasoningFoldouts[foldoutKey])
                    {
                        EditorGUILayout.LabelField($"<i>Arguments:</i> {toolCall.arguments}", 
                        new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10, wordWrap = true });
                    }     
                }           
            }
            
            if (message.attachedObjects != null && message.attachedObjects.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"<i>Attached: {string.Join(", ", message.attachedObjects.Select(o => o != null ? o.name : "null"))}</i>", 
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });
            }

            // Display reasoning content foldout for assistant messages if present
            if (message.role == "assistant" && !string.IsNullOrEmpty(message.reasoningContent))
            {
                string foldoutKey = message.timestamp.ToBinary().ToString();
                if (!reasoningFoldouts.ContainsKey(foldoutKey))
                {
                    reasoningFoldouts[foldoutKey] = false;
                }
                
                reasoningFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                    reasoningFoldouts[foldoutKey], 
                    "💭 Reasoning for this action",
                    true,
                    new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Italic }
                );
                
                if (reasoningFoldouts[foldoutKey])
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    var reasoningStyle = new GUIStyle(EditorStyles.label) 
                    { 
                        richText = true, 
                        wordWrap = true,
                        fontSize = 10,
                        fontStyle = FontStyle.Italic
                    };
                    EditorGUILayout.LabelField(message.reasoningContent, reasoningStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }            
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawToolResultMessage(ChatMessage message)
        {
            string foldoutKey = message.timestamp.ToBinary().ToString();
            if (!reasoningFoldouts.ContainsKey(foldoutKey))
            {
                reasoningFoldouts[foldoutKey] = false;
            }

            EditorGUILayout.BeginVertical(toolMessageStyle);
            
            reasoningFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                reasoningFoldouts[foldoutKey], 
                $"🔧 Tool Called {message.toolName}",
                true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Italic }
            );
            
            if (reasoningFoldouts[foldoutKey])
            {
                EditorGUILayout.BeginVertical();
                           
                if (!string.IsNullOrEmpty(message.content))
                {
                    MarkdownRenderer.RenderMarkdown(message.content);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatistics(ChatStatistics stats)
        {
            var statsStyle = new GUIStyle(messageBoxStyle);
            statsStyle.normal.background = CreateColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.2f));
            
            EditorGUILayout.BeginVertical(statsStyle);
            
            EditorGUILayout.LabelField("<b>📊 Statistics</b>", new GUIStyle(EditorStyles.label) { richText = true });
            
            if (stats.totalTokens > 0)
            {
                EditorGUILayout.LabelField($"<b>Tokens:</b> {stats.completionTokens:N0} predicted / {stats.totalTokens:N0} total", 
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });
                
                float percentUsed = (stats.totalTokens / (float)ChatbotSettings.Instance.maxTokens) * 100f;
                EditorGUILayout.LabelField($"<b>Context Window:</b> {percentUsed:F1}% used ({stats.totalTokens:N0} / {ChatbotSettings.Instance.maxTokens:N0})", 
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });
            }
            
            if (stats.predictedN > 0 && stats.predictedMs > 0)
            {
                float tokensPerSecond = (stats.predictedN / (stats.predictedMs / 1000f));
                EditorGUILayout.LabelField($"<b>Generation Speed:</b> {tokensPerSecond:F2} tokens/sec", 
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });
                EditorGUILayout.LabelField($"<b>Generation Time:</b> {(stats.predictedMs / 1000f):F2}s for {stats.predictedN} tokens", 
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawAttachmentsSection()
        {
            EditorGUILayout.LabelField("Attachments (Unity Objects)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Add Object", GUILayout.Width(100)))
            {
                attachedObjects.Add(null);
            }
            
            if (attachedObjects.Count > 0 && GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                attachedObjects.Clear();
            }
            
            EditorGUILayout.EndHorizontal();
            
            attachmentsScrollPosition = EditorGUILayout.BeginScrollView(attachmentsScrollPosition, GUILayout.Height(100));
            
            for (int i = attachedObjects.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                
                attachedObjects[i] = EditorGUILayout.ObjectField(attachedObjects[i], typeof(Object), true);
                
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    attachedObjects.RemoveAt(i);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawInputArea()
        {
            EditorGUILayout.LabelField("Your Message", EditorStyles.boldLabel);
            
            currentPrompt = EditorGUILayout.TextArea(currentPrompt, GUILayout.Height(80));
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isWaitingForResponse && !string.IsNullOrWhiteSpace(currentPrompt);
            
            if (GUILayout.Button("Send", GUILayout.Height(40)) || 
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && Event.current.control))
            {
                SendMessage();
            }
            
            // Hack on domain reload continue working on an ongoing request also try to add any compilation errors to the response
            if ( isWaitingForResponse == false && iterationCount > 0)
            {
                Debug.Log($"!!! Resuming tool execution after domain reload... {iterationCount} iterations completed.");
                // Append any errors to the last response
                //var errors = CreateScriptAssetTool.GetCurrentErrors();
                //if ( errors.Count > 0 )
                //{
                //     chatSessions[selectedChatIndex].messages.Last().content += "\n\n**Errors during tool execution:**\n" + string.Join("\n", errors);
                //}

                isWaitingForResponse = true;
                SendMessage(true);
            }

            GUI.enabled = true;

            if (GUILayout.Button("Stop", GUILayout.Height(40)))
            {
                isWaitingForResponse = false;
            }          
            
            if (isWaitingForResponse)
            {
                EditorGUILayout.LabelField("Waiting for response...", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private ChatStatistics lastStatistics;
        
        private async void SendMessage(bool resume = false)
        {
            var currentChat = chatSessions[selectedChatIndex];

            if (!resume)
            {
                if (string.IsNullOrWhiteSpace(currentPrompt)) return;
   
                // Add user message
                var userMessage = new ChatMessage
                {
                    role = "user",
                    content = currentPrompt,
                    attachedObjects = new List<Object>(attachedObjects.Where(o => o != null))
                };
                
                currentChat.messages.Add(userMessage);
                
                string promptToSend = currentPrompt;
                var objectsToSend = new List<Object>(attachedObjects.Where(o => o != null));
                
                currentPrompt = "";
                attachedObjects.Clear();
                isWaitingForResponse = true;
                iterationCount = 0;
            }
            else
            {
                // Resuming after domain reload
                isWaitingForResponse = true;
            }
            
            Repaint();
            
            try
            {
                // Send to OpenAI (messages are added directly to session)
                lastStatistics = await OpenAIService.SendChatRequest(currentChat,  (int iteration) => iterationCount = iteration, ()  => isWaitingForResponse == false);
                
                // Auto-scroll to bottom
                chatHistoryScrollPosition.y = float.MaxValue;
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to get response: {ex.Message}", "OK");
                isWaitingForResponse = false;
                iterationCount = 0;
            }
            finally
            {
                isWaitingForResponse = false;
                SaveSessions();
                Repaint();
            }
        }

        private void CreateNewChat()
        {
            var newChat = new ChatSession
            {
                chatId = System.Guid.NewGuid().ToString(),
                chatName = $"Chat {chatSessions.Count + 1}",
                createdTime = System.DateTime.Now,
                messages = new List<ChatMessage>()
            };
            
            chatSessions.Add(newChat);
            selectedChatIndex = chatSessions.Count - 1;
            SaveSessions();
        }

        private void DeleteChat(int index)
        {
            if (EditorUtility.DisplayDialog("Delete Chat", 
                $"Are you sure you want to delete '{chatSessions[index].chatName}'?", 
                "Delete", "Cancel"))
            {
                chatSessions.RemoveAt(index);
                
                if (selectedChatIndex >= chatSessions.Count)
                {
                    selectedChatIndex = Mathf.Max(0, chatSessions.Count - 1);
                }
                
                if (chatSessions.Count == 0)
                {
                    CreateNewChat();
                }
                
                SaveSessions();
            }
        }

        private void RenameChat(int index)
        {
            string newName = EditorUtility.SaveFilePanel("Rename Chat", "", chatSessions[index].chatName, "");
            if (!string.IsNullOrEmpty(newName))
            {
                chatSessions[index].chatName = System.IO.Path.GetFileNameWithoutExtension(newName);
                SaveSessions();
            }
        }

        private void ClearChat(int index)
        {
            if (EditorUtility.DisplayDialog("Clear Chat", 
                "Are you sure you want to clear all messages in this chat?", 
                "Clear", "Cancel"))
            {
                chatSessions[index].messages.Clear();
                SaveSessions();
            }
        }

        private void SaveSessions()
        {
            var jsonArray = new SimpleJSON.JSONArray();
            foreach (var session in chatSessions)
            {
                var sessionObj = new SimpleJSON.JSONClass();
                sessionObj["chatId"] = session.chatId;
                sessionObj["chatName"] = session.chatName;
                sessionObj["createdTime"] = session.createdTime.ToBinary().ToString();
                
                var messagesArray = new SimpleJSON.JSONArray();
                foreach (var message in session.messages)
                {
                    var msgObj = new SimpleJSON.JSONClass();
                    msgObj["role"] = message.role;
                    msgObj["content"] = message.content ?? "";
                    msgObj["reasoningContent"] = message.reasoningContent ?? "";
                    msgObj["timestamp"] = message.timestamp.ToBinary().ToString();
                    
                    // Save tool message fields
                    if (message.role == "tool")
                    {
                        msgObj["toolCallId"] = message.toolCallId ?? "";
                        msgObj["toolName"] = message.toolName ?? "";
                    }
                    
                    // Save tool calls for assistant messages
                    if (message.toolCalls != null && message.toolCalls.Count > 0)
                    {
                        var toolCallsArray = new SimpleJSON.JSONArray();
                        foreach (var tc in message.toolCalls)
                        {
                            var tcObj = new SimpleJSON.JSONClass();
                            tcObj["id"] = tc.id;
                            tcObj["type"] = tc.type;
                            tcObj["functionName"] = tc.functionName;
                            tcObj["arguments"] = tc.arguments;
                            toolCallsArray[toolCallsArray.Count.ToString()] = tcObj;
                        }
                        msgObj["toolCalls"] = toolCallsArray;
                    }
                    
                    messagesArray[messagesArray.Count.ToString()] = msgObj;
                }
                sessionObj["messages"] = messagesArray;
                
                jsonArray[jsonArray.Count.ToString()] = sessionObj;
            }
            
            EditorPrefs.SetString("ChatbotSessions", jsonArray.ToString());
        }

        private void LoadSessions()
        {
            string json = EditorPrefs.GetString("ChatbotSessions", "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var jsonArray = SimpleJSON.JSONNode.Parse(json).AsArray;
                    chatSessions = new List<ChatSession>();
                    
                    if (jsonArray != null)
                    {
                        for (int i = 0; i < jsonArray.Count; i++)
                        {
                            var sessionNode = jsonArray[i];
                            var session = new ChatSession();
                            session.chatId = sessionNode["chatId"].Value;
                            session.chatName = sessionNode["chatName"].Value;
                            
                            long timeBinary;
                            if (long.TryParse(sessionNode["createdTime"].Value, out timeBinary))
                            {
                                session.createdTime = System.DateTime.FromBinary(timeBinary);
                            }
                            
                            session.messages = new List<ChatMessage>();
                            var messagesArray = sessionNode["messages"].AsArray;
                            if (messagesArray != null)
                            {
                                for (int j = 0; j < messagesArray.Count; j++)
                                {
                                    var msgNode = messagesArray[j];
                                    var message = new ChatMessage();
                                    message.role = msgNode["role"].Value;
                                    message.content = msgNode["content"].Value;
                                    message.reasoningContent = msgNode["reasoningContent"].Value;
                                    
                                    if (long.TryParse(msgNode["timestamp"].Value, out timeBinary))
                                    {
                                        message.timestamp = System.DateTime.FromBinary(timeBinary);
                                    }
                                    
                                    // Load tool message fields
                                    if (message.role == "tool")
                                    {
                                        message.toolCallId = msgNode["toolCallId"].Value;
                                        message.toolName = msgNode["toolName"].Value;
                                    }
                                    
                                    // Load tool calls for assistant messages
                                    var toolCallsArray = msgNode["toolCalls"].AsArray;
                                    if (toolCallsArray != null && toolCallsArray.Count > 0)
                                    {
                                        message.toolCalls = new List<ToolCallInfo>();
                                        for (int k = 0; k < toolCallsArray.Count; k++)
                                        {
                                            var tcNode = toolCallsArray[k];
                                            message.toolCalls.Add(new ToolCallInfo
                                            {
                                                id = tcNode["id"].Value,
                                                type = tcNode["type"].Value,
                                                functionName = tcNode["functionName"].Value,
                                                arguments = tcNode["arguments"].Value
                                            });
                                        }
                                    }
                                    
                                    session.messages.Add(message);
                                }
                            }
                            
                            chatSessions.Add(session);
                        }
                    }
                }
                catch
                {
                    chatSessions = new List<ChatSession>();
                }
            }
        }
    }
}
