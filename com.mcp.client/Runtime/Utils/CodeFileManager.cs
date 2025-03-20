using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using MCP.Models;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MCP.Utils
{
    /// <summary>
    /// Handles file operations for code files in the Unity project.
    /// </summary>
    public class CodeFileManager
    {
        private static CodeFileManager _instance;
        
        // Configuration
        private string _scriptRootPath = "Assets/Scripts";
        private string _defaultExtension = ".cs";
        private bool _createBackups = true;
        private string _backupExtension = ".bak";
        
        // Events
        public delegate void FileModificationHandler(CodeModification modification);
        public event FileModificationHandler OnFileModified;
        
        public static CodeFileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CodeFileManager();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Configure the file manager.
        /// </summary>
        public void Configure(string scriptRootPath = null, string defaultExtension = null, bool? createBackups = null)
        {
            if (scriptRootPath != null) _scriptRootPath = scriptRootPath;
            if (defaultExtension != null) _defaultExtension = defaultExtension;
            if (createBackups != null) _createBackups = createBackups.Value;
        }
        
        /// <summary>
        /// Normalize a file path to ensure it has the correct format.
        /// </summary>
        /// <param name="filePath">Raw file path</param>
        /// <returns>Normalized file path</returns>
        public string NormalizeFilePath(string filePath)
        {
            string normalizedPath = filePath.Replace('\\', '/');
            
            // Add script root path if not already present
            if (!normalizedPath.StartsWith(_scriptRootPath))
            {
                if (normalizedPath.StartsWith("Assets/") || normalizedPath.StartsWith("/Assets/"))
                {
                    // Path already has Assets/, don't need to modify
                }
                else
                {
                    normalizedPath = Path.Combine(_scriptRootPath, normalizedPath).Replace('\\', '/');
                }
            }
            
            // Add default extension if no extension present
            if (!HasCodeExtension(normalizedPath))
            {
                normalizedPath += _defaultExtension;
            }
            
            return normalizedPath;
        }
        
        /// <summary>
        /// Check if a file path has a recognized code file extension.
        /// </summary>
        private bool HasCodeExtension(string filePath)
        {
            string lowerPath = filePath.ToLower();
            return lowerPath.EndsWith(".cs") || 
                   lowerPath.EndsWith(".js") || 
                   lowerPath.EndsWith(".shader") || 
                   lowerPath.EndsWith(".compute") || 
                   lowerPath.EndsWith(".json");
        }
        
        /// <summary>
        /// Create a new file with the specified content.
        /// </summary>
        /// <param name="filePath">Path to create the file at</param>
        /// <param name="content">Content of the file</param>
        /// <returns>A CodeModification object with the result</returns>
        public CodeModification CreateFile(string filePath, string content)
        {
            try
            {
                string normalizedPath = NormalizeFilePath(filePath);
                string fullPath = Path.Combine(Application.dataPath, "..", normalizedPath).Replace('\\', '/');
                
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Check if file already exists
                bool fileExists = File.Exists(fullPath);
                
                // Create backup if needed
                if (fileExists && _createBackups)
                {
                    string backupPath = fullPath + _backupExtension;
                    File.Copy(fullPath, backupPath, true);
                    Debug.Log($"Created backup at {backupPath}");
                }
                
                // Write the file
                File.WriteAllText(fullPath, content);
                
                // Refresh asset database in editor
                RefreshAssetDatabase();
                
                var modification = CodeModification.CreateSuccess(normalizedPath, content);
                OnFileModified?.Invoke(modification);
                
                return modification;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating file {filePath}: {e.Message}");
                return CodeModification.Failed(filePath, "create", e.Message);
            }
        }
        
        /// <summary>
        /// Modify an existing file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="content">New content</param>
        /// <param name="sectionName">Optional section to modify</param>
        /// <returns>A CodeModification object with the result</returns>
        public CodeModification ModifyFile(string filePath, string content, string sectionName = null)
        {
            try
            {
                string normalizedPath = NormalizeFilePath(filePath);
                string fullPath = Path.Combine(Application.dataPath, "..", normalizedPath).Replace('\\', '/');
                
                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    return CreateFile(normalizedPath, content);
                }
                
                // Read the original content
                string originalContent = File.ReadAllText(fullPath);
                
                // Determine the new content
                string newContent;
                if (!string.IsNullOrEmpty(sectionName))
                {
                    // Modify only the specified section
                    newContent = ModifySection(originalContent, sectionName, content);
                }
                else
                {
                    // Replace the entire content
                    newContent = content;
                }
                
                // Create backup if needed
                if (_createBackups)
                {
                    string backupPath = fullPath + _backupExtension;
                    File.Copy(fullPath, backupPath, true);
                    Debug.Log($"Created backup at {backupPath}");
                }
                
                // Write the file
                File.WriteAllText(fullPath, newContent);
                
                // Refresh asset database in editor
                RefreshAssetDatabase();
                
                var modification = CodeModification.ModifySuccess(normalizedPath, originalContent, newContent, sectionName);
                OnFileModified?.Invoke(modification);
                
                return modification;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error modifying file {filePath}: {e.Message}");
                return CodeModification.Failed(filePath, "modify", e.Message);
            }
        }
        
        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>A CodeModification object with the result</returns>
        public CodeModification DeleteFile(string filePath)
        {
            try
            {
                string normalizedPath = NormalizeFilePath(filePath);
                string fullPath = Path.Combine(Application.dataPath, "..", normalizedPath).Replace('\\', '/');
                
                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    return CodeModification.Failed(normalizedPath, "delete", "File does not exist");
                }
                
                // Create backup before deletion
                if (_createBackups)
                {
                    string backupPath = fullPath + _backupExtension;
                    File.Copy(fullPath, backupPath, true);
                    Debug.Log($"Created backup at {backupPath}");
                }
                
                // Delete the file
                File.Delete(fullPath);
                
                // Refresh asset database in editor
                RefreshAssetDatabase();
                
                var modification = new CodeModification
                {
                    FilePath = normalizedPath,
                    Operation = "delete",
                    Success = true
                };
                
                OnFileModified?.Invoke(modification);
                
                return modification;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting file {filePath}: {e.Message}");
                return CodeModification.Failed(filePath, "delete", e.Message);
            }
        }
        
        /// <summary>
        /// Read a file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>File content or null if file doesn't exist</returns>
        public string ReadFile(string filePath)
        {
            try
            {
                string normalizedPath = NormalizeFilePath(filePath);
                string fullPath = Path.Combine(Application.dataPath, "..", normalizedPath).Replace('\\', '/');
                
                if (!File.Exists(fullPath))
                {
                    return null;
                }
                
                return File.ReadAllText(fullPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading file {filePath}: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get a list of script files in a directory.
        /// </summary>
        /// <param name="directory">Directory to search in (relative to script root)</param>
        /// <returns>List of script files</returns>
        public List<string> GetScriptFiles(string directory = "")
        {
            try
            {
                string normalizedDir = directory.Replace('\\', '/');
                string fullPath = Path.Combine(Application.dataPath, "..", _scriptRootPath, normalizedDir).Replace('\\', '/');
                
                if (!Directory.Exists(fullPath))
                {
                    return new List<string>();
                }
                
                string[] files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
                List<string> result = new List<string>();
                
                string rootPath = Path.Combine(Application.dataPath, "..").Replace('\\', '/');
                
                foreach (string file in files)
                {
                    string relativePath = file.Replace('\\', '/').Replace(rootPath + "/", "");
                    result.Add(relativePath);
                }
                
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing script files: {e.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Extract and modify a section of code.
        /// </summary>
        /// <param name="originalContent">Original file content</param>
        /// <param name="sectionName">Name of the section to modify</param>
        /// <param name="newSectionContent">New content for the section</param>
        /// <returns>Modified content</returns>
        private string ModifySection(string originalContent, string sectionName, string newSectionContent)
        {
            string startMarker = $"// BEGIN {sectionName}";
            string endMarker = $"// END {sectionName}";
            
            int startIndex = originalContent.IndexOf(startMarker);
            int endIndex = originalContent.IndexOf(endMarker);
            
            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
            {
                // Section not found, append it at the end
                return originalContent + "\n\n" + startMarker + "\n" + newSectionContent + "\n" + endMarker + "\n";
            }
            
            // Replace the content between markers (preserving the markers)
            string prefix = originalContent.Substring(0, startIndex + startMarker.Length);
            string suffix = originalContent.Substring(endIndex);
            
            return prefix + "\n" + newSectionContent + "\n" + suffix;
        }
        
        /// <summary>
        /// Refresh the Unity asset database to reflect file changes.
        /// </summary>
        private void RefreshAssetDatabase()
        {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
    }
}