"use client";

import { useState, useEffect } from "react";

interface ProviderConfig {
  key: string;
  name: string;
  icon: string;
  models: string[];
  description: string;
  supportsTools: boolean;
  envKey: string;
  placeholder: string;
  getKeyUrl: string;
}

const PROVIDERS: ProviderConfig[] = [
  {
    key: "groq",
    name: "Groq",
    icon: "⚡",
    models: ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "llama3-8b-8192", "mixtral-8x7b-32768", "gemma2-9b-it"],
    description: "Бесплатный! Очень быстрый",
    supportsTools: true,
    envKey: "GROQ_API_KEY",
    placeholder: "gsk_...",
    getKeyUrl: "https://console.groq.com/keys",
  },
  {
    key: "deepseek",
    name: "DeepSeek",
    icon: "🧠",
    models: ["deepseek-chat", "deepseek-coder"],
    description: "Отличный для кода, дешёвый",
    supportsTools: true,
    envKey: "DEEPSEEK_API_KEY",
    placeholder: "sk-...",
    getKeyUrl: "https://platform.deepseek.com/api_keys",
  },
  {
    key: "grok",
    name: "Grok (xAI)",
    icon: "🤖",
    models: ["grok-beta", "grok-2"],
    description: "От Elon Musk",
    supportsTools: true,
    envKey: "GROK_API_KEY",
    placeholder: "xai-...",
    getKeyUrl: "https://console.x.ai",
  },
  {
    key: "openai",
    name: "OpenAI",
    icon: "✨",
    models: ["gpt-4o-mini", "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo"],
    description: "Премиум качество",
    supportsTools: true,
    envKey: "OPENAI_API_KEY",
    placeholder: "sk-...",
    getKeyUrl: "https://platform.openai.com/api-keys",
  },
  {
    key: "together",
    name: "Together AI",
    icon: "🔗",
    models: ["meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1"],
    description: "Open-source модели",
    supportsTools: true,
    envKey: "TOGETHER_API_KEY",
    placeholder: "...",
    getKeyUrl: "https://api.together.xyz/settings/api-keys",
  },
  {
    key: "ollama",
    name: "Ollama (Local)",
    icon: "🏠",
    models: ["llama3.1", "codellama", "mistral", "qwen2.5-coder"],
    description: "Локальный, полностью бесплатный",
    supportsTools: false,
    envKey: "",
    placeholder: "Не нужен",
    getKeyUrl: "https://ollama.com",
  },
  {
    key: "custom",
    name: "Custom",
    icon: "⚙️",
    models: ["any-model"],
    description: "Любой OpenAI-совместимый API",
    supportsTools: true,
    envKey: "",
    placeholder: "sk-...",
    getKeyUrl: "",
  },
];

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentProvider: string;
  currentModel: string;
  onSave: (provider: string, model: string, apiKey: string, baseUrl: string) => void;
}

export function SettingsModal({
  isOpen,
  onClose,
  currentProvider,
  currentModel,
  onSave,
}: SettingsModalProps) {
  const [selectedProvider, setSelectedProvider] = useState(currentProvider);
  const [selectedModel, setSelectedModel] = useState(currentModel);
  const [apiKey, setApiKey] = useState("");
  const [customBaseUrl, setCustomBaseUrl] = useState("");
  const [showKey, setShowKey] = useState(false);
  const [savedMessage, setSavedMessage] = useState("");

  // Load saved settings when modal opens
  useEffect(() => {
    if (isOpen) {
      const savedKeys = localStorage.getItem("ai_api_keys");
      const savedProvider = localStorage.getItem("ai_provider") || currentProvider;
      const savedModel = localStorage.getItem("ai_model") || currentModel;
      const savedBaseUrl = localStorage.getItem("ai_custom_base_url") || "";

      setSelectedProvider(savedProvider);
      setSelectedModel(savedModel);
      setCustomBaseUrl(savedBaseUrl);

      if (savedKeys) {
        const keys = JSON.parse(savedKeys);
        setApiKey(keys[savedProvider] || "");
      }
    }
  }, [isOpen, currentProvider, currentModel]);

  // Update model when provider changes
  useEffect(() => {
    const provider = PROVIDERS.find((p) => p.key === selectedProvider);
    if (provider && !provider.models.includes(selectedModel)) {
      setSelectedModel(provider.models[0]);
    }

    // Load API key for this provider
    const savedKeys = localStorage.getItem("ai_api_keys");
    if (savedKeys) {
      const keys = JSON.parse(savedKeys);
      setApiKey(keys[selectedProvider] || "");
    }
  }, [selectedProvider]);

  const handleSave = () => {
    // Save API key to localStorage
    const savedKeys = localStorage.getItem("ai_api_keys");
    const keys = savedKeys ? JSON.parse(savedKeys) : {};
    keys[selectedProvider] = apiKey;
    localStorage.setItem("ai_api_keys", JSON.stringify(keys));

    // Save other settings
    localStorage.setItem("ai_provider", selectedProvider);
    localStorage.setItem("ai_model", selectedModel);
    localStorage.setItem("ai_custom_base_url", customBaseUrl);

    onSave(selectedProvider, selectedModel, apiKey, customBaseUrl);

    setSavedMessage("✅ Сохранено!");
    setTimeout(() => {
      setSavedMessage("");
      onClose();
    }, 1000);
  };

  const currentProviderConfig = PROVIDERS.find((p) => p.key === selectedProvider);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative bg-slate-800 rounded-2xl shadow-2xl border border-slate-700 w-full max-w-2xl max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="bg-gradient-to-r from-emerald-600/20 to-cyan-600/20 border-b border-slate-700 px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold">🤖 Настройка AI</h2>
              <p className="text-sm text-slate-400 mt-1">
                Выбери модель и вставь API ключ
              </p>
            </div>
            <button
              onClick={onClose}
              className="text-slate-400 hover:text-white text-2xl"
            >
              ×
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto max-h-[calc(90vh-180px)]">
          {/* Provider Selection */}
          <div className="mb-6">
            <label className="block text-sm font-medium text-slate-300 mb-3">
              Выбери провайдера:
            </label>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {PROVIDERS.map((provider) => (
                <button
                  key={provider.key}
                  onClick={() => setSelectedProvider(provider.key)}
                  className={`relative p-4 rounded-xl border-2 transition-all text-left ${
                    selectedProvider === provider.key
                      ? "border-emerald-500 bg-emerald-500/10"
                      : "border-slate-600 hover:border-slate-500 bg-slate-700/50"
                  }`}
                >
                  {provider.key === "groq" && (
                    <span className="absolute top-2 right-2 text-xs bg-emerald-600 px-2 py-0.5 rounded-full">
                      FREE
                    </span>
                  )}
                  <div className="text-2xl mb-2">{provider.icon}</div>
                  <div className="font-medium text-sm">{provider.name}</div>
                  <div className="text-xs text-slate-400 mt-1">{provider.description}</div>
                </button>
              ))}
            </div>
          </div>

          {/* Model Selection */}
          {currentProviderConfig && (
            <div className="mb-6">
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Модель:
              </label>
              <select
                value={selectedModel}
                onChange={(e) => setSelectedModel(e.target.value)}
                className="w-full bg-slate-700 border border-slate-600 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {currentProviderConfig.models.map((model) => (
                  <option key={model} value={model}>
                    {model}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* API Key Input */}
          {selectedProvider !== "ollama" && (
            <div className="mb-6">
              <div className="flex items-center justify-between mb-2">
                <label className="block text-sm font-medium text-slate-300">
                  API ключ:
                </label>
                {currentProviderConfig?.getKeyUrl && (
                  <a
                    href={currentProviderConfig.getKeyUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-xs text-emerald-400 hover:text-emerald-300"
                  >
                    🔗 Получить ключ →
                  </a>
                )}
              </div>
              <div className="relative">
                <input
                  type={showKey ? "text" : "password"}
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder={currentProviderConfig?.placeholder || "sk-..."}
                  className="w-full bg-slate-700 border border-slate-600 rounded-xl px-4 py-3 pr-12 focus:outline-none focus:ring-2 focus:ring-emerald-500 font-mono"
                />
                <button
                  onClick={() => setShowKey(!showKey)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
                >
                  {showKey ? "🙈" : "👁️"}
                </button>
              </div>

              {/* Key saved indicator */}
              {apiKey && (
                <div className="mt-2 flex items-center gap-2 text-xs text-emerald-400">
                  <span>✓</span>
                  <span>Ключ введён</span>
                </div>
              )}
            </div>
          )}

          {/* Custom Base URL */}
          {selectedProvider === "custom" && (
            <div className="mb-6">
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Base URL:
              </label>
              <input
                type="text"
                value={customBaseUrl}
                onChange={(e) => setCustomBaseUrl(e.target.value)}
                placeholder="https://api.example.com/v1"
                className="w-full bg-slate-700 border border-slate-600 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
          )}

          {/* Ollama Info */}
          {selectedProvider === "ollama" && (
            <div className="mb-6 bg-slate-700/50 rounded-xl p-4">
              <h4 className="font-medium mb-2">🏠 Ollama — локальный AI</h4>
              <div className="text-sm text-slate-300 space-y-2">
                <p>1. Установи Ollama: <code className="bg-slate-600 px-2 py-0.5 rounded">ollama.com</code></p>
                <p>2. Загрузи модель: <code className="bg-slate-600 px-2 py-0.5 rounded">ollama pull llama3.1</code></p>
                <p>3. Запусти сервер: <code className="bg-slate-600 px-2 py-0.5 rounded">ollama serve</code></p>
                <p className="text-yellow-400 mt-2">⚠️ Tool use не поддерживается в Ollama</p>
              </div>
            </div>
          )}

          {/* Features Info */}
          <div className="bg-slate-700/30 rounded-xl p-4">
            <h4 className="font-medium mb-3 text-sm">📊 Возможности модели:</h4>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div className="flex items-center gap-2">
                <span className={currentProviderConfig?.supportsTools ? "text-emerald-400" : "text-red-400"}>
                  {currentProviderConfig?.supportsTools ? "✅" : "❌"}
                </span>
                <span>Tool Use (Function Calling)</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-emerald-400">✅</span>
                <span>Генерация кода C#</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-emerald-400">✅</span>
                <span>Анализ проекта</span>
              </div>
              <div className="flex items-center gap-2">
                <span className={selectedProvider === "groq" ? "text-emerald-400" : "text-slate-400"}>
                  {selectedProvider === "groq" ? "⚡" : "•"}
                </span>
                <span>{selectedProvider === "groq" ? "Самый быстрый!" : "Обычная скорость"}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="border-t border-slate-700 px-6 py-4 flex items-center justify-between">
          <div>
            {savedMessage && (
              <span className="text-emerald-400 text-sm">{savedMessage}</span>
            )}
          </div>
          <div className="flex gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 rounded-xl text-slate-300 hover:bg-slate-700 transition-colors"
            >
              Отмена
            </button>
            <button
              onClick={handleSave}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-500 rounded-xl font-medium transition-colors"
            >
              💾 Сохранить
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
