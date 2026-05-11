#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class AliTerraAI : EditorWindow
{
    private string apiKey = "alterra_c242233d1b9a4d7c941064350cf88315";
    private string serverUrl = "http://3000-io1dc1x4vgibg00gk4v83.e2b.app";
    private float pollInterval = 2f;
    
    private bool isConnected = false;
    private bool isPolling = false;
    private Vector2 scrollPos;
    private string statusMessage = "Ready";
    private double lastPollTime;
    
    private static List<LogEntry> capturedLogs = new List<LogEntry>();
    private static bool isWaitingForCompilation = false;
    
    [System.Serializable]
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
        if (isPolling && EditorApplication.timeSinceStartup - lastPollTime >= pollInterval)
        {
            lastPollTime = EditorApplication.timeSinceStartup;
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
            if (isPolling) lastPollTime = 0;
            statusMessage = isPolling ? "Polling..." : "Stopped";
        }
        if (GUILayout.Button("Sync Files")) { SyncProjectFiles(); }
        if (GUILayout.Button("Send Logs")) { SendLogs(); }
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
    
    private string JsonGetString(string json, string key)
    {
        string searchKey = "\"" + key + "\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex < 0) return null;
        int colonIndex = json.IndexOf(':', keyIndex);
        if (colonIndex < 0) return null;
        int nullCheck = colonIndex + 1;
        while (nullCheck < json.Length && char.IsWhiteSpace(json[nullCheck])) nullCheck++;
        if (nullCheck + 3 < json.Length && json.Substring(nullCheck, 4) == "null") return null;
        int valueStart = json.IndexOf('"', colonIndex + 1);
        if (valueStart < 0) return null;
        valueStart++;
        int valueEnd = valueStart;
        while (valueEnd < json.Length)
        {
            if (json[valueEnd] == '"' && (valueEnd == 0 || json[valueEnd - 1] != '\\')) break;
            valueEnd++;
        }
        return json.Substring(valueStart, valueEnd - valueStart);
    }
    
    private string JsonGetObject(string json, string key)
    {
        string searchKey = "\"" + key + "\"";
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
    
    private UnityWebRequest CreateRequest(string url, string method = "GET", string body = null)
    {
        UnityWebRequest request;
        if (method == "GET")
        {
            request = UnityWebRequest.Get(url);
        }
        else
        {
            request = new UnityWebRequest(url, method);
            if (body != null)
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }
        request.timeout = 10;
        request.certificateHandler = new BypassCertificateHandler();
        return request;
    }
    
    private void PollForCommands()
    {
        string url = serverUrl + "/api/unity/commands?apiKey=" + apiKey;
        UnityWebRequest request = CreateRequest(url);
        
        request.SendWebRequest().completed += (op) =>
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                isConnected = true;
                ProcessCommandsJson(request.downloadHandler.text);
            }
            else
            {
                isConnected = false;
                statusMessage = "Error: " + request.error;
            }
            request.Dispose();
        };
    }
    
    private void ProcessCommandsJson(string json)
    {
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
    
    private void ExecuteCommand(string cmdId, string cmdType, string payloadJson)
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
            else if (cmdType == "create_tag")
            {
                string tagName = JsonGetString(payloadJson, "tag_name");
                success = CreateTag(tagName);
            }
            else if (cmdType == "assign_tag")
            {
                string objectName = JsonGetString(payloadJson, "object_name");
                string tagName = JsonGetString(payloadJson, "tag_name");
                success = AssignTag(objectName, tagName);
            }
            
            ReportResult(cmdId, success);
            statusMessage = success ? "Done: " + cmdType : "Failed: " + cmdType;
        }
        catch (Exception ex)
        {
            Debug.LogError("Command failed: " + ex.Message);
            ReportResult(cmdId, false);
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
        
        content = content.Replace("\\n", "\n").Replace("\\t", "\t");
        
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
        Debug.Log("Set property: " + payloadJson);
        return true;
    }
    
    private bool HandleCreateSO(string payloadJson)
    {
        Debug.Log("Create SO: " + payloadJson);
        return true;
    }
    
    private bool HandleEditorCommand(string command)
    {
        if (command == "play") { EditorApplication.isPlaying = true; return true; }
        if (command == "stop") { EditorApplication.isPlaying = false; return true; }
        if (command == "save") { EditorApplication.ExecuteMenuItem("File/Save"); return true; }
        if (command == "refresh") { AssetDatabase.Refresh(); return true; }
        if (command == "compile") { AssetDatabase.Refresh(); isWaitingForCompilation = true; return true; }
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
            string tagName = JsonGetString(payloadJson, "tag");
            
            Vector3 position = ParseVector3(positionStr);
            Vector3 rotation = ParseVector3(rotationStr);
            Vector3 scale = ParseVector3(scaleStr);
            
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
            
            GameObject obj = GameObject.CreatePrimitive(primType);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(rotation);
            obj.transform.localScale = scale;
            
            Color color = ParseColor(colorStr);
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                renderer.material = mat;
            }
            
            if (!string.IsNullOrEmpty(componentsStr))
            {
                string[] components = componentsStr.Split(',');
                foreach (string comp in components)
                {
                    AddComponentByName(obj, comp.Trim());
                }
            }
            
            if (!string.IsNullOrEmpty(tagName))
            {
                if (!TagExists(tagName))
                    CreateTag(tagName);
                obj.tag = tagName;
            }
            
            UnityEditor.Selection.activeGameObject = obj;
            EditorUtility.SetDirty(obj);
            
            Debug.Log("Created: " + name);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed: " + ex.Message);
            return false;
        }
    }
    
    private bool CreateTag(string tagName)
    {
        try
        {
            if (TagExists(tagName)) return true;
            
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            
            tags.InsertArrayElementAtIndex(tags.arraySize);
            SerializedProperty newTag = tags.GetArrayElementAtIndex(tags.arraySize - 1);
            newTag.stringValue = tagName;
            
            tagManager.ApplyModifiedProperties();
            Debug.Log("Created tag: " + tagName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to create tag: " + ex.Message);
            return false;
        }
    }
    
    private bool TagExists(string tagName)
    {
        foreach (string tag in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (tag == tagName) return true;
        }
        return false;
    }
    
    private bool AssignTag(string objectName, string tagName)
    {
        try
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj == null)
            {
                Debug.LogError("Object not found: " + objectName);
                return false;
            }
            
            if (!TagExists(tagName))
                CreateTag(tagName);
            
            obj.tag = tagName;
            EditorUtility.SetDirty(obj);
            Debug.Log("Assigned tag " + tagName + " to " + objectName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed: " + ex.Message);
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
            case "gray":
            case "grey": return Color.gray;
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
            case "Rigidbody": obj.AddComponent<Rigidbody>(); break;
            case "Rigidbody2D": obj.AddComponent<Rigidbody2D>(); break;
            case "BoxCollider": obj.AddComponent<BoxCollider>(); break;
            case "SphereCollider": obj.AddComponent<SphereCollider>(); break;
            case "CapsuleCollider": obj.AddComponent<CapsuleCollider>(); break;
            case "CharacterController": obj.AddComponent<CharacterController>(); break;
            case "AudioSource": obj.AddComponent<AudioSource>(); break;
            case "Light": obj.AddComponent<Light>(); break;
            case "Camera": obj.AddComponent<Camera>(); break;
            default:
                System.Type type = System.Type.GetType(componentName);
                if (type == null)
                {
                    foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(componentName);
                        if (type != null) break;
                    }
                }
                if (type != null && type.IsSubclassOf(typeof(MonoBehaviour)))
                    obj.AddComponent(type);
                break;
        }
    }
    
    private void ReportResult(string commandId, bool success)
    {
        string url = serverUrl + "/api/unity/commands";
        string json = "{\"apiKey\":\"" + apiKey + "\",\"commandId\":\"" + commandId + "\",\"success\":" + success.ToString().ToLower() + "}";
        
        UnityWebRequest request = CreateRequest(url, "POST", json);
        request.SendWebRequest().completed += (op) =>
        {
            request.Dispose();
        };
    }
    
    private void SyncProjectFiles()
    {
        statusMessage = "Syncing...";
        
        StringBuilder filesJson = new StringBuilder("[");
        bool first = true;
        
        string[] scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        foreach (string path in scripts)
        {
            string relPath = path.Replace(Application.dataPath, "").Replace("\\", "/").TrimStart('/');
            string content = File.ReadAllText(path).Replace("\\", "\\\\").Replace("\"","\\\"").Replace("\n", "\\n").Replace("\r", "");
            
            if (!first) filesJson.Append(",");
            filesJson.Append("{\"path\":\"" + relPath + "\",\"type\":\"script\",\"content\":\"" + content + "\"}");
            first = false;
        }
        
        filesJson.Append("]");
        
        string url = serverUrl + "/api/unity/sync";
        string json = "{\"apiKey\":\"" + apiKey + "\",\"files\":" + filesJson.ToString() + "}";
        
        UnityWebRequest request = CreateRequest(url, "POST", json);
        request.SendWebRequest().completed += (op) =>
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                isConnected = true;
                statusMessage = "Synced!";
            }
            else
            {
                statusMessage = "Sync failed: " + request.error;
            }
            request.Dispose();
        };
    }
    
    private void SendLogs()
    {
        if (capturedLogs.Count == 0)
        {
            statusMessage = "No logs";
            return;
        }
        
        List<LogEntry> logsToSend = new List<LogEntry>(capturedLogs);
        capturedLogs.Clear();
        
        StringBuilder logsJson = new StringBuilder("[");
        for (int i = 0; i < logsToSend.Count; i++)
        {
            if (i > 0) logsJson.Append(",");
            string msg = logsToSend[i].message.Replace("\\", "\\\\").Replace("\"","\\\"").Replace("\n", "\\n");
            logsJson.Append("{\"type\":\"" + logsToSend[i].type + "\",\"message\":\"" + msg + "\"}");
        }
        logsJson.Append("]");
        
        string url = serverUrl + "/api/unity/logs";
        string json = "{\"apiKey\":\"" + apiKey + "\",\"logs\":" + logsJson.ToString() + "}";
        
        UnityWebRequest request = CreateRequest(url, "POST", json);
        request.SendWebRequest().completed += (op) =>
        {
            if (request.result == UnityWebRequest.Result.Success)
                statusMessage = "Sent " + logsToSend.Count + " logs";
            else
            {
                statusMessage = "Send failed";
                capturedLogs.InsertRange(0, logsToSend);
            }
            request.Dispose();
        };
    }
}

// Bypass SSL certificate validation
public class BypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}
#endif