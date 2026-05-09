import { useState, useEffect } from "react";
import { getFullPluginTemplate } from "./pluginTemplate";

interface ApiProvider {
  id: string;
  name: string;
  logo: string;
  color: string;
  gradient: string;
  description: string;
  getKeyUrl: string;
  endpoint: string;
  models: string[];
  defaultModel: string;
  freeTier: string;
}

const providers: ApiProvider[] = [
  {
    id: "gemini",
    name: "Google Gemini",
    logo: "✨",
    color: "from-blue-500 to-cyan-400",
    gradient: "bg-gradient-to-br from-blue-500 to-cyan-400",
    description: "1M контекст, мощный Gemini 2.5 Flash",
    getKeyUrl: "https://aistudio.google.com/apikey",
    endpoint: "https://generativelanguage.googleapis.com/v1beta",
    models: ["gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash"],
    defaultModel: "gemini-2.5-flash",
    freeTier: "15 RPM / 1,500 в день",
  },
  {
    id: "deepseek",
    name: "DeepSeek",
    logo: "🐋",
    color: "from-emerald-500 to-teal-400",
    gradient: "bg-gradient-to-br from-emerald-500 to-teal-400",
    description: "Дешёвый, отличный для C# кода",
    getKeyUrl: "https://platform.deepseek.com/api_keys",
    endpoint: "https://api.deepseek.com/v1",
    models: ["deepseek-chat", "deepseek-reasoner", "deepseek-coder"],
    defaultModel: "deepseek-chat",
    freeTier: "$5 новых аккаунтам",
  },
  {
    id: "groq",
    name: "Groq",
    logo: "⚡",
    color: "from-orange-500 to-red-400",
    gradient: "bg-gradient-to-br from-orange-500 to-red-400",
    description: "300+ токен/сек, молниеносный",
    getKeyUrl: "https://console.groq.com/keys",
    endpoint: "https://api.groq.com/openai/v1",
    models: [
      "llama-4-scout-17b-16e-instruct",
      "llama-3.3-70b-versatile",
      "qwen/qwen3-32b",
      "moonshotai/kimi-k2-instruct",
    ],
    defaultModel: "llama-4-scout-17b-16e-instruct",
    freeTier: "30 RPM / 1,000 в день",
  },
];

interface StoredKey {
  apiKey: string;
  model: string;
  enabled: boolean;
}

export default function App() {
  const [keys, setKeys] = useState<Record<string, StoredKey>>({});
  const [activeProvider, setActiveProvider] = useState<string>("gemini");
  const [showKeys, setShowKeys] = useState<Record<string, boolean>>({});
  const [copied, setCopied] = useState(false);
  const [testResult, setTestResult] = useState<Record<string, string>>({});
  const [downloadStatus, setDownloadStatus] = useState("");

  // Load from localStorage
  useEffect(() => {
    const saved = localStorage.getItem("aliterra-api-keys");
    if (saved) {
      try {
        setKeys(JSON.parse(saved));
      } catch {}
    }
  }, []);

  // Save to localStorage
  const saveKeys = (newKeys: Record<string, StoredKey>) => {
    setKeys(newKeys);
    localStorage.setItem("aliterra-api-keys", JSON.stringify(newKeys));
  };

  const updateKey = (providerId: string, field: keyof StoredKey, value: any) => {
    const existing = keys[providerId] || {
      apiKey: "",
      model: providers.find((p) => p.id === providerId)?.defaultModel || "",
      enabled: true,
    };
    const updated = {
      ...keys,
      [providerId]: {
        ...existing,
        [field]: value,
      },
    };
    saveKeys(updated);
  };

  const toggleShow = (id: string) => {
    setShowKeys((prev) => ({ ...prev, [id]: !prev[id] }));
  };



  const generateCSharpCode = () => {
    const enabledProviders = providers.filter(
      (p) => keys[p.id]?.enabled && keys[p.id]?.apiKey
    );

    if (enabledProviders.length === 0) {
      return "// Добавьте хотя бы один API ключ выше";
    }

    let code = `// ═══════════════════════════════════════════════════════════════
// AliTerra AI — Настройки API (сгенерировано автоматически)
// Скопируйте этот блок в AliTerraAI.cs после строки с SERVER_URL
// ═══════════════════════════════════════════════════════════════

// Выбранный провайдер по умолчанию
private const string AI_PROVIDER = "${enabledProviders[0].id}";

// API ключи
`;

    for (const p of enabledProviders) {
      const k = keys[p.id];
      const constName = p.id.toUpperCase() + "_API_KEY";
      const modelConst = p.id.toUpperCase() + "_MODEL";
      code += `private const string ${constName} = "${k.apiKey}";\n`;
      code += `private const string ${modelConst} = "${k.model}";\n`;
    }

    code += `
// ═══════════════════════════════════════════════════════════════
// Метод для отправки запроса (добавьте в класс AliTerraAICoder)
// ═══════════════════════════════════════════════════════════════

private IEnumerator SendToAI(string prompt, System.Action<string> callback)
{
    string url = "";
    string json = "";
    string authHeader = "";
    
    switch (AI_PROVIDER)
    {`;

    for (const p of enabledProviders) {
      const k = keys[p.id];
      if (p.id === "gemini") {
        code += `
        case "gemini":
            url = "${p.endpoint}/models/${k.model}:generateContent?key=" + GEMINI_API_KEY;
            json = "{\\"contents\\":[{\\"parts\\":[{\\"text\\":\\"" + EscapeJson(prompt) + "\\"}]}]}";
            break;`;
      } else if (p.id === "deepseek") {
        code += `
        case "deepseek":
            url = "${p.endpoint}/chat/completions";
            json = "{\\"model\\":\\"${k.model}\\",\\"messages\\":[{\\"role\\":\\"user\\",\\"content\\":\\"" + EscapeJson(prompt) + "\\"}]}";
            authHeader = "Bearer " + DEEPSEEK_API_KEY;
            break;`;
      } else if (p.id === "groq") {
        code += `
        case "groq":
            url = "${p.endpoint}/chat/completions";
            json = "{\\"model\\":\\"${k.model}\\",\\"messages\\":[{\\"role\\":\\"user\\",\\"content\\":\\"" + EscapeJson(prompt) + "\\"}]}";
            authHeader = "Bearer " + GROQ_API_KEY;
            break;`;
      }
    }

    code += `
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
    return s.Replace("\\\\", "\\\\\\\\")
            .Replace("\\"", "\\\\\\"")
            .Replace("\\n", "\\\\n")
            .Replace("\\r", "\\\\r")
            .Replace("\\t", "\\\\t");
}`;

    return code;
  };

  const copyToClipboard = async () => {
    const code = generateCSharpCode();
    await navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const downloadPlugin = () => {
    const geminiKey = keys.gemini?.apiKey || "";
    const geminiModel = keys.gemini?.model || "gemini-2.5-flash";
    const deepseekKey = keys.deepseek?.apiKey || "";
    const deepseekModel = keys.deepseek?.model || "deepseek-chat";
    const groqKey = keys.groq?.apiKey || "";
    const groqModel = keys.groq?.model || "llama-4-scout-17b-16e-instruct";

    const template = getFullPluginTemplate(geminiKey, geminiModel, deepseekKey, deepseekModel, groqKey, groqModel);
    
    const blob = new Blob([template], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "UnityAIStudio.cs";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    setDownloadStatus("✅ Скачано!");
    setTimeout(() => setDownloadStatus(""), 3000);
  };

  const testKey = async (providerId: string) => {
    const k = keys[providerId];
    if (!k?.apiKey) return;

    setTestResult((prev) => ({ ...prev, [providerId]: "⏳ Проверка..." }));

    try {
      const p = providers.find((pp) => pp.id === providerId)!;
      let response: Response;

      if (providerId === "gemini") {
        response = await fetch(
          `${p.endpoint}/models/${k.model}:generateContent?key=${k.apiKey}`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              contents: [{ parts: [{ text: "Say OK" }] }],
            }),
          }
        );
      } else {
        response = await fetch(`${p.endpoint}/chat/completions`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${k.apiKey}`,
          },
          body: JSON.stringify({
            model: k.model,
            messages: [{ role: "user", content: "Say OK" }],
            max_tokens: 10,
          }),
        });
      }

      if (response.ok) {
        setTestResult((prev) => ({ ...prev, [providerId]: "✅ Ключ работает!" }));
      } else {
        setTestResult((prev) => ({
          ...prev,
          [providerId]: `❌ Ошибка ${response.status}`,
        }));
      }
    } catch {
      setTestResult((prev) => ({
        ...prev,
        [providerId]: "❌ Ошибка сети",
      }));
    }
  };

  const enabledCount = providers.filter(
    (p) => keys[p.id]?.enabled && keys[p.id]?.apiKey
  ).length;

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      {/* Header */}
      <header className="border-b border-slate-700/50 bg-slate-900/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-5xl mx-auto px-4 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-violet-500/25">
              <span className="text-xl">🤖</span>
            </div>
            <div>
              <h1 className="text-lg font-bold text-white">Unity AI Studio Pro</h1>
              <p className="text-xs text-slate-400">API Keys Manager для Unity Editor</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span
              className={`px-3 py-1 rounded-full text-xs font-medium ${
                enabledCount > 0
                  ? "bg-emerald-500/20 text-emerald-400 border border-emerald-500/30"
                  : "bg-slate-700 text-slate-400 border border-slate-600"
              }`}
            >
              {enabledCount > 0
                ? `${enabledCount} ключ${enabledCount > 1 ? "а" : ""} активн${enabledCount > 1 ? "ы" : "о"}`
                : "Нет активных ключей"}
            </span>
          </div>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 py-8 space-y-8">
        {/* Provider Cards */}
        <section>
          <h2 className="text-xl font-semibold text-white mb-4 flex items-center gap-2">
            <span>🔑</span> API Ключи
          </h2>
          <div className="grid gap-4 md:grid-cols-3">
            {providers.map((p) => {
              const k = keys[p.id] || {
                apiKey: "",
                model: p.defaultModel,
                enabled: true,
              };
              const isActive = activeProvider === p.id;

              return (
                <div
                  key={p.id}
                  onClick={() => setActiveProvider(p.id)}
                  className={`relative rounded-2xl border transition-all cursor-pointer ${
                    isActive
                      ? "border-violet-500/50 bg-slate-800/80 shadow-lg shadow-violet-500/10"
                      : "border-slate-700/50 bg-slate-800/40 hover:bg-slate-800/60"
                  }`}
                >
                  {/* Provider Header */}
                  <div className="p-4 pb-3">
                    <div className="flex items-center justify-between mb-2">
                      <div className="flex items-center gap-2">
                        <span
                          className={`w-8 h-8 rounded-lg ${p.gradient} flex items-center justify-center text-lg shadow-md`}
                        >
                          {p.logo}
                        </span>
                        <div>
                          <h3 className="font-semibold text-white text-sm">
                            {p.name}
                          </h3>
                          <p className="text-[10px] text-slate-400">
                            {p.freeTier}
                          </p>
                        </div>
                      </div>
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          checked={k.enabled !== false}
                          onChange={(e) =>
                            updateKey(p.id, "enabled", e.target.checked)
                          }
                          className="sr-only peer"
                        />
                        <div className="w-9 h-5 bg-slate-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-emerald-500"></div>
                      </label>
                    </div>
                    <p className="text-xs text-slate-400 mb-3">
                      {p.description}
                    </p>
                  </div>

                  {/* API Key Input */}
                  <div className="px-4 pb-3">
                    <label className="text-[10px] uppercase tracking-wider text-slate-500 mb-1 block">
                      API Key
                    </label>
                    <div className="relative">
                      <input
                        type={showKeys[p.id] ? "text" : "password"}
                        value={k.apiKey}
                        onChange={(e) =>
                          updateKey(p.id, "apiKey", e.target.value)
                        }
                        placeholder="sk-..."
                        className="w-full bg-slate-900/50 border border-slate-600/50 rounded-lg px-3 py-2 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-violet-500/50 focus:ring-1 focus:ring-violet-500/25 pr-16"
                      />
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          toggleShow(p.id);
                        }}
                        className="absolute right-2 top-1/2 -translate-y-1/2 text-xs text-slate-400 hover:text-white transition-colors"
                      >
                        {showKeys[p.id] ? "🙈" : "👁️"}
                      </button>
                    </div>
                  </div>

                  {/* Model Select */}
                  <div className="px-4 pb-3">
                    <label className="text-[10px] uppercase tracking-wider text-slate-500 mb-1 block">
                      Модель
                    </label>
                    <select
                      value={k.model}
                      onChange={(e) =>
                        updateKey(p.id, "model", e.target.value)
                      }
                      className="w-full bg-slate-900/50 border border-slate-600/50 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-violet-500/50 appearance-none cursor-pointer"
                    >
                      {p.models.map((m) => (
                        <option key={m} value={m}>
                          {m}
                        </option>
                      ))}
                    </select>
                  </div>

                  {/* Actions */}
                  <div className="px-4 pb-4 flex gap-2">
                    <a
                      href={p.getKeyUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={(e) => e.stopPropagation()}
                      className="flex-1 text-center py-1.5 rounded-lg bg-slate-700/50 text-xs text-slate-300 hover:bg-slate-700 transition-colors"
                    >
                      Получить ключ →
                    </a>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        testKey(p.id);
                      }}
                      disabled={!k.apiKey}
                      className="px-3 py-1.5 rounded-lg bg-slate-700/50 text-xs text-slate-300 hover:bg-slate-700 transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                    >
                      Тест
                    </button>
                  </div>

                  {/* Test Result */}
                  {testResult[p.id] && (
                    <div className="px-4 pb-3">
                      <p className="text-xs text-slate-400">
                        {testResult[p.id]}
                      </p>
                    </div>
                  )}

                  {/* Active Indicator */}
                  {isActive && (
                    <div className="absolute -top-px -right-px w-3 h-3">
                      <div className="w-3 h-3 rounded-full bg-violet-500 animate-pulse"></div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </section>

        {/* Generated Code */}
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-semibold text-white flex items-center gap-2">
              <span>📝</span> Код для Unity плагина
            </h2>
            <div className="flex gap-2">
              <button
                onClick={copyToClipboard}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-all ${
                  copied
                    ? "bg-emerald-500 text-white"
                    : "bg-slate-700 text-slate-200 hover:bg-slate-600"
                }`}
              >
                {copied ? "✅ Скопировано!" : "📋 Копировать"}
              </button>
              <button
                onClick={downloadPlugin}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-all ${
                  downloadStatus.includes("✅")
                    ? "bg-emerald-500 text-white"
                    : "bg-violet-600 text-white hover:bg-violet-500"
                }`}
              >
                {downloadStatus || "⬇️ Скачать UnityAIStudio.cs"}
              </button>
            </div>
          </div>

          <div className="rounded-2xl border border-slate-700/50 bg-slate-900/80 overflow-hidden">
            <div className="flex items-center gap-2 px-4 py-2 border-b border-slate-700/50 bg-slate-800/50">
              <div className="flex gap-1.5">
                <div className="w-3 h-3 rounded-full bg-red-500/60"></div>
                <div className="w-3 h-3 rounded-full bg-yellow-500/60"></div>
                <div className="w-3 h-3 rounded-full bg-green-500/60"></div>
              </div>
              <span className="text-xs text-slate-400 ml-2">
                AliTerraAI_Settings.cs
              </span>
            </div>
            <pre className="p-4 overflow-x-auto max-h-[500px] overflow-y-auto">
              <code className="text-sm text-slate-300 font-mono whitespace-pre-wrap">
                {generateCSharpCode()}
              </code>
            </pre>
          </div>
        </section>

        {/* Instructions */}
        <section className="rounded-2xl border border-slate-700/50 bg-slate-800/40 p-6">
          <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <span>📖</span> Как использовать
          </h2>
          <ol className="space-y-3 text-sm text-slate-300">
            <li className="flex gap-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-violet-500/20 text-violet-400 flex items-center justify-center text-xs font-bold">
                1
              </span>
              <span>
                Получите API ключи на сайтах провайдеров (кнопка "Получить ключ")
              </span>
            </li>
            <li className="flex gap-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-violet-500/20 text-violet-400 flex items-center justify-center text-xs font-bold">
                2
              </span>
              <span>
                Вставьте ключи выше и выберите модели. Нажмите "Тест" для проверки
              </span>
            </li>
            <li className="flex gap-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-violet-500/20 text-violet-400 flex items-center justify-center text-xs font-bold">
                3
              </span>
              <span>
                Нажмите <strong>"Скачать UnityAIStudio.cs"</strong> — плагин скачается с вашими ключами!
              </span>
            </li>
            <li className="flex gap-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-violet-500/20 text-violet-400 flex items-center justify-center text-xs font-bold">
                4
              </span>
              <span>
                Поместите файл в{" "}
                <code className="px-1.5 py-0.5 rounded bg-slate-700 text-violet-300">
                  Assets/Editor/UnityAIStudio.cs
                </code>
              </span>
            </li>
            <li className="flex gap-3">
              <span className="flex-shrink-0 w-6 h-6 rounded-full bg-violet-500/20 text-violet-400 flex items-center justify-center text-xs font-bold">
                5
              </span>
              <span>
                Откройте <strong>Unity → Window → AI Studio Pro</strong> (Ctrl+Shift+Q) и используйте!
              </span>
            </li>
          </ol>

          {/* Features */}
          <div className="mt-6 pt-4 border-t border-slate-700/50">
            <h3 className="text-sm font-semibold text-white mb-3">🚀 Возможности плагина:</h3>
            <div className="grid grid-cols-2 gap-2 text-xs text-slate-400">
              <div>💬 AI чат с 3 провайдерами</div>
              <div>📁 Файловый менеджер</div>
              <div>🔍 Анализ сцены и иерархии</div>
              <div>📊 Анализ производительности</div>
              <div>✏️ Встроенный редактор кода</div>
              <div>🔎 Глобальный поиск</div>
              <div>🔗 Анализ зависимостей</div>
              <div>🔧 Batch инструменты</div>
              <div>🐙 GitHub интеграция</div>
              <div>⚡ Auto-apply режим</div>
            </div>
          </div>
        </section>

        {/* Footer */}
        <footer className="text-center py-6">
          <p className="text-xs text-slate-500">
            Unity AI Studio Pro v1.0 — Полноценный AI-ассистент для Unity
          </p>
          <p className="text-[10px] text-slate-600 mt-1">
            Ключи хранятся только в вашем браузере (localStorage) и встраиваются в скачиваемый файл
          </p>
        </footer>
      </main>
    </div>
  );
}
