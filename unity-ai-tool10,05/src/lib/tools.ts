// AI Tool definitions for Unity Orchestrator (OpenAI function calling format)
import type { ChatCompletionTool } from "openai/resources/chat/completions";

export const UNITY_TOOLS: ChatCompletionTool[] = [
  {
    type: "function",
    function: {
      name: "create_script",
      description:
        "Creates a new C# script file in the Unity project. Use this to add new MonoBehaviours, ScriptableObjects, or utility classes.",
      parameters: {
        type: "object",
        properties: {
          name: {
            type: "string",
            description: "Script file name WITHOUT .cs extension. E.g. 'EnemyAI'",
          },
          folder: {
            type: "string",
            description:
              "Subfolder within Assets/Scripts. E.g. 'AI', 'Player', 'UI'. Leave empty for root Scripts folder.",
            default: "",
          },
          content: {
            type: "string",
            description: "Full C# source code of the script",
          },
          description: {
            type: "string",
            description: "Short description of what this script does (for user display)",
          },
        },
        required: ["name", "content", "description"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "modify_script",
      description:
        "Modifies an existing C# script in the Unity project. Provide the complete new content of the file.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Full path relative to Assets folder. E.g. 'Scripts/Player/PlayerController.cs'",
          },
          content: {
            type: "string",
            description: "Complete new content of the script (full file, not a diff)",
          },
          reason: {
            type: "string",
            description: "Explanation of what was changed and why",
          },
        },
        required: ["path", "content", "reason"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_script",
      description: "Reads the content of a specific script or file from the Unity project.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Path relative to Assets. E.g. 'Scripts/EnemyAI.cs'",
          },
        },
        required: ["path"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "list_project_files",
      description:
        "Lists all files in the Unity project index. Use this to understand the project structure before making changes.",
      parameters: {
        type: "object",
        properties: {
          filter_type: {
            type: "string",
            description: "Filter by file type: 'cs', 'scene', 'prefab', 'asset', or leave empty for all",
            default: "",
          },
        },
        required: [],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "set_object_property",
      description:
        "Sets a property of a GameObject or Component in a Unity scene or prefab. Used to configure values in the Inspector programmatically.",
      parameters: {
        type: "object",
        properties: {
          object_name: {
            type: "string",
            description: "Name of the GameObject in the hierarchy. E.g. 'Enemy', 'Player', 'Main Camera'",
          },
          component: {
            type: "string",
            description: "Component type name. E.g. 'Transform', 'Rigidbody', 'EnemyAI'",
          },
          property: {
            type: "string",
            description: "Property path. E.g. 'detectionRadius', 'transform.position.y', 'speed'",
          },
          value: {
            type: "string",
            description: "Value to set (serialized as string). Numbers, booleans, Vector3 as '(x,y,z)'",
          },
        },
        required: ["object_name", "component", "property", "value"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_console_logs",
      description:
        "Reads recent logs from the Unity console. Use this after applying code to check for compilation errors or runtime exceptions.",
      parameters: {
        type: "object",
        properties: {
          log_types: {
            type: "array",
            items: { type: "string" },
            description: "Types to filter: ['error', 'warning', 'log', 'exception']. Empty = all types.",
            default: ["error", "exception"],
          },
          limit: {
            type: "number",
            description: "Max number of logs to return (default 20)",
            default: 20,
          },
        },
        required: [],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "create_scriptable_object",
      description:
        "Creates a new ScriptableObject asset in the Unity project from an existing ScriptableObject class.",
      parameters: {
        type: "object",
        properties: {
          class_name: {
            type: "string",
            description: "The ScriptableObject class name",
          },
          asset_name: {
            type: "string",
            description: "Name for the created asset file",
          },
          folder: {
            type: "string",
            description: "Folder under Assets to save the asset",
            default: "ScriptableObjects",
          },
        },
        required: ["class_name", "asset_name"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "execute_editor_command",
      description:
        "Executes a Unity Editor command or menu item. For example: refresh assets, enter play mode, run tests.",
      parameters: {
        type: "object",
        properties: {
          command: {
            type: "string",
            description:
              "Command type: 'refresh_assets' | 'play_mode' | 'stop_play_mode' | 'save_scene' | 'run_tests'",
          },
        },
        required: ["command"],
      },
    },
  },
];

export type ToolName =
  | "create_script"
  | "modify_script"
  | "read_script"
  | "list_project_files"
  | "set_object_property"
  | "read_console_logs"
  | "create_scriptable_object"
  | "execute_editor_command";
