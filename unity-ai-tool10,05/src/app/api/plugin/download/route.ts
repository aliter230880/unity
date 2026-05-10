import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const projectId = searchParams.get("projectId");

  if (!projectId) {
    return NextResponse.json({ error: "projectId required" }, { status: 400 });
  }

  const [project] = await db
    .select()
    .from(projects)
    .where(eq(projects.id, projectId))
    .limit(1);

  if (!project) {
    return NextResponse.json({ error: "Project not found" }, { status: 404 });
  }

  const host = req.headers.get("host") ?? "localhost:3000";
  const protocol = req.headers.get("x-forwarded-proto") ?? "http";
  const serverUrl = `${protocol}://${host}`;

  const pluginCode = generatePluginCode(project.apiKey, serverUrl, project.name);

  return new NextResponse(pluginCode, {
    headers: {
      "Content-Type": "text/plain; charset=utf-8",
      "Content-Disposition": `attachment; filename="AliTerraAI.cs"`,
    },
  });
}

function generatePluginCode(apiKey: string, serverUrl: string, projectName: string): string {
  return `// ============================================================
// AliTerra AI — Unity Editor Plugin
// Project: ${projectName}
// Auto-generated. Place in Assets/Editor/AliTerraAI.cs
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

public class AliTerraAI : EditorWindow
{
    // ===================== CONFIG =====================
    private const string API_KEY = "${apiKey}";
    private const string SERVER_URL = "${serverUrl}";
    private const float POLL_INTERVAL = 2f; // seconds between polls
    private const float LOG_FLUSH_INTERVAL = 3f; // seconds between log flushes
    private const float SYNC_INTERVAL = 30f; // seconds between file syncs
    
    // ===================== STATE ======================
    private bool isConnected = false;
    private string statusMessage = "Not connected";
    private List<string> activityLog = new List<string>();
    private double lastPollTime = 0;
    private double lastLogFlushTime = 0;
    private double lastSyncTime = 0;
    
    // Auto-fix state
    private bool isWaitingForCompilation = false;
    private int autoFixAttempts = 0;
    private const int MAX_AUTO_FIX_ATTEMPTS = 3;
    
    // Queued logs
    private List<LogEntry> pendingLogs = new List<LogEntry>();
    private bool isPolling = false;
    private bool isSyncing = false;

    [Serializable]
    private class LogEntry
    {
        public string logType;
        public string message;
        public string stackTrace;
        public bool isCompilationError;
    }

    // =================== MENU ITEM ===================
    [MenuItem("Window/AliTerra AI")]
    public static void ShowWindow()
    {
        var w = GetWindow<AliTerraAI>("AliTerra AI");
        w.minSize = new Vector2(300, 400);
        w.Show();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        AddActivity("Plugin enabled. Click Connect to start.");
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!isConnected) return;
        
        double now = EditorApplication.timeSinceStartup;
        
        // Poll for commands
        if (!isPolling && now - lastPollTime > POLL_INTERVAL)
        {
            lastPollTime = now;
            EditorCoroutineUtility.StartCoroutine(PollCommands(), this);
        }
        
        // Flush logs
        if (now - lastLogFlushTime > LOG_FLUSH_INTERVAL && pendingLogs.Count > 0)
        {
            lastLogFlushTime = now;
            EditorCoroutineUtility.StartCoroutine(FlushLogs(), this);
        }
        
        // Sync files periodically
        if (!isSyncing && now - lastSyncTime > SYNC_INTERVAL)
        {
            lastSyncTime = now;
            EditorCoroutineUtility.StartCoroutine(SyncProjectFiles(), this);
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string logType = type switch
        {
            LogType.Error => "error",
            LogType.Exception => "exception",
            LogType.Warning => "warning",
            _ => "log"
        };

        bool isCompError = logType == "error" &&
            (logString.Contains("CS0") || logString.Contains("error CS") ||
             logString.Contains("Assets/Scripts") || stackTrace.Contains(".cs("));

        pendingLogs.Add(new LogEntry
        {
            logType = logType,
            message = logString,
            stackTrace = stackTrace,
            isCompilationError = isCompError
        });

        // Auto-fix trigger
        if (isWaitingForCompilation && (type == LogType.Error || type == LogType.Exception))
        {
            isWaitingForCompilation = false;
            AddActivity($"[AutoFix] Compilation error detected! Queuing auto-fix...");
            // The server orchestrator will handle fix via read_console_logs tool
        }
    }

    // =================== GUI ==========================
    private Vector2 scrollPos;

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        
        // Header
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🤖 AliTerra AI", headerStyle);
        EditorGUILayout.Space(4);

        // Status
        var statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = isConnected ? Color.green : Color.gray }
        };
        EditorGUILayout.LabelField(statusMessage, statusStyle);
        EditorGUILayout.Space(8);

        // Connect / Disconnect
        if (!isConnected)
        {
            if (GUILayout.Button("⚡ Connect to AliTerra Server", GUILayout.Height(36)))
            {
                EditorCoroutineUtility.StartCoroutine(Connect(), this);
            }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Sync Files", GUILayout.Height(28)))
            {
                EditorCoroutineUtility.StartCoroutine(SyncProjectFiles(), this);
            }
            if (GUILayout.Button("✕ Disconnect", GUILayout.Height(28)))
            {
                isConnected = false;
                statusMessage = "Disconnected";
                AddActivity("Disconnected from server.");
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Activity Log", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        foreach (var entry in activityLog)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            if (entry.Contains("ERROR") || entry.Contains("error") || entry.Contains("AutoFix"))
                style.normal.textColor = new Color(1f, 0.4f, 0.4f);
            else if (entry.Contains("✓") || entry.Contains("success"))
                style.normal.textColor = new Color(0.4f, 1f, 0.6f);
            else if (entry.Contains("→"))
                style.normal.textColor = new Color(0.5f, 0.8f, 1f);
                
            EditorGUILayout.LabelField(entry, style);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Server: {SERVER_URL}", EditorStyles.miniLabel);
    }

    // ================ CONNECT =========================
    private IEnumerator Connect()
    {
        statusMessage = "Connecting...";
        AddActivity($"→ Connecting to {SERVER_URL}...");
        
        yield return EditorCoroutineUtility.StartCoroutine(SyncProjectFiles(), this);
        
        isConnected = true;
        statusMessage = $"✓ Connected — Polling every {POLL_INTERVAL}s";
        AddActivity("✓ Connected! AliTerra AI is watching your project.");
        lastPollTime = EditorApplication.timeSinceStartup;
        lastSyncTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    // ================ SYNC FILES ======================
    private IEnumerator SyncProjectFiles()
    {
        if (isSyncing) yield break;
        isSyncing = true;
        
        AddActivity("→ Indexing project files...");
        
        var files = new List<Dictionary<string, string>>();
        
        // Index C# scripts
        string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        foreach (var f in csFiles)
        {
            string relativePath = f.Replace(Application.dataPath, "").TrimStart('/','\\\\');
            string content = File.ReadAllText(f);
            if (content.Length > 50000) content = content.Substring(0, 50000) + "\\n// [truncated]";
            files.Add(new Dictionary<string, string>
            {
                { "path", "Assets/" + relativePath.Replace("\\\\", "/") },
                { "type", "cs" },
                { "content", content },
                { "size", content.Length.ToString() }
            });
        }
        
        // Index scenes
        string[] sceneFiles = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
        foreach (var f in sceneFiles)
        {
            string relativePath = f.Replace(Application.dataPath, "").TrimStart('/','\\\\');
            files.Add(new Dictionary<string, string>
            {
                { "path", "Assets/" + relativePath.Replace("\\\\", "/") },
                { "type", "scene" },
                { "size", new FileInfo(f).Length.ToString() }
            });
        }
        
        // Index prefabs
        string[] prefabFiles = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
        foreach (var f in prefabFiles)
        {
            string relativePath = f.Replace(Application.dataPath, "").TrimStart('/','\\\\');
            files.Add(new Dictionary<string, string>
            {
                { "path", "Assets/" + relativePath.Replace("\\\\", "/") },
                { "type", "prefab" },
                { "size", new FileInfo(f).Length.ToString() }
            });
        }

        var payload = new SyncPayload
        {
            files = files.ToArray(),
            unityVersion = Application.unityVersion
        };
        
        string json = JsonUtility.ToJson(payload);
        // Manual serialization for dictionary array
        json = BuildFilesJson(files, Application.unityVersion);
        
        using var req = new UnityWebRequest($"{SERVER_URL}/api/unity/sync", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-api-key", API_KEY);
        
        yield return req.SendWebRequest();
        
        if (req.result == UnityWebRequest.Result.Success)
        {
            AddActivity($"✓ Synced {files.Count} files to server");
        }
        else
        {
            AddActivity($"ERROR syncing: {req.error}");
        }
        
        isSyncing = false;
        Repaint();
    }

    private string BuildFilesJson(List<Dictionary<string, string>> files, string unityVersion)
    {
        var sb = new StringBuilder();
        sb.Append("{\\\"files\\\":[");
        for (int i = 0; i < files.Count; i++)
        {
            var f = files[i];
            sb.Append("{");
            sb.Append($"\\\"path\\\":{EscapeJson(f.GetValueOrDefault("path",""))},");
            sb.Append($"\\\"type\\\":{EscapeJson(f.GetValueOrDefault("type",""))},");
            sb.Append($"\\\"content\\\":{EscapeJson(f.GetValueOrDefault("content",""))},");
            sb.Append($"\\\"size\\\":{f.GetValueOrDefault("size","0")}");
            sb.Append("}");
            if (i < files.Count - 1) sb.Append(",");
        }
        sb.Append($"],\\\"unityVersion\\\":{EscapeJson(unityVersion)}}}");
        return sb.ToString();
    }
    
    private string EscapeJson(string s)
    {
        if (s == null) return "null";
        return "\\"" + s
            .Replace("\\\\", "\\\\\\\\")
            .Replace("\\"", "\\\\\\"")
            .Replace("\\n", "\\\\n")
            .Replace("\\r", "\\\\r")
            .Replace("\\t", "\\\\t")
            + "\\"";
    }

    [Serializable] private class SyncPayload { public object[] files; public string unityVersion; }

    // =============== POLL COMMANDS ====================
    private IEnumerator PollCommands()
    {
        if (isPolling) yield break;
        isPolling = true;
        
        using var req = UnityWebRequest.Get($"{SERVER_URL}/api/unity/commands");
        req.SetRequestHeader("x-api-key", API_KEY);
        yield return req.SendWebRequest();
        
        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<CommandsResponse>(req.downloadHandler.text);
            if (response?.commands != null && response.commands.Length > 0)
            {
                foreach (var cmd in response.commands)
                {
                    AddActivity($"→ Executing: {cmd.command}");
                    yield return EditorCoroutineUtility.StartCoroutine(ExecuteCommand(cmd), this);
                }
                Repaint();
            }
        }
        else if (!req.error.Contains("Cannot connect"))
        {
            // Only log non-connection errors
            AddActivity($"Poll error: {req.error}");
        }
        
        isPolling = false;
    }

    [Serializable] private class CommandsResponse { public PendingCommand[] commands; }
    [Serializable] private class PendingCommand
    {
        public int id;
        public string command;
        public string payload; // JSON string
    }

    // ============= EXECUTE COMMAND ====================
    private IEnumerator ExecuteCommand(PendingCommand cmd)
    {
        string result = "done";
        bool success = true;
        
        try
        {
            switch (cmd.command)
            {
                case "create_script":
                    result = ExecuteCreateScript(cmd.payload);
                    break;
                case "modify_script":
                    result = ExecuteModifyScript(cmd.payload);
                    break;
                case "execute_editor_command":
                    result = ExecuteEditorCommand(cmd.payload);
                    break;
                case "set_object_property":
                    result = "set_object_property queued (requires scene context)";
                    break;
                default:
                    result = $"Command '{cmd.command}' executed";
                    break;
            }
        }
        catch (Exception ex)
        {
            result = $"Error: {ex.Message}";
            success = false;
            AddActivity($"ERROR in {cmd.command}: {ex.Message}");
        }
        
        // Report result back
        var reportJson = $"{{\\\"commandId\\\":{cmd.id},\\\"status\\\":\\\"{(success ? "done" : "error")}\\\",\\\"result\\\":{EscapeJson(result)}}}";
        
        using var req = new UnityWebRequest($"{SERVER_URL}/api/unity/commands", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reportJson));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-api-key", API_KEY);
        yield return req.SendWebRequest();
        
        if (success)
        {
            AddActivity($"✓ {cmd.command}: {result.Substring(0, Math.Min(result.Length, 60))}");
        }
        
        // Refresh assets after file operations
        if (cmd.command == "create_script" || cmd.command == "modify_script")
        {
            AssetDatabase.Refresh();
            isWaitingForCompilation = true;
            autoFixAttempts = 0;
            AddActivity("Waiting for compilation...");
        }
        
        Repaint();
    }

    private string ExecuteCreateScript(string payloadJson)
    {
        // Parse simple JSON manually
        string name = ExtractJsonString(payloadJson, "name");
        string folder = ExtractJsonString(payloadJson, "folder");
        string content = ExtractJsonString(payloadJson, "content");
        
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content))
            return "Error: missing name or content";
        
        string targetFolder = string.IsNullOrEmpty(folder)
            ? Path.Combine(Application.dataPath, "Scripts")
            : Path.Combine(Application.dataPath, "Scripts", folder);
        
        Directory.CreateDirectory(targetFolder);
        
        string filePath = Path.Combine(targetFolder, name + ".cs");
        File.WriteAllText(filePath, content, Encoding.UTF8);
        
        return $"Created {name}.cs in Assets/Scripts/{folder}";
    }

    private string ExecuteModifyScript(string payloadJson)
    {
        string path = ExtractJsonString(payloadJson, "path");
        string content = ExtractJsonString(payloadJson, "content");
        
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(content))
            return "Error: missing path or content";
        
        // path is relative to Assets, convert to full path
        string fullPath = Path.Combine(Application.dataPath, "..", path);
        fullPath = Path.GetFullPath(fullPath);
        
        if (!File.Exists(fullPath))
        {
            // Create parent directory if needed
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "");
        }
        
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        return $"Modified {path}";
    }

    private string ExecuteEditorCommand(string payloadJson)
    {
        string command = ExtractJsonString(payloadJson, "command");
        switch (command)
        {
            case "refresh_assets":
                AssetDatabase.Refresh();
                return "Assets refreshed";
            case "save_scene":
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                return "Scene saved";
            case "play_mode":
                EditorApplication.isPlaying = true;
                return "Entered play mode";
            case "stop_play_mode":
                EditorApplication.isPlaying = false;
                return "Stopped play mode";
            default:
                return $"Unknown command: {command}";
        }
    }

    // ============== FLUSH LOGS ========================
    private IEnumerator FlushLogs()
    {
        if (pendingLogs.Count == 0) yield break;
        
        var logsToSend = new List<LogEntry>(pendingLogs);
        pendingLogs.Clear();
        
        var sb = new StringBuilder();
        sb.Append("{\\\"logs\\\":[");
        for (int i = 0; i < logsToSend.Count; i++)
        {
            var l = logsToSend[i];
            sb.Append("{");
            sb.Append($"\\\"logType\\\":{EscapeJson(l.logType)},");
            sb.Append($"\\\"message\\\":{EscapeJson(l.message)},");
            sb.Append($"\\\"stackTrace\\\":{EscapeJson(l.stackTrace ?? "")},");
            sb.Append($"\\\"isCompilationError\\\":{l.isCompilationError.ToString().ToLower()}");
            sb.Append("}");
            if (i < logsToSend.Count - 1) sb.Append(",");
        }
        sb.Append("]}");
        
        using var req = new UnityWebRequest($"{SERVER_URL}/api/unity/logs", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-api-key", API_KEY);
        yield return req.SendWebRequest();
    }

    // ============== HELPERS ===========================
    private string ExtractJsonString(string json, string key)
    {
        // Simple extraction — find "key":"value"
        string search = $"\\"{key}\\":\\"";
        int start = json.IndexOf(search);
        if (start < 0) return "";
        start += search.Length;
        
        var sb = new StringBuilder();
        bool escape = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escape)
            {
                switch (c)
                {
                    case 'n': sb.Append('\\n'); break;
                    case 'r': sb.Append('\\r'); break;
                    case 't': sb.Append('\\t'); break;
                    case '\\\\': sb.Append('\\\\'); break;
                    case '"': sb.Append('"'); break;
                    default: sb.Append(c); break;
                }
                escape = false;
            }
            else if (c == '\\\\') { escape = true; }
            else if (c == '"') { break; }
            else { sb.Append(c); }
        }
        return sb.ToString();
    }

    private void AddActivity(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        activityLog.Insert(0, timestamped);
        if (activityLog.Count > 100) activityLog.RemoveAt(activityLog.Count - 1);
    }
}

// ============ EditorCoroutineUtility (lightweight) ============
// If you have Unity's Editor Coroutines package, remove this class
// and use Unity.EditorCoroutines.Editor.EditorCoroutineUtility instead
public static class EditorCoroutineUtility
{
    private static List<(IEnumerator routine, object owner)> _running = new();

    public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
    {
        var co = new EditorCoroutine(routine);
        _running.Add((routine, owner));
        EditorApplication.update += co.Tick;
        return co;
    }
}

public class EditorCoroutine
{
    private readonly IEnumerator _routine;
    private bool _done;

    public EditorCoroutine(IEnumerator routine) => _routine = routine;

    public void Tick()
    {
        if (_done) return;
        try
        {
            if (!_routine.MoveNext())
            {
                _done = true;
                EditorApplication.update -= Tick;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AliTerra] Coroutine error: {e}");
            _done = true;
            EditorApplication.update -= Tick;
        }
    }
}
`;
}
