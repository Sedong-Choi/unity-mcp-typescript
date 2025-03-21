using UnityEngine;
using UnityEditor;
using MCP.Core;

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
                bool autoCreate = EditorPrefs.GetBool("MCP_AutoCreateManager", false);
                
                if (autoCreate)
                {
                    // MCP 관리자 게임오브젝트 생성
                    GameObject mcpManager = new GameObject("MCP Manager");
                    mcpManager.AddComponent<MCPConnection>();
                    mcpManager.AddComponent<MCPRequestManager>();
                    mcpManager.AddComponent<MCPResponseHandler>();
                    
                    Debug.Log("MCP 관리자가 자동으로 씬에 추가되었습니다.");
                }
            }
        }
    }
}