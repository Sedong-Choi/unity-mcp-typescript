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
    /// Manages templates for AI prompts.
    /// </summary>
    public class PromptTemplates
    {
        private static PromptTemplates _instance;
        
        // Template storage
        private Dictionary<string, string> _builtInTemplates = new Dictionary<string, string>();
        private Dictionary<string, string> _customTemplates = new Dictionary<string, string>();
        
        // Template categories
        private Dictionary<string, List<string>> _categories = new Dictionary<string, List<string>>();
        
        // Preferences key for custom templates
        private const string PREFS_CUSTOM_TEMPLATES = "MCP_CUSTOM_TEMPLATES";
        
        public static PromptTemplates Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PromptTemplates();
                }
                return _instance;
            }
        }
        
        public PromptTemplates()
        {
            InitializeBuiltInTemplates();
            LoadCustomTemplates();
        }
        
        /// <summary>
        /// Initialize built-in templates.
        /// </summary>
        private void InitializeBuiltInTemplates()
        {
            // Define categories
            _categories["Components"] = new List<string>();
            _categories["Behaviors"] = new List<string>();
            _categories["UI"] = new List<string>();
            _categories["Utilities"] = new List<string>();
            
            // Standard Unity component templates
            AddBuiltInTemplate(
                "Basic MonoBehaviour",
                "Create a basic MonoBehaviour script that implements the standard Unity lifecycle methods (Awake, Start, Update).",
                "Components"
            );
            
            AddBuiltInTemplate(
                "Player Controller",
                "Create a player controller script with WASD movement and space to jump. Include smooth rotation and physics-based movement.",
                "Components"
            );
            
            AddBuiltInTemplate(
                "Camera Controller",
                "Create a smooth-following third-person camera controller that follows a target with configurable offset, smoothing, and collision avoidance.",
                "Components"
            );
            
            // Behavior templates
            AddBuiltInTemplate(
                "State Machine",
                "Create a flexible state machine system with base state class and state manager. Include example states for idle, move, and attack.",
                "Behaviors"
            );
            
            AddBuiltInTemplate(
                "Enemy AI",
                "Create an enemy AI with patrol, chase, and attack behaviors using a state machine pattern. Include vision detection and navmesh integration.",
                "Behaviors"
            );
            
            // UI templates
            AddBuiltInTemplate(
                "UI Manager",
                "Create a UI manager that handles showing/hiding different UI panels, manages transitions, and maintains references to UI elements.",
                "UI"
            );
            
            AddBuiltInTemplate(
                "Inventory UI",
                "Create an inventory UI system with item slots, dragging and dropping items, tooltips, and integration with a backend inventory system.",
                "UI"
            );
            
            // Utility templates
            AddBuiltInTemplate(
                "Object Pooling",
                "Create an object pooling system for efficiently reusing GameObjects like projectiles, particles, or enemies instead of destroying and instantiating them.",
                "Utilities"
            );
            
            AddBuiltInTemplate(
                "Singleton",
                "Create a generic singleton pattern implementation that enforces single instance of a MonoBehaviour across scenes.",
                "Utilities"
            );
            
            AddBuiltInTemplate(
                "Event System",
                "Create a flexible event system using C# events and delegates, with global and local event managers for game-wide communication.",
                "Utilities"
            );
            
            AddBuiltInTemplate(
                "Save System",
                "Create a save/load system using JSON serialization to persist game data between sessions. Include automatic saving and loading from files.",
                "Utilities"
            );
        }
        
        /// <summary>
        /// Add a built-in template.
        /// </summary>
        private void AddBuiltInTemplate(string name, string content, string category)
        {
            // Generate a key based on the name
            string key = name.Replace(" ", "_").ToLower();
            
            // Add to templates
            _builtInTemplates[key] = content;
            
            // Add to category
            if (!_categories.ContainsKey(category))
            {
                _categories[category] = new List<string>();
            }
            
            _categories[category].Add(key);
        }
        
        /// <summary>
        /// Load custom templates from editor preferences.
        /// </summary>
        private void LoadCustomTemplates()
        {
#if UNITY_EDITOR
            string json = EditorPrefs.GetString(PREFS_CUSTOM_TEMPLATES, "{}");
            try
            {
                _customTemplates = JsonUtility.FromJson<SerializableDictionary>(json).ToDictionary();
                
                // Add custom category if needed
                if (!_categories.ContainsKey("Custom"))
                {
                    _categories["Custom"] = new List<string>();
                }
                
                // Add custom templates to their category
                _categories["Custom"].Clear();
                foreach (var key in _customTemplates.Keys)
                {
                    _categories["Custom"].Add(key);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading custom templates: {e.Message}");
                _customTemplates = new Dictionary<string, string>();
            }
#endif
        }
        
        /// <summary>
        /// Save custom templates to editor preferences.
        /// </summary>
        private void SaveCustomTemplates()
        {
#if UNITY_EDITOR
            try
            {
                string json = JsonUtility.ToJson(new SerializableDictionary(_customTemplates));
                EditorPrefs.SetString(PREFS_CUSTOM_TEMPLATES, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving custom templates: {e.Message}");
            }
#endif
        }
        
        /// <summary>
        /// Get a template by key.
        /// </summary>
        /// <param name="key">Template key</param>
        /// <returns>Template content or null if not found</returns>
        public string GetTemplate(string key)
        {
            if (_builtInTemplates.TryGetValue(key, out string builtInTemplate))
            {
                return builtInTemplate;
            }
            
            if (_customTemplates.TryGetValue(key, out string customTemplate))
            {
                return customTemplate;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get all template keys.
        /// </summary>
        /// <returns>List of template keys</returns>
        public List<string> GetAllTemplateKeys()
        {
            List<string> keys = new List<string>();
            keys.AddRange(_builtInTemplates.Keys);
            keys.AddRange(_customTemplates.Keys);
            return keys;
        }
        
        /// <summary>
        /// Get all template names organized by category.
        /// </summary>
        /// <returns>Dictionary of category name to list of template keys</returns>
        public Dictionary<string, List<string>> GetTemplatesByCategory()
        {
            return new Dictionary<string, List<string>>(_categories);
        }
        
        /// <summary>
        /// Get the display name for a template key.
        /// </summary>
        /// <param name="key">Template key</param>
        /// <returns>Display name</returns>
        public string GetTemplateName(string key)
        {
            // Convert key to display name (replace underscores with spaces, title case)
            return System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(
                key.Replace("_", " ")
            );
        }
        
        /// <summary>
        /// Add a custom template.
        /// </summary>
        /// <param name="name">Template name</param>
        /// <param name="content">Template content</param>
        /// <returns>Generated key</returns>
        public string AddCustomTemplate(string name, string content)
        {
            // Generate a key based on the name
            string key = name.Replace(" ", "_").ToLower();
            
            // Ensure the key is unique
            if (_builtInTemplates.ContainsKey(key))
            {
                key = "custom_" + key;
            }
            
            int counter = 1;
            string baseKey = key;
            while (_customTemplates.ContainsKey(key))
            {
                key = $"{baseKey}_{counter}";
                counter++;
            }
            
            // Add to custom templates
            _customTemplates[key] = content;
            
            // Add to category
            if (!_categories.ContainsKey("Custom"))
            {
                _categories["Custom"] = new List<string>();
            }
            
            _categories["Custom"].Add(key);
            
            // Save changes
            SaveCustomTemplates();
            
            return key;
        }
        
        /// <summary>
        /// Update a custom template.
        /// </summary>
        /// <param name="key">Template key</param>
        /// <param name="content">New content</param>
        /// <returns>True if successful</returns>
        public bool UpdateCustomTemplate(string key, string content)
        {
            if (_customTemplates.ContainsKey(key))
            {
                _customTemplates[key] = content;
                SaveCustomTemplates();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Remove a custom template.
        /// </summary>
        /// <param name="key">Template key</param>
        /// <returns>True if successful</returns>
        public bool RemoveCustomTemplate(string key)
        {
            if (_customTemplates.ContainsKey(key))
            {
                _customTemplates.Remove(key);
                
                // Remove from category
                if (_categories.ContainsKey("Custom"))
                {
                    _categories["Custom"].Remove(key);
                }
                
                SaveCustomTemplates();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Export templates to a JSON file.
        /// </summary>
        /// <param name="filePath">File path to export to</param>
        /// <returns>True if successful</returns>
        public bool ExportTemplates(string filePath)
        {
            try
            {
                string json = JsonUtility.ToJson(new SerializableDictionary(_customTemplates), true);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting templates: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Import templates from a JSON file.
        /// </summary>
        /// <param name="filePath">File path to import from</param>
        /// <returns>True if successful</returns>
        public bool ImportTemplates(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                Dictionary<string, string> importedTemplates = JsonUtility.FromJson<SerializableDictionary>(json).ToDictionary();
                
                // Merge with existing templates
                foreach (var kvp in importedTemplates)
                {
                    _customTemplates[kvp.Key] = kvp.Value;
                    
                    // Add to category
                    if (!_categories.ContainsKey("Custom"))
                    {
                        _categories["Custom"] = new List<string>();
                    }
                    
                    if (!_categories["Custom"].Contains(kvp.Key))
                    {
                        _categories["Custom"].Add(kvp.Key);
                    }
                }
                
                SaveCustomTemplates();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error importing templates: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Helper class for serializing dictionaries.
        /// </summary>
        [Serializable]
        private class SerializableDictionary
        {
            [Serializable]
            public struct Entry
            {
                public string Key;
                public string Value;
            }
            
            public List<Entry> Entries = new List<Entry>();
            
            public SerializableDictionary() { }
            
            public SerializableDictionary(Dictionary<string, string> dictionary)
            {
                foreach (var kvp in dictionary)
                {
                    Entries.Add(new Entry { Key = kvp.Key, Value = kvp.Value });
                }
            }
            
            public Dictionary<string, string> ToDictionary()
            {
                Dictionary<string, string> result = new Dictionary<string, string>();
                foreach (var entry in Entries)
                {
                    result[entry.Key] = entry.Value;
                }
                return result;
            }
        }
    }
}