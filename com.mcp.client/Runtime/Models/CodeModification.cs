using System;

namespace MCP.Models
{
    /// <summary>
    /// Represents a code modification operation result.
    /// </summary>
    [Serializable]
    public class CodeModification
    {
        /// <summary>
        /// Path to the file that was modified.
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Type of operation performed: 'create', 'modify', or 'delete'.
        /// </summary>
        public string Operation { get; set; }
        
        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Target section for modification operations.
        /// </summary>
        public string TargetSection { get; set; }
        
        /// <summary>
        /// The original file content before modification (if available).
        /// </summary>
        public string OriginalContent { get; set; }
        
        /// <summary>
        /// The new file content after modification (if available).
        /// </summary>
        public string NewContent { get; set; }
        
        /// <summary>
        /// Check if this is a create operation.
        /// </summary>
        public bool IsCreateOperation => Operation?.ToLower() == "create";
        
        /// <summary>
        /// Check if this is a modify operation.
        /// </summary>
        public bool IsModifyOperation => Operation?.ToLower() == "modify";
        
        /// <summary>
        /// Check if this is a delete operation.
        /// </summary>
        public bool IsDeleteOperation => Operation?.ToLower() == "delete";
        
        /// <summary>
        /// Get the filename without the path.
        /// </summary>
        public string Filename 
        { 
            get 
            {
                if (string.IsNullOrEmpty(FilePath))
                    return string.Empty;
                
                int lastSlash = FilePath.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < FilePath.Length - 1)
                    return FilePath.Substring(lastSlash + 1);
                
                return FilePath;
            }
        }
        
        /// <summary>
        /// Get a human-readable summary of the modification.
        /// </summary>
        public string Summary
        {
            get
            {
                string action = Operation switch
                {
                    "create" => "Created",
                    "modify" => "Modified",
                    "delete" => "Deleted",
                    _ => Operation
                };
                
                string result = Success ? "Success" : "Failed";
                
                if (!Success && !string.IsNullOrEmpty(Error))
                {
                    result += $": {Error}";
                }
                
                return $"{action} {Filename} - {result}";
            }
        }
        
        /// <summary>
        /// Create a CodeModification for a successful file creation.
        /// </summary>
        public static CodeModification CreateSuccess(string filePath, string content)
        {
            return new CodeModification
            {
                FilePath = filePath,
                Operation = "create",
                Success = true,
                NewContent = content
            };
        }
        
        /// <summary>
        /// Create a CodeModification for a successful file modification.
        /// </summary>
        public static CodeModification ModifySuccess(string filePath, string originalContent, string newContent, string section = null)
        {
            return new CodeModification
            {
                FilePath = filePath,
                Operation = "modify",
                Success = true,
                OriginalContent = originalContent,
                NewContent = newContent,
                TargetSection = section
            };
        }
        
        /// <summary>
        /// Create a CodeModification for a failed operation.
        /// </summary>
        public static CodeModification Failed(string filePath, string operation, string error)
        {
            return new CodeModification
            {
                FilePath = filePath,
                Operation = operation,
                Success = false,
                Error = error
            };
        }
    }
}