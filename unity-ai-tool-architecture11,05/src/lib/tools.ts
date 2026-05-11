// Tool definitions for OpenAI function calling
// These map directly to Unity commands the plugin can execute

export const UNITY_TOOLS = [
  {
    type: "function" as const,
    function: {
      name: "list_project_files",
      description:
        "Get a list of all files in the Unity project with their paths and types. Use this first to understand the project structure before making changes.",
      parameters: {
        type: "object",
        properties: {
          filter_type: {
            type: "string",
            enum: ["all", "scripts", "scenes", "prefabs", "shaders", "materials", "configs"],
            description: "Filter by file type",
          },
          search: {
            type: "string",
            description: "Optional search term to filter filenames",
          },
        },
        required: [],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "read_file",
      description:
        "Read the full content of any file in the Unity project. Use this to understand existing code before modifying it. Always read scripts before editing them.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path to the file, e.g. Assets/Scripts/EnemyAI.cs",
          },
        },
        required: ["path"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "write_file",
      description:
        "Create or completely overwrite a file in the Unity project. Use for new scripts, modified scripts, shaders, configs etc. The Unity plugin will automatically refresh assets after writing.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path, e.g. Assets/Scripts/PlayerController.cs",
          },
          content: {
            type: "string",
            description: "Full file content to write",
          },
        },
        required: ["path", "content"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "create_gameobject",
      description:
        "Create a new GameObject in the active Unity scene with specified components.",
      parameters: {
        type: "object",
        properties: {
          name: {
            type: "string",
            description: "Name for the new GameObject",
          },
          primitive: {
            type: "string",
            enum: ["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad", "Empty"],
            description: "Primitive type or Empty for empty GameObject",
          },
          components: {
            type: "string",
            description: "Comma-separated list of component types to add, e.g. 'Rigidbody,BoxCollider,MyScript'",
          },
          position: {
            type: "string",
            description: "Position as 'x,y,z', e.g. '0,1,0'",
          },
          color: {
            type: "string",
            description: "Material color as 'r,g,b' values 0-1, e.g. '1,0,0' for red",
          },
          parent: {
            type: "string",
            description: "Name of parent GameObject (optional)",
          },
        },
        required: ["name"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "add_component",
      description:
        "Add one or more components to an existing GameObject in the scene.",
      parameters: {
        type: "object",
        properties: {
          name: {
            type: "string",
            description: "Name of the existing GameObject in the scene",
          },
          components: {
            type: "string",
            description: "Comma-separated component type names to add, e.g. 'Rigidbody,NavMeshAgent'",
          },
        },
        required: ["name", "components"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "execute_editor_command",
      description:
        "Execute a Unity Editor command like Play, Stop, Save, Refresh assets, or show a message.",
      parameters: {
        type: "object",
        properties: {
          command: {
            type: "string",
            enum: ["play", "stop", "pause", "save", "refresh", "message"],
            description: "Editor command to execute",
          },
          message: {
            type: "string",
            description: "Message to show (only for 'message' command)",
          },
        },
        required: ["command"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "read_console_logs",
      description:
        "Read recent Unity console logs including errors, warnings and exceptions. ALWAYS use this after writing code to check for compilation errors and fix them automatically.",
      parameters: {
        type: "object",
        properties: {
          filter_type: {
            type: "string",
            enum: ["all", "error", "warning", "log", "exception"],
            description: "Filter by log type",
          },
          limit: {
            type: "number",
            description: "Max number of logs to return (default 30)",
          },
        },
        required: [],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "get_scene_hierarchy",
      description:
        "Get the full scene hierarchy showing all GameObjects and their components. Use this to understand the current scene structure.",
      parameters: {
        type: "object",
        properties: {},
        required: [],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "search_in_files",
      description:
        "Search for a specific text pattern across all project files. Useful for finding where a class, method or variable is used.",
      parameters: {
        type: "object",
        properties: {
          query: {
            type: "string",
            description: "Text to search for",
          },
          file_type: {
            type: "string",
            description: "Optional file extension to limit search, e.g. '.cs'",
          },
        },
        required: ["query"],
      },
    },
  },
  {
    type: "function" as const,
    function: {
      name: "delete_file",
      description: "Delete a file from the Unity project.",
      parameters: {
        type: "object",
        properties: {
          path: {
            type: "string",
            description: "Relative path to the file to delete",
          },
        },
        required: ["path"],
      },
    },
  },
];

export type ToolName = 
  | "list_project_files"
  | "read_file"
  | "write_file"
  | "create_gameobject"
  | "add_component"
  | "execute_editor_command"
  | "read_console_logs"
  | "get_scene_hierarchy"
  | "search_in_files"
  | "delete_file";
