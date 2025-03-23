using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Utils;

namespace MCP.Editor.Utils
{
    [InitializeOnLoad]
    public class MCPInitializer
    {
        private static bool _initialized = false;
        
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
            
            // 이미 씬에 존재하는 컴포넌트 확인
            MCPConnection existingConnection = Object.FindObjectOfType<MCPConnection>();
            MCPRequestManager existingRequestManager = Object.FindObjectOfType<MCPRequestManager>();
            MCPResponseHandler existingResponseHandler = Object.FindObjectOfType<MCPResponseHandler>();
            
            GameObject mcpManager = null;
            
            // 이미 생성된 컴포넌트가 있는지 확인
            if (existingConnection != null)
            {
                mcpManager = existingConnection.gameObject;
                Debug.Log("기존 MCP Connection을 찾았습니다.");
            }
            else if (existingRequestManager != null)
            {
                mcpManager = existingRequestManager.gameObject;
                Debug.Log("기존 MCP Request Manager를 찾았습니다.");
            }
            else if (existingResponseHandler != null)
            {
                mcpManager = existingResponseHandler.gameObject;
                Debug.Log("기존 MCP Response Handler를 찾았습니다.");
            }
            
            // 기존 객체가 없는 경우 새로 생성
            if (mcpManager == null)
            {
                bool autoCreate = EditorPrefs.GetBool("MCP_AutoCreateManager", true);
                
                if (autoCreate)
                {
                    mcpManager = new GameObject("MCP Manager");
                    
                    // 필요한 컴포넌트 추가
                    if (existingConnection == null) mcpManager.AddComponent<MCPConnection>();
                    if (existingRequestManager == null) mcpManager.AddComponent<MCPRequestManager>();
                    if (existingResponseHandler == null) mcpManager.AddComponent<MCPResponseHandler>();
                    
                    // 씬 전환 시에도 보존
                    Object.DontDestroyOnLoad(mcpManager);
                    
                    Debug.Log("MCP 관리자가 생성되었습니다.");
                }
            }
            
            // 각 인스턴스 초기화 (싱글톤 패턴 강제)
            if (mcpManager != null)
            {
                MCPConnection.Instance.Initialize();
                MCPRequestManager.Instance.Initialize();
                MCPResponseHandler.Instance.Initialize();
            }
        }
    }
}