using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using WebSocketSharp;
#endif

namespace MCP.Core
{
    /// <summary>
    /// Handles the WebSocket connection to the MCP server.
    /// Implements a singleton pattern for easy access throughout the project.
    /// </summary>
    public class MCPConnection : MonoBehaviour
    {
        // Singleton instance
        private static MCPConnection _instance;
        
        // WebSocket connection
#if UNITY_WEBGL && !UNITY_EDITOR
        private int _webSocketInstance = -1;
#else
        private WebSocket _webSocket;
#endif
        
        // Connection status
        private bool _isConnected = false;
        private bool _isConnecting = false;
        private bool _autoReconnect = true;
        
        // Server information
        [SerializeField] private string _serverUrl = "ws://localhost:8765";
        [SerializeField] private string _apiKey = "";
        
        // Reconnection settings
        [SerializeField] private int _reconnectDelay = 5;
        [SerializeField] private int _maxReconnectAttempts = 5;
        private int _reconnectAttempts = 0;
        
        // Connection events
        public delegate void ConnectionEventHandler();
        public event ConnectionEventHandler OnConnected;
        public event ConnectionEventHandler OnDisconnected;
        public event ConnectionEventHandler OnReconnecting;
        
        // Message events
        public delegate void MessageEventHandler(string message);
        public event MessageEventHandler OnMessageReceived;
        public event MessageEventHandler OnError;
        
        // Session information received from server
        private string _sessionId;
        
        public static MCPConnection Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MCPConnection");
                    _instance = go.AddComponent<MCPConnection>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        public bool IsConnected => _isConnected;
        public string SessionId => _sessionId;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int WebSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern int WebSocketState(int instanceId);

        [DllImport("__Internal")]
        private static extern void WebSocketSend(int instanceId, string message);

        [DllImport("__Internal")]
        private static extern void WebSocketConnect(int instanceId);

        [DllImport("__Internal")]
        private static extern void WebSocketClose(int instanceId);

        [DllImport("__Internal")]
        private static extern void WebSocketAddMessageCallback(int instanceId, Action<string> callback);

        [DllImport("__Internal")]
        private static extern void WebSocketAddErrorCallback(int instanceId, Action<string> callback);

        [DllImport("__Internal")]
        private static extern void WebSocketAddOpenCallback(int instanceId, Action callback);

        [DllImport("__Internal")]
        private static extern void WebSocketAddCloseCallback(int instanceId, Action callback);
#endif
        
        /// <summary>
        /// Connect to the MCP server.
        /// </summary>
        /// <param name="serverUrl">Optional server URL override</param>
        /// <param name="apiKey">Optional API key override</param>
        public void Connect(string serverUrl = null, string apiKey = null)
        {
            if (_isConnected || _isConnecting)
                return;
            
            _isConnecting = true;
            
            // Use provided parameters or defaults
            string url = serverUrl ?? _serverUrl;
            string key = apiKey ?? _apiKey;
            
            // Add API key to URL if provided
            if (!string.IsNullOrEmpty(key))
            {
                url += $"?api_key={key}";
            }
            
            Debug.Log($"Connecting to MCP server: {url}");
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                _webSocketInstance = WebSocketCreate(url);
                
                WebSocketAddOpenCallback(_webSocketInstance, () => {
                    _isConnected = true;
                    _isConnecting = false;
                    _reconnectAttempts = 0;
                    Debug.Log("Connected to MCP server");
                    OnConnected?.Invoke();
                });
                
                WebSocketAddMessageCallback(_webSocketInstance, (message) => {
                    ProcessReceivedMessage(message);
                });
                
                WebSocketAddErrorCallback(_webSocketInstance, (error) => {
                    Debug.LogError($"WebSocket error: {error}");
                    OnError?.Invoke($"WebSocket error: {error}");
                });
                
                WebSocketAddCloseCallback(_webSocketInstance, () => {
                    _isConnected = false;
                    _isConnecting = false;
                    Debug.Log("Disconnected from MCP server");
                    OnDisconnected?.Invoke();
                    
                    if (_autoReconnect)
                    {
                        TryReconnect();
                    }
                });
                
                WebSocketConnect(_webSocketInstance);
#else
                _webSocket = new WebSocket(url);
                
                // Set up event handlers
                _webSocket.OnOpen += OnWebSocketOpen;
                _webSocket.OnClose += OnWebSocketClose;
                _webSocket.OnMessage += OnWebSocketMessage;
                _webSocket.OnError += OnWebSocketError;
                
                // Connect
                _webSocket.Connect();
#endif
            }
            catch (Exception e)
            {
                _isConnecting = false;
                Debug.LogError($"Error connecting to MCP server: {e.Message}");
                OnError?.Invoke($"Connection error: {e.Message}");
                
                if (_autoReconnect)
                {
                    TryReconnect();
                }
            }
        }
        
        /// <summary>
        /// Disconnect from the MCP server.
        /// </summary>
        public void Disconnect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_webSocketInstance != -1 && (_isConnected || _isConnecting))
            {
                _autoReconnect = false; // Do not reconnect after manual disconnect
                
                try
                {
                    WebSocketClose(_webSocketInstance);
                    _webSocketInstance = -1;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error disconnecting from MCP server: {e.Message}");
                }
                
                _isConnected = false;
                _isConnecting = false;
            }
#else
            if (_webSocket != null && (_isConnected || _isConnecting))
            {
                _autoReconnect = false; // Do not reconnect after manual disconnect
                
                try
                {
                    _webSocket.Close();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error disconnecting from MCP server: {e.Message}");
                }
                
                _isConnected = false;
                _isConnecting = false;
            }
#endif
        }
        
        /// <summary>
        /// Send a message to the MCP server.
        /// </summary>
        /// <param name="message">JSON message string</param>
        public new void SendMessage(string message)
        {
            if (!_isConnected)
            {
                Debug.LogWarning("Cannot send message: Not connected to MCP server");
                return;
            }
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                WebSocketSend(_webSocketInstance, message);
#else
                _webSocket.Send(message);
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending message to MCP server: {e.Message}");
                OnError?.Invoke($"Send error: {e.Message}");
                
                // Connection might be lost, try to reconnect
                if (_isConnected)
                {
                    _isConnected = false;
                    if (_autoReconnect)
                    {
                        TryReconnect();
                    }
                }
            }
        }
        
        /// <summary>
        /// Attempt to reconnect to the server after a connection failure.
        /// </summary>
        private void TryReconnect()
        {
            if (!_autoReconnect || _isConnected || _isConnecting || _reconnectAttempts >= _maxReconnectAttempts)
                return;
            
            _reconnectAttempts++;
            _isConnecting = true;
            
            OnReconnecting?.Invoke();
            Debug.Log($"Attempting to reconnect to MCP server (attempt {_reconnectAttempts}/{_maxReconnectAttempts})...");
            
            // Schedule reconnection after delay
            StartCoroutine(ReconnectAfterDelay());
        }
        
        private IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(_reconnectDelay);
            Connect();
        }
        
#if !UNITY_WEBGL || UNITY_EDITOR
        private void OnWebSocketOpen(object sender, EventArgs e)
        {
            _isConnected = true;
            _isConnecting = false;
            _reconnectAttempts = 0;
            
            Debug.Log("Connected to MCP server");
            OnConnected?.Invoke();
        }
        
        private void OnWebSocketClose(object sender, CloseEventArgs e)
        {
            _isConnected = false;
            _isConnecting = false;
            
            Debug.Log($"Disconnected from MCP server: {e.Reason} (Code: {e.Code})");
            OnDisconnected?.Invoke();
            
            // Try to reconnect if configured to do so
            if (_autoReconnect)
            {
                TryReconnect();
            }
        }
        
        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            string message = e.Data;
            ProcessReceivedMessage(message);
        }
        
        private void OnWebSocketError(object sender, ErrorEventArgs e)
        {
            Debug.LogError($"WebSocket error: {e.Message}");
            OnError?.Invoke($"WebSocket error: {e.Message}");
        }
#endif
        
        /// <summary>
        /// Process messages received from the server.
        /// </summary>
        private void ProcessReceivedMessage(string message)
        {
            try
            {
                // Notify subscribers
                OnMessageReceived?.Invoke(message);
                
                // Simple parsing to extract the session ID from the initial connection response
                // For more complex messages, we'll use MCPResponseHandler class
                if (message.Contains("\"sessionId\""))
                {
                    // Very basic parsing, in the future we should use proper JSON deserialization
                    int start = message.IndexOf("\"sessionId\":") + "\"sessionId\":".Length;
                    if (start > 0)
                    {
                        start = message.IndexOf("\"", start) + 1;
                        int end = message.IndexOf("\"", start);
                        if (start > 0 && end > start)
                        {
                            _sessionId = message.Substring(start, end - start);
                            Debug.Log($"Session ID: {_sessionId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing message from MCP server: {ex.Message}");
                OnError?.Invoke($"Processing error: {ex.Message}");
            }
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
    }
}