using UnityEngine;
using UnityEditor;
using MCP.Core;

namespace MCP.Editor.Inspectors
{
    [CustomEditor(typeof(MCPConnection))]
    public class MCPConnectionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            MCPConnection connection = (MCPConnection)target;
            
            EditorGUILayout.LabelField("MCP 연결 상태", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 연결 상태 표시
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("연결됨", connection.IsConnected);
            EditorGUILayout.TextField("세션 ID", connection.SessionId ?? "없음");
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            
            // 서버 설정 필드
            SerializedProperty serverUrlProperty = serializedObject.FindProperty("_serverUrl");
            EditorGUILayout.PropertyField(serverUrlProperty, new GUIContent("서버 URL"));
            
            SerializedProperty apiKeyProperty = serializedObject.FindProperty("_apiKey");
            EditorGUILayout.PropertyField(apiKeyProperty, new GUIContent("API 키"));
            
            SerializedProperty autoReconnectProperty = serializedObject.FindProperty("_autoReconnect");
            EditorGUILayout.PropertyField(autoReconnectProperty, new GUIContent("자동 재연결"));
            
            // 변경사항 적용
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Space();
            
            // 연결 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("연결"))
            {
                connection.Connect();
            }
            
            if (GUILayout.Button("연결 해제"))
            {
                connection.Disconnect();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}