using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Models;

namespace MCP.UI
{
    /// <summary>
    /// UI component for displaying and managing conversations with the MCP service.
    /// Can be used as a standalone control in custom editor windows.
    /// </summary>
    public class ConversationPanel
    {
        // References to core services
        private MCPRequestManager _requestManager;
        private MCPResponseHandler _responseHandler;
        
        // UI state
        private string _conversationId;
        private string _currentPrompt = "";
        private Vector2 _scrollPosition;
        private bool _isGenerating = false;
        private float _lastHeight = 0;
        private GUIStyle _userStyle;
        private GUIStyle _assistantStyle;
        private GUIStyle _systemStyle;
        private GUIStyle _inputStyle;
        
        // Max height for input field
        private const float MAX_INPUT_HEIGHT = 100f;
        
        // Events
        public delegate void GenerationStartedHandler(string conversationId);
        public delegate void GenerationCompletedHandler(string conversationId);
        
        public event GenerationStartedHandler OnGenerationStarted;
        public event GenerationCompletedHandler OnGenerationCompleted;
        
        public string ConversationId => _conversationId;
        public bool IsGenerating => _isGenerating;
        
        /// <summary>
        /// Initialize the conversation panel.
        /// </summary>
        /// <param name="conversationId">Optional conversation ID to load</param>
        public ConversationPanel(string conversationId = null)
        {
            _requestManager = MCPRequestManager.Instance;
            _responseHandler = MCPResponseHandler.Instance;
            
            // Create a new conversation if none provided
            _conversationId = conversationId ?? _requestManager.CreateConversation();
            
            // Subscribe to response events
            _responseHandler.OnTextResponse += HandleTextResponse;
            
            InitializeStyles();
        }
        
        /// <summary>
        /// Initialize GUI styles.
        /// </summary>
        private void InitializeStyles()
        {
            _userStyle = new GUIStyle(EditorStyles.helpBox);
            _userStyle.normal.textColor = new Color(0.2f, 0.6f, 1.0f);
            _userStyle.fontSize = 12;
            _userStyle.richText = true;
            _userStyle.wordWrap = true;
            _userStyle.alignment = TextAnchor.MiddleLeft;
            _userStyle.padding = new RectOffset(10, 10, 10, 10);
            
            _assistantStyle = new GUIStyle(EditorStyles.helpBox);
            _assistantStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
            _assistantStyle.fontSize = 12;
            _assistantStyle.richText = true;
            _assistantStyle.wordWrap = true;
            _assistantStyle.alignment = TextAnchor.MiddleLeft;
            _assistantStyle.padding = new RectOffset(10, 10, 10, 10);
            
            _systemStyle = new GUIStyle(EditorStyles.helpBox);
            _systemStyle.normal.textColor = new Color(0.8f, 0.8f, 0.2f);
            _systemStyle.fontSize = 12;
            _systemStyle.richText = true;
            _systemStyle.wordWrap = true;
            _systemStyle.alignment = TextAnchor.MiddleLeft;
            _systemStyle.padding = new RectOffset(10, 10, 10, 10);
            
            _inputStyle = new GUIStyle(EditorStyles.textArea);
            _inputStyle.wordWrap = true;
        }
        
        /// <summary>
        /// Draw the conversation panel.
        /// </summary>
        /// <param name="rect">Area to draw the panel</param>
        public void OnGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            
            // Message history
            float inputAreaHeight = Mathf.Min(MAX_INPUT_HEIGHT, _lastHeight + 20);
            float historyHeight = rect.height - inputAreaHeight - 50; // 50 for buttons
            
            EditorGUILayout.BeginVertical(GUILayout.Height(historyHeight));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            
            // Get message history for the current conversation
            List<MCPMessage> messages = _responseHandler.GetMessageHistory(_conversationId);
            
            foreach (MCPMessage message in messages)
            {
                // Skip empty messages
                if (string.IsNullOrWhiteSpace(message.Content))
                    continue;
                
                // Select style based on role
                GUIStyle style;
                string prefix;
                
                switch (message.MessageRole)
                {
                    case MCPMessage.Role.User:
                        style = _userStyle;
                        prefix = "<b>You:</b> ";
                        break;
                    case MCPMessage.Role.Assistant:
                        style = _assistantStyle;
                        prefix = "<b>AI:</b> ";
                        break;
                    case MCPMessage.Role.System:
                        style = _systemStyle;
                        prefix = "<b>System:</b> ";
                        break;
                    default:
                        style = _userStyle;
                        prefix = "";
                        break;
                }
                
                // Display message with proper formatting
                string displayText = prefix + FormatMessageContent(message.Content);
                
                // If the message is not complete, add an indicator
                if (!message.IsComplete && message.MessageRole == MCPMessage.Role.Assistant)
                {
                    displayText += " <i>(generating...)</i>";
                }
                
                EditorGUILayout.LabelField(displayText, style);
                EditorGUILayout.Space(10);
            }
            
            // Auto-scroll to bottom when new messages arrive
            if (_isGenerating)
            {
                _scrollPosition = new Vector2(_scrollPosition.x, float.MaxValue);
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            // Input area
            EditorGUILayout.Space(10);
            
            // Calculate ideal height based on content
            float idealHeight = _inputStyle.CalcHeight(new GUIContent(_currentPrompt), rect.width - 10);
            _lastHeight = Mathf.Min(MAX_INPUT_HEIGHT, idealHeight);
            
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, _inputStyle, GUILayout.Height(_lastHeight));
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(!MCPConnection.Instance.IsConnected || string.IsNullOrEmpty(_currentPrompt) || _isGenerating);
            
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
            
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Reset Conversation", 
                    "Are you sure you want to reset this conversation? This will clear all message history.", 
                    "Reset", "Cancel"))
                {
                    ResetConversation();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Format message content for display.
        /// </summary>
        private string FormatMessageContent(string content)
        {
            // Replace code blocks and other Markdown with rich text
            string formatted = content;
            
            // Handle code blocks (```)
            int codeStart = formatted.IndexOf("```");
            while (codeStart >= 0)
            {
                int codeEnd = formatted.IndexOf("```", codeStart + 3);
                if (codeEnd < 0) break;
                
                // Extract the code block
                string codeBlock = formatted.Substring(codeStart, codeEnd - codeStart + 3);
                
                // Create replacement with monospace font
                string language = "";
                string codeContent = codeBlock.Substring(3);
                int newlinePos = codeContent.IndexOf('\n');
                if (newlinePos > 0)
                {
                    language = codeContent.Substring(0, newlinePos).Trim();
                    codeContent = codeContent.Substring(newlinePos + 1);
                }
                
                // Remove trailing ```
                codeContent = codeContent.Substring(0, codeContent.Length - 3);
                
                // Create replacement
                string replacement = $"<color=#888888><i>{language}</i></color>\n<color=#DDDDDD>{codeContent}</color>";
                
                // Apply replacement
                formatted = formatted.Replace(codeBlock, replacement);
                
                // Find next code block
                codeStart = formatted.IndexOf("```");
            }
            
            return formatted;
        }
        
        /// <summary>
        /// Send a chat message.
        /// </summary>
        private void SendChatMessage()
        {
            if (string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // Create a user message
            MCPMessage userMessage = MCPMessage.CreateUserMessage(_currentPrompt);
            
            // Add to history
            _responseHandler.AddMessageToHistory(_conversationId, userMessage);
            
            // Send to server
            _isGenerating = true;
            OnGenerationStarted?.Invoke(_conversationId);
            _requestManager.SendChatMessage(_currentPrompt, _conversationId);
            
            // Clear input
            _currentPrompt = "";
        }
        
        /// <summary>
        /// Send a code generation request.
        /// </summary>
        private void SendGenerateRequest()
        {
            if (string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // Create a user message
            MCPMessage userMessage = MCPMessage.CreateUserMessage(_currentPrompt);
            
            // Add to history
            _responseHandler.AddMessageToHistory(_conversationId, userMessage);
            
            // Send to server
            _isGenerating = true;
            OnGenerationStarted?.Invoke(_conversationId);
            _requestManager.SendGenerateRequest(_currentPrompt, _conversationId);
            
            // Clear input
            _currentPrompt = "";
        }
        
        /// <summary>
        /// Reset the current conversation.
        /// </summary>
        private void ResetConversation()
        {
            _requestManager.ResetConversation(_conversationId);
            _responseHandler.ClearMessageHistory(_conversationId);
        }
        
        /// <summary>
        /// Set the conversation ID to use.
        /// </summary>
        public void SetConversation(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;
            
            _conversationId = conversationId;
        }
        
        /// <summary>
        /// Handle text response from the server.
        /// </summary>
        private void HandleTextResponse(string conversationId, string text, bool isDone)
        {
            if (conversationId != _conversationId)
                return;
            
            if (isDone)
            {
                _isGenerating = false;
                OnGenerationCompleted?.Invoke(conversationId);
            }
        }
        
        /// <summary>
        /// Clean up when destroying this panel.
        /// </summary>
        public void OnDestroy()
        {
            // Unsubscribe from events
            _responseHandler.OnTextResponse -= HandleTextResponse;
        }
    }
}