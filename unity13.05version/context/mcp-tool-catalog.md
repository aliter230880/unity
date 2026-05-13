# MCP tool catalog — Unity-MCP server (ivanmurzakdev v0.72.1)

Total: **82** tools. Snapshot taken 2026-05-13 from a connected `tps1` project on Unity 6.3 / 6000.3.9f1.

## `animation-*` (3)

### `animation-create`

Create Unity's Animation asset files (AnimationClip). Creates folders recursively if they do not
exist. Each path should start with 'Assets/' and end with '.anim'.

**Parameters:**

- `sourcePaths` (?, **required**) — The paths of the animation assets to create. Each path should start with 'Assets/' and end with '.anim'.

### `animation-get-data`

Get data about a Unity AnimationClip asset file. Returns information such as name, length, frame
rate, wrap mode, animation curves, and events.

**Parameters:**

- `animRef` (?, **required**) — Reference to the animation asset. The path should start with 'Assets/' and end with '.anim'.

### `animation-modify`

Modify Unity's AnimationClip asset. Apply an array of modifications including setting curves,
clearing curves, setting properties, and managing animation events. Use 'animation-get-data' tool to
get valid property names and existing curves for modifications.

**Parameters:**

- `animRef` (?, **required**) — Reference to the AnimationClip asset to modify.
- `modifications` (?, **required**) — Array of modifications to apply to the clip.

## `animator-*` (3)

### `animator-create`

Create Unity's AnimatorController asset files. Creates folders recursively if they do not exist.
Each path should start with 'Assets/' and end with '.controller'.

**Parameters:**

- `sourcePaths` (?, **required**) — The paths of the animator controller assets to create. Each path should start with 'Assets/' and end with '.controller'.

### `animator-get-data`

Get data about a Unity AnimatorController asset file. Returns information such as name, layers,
parameters, and states.

**Parameters:**

- `animatorRef` (?, **required**) — Reference to the AnimatorController asset. The path should start with 'Assets/' and end with '.controller'.

### `animator-modify`

Modify Unity's AnimatorController asset. Apply an array of modifications including adding/removing
parameters, layers, states, and transitions. Use 'animator-get-data' tool to get valid names and
parameters for modifications.

**Parameters:**

- `animatorRef` (?, **required**) — Reference to the AnimatorController asset to modify.
- `modifications` (?, **required**) — Array of modifications to apply to the controller.

## `assets-*` (17)

### `assets-copy`

Copy assets at given paths and store them at new paths. Does AssetDatabase.Refresh() at the end. Use
'assets-find' tool to find assets before copying.

**Parameters:**

- `sourcePaths` (?, **required**) — The paths of the assets to copy.
- `destinationPaths` (?, **required**) — The paths to store the copied assets.

### `assets-create-folder`

Creates a new folder in the specified parent folder. The parent folder string must start with the
'Assets' folder, and all folders within the parent folder string must already exist. For example,
when specifying 'Assets/ParentFolder1/ParentFolder2/', the new folder will be created in
'ParentFolder2' only if ParentFolder1 and ParentFolder2 already exist. Use it to organize scripts
and assets in the project. Does AssetDatabase.Refresh() at the end. Returns the GUID of the newly
created folder, if successful.

**Parameters:**

- `inputs` (?, **required**) — The paths for the folders to create.

### `assets-delete`

Delete the assets at paths from the project. Does AssetDatabase.Refresh() at the end. Use
'assets-find' tool to find assets before deleting.

**Parameters:**

- `paths` (?, **required**) — The paths of the assets

### `assets-find`

Search the asset database using the search filter string. Allows you to search for Assets. The
string argument can provide names, labels or types (classnames).

**Parameters:**

- `filter` (string, optional) — The filter string can contain search data. Could be empty. Name: Filter assets by their filename (without extension). Words separated by whitespace are treated as separate name searches. Labels (l:): 
- `searchInFolders` (?, optional) — The folders where the search will start. If null, the search will be performed in all folders.
- `maxResults` (integer, optional) — Maximum number of assets to return. If the number of found assets exceeds this limit, the result will be truncated.

### `assets-find-built-in`

Search the built-in assets of the Unity Editor located in the built-in resources:
Resources/unity_builtin_extra. Doesn't support GUIDs since built-in assets do not have them.

**Parameters:**

- `name` (string, optional) — The name of the asset to filter by.
- `type` (?, optional) — The type of the asset to filter by.
- `maxResults` (integer, optional) — Maximum number of assets to return. If the number of found assets exceeds this limit, the result will be truncated.

### `assets-get-data`

Get asset data from the asset file in the Unity project. It includes all serializable fields and
properties of the asset. Use 'assets-find' tool to find asset before using this tool. Path-scoped
reads (token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via
Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name
regex / max depth / type via Reflector.View. These two parameters are mutually exclusive — supply at
most one. When neither is supplied the full asset is serialized as before (backwards compatible).
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is
stripped.

**Parameters:**

- `assetRef` (?, **required**) — Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders.
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Mutually exclusive with 'viewQuery'.
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View — combines a starting Path, a case-insensitive NamePattern regex, MaxDepth, and an optional TypeFilter. Mutually exclusive with 'paths'.

### `assets-material-create`

Create new material asset with default parameters. Creates folders recursively if they do not exist.
Provide proper 'shaderName' - use 'assets-shader-list-all' tool to find available shaders.

**Parameters:**

- `assetPath` (string, **required**) — Asset path. Starts with 'Assets/'. Ends with '.mat'.
- `shaderName` (string, **required**) — Name of the shader that need to be used to create the material.

### `assets-modify`

Modify asset file in the project. Use 'assets-get-data' tool first to inspect the asset structure
before modifying. Not allowed to modify asset file in 'Packages/' folder. Please modify it in
'Assets/' folder. Three modification surfaces (use whichever fits the task): 1. 'content' — full
SerializedMember override (legacy, backwards compatible). 2. 'pathPatches' — list of {path, value}
pairs routed through Reflector.TryModifyAt. 3. 'jsonPatch' — JSON Merge Patch routed through
Reflector.TryPatch. When more than one is supplied they run in this order: jsonPatch → pathPatches →
content. At least one is required. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]',
'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `assetRef` (?, **required**) — Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders.
- `content` (?, optional) — Optional. The asset content. It overrides the existing asset content (legacy path).
- `pathPatches` (?, optional) — Optional. List of path-scoped patches routed through Reflector.TryModifyAt.
- `jsonPatch` (string, optional) — Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch.

### `assets-move`

Move the assets at paths in the project. Should be used for asset rename. Does
AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before moving.

**Parameters:**

- `sourcePaths` (?, **required**) — The paths of the assets to move.
- `destinationPaths` (?, **required**) — The paths of moved assets.

### `assets-prefab-close`

Close currently opened prefab. Use it when you are in prefab editing mode in Unity Editor. Use
'assets-prefab-open' tool to open a prefab first.

**Parameters:**

- `save` (boolean, optional) — True to save prefab. False to discard changes.

### `assets-prefab-create`

Create a prefab from a GameObject in the current active scene. The prefab will be saved in the
project assets at the specified path. Creates folders recursively if they do not exist. If the
source GameObject is already a prefab instance and 'connectGameObjectToPrefab' is true, a Prefab
Variant is created automatically. To create a Prefab Variant from an existing prefab asset, provide
'sourcePrefabAssetPath' instead of 'gameObjectRef'. Use 'gameobject-find' tool to find the target
GameObject first.

**Parameters:**

- `prefabAssetPath` (string, **required**) — Prefab asset path. Should be in the format 'Assets/Path/To/Prefab.prefab'.
- `gameObjectRef` (?, optional) — Reference to a scene GameObject to create the prefab from. If the GameObject is already a prefab instance, a Prefab Variant is created when 'connectGameObjectToPrefab' is true. Optional if 'sourcePref
- `sourcePrefabAssetPath` (string, optional) — Path to an existing prefab asset to create a Prefab Variant from (e.g. 'Assets/Prefabs/Base.prefab'). When provided, a temporary instance is created, saved as a Prefab Variant, and cleaned up. Optiona
- `connectGameObjectToPrefab` (boolean, optional) — If true, the scene GameObject will be connected to the new prefab (becoming a prefab instance). If the source is already a prefab instance, this creates a Prefab Variant. If false, the prefab asset is

### `assets-prefab-instantiate`

Instantiates prefab in the current active scene. Use 'assets-find' tool to find prefab assets in the
project.

**Parameters:**

- `prefabAssetPath` (string, **required**) — Prefab asset path.
- `gameObjectPath` (string, **required**) — GameObject path in the current active scene.
- `position` (?, optional) — Transform position of the GameObject.
- `rotation` (?, optional) — Transform rotation of the GameObject. Euler angles in degrees.
- `scale` (?, optional) — Transform scale of the GameObject.
- `isLocalSpace` (boolean, optional) — World or Local space of transform.

### `assets-prefab-open`

Open prefab edit mode for a specific GameObject. In the Edit mode you can modify the prefab. The
modification will be applied to all instances of the prefab across the project. Note: Please use
'assets-prefab-close' tool later to exit prefab editing mode.

**Parameters:**

- `gameObjectRef` (?, **required**) — GameObject that represents prefab instance of an original prefab GameObject.

### `assets-prefab-save`

Save a prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open'
tool to open a prefab first.

**Parameters:**

- `nothing` (string, optional) — 

### `assets-refresh`

Refreshes the AssetDatabase. Use it if any file was added or updated in the project outside of Unity
API. Use it if need to force scripts recompilation when '.cs' file changed.

**Parameters:**

- `options` (?, optional) — Asset import options.

### `assets-shader-get-data`

Get detailed data about a shader asset in the Unity project. Returns shader properties, subshaders,
passes, compilation errors, and supported status. Use 'assets-find' tool with filter 't:Shader' to
find shaders, or 'assets-shader-list-all' tool to list all shader names. Path-scoped reads
(token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via
Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name
regex / max depth / type via Reflector.View. The result populates 'View' on the returned ShaderData.
These two parameters are mutually exclusive. Path syntax: 'fieldName', 'nested/field',
'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `assetRef` (?, **required**) — Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders.
- `includeMessages` (?, optional) — Include compilation error and warning messages. Default: true
- `includeProperties` (?, optional) — Include shader properties (uniforms) list. Default: false
- `includeSubshaders` (?, optional) — Include subshader and pass structure. Default: false
- `includeSourceCode` (?, optional) — Include pass source code in subshader data. Requires 'includeSubshaders' to be true. Can produce very large responses. Default: false
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt against the underlying Shader asset. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Mutually exclus
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View against the underlying Shader asset. Mutually exclusive with 'paths'.

### `assets-shader-list-all`

List all available shaders in the project assets and packages. Returns their names. Use this to find
a shader name for 'assets-material-create' tool.

**Parameters:**

- `nothing` (string, optional) — 

## `console-*` (2)

### `console-clear-logs`

Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for
isolating errors related to a specific action by clearing logs before performing the action.

**Parameters:**

- `nothing` (string, optional) — 

### `console-get-logs`

Retrieves Unity Editor logs. Useful for debugging and monitoring Unity Editor activity.

**Parameters:**

- `maxEntries` (integer, optional) — Maximum number of log entries to return. Minimum: 1. Default: 100
- `logTypeFilter` (?, optional) — Filter by log type. 'null' means All.
- `includeStackTrace` (boolean, optional) — Include stack traces in the output. Default: false
- `lastMinutes` (integer, optional) — Return logs from the last N minutes. If 0, returns all available logs. Default: 0

## `editor-*` (4)

### `editor-application-get-state`

Returns available information about 'UnityEditor.EditorApplication'. Use it to get information about
the current state of the Unity Editor application. Such as: playmode, paused state, compilation
state, etc.

**Parameters:**

- `nothing` (string, optional) — 

### `editor-application-set-state`

Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use
'editor-application-get-state' tool to get the current state first.

**Parameters:**

- `isPlaying` (boolean, optional) — If true, the 'playmode' will be started. If false, the 'playmode' will be stopped.
- `isPaused` (boolean, optional) — If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed.

### `editor-selection-get`

Get information about the current Selection in the Unity Editor. Use 'editor-selection-set' tool to
set the selection.

**Parameters:**

- `includeGameObjects` (boolean, optional) — 
- `includeTransforms` (boolean, optional) — 
- `includeInstanceIDs` (boolean, optional) — 
- `includeAssetGUIDs` (boolean, optional) — 
- `includeActiveObject` (boolean, optional) — 
- `includeActiveTransform` (boolean, optional) — 

### `editor-selection-set`

Set the current Selection in the Unity Editor to the provided objects. Use 'editor-selection-get'
tool to get the current selection first.

**Parameters:**

- `select` (?, **required**) — 

## `gameobject-*` (11)

### `gameobject-component-add`

Add Component to GameObject in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the
target GameObject first. Use 'gameobject-component-list-all' tool to find the component type names
to add.

**Parameters:**

- `componentNames` (?, **required**) — Full name of the Component. It should include full namespace path and the class name.
- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.

### `gameobject-component-destroy`

Destroy one or many components from target GameObject. Can't destroy missed components. Use
'gameobject-find' tool to find the target GameObject and 'gameobject-component-get' to get component
details first.

**Parameters:**

- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.
- `destroyComponentRefs` (?, **required**) — Component reference array. Used to find Component at GameObject.

### `gameobject-component-get`

Get detailed information about a specific Component on a GameObject. Returns component type, enabled
state, and optionally serialized fields and properties. Use this to inspect component data before
modifying it. Use 'gameobject-find' tool to get the list of all components on the GameObject.
Path-scoped reads (token-saving): supply 'paths' (a list of paths) to read only the listed
fields/elements via Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree
and/or filter by name regex / max depth / type via Reflector.View. The result is returned in the
'View' field of the response. These two parameters are mutually exclusive — supply at most one. Path
syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.
- `componentRef` (?, **required**) — Component reference. Used to find a Component at GameObject.
- `includeFields` (boolean, optional) — Include serialized fields of the component.
- `includeProperties` (boolean, optional) — Include serialized properties of the component.
- `deepSerialization` (boolean, optional) — Performs deep serialization including all nested objects. Otherwise, only serializes top-level members.
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt. When supplied, the legacy 'Fields'/'Properties' lists are skipped and the result is returned in 'View'. Path syntax: 'fieldName', 
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View. When supplied, the legacy 'Fields'/'Properties' lists are skipped and the filtered subtree is returned in 'View'. Mutually exclusive with 'pa

### `gameobject-component-list-all`

List C# class names extended from UnityEngine.Component. Use this to find component type names for
'gameobject-component-add' tool. Results are paginated to avoid overwhelming responses.

**Parameters:**

- `search` (string, optional) — Substring for searching components. Could be empty.
- `page` (integer, optional) — Page number (0-based). Default is 0.
- `pageSize` (integer, optional) — Number of items per page. Default is 5. Max is 500.

### `gameobject-component-modify`

Modify a specific Component on a GameObject in opened Prefab or in a Scene. Allows direct
modification of component fields and properties without wrapping in GameObject structure. Use
'gameobject-component-get' first to inspect the component structure before modifying. Three
modification surfaces (use whichever fits the task): 1. 'componentDiff' — full SerializedMember diff
(legacy, backwards compatible). 2. 'pathPatches' — list of {path, value} pairs routed through
Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through
Reflector.TryPatch; multiple fields at any depth in a single call. When more than one is supplied
they run in this order: jsonPatch → pathPatches → componentDiff. At least one is required. Path
syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.
- `componentRef` (?, **required**) — Component reference. Used to find a Component at GameObject.
- `componentDiff` (?, optional) — Optional. The full component data to apply (legacy path). Should contain 'fields' and/or 'props' with the values to modify. Only include the fields/properties you want to change. Any unknown or invali
- `pathPatches` (?, optional) — Optional. List of path-scoped patches routed through Reflector.TryModifyAt. Each entry targets one field/element/entry by path. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[
- `jsonPatch` (string, optional) — Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. Allows multiple fields at any depth to be updated in a single call. Use '$type' for compatible-su

### `gameobject-create`

Create a new GameObject in opened Prefab or in a Scene. If needed - provide proper 'position',
'rotation' and 'scale' to reduce amount of operations.

**Parameters:**

- `name` (string, **required**) — Name of the new GameObject.
- `parentGameObjectRef` (?, optional) — Parent GameObject reference. If not provided, the GameObject will be created at the root of the scene or prefab.
- `position` (?, optional) — Transform position of the GameObject.
- `rotation` (?, optional) — Transform rotation of the GameObject. Euler angles in degrees.
- `scale` (?, optional) — Transform scale of the GameObject.
- `isLocalSpace` (boolean, optional) — World or Local space of transform.
- `primitiveType` (?, optional) — 

### `gameobject-destroy`

Destroy GameObject and all nested GameObjects recursively in opened Prefab or in a Scene. Use
'gameobject-find' tool to find the target GameObject first.

**Parameters:**

- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.

### `gameobject-duplicate`

Duplicate GameObjects in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target
GameObjects first.

**Parameters:**

- `gameObjectRefs` (?, **required**) — Array of GameObjects in opened Prefab or in the active Scene.

### `gameobject-find`

Finds specific GameObject by provided information in opened Prefab or in a Scene. First it looks for
the opened Prefab, if any Prefab is opened it looks only there ignoring a scene. If no opened Prefab
it looks into current active scene. Returns GameObject information and its children. Also, it
returns Components preview just for the target GameObject. Path-scoped reads (token-saving): supply
'paths' (a list of paths) to read only the listed fields/elements via Reflector.TryReadAt, or
'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name regex / max depth / type
via Reflector.View. When either is supplied, the result populates 'Data' on the returned
GameObjectData and overrides 'includeData' (which would otherwise produce a full recursive
serialization). These two parameters are mutually exclusive — supply at most one. Path syntax:
'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `gameObjectRef` (?, **required**) — Find GameObject in opened Prefab or in the active Scene.
- `includeData` (boolean, optional) — Include editable GameObject data (tag, layer, etc).
- `includeComponents` (boolean, optional) — Include attached components references.
- `includeBounds` (boolean, optional) — Include 3D bounds of the GameObject.
- `includeHierarchy` (boolean, optional) — Include hierarchy metadata.
- `hierarchyDepth` (integer, optional) — Determines the depth of the hierarchy to include. 0 - means only the target GameObject. 1 - means to include one layer below.
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt. When supplied, replaces 'includeData'-style full serialization with a path-scoped aggregate. Path syntax: 'fieldName', 'nested/fie
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View. When supplied, replaces 'includeData'-style full serialization with the filtered subtree. Mutually exclusive with 'paths'.

### `gameobject-modify`

Modify GameObject fields and properties in opened Prefab or in a Scene. You can modify multiple
GameObjects at once. Just provide the same number of GameObject references and SerializedMember
objects. Three modification surfaces (per GameObject — parallel arrays must have the same length as
gameObjectRefs): 1. 'gameObjectDiffs' — full SerializedMember diff per GameObject (legacy, backwards
compatible). 2. 'pathPatchesPerGameObject' — list of {path, value} patches per GameObject routed
through Reflector.TryModifyAt; atomic per-path modification. 3. 'jsonPatchesPerGameObject' — JSON
Merge Patch per GameObject routed through Reflector.TryPatch. When more than one is supplied for the
same GameObject they run in this order: jsonPatch → pathPatches → diff. At least one of the three is
required. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.

**Parameters:**

- `gameObjectRefs` (?, **required**) — Array of GameObjects in opened Prefab or in the active Scene.
- `gameObjectDiffs` (?, optional) — Optional. Each item in the array represents a GameObject modification of the 'gameObjectRefs' at the same index. Usually a GameObject is a container for components. Each component may have fields and 
- `pathPatchesPerGameObject` (?, optional) — Optional. Per-GameObject list of path-scoped patches routed through Reflector.TryModifyAt. Outer index aligns with 'gameObjectRefs'; inner list contains {path, value} entries. Pass null or omit for Ga
- `jsonPatchesPerGameObject` (?, optional) — Optional. Per-GameObject JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. Outer index aligns with 'gameObjectRefs'. Pass null or omit for GameObjects that s

### `gameobject-set-parent`

Set parent GameObject to list of GameObjects in opened Prefab or in a Scene. Use 'gameobject-find'
tool to find the target GameObjects first.

**Parameters:**

- `gameObjectRefs` (?, **required**) — List of references to the GameObjects to set new parent.
- `parentGameObjectRef` (?, **required**) — Reference to the parent GameObject.
- `worldPositionStays` (boolean, optional) — A boolean flag indicating whether the GameObject's world position should remain unchanged when setting its parent.

## `object-*` (2)

### `object-get-data`

Get data of the specified Unity Object. Returns serialized data of the object including its
properties and fields. If need to modify the data use 'object-modify' tool. Path-scoped reads
(token-saving): supply 'paths' (a list of paths) to read only the listed fields/elements via
Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate to a subtree and/or filter by name
regex / max depth / type via Reflector.View. These two parameters are mutually exclusive — supply at
most one. When neither is supplied the full object is serialized as before (backwards compatible).
Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is
stripped.

**Parameters:**

- `objectRef` (?, **required**) — Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object.
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt. Each path may target a different depth. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Mutually ex
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View — combines a starting Path, a case-insensitive NamePattern regex, MaxDepth, and an optional TypeFilter. Mutually exclusive with 'paths'.

### `object-modify`

Modify the specified Unity Object. Allows direct modification of object fields and properties. Use
'object-get-data' first to inspect the object structure before modifying. Three modification
surfaces (use whichever fits the task): 1. 'objectDiff' — full SerializedMember diff (legacy,
backwards compatible). 2. 'pathPatches' — list of {path, value} pairs routed through
Reflector.TryModifyAt; atomic per-path modification, multiple entries can target different depths.
3. 'jsonPatch' — a JSON Merge Patch (RFC 7396, extended with [i]/[key] notation) routed through
Reflector.TryPatch; multiple fields at any depth in a single call. When more than one is supplied
they run in this order: jsonPatch → pathPatches → objectDiff. At least one is required. Path syntax:
'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.

**Parameters:**

- `objectRef` (?, **required**) — Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object.
- `objectDiff` (?, optional) — Optional. The full object data to apply (legacy path). Should contain 'fields' and/or 'props' with the values to modify. Only include the fields/properties you want to change. Any unknown or invalid f
- `pathPatches` (?, optional) — Optional. List of path-scoped patches routed through Reflector.TryModifyAt.
- `jsonPatch` (string, optional) — Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch.

## `package-*` (4)

### `package-add`

Install a package from the Unity Package Manager registry, Git URL, or local path. This operation
modifies the project's manifest.json and triggers package resolution. Note: Package installation may
trigger a domain reload. The result will be sent after the reload completes. Use 'package-search'
tool to search for packages and 'package-list' to list installed packages.

**Parameters:**

- `packageId` (string, **required**) — The package ID to install. Formats: Package ID 'com.unity.textmeshpro' (installs latest compatible version), Package ID with version 'com.unity.textmeshpro@3.0.6', Git URL 'https://github.com/user/rep

### `package-list`

List all packages installed in the Unity project (UPM packages). Returns information about each
installed package including name, version, source, and description. Use this to check which packages
are currently installed before adding or removing packages.

**Parameters:**

- `sourceFilter` (string, optional) — Filter packages by source.
- `nameFilter` (string, optional) — Filter packages by name, display name, or description (case-insensitive). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description subst
- `directDependenciesOnly` (boolean, optional) — Include only direct dependencies (packages in manifest.json). If false, includes all resolved packages. Default: false

### `package-remove`

Remove (uninstall) a package from the Unity project. This removes the package from the project's
manifest.json and triggers package resolution. Note: Built-in packages and packages that are
dependencies of other installed packages cannot be removed. Note: Package removal may trigger a
domain reload. The result will be sent after the reload completes. Use 'package-list' tool to list
installed packages first.

**Parameters:**

- `packageId` (string, **required**) — The ID of the package to remove. Example: 'com.unity.textmeshpro'. Do not include version number.

### `package-search`

Search for packages in both Unity Package Manager registry and installed packages. Use this to find
packages by name before installing them. Returns available versions and installation status.
Searches both the Unity registry and locally installed packages (including Git, local, and embedded
sources). Results are prioritized: exact name match, exact display name match, name substring,
display name substring, description substring. Note: Online mode fetches exact matches from live
registry, then supplements with cached substring matches.

**Parameters:**

- `query` (string, **required**) — The package id, name, or description. Can be: Full package id 'com.unity.textmeshpro', Full package name 'TextMesh Pro', Partial name 'TextMesh' (will search in Unity registry and installed packages),
- `maxResults` (integer, optional) — Maximum number of results to return. Default: 10
- `offlineMode` (boolean, optional) — Whether to perform the search in offline mode (uses cached registry data only). Default: true. Set to false to fetch latest exact matches from Unity registry.

## `particle-*` (2)

### `particle-system-get`

Get detailed information about a ParticleSystem component on a GameObject. Returns particle system
state and optionally serialized data for each module. Use the boolean flags to request specific
modules. Use this to inspect ParticleSystem data before modifying it.

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject containing the ParticleSystem component.
- `componentRef` (?, optional) — Optional reference to a specific ParticleSystem component if the GameObject has multiple. If not provided, uses the first ParticleSystem found.
- `includeMain` (boolean, optional) — Include Main module data (duration, looping, prewarm, startDelay, startLifetime, startSpeed, startSize, startRotation, startColor, gravityModifier, simulationSpace, scalingMode, playOnAwake, maxPartic
- `includeEmission` (boolean, optional) — Include Emission module data (rateOverTime, rateOverDistance, bursts).
- `includeShape` (boolean, optional) — Include Shape module data (shapeType, radius, angle, arc, position, rotation, scale, mesh, texture, etc.).
- `includeVelocityOverLifetime` (boolean, optional) — Include Velocity over Lifetime module data (x, y, z, space, orbital, radial, speedModifier).
- `includeLimitVelocityOverLifetime` (boolean, optional) — Include Limit Velocity over Lifetime module data (limit, dampen, separateAxes, drag).
- `includeInheritVelocity` (boolean, optional) — Include Inherit Velocity module data (mode, curve).
- `includeLifetimeByEmitterSpeed` (boolean, optional) — Include Lifetime by Emitter Speed module data (curve, range).
- `includeForceOverLifetime` (boolean, optional) — Include Force over Lifetime module data (x, y, z, space, randomized).
- `includeColorOverLifetime` (boolean, optional) — Include Color over Lifetime module data (color gradient).
- `includeColorBySpeed` (boolean, optional) — Include Color by Speed module data (color, range).
- `includeSizeOverLifetime` (boolean, optional) — Include Size over Lifetime module data (size curve, separateAxes).
- `includeSizeBySpeed` (boolean, optional) — Include Size by Speed module data (size, range).
- `includeRotationOverLifetime` (boolean, optional) — Include Rotation over Lifetime module data (angular velocity, separateAxes).
- `includeRotationBySpeed` (boolean, optional) — Include Rotation by Speed module data (angular velocity, range).
- `includeExternalForces` (boolean, optional) — Include External Forces module data (multiplier, influenceFilter).
- `includeNoise` (boolean, optional) — Include Noise module data (strength, frequency, scrollSpeed, damping, octaves, quality, remap).
- `includeCollision` (boolean, optional) — Include Collision module data (type, mode, planes, dampen, bounce, lifetimeLoss).
- `includeTrigger` (boolean, optional) — Include Trigger module data (inside, outside, enter, exit actions).
- `includeSubEmitters` (boolean, optional) — Include Sub Emitters module data (birth, collision, death, trigger, manual emitters).
- `includeTextureSheetAnimation` (boolean, optional) — Include Texture Sheet Animation module data (mode, tiles, animation, frameOverTime).
- `includeLights` (boolean, optional) — Include Lights module data (ratio, light, color, range, intensity).
- `includeTrails` (boolean, optional) — Include Trails module data (mode, ratio, lifetime, width, color).
- `includeCustomData` (boolean, optional) — Include Custom Data module data (modes, vectors, colors).
- `includeRenderer` (boolean, optional) — Include Renderer module data (renderMode, material, sortMode, alignment, shadows).
- `includeAll` (boolean, optional) — Include ALL modules data. Overrides individual flags.
- `deepSerialization` (boolean, optional) — Performs deep serialization including all nested objects. Otherwise, only serializes top-level members.

### `particle-system-modify`

Modify a ParticleSystem component on a GameObject. Provide the data model with only the modules you
want to change. Use 'particle-system-get' first to inspect the ParticleSystem structure before
modifying. Only include the modules and properties you want to change.

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject containing the ParticleSystem component.
- `componentRef` (?, optional) — Optional reference to a specific ParticleSystem component if the GameObject has multiple. If not provided, uses the first ParticleSystem found.
- `main` (?, optional) — Main module data to apply. Only include properties you want to change.
- `emission` (?, optional) — Emission module data to apply. Only include properties you want to change.
- `shape` (?, optional) — Shape module data to apply. Only include properties you want to change.
- `velocityOverLifetime` (?, optional) — Velocity over Lifetime module data to apply. Only include properties you want to change.
- `limitVelocityOverLifetime` (?, optional) — Limit Velocity over Lifetime module data to apply. Only include properties you want to change.
- `inheritVelocity` (?, optional) — Inherit Velocity module data to apply. Only include properties you want to change.
- `lifetimeByEmitterSpeed` (?, optional) — Lifetime by Emitter Speed module data to apply. Only include properties you want to change.
- `forceOverLifetime` (?, optional) — Force over Lifetime module data to apply. Only include properties you want to change.
- `colorOverLifetime` (?, optional) — Color over Lifetime module data to apply. Only include properties you want to change.
- `colorBySpeed` (?, optional) — Color by Speed module data to apply. Only include properties you want to change.
- `sizeOverLifetime` (?, optional) — Size over Lifetime module data to apply. Only include properties you want to change.
- `sizeBySpeed` (?, optional) — Size by Speed module data to apply. Only include properties you want to change.
- `rotationOverLifetime` (?, optional) — Rotation over Lifetime module data to apply. Only include properties you want to change.
- `rotationBySpeed` (?, optional) — Rotation by Speed module data to apply. Only include properties you want to change.
- `externalForces` (?, optional) — External Forces module data to apply. Only include properties you want to change.
- `noise` (?, optional) — Noise module data to apply. Only include properties you want to change.
- `collision` (?, optional) — Collision module data to apply. Only include properties you want to change.
- `trigger` (?, optional) — Trigger module data to apply. Only include properties you want to change.
- `subEmitters` (?, optional) — Sub Emitters module data to apply. Only include properties you want to change.
- `textureSheetAnimation` (?, optional) — Texture Sheet Animation module data to apply. Only include properties you want to change.
- `lights` (?, optional) — Lights module data to apply. Only include properties you want to change.
- `trails` (?, optional) — Trails module data to apply. Only include properties you want to change.
- `customData` (?, optional) — Custom Data module data to apply. Only include properties you want to change.
- `renderer` (?, optional) — Renderer module data to apply. Only include properties you want to change.

## `probuilder-*` (13)

### `probuilder-bevel`

Bevels selected edges of a ProBuilder mesh, creating chamfered corners. Use ProBuilder_GetMeshInfo
to identify edges by their vertex pairs. Beveling replaces sharp edges with angled faces for a
smoother appearance.

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `edges` (?, **required**) — Array of edge definitions. Each edge is defined by two vertex indices [vertexA, vertexB]. Example: [[0,1], [2,3]] bevels edges from vertex 0 to 1 and from vertex 2 to 3.
- `amount` (number, optional) — Bevel amount from 0 (no bevel) to 1 (maximum bevel reaching face center). Recommended values: 0.05 to 0.2.

### `probuilder-bridge`

Creates a new face connecting two edges. Useful for connecting separate parts of geometry or filling
gaps. Example: - edgeA=[0,1], edgeB=[4,5] creates a quad face between the two edges

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `edgeA` (?, **required**) — First edge as [vertexA, vertexB].
- `edgeB` (?, **required**) — Second edge as [vertexA, vertexB].
- `allowNonManifold` (boolean, optional) — If true, allows creation of non-manifold geometry (edges shared by more than 2 faces).

### `probuilder-connect-edges`

Inserts new edges connecting the midpoints of selected edges within faces. If a face has more than 2
edges to connect, a center vertex is added. This is useful for creating new edge loops and adding
geometry detail. Examples: - Connect opposite edges of top face: faceDirection="up" - Connect
specific edges: edges=[[0,1], [2,3]]

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `edges` (?, optional) — Array of edge definitions. Each edge is [vertexA, vertexB]. Use ProBuilder_GetMeshInfo to get vertex indices.
- `faceDirection` (?, optional) — Semantic face selection - connect edges of faces facing this direction.

### `probuilder-create-poly-shape`

Creates a 3D mesh from a 2D polygon outline. Perfect for: - Floor plans and room layouts - Custom
terrain patches - Architectural elements (walls, platforms) - Any shape that can be defined by a 2D
outline The polygon is defined by an array of 2D points (x,z coordinates) that form the outline. The
shape is then extruded upward by the specified height. Examples: - Rectangle: points=[[0,0], [4,0],
[4,3], [0,3]] height=2.5 - L-shape: points=[[0,0], [3,0], [3,2], [1,2], [1,3], [0,3]] height=3 -
Triangle: points=[[0,0], [2,0], [1,1.7]] height=1

**Parameters:**

- `points` (?, **required**) — 2D polygon points as [x,z] coordinates. Minimum 3 points. Points should be in clockwise or counter-clockwise order. Example: [[0,0], [4,0], [4,3], [0,3]] creates a 4x3 rectangle.
- `height` (number, optional) — Height to extrude the polygon upward. Default is 1.
- `name` (string, optional) — Name of the new GameObject.
- `parentGameObjectRef` (?, optional) — Parent GameObject reference. If not provided, the shape will be created at the root of the scene.
- `position` (?, optional) — Position of the shape in world or local space.
- `rotation` (?, optional) — Rotation of the shape in euler angles (degrees).
- `flipNormals` (boolean, optional) — If true, flip the normals so the faces point inward instead of outward.
- `isLocalSpace` (boolean, optional) — If true, position/rotation are in local space relative to parent.

### `probuilder-create-shape`

Creates a new ProBuilder mesh shape in the scene. ProBuilder shapes are editable 3D meshes that can
be modified using other ProBuilder tools like extrusion, beveling, etc.

**Parameters:**

- `shapeType` (string, **required**) — The type of shape to create.
- `name` (string, optional) — Name of the new GameObject.
- `parentGameObjectRef` (?, optional) — Parent GameObject reference. If not provided, the shape will be created at the root of the scene.
- `position` (?, optional) — Position of the shape in world or local space.
- `rotation` (?, optional) — Rotation of the shape in euler angles (degrees).
- `scale` (?, optional) — Scale of the shape.
- `size` (?, optional) — Size of the shape (width, height, depth). Default is (1, 1, 1).
- `isLocalSpace` (boolean, optional) — If true, position/rotation/scale are in local space relative to parent.

### `probuilder-delete-faces`

Deletes selected faces from a ProBuilder mesh. You can select faces by index OR by direction
(semantic selection). Deleting faces creates holes in the mesh or removes geometry entirely.
Examples: - Delete bottom face: faceDirection="down" - Delete specific faces: faceIndices=[0, 2, 4]

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `faceIndices` (?, optional) — Array of face indices to delete. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices.
- `faceDirection` (?, optional) — Semantic face selection by direction. Use this OR faceIndices, not both.

### `probuilder-extrude`

Extrudes selected faces of a ProBuilder mesh along their normals. You can select faces by index OR
by direction (semantic selection). Extrusion creates new geometry by pushing faces outward (or
inward with negative distance). Examples: - Extrude top face: faceDirection="up" - Extrude specific
faces: faceIndices=[0, 2, 4]

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `faceIndices` (?, optional) — Array of face indices to extrude. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices.
- `faceDirection` (?, optional) — Semantic face selection by direction. Use this OR faceIndices, not both.
- `distance` (number, optional) — Distance to extrude the faces. Positive values extrude outward along face normals, negative values extrude inward.
- `extrudeMethod` (string, optional) — Extrusion method: IndividualFaces (each face extrudes independently), FaceNormal (faces extrude as a group along averaged normal), VertexNormal (vertices move along their normals).

### `probuilder-flip-normals`

Reverses the normal direction of selected faces, flipping them inside-out. Useful for creating
interior spaces or fixing inverted faces. Examples: - Flip all faces: leave faceIndices and
faceDirection empty - Flip top face only: faceDirection=Up - Flip specific faces: faceIndices=[0, 2,
4]

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `faceIndices` (?, optional) — Array of face indices to flip. If empty and faceDirection is empty, flips all faces.
- `faceDirection` (?, optional) — Semantic face selection by direction. If empty and faceIndices is empty, flips all faces.

### `probuilder-get-mesh-info`

Retrieves information about a ProBuilder mesh including faces, vertices, and edges. Use
detail="summary" for a token-efficient overview showing face directions. Use detail="full" for
detailed face-by-face information. TIP: With semantic face selection (faceDirection parameter) in
Extrude/DeleteFaces/SetFaceMaterial, you often don't need GetMeshInfo at all - just use
faceDirection="up" etc. directly.

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `detail` (string, optional) — Detail level for output.
- `includeVertexPositions` (boolean, optional) — If true, includes detailed vertex positions for each face (only with detail='full').
- `includeEdges` (boolean, optional) — If true, includes edge information for each face (only with detail='full').
- `maxFacesToShow` (integer, optional) — Maximum number of faces to include in detail (only with detail='full'). Use -1 for all faces.

### `probuilder-merge-objects`

Combines multiple ProBuilder meshes into a single mesh. Useful for optimizing draw calls or creating
a unified object from parts. The first mesh in the list becomes the target that others merge into.
Example: Merge a table made of separate leg and top meshes into one object.

**Parameters:**

- `gameObjectRefs` (?, **required**) — Array of GameObject references with ProBuilderMesh components to merge. First object becomes the merge target.
- `deleteSourceObjects` (boolean, optional) — If true, delete the source GameObjects after merging (except the target). Default is true.

### `probuilder-set-face-material`

Assigns a material to specific faces of a ProBuilder mesh. You can select faces by index OR by
direction (semantic selection). This enables multi-material meshes where different faces have
different materials. Examples: - Set material on top face: faceDirection="up" - Set material on
specific faces: faceIndices=[0, 2, 4]

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `materialPath` (string, **required**) — Path to the material asset (e.g., 'Assets/Materials/MyMaterial.mat') or material name.
- `faceIndices` (?, optional) — Array of face indices to apply the material to. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices.
- `faceDirection` (?, optional) — Semantic face selection by direction. Use this OR faceIndices, not both.

### `probuilder-set-pivot`

Changes the pivot (origin) point of a ProBuilder mesh. The mesh geometry is adjusted so the pivot
moves without changing the visual position. Examples: - Center the pivot: pivotLocation=Center - Set
pivot to first vertex: pivotLocation=FirstVertex - Set custom pivot: pivotLocation=Custom,
customPosition=(0, 0, 0)

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `pivotLocation` (string, optional) — Where to place the pivot.
- `customPosition` (?, optional) — Custom world position for pivot (only used when pivotLocation=Custom).

### `probuilder-subdivide-edges`

Inserts new vertices on edges, subdividing them into smaller segments. Useful for adding detail to
specific edges for further manipulation. Examples: - Subdivide all edges of top face:
faceDirection="up", subdivisions=2 - Subdivide specific edges: edges=[[0,1], [2,3]], subdivisions=1

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the GameObject with a ProBuilderMesh component.
- `edges` (?, optional) — Array of edge definitions. Each edge is [vertexA, vertexB]. Use ProBuilder_GetMeshInfo to get vertex indices.
- `faceDirection` (?, optional) — Semantic face selection - subdivide all edges of faces facing this direction.
- `subdivisions` (integer, optional) — Number of subdivisions per edge. 1 = splits edge in half, 2 = splits into thirds, etc. Default is 1.

## `reflection-*` (2)

### `reflection-method-call`

Call C# method. Any method could be called, even private methods. It requires to receive proper
method schema. Use 'reflection-method-find' to find available method before using it. Receives input
parameters and returns result.

**Parameters:**

- `filter` (?, **required**) — Method reference. Used to find method in codebase of the project.
- `knownNamespace` (boolean, optional) — Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false.
- `typeNameMatchLevel` (integer, optional) — Minimal match level for 'typeName'. 0 - ignore 'filter.typeName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive
- `methodNameMatchLevel` (integer, optional) — Minimal match level for 'MethodName'. 0 - ignore 'filter.MethodName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensi
- `parametersMatchLevel` (integer, optional) — Minimal match level for 'Parameters'. 0 - ignore 'filter.Parameters', 1 - parameters count is the same, 2 - equals (default value).
- `targetObject` (?, optional) — Specify target object to call method on. Should be null if the method is static or if there is no specific target instance. New instance of the specified class will be created if the method is instanc
- `inputParameters` (?, optional) — Method input parameters. Per each parameter specify: type - full type name of the object to call method on, name - parameter name, value - serialized object value (it will be deserialized to the speci
- `executeInMainThread` (boolean, optional) — Set to true if the method should be executed in the main thread. Otherwise, set to false.

### `reflection-method-find`

Find method in the project using C# Reflection. It looks for all assemblies in the project and finds
method by its name, class name and parameters. Even private methods are available. Use
'reflection-method-call' to call the method after finding it.

**Parameters:**

- `filter` (?, **required**) — Method reference. Used to find method in codebase of the project.
- `knownNamespace` (boolean, optional) — Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false.
- `typeNameMatchLevel` (integer, optional) — Minimal match level for 'typeName'. 0 - ignore 'filter.typeName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensitive
- `methodNameMatchLevel` (integer, optional) — Minimal match level for 'MethodName'. 0 - ignore 'filter.MethodName', 1 - contains ignoring case (default value), 2 - contains case sensitive, 3 - starts with ignoring case, 4 - starts with case sensi
- `parametersMatchLevel` (integer, optional) — Minimal match level for 'Parameters'. 0 - ignore 'filter.Parameters' (default value), 1 - parameters count is the same, 2 - equals.

## `scene-*` (7)

### `scene-create`

Create new scene in the project assets. Use 'scene-list-opened' tool to list all opened scenes after
creation.

**Parameters:**

- `path` (string, **required**) — Path to the scene file. Should end with ".unity" extension.
- `newSceneSetup` (?, optional) — 
- `newSceneMode` (?, optional) — 

### `scene-get-data`

This tool retrieves the list of root GameObjects in the specified scene. Use 'scene-list-opened'
tool to get the list of all opened scenes. Path-scoped reads (token-saving): supply 'paths' (a list
of paths) to read only the listed fields/elements from the scene's root-GameObjects array via
Reflector.TryReadAt, or 'viewQuery' (a ViewQuery) to navigate/filter the same array via
Reflector.View. The result populates 'Data' on the returned SceneData. These two parameters are
mutually exclusive. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.
Leading '#/' is stripped. Example: paths=['[0]/name'] reads the name of the first root GameObject.

**Parameters:**

- `openedSceneName` (string, optional) — Name of the opened scene. If empty or null, the active scene will be used.
- `includeRootGameObjects` (boolean, optional) — If true, includes root GameObjects in the scene data.
- `includeChildrenDepth` (integer, optional) — Determines the depth of the hierarchy to include.
- `includeBounds` (boolean, optional) — If true, includes bounding box information for GameObjects.
- `includeData` (boolean, optional) — If true, includes component data for GameObjects.
- `paths` (?, optional) — Optional. List of paths to read individually via Reflector.TryReadAt against the scene's root-GameObjects array. Path syntax: 'fieldName', '[i]/field', '[i]/component/[j]/property'. Mutually exclusive
- `viewQuery` (?, optional) — Optional. View-query filter routed through Reflector.View on the scene's root-GameObjects array. Mutually exclusive with 'paths'.

### `scene-list-opened`

Returns the list of currently opened scenes in Unity Editor. Use 'scene-get-data' tool to get
detailed information about a specific scene.

**Parameters:**

- `nothing` (string, optional) — 

### `scene-open`

Open scene from the project asset file. Use 'assets-find' tool to find the scene asset first.

**Parameters:**

- `sceneRef` (?, **required**) — Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders.
- `loadSceneMode` (string, optional) — Open scene mode. Single: closes the current scenes and opens a new one. Additive: keeps the current scene and opens additional one.

### `scene-save`

Save Opened scene to the asset file. Use 'scene-list-opened' tool to get the list of all opened
scenes.

**Parameters:**

- `openedSceneName` (string, optional) — Name of the opened scene that should be saved. Could be empty if need to save the current active scene.
- `path` (string, optional) — Path to the scene file. Should end with ".unity". If null or empty save to the existed scene asset file.

### `scene-set-active`

Set the specified opened scene as the active scene. Use 'scene-list-opened' tool to get the list of
all opened scenes.

**Parameters:**

- `sceneRef` (?, **required**) — Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders.

### `scene-unload`

Unload scene from the Opened scenes in Unity Editor. Use 'scene-list-opened' tool to get the list of
all opened scenes.

**Parameters:**

- `name` (string, **required**) — Name of the loaded scene.

## `screenshot-*` (4)

### `screenshot-camera`

Captures a screenshot from a camera and returns it as an image. If no camera is specified, uses the
Main Camera. Returns the image directly for visual inspection by the LLM.

**Parameters:**

- `cameraRef` (?, optional) — Reference to the camera GameObject. If not specified, uses the Main Camera.
- `width` (integer, optional) — Width of the screenshot in pixels.
- `height` (integer, optional) — Height of the screenshot in pixels.

### `screenshot-game-view`

Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game
View's own render texture directly via the Unity Editor API. The image size matches the current Game
View resolution. Returns the image directly for visual inspection by the LLM.

**Parameters:**

- `nothing` (string, optional) — 

### `screenshot-isolated`

Renders a screenshot of a target GameObject with configurable isolation, background, camera angle,
and lighting. When isolated=true (default), only the target object is visible via layer-based
culling and inactive children of the target are temporarily activated for the render (their OnEnable
callbacks may fire — restored in finally, but side effects like audio/network/animation events are
not undoable). When isolated=false, the existing scene state is rendered as-is without activating
inactive objects. Supports custom multi-light setups via JSON. Returns a base64-encoded PNG.

**Parameters:**

- `gameObjectRef` (?, **required**) — Reference to the target GameObject (by instanceId, path, or name).
- `includeChildren` (?, optional) — Include child GameObjects in the render. Default: true.
- `isolated` (?, optional) — When true, renders only the target object using layer-based culling. When false, renders the full scene from the computed camera position. Default: true.
- `backgroundMode` (?, optional) — Background mode. Default: SolidColor.
- `backgroundColor` (string, optional) — Hex background color (e.g. '#404040'). Only used when backgroundMode is SolidColor.
- `cameraView` (?, optional) — Camera angle relative to the target object's bounding box. Default: Front.
- `fieldOfView` (?, optional) — Camera vertical field of view in degrees. Default: 60.
- `nearClipPlane` (?, optional) — Camera near clip plane distance. Default: 0.01.
- `farClipPlane` (?, optional) — Camera far clip plane distance. Default: 1000.
- `padding` (?, optional) — Framing multiplier around the object. 1.0 = tight fit, 1.5 = 50% extra space. Default: 1.2.
- `lights` (string, optional) — JSON array of light configurations. Each object defines type, color, intensity, rotation, position, range, spotAngle, shadows, etc. When null, a default white directional light at rotation (50,-30,0) 
- `resolution` (?, optional) — Output image resolution in pixels (width = height). Default: 512.

### `screenshot-scene-view`

Captures a screenshot from the Unity Editor Scene View and returns it as an image. Returns the image
directly for visual inspection by the LLM.

**Parameters:**

- `width` (integer, optional) — Width of the screenshot in pixels.
- `height` (integer, optional) — Height of the screenshot in pixels.

## `script-*` (4)

### `script-delete`

Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete
before reporting results. Use 'script-read' tool to read existing script files first.

**Parameters:**

- `files` (?, **required**) — File paths to the files. Sample: "Assets/Scripts/MyScript.cs".

### `script-execute`

Compiles and executes C# code dynamically using Roslyn. Supports two modes: full code mode (default)
requires a complete class definition, while body-only mode (isMethodBody=true) auto-generates the
boilerplate so you only provide the method body. Unity objects (GameObject, Component, etc.) can be
passed as parameters using their Ref types (GameObjectRef, ComponentRef, etc.) or directly by type.

**Parameters:**

- `csharpCode` (string, **required**) — C# code to compile and execute. In full code mode (default, isMethodBody=false): must define a complete class with a static method. Example: 'using UnityEngine; public class Script { public static voi
- `className` (string, optional) — The name of the class containing the method to execute. In body-only mode this becomes the generated class name.
- `methodName` (string, optional) — The name of the method to execute. Must be a static method. In body-only mode this becomes the generated method name.
- `parameters` (?, optional) — Serialized parameters to pass to the method. Each entry must specify 'name' and 'typeName'. Supported parameter types include primitives, strings, and Unity object references: - 'UnityEngine.GameObjec
- `isMethodBody` (boolean, optional) — When true, 'csharpCode' is treated as just the method body. The tool auto-generates standard using directives (System, UnityEngine, AIGD, com.IvanMurzak.Unity.MCP.Runtime.Extensions, UnityEditor), the

### `script-read`

Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to
update or create script files.

**Parameters:**

- `filePath` (string, **required**) — The path to the file. Sample: "Assets/Scripts/MyScript.cs".
- `lineFrom` (integer, optional) — The line number to start reading from (1-based).
- `lineTo` (integer, optional) — The line number to stop reading at (1-based, -1 for all lines).

### `script-update-or-create`

Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end.
Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read
existing script files first.

**Parameters:**

- `filePath` (string, **required**) — The path to the file. Sample: "Assets/Scripts/MyScript.cs".
- `content` (string, **required**) — C# code - content of the file.

## `tests-*` (1)

### `tests-run`

Execute Unity tests and return detailed results. Supports filtering by test mode, assembly,
namespace, class, and method. Recommended to use 'EditMode' for faster iteration during development.
Precondition: every open scene MUST be saved (no unsaved changes). If any open scene is dirty, this
tool throws an InvalidOperationException listing the dirty scenes; save them and retry.

**Parameters:**

- `testMode` (string, optional) — Test mode to run. Options: 'EditMode', 'PlayMode'. Default: 'EditMode'
- `testAssembly` (string, optional) — Specific test assembly name to run (optional). Example: 'Assembly-CSharp-Editor-testable'
- `testNamespace` (string, optional) — Specific test namespace to run (optional). Example: 'MyTestNamespace'
- `testClass` (string, optional) — Specific test class name to run (optional). Example: 'MyTestClass'
- `testMethod` (string, optional) — Specific fully qualified test method to run (optional). Example: 'MyTestNamespace.FixtureName.TestName'
- `includePassingTests` (boolean, optional) — Include details for all tests, both passing and failing (default: false). If you just need details for failing tests, set to false.
- `includeMessages` (boolean, optional) — Include test result messages in the test results (default: true). If you just need pass/fail status, set to false.
- `includeStacktrace` (boolean, optional) — Include stack traces in the test results (default: false).
- `includeLogs` (boolean, optional) — Include console logs in the test results (default: false).
- `logType` (string, optional) — Log type filter for console logs. Options: 'Log', 'Warning', 'Assert', 'Error', 'Exception'. (default: 'Warning')
- `includeLogsStacktrace` (boolean, optional) — Include stack traces for console logs in the test results (default: false). This is huge amount of data, use only if really needed.

## `tool-*` (2)

### `tool-list`

List all available MCP tools. Optionally filter by regex across tool names, descriptions, and
arguments.

**Parameters:**

- `regexSearch` (string, optional) — Regex pattern to filter tools. Matches against tool name, description, and argument names and descriptions.
- `includeDescription` (?, optional) — Include tool descriptions in the result. Default: false
- `includeInputs` (?, optional) — Include input arguments in the result. Default: None

### `tool-set-enabled-state`

Enable or disable MCP tools by name. Allows controlling which tools are available for the AI agent.

**Parameters:**

- `tools` (?, **required**) — Array of tools with their desired enabled state.
- `includeLogs` (?, optional) — Include operation logs in the result. Default: false

## `type-*` (1)

### `type-get-json-schema`

Generates a JSON Schema for a given C# type name using reflection. Supports primitives, enums,
arrays, generic collections, dictionaries, and complex objects. The type must be present in any
loaded assembly. Use the full type name (e.g. 'UnityEngine.Vector3') for best results.

**Parameters:**

- `typeName` (string, **required**) — Full C# type name to generate the schema for. Examples: 'System.String', 'UnityEngine.Vector3', 'System.Collections.Generic.List<System.Int32>'. Simple names like 'Vector3' are also accepted when unam
- `descriptionMode` (string, optional) — Controls the type-level 'description' field. Include: keep on the target type only. IncludeRecursively: keep on the target type and inside $defs entries. Ignore: strip all type-level descriptions. Def
- `propertyDescriptionMode` (string, optional) — Controls 'description' fields on properties, fields, and array items. Include: keep on the target type's own properties/items only. IncludeRecursively: keep on all properties/items including those ins
- `includeNestedTypes` (boolean, optional) — When true, complex nested types are extracted into '$defs' and referenced via '$ref' instead of being inlined. Useful for large or recursive types. Default: false.
- `writeIndented` (boolean, optional) — Whether to format the output JSON with indentation for readability. Default: false.

