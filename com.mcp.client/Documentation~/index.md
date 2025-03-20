# MCP Client Documentation

This documentation provides comprehensive guidance on using the Model Context Protocol (MCP) Client package for Unity.

## Table of Contents

1. [Introduction](#introduction)
2. [Architecture Overview](#architecture-overview)
3. [Installation](#installation)
4. [Getting Started](#getting-started)
5. [Main Components](#main-components)
6. [Usage Guide](#usage-guide)
7. [API Reference](#api-reference)
8. [Troubleshooting](#troubleshooting)

## Introduction

The MCP Client is a Unity Editor extension that connects to an MCP server to enable AI-powered code generation and modification directly within your Unity project. It allows developers to leverage large language models to assist with scripting tasks, accelerating the development process and reducing boilerplate code.

Key benefits include:

- Rapid prototyping of Unity scripts
- Automatic code generation based on natural language descriptions
- Seamless integration with your Unity project's file structure
- Editor-only functionality to ensure build sizes remain optimized

## Architecture Overview

The MCP Client package follows a modular architecture:

### Core Systems

- **Connection Layer**: Manages WebSocket communication with the MCP server
- **Request/Response System**: Handles message formatting, sending, and processing
- **Conversation Management**: Maintains conversation context and history

### UI Components

- **Editor Window**: The main interface for interacting with the AI
- **Conversation Panel**: Displays conversation history and handles input
- **Code Preview Panel**: Previews and manages generated code
- **Template Management**: Interface for managing code templates

### Utilities

- **Code File Manager**: Handles file operations for generated code
- **Asset Database Helper**: Manages Unity-specific asset operations

## Installation

The MCP Client is designed to be used as a Unity package. To install:

1. Ensure you have the MCP Server component set up and running
2. Add the MCP Client package to your Unity project using one of these methods:
   - Via Unity Package Manager: Window > Package Manager > Add package from disk...
   - Manual installation: Copy the package folder to your project's Packages directory
   - Via manifest.json: Add a reference to the package in your project's manifest

For detailed installation instructions, see the [README.md](../README.md) file.

## Getting Started

### Setting Up the MCP Server

Before using the MCP Client, you need a running MCP Server instance:

1. Navigate to the `mcp-server` directory
2. Install dependencies: `npm install`
3. Start the server: `npm run start:dev`
4. The server will be available at `ws://localhost:8765` by default

### Connecting the MCP Client

1. Open your Unity project
2. Navigate to Window > AI > Improved MCP Client
3. The client will attempt to connect to the local server automatically
4. If needed, update the connection settings in the Settings tab

### Your First Conversation

1. Start a new conversation using the "New Conversation" button
2. Enter a description of the script you want to create, for example:
   ```
   Create a player controller that uses WASD for movement and space to jump
   ```
3. Click "Generate Code" to send the request
4. Review the generated code in the Code Preview tab
5. Apply the changes to create the file in your project

## Main Components

### MCPConnection

The `MCPConnection` class handles WebSocket communication with the MCP server. It manages connection state, reconnection attempts, and message passing.

### MCPRequestManager

The `MCPRequestManager` class creates and sends requests to the MCP server. It handles message formatting, conversation tracking, and request options.

### MCPResponseHandler

The `MCPResponseHandler` class processes responses from the MCP server. It parses response data, extracts code modifications, and maintains message history.

### ConversationPanel

The `ConversationPanel` class provides a UI for interacting with the AI. It displays conversation history and allows for sending messages and code generation requests.

### CodePreviewPanel

The `CodePreviewPanel` class displays generated or modified code with syntax highlighting. It provides options for applying, editing, or rejecting code changes.

### PromptTemplates

The `PromptTemplates` class manages predefined and custom prompt templates for common code generation tasks. It allows saving, loading, and managing templates.

## Usage Guide

### Generating New Scripts

1. Start a new conversation or select an existing one
2. Describe the script you want to create in natural language
3. Click "Generate Code" to send the request
4. Review the generated code in the Code Preview tab
5. Click "Apply" to create the file in your project

### Modifying Existing Scripts

1. Start a new conversation or select an existing one
2. Describe the changes you want to make to an existing script
3. Include the file path in your description, for example:
   ```
   Modify Assets/Scripts/PlayerController.cs to add a crouch function when pressing C
   ```
4. Click "Generate Code" to send the request
5. Review the modified code in the Code Preview tab
6. Click "Apply" to update the file in your project

### Using Templates

1. Navigate to the Templates tab
2. Browse available templates by category
3. Select a template to view its content
4. Click "Use Template" to start a new conversation with the template as the prompt
5. Customize the prompt as needed before sending

### Creating Custom Templates

1. Navigate to the Templates tab
2. Enter a name and content for your template
3. Click "Save Template" to add it to your custom templates
4. Your custom templates will appear in the "Custom" category

## API Reference

### MCP.Core Namespace

- `MCPConnection`: Manages WebSocket connection to the MCP server
- `MCPRequestManager`: Handles request creation and sending
- `MCPResponseHandler`: Processes responses from the server

### MCP.Models Namespace

- `MCPMessage`: Represents a message in a conversation
- `CodeModification`: Represents a code modification operation

### MCP.UI Namespace

- `ConversationPanel`: UI component for displaying conversations
- `CodePreviewPanel`: UI component for previewing code
- `ImprovedMCPEditorWindow`: Main editor window

### MCP.Utils Namespace

- `CodeFileManager`: Manages file operations for code
- `AssetDatabaseHelper`: Helper for Unity's Asset Database
- `PromptTemplates`: Manages code generation templates

## Troubleshooting

### Connection Issues

If you're having trouble connecting to the MCP server:

1. Ensure the server is running (`npm run start:dev` in the `mcp-server` directory)
2. Check the server URL in the Settings tab (default: `ws://localhost:8765`)
3. Verify network connectivity and firewall settings
4. Check the console for error messages

### Missing Generated Files

If generated files are not appearing in your project:

1. Check the Output Path setting in the Settings tab
2. Ensure the specified directory exists in your project
3. Check if Unity's AssetDatabase needs refreshing (AssetDatabase.Refresh())
4. Look for error messages in the console

### Other Issues

For other issues:

1. Check the Unity Console for error messages
2. Verify that you're using a compatible Unity version (2020.3+)
3. Ensure all dependencies are properly installed

If problems persist, please report issues on the project's GitHub repository.