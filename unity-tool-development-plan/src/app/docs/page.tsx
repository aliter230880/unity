import React from 'react';

const CSharpCode = "using UnityEngine;\n" +
"using UnityEngine.Networking;\n" +
"using System.Collections;\n" +
"using System.Collections.Generic;\n" +
"using System.Text;\n" +
"using System.IO;\n\n" +
"public class AliTerraAI : MonoBehaviour\n" +
"{\n" +
"    [Header(\"Settings\")]\n" +
"    public string serverUrl = \"https://your-app-url.com\";\n" +
"    public string projectName = \"MyUnityProject\";\n\n" +
"    private string projectId;\n" +
"    private bool isWaitingForCompilation = false;\n" +
"    private int autoFixAttempts = 0;\n" +
"    private const int MAX_AUTO_FIX_ATTEMPTS = 3;\n\n" +
"    void OnEnable() {\n" +
"        Application.logMessageReceived += HandleLog;\n" +
"        StartCoroutine(InitializeProject());\n" +
"    }\n\n" +
"    void OnDisable() {\n" +
"        Application.logMessageReceived -= HandleLog;\n" +
"    }\n\n" +
"    IEnumerator InitializeProject() {\n" +
"        Debug.Log(\"[AliTerra AI] Initializing project...\");\n" +
"        WWWForm form = new WWWForm();\n" +
"        form.AddField(\"name\", projectName);\n" +
"        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + \"/api/unity/project\", form)) {\n" +
"            yield return www.SendWebRequest();\n" +
"            if (www.result != UnityWebRequest.Result.Success) {\n" +
"                Debug.LogError(\"[AliTerra AI] Failed to initialize project: \" + www.error);\n" +
"            } else {\n" +
"                var response = JsonUtility.FromJson<ProjectResponse>(www.downloadHandler.text);\n" +
"                projectId = response.projectId;\n" +
"                Debug.Log(\"[AliTerra AI] Project initialized. ID: \" + projectId);\n" +
"            }\n" +
"        }\n" +
"    }\n\n" +
"    public void SendPrompt(string prompt) {\n" +
"        StartCoroutine(SendMessageToAI(prompt, false, \"\"));\n" +
"    }\n\n" +
"    void HandleLog(string logString, String logType) {\n" +
"        if (logType == LogType.Error || logType == LogType.Exception) {\n" +
"            if (isWaitingForCompilation) {\n" +
"                Debug.LogError(\"[AliTerra AI] Error detected. Starting auto-fix...\");\n" +
"                StartCoroutine(RequestAutoFix(logString));\n" +
"            }\n" +
"        }\n" +
"    }\n\n" +
"    IEnumerator RequestAutoFix(string errorLog) {\n" +
"        isWaitingForCompilation = false;\n" +
"        autoFixAttempts++;\n" +
"        if (autoFixAttempts > MAX_AUTO_FIX_ATTEMPTS) {\n" +
"            Debug.LogError(\"[AliTerra AI] Max auto-fix attempts reached.\");\n" +
"            yield break;\n" +
"        }\n" +
"        Debug.Log(\"[AliTerra AI] Auto-fix attempt \" + autoFixAttempts + \"/\" + MAX_AUTO_FIX_ATTEMPTS + \"...\");\n" +
"        yield return StartCoroutine(SendMessageToAI(\"Please fix the error\", true, errorLog));\n" +
"    }\n\n" +
"    IEnumerator SendMessageToAI(string prompt, bool isErrorFix, string errorLog) {\n" +
"        string jsonPayload = \"{\\\"projectId\\\":\\\"\" + projectId + \"\\\", \\\"prompt\\\":\\\"\" + prompt.Replace(\"\\\"\", \"\\\\\\\"\").Replace(\"\\n\", \"\\\\\\\\n\") + \"\\\", \\\"isErrorFix\\\":\" + isErrorFix.ToString().ToLower() + \", \\\"errorLog\\\":\\\"\" + errorLog.Replace(\"\\\"\", \"\\\\\\\"\").Replace(\"\\n\", \"\\\\\\\\n\") + \"\\\"}\";\n" +
"        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);\n" +
"        UnityWebRequest www = new UnityWebRequest(serverUrl + \"/api/unity/chat\", \"POST\");\n" +
"        www.uploadHandler = new UploadHandlerRaw(bodyRaw);\n" +
"        www.downloadHandler = new DownloadHandlerBuffer();\n" +
"        www.SetRequestHeader(\"Content-Type\", \"application/json\");\n" +
"        yield return www.SendWebRequest();\n" +
"        if (www.result != UnityWebRequest.Result.Success) {\n" +
"            Debug.LogError(\"[AliTerra AI] API Error: \" + www.error);\n" +
"        } else {\n" +
"            var response = JsonUtility.FromJson<AgentResponse>(www.downloadHandler.text);\n" +
"            Debug.Log(\"[AliTerra AI] Plan: \" + response.plan);\n" +
"            foreach(var action in response.actions) { ExecuteAction(action); }\n" +
"            Debug.Log(\"[AliTerra AI] \" + response.message);\n" +
"        }\n" +
"    }\n\n" +
"    void ExecuteAction(AIAction action) {\n" +
"        Debug.Log(\"[AliTerra AI] Executing \" + action.type + \"...\");\n" +
"        switch (action.type) {\n" +
"            case \"CREATE_SCRIPT\":\n" +
"            case \"MODIFY_SCRIPT\": ApplyScript(action.paramsData); break;\n" +
"            case \"MODIFY_PROPERTY\": ApplyPropertyChange(action.paramsData); break;\n" +
"            default: Debug.LogWarning(\"[AliTerra AI] Unknown action type: \" + action.type); break;\n" +
"        }\n" +
"    }\n\n" +
"    void ApplyScript(string paramsJson) {\n" +
"        ScriptParams p = JsonUtility.FromJson<ScriptParams>(paramsJson);\n" +
"        string path = Application.dataPath + \"/Scripts/\" + p.name + \".cs\";\n" +
"        string dir = Path.GetDirectoryName(path);\n" +
"        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);\n" +
"        File.WriteAllText(path, p.content);\n" +
"        Debug.Log(\"[AliTerra AI] Script saved to \" + path);\n" +
"        isWaitingForCompilation = true;\n" +
"        autoFixAttempts = 0;\n" +
"        Invoke(\"StopWaitingForCompilation\", 10f);\n" +
"    }\n\n" +
"    void ApplyPropertyChange(string paramsJson) {\n" +
"        PropertyParams p = JsonUtility.FromJson<PropertyParams>(paramsJson);\n" +
"        Debug.Log(\"[AliTerra AI] Changing \" + p.property + \" on \" + p.target + \" to \" + p.value);\n" +
"    }\n\n" +
"    void StopWaitingForCompilation() {\n" +
"        if (isWaitingForCompilation) {\n" +
"            isWaitingForCompilation = false;\n" +
"            Debug.Log(\"[AliTerra AI] Code applied successfully without errors!\");\n" +
"        }\n" +
"    }\n\n" +
"    [System.Serializable] public class ProjectResponse { public string projectId; }\n" +
"    [System.Serializable] public class AgentResponse { public string plan; public string message; public List<AIAction> actions; }\n" +
"    [System.Serializable] public class AIAction { public string type; public string paramsData; }\n" +
"    [System.Serializable] public class ScriptParams { public string name; public string content; }\n" +
"    [System.Serializable] public class PropertyParams { public string target; public string component; public string property; public string value; }\n" +
"}\n";

export default function DocsPage() {
  return (
    <div className="p-8 max-w-4xl mx-auto">
      <h1 className="text-3xl font-bold mb-4">Unity AI Bridge Agent Integration</h1>
      <p className="mb-6 text-gray-600">
        Your plugin has been upgraded from a simple chat-proxy to an <strong>AI Agent Executor</strong>.
      </p>
      
      <div className="bg-blue-50 p-4 rounded-lg mb-8 border border-blue-200">
        <h2 className="text-lg font-semibold mb-2">New Architecture: Orchestrator Pattern</h2>
        <ul className="list-disc ml-6 space-y-2 text-sm">
          <li><strong>Plan-Based Execution:</strong> The AI now returns a multi-step plan and a list of structured actions.</li>
          <li><strong>Action Types:</strong> Supports <code>CREATE_SCRIPT</code>, <code>MODIFY_SCRIPT</code>, and <code>MODIFY_PROPERTY</code>.</li>
          <li><strong>Automatic Feedback:</strong> Errors are captured and sent back as high-priority fix requests.</li>
        </ul>
      </div>

      <div className="mb-8">
        <h2 className="text-lg font-semibold mb-2">Integration Script</h2>
        <div className="relative">
          <pre className="bg-gray-900 text-gray-100 p-4 rounded-lg overflow-x-auto text-sm">
            <code>{CSharpCode}</code>
          </pre>
        </div>
      </div>
    </div>
  );
}
