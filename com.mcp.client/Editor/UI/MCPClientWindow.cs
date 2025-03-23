using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Models;
using MCP.Utils;
using MCP.Editor.Utils;

namespace MCP.UI
{
    /// <summary>
    /// 통합된 MCP 클라이언트 창
    /// </summary>
    public class MCPClientWindow : EditorWindow
    {
        // 코어 서비스
        private MCPConnection _connection;
        private MCPRequestManager _requestManager;
        private MCPResponseHandler _responseHandler;
        private CodeFileManager _codeFileManager;
        
        // UI 상태
        private string _serverUrl = "ws://localhost:8765";
        private string _apiKey = "";
        private bool _autoConnect = true;
        private string _currentPrompt = "";
        private string _activeConversationId = "";
        private Vector2 _conversationListScrollPos;
        private Vector2 _messageScrollPos;
        private Vector2 _modificationScrollPos;
        private bool _isConnected = false;
        private string _connectionStatusText = "연결 안됨";
        private int _selectedTab = 0;
        private string[] _tabNames = new string[] { "채팅", "코드", "설정" };
        private bool _isGenerating = false;
        private string _outputPath = "Assets/Scripts/Generated";
        
        // 대화 및 메시지 기록
        private List<string> _conversations = new List<string>();
        private List<MCPMessage> _messages = new List<MCPMessage>();
        private List<CodeModification> _codeModifications = new List<CodeModification>();
        
        // 환경설정 키
        private const string PREF_SERVER_URL = "MCP_SERVER_URL";
        private const string PREF_API_KEY = "MCP_API_KEY";
        private const string PREF_AUTO_CONNECT = "MCP_AUTO_CONNECT";
        private const string PREF_OUTPUT_PATH = "MCP_OUTPUT_PATH";
        
        [MenuItem("Window/AI/MCP Client")]
        public static void ShowWindow()
        {
            MCPClientWindow window = GetWindow<MCPClientWindow>("MCP Client");
            window.minSize = new Vector2(700, 500);
            window.Show();
        }
        
        private void OnEnable()
        {
            // 저장된 설정 불러오기
            _serverUrl = EditorPrefs.GetString(PREF_SERVER_URL, "ws://localhost:8765");
            _apiKey = EditorPrefs.GetString(PREF_API_KEY, "");
            _autoConnect = EditorPrefs.GetBool(PREF_AUTO_CONNECT, true);
            _outputPath = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Assets/Scripts/Generated");
            
            // MCPInitializer를 사용하여 MCP 서비스 초기화
            GameObject mcpManager = MCPInitializer.InitializeMCPManager();
            
            if (mcpManager != null)
            {
                // 서비스 참조 가져오기
                _connection = MCPConnection.Instance;
                _requestManager = MCPRequestManager.Instance;
                _responseHandler = MCPResponseHandler.Instance;
                _codeFileManager = CodeFileManager.Instance;
                
                // 파일 관리자 구성
                _codeFileManager.Configure(_outputPath);
                
                // 이벤트 구독
                _responseHandler.OnTextResponse += HandleTextResponse;
                _responseHandler.OnCodeModifications += HandleCodeModifications;
                _responseHandler.OnError += HandleError;
                _responseHandler.OnConnectionStatusChanged += HandleConnectionStatusChanged;
                
                // 현재 연결 상태 확인
                _isConnected = _connection.IsConnected;
                _connectionStatusText = _isConnected ? "연결됨" : "연결 안됨";
                
                // 이미 연결되어 있으면 대화 목록 새로고침
                if (_isConnected)
                {
                    RefreshConversationList();
                }
                
                // 자동 연결 설정이 활성화되어 있으면 연결 시도
                if (_autoConnect && !_isConnected)
                {
                    Connect();
                }
            }
            else
            {
                Debug.LogError("MCP 서비스를 초기화할 수 없습니다.");
            }
        }
        
        private void OnDisable()
        {
            // 환경설정 저장
            EditorPrefs.SetString(PREF_SERVER_URL, _serverUrl);
            EditorPrefs.SetString(PREF_API_KEY, _apiKey);
            EditorPrefs.SetBool(PREF_AUTO_CONNECT, _autoConnect);
            EditorPrefs.SetString(PREF_OUTPUT_PATH, _outputPath);
            
            // 이벤트 구독 해제
            if (_responseHandler != null)
            {
                _responseHandler.OnTextResponse -= HandleTextResponse;
                _responseHandler.OnCodeModifications -= HandleCodeModifications;
                _responseHandler.OnError -= HandleError;
                _responseHandler.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            }
        }
        
        private void OnGUI()
        {
            // 연결 상태 및 버튼
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"상태: {_connectionStatusText}", GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button(_isConnected ? "연결 해제" : "연결", EditorStyles.toolbarButton, GUILayout.Width(100)))
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
            
            // 메인 콘텐츠 탭
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            
            switch (_selectedTab)
            {
                case 0: // 채팅
                    DrawChatTab();
                    break;
                case 1: // 코드
                    DrawCodeTab();
                    break;
                case 2: // 설정
                    DrawSettingsTab();
                    break;
            }
        }
        
        private void DrawChatTab()
        {
            EditorGUILayout.BeginHorizontal();
            
            // 왼쪽 패널 - 대화 목록
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("대화", EditorStyles.boldLabel);
            
            if (GUILayout.Button("새 대화"))
            {
                _activeConversationId = _requestManager.CreateConversation();
                RefreshConversationList();
                _messages.Clear();
            }
            
            _conversationListScrollPos = EditorGUILayout.BeginScrollView(_conversationListScrollPos, GUILayout.ExpandHeight(true));
            
            foreach (string convId in _conversations)
            {
                bool isActive = convId == _activeConversationId;
                string description = _requestManager.GetConversationDescription(convId) ?? "제목 없음";
                
                // 긴 설명 줄이기
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
            
            // 오른쪽 패널 - 메시지 기록 및 입력
            EditorGUILayout.BeginVertical();
            
            // 메시지 기록
            EditorGUILayout.LabelField("메시지", EditorStyles.boldLabel);
            _messageScrollPos = EditorGUILayout.BeginScrollView(_messageScrollPos, GUILayout.ExpandHeight(true));
            
            foreach (MCPMessage message in _messages)
            {
                EditorGUILayout.BeginHorizontal();
                
                // 역할 라벨
                GUIStyle roleStyle = new GUIStyle(EditorStyles.boldLabel);
                switch (message.MessageRole)
                {
                    case MCPMessage.Role.User:
                        roleStyle.normal.textColor = new Color(0.2f, 0.6f, 1.0f);
                        EditorGUILayout.LabelField("사용자:", roleStyle, GUILayout.Width(60));
                        break;
                    case MCPMessage.Role.Assistant:
                        roleStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("AI:", roleStyle, GUILayout.Width(60));
                        break;
                    case MCPMessage.Role.System:
                        roleStyle.normal.textColor = new Color(0.8f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("시스템:", roleStyle, GUILayout.Width(60));
                        break;
                }
                
                // 메시지 내용
                EditorGUILayout.LabelField(message.Content, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(10);
            }
            
            EditorGUILayout.EndScrollView();
            
            // 입력 영역
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUIStyle promptStyle = new GUIStyle(EditorStyles.textArea);
            promptStyle.wordWrap = true;
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, promptStyle, GUILayout.Height(60));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(!_isConnected || string.IsNullOrEmpty(_currentPrompt) || _isGenerating);
            
            if (GUILayout.Button("메시지 보내기"))
            {
                SendChatMessage();
            }
            
            if (GUILayout.Button("코드 생성"))
            {
                SendGenerateRequest();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSettingsTab()
        {
            EditorGUILayout.Space(10);
            
            // 연결 설정
            EditorGUILayout.LabelField("연결 설정", EditorStyles.boldLabel);
            
            _serverUrl = EditorGUILayout.TextField("서버 URL", _serverUrl);
            
            EditorGUILayout.BeginHorizontal();
            _apiKey = EditorGUILayout.PasswordField("API 키", _apiKey);
            if (GUILayout.Button("지우기", GUILayout.Width(60)))
            {
                _apiKey = "";
            }
            EditorGUILayout.EndHorizontal();
            
            _autoConnect = EditorGUILayout.Toggle("자동 연결", _autoConnect);
            
            EditorGUILayout.Space(20);
            
            // 코드 생성 설정
            EditorGUILayout.LabelField("코드 생성 설정", EditorStyles.boldLabel);
            
            _outputPath = EditorGUILayout.TextField("출력 경로", _outputPath);
            
            if (GUILayout.Button("저장", GUILayout.Width(100)))
            {
                EditorPrefs.SetString(PREF_SERVER_URL, _serverUrl);
                EditorPrefs.SetString(PREF_API_KEY, _apiKey);
                EditorPrefs.SetBool(PREF_AUTO_CONNECT, _autoConnect);
                EditorPrefs.SetString(PREF_OUTPUT_PATH, _outputPath);
                
                // 파일 관리자 구성 업데이트
                _codeFileManager.Configure(_outputPath);
                
                EditorUtility.DisplayDialog("저장됨", "설정이 저장되었습니다.", "확인");
            }
        }
        
        private void DrawCodeTab()
        {
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.LabelField("코드 수정사항", EditorStyles.boldLabel);
            _modificationScrollPos = EditorGUILayout.BeginScrollView(_modificationScrollPos, GUILayout.ExpandHeight(true));
            
            if (_codeModifications.Count == 0)
            {
                EditorGUILayout.HelpBox("코드 수정 기록이 없습니다.", MessageType.Info);
            }
            else
            {
                foreach (CodeModification mod in _codeModifications)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    
                    // 파일 경로 및 작업
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(mod.FilePath, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(mod.Operation, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                    
                    // 상태
                    string statusText = mod.Success ? "성공" : "실패";
                    GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
                    statusStyle.normal.textColor = mod.Success ? Color.green : Color.red;
                    EditorGUILayout.LabelField("상태: " + statusText, statusStyle);
                    
                    // 오류 메시지 표시
                    if (!string.IsNullOrEmpty(mod.Error))
                    {
                        EditorGUILayout.HelpBox(mod.Error, MessageType.Error);
                    }
                    
                    // 파일 열기 버튼
                    if (GUILayout.Button("파일 열기", GUILayout.Width(100)))
                    {
                        if (System.IO.File.Exists(mod.FilePath))
                        {
                            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(mod.FilePath, 1);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("오류", "파일을 찾을 수 없습니다: " + mod.FilePath, "확인");
                        }
                    }
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void Connect()
        {
            _connection.Connect(_serverUrl, _apiKey);
        }
        
        private void Disconnect()
        {
            _connection.Disconnect();
        }
        
        private void SendChatMessage()
        {
            if (!_isConnected || string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // 새 대화 시작 또는 기존 대화 계속
            if (string.IsNullOrEmpty(_activeConversationId))
            {
                _activeConversationId = _requestManager.CreateConversation();
                RefreshConversationList();
            }
            
            // 사용자 메시지 추가
            _responseHandler.AddMessageToHistory(_activeConversationId, MCPMessage.CreateUserMessage(_currentPrompt));
            
            // 메시지 전송
            _isGenerating = true;
            _requestManager.SendGenerateRequest(_currentPrompt, _activeConversationId, true);
            
            // 메시지 히스토리 새로고침 및 입력 비우기
            RefreshMessageHistory();
            _currentPrompt = "";
        }
        
        private void SendGenerateRequest()
        {
            if (!_isConnected || string.IsNullOrEmpty(_currentPrompt))
                return;
            
            // 새 대화 시작 또는 기존 대화 계속
            if (string.IsNullOrEmpty(_activeConversationId))
            {
                _activeConversationId = _requestManager.CreateConversation();
                RefreshConversationList();
            }
            
            // 사용자 메시지 추가
            _responseHandler.AddMessageToHistory(_activeConversationId, MCPMessage.CreateUserMessage(_currentPrompt));
            
            // 메시지 전송 (코드 생성 모드)
            _isGenerating = true;
            _requestManager.SendGenerateRequest(_currentPrompt, _activeConversationId, true);
            
            // 메시지 히스토리 새로고침 및 입력 비우기
            RefreshMessageHistory();
            _currentPrompt = "";
        }
        
        private void RefreshConversationList()
        {
            _conversations = _requestManager.GetActiveConversations();
        }
        
        private void RefreshMessageHistory()
        {
            if (!string.IsNullOrEmpty(_activeConversationId))
            {
                _messages = _responseHandler.GetMessageHistory(_activeConversationId);
                
                // 스크롤을 가장 아래로 이동
                EditorApplication.delayCall += () => _messageScrollPos = new Vector2(0, float.MaxValue);
            }
        }
        
        private void HandleTextResponse(string conversationId, string text, bool isDone)
        {
            if (conversationId == _activeConversationId)
            {
                // 입력 생성이 완료되면 상태 업데이트
                if (isDone)
                {
                    _isGenerating = false;
                }
                
                // 메시지 히스토리 새로고침
                RefreshMessageHistory();
                
                // 창 다시 그리기
                Repaint();
            }
        }
        
        private void HandleCodeModifications(string conversationId, List<CodeModification> modifications)
        {
            if (modifications == null || modifications.Count == 0)
                return;
            
            // 코드 수정 목록 업데이트
            _codeModifications.InsertRange(0, modifications);
            
            // 최근 20개 수정 제한
            if (_codeModifications.Count > 20)
            {
                _codeModifications.RemoveRange(20, _codeModifications.Count - 20);
            }
            
            // 창 다시 그리기
            Repaint();
        }
        
        private void HandleError(string conversationId, string errorMessage)
        {
            _isGenerating = false;
            EditorUtility.DisplayDialog("MCP 오류", errorMessage, "확인");
            Repaint();
        }
        
        private void HandleConnectionStatusChanged(bool isConnected, string sessionId)
        {
            _isConnected = isConnected;
            _connectionStatusText = isConnected ? "연결됨" : "연결 안됨";
            
            if (isConnected)
            {
                // 연결되면 대화 목록 새로고침
                RefreshConversationList();
                
                // 필요한 경우 새 대화 생성
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
            // OnGUI 외부에서 실행할 때 UI 업데이트
            if (_isGenerating)
            {
                Repaint();
            }
        }
    }
} 