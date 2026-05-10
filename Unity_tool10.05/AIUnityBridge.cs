using UnityEngine;
using UnityEditor;
using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;

[CustomEditor(typeof(AIUnityBridge))]
public class AIUnityBridge : EditorWindow
{
    private static HttpListener listener;
    private static bool isRunning = false;
    private static int port = 8080;

    [MenuItem("AI/Start Bridge Server")]
    public static void StartServer()
    {
        if (isRunning) return;
        
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        isRunning = true;
        
        // Запускаем прослушивание в отдельном потоке, чтобы Unity не завис
        System.Threading.ThreadPool.QueueUserWorkItem(Listen);
        Debug.Log($"🚀 AI Bridge Server started on port {port}...");
    }

    [MenuItem("AI/Stop Bridge Server")]
    public static void StopServer()
    {
        if (listener != null)
        {
            listener.Stop();
            listener = null;
        }
        isRunning = false;
        Debug.Log("🛑 AI Bridge Server stopped.");
    }

    private static void Listen()
    {
        while (isRunning)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                
                string command = request.QueryString["cmd"];
                string path = request.QueryString["path"];
                string content = request.QueryString["content"];

                string responseText = HandleCommand(command, path, content);
                
                byte[] buffer = Encoding.UTF8.GetBytes(responseText);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                if (isRunning) Debug.LogError($"Bridge Error: {e.Message}");
            }
        }
    }

    private static string HandleCommand(string cmd, string path, string content)
    {
        switch (cmd)
        {
            case "read":
                if (File.Exists(path)) return File.ReadAllText(path);
                return "Error: File not found";

            case "write":
                try {
                    File.WriteAllText(path, content);
                    AssetDatabase.Refresh(); // Чтобы Unity увидел изменения
                    return "Success: File written";
                } catch (Exception e) { return "Error: " + e.Message; }

            case "errors":
                return GetConsoleErrors();

            default:
                return "Error: Unknown command";
        }
    }

    private static string GetConsoleErrors()
    {
        // В Unity нет прямого API для получения списка ошибок консоли в реальном времени,
        // поэтому мы читаем лог-файл Unity.
        string logPath = Application.sLogFilePath; // путь к Editor.log
        if (File.Exists(logPath))
        {
            string fullLog = File.ReadAllText(logPath);
            // Берем последние 20 строк, где есть слово "Error"
            string[] lines = fullLog.Split('\n');
            List<string> errors = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && errors.Count < 10; i--)
            {
                if (lines[i].Contains("Error") || lines[i].Contains("Exception"))
                    errors.Add(lines[i]);
            }
            return string.Join(" | ", errors);
        }
        return "No errors found";
    }
}