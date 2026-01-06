using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAgents.Editor
{
    public class ChatbotSettings : ScriptableObject
    {
        private static ChatbotSettings instance;

        public static ChatbotSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = CreateInstance<ChatbotSettings>();
                    instance.LoadFromEditorPrefs();
                }
                return instance;
            }
        }

        public string apiKey = "";
        public string apiEndpoint = "https://api.openai.com/v1/chat/completions";
        public string model = "gpt-4";
        public float temperature = 0.7f;
        public int maxTokens = 2000;
        public bool enableTools = false;
        public bool autoExecuteTools = true;

        public void LoadFromEditorPrefs()
        {
            apiKey = EditorPrefs.GetString("Chatbot_ApiKey", "");
            apiEndpoint = EditorPrefs.GetString("Chatbot_ApiEndpoint", "https://api.openai.com/v1/chat/completions");
            model = EditorPrefs.GetString("Chatbot_Model", "gpt-4");
            temperature = EditorPrefs.GetFloat("Chatbot_Temperature", 0.7f);
            maxTokens = EditorPrefs.GetInt("Chatbot_MaxTokens", 2000);
            enableTools = EditorPrefs.GetBool("Chatbot_EnableTools", false);
            autoExecuteTools = EditorPrefs.GetBool("Chatbot_AutoExecuteTools", true);
        }

        public void SaveToEditorPrefs()
        {
            EditorPrefs.SetString("Chatbot_ApiKey", apiKey);
            EditorPrefs.SetString("Chatbot_ApiEndpoint", apiEndpoint);
            EditorPrefs.SetString("Chatbot_Model", model);
            EditorPrefs.SetFloat("Chatbot_Temperature", temperature);
            EditorPrefs.SetInt("Chatbot_MaxTokens", maxTokens);
            EditorPrefs.SetBool("Chatbot_EnableTools", enableTools);
            EditorPrefs.SetBool("Chatbot_AutoExecuteTools", autoExecuteTools);
        }
    }
}
