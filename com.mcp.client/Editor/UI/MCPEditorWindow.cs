using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Models;
using MCP.Utils;

namespace MCP.UI
{
    /// <summary>
    /// Unity Editor window for the MCP client.
    /// </summary>
    public class MCPEditorWindow : EditorWindow
    {
        // Core services
        private MCPConnection _connection;
        private MCPRequestManager _requestManager;
        private MCPResponseHandler _responseHandler;
        private CodeFileManager _codeFileManager;
        
        // UI State
        private string _serverUrl = "ws://localhost:8765";
        private string _apiKey = "";
        private bool _autoConnect = true;
        private string _currentPrompt = "";
        private string _activeConversationId = "";
        private Vector2 _conversationListScrollPos;
        private Vector2 _messageScrollPos;
        private Vector2 _modificationScrollPos;
        private bool _isConnected = false;
        private string _connectionStatusText = "Disconnected";
        private int _selectedTab = 0;
        private string[] _tabNames = new string[] { "Chat", "Settings", "Code" };
        private bool _isGenerating = false;
        private string _outputPath = "Assets/Scripts/Generated";
        
        // Conversation and message history
        private List<string> _conversations = new List<string>();
        private List<MCPMessage> _messages = new List<MCPMessage>();
        private List<CodeModification> _codeModifications = new List<CodeModification>();
        
        // Preferences keys
        private const string PREF_SERVER_URL = "MCP_SERVER_URL";
        private const string PREF_API_KEY = "MCP_API_KEY";
        private const string PREF_AUTO_CONNECT = "MCP_AUTO_CONNECT";
        private const string PREF_OUTPUT_PATH = "MCP_OUTPUT_PATH";
        
        [MenuItem("Window/AI/MCP Client")]
        public static void ShowWindow()
        {
            MCPEditorWindow window = GetWindow<MCPEditorWindow>("MCP Client");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Load saved preferences
            _serverUrl = EditorPrefs.GetString(PREF_SERVER_URL, "ws://localhost:8765");
            _apiKey = EditorPrefs.GetString(PREF_API_KEY, "");
            _autoConnect = EditorPrefs.GetBool(PREF_AUTO_CONNECT, true);
            _outputPath = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Assets/Scripts/Generated");
            
            InitializeServices();
            
            if (_autoConnect)
            {
                Connect();
            }
        }
        
        private void OnDisable()
        {
            // Save preferences
            EditorPrefs.SetString(PREF_SERVER_URL, _serverUrl);
            EditorPrefs.SetString(PREF_API_KEY, _apiKey);
            EditorPrefs.SetBool(PREF_AUTO_CONNECT, _autoConnect);
            EditorPrefs.SetString(PREF_OUTPUT_PATH, _outputPath);
            
            // Unsubscribe from events
            if (_responseHandler != null)
            {
                _responseHandler.OnTextResponse -= HandleTextResponse;
                _responseHandler.OnCodeModifications -= HandleCodeModifications;
                _responseHandler.OnError -= HandleError;
                _responseHandler.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            }
        }
        
        private void InitializeServices()
        {
            // Get service references
            _connection = MCPConnection.Instance;
            _requestManager = MCPRequestManager.Instance;
            _responseHandler = MCPResponseHandler.Instance;
            _codeFileManager = CodeFileManager.Instance;
            
            // Configure file manager
            _codeFileManager.Configure(_outputPath);
            
            // Subscribe to events
            _responseHandler.OnTextResponse += HandleTextResponse;
            _responseHandler.OnCodeModifications += HandleCodeModifications;
            _responseHandler.OnError += HandleError;
            _responseHandler.OnConnectionStatusChanged += HandleConnectionStatusChanged;
        }
        
        private void OnGUI()
        {
            // Connection status
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Status: {_connectionStatusText}", GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button(_isConnected ? "Disconnect" : "Connect", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                if (_isConnected)
                {
                    Disconnect();
                }
                else
                {
                    Connect();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Main content tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            
            switch (_selectedTab)
            {
                case 0: // Chat
                    DrawChatTab();
                    break;
                case 1: // Settings
                    DrawSettingsTab();
                    break;
                case 2: // Code
                    DrawCodeTab();
                    break;
            }
        }
        
        private void DrawChatTab()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Left panel - conversation list
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Conversations", EditorStyles.boldLabel);
            
            if (GUILayout.Button("New Conversation"))
            {
                _activeConversationId = _requestManager.CreateConversation();
                RefreshConversationList();
                _messages.Clear();
            }
            
            _conversationListScrollPos = EditorGUILayout.BeginScrollView(_conversationListScrollPos, GUILayout.ExpandHeight(true));
            
            foreach (string convId in _conversations)
            {
                bool isActive = convId == _activeConversationId;
                string description = _requestManager.GetConversationDescription(convId) ?? "Untitled";
                
                // Truncate long descriptions
                if (description.Length > 25)
                {
                    description = description.Substring(0, 22) + "...";
                }
                
                GUIStyle style = new GUIStyle(GUI.skin.button);
                if (isActive)
                {
                    style.normal.background = EditorGUIUtility.whiteTexture;
                    style.normal.textColor = Color.black;
                }
                
                if (GUILayout.Button(description, style))
                {
                    _activeConversationId = convId;
                    RefreshMessageHistory();
                }
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            // Right panel - message history and input
            EditorGUILayout.BeginVertical();
            
            // Message history
            EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);
            _messageScrollPos = EditorGUILayout.BeginScrollView(_messageScrollPos, GUILayout.ExpandHeight(true));
            
            foreach (MCPMessage message in _messages)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Role label
                GUIStyle roleStyle = new GUIStyle(EditorStyles.boldLabel);
                switch (message.MessageRole)
                {
                    case MCPMessage.Role.User:
                        roleStyle.normal.textColor = new Color(0.2f, 0.6f, 1.0f);
                        EditorGUILayout.LabelField("You:", roleStyle, GUILayout.Width(50));
                        break;
                    case MCPMessage.Role.Assistant:
                        roleStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("AI:", roleStyle, GUILayout.Width(50));
                        break;
                    case MCPMessage.Role.System:
                        roleStyle.normal.textColor = new Color(0.8f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("System:", roleStyle, GUILayout.Width(50));
                        break;
                }
                
                // Message content
                EditorGUILayout.LabelField(message.Content, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(10);
            }
            
            EditorGUILayout.EndScrollView();
            
            // Input area
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUIStyle promptStyle = new GUIStyle(EditorStyles.textArea);
            promptStyle.wordWrap = true;
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, promptStyle, GUILayout.Height(60));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(!_isConnected || string.IsNullOrEmpty(_currentPrompt) || _isGenerating);
            
            if (GUILayout.Button("Send Message"))
            {
                SendChatMessage();
            }
            
            if (GUILayout.Button("Generate Code"))
            {
                SendGenerateRequest();
            }
            
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Clear"))
            {
                _currentPrompt = "";
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSettingsTab()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Connection Settings", EditorStyles.boldLabel);
            
            _serverUrl = EditorGUILayout.TextField("Server URL", _serverUrl);
            
            EditorGUILayout.BeginHorizontal();
            _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _apiKey = "";
            }
            EditorGUILayout.EndHorizontal();
            
            _autoConnect = EditorGUILayout.Toggle("Auto Connect", _autoConnect);
            
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Code Generation Settings", EditorStyles.boldLabel);
            
            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
            
            if (GUILayout.Button("Apply Settings"))
            {
                // Save preferences
                EditorPrefs.SetString(PREF_SERVER_URL, _serverUrl);
                EditorPrefs.SetString(PREF_API_KEY, _apiKey);
                EditorPrefs.SetBool(PREF_AUTO_CONNECT, _autoConnect);
                EditorPrefs.SetString(PREF_OUTPUT_PATH, _outputPath);
                
                // Update file manager
                _codeFileManager.Configure(_outputPath);
                
                // Update connection if needed
                if (_isConnected)
                {
                    Disconnect();
                    Connect();
                }
            }
        }
        
        private void DrawCodeTab()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Recent Code Modifications", EditorStyles.boldLabel);
            
            _modificationScrollPos = EditorGUILayout.BeginScrollView(_modificationScrollPos);
            
            foreach (CodeModification mod in _codeModifications)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                
                EditorGUILayout.BeginHorizontal();
                GUIStyle fileStyle = new GUIStyle(EditorStyles.boldLabel);
                
                // Color based on success/failure
                if (mod.Success)
                {
                    fileStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                }
                else
                {
                    fileStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f);
                }
                
                EditorGUILayout.LabelField(mod.Filename, fileStyle);
                
                GUIStyle operationStyle = new GUIStyle(EditorStyles.label);
                string operationText = mod.Operation.ToUpper();
                
                switch (operationText)
                {
                    case "CREATE":
                        operationStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                        break;
                    case "MODIFY":
                        operationStyle.normal.textColor = new Color(0.2f, 0.6f, 1.0f);
                        break;
                    case "DELETE":
                        operationStyle.normal.textColor = new Color(1.0f, 0.6f, 0.2f);
                        break;
                }
                
                EditorGUILayout.LabelField(operationText, operationStyle, GUILayout.Width(80));
                
                if (GUILayout.Button("View", GUILayout.Width(60)))
                {
                    // Open the file in the editor
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(
                        System.IO.Path.Combine(Application.dataPath, "..", mod.FilePath),
                        1
                    );
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField(mod.FilePath);
                
                if (!mod.Success && !string.IsNullOrEmpty(mod.Error))
                {
                    EditorGUILayout.HelpBox(mod.Error, MessageType.Error);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void Connect()
        {
            _connectionStatusText = "Connecting...";
            _requestManager.Connect(_serverUrl, _apiKey);
        }
        
        private void Disconnect()
        {
            _connection.Disconnect();
            _isConnected = false;
            _connectionStatusText = "Disconnected";
        }
        
        private void SendChatMessage()
        {
            if (string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // Create a user message
            MCPMessage userMessage = MCPMessage.CreateUserMessage(_currentPrompt);
            
            // Add to history
            _responseHandler.AddMessageToHistory(_activeConversationId, userMessage);
            
            // Send to server
            _isGenerating = true;
            _requestManager.SendChatMessage(_currentPrompt, _activeConversationId);
            
            // Clear input
            _currentPrompt = "";
            
            // Refresh view
            RefreshMessageHistory();
        }
        
        private void SendGenerateRequest()
        {
            if (string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // Create a user message
            MCPMessage userMessage = MCPMessage.CreateUserMessage(_currentPrompt);
            
            // Add to history
            _responseHandler.AddMessageToHistory(_activeConversationId, userMessage);
            
            // Send to server
            _isGenerating = true;
            _requestManager.SendGenerateRequest(_currentPrompt, _activeConversationId);
            
            // Clear input
            _currentPrompt = "";
            
            // Refresh view
            RefreshMessageHistory();
        }
        
        private void RefreshConversationList()
        {
            _conversations = _requestManager.GetActiveConversations();
            Repaint();
        }
        
        private void RefreshMessageHistory()
        {
            if (string.IsNullOrEmpty(_activeConversationId))
            {
                _messages.Clear();
                return;
            }
            
            _messages = _responseHandler.GetMessageHistory(_activeConversationId);
            
            // Scroll to bottom
            EditorApplication.delayCall += () => {
                _messageScrollPos = new Vector2(_messageScrollPos.x, float.MaxValue);
                Repaint();
            };
        }
        
        // Event handlers
        
        private void HandleTextResponse(string conversationId, string text, bool isDone)
        {
            _isGenerating = !isDone;
            
            if (conversationId == _activeConversationId)
            {
                RefreshMessageHistory();
            }
            
            // Make sure to repaint the window to show the updated message
            Repaint();
        }
        
        private void HandleCodeModifications(string conversationId, List<CodeModification> modifications)
        {
            if (modifications == null || modifications.Count == 0)
                return;
            
            // Update code modifications list
            _codeModifications.InsertRange(0, modifications);
            
            // Limit to the most recent 20 modifications
            if (_codeModifications.Count > 20)
            {
                _codeModifications.RemoveRange(20, _codeModifications.Count - 20);
            }
            
            // Repaint the window
            Repaint();
        }
        
        private void HandleError(string conversationId, string errorMessage)
        {
            _isGenerating = false;
            EditorUtility.DisplayDialog("MCP Error", errorMessage, "OK");
            Repaint();
        }
        
        private void HandleConnectionStatusChanged(bool isConnected, string sessionId)
        {
            _isConnected = isConnected;
            _connectionStatusText = isConnected ? "Connected" : "Disconnected";
            
            if (isConnected)
            {
                // Refresh conversation list when connected
                RefreshConversationList();
                
                // Create a new conversation if needed
                if (string.IsNullOrEmpty(_activeConversationId) || !_conversations.Contains(_activeConversationId))
                {
                    _activeConversationId = _requestManager.CreateConversation();
                    RefreshConversationList();
                }
            }
            
            Repaint();
        }
        
        private void Update()
        {
            // This is needed to update the UI when running outside of OnGUI
            if (_isGenerating)
            {
                Repaint();
            }
        }
    }
}