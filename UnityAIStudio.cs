// ═══════════════════════════════════════════════════════════════
// AliTerra AI — Настройки API (сгенерировано автоматически)
// Скопируйте этот блок в AliTerraAI.cs после строки с SERVER_URL
// ═══════════════════════════════════════════════════════════════

// Выбранный провайдер по умолчанию
private const string AI_PROVIDER = "gemini";

// API ключи
private const string GEMINI_API_KEY = "AIzaSyC0zIacbiLc9uJUVwznrEo-dufYvb7l48I";
private const string GEMINI_MODEL = "gemini-2.5-flash";
private const string DEEPSEEK_API_KEY = "sk-c19681771fc84bc396fff4171d80386d";
private const string DEEPSEEK_MODEL = "deepseek-chat";
private const string GROQ_API_KEY = "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";
private const string GROQ_MODEL = "llama-4-scout-17b-16e-instruct";

// ═══════════════════════════════════════════════════════════════
// Метод для отправки запроса (добавьте в класс AliTerraAICoder)
// ═══════════════════════════════════════════════════════════════

private IEnumerator SendToAI(string prompt, System.Action<string> callback)
{
    string url = "";
    string json = "";
    string authHeader = "";
    
    switch (AI_PROVIDER)
    {
        case "gemini":
            url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=" + GEMINI_API_KEY;
            json = "{\"contents\":[{\"parts\":[{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";
            break;
        case "deepseek":
            url = "https://api.deepseek.com/v1/chat/completions";
            json = "{\"model\":\"deepseek-chat\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}]}";
            authHeader = "Bearer " + DEEPSEEK_API_KEY;
            break;
        case "groq":
            url = "https://api.groq.com/openai/v1/chat/completions";
            json = "{\"model\":\"llama-4-scout-17b-16e-instruct\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}]}";
            authHeader = "Bearer " + GROQ_API_KEY;
            break;
    }

    byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
    var req = new UnityWebRequest(url, "POST");
    req.uploadHandler = new UploadHandlerRaw(body);
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");
    if (!string.IsNullOrEmpty(authHeader))
        req.SetRequestHeader("Authorization", authHeader);
    req.timeout = 120;

    yield return req.SendWebRequest();

    if (req.result == UnityWebRequest.Result.Success)
    {
        callback(req.downloadHandler.text);
    }
    else
    {
        callback("Ошибка: " + req.error);
    }
    req.Dispose();
}

private static string EscapeJson(string s)
{
    return s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
}