using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using MCP.Core;

namespace MCP.Editor.Utils
{
    /// <summary>
    /// AI 에이전트가 Unity 에디터를 직접 조작할 수 있도록 하는 컨트롤러 클래스
    /// </summary>
    public static class MCPEditorController
    {
        // 에디터 작업 결과를 알리는 이벤트
        public static event Action<string> OnEditorActionCompleted;
        
        #region 게임오브젝트 조작
        
        /// <summary>
        /// 새 게임오브젝트를 생성합니다.
        /// </summary>
        public static GameObject CreateGameObject(string name, Vector3 position = default)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = position;
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            Selection.activeGameObject = obj;
            
            NotifyActionCompleted($"'{name}' 게임오브젝트를 생성했습니다.");
            return obj;
        }
        
        /// <summary>
        /// 프리팹을 인스턴스화합니다.
        /// </summary>
        public static GameObject InstantiatePrefab(string prefabPath, Vector3 position = default)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"'{prefabPath}' 경로에 프리팹이 존재하지 않습니다.");
                return null;
            }
            
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.position = position;
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");
                Selection.activeGameObject = instance;
                
                NotifyActionCompleted($"'{prefabPath}' 프리팹을 인스턴스화했습니다.");
            }
            
            return instance;
        }
        
        /// <summary>
        /// 게임오브젝트에 컴포넌트를 추가합니다.
        /// </summary>
        public static Component AddComponent(GameObject targetObject, string componentTypeName)
        {
            if (targetObject == null)
            {
                Debug.LogError("컴포넌트를 추가할 대상 게임오브젝트가 없습니다.");
                return null;
            }
            
            // 컴포넌트 타입 찾기
            Type componentType = FindType(componentTypeName);
            if (componentType == null)
            {
                Debug.LogError($"'{componentTypeName}' 타입을 찾을 수 없습니다.");
                return null;
            }
            
            // 컴포넌트 추가
            Component component = Undo.AddComponent(targetObject, componentType);
            NotifyActionCompleted($"'{targetObject.name}'에 '{componentTypeName}' 컴포넌트를 추가했습니다.");
            return component;
        }
        
        /// <summary>
        /// 현재 선택된 게임오브젝트에 컴포넌트를 추가합니다.
        /// </summary>
        public static Component AddComponentToSelection(string componentTypeName)
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogError("선택된 게임오브젝트가 없습니다.");
                return null;
            }
            
            return AddComponent(selectedObject, componentTypeName);
        }
        
        /// <summary>
        /// 게임오브젝트 삭제
        /// </summary>
        public static void DestroyGameObject(GameObject targetObject)
        {
            if (targetObject == null) return;
            
            string objName = targetObject.name;
            Undo.DestroyObjectImmediate(targetObject);
            NotifyActionCompleted($"'{objName}' 게임오브젝트를 삭제했습니다.");
        }
        
        #endregion
        
        #region 컴포넌트 속성 조작
        
        /// <summary>
        /// 컴포넌트의 속성값을 변경합니다.
        /// </summary>
        public static bool SetComponentProperty(Component component, string propertyName, object value)
        {
            if (component == null) return false;
            
            try
            {
                // 속성 찾기
                PropertyInfo property = component.GetType().GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (property != null && property.CanWrite)
                {
                    // 값 타입 변환
                    object convertedValue = ConvertValue(value, property.PropertyType);
                    
                    // Undo 등록
                    Undo.RecordObject(component, $"Change {propertyName}");
                    
                    // 속성 설정
                    property.SetValue(component, convertedValue);
                    
                    NotifyActionCompleted($"'{component.gameObject.name}'의 '{component.GetType().Name}.{propertyName}'을(를) '{value}'로 변경했습니다.");
                    EditorUtility.SetDirty(component);
                    return true;
                }
                
                // 필드 찾기
                FieldInfo field = component.GetType().GetField(propertyName, 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (field != null)
                {
                    // 값 타입 변환
                    object convertedValue = ConvertValue(value, field.FieldType);
                    
                    // Undo 등록
                    Undo.RecordObject(component, $"Change {propertyName}");
                    
                    // 필드 설정
                    field.SetValue(component, convertedValue);
                    
                    NotifyActionCompleted($"'{component.gameObject.name}'의 '{component.GetType().Name}.{propertyName}'을(를) '{value}'로 변경했습니다.");
                    EditorUtility.SetDirty(component);
                    return true;
                }
                
                Debug.LogError($"'{component.GetType().Name}'에서 '{propertyName}' 속성이나 필드를 찾을 수 없습니다.");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"컴포넌트 속성 변경 중 오류 발생: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 현재 선택된 게임오브젝트의 컴포넌트 속성값을 변경합니다.
        /// </summary>
        public static bool SetSelectedComponentProperty(string componentTypeName, string propertyName, object value)
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogError("선택된 게임오브젝트가 없습니다.");
                return false;
            }
            
            Type componentType = FindType(componentTypeName);
            if (componentType == null)
            {
                Debug.LogError($"'{componentTypeName}' 타입을 찾을 수 없습니다.");
                return false;
            }
            
            Component component = selectedObject.GetComponent(componentType);
            if (component == null)
            {
                Debug.LogError($"선택된 게임오브젝트에 '{componentTypeName}' 컴포넌트가 없습니다.");
                return false;
            }
            
            return SetComponentProperty(component, propertyName, value);
        }
        
        #endregion
        
        #region 씬 관리
        
        /// <summary>
        /// 새 씬을 생성합니다.
        /// </summary>
        public static void CreateNewScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
                NotifyActionCompleted("새 씬을 생성했습니다.");
            }
        }
        
        /// <summary>
        /// 씬을 저장합니다.
        /// </summary>
        public static void SaveScene(string path = null)
        {
            UnityEngine.SceneManagement.Scene currentScene = EditorSceneManager.GetActiveScene();
            
            if (string.IsNullOrEmpty(path))
            {
                if (string.IsNullOrEmpty(currentScene.path))
                {
                    // 경로가 없는 경우 다이얼로그 표시
                    string savePath = EditorUtility.SaveFilePanel("씬 저장", "Assets", "New Scene", "unity");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        string relativePath = savePath.Substring(Application.dataPath.Length - "Assets".Length);
                        EditorSceneManager.SaveScene(currentScene, relativePath);
                        NotifyActionCompleted($"씬을 '{relativePath}'에 저장했습니다.");
                    }
                }
                else
                {
                    // 기존 경로에 저장
                    EditorSceneManager.SaveScene(currentScene);
                    NotifyActionCompleted($"씬을 '{currentScene.path}'에 저장했습니다.");
                }
            }
            else
            {
                // 지정된 경로에 저장
                EditorSceneManager.SaveScene(currentScene, path);
                NotifyActionCompleted($"씬을 '{path}'에 저장했습니다.");
            }
        }
        
        /// <summary>
        /// 씬을 열기
        /// </summary>
        public static void OpenScene(string scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
                NotifyActionCompleted($"'{scenePath}' 씬을 열었습니다.");
            }
        }
        
        #endregion
        
        #region 유틸리티
        
        /// <summary>
        /// 타입 이름으로 타입을 찾습니다.
        /// </summary>
        private static Type FindType(string typeName)
        {
            // 정확한 타입 이름이 주어진 경우
            Type type = Type.GetType(typeName);
            if (type != null) return type;
            
            // UnityEngine 네임스페이스에서 검색
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;
            
            // 모든 어셈블리에서 검색
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetTypes().FirstOrDefault(t => 
                    t.Name == typeName || t.FullName == typeName);
                
                if (type != null) return type;
            }
            
            return null;
        }
        
        /// <summary>
        /// 값을 대상 타입으로 변환합니다.
        /// </summary>
        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            
            // 값이 이미 대상 타입인 경우
            if (targetType.IsInstanceOfType(value)) return value;
            
            // string을 다른 타입으로 변환
            if (value is string stringValue)
            {
                // Vector3 변환
                if (targetType == typeof(Vector3))
                {
                    string[] parts = stringValue.Replace("(", "").Replace(")", "").Split(',');
                    if (parts.Length >= 3)
                    {
                        float x = float.Parse(parts[0].Trim());
                        float y = float.Parse(parts[1].Trim());
                        float z = float.Parse(parts[2].Trim());
                        return new Vector3(x, y, z);
                    }
                }
                
                // Color 변환
                if (targetType == typeof(Color))
                {
                    if (ColorUtility.TryParseHtmlString(stringValue, out Color color))
                    {
                        return color;
                    }
                }
                
                // Enum 변환
                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, stringValue);
                }
                
                // 기본 타입 변환
                return Convert.ChangeType(stringValue, targetType);
            }
            
            // 기타 타입 변환
            return Convert.ChangeType(value, targetType);
        }
        
        /// <summary>
        /// 액션 완료 이벤트 발생
        /// </summary>
        private static void NotifyActionCompleted(string message)
        {
            Debug.Log(message);
            OnEditorActionCompleted?.Invoke(message);
        }
        
        #endregion
    }
} 