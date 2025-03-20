using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MCP.Utils
{
    /// <summary>
    /// Helper class for interacting with Unity's Asset Database.
    /// </summary>
    public static class AssetDatabaseHelper
    {
        /// <summary>
        /// Create a new script asset.
        /// </summary>
        /// <param name="fileName">Name of the file to create</param>
        /// <param name="content">Content of the script</param>
        /// <param name="directory">Directory to create the script in (relative to Assets)</param>
        /// <returns>True if successful</returns>
        public static bool CreateScriptAsset(string fileName, string content, string directory = "Scripts")
        {
#if UNITY_EDITOR
            try
            {
                // Normalize file name and add extension if needed
                if (!fileName.EndsWith(".cs"))
                {
                    fileName = fileName + ".cs";
                }
                
                // Ensure the directory exists
                string fullDir = Path.Combine("Assets", directory);
                if (!AssetDatabase.IsValidFolder(fullDir))
                {
                    CreateDirectoryRecursive(fullDir);
                }
                
                // Create the file path
                string fullPath = Path.Combine(fullDir, fileName);
                
                // Write the file
                File.WriteAllText(fullPath, content);
                
                // Refresh the asset database
                AssetDatabase.Refresh();
                
                Debug.Log($"Created script asset: {fullPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating script asset: {e.Message}");
                return false;
            }
#else
            Debug.LogError("Asset Database operations are only available in the Unity Editor");
            return false;
#endif
        }
        
        /// <summary>
        /// Create a directory and all necessary parent directories.
        /// </summary>
        private static void CreateDirectoryRecursive(string directory)
        {
#if UNITY_EDITOR
            string[] parts = directory.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            string currentPath = "";
            
            // Handle the special case of starting with "Assets"
            int startIndex = (parts[0] == "Assets") ? 1 : 0;
            if (startIndex == 1)
            {
                currentPath = "Assets";
            }
            
            for (int i = startIndex; i < parts.Length; i++)
            {
                string parentFolder = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : Path.Combine(currentPath, parts[i]);
                
                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    string guid = AssetDatabase.CreateFolder(parentFolder, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new Exception($"Failed to create folder: {currentPath}");
                    }
                }
            }
#endif
        }
        
        /// <summary>
        /// Get all script assets in a directory.
        /// </summary>
        /// <param name="directory">Directory to search in (relative to Assets)</param>
        /// <param name="recursive">Whether to include subdirectories</param>
        /// <returns>List of script asset paths</returns>
        public static List<string> GetScriptAssets(string directory = "Scripts", bool recursive = true)
        {
            List<string> scripts = new List<string>();
            
#if UNITY_EDITOR
            string fullDir = Path.Combine("Assets", directory);
            if (!AssetDatabase.IsValidFolder(fullDir))
            {
                return scripts;
            }
            
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { fullDir });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cs"))
                {
                    scripts.Add(path);
                }
            }
#endif
            
            return scripts;
        }
        
        /// <summary>
        /// Get script content.
        /// </summary>
        /// <param name="path">Path to the script asset</param>
        /// <returns>Script content or null if not found</returns>
        public static string GetScriptContent(string path)
        {
#if UNITY_EDITOR
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
                
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (textAsset != null)
                {
                    return textAsset.text;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading script content: {e.Message}");
            }
#endif
            
            return null;
        }
        
        /// <summary>
        /// Update script content.
        /// </summary>
        /// <param name="path">Path to the script asset</param>
        /// <param name="content">New content</param>
        /// <returns>True if successful</returns>
        public static bool UpdateScriptContent(string path, string content)
        {
#if UNITY_EDITOR
            try
            {
                // Ensure the file exists
                if (!File.Exists(path))
                {
                    Debug.LogError($"Script file not found: {path}");
                    return false;
                }
                
                // Write the new content
                File.WriteAllText(path, content);
                
                // Refresh the asset database
                AssetDatabase.Refresh();
                
                Debug.Log($"Updated script asset: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating script content: {e.Message}");
            }
#endif
            
            return false;
        }
        
        /// <summary>
        /// Create a backup of a script file.
        /// </summary>
        /// <param name="path">Path to the script asset</param>
        /// <returns>Path to the backup file or null if failed</returns>
        public static string CreateScriptBackup(string path)
        {
#if UNITY_EDITOR
            try
            {
                // Ensure the file exists
                if (!File.Exists(path))
                {
                    Debug.LogError($"Script file not found: {path}");
                    return null;
                }
                
                // Create backup filename with timestamp
                string directory = Path.GetDirectoryName(path);
                string fileName = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(directory, $"{fileName}_backup_{timestamp}{extension}");
                
                // Copy the file
                File.Copy(path, backupPath);
                
                Debug.Log($"Created backup: {backupPath}");
                return backupPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating script backup: {e.Message}");
            }
#endif
            
            return null;
        }
        
        /// <summary>
        /// Restore a script from a backup.
        /// </summary>
        /// <param name="backupPath">Path to the backup file</param>
        /// <param name="originalPath">Path to restore to (or null to infer from backup name)</param>
        /// <returns>True if successful</returns>
        public static bool RestoreScriptFromBackup(string backupPath, string originalPath = null)
        {
#if UNITY_EDITOR
            try
            {
                // Ensure the backup file exists
                if (!File.Exists(backupPath))
                {
                    Debug.LogError($"Backup file not found: {backupPath}");
                    return false;
                }
                
                // Determine original path if not provided
                if (string.IsNullOrEmpty(originalPath))
                {
                    string directory = Path.GetDirectoryName(backupPath);
                    string fileName = Path.GetFileNameWithoutExtension(backupPath);
                    string extension = Path.GetExtension(backupPath);
                    
                    // Remove "_backup_yyyyMMdd_HHmmss" suffix
                    int backupIndex = fileName.IndexOf("_backup_");
                    if (backupIndex > 0)
                    {
                        fileName = fileName.Substring(0, backupIndex);
                    }
                    
                    originalPath = Path.Combine(directory, fileName + extension);
                }
                
                // Copy the backup to the original
                File.Copy(backupPath, originalPath, true);
                
                // Refresh the asset database
                AssetDatabase.Refresh();
                
                Debug.Log($"Restored script from backup: {originalPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error restoring script from backup: {e.Message}");
            }
#endif
            
            return false;
        }
        
        /// <summary>
        /// Import a new asset into the project.
        /// </summary>
        /// <param name="filePath">Path to the file outside the project</param>
        /// <param name="targetPath">Target path in the project (relative to Assets)</param>
        /// <returns>True if successful</returns>
        public static bool ImportAsset(string filePath, string targetPath)
        {
#if UNITY_EDITOR
            try
            {
                // Ensure the file exists
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"File not found: {filePath}");
                    return false;
                }
                
                // Ensure the target directory exists
                string directory = Path.GetDirectoryName(targetPath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    CreateDirectoryRecursive(directory);
                }
                
                // Copy the file
                string fullTargetPath = Path.Combine(Application.dataPath, "..", targetPath);
                File.Copy(filePath, fullTargetPath, true);
                
                // Refresh the asset database
                AssetDatabase.Refresh();
                
                Debug.Log($"Imported asset: {targetPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error importing asset: {e.Message}");
            }
#endif
            
            return false;
        }
    }
}