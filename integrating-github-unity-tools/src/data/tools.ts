import { McpTool } from '../types';

export const MCP_TOOLS: McpTool[] = [
  // animation-*
  { name: 'animation-create', category: 'animation', description: 'Create Unity AnimationClip (.anim) asset files. Creates folders recursively.', params: ['sourcePaths'] },
  { name: 'animation-get-data', category: 'animation', description: 'Get data about a Unity AnimationClip: name, length, frame rate, wrap mode, curves, events.', params: ['animRef'] },
  { name: 'animation-modify', category: 'animation', description: 'Modify Unity AnimationClip: set/clear curves, properties, animation events.', params: ['animRef', 'modifications'] },

  // animator-*
  { name: 'animator-create', category: 'animator', description: 'Create Unity AnimatorController (.controller) asset files.', params: ['sourcePaths'] },
  { name: 'animator-get-data', category: 'animator', description: 'Get AnimatorController data: layers, parameters, states.', params: ['animatorRef'] },
  { name: 'animator-modify', category: 'animator', description: 'Modify AnimatorController: add/remove parameters, layers, states, transitions.', params: ['animatorRef', 'modifications'] },

  // assets-*
  { name: 'assets-copy', category: 'assets', description: 'Copy assets to new paths. Does AssetDatabase.Refresh() at the end.', params: ['sourcePaths', 'destinationPaths'] },
  { name: 'assets-create-folder', category: 'assets', description: 'Creates a new folder in the specified parent folder under Assets/.', params: ['inputs'] },
  { name: 'assets-delete', category: 'assets', description: 'Delete assets at paths from the project. Does AssetDatabase.Refresh().', params: ['paths'] },
  { name: 'assets-find', category: 'assets', description: 'Search the asset database by name, labels, or types (classnames).', params: ['filter', 'searchInFolders?', 'maxResults?'] },
  { name: 'assets-find-built-in', category: 'assets', description: 'Search Unity built-in assets in Resources/unity_builtin_extra.', params: ['name?', 'type?', 'maxResults?'] },
  { name: 'assets-get-data', category: 'assets', description: 'Get asset data including all serializable fields and properties.', params: ['assetRef', 'paths?', 'viewQuery?'] },
  { name: 'assets-material-create', category: 'assets', description: 'Create new material asset (.mat) with default parameters.', params: ['assetPath', 'shaderName'] },
  { name: 'assets-modify', category: 'assets', description: 'Modify asset file. Supports content, pathPatches, jsonPatch modes.', params: ['assetRef', 'content?', 'pathPatches?', 'jsonPatch?'] },
  { name: 'assets-move', category: 'assets', description: 'Move/rename assets. Does AssetDatabase.Refresh() at the end.', params: ['sourcePaths', 'destinationPaths'] },
  { name: 'assets-prefab-close', category: 'assets', description: 'Close currently opened prefab editing mode.', params: ['save?'] },
  { name: 'assets-prefab-create', category: 'assets', description: 'Create a prefab from a GameObject in the current active scene.', params: ['prefabAssetPath', 'gameObjectRef?', 'sourcePrefabAssetPath?'] },
  { name: 'assets-prefab-instantiate', category: 'assets', description: 'Instantiate prefab in the current active scene.', params: ['prefabAssetPath', 'gameObjectPath', 'position?', 'rotation?', 'scale?'] },
  { name: 'assets-prefab-open', category: 'assets', description: 'Open prefab edit mode for a specific GameObject.', params: ['gameObjectRef'] },
  { name: 'assets-prefab-save', category: 'assets', description: 'Save a prefab while in prefab editing mode.', params: ['nothing?'] },
  { name: 'assets-refresh', category: 'assets', description: 'Refresh AssetDatabase. Forces script recompilation if .cs file changed.', params: ['options?'] },
  { name: 'assets-shader-get-data', category: 'assets', description: 'Get detailed data about a shader: properties, subshaders, passes, errors.', params: ['assetRef', 'includeMessages?', 'includeProperties?'] },
  { name: 'assets-shader-list-all', category: 'assets', description: 'List all available shaders in the project assets and packages.', params: ['nothing?'] },

  // console-*
  { name: 'console-clear-logs', category: 'console', description: 'Clears the MCP log cache and the Unity Editor Console window.', params: ['nothing?'] },
  { name: 'console-get-logs', category: 'console', description: 'Retrieves Unity Editor logs. Filter by type, max entries, last N minutes.', params: ['maxEntries?', 'logTypeFilter?', 'includeStackTrace?', 'lastMinutes?'] },

  // editor-*
  { name: 'editor-application-get-state', category: 'editor', description: 'Returns info about EditorApplication: playmode, paused state, compilation state.', params: ['nothing?'] },
  { name: 'editor-application-set-state', category: 'editor', description: 'Control Unity Editor state: start/stop/pause playmode.', params: ['isPlaying?', 'isPaused?'] },
  { name: 'editor-selection-get', category: 'editor', description: 'Get information about current Selection in Unity Editor.', params: ['includeGameObjects?', 'includeTransforms?', 'includeInstanceIDs?'] },
  { name: 'editor-selection-set', category: 'editor', description: 'Set current Selection in Unity Editor to provided objects.', params: ['select'] },

  // gameobject-*
  { name: 'gameobject-component-add', category: 'gameobject', description: 'Add Component to GameObject in Prefab or Scene.', params: ['componentNames', 'gameObjectRef'] },
  { name: 'gameobject-component-destroy', category: 'gameobject', description: 'Destroy one or many components from target GameObject.', params: ['gameObjectRef', 'destroyComponentRefs'] },
  { name: 'gameobject-component-get', category: 'gameobject', description: 'Get detailed info about a specific Component on a GameObject.', params: ['gameObjectRef', 'componentRef', 'includeFields?', 'includeProperties?'] },
  { name: 'gameobject-component-list-all', category: 'gameobject', description: 'List C# class names extended from UnityEngine.Component. Paginated.', params: ['search?', 'page?', 'pageSize?'] },
  { name: 'gameobject-component-modify', category: 'gameobject', description: 'Modify a specific Component on a GameObject. Supports componentDiff, pathPatches, jsonPatch.', params: ['gameObjectRef', 'componentRef', 'componentDiff?', 'pathPatches?'] },
  { name: 'gameobject-create', category: 'gameobject', description: 'Create new empty GameObject or from primitive in a Scene or Prefab.', params: ['gameObjectPath', 'primitiveType?', 'position?', 'rotation?'] },
  { name: 'gameobject-destroy', category: 'gameobject', description: 'Destroy GameObjects from current active Scene or opened Prefab.', params: ['gameObjectRefs'] },
  { name: 'gameobject-find', category: 'gameobject', description: 'Find GameObjects by path, name, tag, layer, or component type.', params: ['gameObjectRef'] },
  { name: 'gameobject-get-data', category: 'gameobject', description: 'Get detailed info about a GameObject including components and children.', params: ['gameObjectRef', 'includeChildren?', 'includeComponents?'] },
  { name: 'gameobject-modify', category: 'gameobject', description: 'Modify GameObject properties: name, tag, layer, active state, transform.', params: ['gameObjectRef', 'modifications'] },
  { name: 'gameobject-set-parent', category: 'gameobject', description: 'Set parent of a GameObject. Supports worldPositionStays option.', params: ['gameObjectRef', 'parentRef?', 'worldPositionStays?'] },

  // package-*
  { name: 'package-add', category: 'package', description: 'Add a Unity package to the project by package name.', params: ['packageName'] },
  { name: 'package-list', category: 'package', description: 'List all installed packages in the Unity project.', params: ['nothing?'] },
  { name: 'package-remove', category: 'package', description: 'Remove a Unity package from the project.', params: ['packageName'] },
  { name: 'package-search', category: 'package', description: 'Search for Unity packages in the registry.', params: ['query'] },

  // probuilder-*
  { name: 'probuilder-create-shape', category: 'probuilder', description: 'Create a new ProBuilder shape (cube, sphere, cylinder, etc.).', params: ['gameObjectPath', 'shapeType', 'position?', 'rotation?', 'scale?'] },
  { name: 'probuilder-extrude-faces', category: 'probuilder', description: 'Extrude faces of a ProBuilder mesh by distance or direction.', params: ['gameObjectRef', 'faceIndices?', 'faceDirection?', 'extrudeType?'] },
  { name: 'probuilder-get-mesh-info', category: 'probuilder', description: 'Get information about a ProBuilder mesh: vertices, faces, edges.', params: ['gameObjectRef', 'detail?', 'includeEdges?'] },
  { name: 'probuilder-merge-objects', category: 'probuilder', description: 'Combine multiple ProBuilder meshes into a single mesh.', params: ['gameObjectRefs', 'deleteSourceObjects?'] },
  { name: 'probuilder-set-face-material', category: 'probuilder', description: 'Assign a material to specific faces of a ProBuilder mesh.', params: ['gameObjectRef', 'materialPath', 'faceIndices?', 'faceDirection?'] },
  { name: 'probuilder-set-pivot', category: 'probuilder', description: 'Change the pivot point of a ProBuilder mesh.', params: ['gameObjectRef', 'pivotLocation?', 'customPosition?'] },
  { name: 'probuilder-subdivide-edges', category: 'probuilder', description: 'Insert new vertices on edges, subdividing them into smaller segments.', params: ['gameObjectRef', 'edges?', 'faceDirection?', 'subdivisions?'] },

  // reflection-*
  { name: 'reflection-method-call', category: 'reflection', description: 'Call any C# method (even private) using Reflection.', params: ['filter', 'targetObject?', 'inputParameters?', 'executeInMainThread?'] },
  { name: 'reflection-method-find', category: 'reflection', description: 'Find C# method in all assemblies by name, class name, and parameters.', params: ['filter', 'typeNameMatchLevel?', 'methodNameMatchLevel?'] },

  // scene-*
  { name: 'scene-create', category: 'scene', description: 'Create new scene in project assets (.unity file).', params: ['path', 'newSceneSetup?', 'newSceneMode?'] },
  { name: 'scene-get-data', category: 'scene', description: 'Get list of root GameObjects in the specified scene with hierarchy.', params: ['openedSceneName?', 'includeRootGameObjects?', 'includeChildrenDepth?'] },
  { name: 'scene-list-opened', category: 'scene', description: 'Returns list of currently opened scenes in Unity Editor.', params: ['nothing?'] },
  { name: 'scene-open', category: 'scene', description: 'Open scene from the project asset file.', params: ['sceneRef', 'loadSceneMode?'] },
  { name: 'scene-save', category: 'scene', description: 'Save opened scene to the asset file.', params: ['openedSceneName?', 'path?'] },
  { name: 'scene-set-active', category: 'scene', description: 'Set the specified opened scene as the active scene.', params: ['sceneRef'] },
  { name: 'scene-unload', category: 'scene', description: 'Unload scene from opened scenes in Unity Editor.', params: ['name'] },

  // screenshot-*
  { name: 'screenshot-camera', category: 'screenshot', description: 'Capture screenshot from a camera (or Main Camera). Returns image.', params: ['cameraRef?', 'width?', 'height?'] },
  { name: 'screenshot-game-view', category: 'screenshot', description: 'Capture screenshot from Unity Editor Game View.', params: ['nothing?'] },
  { name: 'screenshot-isolated', category: 'screenshot', description: 'Render isolated screenshot of a target GameObject with configurable camera, lighting, background.', params: ['gameObjectRef', 'isolated?', 'backgroundMode?', 'cameraView?', 'resolution?'] },
  { name: 'screenshot-scene-view', category: 'screenshot', description: 'Capture screenshot from Unity Editor Scene View.', params: ['width?', 'height?'] },

  // script-*
  { name: 'script-delete', category: 'script', description: 'Delete script file(s). Does AssetDatabase.Refresh() and waits for compilation.', params: ['files'] },
  { name: 'script-execute', category: 'script', description: 'Compile and execute C# code dynamically using Roslyn. Supports full class or body-only mode.', params: ['csharpCode', 'className?', 'methodName?', 'parameters?', 'isMethodBody?'] },
  { name: 'script-read', category: 'script', description: 'Read content of a script file. Supports line range.', params: ['filePath', 'lineFrom?', 'lineTo?'] },
  { name: 'script-update-or-create', category: 'script', description: 'Update or create script file. Does AssetDatabase.Refresh(). Returns compilation errors.', params: ['filePath', 'content'] },

  // tests-*
  { name: 'tests-run', category: 'tests', description: 'Execute Unity tests with filtering by mode, assembly, namespace, class, method.', params: ['testMode?', 'testAssembly?', 'testClass?', 'testMethod?'] },

  // tool-*
  { name: 'tool-list', category: 'tool', description: 'List all available MCP tools. Filter by regex.', params: ['regexSearch?', 'includeDescription?', 'includeInputs?'] },
  { name: 'tool-set-enabled-state', category: 'tool', description: 'Enable or disable MCP tools by name.', params: ['tools', 'includeLogs?'] },

  // type-*
  { name: 'type-get-json-schema', category: 'type', description: 'Generate JSON Schema for a C# type using reflection.', params: ['typeName', 'descriptionMode?', 'includeNestedTypes?'] },
];

export const CATEGORIES = [
  { id: 'all', label: 'All Tools', count: MCP_TOOLS.length, color: 'bg-slate-600' },
  { id: 'animation', label: 'Animation', count: MCP_TOOLS.filter(t => t.category === 'animation').length, color: 'bg-purple-600' },
  { id: 'animator', label: 'Animator', count: MCP_TOOLS.filter(t => t.category === 'animator').length, color: 'bg-violet-600' },
  { id: 'assets', label: 'Assets', count: MCP_TOOLS.filter(t => t.category === 'assets').length, color: 'bg-blue-600' },
  { id: 'console', label: 'Console', count: MCP_TOOLS.filter(t => t.category === 'console').length, color: 'bg-red-600' },
  { id: 'editor', label: 'Editor', count: MCP_TOOLS.filter(t => t.category === 'editor').length, color: 'bg-orange-600' },
  { id: 'gameobject', label: 'GameObject', count: MCP_TOOLS.filter(t => t.category === 'gameobject').length, color: 'bg-green-600' },
  { id: 'package', label: 'Package', count: MCP_TOOLS.filter(t => t.category === 'package').length, color: 'bg-teal-600' },
  { id: 'probuilder', label: 'ProBuilder', count: MCP_TOOLS.filter(t => t.category === 'probuilder').length, color: 'bg-yellow-600' },
  { id: 'reflection', label: 'Reflection', count: MCP_TOOLS.filter(t => t.category === 'reflection').length, color: 'bg-pink-600' },
  { id: 'scene', label: 'Scene', count: MCP_TOOLS.filter(t => t.category === 'scene').length, color: 'bg-indigo-600' },
  { id: 'screenshot', label: 'Screenshot', count: MCP_TOOLS.filter(t => t.category === 'screenshot').length, color: 'bg-cyan-600' },
  { id: 'script', label: 'Script', count: MCP_TOOLS.filter(t => t.category === 'script').length, color: 'bg-emerald-600' },
  { id: 'tests', label: 'Tests', count: MCP_TOOLS.filter(t => t.category === 'tests').length, color: 'bg-rose-600' },
  { id: 'tool', label: 'Tool Meta', count: MCP_TOOLS.filter(t => t.category === 'tool').length, color: 'bg-gray-600' },
  { id: 'type', label: 'Type', count: MCP_TOOLS.filter(t => t.category === 'type').length, color: 'bg-lime-600' },
];

export const CATEGORY_COLORS: Record<string, string> = {
  animation: 'bg-purple-100 text-purple-700 border-purple-200',
  animator: 'bg-violet-100 text-violet-700 border-violet-200',
  assets: 'bg-blue-100 text-blue-700 border-blue-200',
  console: 'bg-red-100 text-red-700 border-red-200',
  editor: 'bg-orange-100 text-orange-700 border-orange-200',
  gameobject: 'bg-green-100 text-green-700 border-green-200',
  package: 'bg-teal-100 text-teal-700 border-teal-200',
  probuilder: 'bg-yellow-100 text-yellow-700 border-yellow-200',
  reflection: 'bg-pink-100 text-pink-700 border-pink-200',
  scene: 'bg-indigo-100 text-indigo-700 border-indigo-200',
  screenshot: 'bg-cyan-100 text-cyan-700 border-cyan-200',
  script: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  tests: 'bg-rose-100 text-rose-700 border-rose-200',
  tool: 'bg-gray-100 text-gray-700 border-gray-200',
  type: 'bg-lime-100 text-lime-700 border-lime-200',
};
