import type OpenAI from "openai";

// 8 Tools for AI Unity Developer
export const UNITY_TOOLS: OpenAI.ChatCompletionTool[] = [
  {
    type: "function",
    function: {
      name: "create_script",
      description: "Create a new C# script in Unity project. Use this to create MonoBehaviours, ScriptableObjects, Editor scripts, etc.",
      parameters: {
        type: "object",
        properties: {
          file_path: {
            type: "string",
            description: "Path relative to Assets folder, e.g. 'Scripts/Enemy/EnemyAI.cs'",
          },
          content: {
            type: "string",
            description: "Full C# code content of the script",
          },
        },
        required: ["file_path", "content"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "modify_script",
      description: "Modify an existing C# script. Use this to update logic, add methods, fix bugs, etc.",
      parameters: {
        type: "object",
        properties: {
          file_path: {
            type: "string",
            description: "Path relative to Assets folder, e.g. 'Scripts/Enemy/EnemyAI.cs'",
          },
          content: {
            type: "string",
            description: "New complete C# code content (full file replacement)",
          },
        },
        required: ["file_path", "content"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_script",
      description: "Read the content of an existing script or file in the Unity project",
      parameters: {
        type: "object",
        properties: {
          file_path: {
            type: "string",
            description: "Path relative to Assets folder, e.g. 'Scripts/Player/PlayerController.cs'",
          },
        },
        required: ["file_path"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "list_project_files",
      description: "Get the full list of files in the Unity project (the Project Map). Use this to understand project structure.",
      parameters: {
        type: "object",
        properties: {
          filter_type: {
            type: "string",
            description: "Optional: filter by file type. Values: 'script', 'shader', 'scene', 'prefab', 'all'",
            enum: ["script", "shader", "scene", "prefab", "all"],
          },
        },
        required: ["filter_type"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "set_object_property",
      description: "Change a property on a GameObject in the scene or prefab. Use this to configure components, positions, colors, etc.",
      parameters: {
        type: "object",
        properties: {
          object_path: {
            type: "string",
            description: "Hierarchy path of the GameObject, e.g. 'Enemies/Enemy_01'",
          },
          component: {
            type: "string",
            description: "Component type name, e.g. 'Transform', 'Light', 'Renderer'",
          },
          property: {
            type: "string",
            description: "Property name, e.g. 'position', 'color', 'intensity'",
          },
          value: {
            type: "string",
            description: "JSON-encoded value, e.g. '{\"x\":1,\"y\":2,\"z\":3}' for Vector3",
          },
        },
        required: ["object_path", "component", "property", "value"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_console_logs",
      description: "Read Unity console logs (errors, warnings). Use this to debug compilation errors or runtime issues.",
      parameters: {
        type: "object",
        properties: {
          log_type: {
            type: "string",
            description: "Filter by log type",
            enum: ["error", "warning", "log", "all"],
          },
          limit: {
            type: "number",
            description: "Max number of logs to return (default 50)",
          },
        },
        required: ["log_type"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "create_scriptable_object",
      description: "Create a ScriptableObject asset in Unity project",
      parameters: {
        type: "object",
        properties: {
          asset_path: {
            type: "string",
            description: "Path relative to Assets, e.g. 'Data/Items/Sword.asset'",
          },
          script_class: {
            type: "string",
            description: "The ScriptableObject class name, e.g. 'ItemData'",
          },
          properties: {
            type: "string",
            description: "JSON object with property values to set on the SO",
          },
        },
        required: ["asset_path", "script_class", "properties"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "execute_editor_command",
      description: "Execute an editor command like Play, Stop, Save, Refresh. Use after making changes to apply them.",
      parameters: {
        type: "object",
        properties: {
          command: {
            type: "string",
            description: "Command to execute",
            enum: ["play", "stop", "save", "refresh", "compile"],
          },
        },
        required: ["command"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "create_game_object",
      description: "Create a GameObject in the current scene with components. Use this to spawn characters, enemies, items, etc. directly in the scene.",
      parameters: {
        type: "object",
        properties: {
          name: {
            type: "string",
            description: "Name of the GameObject, e.g. 'Player', 'Enemy', 'Coin'",
          },
          position: {
            type: "string",
            description: "JSON position, e.g. '{\"x\":0,\"y\":1,\"z\":0}'",
          },
          rotation: {
            type: "string",
            description: "JSON rotation, e.g. '{\"x\":0,\"y\":0,\"z\":0}'",
          },
          scale: {
            type: "string",
            description: "JSON scale, e.g. '{\"x\":1,\"y\":1,\"z\":1}'",
          },
          components: {
            type: "string",
            description: "Comma-separated list of components to add, e.g. 'Rigidbody,CapsuleCollider,MeshRenderer,PlayerController'",
          },
          primitive: {
            type: "string",
            description: "Primitive mesh type for visual representation",
            enum: ["cube", "sphere", "capsule", "cylinder", "plane", "quad", "none"],
          },
          color: {
            type: "string",
            description: "Color for the object, e.g. 'red', 'blue', 'green', '#FF0000'",
          },
          parent: {
            type: "string",
            description: "Parent GameObject name (optional)",
          },
        },
        required: ["name", "components", "primitive"],
      },
    },
  },
];

// Tool execution handlers (server-side logic)
export async function executeToolCall(
  toolName: string,
  args: Record<string, unknown>,
  projectId: string,
  db: any
): Promise<string> {
  const { pendingCommands, projectFiles, consoleLogs } = await import("@/db/schema");
  const { eq, desc } = await import("drizzle-orm");

  switch (toolName) {
    case "create_script":
    case "modify_script": {
      // Add command to queue
      const [cmd] = await db
        .insert(pendingCommands)
        .values({
          projectId,
          commandType: toolName,
          payload: {
            file_path: args.file_path,
            content: args.content,
          },
        })
        .returning({ id: pendingCommands.id });

      // Also update file index
      await db
        .insert(projectFiles)
        .values({
          projectId,
          filePath: args.file_path as string,
          fileType: "script",
          content: args.content as string,
        })
        .onConflictDoUpdate({
          target: [projectFiles.projectId, projectFiles.filePath],
          set: {
            content: args.content as string,
            lastSynced: new Date(),
          },
        });

      return JSON.stringify({
        success: true,
        commandId: cmd.id,
        message: `${toolName === "create_script" ? "Created" : "Modified"} script: ${args.file_path}`,
      });
    }

    case "read_script": {
      const files = await db
        .select()
        .from(projectFiles)
        .where(eq(projectFiles.filePath, args.file_path as string))
        .limit(1);

      if (files.length === 0) {
        return JSON.stringify({
          success: false,
          error: `File not found: ${args.file_path}`,
        });
      }

      return JSON.stringify({
        success: true,
        file_path: files[0].filePath,
        content: files[0].content,
      });
    }

    case "list_project_files": {
      const filterType = args.filter_type as string;
      let query = db
        .select({
          filePath: projectFiles.filePath,
          fileType: projectFiles.fileType,
          lastSynced: projectFiles.lastSynced,
        })
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));

      if (filterType && filterType !== "all") {
        const files = await db
          .select({
            filePath: projectFiles.filePath,
            fileType: projectFiles.fileType,
            lastSynced: projectFiles.lastSynced,
          })
          .from(projectFiles)
          .where(
            eq(projectFiles.projectId, projectId)
          );

        const filtered = files.filter(
          (f: any) => f.fileType === filterType
        );

        return JSON.stringify({
          success: true,
          files: filtered,
          total: filtered.length,
        });
      }

      const files = await db
        .select({
          filePath: projectFiles.filePath,
          fileType: projectFiles.fileType,
          lastSynced: projectFiles.lastSynced,
        })
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));

      return JSON.stringify({
        success: true,
        files,
        total: files.length,
      });
    }

    case "set_object_property": {
      const [cmd] = await db
        .insert(pendingCommands)
        .values({
          projectId,
          commandType: "set_object_property",
          payload: {
            object_path: args.object_path,
            component: args.component,
            property: args.property,
            value: args.value,
          },
        })
        .returning({ id: pendingCommands.id });

      return JSON.stringify({
        success: true,
        commandId: cmd.id,
        message: `Set ${args.component}.${args.property} on ${args.object_path}`,
      });
    }

    case "read_console_logs": {
      const logType = args.log_type as string;
      const limit = (args.limit as number) || 50;

      let query = db
        .select()
        .from(consoleLogs)
        .where(eq(consoleLogs.projectId, projectId))
        .orderBy(desc(consoleLogs.timestamp))
        .limit(limit);

      if (logType && logType !== "all") {
        const logs = await db
          .select()
          .from(consoleLogs)
          .where(eq(consoleLogs.projectId, projectId))
          .orderBy(desc(consoleLogs.timestamp))
          .limit(limit);

        const filtered = logs.filter(
          (l: any) => l.logType === logType
        );

        return JSON.stringify({
          success: true,
          logs: filtered,
          total: filtered.length,
        });
      }

      const logs = await db
        .select()
        .from(consoleLogs)
        .where(eq(consoleLogs.projectId, projectId))
        .orderBy(desc(consoleLogs.timestamp))
        .limit(limit);

      return JSON.stringify({
        success: true,
        logs,
        total: logs.length,
      });
    }

    case "create_scriptable_object": {
      const [cmd] = await db
        .insert(pendingCommands)
        .values({
          projectId,
          commandType: "create_scriptable_object",
          payload: {
            asset_path: args.asset_path,
            script_class: args.script_class,
            properties: args.properties,
          },
        })
        .returning({ id: pendingCommands.id });

      return JSON.stringify({
        success: true,
        commandId: cmd.id,
        message: `Created ScriptableObject: ${args.asset_path}`,
      });
    }

    case "execute_editor_command": {
      const [cmd] = await db
        .insert(pendingCommands)
        .values({
          projectId,
          commandType: "execute_editor_command",
          payload: {
            command: args.command,
          },
        })
        .returning({ id: pendingCommands.id });

      return JSON.stringify({
        success: true,
        commandId: cmd.id,
        message: `Executed editor command: ${args.command}`,
      });
    }

    case "create_game_object": {
      const [cmd] = await db
        .insert(pendingCommands)
        .values({
          projectId,
          commandType: "create_game_object",
          payload: {
            name: args.name,
            position: args.position || '{"x":0,"y":0,"z":0}',
            rotation: args.rotation || '{"x":0,"y":0,"z":0}',
            scale: args.scale || '{"x":1,"y":1,"z":1}',
            components: args.components,
            primitive: args.primitive,
            color: args.color || 'white',
            parent: args.parent,
          },
        })
        .returning({ id: pendingCommands.id });

      return JSON.stringify({
        success: true,
        commandId: cmd.id,
        message: `Created GameObject: ${args.name} with ${args.primitive} mesh`,
      });
    }

    default:
      return JSON.stringify({
        success: false,
        error: `Unknown tool: ${toolName}`,
      });
  }
}
