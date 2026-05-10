import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

// The Unity C# plugin code template
function generatePluginCode(apiKey: string, serverUrl: string): string {
  return `#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    private List<LogEntry> recentLogs = new List<LogEntry>();
    private string statusMessage = "Ready";
    private DateTime lastPollTime;
    
    // Console log interception
    private static List<LogEntry> capturedLogs = new List<LogEntry>();
    private static bool isWaitingForCompilation = false;
    
    [Serializable]
    public class LogEntry
    {
        public string type;
        public string message;
        public string stackTrace;
        public string timestamp;
        
        public LogEntry(string type, string message, string stackTrace = null)
        {
            this.type = type;
            this.message = message;
            this.stackTrace = stackTrace;
            this.timestamp = DateTime.UtcNow.ToString("o");
        }
    }
    
    [Serializable]
    public class Command
    {
        public string id;
        public string commandType;
        public JObject payload;
    }
    
    [Serializable]
    public class CommandsResponse
    {
        public List<Command> commands;
    }
    
    [MenuItem("Window/AliTerra AI")]
    public static void ShowWindow()
    {
        GetWindow<AliTerraAI>("AliTerra AI");
    }
    
    private void OnEnable()
    {
        // Subscribe to console log events
        Application.logMessageReceived += OnLogReceived;
        EditorApplication.update += OnEditorUpdate;
    }
    
    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogReceived;
        EditorApplication.update -= OnEditorUpdate;
        
        if (isPolling)
        {
            isPolling = false;
        }
    }
    
    private void OnLogReceived(string message, string stackTrace, LogType type)
    {
        string logType = type switch
        {
            LogType.Error or LogType.Exception => "error",
            LogType.Warning => "warning",
            _ => "log"
        };
        
        capturedLogs.Add(new LogEntry(logType, message, stackTrace));
        
        // Keep only last 100 logs
        if (capturedLogs.Count > 100)
        {
            capturedLogs.RemoveAt(0);
        }
        
        // If we were waiting for compilation and got an error, note it
        if (isWaitingForCompilation && logType == "error")
        {
            isWaitingForCompilation = false;
        }
    }
    
    private void OnEditorUpdate()
    {
        if (isPolling && (DateTime.Now - lastPollTime).TotalSeconds >= pollInterval)
        {
            lastPollTime = DateTime.Now;
            _ = PollForCommandsAsync();
        }
        
        Repaint();
    }
    
    private void OnGUI()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Header
        EditorGUILayout.LabelField("AliTerra AI - Unity Fullstack Developer", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Connection settings
        EditorGUILayout.LabelField("Server URL:", EditorStyles.miniLabel);
        serverUrl = EditorGUILayout.TextField(serverUrl);
        
        EditorGUILayout.LabelField("API Key:", EditorStyles.miniLabel);
        apiKey = EditorGUILayout.TextField(apiKey);
        
        EditorGUILayout.Space();
        
        // Status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Status:", GUILayout.Width(50));
        GUI.color = isConnected ? Color.green : Color.red;
        EditorGUILayout.LabelField(isConnected ? "Connected" : "Disconnected", GUILayout.Width(80));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField(statusMessage, EditorStyles.miniLabel);
        
        EditorGUILayout.Space();
        
        // Buttons
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button(isPolling ? "Stop Polling" : "Start Polling"))
        {
            isPolling = !isPolling;
            if (isPolling)
            {
                statusMessage = "Polling for commands...";
                lastPollTime = DateTime.MinValue;
            }
            else
            {
                statusMessage = "Polling stopped";
            }
        }
        
        if (GUILayout.Button("Sync Files"))
        {
            _ = SyncProjectFilesAsync();
        }
        
        if (GUILayout.Button("Send Logs"))
        {
            _ = SendLogsAsync();
        }
        
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Refresh & Compile"))
        {
            AssetDatabase.Refresh();
            isWaitingForCompilation = true;
            statusMessage = "Compiling...";
        }
        
        EditorGUILayout.Space();
        
        // Recent logs display
        EditorGUILayout.LabelField("Recent Logs:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        
        foreach (var log in capturedLogs.AsEnumerable().Reverse().Take(20))
        {
            GUI.color = log.type == "error" ? Color.red : 
                       log.type == "warning" ? Color.yellow : Color.white;
            EditorGUILayout.LabelField($"[{log.type}] {log.message}", EditorStyles.wordWrappedMiniLabel);
            GUI.color = Color.white;
        }
        
        EditorGUILayout.EndScrollView();
        
        GUILayout.EndVertical();
    }
    
    private async Task PollForCommandsAsync()
    {
        try
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"{serverUrl}/api/unity/commands?apiKey={apiKey}");
                
                if (response.IsSuccessStatusCode)
                {
                    isConnected = true;
                    var json = await response.Content.ReadAsStringAsync();
                    var commandsResponse = JsonConvert.DeserializeObject<CommandsResponse>(json);
                    
                    if (commandsResponse?.commands != null)
                    {
                        foreach (var cmd in commandsResponse.commands)
                        {
                            await ExecuteCommandAsync(cmd);
                        }
                    }
                }
                else
                {
                    isConnected = false;
                    statusMessage = $"Connection failed: {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            isConnected = false;
            statusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async Task ExecuteCommandAsync(Command cmd)
    {
        try
        {
            statusMessage = $"Executing: {cmd.commandType}";
            
            bool success = false;
            object result = null;
            
            switch (cmd.commandType)
            {
                case "create_script":
                case "modify_script":
                    success = await HandleScriptCommand(cmd.payload);
                    break;
                    
                case "set_object_property":
                    success = HandleSetProperty(cmd.payload);
                    break;
                    
                case "create_scriptable_object":
                    success = HandleCreateSO(cmd.payload);
                    break;
                    
                case "execute_editor_command":
                    success = HandleEditorCommand(cmd.payload);
                    break;
                    
                default:
                    Debug.LogWarning($"Unknown command type: {cmd.commandType}");
                    break;
            }
            
            // Report completion
            await ReportCommandResult(cmd.id, success, result);
            
            if (success)
            {
                statusMessage = $"Completed: {cmd.commandType}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Command execution failed: {ex.Message}");
            await ReportCommandResult(cmd.id, false, new { error = ex.Message });
            statusMessage = $"Failed: {cmd.commandType} - {ex.Message}";
        }
    }
    
    private async Task<bool> HandleScriptCommand(JObject payload)
    {
        var filePath = payload["file_path"]?.ToString();
        var content = payload["content"]?.ToString();
        
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(content))
        {
            Debug.LogError("Invalid script command payload");
            return false;
        }
        
        var fullPath = Path.Combine(Application.dataPath, filePath);
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Write the file
        File.WriteAllText(fullPath, content);
        
        // Refresh to trigger compilation
        AssetDatabase.Refresh();
        isWaitingForCompilation = true;
        
        // Wait a bit for compilation
        await Task.Delay(500);
        
        return true;
    }
    
    private bool HandleSetProperty(JObject payload)
    {
        var objectPath = payload["object_path"]?.ToString();
        var component = payload["component"]?.ToString();
        var property = payload["property"]?.ToString();
        var value = payload["value"]?.ToString();
        
        if (string.IsNullOrEmpty(objectPath))
        {
            Debug.LogError("Invalid set property payload");
            return false;
        }
        
        // Find the GameObject
        var obj = GameObject.Find(objectPath);
        if (obj == null)
        {
            Debug.LogError($"GameObject not found: {objectPath}");
            return false;
        }
        
        // Find component by type name
        var comp = obj.GetComponent(component);
        if (comp == null)
        {
            Debug.LogError($"Component not found: {component} on {objectPath}");
            return false;
        }
        
        // Set property using reflection
        var prop = comp.GetType().GetProperty(property);
        if (prop != null)
        {
            try
            {
                var parsedValue = JsonConvert.DeserializeObject(value, prop.PropertyType);
                prop.SetValue(comp, parsedValue);
                EditorUtility.SetDirty(comp);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set property: {ex.Message}");
                return false;
            }
        }
        
        // Try field
        var field = comp.GetType().GetField(property);
        if (field != null)
        {
            try
            {
                var parsedValue = JsonConvert.DeserializeObject(value, field.FieldType);
                field.SetValue(comp, parsedValue);
                EditorUtility.SetDirty(comp);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set field: {ex.Message}");
                return false;
            }
        }
        
        Debug.LogError($"Property/Field not found: {property} on {component}");
        return false;
    }
    
    private bool HandleCreateSO(JObject payload)
    {
        var assetPath = payload["asset_path"]?.ToString();
        var scriptClass = payload["script_class"]?.ToString();
        
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(scriptClass))
        {
            Debug.LogError("Invalid scriptable object payload");
            return false;
        }
        
        // Find the ScriptableObject type
        var soType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == scriptClass && t.IsSubclassOf(typeof(ScriptableObject)));
        
        if (soType == null)
        {
            Debug.LogError($"ScriptableObject type not found: {scriptClass}");
            return false;
        }
        
        var so = ScriptableObject.CreateInstance(soType);
        var fullPath = $"Assets/{assetPath}";
        
        var directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        AssetDatabase.CreateAsset(so, fullPath);
        AssetDatabase.SaveAssets();
        
        return true;
    }
    
    private bool HandleEditorCommand(JObject payload)
    {
        var command = payload["command"]?.ToString();
        
        switch (command)
        {
            case "play":
                EditorApplication.isPlaying = true;
                return true;
                
            case "stop":
                EditorApplication.isPlaying = false;
                return true;
                
            case "save":
                EditorApplication.ExecuteMenuItem("File/Save");
                return true;
                
            case "refresh":
                AssetDatabase.Refresh();
                return true;
                
            case "compile":
                AssetDatabase.Refresh();
                isWaitingForCompilation = true;
                return true;
                
            default:
                Debug.LogWarning($"Unknown editor command: {command}");
                return false;
        }
    }
    
    private async Task ReportCommandResult(string commandId, bool success, object result)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var payload = new
                {
                    apiKey,
                    commandId,
                    success,
                    result
                };
                
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                await client.PostAsync($"{serverUrl}/api/unity/commands", content);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to report command result: {ex.Message}");
        }
    }
    
    private async Task SyncProjectFilesAsync()
    {
        try
        {
            statusMessage = "Syncing project files...";
            
            var files = new List<object>();
            
            // Index all scripts
            var scriptPaths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            foreach (var path in scriptPaths)
            {
                var relativePath = path.Replace(Application.dataPath + "\\", "").Replace("\\", "/");
                files.Add(new
                {
                    path = relativePath,
                    type = "script",
                    content = File.ReadAllText(path)
                });
            }
            
            // Index shaders
            var shaderPaths = Directory.GetFiles(Application.dataPath, "*.shader", SearchOption.AllDirectories);
            foreach (var path in shaderPaths)
            {
                var relativePath = path.Replace(Application.dataPath + "\\", "").Replace("\\", "/");
                files.Add(new
                {
                    path = relativePath,
                    type = "shader",
                    content = File.ReadAllText(path)
                });
            }
            
            // Index scenes
            var scenePaths = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            foreach (var path in scenePaths)
            {
                var relativePath = path.Replace(Application.dataPath + "\\", "").Replace("\\", "/");
                files.Add(new
                {
                    path = relativePath,
                    type = "scene",
                    content = (string)null
                });
            }
            
            // Index prefabs
            var prefabPaths = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            foreach (var path in prefabPaths)
            {
                var relativePath = path.Replace(Application.dataPath + "\\", "").Replace("\\", "/");
                files.Add(new
                {
                    path = relativePath,
                    type = "prefab",
                    content = (string)null
                });
            }
            
            // Send to server
            using (var client = new HttpClient())
            {
                var payload = new { apiKey, files };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync($"{serverUrl}/api/unity/sync", content);
                
                if (response.IsSuccessStatusCode)
                {
                    isConnected = true;
                    statusMessage = $"Synced {files.Count} files";
                }
                else
                {
                    statusMessage = $"Sync failed: {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"Sync error: {ex.Message}";
        }
    }
    
    private async Task SendLogsAsync()
    {
        try
        {
            if (capturedLogs.Count == 0)
            {
                statusMessage = "No logs to send";
                return;
            }
            
            statusMessage = "Sending logs...";
            
            var logsToSend = capturedLogs.ToList();
            capturedLogs.Clear();
            
            using (var client = new HttpClient())
            {
                var payload = new
                {
                    apiKey,
                    logs = logsToSend
                };
                
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync($"{serverUrl}/api/unity/logs", content);
                
                if (response.IsSuccessStatusCode)
                {
                    statusMessage = $"Sent {logsToSend.Count} logs";
                }
                else
                {
                    statusMessage = $"Failed to send logs: {response.StatusCode}";
                    // Put logs back
                    capturedLogs.InsertRange(0, logsToSend);
                }
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"Log send error: {ex.Message}";
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
