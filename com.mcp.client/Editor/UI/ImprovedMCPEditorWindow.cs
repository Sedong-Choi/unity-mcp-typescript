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
    /// Improved Unity Editor window for the MCP client that uses the UI components.
    /// </summary>
    public class ImprovedMCPEditorWindow : EditorWindow
    {
        // Core services
        private MCPConnection _connection;
        private MCPRequestManager _requestManager;
        private MCPResponseHandler _responseHandler;
        
        // UI components
        private ConversationPanel _conversationPanel;
        private CodePreviewPanel _codePreviewPanel;
        
        // UI state
        private string _serverUrl = "ws://localhost:8765";
        private string _apiKey = "";
        private bool _autoConnect = true;
        private string _activeConversationId = "";
        private Vector2 _templateScrollPos;
        private bool _isConnected = false;
        private string _connectionStatusText = "Disconnected";
        private int _selectedTab = 0;
        private string[] _tabNames = new string[] { "Chat", "Code Preview", "Templates", "Settings" };
        private string _outputPath = "Assets/Scripts/Generated";
        
        // Template management
        private PromptTemplates _promptTemplates;
        private string _selectedCategory = "Components";
        private string _selectedTemplateKey = "";
        private string _customTemplateName = "";
        private string _customTemplateContent = "";
        
        // Layout
        private float _sidebarWidth = 200f;
        private float _resizeHandleWidth = 5f;
        private bool _isResizing = false;
        
        // Conversation and message history
        private List<string> _conversations = new List<string>();
        
        // Preferences keys
        private const string PREF_SERVER_URL = "MCP_SERVER_URL";
        private const string PREF_API_KEY = "MCP_API_KEY";
        private const string PREF_AUTO_CONNECT = "MCP_AUTO_CONNECT";
        private const string PREF_OUTPUT_PATH = "MCP_OUTPUT_PATH";
        private const string PREF_SIDEBAR_WIDTH = "MCP_SIDEBAR_WIDTH";
        
        [MenuItem("Window/AI/Improved MCP Client")]
        public static void ShowWindow()
        {
            ImprovedMCPEditorWindow window = GetWindow<ImprovedMCPEditorWindow>("MCP Client");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Load saved preferences
            _serverUrl = EditorPrefs.GetString(PREF_SERVER_URL, "ws://localhost:8765");
            _apiKey = EditorPrefs.GetString(PREF_API_KEY, "");
            _autoConnect = EditorPrefs.GetBool(PREF_AUTO_CONNECT, true);
            _outputPath = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Assets/Scripts/Generated");
            _sidebarWidth = EditorPrefs.GetFloat(PREF_SIDEBAR_WIDTH, 200f);
            
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
            EditorPrefs.SetFloat(PREF_SIDEBAR_WIDTH, _sidebarWidth);
            
            // Clean up UI components
            _conversationPanel?.OnDestroy();
            _codePreviewPanel?.OnDestroy();
        }
        
        private void InitializeServices()
        {
            // Get service references
            _connection = MCPConnection.Instance;
            _requestManager = MCPRequestManager.Instance;
            _responseHandler = MCPResponseHandler.Instance;
            _promptTemplates = PromptTemplates.Instance;
            
            // Configure file manager
            CodeFileManager.Instance.Configure(_outputPath);
            
            // Create UI components
            _conversationPanel = new ConversationPanel();
            _codePreviewPanel = new CodePreviewPanel();
            
            // Subscribe to connection status events
            _connection.OnConnected += HandleConnected;
            _connection.OnDisconnected += HandleDisconnected;
            
            // Subscribe to response handler events
            _responseHandler.OnConnectionStatusChanged += HandleConnectionStatusChanged;
        }
        
        private void OnGUI()
        {
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            
            // Left sidebar (conversation list)
            DrawSidebar();
            
            // Resize handle
            DrawResizeHandle();
            
            // Main content area
            EditorGUILayout.BeginVertical();
            
            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            
            // Tab content
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            
            switch (_selectedTab)
            {
                case 0: // Chat
                    DrawChatTab();
                    break;
                case 1: // Code Preview
                    DrawCodePreviewTab();
                    break;
                case 2: // Templates
                    DrawTemplatesTab();
                    break;
                case 3: // Settings
                    DrawSettingsTab();
                    break;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // Handle resizing logic
            HandleResizing();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Connection status
            GUILayout.Label($"Status: {_connectionStatusText}", GUILayout.Width(200));
            
            GUILayout.FlexibleSpace();
            
            // Connection button
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
        }
        
        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_sidebarWidth));
            
            // Conversations header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Conversations", EditorStyles.boldLabel);
            
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                CreateNewConversation();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Conversation list
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            
            foreach (string convId in _conversations)
            {
                bool isActive = convId == _activeConversationId;
                string description = _requestManager.GetConversationDescription(convId) ?? "Untitled";
                
                // Truncate long descriptions
                if (description.Length > 25)
                {
                    description = description.Substring(0, 22) + "...";
                }
                
                GUIStyle style = new GUIStyle(EditorStyles.toolbarButton);
                style.alignment = TextAnchor.MiddleLeft;
                style.normal.textColor = isActive ? Color.white : GUI.skin.button.normal.textColor;
                
                if (isActive)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.4f, 0.7f);
                }
                
                if (GUILayout.Button(description, style))
                {
                    SelectConversation(convId);
                }
                
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawResizeHandle()
        {
            EditorGUILayout.BeginVertical();
            
            // Make the resize handle
            Rect resizeHandleRect = GUILayoutUtility.GetRect(_resizeHandleWidth, 0f, GUILayout.ExpandHeight(true));
            resizeHandleRect.x = _sidebarWidth;
            resizeHandleRect.width = _resizeHandleWidth;
            
            // Change cursor when hovering over resize handle
            EditorGUIUtility.AddCursorRect(resizeHandleRect, MouseCursor.ResizeHorizontal);
            
            // Draw handle visually
            Color originalColor = GUI.color;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.DrawTexture(resizeHandleRect, EditorGUIUtility.whiteTexture);
            GUI.color = originalColor;
            
            // Handle mouse down
            if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
                Event.current.Use();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleResizing()
        {
            // Handle resizing logic
            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    _sidebarWidth += Event.current.delta.x;
                    _sidebarWidth = Mathf.Clamp(_sidebarWidth, 100f, position.width / 2);
                    Event.current.Use();
                    Repaint();
                }
            }
        }
        
        private void DrawChatTab()
        {
            if (_conversationPanel != null)
            {
                // Draw the conversation panel in the available area
                Rect contentRect = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                _conversationPanel.OnGUI(contentRect);
            }
            else
            {
                EditorGUILayout.HelpBox("Conversation panel not initialized.", MessageType.Error);
            }
        }
        
        private void DrawCodePreviewTab()
        {
            if (_codePreviewPanel != null)
            {
                // Draw the code preview panel in the available area
                Rect contentRect = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                _codePreviewPanel.OnGUI(contentRect);
            }
            else
            {
                EditorGUILayout.HelpBox("Code preview panel not initialized.", MessageType.Error);
            }
        }
        
        private void DrawTemplatesTab()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Left side - categories and templates
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
            
            // Get categories
            Dictionary<string, List<string>> categories = _promptTemplates.GetTemplatesByCategory();
            
            foreach (string category in categories.Keys)
            {
                bool isSelected = category == _selectedCategory;
                
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.4f, 0.7f);
                }
                
                if (GUILayout.Button(category, EditorStyles.toolbarButton))
                {
                    _selectedCategory = category;
                    _selectedTemplateKey = "";
                }
                
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.Space(10);
            
            // Templates in selected category
            if (!string.IsNullOrEmpty(_selectedCategory) && categories.ContainsKey(_selectedCategory))
            {
                EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);
                
                foreach (string key in categories[_selectedCategory])
                {
                    bool isSelected = key == _selectedTemplateKey;
                    string name = _promptTemplates.GetTemplateName(key);
                    
                    if (isSelected)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.7f);
                    }
                    
                    if (GUILayout.Button(name, EditorStyles.toolbarButton))
                    {
                        _selectedTemplateKey = key;
                    }
                    
                    GUI.backgroundColor = Color.white;
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // Right side - template content
            EditorGUILayout.BeginVertical();
            
            if (!string.IsNullOrEmpty(_selectedTemplateKey))
            {
                // Display and allow using the template
                string templateContent = _promptTemplates.GetTemplate(_selectedTemplateKey);
                string templateName = _promptTemplates.GetTemplateName(_selectedTemplateKey);
                
                EditorGUILayout.LabelField(templateName, EditorStyles.boldLabel);
                
                // Display template in a read-only text area
                EditorGUI.BeginDisabledGroup(true);
                _templateScrollPos = EditorGUILayout.BeginScrollView(_templateScrollPos, GUILayout.Height(200));
                EditorGUILayout.TextArea(templateContent, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUI.EndDisabledGroup();
                
                // Action buttons
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Use Template"))
                {
                    UseSelectedTemplate();
                }
                
                if (_selectedCategory == "Custom" && GUILayout.Button("Edit Template"))
                {
                    _customTemplateName = templateName;
                    _customTemplateContent = templateContent;
                }
                
                if (_selectedCategory == "Custom" && GUILayout.Button("Delete Template"))
                {
                    if (EditorUtility.DisplayDialog("Delete Template", 
                        $"Are you sure you want to delete the template '{templateName}'?", 
                        "Delete", "Cancel"))
                    {
                        _promptTemplates.RemoveCustomTemplate(_selectedTemplateKey);
                        _selectedTemplateKey = "";
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Create new custom template section
                EditorGUILayout.LabelField("Create New Template", EditorStyles.boldLabel);
                
                _customTemplateName = EditorGUILayout.TextField("Template Name", _customTemplateName);
                
                EditorGUILayout.LabelField("Content");
                _customTemplateContent = EditorGUILayout.TextArea(_customTemplateContent, GUILayout.Height(200));
                
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_customTemplateName) || string.IsNullOrEmpty(_customTemplateContent));
                
                if (GUILayout.Button("Save Template"))
                {
                    string key = _promptTemplates.AddCustomTemplate(_customTemplateName, _customTemplateContent);
                    _selectedCategory = "Custom";
                    _selectedTemplateKey = key;
                    _customTemplateName = "";
                    _customTemplateContent = "";
                }
                
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSettingsTab()
        {
            EditorGUILayout.Space(10);
            
            // Connection settings
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
            
            // Code generation settings
            EditorGUILayout.LabelField("Code Generation Settings", EditorStyles.boldLabel);
            
            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
            
            // Import/export templates
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Template Management", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Templates"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Export Templates",
                    "",
                    "mcp_templates.json",
                    "json");
                
                if (!string.IsNullOrEmpty(path))
                {
                    _promptTemplates.ExportTemplates(path);
                }
            }
            
            if (GUILayout.Button("Import Templates"))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Import Templates",
                    "",
                    "json");
                
                if (!string.IsNullOrEmpty(path))
                {
                    _promptTemplates.ImportTemplates(path);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            
            // Apply settings
            if (GUILayout.Button("Apply Settings"))
            {
                // Save preferences
                EditorPrefs.SetString(PREF_SERVER_URL, _serverUrl);
                EditorPrefs.SetString(PREF_API_KEY, _apiKey);
                EditorPrefs.SetBool(PREF_AUTO_CONNECT, _autoConnect);
                EditorPrefs.SetString(PREF_OUTPUT_PATH, _outputPath);
                
                // Update file manager
                CodeFileManager.Instance.Configure(_outputPath);
                
                // Update connection if needed
                if (_isConnected)
                {
                    Disconnect();
                    Connect();
                }
            }
        }
        
        private void Connect()
        {
            _connectionStatusText = "Connecting...";
            _connection.Connect(_serverUrl, _apiKey);
        }
        
        private void Disconnect()
        {
            _connection.Disconnect();
        }
        
        private void CreateNewConversation()
        {
            _activeConversationId = _requestManager.CreateConversation();
            _conversationPanel.SetConversation(_activeConversationId);
            RefreshConversationList();
        }
        
        private void SelectConversation(string conversationId)
        {
            _activeConversationId = conversationId;
            _conversationPanel.SetConversation(_activeConversationId);
            Repaint();
        }
        
        private void RefreshConversationList()
        {
            _conversations = _requestManager.GetActiveConversations();
            Repaint();
        }
        
        private void UseSelectedTemplate()
        {
            // Switch to chat tab
            _selectedTab = 0;
            
            // Create a new conversation if needed
            if (string.IsNullOrEmpty(_activeConversationId))
            {
                CreateNewConversation();
            }
            
            // Get the template content
            string templateContent = _promptTemplates.GetTemplate(_selectedTemplateKey);
            
            // Send the template as a message
            _requestManager.SendGenerateRequest(templateContent, _activeConversationId);
            
            Repaint();
        }
        
        // Event handlers
        
        private void HandleConnected()
        {
            _isConnected = true;
            _connectionStatusText = "Connected";
            
            // Refresh conversation list
            RefreshConversationList();
            
            // Create a new conversation if needed
            if (string.IsNullOrEmpty(_activeConversationId) || !_conversations.Contains(_activeConversationId))
            {
                CreateNewConversation();
            }
            
            Repaint();
        }
        
        private void HandleDisconnected()
        {
            _isConnected = false;
            _connectionStatusText = "Disconnected";
            Repaint();
        }
        
        private void HandleConnectionStatusChanged(bool isConnected, string sessionId)
        {
            _isConnected = isConnected;
            _connectionStatusText = isConnected ? "Connected" : "Disconnected";
            
            if (isConnected)
            {
                // Refresh conversation list
                RefreshConversationList();
                
                // Create a new conversation if needed
                if (string.IsNullOrEmpty(_activeConversationId) || !_conversations.Contains(_activeConversationId))
                {
                    CreateNewConversation();
                }
            }
            
            Repaint();
        }
        
        private void Update()
        {
            // This is needed to update the UI when running outside of OnGUI
            if (_conversationPanel != null && _conversationPanel.IsGenerating)
            {
                Repaint();
            }
        }
    }
}