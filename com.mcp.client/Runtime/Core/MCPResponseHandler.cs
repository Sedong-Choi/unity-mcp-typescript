using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCP.Models;

namespace MCP.Core
{
    /// <summary>
    /// Handles and processes responses from the MCP server.
    /// </summary>
    public class MCPResponseHandler : MonoBehaviour
    {
        private static MCPResponseHandler _instance;
        
        // References
        private MCPConnection _connection;
        
        // Response events
        public delegate void TextResponseHandler(string conversationId, string text, bool isDone);
        public delegate void CodeModificationHandler(string conversationId, List<CodeModification> modifications);
        public delegate void ErrorHandler(string conversationId, string errorMessage);
        public delegate void ConnectionStatusHandler(bool isConnected, string sessionId);
        
        public event TextResponseHandler OnTextResponse;
        public event CodeModificationHandler OnCodeModifications;
        public event ErrorHandler OnError;
        public event ConnectionStatusHandler OnConnectionStatusChanged;
        
        // For building streaming responses
        private Dictionary<string, StringBuilder> _streamResponses = new Dictionary<string, StringBuilder>();
        
        // For tracking message history by conversation
        private Dictionary<string, List<MCPMessage>> _messageHistory = new Dictionary<string, List<MCPMessage>>();
        
        // 유니티 에디터 명령 처리 결과 이벤트
        public event Action<string> OnEditorCommandExecuted;
        
        public static MCPResponseHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    MCPResponseHandler existingHandler = UnityEngine.Object.FindAnyObjectByType<MCPResponseHandler>();
                    
                    if (existingHandler != null)
                    {
                        _instance = existingHandler;
                    }
                    else
                    {
                        // 이 부분은 MCPInitializer에서 관리하도록 변경
                        GameObject go = new GameObject("MCPResponseHandler");
                        _instance = go.AddComponent<MCPResponseHandler>();
                        DontDestroyOnLoad(go);
                        Debug.LogWarning("MCPResponseHandler가 자동 생성되었습니다. MCPInitializer.InitializeMCPManager()를 사용하는 것이 좋습니다.");
                    }
                }
                return _instance;
            }
        }
        
        
        /// <summary>
        /// MCPResponseHandler 인스턴스를 초기화합니다.
        /// </summary>
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
                Debug.LogWarning("중복된 MCPResponseHandler 인스턴스가 감지되어 제거됩니다.");
                Destroy(gameObject);
                return;
            }
            
            // Start 메서드의 로직을 여기로 이동
            if (_connection == null)
            {
                _connection = MCPConnection.Instance;
                
                // 이벤트 구독
                _connection.OnMessageReceived += HandleMessage;
                _connection.OnError += HandleConnectionError;
                _connection.OnConnected += () => OnConnectionStatusChanged?.Invoke(true, _connection.SessionId);
                _connection.OnDisconnected += () => OnConnectionStatusChanged?.Invoke(false, null);
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
            _connection = MCPConnection.Instance;
            
            // 이벤트 구독
            _connection.OnMessageReceived += HandleMessage;
            _connection.OnError += HandleConnectionError;
            _connection.OnConnected += () => OnConnectionStatusChanged?.Invoke(true, _connection.SessionId);
            _connection.OnDisconnected += () => OnConnectionStatusChanged?.Invoke(false, null);

#if UNITY_EDITOR
            // 에디터 명령 실행 결과 이벤트 구독
            if (Application.isEditor)
            {
                MCP.Editor.Utils.MCPEditorController.OnEditorActionCompleted += HandleEditorActionCompleted;
                MCP.Editor.Utils.MCPCommandInterpreter.OnCommandInterpreted += HandleCommandInterpreted;
            }
#endif
        }
        
        /// <summary>
        /// Process a message received from the server.
        /// </summary>
        /// <param name="message">JSON message from server</param>
        private void HandleMessage(string message)
        {
            try
            {
                // Parse JSON response
                JObject responseObj = JObject.Parse(message);
                
                // Extract common fields
                string responseType = responseObj["type"]?.ToString();
                string conversationId = responseObj["conversationId"]?.ToString();
                string sessionId = responseObj["sessionId"]?.ToString();
                
                // Process by response type
                switch (responseType)
                {
                    case "generation":
                        HandleGenerationResponse(responseObj, conversationId);
                        break;
                    
                    case "generation_chunk":
                        HandleGenerationChunk(responseObj, conversationId);
                        break;
                    
                    case "code_modification":
                        HandleCodeModification(responseObj, conversationId);
                        break;
                    
                    case "error":
                        HandleErrorResponse(responseObj, conversationId);
                        break;
                    
                    default:
                        // Handle connection success message (type might be null)
                        if (responseObj["status"]?.ToString() == "success" && responseObj["sessionId"] != null)
                        {
                            Debug.Log($"Session established: {sessionId}");
                            OnConnectionStatusChanged?.Invoke(true, sessionId);
                        }
                        else
                        {
                            Debug.Log($"Received unknown response type: {responseType}");
                            Debug.Log(message);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling message: {e.Message}\n{message}");
            }
        }
        
        /// <summary>
        /// Handle a complete generation response.
        /// </summary>
        private void HandleGenerationResponse(JObject response, string conversationId)
        {
            bool done = response["done"]?.ToObject<bool>() ?? true;
            string content = response["content"]?.ToString() ?? "";
            
            // Add to message history if this is a complete response
            if (done)
            {
                AddMessageToHistory(conversationId, MCPMessage.CreateAssistantMessage(content));
            }
            
            // Trigger event
            OnTextResponse?.Invoke(conversationId, content, done);
            
            // Clear any streaming buffer for this conversation
            if (done && _streamResponses.ContainsKey(conversationId))
            {
                _streamResponses.Remove(conversationId);
            }
        }
        
        /// <summary>
        /// Handle a chunk of a streamed generation response.
        /// </summary>
        private void HandleGenerationChunk(JObject response, string conversationId)
        {
            string chunk = response["chunk"]?.ToString() ?? "";
            bool done = response["done"]?.ToObject<bool>() ?? false;
            
            // Append to the streaming buffer
            if (!_streamResponses.ContainsKey(conversationId))
            {
                _streamResponses[conversationId] = new StringBuilder();
            }
            
            _streamResponses[conversationId].Append(chunk);
            
            // Build current full response
            string currentResponse = _streamResponses[conversationId].ToString();
            
            // Add or update message in history
            if (done)
            {
                AddMessageToHistory(conversationId, MCPMessage.CreateAssistantMessage(currentResponse));
            }
            else
            {
                // Update the in-progress message or add a new one
                UpdateInProgressMessage(conversationId, currentResponse);
            }
            
            // Trigger event with the current accumulated text
            OnTextResponse?.Invoke(
                conversationId,
                currentResponse,
                done
            );
            
            // Clear buffer if this is the final chunk
            if (done)
            {
                _streamResponses.Remove(conversationId);
            }
        }
        
        /// <summary>
        /// Handle a code modification response.
        /// </summary>
        private void HandleCodeModification(JObject response, string conversationId)
        {
            try
            {
                var modifications = new List<CodeModification>();
                
                JArray modsArray = response["modifications"] as JArray;
                if (modsArray != null)
                {
                    foreach (JToken mod in modsArray)
                    {
                        var filePath = mod["filePath"]?.ToString();
                        var operation = mod["operation"]?.ToString();
                        var success = mod["success"]?.ToObject<bool>() ?? false;
                        var error = mod["error"]?.ToString();
                        var section = mod["section"]?.ToString();
                        
                        modifications.Add(new CodeModification
                        {
                            FilePath = filePath,
                            Operation = operation,
                            Success = success,
                            Error = error,
                            TargetSection = section
                        });
                    }
                }
                
                // Add system message to history indicating code modifications
                if (modifications.Count > 0)
                {
                    StringBuilder modMsg = new StringBuilder("Code modifications received:\n");
                    foreach (var mod in modifications)
                    {
                        modMsg.AppendLine($"- {mod.Operation} {mod.FilePath}: {(mod.Success ? "Success" : "Failed" + (mod.Error != null ? " - " + mod.Error : ""))}");
                    }
                    
                    AddMessageToHistory(conversationId, MCPMessage.CreateSystemMessage(modMsg.ToString()));
                }
                
                // Trigger event
                OnCodeModifications?.Invoke(conversationId, modifications);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing code modifications: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle an error response.
        /// </summary>
        private void HandleErrorResponse(JObject response, string conversationId)
        {
            string errorMessage = response["error"]?.ToString() ?? "Unknown error";
            
            // Add error message to history
            if (!string.IsNullOrEmpty(conversationId))
            {
                AddMessageToHistory(conversationId, MCPMessage.CreateSystemMessage($"Error: {errorMessage}"));
            }
            
            Debug.LogError($"Server error: {errorMessage}");
            OnError?.Invoke(conversationId, errorMessage);
        }
        
        /// <summary>
        /// Handle a connection error.
        /// </summary>
        private void HandleConnectionError(string errorMessage)
        {
            Debug.LogError($"Connection error: {errorMessage}");
            OnError?.Invoke(null, errorMessage);
        }
        
        /// <summary>
        /// Add a message to the history for a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="message">Message to add</param>
        public void AddMessageToHistory(string conversationId, MCPMessage message)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;
            
            if (!_messageHistory.ContainsKey(conversationId))
            {
                _messageHistory[conversationId] = new List<MCPMessage>();
            }
            
            // If this is a user message, or a completed assistant message, add it
            if (message.MessageRole != MCPMessage.Role.Assistant || message.IsComplete)
            {
                _messageHistory[conversationId].Add(message);
            }
        }
        
        /// <summary>
        /// Update an in-progress message, or add a new one if none exists.
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="content">Current content</param>
        private void UpdateInProgressMessage(string conversationId, string content)
        {
            if (!_messageHistory.ContainsKey(conversationId))
            {
                _messageHistory[conversationId] = new List<MCPMessage>();
                _messageHistory[conversationId].Add(MCPMessage.CreateAssistantMessage(content, false));
                return;
            }
            
            var history = _messageHistory[conversationId];
            
            // Check if the last message is an incomplete assistant message
            if (history.Count > 0 && 
                history[history.Count - 1].MessageRole == MCPMessage.Role.Assistant && 
                !history[history.Count - 1].IsComplete)
            {
                // Update it
                history[history.Count - 1].Content = content;
            }
            else
            {
                // Add a new incomplete message
                history.Add(MCPMessage.CreateAssistantMessage(content, false));
            }
        }
        
        /// <summary>
        /// Get the message history for a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <returns>List of messages or empty list if not found</returns>
        public List<MCPMessage> GetMessageHistory(string conversationId)
        {
            if (_messageHistory.TryGetValue(conversationId, out var history))
            {
                return new List<MCPMessage>(history);
            }
            return new List<MCPMessage>();
        }
        
        /// <summary>
        /// Clear the message history for a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        public void ClearMessageHistory(string conversationId)
        {
            if (_messageHistory.ContainsKey(conversationId))
            {
                _messageHistory[conversationId].Clear();
            }
        }
        
        private void OnDestroy()
        {
            if (_connection != null)
            {
                _connection.OnMessageReceived -= HandleMessage;
                _connection.OnError -= HandleConnectionError;
            }

#if UNITY_EDITOR
            // 에디터 명령 실행 결과 이벤트 구독 해제
            if (Application.isEditor)
            {
                MCP.Editor.Utils.MCPEditorController.OnEditorActionCompleted -= HandleEditorActionCompleted;
                MCP.Editor.Utils.MCPCommandInterpreter.OnCommandInterpreted -= HandleCommandInterpreted;
            }
#endif
        }

        /// <summary>
        /// AI의 응답 메시지를 처리합니다.
        /// </summary>
        private void ProcessAssistantResponse(string conversationId, MCPMessage message)
        {
            // 메시지를 대화 기록에 추가
            AddMessageToHistory(conversationId, message);
            
            // 텍스트 응답 이벤트 발생
            OnTextResponse?.Invoke(conversationId, message.Content, message.IsComplete);

#if UNITY_EDITOR
            // 에디터 전용: 메시지에서 유니티 명령 실행
            if (Application.isEditor)
            {
                ProcessEditorCommands(message);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// AI 응답 메시지에서 Unity 에디터 명령을 처리합니다.
        /// </summary>
        private void ProcessEditorCommands(MCPMessage message)
        {
            if (message == null || message.MessageRole != MCPMessage.Role.Assistant)
                return;
            
            try
            {
                bool commandExecuted = MCP.Editor.Utils.MCPCommandInterpreter.ProcessMessage(message);
                
                if (commandExecuted)
                {
                    Debug.Log("AI 응답에서 Unity 에디터 명령이 실행되었습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unity 에디터 명령 처리 중 오류 발생: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 에디터 명령 실행 결과를 처리합니다.
        /// </summary>
        private void HandleEditorActionCompleted(string message)
        {
            OnEditorCommandExecuted?.Invoke(message);
        }
        
        /// <summary>
        /// 명령 해석 결과를 처리합니다.
        /// </summary>
        private void HandleCommandInterpreted(string message)
        {
            // 필요한 경우 추가 처리
        }
#endif
    }
}