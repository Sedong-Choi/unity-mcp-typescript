using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Utils;
using MCP.UI;

namespace MCP.Editor.Utils
{
    [InitializeOnLoad]
    public class MCPInitializer
    {
        private static bool _initialized = false;
        private static GameObject _mcpManager = null;
        
        // MCP 매니저에 대한 전역 접근 제공
        public static GameObject MCPManager => _mcpManager;
        
        static MCPInitializer()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            // 이미 초기화되었는지 확인
            if (_initialized)
                return;
                
            _initialized = true;
            
            // 에디터 모드에서 자동 생성이 필요한지 확인
            bool autoCreate = EditorPrefs.GetBool("MCP_AutoCreateManager", false);
            
            if (!autoCreate)
            {
                // 자동 생성이 비활성화되어 있으면 리턴
                return;
            }
            
            // 이미 존재하는 MCP 컴포넌트 확인
            InitializeMCPManager();
        }
        
        /// <summary>
        /// MCPManager를 초기화하고 필요한 컴포넌트를 추가합니다.
        /// MCPClientWindow에서도 호출할 수 있습니다.
        /// </summary>
        public static GameObject InitializeMCPManager()
        {
            if (_mcpManager != null)
                return _mcpManager;
                
            // 이미 씬에 존재하는 컴포넌트 확인
            MCPConnection existingConnection = UnityEngine.Object.FindAnyObjectByType<MCPConnection>();
            MCPRequestManager existingRequestManager = UnityEngine.Object.FindAnyObjectByType<MCPRequestManager>();
            MCPResponseHandler existingResponseHandler = UnityEngine.Object.FindAnyObjectByType<MCPResponseHandler>();
            
            // 이미 생성된 컴포넌트가 있는지 확인
            if (existingConnection != null)
            {
                _mcpManager = existingConnection.gameObject;
                Debug.Log("기존 MCP Connection을 찾았습니다.");
            }
            else if (existingRequestManager != null)
            {
                _mcpManager = existingRequestManager.gameObject;
                Debug.Log("기존 MCP Request Manager를 찾았습니다.");
            }
            else if (existingResponseHandler != null)
            {
                _mcpManager = existingResponseHandler.gameObject;
                Debug.Log("기존 MCP Response Handler를 찾았습니다.");
            }
            
            // 기존 객체가 없는 경우 새로 생성
            if (_mcpManager == null)
            {
                _mcpManager = new GameObject("MCP Manager");
                
                // 필요한 컴포넌트 추가
                _mcpManager.AddComponent<MCPConnection>();
                _mcpManager.AddComponent<MCPRequestManager>();
                _mcpManager.AddComponent<MCPResponseHandler>();
                
                // 씬 전환 시에도 보존
                Object.DontDestroyOnLoad(_mcpManager);
                
                Debug.Log("MCP 관리자가 생성되었습니다.");
            }
            
            // 컴포넌트 참조 확인
            if (_mcpManager.GetComponent<MCPConnection>() == null)
                _mcpManager.AddComponent<MCPConnection>();
                
            if (_mcpManager.GetComponent<MCPRequestManager>() == null)
                _mcpManager.AddComponent<MCPRequestManager>();
                
            if (_mcpManager.GetComponent<MCPResponseHandler>() == null)
                _mcpManager.AddComponent<MCPResponseHandler>();
            
            // 싱글톤 인스턴스 강제 초기화 (중요)
            MCPConnection.Instance.Initialize();
            MCPRequestManager.Instance.Initialize();
            MCPResponseHandler.Instance.Initialize();
            
            Debug.Log("MCP 서비스가 초기화되었습니다.");
            
            return _mcpManager;
        }
    }
}