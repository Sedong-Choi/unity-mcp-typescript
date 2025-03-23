using UnityEngine;
using UnityEditor;
using MCP.Core;
using MCP.Utils;

namespace MCP.Editor.Utils
{
    [InitializeOnLoad]
    public class MCPInitializer
    {
        static MCPInitializer()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            // 이미 씬에 MCPConnection이 있는지 확인
            MCPConnection existingConnection = Object.FindObjectOfType<MCPConnection>();
            
            if (existingConnection == null)
            {
                // 자동 생성 여부 확인 (선택사항)
                bool autoCreate = EditorPrefs.GetBool("MCP_AutoCreateManager", true);
                
                if (autoCreate)
                {
                    // MCP 관리자 게임오브젝트 생성
                    GameObject mcpManager = new GameObject("MCP Manager");
                    mcpManager.AddComponent<MCPConnection>();
                    mcpManager.AddComponent<MCPRequestManager>();
                    mcpManager.AddComponent<MCPResponseHandler>();
                    
                    // 싱글톤 인스턴스 초기화
                    MCPConnection.Instance.Initialize();
                    MCPRequestManager.Instance.Initialize();
                    MCPResponseHandler.Instance.Initialize();
                    
                    Debug.Log("MCP 관리자가 자동으로 씬에 추가되었습니다.");
                }
            }
            else
            {
                // 기존 인스턴스가 있는 경우에도 초기화
                MCPConnection.Instance.Initialize();
                MCPRequestManager.Instance.Initialize();
                MCPResponseHandler.Instance.Initialize();
            }
        }
    }
}