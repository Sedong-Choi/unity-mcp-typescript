using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MCP.Models;
using MCP.Utils;

namespace MCP.UI
{
    /// <summary>
    /// UI component for previewing and applying code modifications.
    /// </summary>
    public class CodePreviewPanel
    {
        // References
        private CodeFileManager _codeFileManager;

        // UI state
        private Vector2 _scrollPosition;
        private bool _showPreview = true;
        private bool _showOriginal = false;
        private CodeModification _currentModification;
        private List<CodeModification> _recentModifications = new List<CodeModification>();
        private int _selectedModificationIndex = -1;

        // Styles
        private GUIStyle _previewStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _filePathStyle;

        // Events
        public delegate void CodeAppliedHandler(CodeModification modification);
        public event CodeAppliedHandler OnCodeApplied;

        public CodePreviewPanel()
        {
            _codeFileManager = CodeFileManager.Instance;

            // Subscribe to file modification events
            _codeFileManager.OnFileModified += HandleFileModified;

            InitializeStyles();
        }

        /// <summary>
        /// Initialize GUI styles.
        /// </summary>
        private void InitializeStyles()
        {
            _previewStyle = new GUIStyle(EditorStyles.textArea);
            _previewStyle.font = EditorStyles.standardFont;
            _previewStyle.wordWrap = true;
            _previewStyle.richText = true;
            _previewStyle.fontSize = 12;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel);
            _headerStyle.fontSize = 14;

            _filePathStyle = new GUIStyle(EditorStyles.miniLabel);
            _filePathStyle.wordWrap = true;
        }

        /// <summary>
        /// Draw the code preview panel.
        /// </summary>
        /// <param name="rect">Area to draw the panel</param>
        public void OnGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.BeginVertical();

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Code Preview", _headerStyle);

            // Display buttons based on current state
            if (_currentModification != null)
            {
                _showPreview = GUILayout.Toggle(_showPreview, "Preview", EditorStyles.miniButtonLeft);
                _showOriginal = GUILayout.Toggle(_showOriginal, "Original", EditorStyles.miniButtonRight);
            }

            EditorGUILayout.EndHorizontal();

            // Recent modifications dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Recent Modifications:", GUILayout.Width(130));

            // Create dropdown items
            string[] options = new string[_recentModifications.Count + 1];
            options[0] = "-- Select --";

            for (int i = 0; i < _recentModifications.Count; i++)
            {
                var mod = _recentModifications[i];
                options[i + 1] = $"{mod.Operation} {mod.Filename}";
            }

            int newIndex = EditorGUILayout.Popup(_selectedModificationIndex + 1, options) - 1;
            if (newIndex != _selectedModificationIndex)
            {
                _selectedModificationIndex = newIndex;
                if (_selectedModificationIndex >= 0 && _selectedModificationIndex < _recentModifications.Count)
                {
                    _currentModification = _recentModifications[_selectedModificationIndex];
                }
                else
                {
                    _currentModification = null;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Main content
            EditorGUILayout.Space(10);

            if (_currentModification != null)
            {
                DrawModificationPreview();
            }
            else
            {
                EditorGUILayout.HelpBox("No code modification selected.\nAsk the AI to generate or modify code to see a preview here.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            GUILayout.EndArea();
        }

        /// <summary>
        /// Draw the modification preview.
        /// </summary>
        private void DrawModificationPreview()
        {
            // File info
            EditorGUILayout.LabelField($"File: {_currentModification.FilePath}", _filePathStyle);
            EditorGUILayout.LabelField($"Operation: {_currentModification.Operation}", _filePathStyle);

            if (_currentModification.TargetSection != null)
            {
                EditorGUILayout.LabelField($"Section: {_currentModification.TargetSection}", _filePathStyle);
            }

            EditorGUILayout.Space(5);

            // Status and action buttons
            EditorGUILayout.BeginHorizontal();

            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = _currentModification.Success ?
                new Color(0.2f, 0.8f, 0.2f) :
                new Color(1.0f, 0.3f, 0.3f);

            EditorGUILayout.LabelField(_currentModification.Success ? "Applied" : "Not Applied", statusStyle);

            GUILayout.FlexibleSpace();

            // Different actions based on status
            if (_currentModification.Success)
            {
                if (GUILayout.Button("Open File", GUILayout.Width(80)))
                {
                    OpenFile(_currentModification.FilePath);
                }

                if (_currentModification.IsCreateOperation || _currentModification.IsModifyOperation)
                {
                    if (GUILayout.Button("Revert", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("Revert Changes",
                            "Are you sure you want to revert these changes? This will restore the file from backup if available.",
                            "Revert", "Cancel"))
                        {
                            RevertChanges();
                        }
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                {
                    ApplyChanges();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Code preview/original
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            string displayContent = "";

            if (_showOriginal && !string.IsNullOrEmpty(_currentModification.OriginalContent))
            {
                displayContent = _currentModification.OriginalContent;
            }
            else if (_showPreview && !string.IsNullOrEmpty(_currentModification.NewContent))
            {
                displayContent = _currentModification.NewContent;
            }
            else
            {
                // Load content from file if needed
                displayContent = GetContentForDisplay();
            }

            // Apply syntax highlighting
            displayContent = ApplySyntaxHighlighting(displayContent);

            GUIStyle contentStyle = new GUIStyle(_previewStyle);
            contentStyle.normal.background = CreateColorTexture(new Color(0.2f, 0.2f, 0.2f, 0.5f));

            EditorGUILayout.TextArea(displayContent, contentStyle);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Load the appropriate content for display.
        /// </summary>
        private string GetContentForDisplay()
        {
            // If we have NewContent and should show preview, use that
            if (_showPreview && !string.IsNullOrEmpty(_currentModification.NewContent))
            {
                return _currentModification.NewContent;
            }

            // If we have OriginalContent and should show original, use that
            if (_showOriginal && !string.IsNullOrEmpty(_currentModification.OriginalContent))
            {
                return _currentModification.OriginalContent;
            }

            // Try to read from file
            if (_currentModification.FilePath != null)
            {
                string content = _codeFileManager.ReadFile(_currentModification.FilePath);
                if (content != null)
                {
                    return content;
                }
            }

            // Fallback
            if (_showOriginal)
            {
                return "Original content not available.";
            }
            else
            {
                return "Preview content not available.";
            }
        }

        /// <summary>
        /// Apply basic syntax highlighting for code display.
        /// </summary>
        private string ApplySyntaxHighlighting(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            // This is a simple implementation - could be enhanced with more comprehensive parsing

            // Keywords
            string[] keywords = new string[] {
                "public", "private", "protected", "internal", "class", "struct", "interface",
                "void", "int", "float", "double", "bool", "string", "var", "readonly", "const",
                "static", "virtual", "override", "abstract", "sealed", "namespace", "using",
                "if", "else", "for", "foreach", "while", "do", "switch", "case", "break", "continue",
                "return", "new", "this", "base", "null", "true", "false"
            };

            string highlighted = code;

            // Comment highlighting (simple approximation)
            int index = 0;
            while ((index = highlighted.IndexOf("//", index)) != -1)
            {
                int endOfLine = highlighted.IndexOf('\n', index);
                if (endOfLine == -1) endOfLine = highlighted.Length;

                string comment = highlighted.Substring(index, endOfLine - index);
                highlighted = highlighted.Replace(comment, $"<color=#57A64A>{comment}</color>");

                index = endOfLine;
            }

            // Highlight keywords (very simple approach - doesn't account for context)
            foreach (var keyword in keywords)
            {
                // This is a simplistic approach that might color keywords in comments/strings
                highlighted = highlighted.Replace($" {keyword} ", $" <color=#569CD6>{keyword}</color> ");
                highlighted = highlighted.Replace($" {keyword}(", $" <color=#569CD6>{keyword}</color>(");
                highlighted = highlighted.Replace($" {keyword}<", $" <color=#569CD6>{keyword}</color><");
                highlighted = highlighted.Replace($" {keyword};", $" <color=#569CD6>{keyword}</color>;");
                highlighted = highlighted.Replace($" {keyword}\n", $" <color=#569CD6>{keyword}</color>\n");
                highlighted = highlighted.Replace($" {keyword}\r", $" <color=#569CD6>{keyword}</color>\r");
                highlighted = highlighted.Replace($"({keyword} ", $"(<color=#569CD6>{keyword}</color> ");
                highlighted = highlighted.Replace($"({keyword})", $"(<color=#569CD6>{keyword}</color>)");
            }

            // String highlighting (very simple approach - doesn't handle escapes)
            index = 0;
            while (true)
            {
                int startQuote = highlighted.IndexOf("\"", index);
                if (startQuote == -1) break;

                int endQuote = highlighted.IndexOf("\"", startQuote + 1);
                if (endQuote == -1) break;

                string str = highlighted.Substring(startQuote, endQuote - startQuote + 1);
                string coloredStr = $"<color=#D69D85>{str}</color>";

                highlighted = highlighted.Substring(0, startQuote) + coloredStr + highlighted.Substring(endQuote + 1);

                index = startQuote + coloredStr.Length;
            }

            return highlighted;
        }

        /// <summary>
        /// Create a solid color texture.
        /// </summary>
        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Open the file in the Unity editor.
        /// </summary>
        private void OpenFile(string filePath)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, "..", filePath);
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
        }

        /// <summary>
        /// Apply the current changes.
        /// </summary>
        private void ApplyChanges()
        {
            if (_currentModification == null || _currentModification.Success)
                return;

            CodeModification result = null;

            switch (_currentModification.Operation.ToLower())
            {
                case "create":
                    result = _codeFileManager.CreateFile(
                        _currentModification.FilePath,
                        _currentModification.NewContent);
                    break;

                case "modify":
                    result = _codeFileManager.ModifyFile(
                        _currentModification.FilePath,
                        _currentModification.NewContent,
                        _currentModification.TargetSection);
                    break;

                case "delete":
                    result = _codeFileManager.DeleteFile(_currentModification.FilePath);
                    break;
            }

            if (result != null && result.Success)
            {
                // Update the current modification
                _currentModification = result;

                // Update the entry in the recent modifications list
                if (_selectedModificationIndex >= 0 && _selectedModificationIndex < _recentModifications.Count)
                {
                    _recentModifications[_selectedModificationIndex] = result;
                }

                // Notify listeners
                OnCodeApplied?.Invoke(result);
            }
        }

        /// <summary>
        /// Revert the current changes.
        /// </summary>
        private void RevertChanges()
        {
            // 백업 파일 찾기
            string backupPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(_currentModification.FilePath),
                System.IO.Path.GetFileNameWithoutExtension(_currentModification.FilePath) +
                "_backup_*" + System.IO.Path.GetExtension(_currentModification.FilePath));

            string[] backupFiles = System.IO.Directory.GetFiles(
                System.IO.Path.GetDirectoryName(backupPath),
                System.IO.Path.GetFileName(backupPath));

            if (backupFiles.Length > 0)
            {
                // 가장 최근 백업 사용
                string latestBackup = backupFiles[0];
                foreach (string backup in backupFiles)
                {
                    if (System.IO.File.GetLastWriteTime(backup) >
                        System.IO.File.GetLastWriteTime(latestBackup))
                    {
                        latestBackup = backup;
                    }
                }

                // 백업에서 복원
                _codeFileManager.RestoreFromBackup(latestBackup, _currentModification.FilePath);
            }
            else
            {
                EditorUtility.DisplayDialog("백업 없음",
                    "복원할 백업 파일을 찾을 수 없습니다.", "확인");
            }
        }

        /// <summary>
        /// Handle file modification events.
        /// </summary>
        private void HandleFileModified(CodeModification modification)
        {
            // Add to recent modifications
            _recentModifications.Insert(0, modification);

            // Limit to the most recent 20 modifications
            if (_recentModifications.Count > 20)
            {
                _recentModifications.RemoveRange(20, _recentModifications.Count - 20);
            }

            // Select it
            _selectedModificationIndex = 0;
            _currentModification = modification;
        }

        /// <summary>
        /// Set the current modification to preview.
        /// </summary>
        public void SetModification(CodeModification modification)
        {
            _currentModification = modification;

            // Find in recent modifications list
            _selectedModificationIndex = -1;
            for (int i = 0; i < _recentModifications.Count; i++)
            {
                if (_recentModifications[i].FilePath == modification.FilePath &&
                    _recentModifications[i].Operation == modification.Operation)
                {
                    _selectedModificationIndex = i;
                    break;
                }
            }

            // If not found, add it
            if (_selectedModificationIndex == -1)
            {
                _recentModifications.Insert(0, modification);
                _selectedModificationIndex = 0;

                // Limit to the most recent 20 modifications
                if (_recentModifications.Count > 20)
                {
                    _recentModifications.RemoveRange(20, _recentModifications.Count - 20);
                }
            }
        }

        /// <summary>
        /// Clean up when destroying this panel.
        /// </summary>
        public void OnDestroy()
        {
            // Unsubscribe from events
            if (_codeFileManager != null)
            {
                _codeFileManager.OnFileModified -= HandleFileModified;
            }
        }
    }
}