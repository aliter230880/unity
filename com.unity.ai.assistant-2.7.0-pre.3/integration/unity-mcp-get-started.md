---
uid: unity-mcp-get-started
---

# Get started with Unity MCP

Set up Unity MCP and connect your preferred AI client to enable AI-driven control of the Unity Editor through Model Context Protocol (MCP).

Use Unity MCP to connect external AI clients, such as Claude Code or Cursor, to your Unity project.

## Prerequisites

Make sure your environment meets the following requirements:

- Unity 6 (6000.0) or later with the `com.unity.ai.assistant` package installed.
- An MCP-compatible AI client, such as [Claude Code](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview), [Cursor](https://www.cursor.com/), [Windsurf](https://windsurf.com/), or [Claude Desktop](https://claude.ai/download).

## Step 1: Verify Unity setup

Confirm that the Unity MCP bridge is active before you connect to an AI client.

To verify Unity MCP is running:

1. Open your Unity project with the AI Assistant package installed.
2. Go to **Edit** > **Project Settings** > **AI** > **Unity MCP Server**.

   For details about the available settings on this page, refer to the [Unity MCP Server configuration page reference](xref:unity-mcp-reference).

3. Check that **Unity Bridge** shows **Running** (green indicator).

   The bridge starts automatically when the Unity Editor loads.

4. If the editor shows **Stopped**, select **Start**.

When Unity starts, it also installs the relay binary to `~/.unity/relay/`. AI clients use this executable to connect to Unity.

## Step 2: Configure your MCP client

Configure your AI client to launch the Unity MCP relay as a server.

The **Integrations** section in the Unity MCP settings page can automatically configure supported clients. Expand **Integrations**, select your client, and select **Configure**.

Alternatively, configure your client manually by adding a server entry that points to the relay binary path.

### Claude Code

Add the following to your MCP server configuration:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<HOME_ABSOLUTE_PATH>/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64",
      "args": ["--mcp"]
    }
  }
}
```

### Cursor

In **Cursor Settings** > **MCP**, add a new server:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<HOME_ABSOLUTE_PATH>/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64",
      "args": ["--mcp"]
    }
  }
}
```

### Platform-specific paths

Use the following platform-specific paths for the relay binary. Beware that tildes (~) may not be expanded by the MCP client.

| Platform | Relay executable path |
|----------|-----------------------|
| macOS (Apple Silicon) | `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64` |
| macOS (Intel) | `~/.unity/relay/relay_mac_x64.app/Contents/MacOS/relay_mac_x64` |
| Windows | `%USERPROFILE%\.unity\relay\relay_win.exe` |
| Linux | `~/.unity/relay/relay_linux` |

The `--mcp` flag is required because it instructs the relay binary to operate as an MCP server (as opposed to its other modes).

### Target a specific Unity instance

By default, the relay connects to the first Unity Editor instance it discovers. If you have multiple Unity instances open, you can target a specific one by project path or editor process ID (PID).

You can specify targeting through command-line arguments or environment variables. If both are set, the command-line argument takes precedence.

| Method | Argument | Environment variable |
|--------|----------|----------------------|
| Project path | `--project-path <path>` | `UNITY_PROJECT_PATH` |
| Instance ID (editor PID) | `--instance-id <pid>` | `UNITY_INSTANCE_ID` |

For example, to target a specific project path using a command-line argument:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<HOME_ABSOLUTE_PATH>/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64",
      "args": ["--mcp", "--project-path", "/Users/me/MyUnityProject"]
    }
  }
}
```

To target a specific project path using an environment variable:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<HOME_ABSOLUTE_PATH>/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64",
      "args": ["--mcp"],
      "env": {
        "UNITY_PROJECT_PATH": "/Users/me/MyUnityProject"
      }
    }
  }
}
```

## Step 3: Approve the connection

When an external MCP client connects for the first time, Unity shows a **Pending Connection** message in the Unity MCP settings page. You must approve it before the client can invoke tools.

To approve the AI client the first time it connects to Unity:

1. Go to **Edit** > **Project Settings** > **AI** > **Unity MCP**.
2. In the **Pending Connections** section, review the client details.
3. Select **Accept** to approve or **Deny** to reject the connection.

Previously approved clients reconnect automatically and do not need re-approval.

> [!NOTE]
> Connections from Assistant through AI Gateway connections are approved automatically and don't require manual approval.

## Step 4: Test the connection

To verify that AI clients can discover and use Unity tools:

1. Start Unity and open your project.
2. Start your MCP client.
3. Verify the connection:
   - The client appears under **Connected Clients** in the Unity MCP settings page.
   - Your MCP client lists the available Unity MCP tools. For example, `Unity_ManageScene` and `Unity_ManageGameObject`.

### Test with a simple command

To test the functionality, run a simple command in your MCP client:

```
Read the Unity console messages and summarize any warnings or errors.
```

The client might use the `Unity_ReadConsole` tool to fetch Unity's console output.

## Additional resources

- [AI client integration with Unity (MCP)](xref:unity-mcp-overview)
- [Register custom MCP tools](xref:unity-mcp-tool-registration)
- [Troubleshoot Unity MCP bridge issues](xref:unity-mcp-troubleshooting)
