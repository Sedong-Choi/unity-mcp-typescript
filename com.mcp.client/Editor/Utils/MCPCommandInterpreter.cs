using System;
using System.Text.RegularExpressions;
using UnityEngine;
using MCP.Core;
using MCP.Models;

namespace MCP.Editor.Utils
{
    /// <summary>
    /// AI 응답을 파싱하여 Unity 에디터 명령으로 해석하는 인터프리터 클래스
    /// </summary>
    public static class MCPCommandInterpreter
    {
        // AI 명령을 해석한 결과를 알리는 이벤트
        public static event Action<string> OnCommandInterpreted;
        
        // 명령어 식별을 위한 접두사
        private const string COMMAND_PREFIX = "/unity";
        
        // 명령어 패턴
        private static readonly Regex CommandPattern = new Regex(
            @"/unity\s+(\w+)(?:\s+(.+))?", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        // 매개변수 패턴
        private static readonly Regex ParamPattern = new Regex(
            @"--(\w+)(?:=(.+?))?(?:\s+|$)", 
            RegexOptions.Compiled
        );
        
        /// <summary>
        /// 메시지를 분석하여 Unity 명령이 있는지 확인하고 실행합니다.
        /// </summary>
        public static bool ProcessMessage(MCPMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Content))
                return false;
                
            // AI 메시지만 처리
            if (message.MessageRole != MCPMessage.Role.Assistant)
                return false;
                
            string content = message.Content;
            
            // 여러 줄로 된 메시지를 처리
            string[] lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            bool commandExecuted = false;
            
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith(COMMAND_PREFIX))
                {
                    // 명령어 형식 확인
                    Match commandMatch = CommandPattern.Match(line);
                    if (commandMatch.Success)
                    {
                        string cmdName = commandMatch.Groups[1].Value.ToLower();
                        string argsText = commandMatch.Groups[2].Success ? commandMatch.Groups[2].Value : string.Empty;
                        
                        // 명령어 실행
                        if (ExecuteCommand(cmdName, argsText))
                        {
                            commandExecuted = true;
                        }
                    }
                }
            }
            
            return commandExecuted;
        }
        
        /// <summary>
        /// 명령어를 실행합니다.
        /// </summary>
        private static bool ExecuteCommand(string command, string args)
        {
            try
            {
                NotifyCommandInterpreted($"명령 감지됨: {command} {args}");
                
                switch (command.ToLower())
                {
                    case "create":
                        return ExecuteCreateCommand(args);
                        
                    case "addcomponent":
                        return ExecuteAddComponentCommand(args);
                        
                    case "setproperty":
                        return ExecuteSetPropertyCommand(args);
                        
                    case "instantiate":
                        return ExecuteInstantiateCommand(args);
                        
                    case "destroy":
                        return ExecuteDestroyCommand(args);
                        
                    case "newscene":
                        MCPEditorController.CreateNewScene();
                        return true;
                        
                    case "savescene":
                        string path = ExtractParam(args, "path");
                        MCPEditorController.SaveScene(path);
                        return true;
                        
                    case "openscene":
                        string scenePath = ExtractParam(args, "path");
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            MCPEditorController.OpenScene(scenePath);
                            return true;
                        }
                        NotifyCommandInterpreted("씬 경로가 지정되지 않았습니다.");
                        return false;
                        
                    default:
                        NotifyCommandInterpreted($"알 수 없는 명령: {command}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"명령 실행 중 오류 발생: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 게임오브젝트 생성 명령을 실행합니다.
        /// 사용법: /unity create --name=ObjectName [--position=x,y,z]
        /// </summary>
        private static bool ExecuteCreateCommand(string args)
        {
            string name = ExtractParam(args, "name");
            string posStr = ExtractParam(args, "position");
            
            if (string.IsNullOrEmpty(name))
            {
                name = "New GameObject";
            }
            
            Vector3 position = default;
            if (!string.IsNullOrEmpty(posStr))
            {
                string[] coords = posStr.Split(',');
                if (coords.Length >= 3)
                {
                    float x = float.Parse(coords[0].Trim());
                    float y = float.Parse(coords[1].Trim());
                    float z = float.Parse(coords[2].Trim());
                    position = new Vector3(x, y, z);
                }
            }
            
            GameObject obj = MCPEditorController.CreateGameObject(name, position);
            return obj != null;
        }
        
        /// <summary>
        /// 컴포넌트 추가 명령을 실행합니다.
        /// 사용법: /unity addcomponent --type=ComponentType [--target=ObjectName]
        /// </summary>
        private static bool ExecuteAddComponentCommand(string args)
        {
            string type = ExtractParam(args, "type");
            string target = ExtractParam(args, "target");
            
            if (string.IsNullOrEmpty(type))
            {
                NotifyCommandInterpreted("컴포넌트 타입이 지정되지 않았습니다.");
                return false;
            }
            
            // 특정 대상이 지정된 경우
            if (!string.IsNullOrEmpty(target))
            {
                GameObject targetObj = GameObject.Find(target);
                if (targetObj == null)
                {
                    NotifyCommandInterpreted($"'{target}' 게임오브젝트를 찾을 수 없습니다.");
                    return false;
                }
                
                Component component = MCPEditorController.AddComponent(targetObj, type);
                return component != null;
            }
            else
            {
                // 현재 선택된 게임오브젝트에 추가
                Component component = MCPEditorController.AddComponentToSelection(type);
                return component != null;
            }
        }
        
        /// <summary>
        /// 컴포넌트 속성 설정 명령을 실행합니다.
        /// 사용법: /unity setproperty --component=ComponentType --property=PropertyName --value=Value [--target=ObjectName]
        /// </summary>
        private static bool ExecuteSetPropertyCommand(string args)
        {
            string component = ExtractParam(args, "component");
            string property = ExtractParam(args, "property");
            string value = ExtractParam(args, "value");
            string target = ExtractParam(args, "target");
            
            if (string.IsNullOrEmpty(component) || string.IsNullOrEmpty(property) || string.IsNullOrEmpty(value))
            {
                NotifyCommandInterpreted("컴포넌트, 속성, 값이 모두 지정되어야 합니다.");
                return false;
            }
            
            // 특정 대상이 지정된 경우
            if (!string.IsNullOrEmpty(target))
            {
                GameObject targetObj = GameObject.Find(target);
                if (targetObj == null)
                {
                    NotifyCommandInterpreted($"'{target}' 게임오브젝트를 찾을 수 없습니다.");
                    return false;
                }
                
                Component comp = targetObj.GetComponent(component);
                if (comp == null)
                {
                    NotifyCommandInterpreted($"'{target}'에 '{component}' 컴포넌트가 없습니다.");
                    return false;
                }
                
                return MCPEditorController.SetComponentProperty(comp, property, value);
            }
            else
            {
                // 현재 선택된 게임오브젝트에 설정
                return MCPEditorController.SetSelectedComponentProperty(component, property, value);
            }
        }
        
        /// <summary>
        /// 프리팹 인스턴스화 명령을 실행합니다.
        /// 사용법: /unity instantiate --prefab=PrefabPath [--position=x,y,z]
        /// </summary>
        private static bool ExecuteInstantiateCommand(string args)
        {
            string prefabPath = ExtractParam(args, "prefab");
            string posStr = ExtractParam(args, "position");
            
            if (string.IsNullOrEmpty(prefabPath))
            {
                NotifyCommandInterpreted("프리팹 경로가 지정되지 않았습니다.");
                return false;
            }
            
            Vector3 position = default;
            if (!string.IsNullOrEmpty(posStr))
            {
                string[] coords = posStr.Split(',');
                if (coords.Length >= 3)
                {
                    float x = float.Parse(coords[0].Trim());
                    float y = float.Parse(coords[1].Trim());
                    float z = float.Parse(coords[2].Trim());
                    position = new Vector3(x, y, z);
                }
            }
            
            GameObject obj = MCPEditorController.InstantiatePrefab(prefabPath, position);
            return obj != null;
        }
        
        /// <summary>
        /// 게임오브젝트 삭제 명령을 실행합니다.
        /// 사용법: /unity destroy --target=ObjectName
        /// </summary>
        private static bool ExecuteDestroyCommand(string args)
        {
            string target = ExtractParam(args, "target");
            
            if (string.IsNullOrEmpty(target))
            {
                NotifyCommandInterpreted("삭제할 대상이 지정되지 않았습니다.");
                return false;
            }
            
            GameObject targetObj = GameObject.Find(target);
            if (targetObj == null)
            {
                NotifyCommandInterpreted($"'{target}' 게임오브젝트를 찾을 수 없습니다.");
                return false;
            }
            
            MCPEditorController.DestroyGameObject(targetObj);
            return true;
        }
        
        /// <summary>
        /// 매개변수 이름으로 값을 추출합니다.
        /// </summary>
        private static string ExtractParam(string args, string paramName)
        {
            Match match = Regex.Match(args, $@"--{paramName}[=\s]+(.*?)(?:\s+--|$)", RegexOptions.Singleline);
            
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
            
            return null;
        }
        
        /// <summary>
        /// 명령 해석 이벤트 발생
        /// </summary>
        private static void NotifyCommandInterpreted(string message)
        {
            Debug.Log("[MCPCommandInterpreter] " + message);
            OnCommandInterpreted?.Invoke(message);
        }
    }
} 