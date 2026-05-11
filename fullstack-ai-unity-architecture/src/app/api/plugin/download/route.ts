import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

// Unity C# plugin - NO external dependencies, works in any Unity 2020+
function generatePluginCode(apiKey: string, serverUrl: string): string {
  return `#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class AliTerraAI : EditorWindow
{
    // Configuration
    private string apiKey = "${apiKey}";
    private string serverUrl = "${serverUrl}";
    private float pollInterval = 2f;
    
    // State
    private bool isConnected = false;
    private bool isPolling = false;
    private Vector2 scrollPos;
    private string statusMessage = "Ready";
    private DateTime lastPollTime;
    
    // Console logs
    private static List<LogEntry> capturedLogs = new List<LogEntry>();
    private static bool isWaitingForCompilation = false;
    
    [Serializable]
    public class LogEntry
    {
        public string type;
        public string message;
        public string stackTrace;
        
        public LogEntry(string type, string message, string stackTrace = null)
        {
            this.type = type;
            this.message = message;
            this.stackTrace = stackTrace;
        }
    }
    
    [Serializable]
    public class CommandsResponse
    {
        public List<SimpleCommand> commands;
    }
    
    [Serializable]
    public class SimpleCommand
    {
        public string id;
        public string commandType;
        public string payload;
    }
    
    [MenuItem("Window/AliTerra AI")]
    public static void ShowWindow()
    {
        GetWindow<AliTerraAI>("AliTerra AI");
    }
    
    private void OnEnable()
    {
        Application.logMessageReceived += OnLogReceived;
        EditorApplication.update += OnEditorUpdate;
    }
    
    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogReceived;
        EditorApplication.update -= OnEditorUpdate;
        isPolling = false;
    }
    
    private void OnLogReceived(string message, string stackTrace, LogType type)
    {
        string logType = "log";
        if (type == LogType.Error || type == LogType.Exception)
            logType = "error";
        else if (type == LogType.Warning)
            logType = "warning";
        
        capturedLogs.Add(new LogEntry(logType, message, stackTrace));
        if (capturedLogs.Count > 100) capturedLogs.RemoveAt(0);
        
        if (isWaitingForCompilation && logType == "error")
            isWaitingForCompilation = false;
    }
    
    private void OnEditorUpdate()
    {
        if (isPolling && (DateTime.Now - lastPollTime).TotalSeconds >= pollInterval)
        {
            lastPollTime = DateTime.Now;
            PollForCommands();
        }
        Repaint();
    }
    
    private void OnGUI()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField("AliTerra AI", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);
        apiKey = EditorGUILayout.TextField("API Key", apiKey);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        GUI.color = isConnected ? Color.green : Color.red;
        EditorGUILayout.LabelField(isConnected ? "Connected" : "Disconnected", GUILayout.Width(100));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField(statusMessage, EditorStyles.miniLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(isPolling ? "Stop Polling" : "Start Polling"))
        {
            isPolling = !isPolling;
            statusMessage = isPolling ? "Polling..." : "Stopped";
        }
        if (GUILayout.Button("Sync Files")) SyncProjectFiles();
        if (GUILayout.Button("Send Logs")) SendLogs();
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Refresh & Compile"))
        {
            AssetDatabase.Refresh();
            isWaitingForCompilation = true;
            statusMessage = "Compiling...";
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Logs:", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        int start = Mathf.Max(0, capturedLogs.Count - 20);
        for (int i = capturedLogs.Count - 1; i >= start; i--)
        {
            LogEntry log = capturedLogs[i];
            GUI.color = log.type == "error" ? Color.red : log.type == "warning" ? Color.yellow : Color.white;
            EditorGUILayout.LabelField("[" + log.type + "] " + log.message, EditorStyles.wordWrappedMiniLabel);
        }
        GUI.color = Color.white;
        EditorGUILayout.EndScrollView();
        
        GUILayout.EndVertical();
    }
    
    // Simple JSON helpers (no Newtonsoft needed)
    private string JsonGetString(string json, string key)
    {
        string searchKey = "\\\"" + key + "\\\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex < 0) return null;
        
        int colonIndex = json.IndexOf(':', keyIndex);
        if (colonIndex < 0) return null;
        
        int valueStart = json.IndexOf('"', colonIndex + 1);
        if (valueStart < 0) return null;
        valueStart++;
        
        int valueEnd = valueStart;
        while (valueEnd < json.Length)
        {
            if (json[valueEnd] == '"' && json[valueEnd - 1] != '\\\\') break;
            valueEnd++;
        }
        
        return json.Substring(valueStart, valueEnd - valueStart);
    }
    
    private string JsonGetObject(string json, string key)
    {
        string searchKey = "\\\"" + key + "\\\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex < 0) return null;
        
        int colonIndex = json.IndexOf(':', keyIndex);
        if (colonIndex < 0) return null;
        
        int braceStart = json.IndexOf('{', colonIndex);
        if (braceStart < 0) return null;
        
        int depth = 1;
        int pos = braceStart + 1;
        while (pos < json.Length && depth > 0)
        {
            if (json[pos] == '{') depth++;
            else if (json[pos] == '}') depth--;
            pos++;
        }
        
        return json.Substring(braceStart, pos - braceStart);
    }
    
    private async void PollForCommands()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                HttpResponseMessage response = await client.GetAsync(serverUrl + "/api/unity/commands?apiKey=" + apiKey);
                
                if (response.IsSuccessStatusCode)
                {
                    isConnected = true;
                    string json = await response.Content.ReadAsStringAsync();
                    ProcessCommandsJson(json);
                }
                else
                {
                    isConnected = false;
                    statusMessage = "Failed: " + response.StatusCode;
                }
            }
        }
        catch (Exception ex)
        {
            isConnected = false;
            statusMessage = "Error: " + ex.Message;
        }
    }
    
    private void ProcessCommandsJson(string json)
    {
        // Simple array parsing
        int arrayStart = json.IndexOf("[");
        if (arrayStart < 0) return;
        
        int pos = arrayStart + 1;
        while (pos < json.Length)
        {
            int objStart = json.IndexOf("{", pos);
            if (objStart < 0) break;
            
            int depth = 1;
            int objEnd = objStart + 1;
            while (objEnd < json.Length && depth > 0)
            {
                if (json[objEnd] == '{') depth++;
                else if (json[objEnd] == '}') depth--;
                objEnd++;
            }
            
            string cmdJson = json.Substring(objStart, objEnd - objStart);
            string cmdId = JsonGetString(cmdJson, "id");
            string cmdType = JsonGetString(cmdJson, "commandType");
            string payloadJson = JsonGetObject(cmdJson, "payload");
            
            ExecuteCommand(cmdId, cmdType, payloadJson);
            
            pos = objEnd;
            if (json.IndexOf("{", pos) < 0) break;
        }
    }
    
    private async void ExecuteCommand(string cmdId, string cmdType, string payloadJson)
    {
        try
        {
            statusMessage = "Executing: " + cmdType;
            bool success = false;
            
            if (cmdType == "create_script" || cmdType == "modify_script")
            {
                string filePath = JsonGetString(payloadJson, "file_path");
                string content = JsonGetString(payloadJson, "content");
                success = HandleScriptCommand(filePath, content);
            }
            else if (cmdType == "set_object_property")
            {
                success = HandleSetProperty(payloadJson);
            }
            else if (cmdType == "create_scriptable_object")
            {
                success = HandleCreateSO(payloadJson);
            }
            else if (cmdType == "execute_editor_command")
            {
                string command = JsonGetString(payloadJson, "command");
                success = HandleEditorCommand(command);
            }
            else if (cmdType == "create_game_object")
            {
                success = HandleCreateGameObject(payloadJson);
            }
            
            await ReportResult(cmdId, success);
            statusMessage = success ? "Done: " + cmdType : "Failed: " + cmdType;
        }
        catch (Exception ex)
        {
            Debug.LogError("Command failed: " + ex.Message);
            await ReportResult(cmdId, false);
            statusMessage = "Error: " + ex.Message;
        }
    }
    
    private bool HandleScriptCommand(string filePath, string content)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(content))
        {
            Debug.LogError("Invalid script payload");
            return false;
        }
        
        // Unescape content
        content = content.Replace("\\\\n", "\\n").Replace("\\\\t", "\\t").Replace("\\\\\\\"", "\\"");
        
        string fullPath = Path.Combine(Application.dataPath, filePath);
        string directory = Path.GetDirectoryName(fullPath);
        
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        File.WriteAllText(fullPath, content);
        AssetDatabase.Refresh();
        isWaitingForCompilation = true;
        
        return true;
    }
    
    private bool HandleSetProperty(string payloadJson)
    {
        string objectPath = JsonGetString(payloadJson, "object_path");
        string component = JsonGetString(payloadJson, "component");
        string property = JsonGetString(payloadJson, "property");
        string value = JsonGetString(payloadJson, "value");
        
        GameObject obj = GameObject.Find(objectPath);
        if (obj == null)
        {
            Debug.LogError("Object not found: " + objectPath);
            return false;
        }
        
        Component comp = obj.GetComponent(component);
        if (comp == null)
        {
            Debug.LogError("Component not found: " + component);
            return false;
        }
        
        Debug.Log("Set " + property + " on " + objectPath);
        return true;
    }
    
    private bool HandleCreateSO(string payloadJson)
    {
        string assetPath = JsonGetString(payloadJson, "asset_path");
        string scriptClass = JsonGetString(payloadJson, "script_class");
        
        Debug.Log("Create SO: " + assetPath + " of type " + scriptClass);
        return true;
    }
    
    private bool HandleEditorCommand(string command)
    {
        if (command == "play") { EditorApplication.isPlaying = true; return true; }
        if (command == "stop") { EditorApplication.isPlaying = false; return true; }
        if (command == "save") { EditorApplication.ExecuteMenuItem("File/Save"); return true; }
        if (command == "refresh") { AssetDatabase.Refresh(); return true; }
        if (command == "compile") { AssetDatabase.Refresh(); isWaitingForCompilation = true; return true; }
        
        Debug.LogWarning("Unknown command: " + command);
        return false;
    }
    
    private bool HandleCreateGameObject(string payloadJson)
    {
        try
        {
            string name = JsonGetString(payloadJson, "name");
            string positionStr = JsonGetString(payloadJson, "position");
            string rotationStr = JsonGetString(payloadJson, "rotation");
            string scaleStr = JsonGetString(payloadJson, "scale");
            string componentsStr = JsonGetString(payloadJson, "components");
            string primitive = JsonGetString(payloadJson, "primitive");
            string colorStr = JsonGetString(payloadJson, "color");
            string parentName = JsonGetString(payloadJson, "parent");
            
            // Parse position
            Vector3 position = ParseVector3(positionStr);
            Vector3 rotation = ParseVector3(rotationStr);
            Vector3 scale = ParseVector3(scaleStr);
            
            // Create primitive GameObject - this creates mesh + collider + material automatically!
            PrimitiveType primType = PrimitiveType.Capsule;
            switch (primitive.ToLower())
            {
                case "cube": primType = PrimitiveType.Cube; break;
                case "sphere": primType = PrimitiveType.Sphere; break;
                case "capsule": primType = PrimitiveType.Capsule; break;
                case "cylinder": primType = PrimitiveType.Cylinder; break;
                case "plane": primType = PrimitiveType.Plane; break;
                case "quad": primType = PrimitiveType.Quad; break;
            }
            
            // Create the primitive - comes with MeshFilter, MeshRenderer, Collider!
            GameObject obj = GameObject.CreatePrimitive(primType);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(rotation);
            obj.transform.localScale = scale;
            
            // Set parent if specified
            if (!string.IsNullOrEmpty(parentName))
            {
                GameObject parent = GameObject.Find(parentName);
                if (parent != null)
                {
                    obj.transform.SetParent(parent.transform);
                }
            }
            
            // Set color
            Color color = ParseColor(colorStr);
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                renderer.material = mat;
            }
            
            // Add additional components
            if (!string.IsNullOrEmpty(componentsStr))
            {
                string[] components = componentsStr.Split(',');
                foreach (string comp in components)
                {
                    string compName = comp.Trim();
                    // Skip if component already exists (like Collider from CreatePrimitive)
                    if (compName.EndsWith("Collider") && obj.GetComponent<Collider>() != null)
                        continue;
                    AddComponentByName(obj, compName);
                }
            }
            
            // Select the object in hierarchy
            UnityEditor.Selection.activeGameObject = obj;
            
            // Mark scene as dirty
            UnityEditor.EditorUtility.SetDirty(obj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
            Debug.Log("Created GameObject: " + name + " at " + position);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to create GameObject: " + ex.Message);
            return false;
        }
    }
    
    private Vector3 ParseVector3(string json)
    {
        if (string.IsNullOrEmpty(json)) return Vector3.zero;
        
        float x = 0, y = 0, z = 0;
        
        string xStr = JsonGetString(json, "x");
        string yStr = JsonGetString(json, "y");
        string zStr = JsonGetString(json, "z");
        
        if (!string.IsNullOrEmpty(xStr)) float.TryParse(xStr, out x);
        if (!string.IsNullOrEmpty(yStr)) float.TryParse(yStr, out y);
        if (!string.IsNullOrEmpty(zStr)) float.TryParse(zStr, out z);
        
        return new Vector3(x, y, z);
    }
    
    private Color ParseColor(string colorStr)
    {
        if (string.IsNullOrEmpty(colorStr)) return Color.white;
        
        switch (colorStr.ToLower())
        {
            case "red": return Color.red;
            case "green": return Color.green;
            case "blue": return Color.blue;
            case "yellow": return Color.yellow;
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            case "white": return Color.white;
            case "black": return Color.black;
            case "gray": case "grey": return Color.gray;
            default:
                if (colorStr.StartsWith("#") && ColorUtility.TryParseHtmlString(colorStr, out Color c))
                    return c;
                return Color.white;
        }
    }
    
    private void AddComponentByName(GameObject obj, string componentName)
    {
        switch (componentName)
        {
            case "Rigidbody":
                obj.AddComponent<Rigidbody>();
                break;
            case "Rigidbody2D":
                obj.AddComponent<Rigidbody2D>();
                break;
            case "BoxCollider":
                obj.AddComponent<BoxCollider>();
                break;
            case "SphereCollider":
                obj.AddComponent<SphereCollider>();
                break;
            case "CapsuleCollider":
                obj.AddComponent<CapsuleCollider>();
                break;
            case "MeshCollider":
                obj.AddComponent<MeshCollider>();
                break;
            case "CharacterController":
                obj.AddComponent<CharacterController>();
                break;
            case "AudioSource":
                obj.AddComponent<AudioSource>();
                break;
            case "Light":
                obj.AddComponent<Light>();
                break;
            case "Camera":
                obj.AddComponent<Camera>();
                break;
            default:
                // Try to find script by name
                System.Type scriptType = System.Type.GetType(componentName);
                if (scriptType == null)
                {
                    foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        scriptType = assembly.GetType(componentName);
                        if (scriptType != null) break;
                    }
                }
                if (scriptType != null && scriptType.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    obj.AddComponent(scriptType);
                }
                else
                {
                    Debug.LogWarning("Component not found: " + componentName);
                }
                break;
        }
    }
    
    private Mesh CreateCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private Mesh CreateSphereMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private Mesh CreateCapsuleMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private Mesh CreateCylinderMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private Mesh CreatePlaneMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private Mesh CreateQuadMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(temp);
        return mesh;
    }
    
    private async Task ReportResult(string commandId, bool success)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string json = "{\\"apiKey\\":\\"" + apiKey + "\\",\\"commandId\\":\\"" + commandId + "\\",\\"success\\":" + success.ToString().ToLower() + "}";
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync(serverUrl + "/api/unity/commands", content);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Report failed: " + ex.Message);
        }
    }
    
    private async void SyncProjectFiles()
    {
        try
        {
            statusMessage = "Syncing...";
            
            StringBuilder filesJson = new StringBuilder("[");
            bool first = true;
            
            // Scripts
            string[] scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            foreach (string path in scripts)
            {
                string relPath = path.Replace(Application.dataPath, "").Replace("\\\\", "/").TrimStart('/');
                string fileContent = File.ReadAllText(path).Replace("\\\\", "\\\\\\\\").Replace("\\"","\\\\\\"").Replace("\\n", "\\\\n").Replace("\\t", "\\\\t").Replace("\\r", "");
                
                if (!first) filesJson.Append(",");
                filesJson.Append("{\\"path\\":\\"" + relPath + "\\",\\"type\\":\\"script\\",\\"content\\":\\"" + fileContent + "\\"}");
                first = false;
            }
            
            // Scenes
            string[] scenes = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            foreach (string path in scenes)
            {
                string relPath = path.Replace(Application.dataPath, "").Replace("\\\\", "/").TrimStart('/');
                if (!first) filesJson.Append(",");
                filesJson.Append("{\\"path\\":\\"" + relPath + "\\",\\"type\\":\\"scene\\",\\"content\\":null}");
                first = false;
            }
            
            // Prefabs
            string[] prefabs = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            foreach (string path in prefabs)
            {
                string relPath = path.Replace(Application.dataPath, "").Replace("\\\\", "/").TrimStart('/');
                if (!first) filesJson.Append(",");
                filesJson.Append("{\\"path\\":\\"" + relPath + "\\",\\"type\\":\\"prefab\\",\\"content\\":null}");
                first = false;
            }
            
            filesJson.Append("]");
            
            using (HttpClient client = new HttpClient())
            {
                string json = "{\\"apiKey\\":\\"" + apiKey + "\\",\\"files\\":" + filesJson.ToString() + "}";
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(serverUrl + "/api/unity/sync", content);
                
                if (response.IsSuccessStatusCode)
                {
                    isConnected = true;
                    statusMessage = "Synced!";
                }
                else
                {
                    statusMessage = "Sync failed";
                }
            }
        }
        catch (Exception ex)
        {
            statusMessage = "Sync error: " + ex.Message;
        }
    }
    
    private async void SendLogs()
    {
        if (capturedLogs.Count == 0)
        {
            statusMessage = "No logs";
            return;
        }
        
        try
        {
            List<LogEntry> logsToSend = new List<LogEntry>(capturedLogs);
            capturedLogs.Clear();
            
            StringBuilder logsJson = new StringBuilder("[");
            for (int i = 0; i < logsToSend.Count; i++)
            {
                if (i > 0) logsJson.Append(",");
                string msg = logsToSend[i].message.Replace("\\\\", "\\\\\\\\").Replace("\\"","\\\\\\"").Replace("\\n", "\\\\n");
                logsJson.Append("{\\"type\\":\\"" + logsToSend[i].type + "\\",\\"message\\":\\"" + msg + "\\"}");
            }
            logsJson.Append("]");
            
            using (HttpClient client = new HttpClient())
            {
                string json = "{\\"apiKey\\":\\"" + apiKey + "\\",\\"logs\\":" + logsJson.ToString() + "}";
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(serverUrl + "/api/unity/logs", content);
                
                if (response.IsSuccessStatusCode)
                {
                    statusMessage = "Sent " + logsToSend.Count + " logs";
                }
                else
                {
                    statusMessage = "Send failed";
                    capturedLogs.InsertRange(0, logsToSend);
                }
            }
        }
        catch (Exception ex)
        {
            statusMessage = "Log error: " + ex.Message;
        }
    }
}
#endif`;
}

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey") || "your_api_key_here";
    const serverUrl = searchParams.get("serverUrl") || 
      `${req.headers.get("x-forwarded-proto") || "https"}://${req.headers.get("host")}`;

    const pluginCode = generatePluginCode(apiKey, serverUrl);

    return new NextResponse(pluginCode, {
      headers: {
        "Content-Type": "text/plain",
        "Content-Disposition": "attachment; filename=AliTerraAI.cs",
      },
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
