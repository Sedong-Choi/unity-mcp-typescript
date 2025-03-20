# MCP Client for Unity

Model Context Protocol (MCP) Client is a Unity editor extension that connects to an MCP server to generate and modify code using AI models. This package enables developers to leverage AI assistance directly within the Unity Editor for rapid prototyping and development.

## Features

- **AI-Powered Code Generation**: Generate Unity C# scripts based on natural language prompts
- **Code Modification**: Modify existing code with AI assistance
- **WebSocket Communication**: Seamless connection to MCP server
- **Conversation Management**: Maintain multiple conversation threads with the AI
- **Template System**: Use built-in templates or create custom ones for common code patterns
- **Code Preview**: Preview generated code before applying changes
- **Editor-Only Integration**: All functionality contained within the Unity Editor to keep builds lean

## Requirements

- Unity 2020.3 or newer
- MCP Server (included in the mcp-project repository)
- .NET 4.x or newer

## Installation

### Option 1: Using Unity Package Manager (Recommended)

1. Open your Unity project
2. Navigate to Window > Package Manager
3. Click the "+" button in the top-left corner
4. Select "Add package from disk..."
5. Navigate to the `com.mcp.client` folder and select the `package.json` file

### Option 2: Manual Installation

1. Copy the `com.mcp.client` directory into your Unity project's `Packages` folder
2. Unity will automatically detect and import the package

### Option 3: Using manifest.json

Add the following line to your `Packages/manifest.json` file:

```json
{
  "dependencies": {
    "com.mcp.client": "file:../path/to/com.mcp.client"
  }
}
```

## Quick Start

1. Start the MCP server:
   ```
   cd mcp-server
   npm install
   npm run start:dev
   ```

2. Open the MCP client in Unity:
   Navigate to Window > AI > Improved MCP Client

3. Connect to the server:
   The client will automatically connect to the local server (ws://localhost:8765) by default. If your server is running elsewhere, update the Server URL in the settings tab.

4. Start a new conversation:
   Click "New Conversation" and begin by describing what you'd like to create.

## Documentation

For detailed documentation, please refer to the [Documentation](Documentation~/index.md) folder within this package.

## Project Structure

```
com.mcp.client/
├── Editor/          # Editor-only code (not included in builds)
│   ├── UI/          # Editor UI components
│   └── Utils/       # Editor utilities
├── Runtime/         # Runtime code (can be included in builds if needed)
│   ├── Core/        # Core functionality
│   ├── Models/      # Data models
│   └── Utils/       # Utility classes
└── Documentation~/  # Detailed documentation
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

## Acknowledgements

The MCP Client utilizes the following third-party libraries:

- WebSocketSharp: For WebSocket communication in the Unity Editor
- Newtonsoft.Json: For JSON serialization and deserialization