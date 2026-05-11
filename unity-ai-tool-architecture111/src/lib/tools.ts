import type { ChatCompletionTool } from "openai/resources/chat/completions";

// ─── All Tool Definitions for OpenAI Function Calling ────────────────────────
export const UNITY_TOOLS: ChatCompletionTool[] = [
  {
    type: "function",
    function: {
      name: "list_project_files",
      description:
        "List all files in the Unity project. Returns paths, types, and sizes. Use this first to understand what exists in the project before reading or modifying files.",
      parameters: {
        type: "object",
        properties: {
          filter_type: {
            type: "string",
            enum: ["all", "script", "scene", "prefab", "material", "shader", "config", "other"],
            description: "Filter files by type. Use 'all' to see everything.",
          },
          search: {
            type: "string",
            description: "Optional substring to filter file paths (e.g. 'Player', 'Enemy', 'UI')",
          },
        },
        required: ["filter_type"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_file",
      description:
        "Read the full content of any file in the Unity project. Essential for understanding existing code before modifying it.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path to the file, e.g. 'Assets/Scripts/PlayerController.cs'",
          },
        },
        required: ["path"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "write_file",
      description:
        "Create or overwrite a file in the Unity project. Use for creating new scripts, modifying existing ones, creating prefab YAML, shader files, etc. Always read the file first if modifying.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path, e.g. 'Assets/Scripts/EnemyAI.cs' or 'Assets/Scripts/Inventory/Item.cs'",
          },
          content: {
            type: "string",
            description: "Full file content to write",
          },
          description: {
            type: "string",
            description: "Brief description of what this file does / what changed",
          },
        },
        required: ["path", "content", "description"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "delete_file",
      description: "Delete a file from the Unity project. Use with caution.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path to delete, e.g. 'Assets/Scripts/OldScript.cs'",
          },
        },
        required: ["path"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "create_gameobject",
      description:
        "Create a new GameObject in the currently open Unity scene. Can create primitives (Cube, Sphere, Capsule, Cylinder, Plane, Quad) or empty GameObjects.",
      parameters: {
        type: "object",
        properties: {
          name: {
            type: "string",
            description: "Name of the GameObject",
          },
          primitive: {
            type: "string",
            enum: ["empty", "cube", "sphere", "capsule", "cylinder", "plane", "quad"],
            description: "Primitive type. Use 'empty' for an empty GameObject.",
          },
          position: {
            type: "string",
            description: "Position as 'x,y,z', e.g. '0,1,0'. Defaults to origin.",
          },
          components: {
            type: "string",
            description: "Comma-separated list of component class names to add, e.g. 'Rigidbody,BoxCollider'",
          },
          parent: {
            type: "string",
            description: "Name of parent GameObject in scene hierarchy (optional)",
          },
          color: {
            type: "string",
            description: "For primitives: set material color as 'r,g,b' (0-1 range), e.g. '1,0,0' for red",
          },
        },
        required: ["name", "primitive"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "add_component",
      description:
        "Add a component to an existing GameObject in the scene by name.",
      parameters: {
        type: "object",
        properties: {
          gameobject_name: {
            type: "string",
            description: "Exact name of the GameObject in the hierarchy",
          },
          component: {
            type: "string",
            description: "Component class name, e.g. 'Rigidbody', 'AudioSource', 'NavMeshAgent', 'PlayerController'",
          },
        },
        required: ["gameobject_name", "component"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_console_logs",
      description:
        "Read Unity console logs (errors, warnings, compiler errors). Call this after writing files to check for compilation errors and fix them automatically.",
      parameters: {
        type: "object",
        properties: {
          log_type: {
            type: "string",
            enum: ["all", "error", "warning", "log", "compiler_error"],
            description: "Filter by log type. Use 'error' to check for compilation errors.",
          },
          limit: {
            type: "number",
            description: "Maximum number of logs to return. Default 20.",
          },
        },
        required: ["log_type"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "read_scene_hierarchy",
      description:
        "Read the current Unity scene hierarchy — all GameObjects and their components. Use to understand scene structure before modifying it.",
      parameters: {
        type: "object",
        properties: {},
        required: [],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "execute_editor_command",
      description:
        "Execute a Unity Editor command: play/stop the game, save the scene, refresh assets, or open a specific scene.",
      parameters: {
        type: "object",
        properties: {
          command: {
            type: "string",
            enum: ["play", "stop", "save_scene", "refresh_assets", "open_scene"],
            description: "The editor command to execute",
          },
          argument: {
            type: "string",
            description: "Optional argument. For 'open_scene': scene path like 'Assets/Scenes/Main.unity'",
          },
        },
        required: ["command"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "create_scriptable_object",
      description:
        "Create a ScriptableObject asset in the Unity project. First ensure the SO class script exists.",
      parameters: {
        type: "object",
        properties: {
          script_class: {
            type: "string",
            description: "The ScriptableObject class name, e.g. 'ItemData'",
          },
          asset_path: {
            type: "string",
            description: "Where to save the asset, e.g. 'Assets/Data/Items/Sword.asset'",
          },
        },
        required: ["script_class", "asset_path"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "search_in_files",
      description:
        "Search for a string or pattern across all project files. Useful for finding all usages of a class, method, or variable.",
      parameters: {
        type: "object",
        properties: {
          query: {
            type: "string",
            description: "String to search for across all project files",
          },
          file_type: {
            type: "string",
            enum: ["all", "script", "scene", "prefab"],
            description: "Limit search to specific file types",
          },
        },
        required: ["query", "file_type"],
      },
    },
  },
];
