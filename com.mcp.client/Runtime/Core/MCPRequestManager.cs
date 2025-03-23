using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace MCP.Core
{
    /// <summary>
    /// Manages requests to the MCP server, including message creation and tracking.
    /// </summary>
    public class MCPRequestManager : MonoBehaviour
    {
        private static MCPRequestManager _instance;
        
        // References
        private MCPConnection _connection;
        
        // Tracking active conversations
        private Dictionary<string, string> _activeConversations = new Dictionary<string, string>();
        
        // Default options
        [SerializeField] private bool _defaultStreamResponse = true;
        [SerializeField] private float _defaultTemperature = 0.7f;
        [SerializeField] private int _defaultMaxTokens = 2048;
        [SerializeField] private string _defaultModel = "gemma3:12b";
        
        // Request tracking
        public delegate void RequestSentHandler(string conversationId, string requestType);
        public event RequestSentHandler OnRequestSent;
        
        public static MCPRequestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    MCPRequestManager existingManager = UnityEngine.Object.FindObjectOfType<MCPRequestManager>();
                    
                    if (existingManager != null)
                    {
                        _instance = existingManager;
                    }
                    else
                    {
                        GameObject go = new GameObject("MCPRequestManager");
                        _instance = go.AddComponent<MCPRequestManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        public void Initialize()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                // 이미 다른 인스턴스가 있는 경우 이 객체를 제거
                Debug.LogWarning("중복된 MCPRequestManager 인스턴스가 감지되어 제거됩니다.");
                Destroy(gameObject);
            }
        }
        
        private void Awake()
        {
            // Initialize 메서드를 통해 중복 확인 및 초기화를 하므로 여기서는 간단히 처리
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Get reference to the connection manager
            _connection = MCPConnection.Instance;
        }
        
        /// <summary>
        /// Create a new chat conversation.
        /// </summary>
        /// <param name="initialPrompt">Optional initial prompt to start the conversation</param>
        /// <returns>Conversation ID</returns>
        public string CreateConversation(string initialPrompt = null)
        {
            string conversationId = "conv_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            
            if (!string.IsNullOrEmpty(initialPrompt))
            {
                SendChatMessage(initialPrompt, conversationId);
            }
            
            return conversationId;
        }
        
        /// <summary>
        /// Send a chat message to the MCP server.
        /// </summary>
        /// <param name="message">The user message</param>
        /// <param name="conversationId">Optional conversation ID (new one is created if not provided)</param>
        /// <param name="stream">Whether to stream the response</param>
        /// <param name="temperature">Generation temperature (creativity)</param>
        /// <param name="maxTokens">Maximum number of tokens to generate</param>
        /// <param name="model">Model to use for generation</param>
        /// <returns>The conversation ID used</returns>
        public string SendChatMessage(
            string message,
            string conversationId = null,
            bool? stream = null,
            float? temperature = null,
            int? maxTokens = null,
            string model = null)
        {
            // Create conversation if needed
            if (string.IsNullOrEmpty(conversationId))
            {
                conversationId = CreateConversation();
            }
            
            // Create request object
            var request = new
            {
                command = "chat",
                conversationId,
                message,
                options = new
                {
                    stream = stream ?? _defaultStreamResponse,
                    temperature = temperature ?? _defaultTemperature,
                    maxTokens = maxTokens ?? _defaultMaxTokens,
                    model = model ?? _defaultModel
                }
            };
            
            // Convert to JSON
            string jsonRequest = JsonConvert.SerializeObject(request);
            
            // Send to server
            _connection.SendMessage(jsonRequest);
            
            // Track the conversation
            if (!_activeConversations.ContainsKey(conversationId))
            {
                _activeConversations.Add(conversationId, message);
            }
            
            // Notify listeners
            OnRequestSent?.Invoke(conversationId, "chat");
            
            return conversationId;
        }
        
        /// <summary>
        /// Send a request to generate code.
        /// </summary>
        /// <param name="prompt">The code generation prompt</param>
        /// <param name="conversationId">Optional conversation ID</param>
        /// <param name="stream">Whether to stream the response</param>
        /// <param name="temperature">Generation temperature (creativity)</param>
        /// <param name="maxTokens">Maximum number of tokens to generate</param>
        /// <param name="model">Model to use for generation</param>
        /// <returns>The conversation ID used</returns>
        public string SendGenerateRequest(
            string prompt,
            string conversationId = null,
            bool? stream = null,
            float? temperature = null,
            int? maxTokens = null,
            string model = null)
        {
            // Create conversation if needed
            if (string.IsNullOrEmpty(conversationId))
            {
                conversationId = CreateConversation();
            }
            
            // Create request object
            var request = new
            {
                command = "generate",
                conversationId,
                message = prompt,
                options = new
                {
                    stream = stream ?? _defaultStreamResponse,
                    temperature = temperature ?? _defaultTemperature,
                    maxTokens = maxTokens ?? _defaultMaxTokens,
                    model = model ?? _defaultModel
                }
            };
            
            // Convert to JSON
            string jsonRequest = JsonConvert.SerializeObject(request);
            
            // Send to server
            _connection.SendMessage(jsonRequest);
            
            // Track the conversation
            if (!_activeConversations.ContainsKey(conversationId))
            {
                _activeConversations.Add(conversationId, prompt);
            }
            
            // Notify listeners
            OnRequestSent?.Invoke(conversationId, "generate");
            
            return conversationId;
        }
        
        /// <summary>
        /// Reset a conversation, clearing its history.
        /// </summary>
        /// <param name="conversationId">ID of the conversation to reset</param>
        public void ResetConversation(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;
            
            // Create reset request
            var request = new
            {
                command = "reset",
                conversationId
            };
            
            // Convert to JSON
            string jsonRequest = JsonConvert.SerializeObject(request);
            
            // Send to server
            _connection.SendMessage(jsonRequest);
            
            // Update tracking
            if (_activeConversations.ContainsKey(conversationId))
            {
                _activeConversations[conversationId] = "Reset at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            
            // Notify listeners
            OnRequestSent?.Invoke(conversationId, "reset");
        }
        
        /// <summary>
        /// Get all active conversation IDs.
        /// </summary>
        /// <returns>List of active conversation IDs</returns>
        public List<string> GetActiveConversations()
        {
            return new List<string>(_activeConversations.Keys);
        }
        
        /// <summary>
        /// Get the initial prompt or description for a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <returns>Description or null if not found</returns>
        public string GetConversationDescription(string conversationId)
        {
            if (_activeConversations.TryGetValue(conversationId, out string description))
            {
                return description;
            }
            return null;
        }
        
        /// <summary>
        /// Check if the connection to the server is established.
        /// </summary>
        /// <returns>True if connected, false otherwise</returns>
        public bool IsConnected()
        {
            return _connection != null && _connection.IsConnected;
        }
        
        /// <summary>
        /// Connect to the MCP server.
        /// </summary>
        /// <param name="serverUrl">Optional server URL override</param>
        /// <param name="apiKey">Optional API key override</param>
        public void Connect(string serverUrl = null, string apiKey = null)
        {
            _connection.Connect(serverUrl, apiKey);
        }
    }
}