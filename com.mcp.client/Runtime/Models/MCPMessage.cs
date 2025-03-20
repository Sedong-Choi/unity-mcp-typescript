using System;

namespace MCP.Models
{
    /// <summary>
    /// Represents a message in the MCP conversation.
    /// </summary>
    [Serializable]
    public class MCPMessage
    {
        /// <summary>
        /// The role of the message sender.
        /// </summary>
        public enum Role
        {
            User,
            Assistant,
            System
        }
        
        /// <summary>
        /// The role of the message sender.
        /// </summary>
        public Role MessageRole { get; set; }
        
        /// <summary>
        /// The content of the message.
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// The timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Whether this message has been fully received (for streaming responses).
        /// </summary>
        public bool IsComplete { get; set; }
        
        /// <summary>
        /// Creates a new message with the current timestamp.
        /// </summary>
        public MCPMessage()
        {
            Timestamp = DateTime.Now;
            IsComplete = true;
        }
        
        /// <summary>
        /// Creates a new message with the specified role and content.
        /// </summary>
        /// <param name="role">The role of the message sender</param>
        /// <param name="content">The content of the message</param>
        /// <param name="isComplete">Whether the message is complete</param>
        public MCPMessage(Role role, string content, bool isComplete = true)
        {
            MessageRole = role;
            Content = content;
            Timestamp = DateTime.Now;
            IsComplete = isComplete;
        }
        
        /// <summary>
        /// Creates a user message.
        /// </summary>
        /// <param name="content">Message content</param>
        /// <returns>A new MCPMessage instance</returns>
        public static MCPMessage CreateUserMessage(string content)
        {
            return new MCPMessage(Role.User, content);
        }
        
        /// <summary>
        /// Creates an assistant message.
        /// </summary>
        /// <param name="content">Message content</param>
        /// <param name="isComplete">Whether the message is complete</param>
        /// <returns>A new MCPMessage instance</returns>
        public static MCPMessage CreateAssistantMessage(string content, bool isComplete = true)
        {
            return new MCPMessage(Role.Assistant, content, isComplete);
        }
        
        /// <summary>
        /// Creates a system message.
        /// </summary>
        /// <param name="content">Message content</param>
        /// <returns>A new MCPMessage instance</returns>
        public static MCPMessage CreateSystemMessage(string content)
        {
            return new MCPMessage(Role.System, content);
        }
    }
}