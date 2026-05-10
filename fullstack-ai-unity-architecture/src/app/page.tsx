"use client";

import { useState, useEffect, useRef } from "react";
import { UnityDashboard } from "@/components/UnityDashboard";

interface Message {
  id: string;
  role: string;
  content: string | null;
  toolCalls?: any;
  createdAt: string;
}

interface Session {
  id: string;
  title: string;
  createdAt: string;
}

interface Project {
  id: string;
  name: string;
  apiKey: string;
  createdAt: string;
}

// AI Provider presets
const AI_PROVIDERS = {
  groq: {
    name: "Groq (FREE)",
    models: ["llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768"],
    icon: "⚡",
    envKey: "GROQ_API_KEY",
    description: "Бесплатный, очень быстрый",
  },
  deepseek: {
    name: "DeepSeek",
    models: ["deepseek-chat", "deepseek-coder"],
    icon: "🧠",
    envKey: "DEEPSEEK_API_KEY",
    description: "Отличный для кода, дешёвый",
  },
  grok: {
    name: "Grok (xAI)",
    models: ["grok-beta", "grok-2"],
    icon: "🤖",
    envKey: "GROK_API_KEY",
    description: "От Elon Musk",
  },
  openai: {
    name: "OpenAI",
    models: ["gpt-4o-mini", "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo"],
    icon: "✨",
    envKey: "OPENAI_API_KEY",
    description: "Премиум качество",
  },
  together: {
    name: "Together AI",
    models: ["meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1"],
    icon: "🔗",
    envKey: "TOGETHER_API_KEY",
    description: "Open-source модели",
  },
  ollama: {
    name: "Ollama (Local)",
    models: ["llama3.1", "codellama", "mistral", "qwen2.5-coder"],
    icon: "🏠",
    envKey: "",
    description: "Локальный, бесплатный",
  },
  custom: {
    name: "Custom",
    models: ["any-model"],
    icon: "⚙️",
    envKey: "",
    description: "Любой OpenAI-совместимый API",
  },
};

type ProviderKey = keyof typeof AI_PROVIDERS;

export default function HomePage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [selectedProject, setSelectedProject] = useState<Project | null>(null);
  const [selectedSession, setSelectedSession] = useState<Session | null>(null);
  const [inputMessage, setInputMessage] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"chat" | "dashboard">("chat");
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // AI Provider state
  const [selectedProvider, setSelectedProvider] = useState<ProviderKey>("groq");
  const [selectedModel, setSelectedModel] = useState(AI_PROVIDERS.groq.models[0]);
  const [customApiKey, setCustomApiKey] = useState("");
  const [customBaseUrl, setCustomBaseUrl] = useState("");
  const [showProviderSettings, setShowProviderSettings] = useState(false);

  // Load projects on mount
  useEffect(() => {
    loadProjects();
    // Load saved provider settings from localStorage
    const savedProvider = localStorage.getItem("ai_provider") as ProviderKey;
    const savedModel = localStorage.getItem("ai_model");
    if (savedProvider && AI_PROVIDERS[savedProvider]) {
      setSelectedProvider(savedProvider);
      if (savedModel) {
        setSelectedModel(savedModel);
      } else {
        setSelectedModel(AI_PROVIDERS[savedProvider].models[0]);
      }
    }
  }, []);

  // Load sessions when project changes
  useEffect(() => {
    if (selectedProject) {
      loadSessions(selectedProject.id);
    }
  }, [selectedProject]);

  // Load messages when session changes
  useEffect(() => {
    if (selectedSession) {
      loadMessages(selectedSession.id);
    }
  }, [selectedSession]);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Save provider settings when changed
  useEffect(() => {
    localStorage.setItem("ai_provider", selectedProvider);
    localStorage.setItem("ai_model", selectedModel);
  }, [selectedProvider, selectedModel]);

  const loadProjects = async () => {
    try {
      const res = await fetch("/api/projects");
      const data = await res.json();
      setProjects(data.projects || []);
      if (data.projects?.length > 0 && !selectedProject) {
        setSelectedProject(data.projects[0]);
      }
    } catch (err) {
      console.error("Failed to load projects:", err);
    }
  };

  const loadSessions = async (projectId: string) => {
    try {
      const res = await fetch(`/api/sessions?projectId=${projectId}`);
      const data = await res.json();
      setSessions(data.sessions || []);
      if (data.sessions?.length > 0) {
        setSelectedSession(data.sessions[0]);
      }
    } catch (err) {
      console.error("Failed to load sessions:", err);
    }
  };

  const loadMessages = async (sessionId: string) => {
    try {
      const res = await fetch(`/api/chat?sessionId=${sessionId}`);
      const data = await res.json();
      setMessages(data.messages || []);
    } catch (err) {
      console.error("Failed to load messages:", err);
    }
  };

  const createNewProject = async () => {
    const name = prompt("Project name:");
    if (!name) return;

    try {
      const res = await fetch("/api/projects", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name }),
      });
      const data = await res.json();
      await loadProjects();
      setSelectedProject(data.project);
    } catch (err) {
      console.error("Failed to create project:", err);
    }
  };

  const createNewSession = async () => {
    if (!selectedProject) return;

    try {
      const res = await fetch("/api/sessions", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ projectId: selectedProject.id }),
      });
      const data = await res.json();
      await loadSessions(selectedProject.id);
      setSelectedSession(data.session);
      setMessages([]);
    } catch (err) {
      console.error("Failed to create session:", err);
    }
  };

  const sendMessage = async () => {
    if (!inputMessage.trim() || !selectedProject || isLoading) return;

    const userMessage = inputMessage;
    setInputMessage("");
    setIsLoading(true);

    // Add user message to UI immediately
    const tempUserMsg: Message = {
      id: `temp-${Date.now()}`,
      role: "user",
      content: userMessage,
      createdAt: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, tempUserMsg]);

    try {
      const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          sessionId: selectedSession?.id,
          message: userMessage,
          apiKey: selectedProject.apiKey,
          projectName: selectedProject.name,
          provider: selectedProvider,
          customApiKey: customApiKey || undefined,
          customBaseUrl: customBaseUrl || undefined,
          customModel: selectedModel,
        }),
      });

      const data = await res.json();

      if (data.error) {
        throw new Error(data.error);
      }

      // Reload messages to get the full conversation
      if (data.sessionId) {
        setSelectedSession((prev) =>
          prev || { id: data.sessionId, title: "Session", createdAt: new Date().toISOString() }
        );
        await loadMessages(data.sessionId);
      }
    } catch (err: any) {
      console.error("Chat error:", err);
      setMessages((prev) => [
        ...prev,
        {
          id: `error-${Date.now()}`,
          role: "assistant",
          content: `Error: ${err.message}`,
          createdAt: new Date().toISOString(),
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  const downloadPlugin = () => {
    if (!selectedProject) return;
    const url = `/api/plugin/download?apiKey=${selectedProject.apiKey}`;
    window.open(url, "_blank");
  };

  const handleProviderChange = (provider: ProviderKey) => {
    setSelectedProvider(provider);
    setSelectedModel(AI_PROVIDERS[provider].models[0]);
  };

  return (
    <div className="min-h-screen flex flex-col">
      {/* Header */}
      <header className="bg-slate-800/50 border-b border-slate-700 px-6 py-4">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-gradient-to-br from-emerald-400 to-cyan-500 rounded-xl flex items-center justify-center text-xl">
              🎮
            </div>
            <div>
              <h1 className="text-xl font-bold bg-gradient-to-r from-emerald-400 to-cyan-400 bg-clip-text text-transparent">
                AliTerra AI
              </h1>
              <p className="text-xs text-slate-400">Unity Fullstack Developer</p>
            </div>
          </div>

          <div className="flex items-center gap-4">
            {/* AI Provider Selector */}
            <div className="relative">
              <button
                onClick={() => setShowProviderSettings(!showProviderSettings)}
                className="flex items-center gap-2 bg-slate-700 border border-slate-600 rounded-lg px-3 py-2 text-sm hover:bg-slate-600 transition-colors"
              >
                <span>{AI_PROVIDERS[selectedProvider].icon}</span>
                <span>{AI_PROVIDERS[selectedProvider].name}</span>
                <span className="text-slate-400">|</span>
                <span className="text-slate-400 text-xs">{selectedModel}</span>
                <span>▼</span>
              </button>

              {/* Provider Dropdown */}
              {showProviderSettings && (
                <div className="absolute right-0 top-full mt-2 w-80 bg-slate-800 border border-slate-700 rounded-xl shadow-2xl z-50 p-4">
                  <h3 className="font-semibold mb-3">🤖 Выбери AI модель</h3>

                  {/* Provider Selection */}
                  <div className="space-y-2 mb-4">
                    {Object.entries(AI_PROVIDERS).map(([key, provider]) => (
                      <button
                        key={key}
                        onClick={() => handleProviderChange(key as ProviderKey)}
                        className={`w-full text-left px-3 py-2 rounded-lg flex items-center gap-3 transition-colors ${
                          selectedProvider === key
                            ? "bg-emerald-600/20 border border-emerald-600/30"
                            : "hover:bg-slate-700"
                        }`}
                      >
                        <span className="text-xl">{provider.icon}</span>
                        <div className="flex-1">
                          <div className="font-medium text-sm">{provider.name}</div>
                          <div className="text-xs text-slate-400">{provider.description}</div>
                        </div>
                        {selectedProvider === key && (
                          <span className="text-emerald-400">✓</span>
                        )}
                      </button>
                    ))}
                  </div>

                  {/* Model Selection */}
                  <div className="mb-4">
                    <label className="text-xs text-slate-400 block mb-1">Модель:</label>
                    <select
                      value={selectedModel}
                      onChange={(e) => setSelectedModel(e.target.value)}
                      className="w-full bg-slate-700 border border-slate-600 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    >
                      {AI_PROVIDERS[selectedProvider].models.map((model) => (
                        <option key={model} value={model}>
                          {model}
                        </option>
                      ))}
                    </select>
                  </div>

                  {/* Custom API Key (for providers that need it) */}
                  {selectedProvider === "custom" && (
                    <>
                      <div className="mb-3">
                        <label className="text-xs text-slate-400 block mb-1">Base URL:</label>
                        <input
                          type="text"
                          value={customBaseUrl}
                          onChange={(e) => setCustomBaseUrl(e.target.value)}
                          placeholder="https://api.example.com/v1"
                          className="w-full bg-slate-700 border border-slate-600 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                        />
                      </div>
                      <div className="mb-3">
                        <label className="text-xs text-slate-400 block mb-1">API Key:</label>
                        <input
                          type="password"
                          value={customApiKey}
                          onChange={(e) => setCustomApiKey(e.target.value)}
                          placeholder="sk-..."
                          className="w-full bg-slate-700 border border-slate-600 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                        />
                      </div>
                    </>
                  )}

                  {/* Info about env keys */}
                  {selectedProvider !== "custom" && selectedProvider !== "ollama" && (
                    <div className="bg-slate-700/50 rounded-lg p-3 text-xs">
                      <p className="text-slate-300 mb-1">
                        💡 Ключ API берётся из переменной окружения:
                      </p>
                      <code className="text-emerald-400">{AI_PROVIDERS[selectedProvider].envKey}</code>
                      <p className="text-slate-400 mt-2">
                        Или вставьте свой ключ ниже:
                      </p>
                      <input
                        type="password"
                        value={customApiKey}
                        onChange={(e) => setCustomApiKey(e.target.value)}
                        placeholder="sk-..."
                        className="w-full bg-slate-600 border border-slate-500 rounded-lg px-3 py-2 mt-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      />
                    </div>
                  )}

                  {selectedProvider === "ollama" && (
                    <div className="bg-slate-700/50 rounded-lg p-3 text-xs">
                      <p className="text-slate-300 mb-1">
                        🏠 Ollama запущен локально на порте 11434
                      </p>
                      <p className="text-slate-400">
                        Убедитесь, что Ollama запущен: <code className="text-emerald-400">ollama serve</code>
                      </p>
                    </div>
                  )}

                  <button
                    onClick={() => setShowProviderSettings(false)}
                    className="w-full mt-3 bg-emerald-600 hover:bg-emerald-500 px-4 py-2 rounded-lg text-sm font-medium transition-colors"
                  >
                    Применить
                  </button>
                </div>
              )}
            </div>

            {/* Project Selector */}
            <select
              value={selectedProject?.id || ""}
              onChange={(e) => {
                const proj = projects.find((p) => p.id === e.target.value);
                setSelectedProject(proj || null);
              }}
              className="bg-slate-700 border border-slate-600 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">Select Project</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>

            <button
              onClick={createNewProject}
              className="bg-emerald-600 hover:bg-emerald-500 px-3 py-2 rounded-lg text-sm font-medium transition-colors"
            >
              + New Project
            </button>

            {selectedProject && (
              <button
                onClick={downloadPlugin}
                className="bg-cyan-600 hover:bg-cyan-500 px-3 py-2 rounded-lg text-sm font-medium transition-colors"
              >
                ⬇️ Download Plugin
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Tab Navigation */}
      <div className="bg-slate-800/30 border-b border-slate-700 px-6">
        <div className="max-w-7xl mx-auto flex gap-1">
          <button
            onClick={() => setActiveTab("chat")}
            className={`px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === "chat"
                ? "border-emerald-400 text-emerald-400"
                : "border-transparent text-slate-400 hover:text-slate-300"
            }`}
          >
            💬 AI Chat
          </button>
          <button
            onClick={() => setActiveTab("dashboard")}
            className={`px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === "dashboard"
                ? "border-emerald-400 text-emerald-400"
                : "border-transparent text-slate-400 hover:text-slate-300"
            }`}
          >
            📊 Dashboard
          </button>
        </div>
      </div>

      {/* Main Content */}
      <main className="flex-1 max-w-7xl mx-auto w-full p-6">
        {!selectedProject ? (
          <div className="flex flex-col items-center justify-center h-96 text-center">
            <div className="text-6xl mb-4">🎮</div>
            <h2 className="text-2xl font-bold mb-2">Welcome to AliTerra AI</h2>
            <p className="text-slate-400 mb-6">
              Create a project to get started with AI-powered Unity development
            </p>
            <button
              onClick={createNewProject}
              className="bg-emerald-600 hover:bg-emerald-500 px-6 py-3 rounded-lg font-medium transition-colors"
            >
              Create Your First Project
            </button>
          </div>
        ) : activeTab === "chat" ? (
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-6 h-[calc(100vh-200px)]">
            {/* Sessions Sidebar */}
            <div className="lg:col-span-1 bg-slate-800/50 rounded-xl border border-slate-700 p-4">
              <div className="flex items-center justify-between mb-4">
                <h3 className="font-semibold text-sm">Sessions</h3>
                <button
                  onClick={createNewSession}
                  className="text-emerald-400 hover:text-emerald-300 text-sm"
                >
                  + New
                </button>
              </div>
              <div className="space-y-2">
                {sessions.map((session) => (
                  <button
                    key={session.id}
                    onClick={() => setSelectedSession(session)}
                    className={`w-full text-left px-3 py-2 rounded-lg text-sm transition-colors ${
                      selectedSession?.id === session.id
                        ? "bg-emerald-600/20 text-emerald-400 border border-emerald-600/30"
                        : "hover:bg-slate-700/50 text-slate-300"
                    }`}
                  >
                    {session.title || "Session"}
                  </button>
                ))}
                {sessions.length === 0 && (
                  <p className="text-slate-500 text-sm text-center py-4">
                    No sessions yet
                  </p>
                )}
              </div>
            </div>

            {/* Chat Area */}
            <div className="lg:col-span-3 bg-slate-800/50 rounded-xl border border-slate-700 flex flex-col">
              {/* Messages */}
              <div className="flex-1 overflow-y-auto p-4 space-y-4">
                {messages.length === 0 && (
                  <div className="flex flex-col items-center justify-center h-full text-center">
                    <div className="text-4xl mb-3">🤖</div>
                    <h3 className="font-semibold mb-1">Start a conversation</h3>
                    <p className="text-slate-400 text-sm">
                      Ask me to create scripts, fix bugs, or build features for your Unity project
                    </p>
                    <div className="mt-4 bg-slate-700/50 rounded-lg p-3 text-xs text-slate-400">
                      <p>Текущая модель: <span className="text-emerald-400">{AI_PROVIDERS[selectedProvider].icon} {selectedModel}</span></p>
                    </div>
                  </div>
                )}

                {messages.map((msg) => (
                  <div
                    key={msg.id}
                    className={`flex ${
                      msg.role === "user" ? "justify-end" : "justify-start"
                    }`}
                  >
                    <div
                      className={`max-w-[80%] rounded-2xl px-4 py-3 ${
                        msg.role === "user"
                          ? "bg-emerald-600 text-white"
                          : msg.role === "tool"
                          ? "bg-slate-700/50 text-slate-300 text-sm font-mono"
                          : "bg-slate-700 text-slate-100"
                      }`}
                    >
                      {msg.role === "tool" && (
                        <div className="text-xs text-slate-400 mb-1">🔧 Tool Result</div>
                      )}
                      <div className="whitespace-pre-wrap text-sm">{msg.content}</div>
                      {msg.toolCalls && msg.toolCalls.length > 0 && (
                        <div className="mt-2 pt-2 border-t border-slate-600">
                          <div className="text-xs text-slate-400 mb-1">Used tools:</div>
                          <div className="flex flex-wrap gap-1">
                            {msg.toolCalls.map((tc: any, i: number) => (
                              <span
                                key={i}
                                className="bg-slate-600/50 px-2 py-0.5 rounded text-xs"
                              >
                                {tc.function?.name}
                              </span>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                ))}

                {isLoading && (
                  <div className="flex justify-start">
                    <div className="bg-slate-700 rounded-2xl px-4 py-3">
                      <div className="flex items-center gap-2">
                        <div className="flex gap-1">
                          <div className="w-2 h-2 bg-emerald-400 rounded-full animate-bounce" />
                          <div
                            className="w-2 h-2 bg-emerald-400 rounded-full animate-bounce"
                            style={{ animationDelay: "0.1s" }}
                          />
                          <div
                            className="w-2 h-2 bg-emerald-400 rounded-full animate-bounce"
                            style={{ animationDelay: "0.2s" }}
                          />
                        </div>
                        <span className="text-sm text-slate-400">AI is thinking...</span>
                      </div>
                    </div>
                  </div>
                )}

                <div ref={messagesEndRef} />
              </div>

              {/* Input Area */}
              <div className="border-t border-slate-700 p-4">
                <div className="flex gap-3">
                  <textarea
                    value={inputMessage}
                    onChange={(e) => setInputMessage(e.target.value)}
                    onKeyDown={handleKeyDown}
                    placeholder="Describe what you want to build..."
                    className="flex-1 bg-slate-900/50 border border-slate-600 rounded-xl px-4 py-3 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                    rows={2}
                    disabled={isLoading}
                  />
                  <button
                    onClick={sendMessage}
                    disabled={isLoading || !inputMessage.trim()}
                    className="bg-emerald-600 hover:bg-emerald-500 disabled:bg-slate-600 disabled:cursor-not-allowed px-6 py-3 rounded-xl font-medium transition-colors"
                  >
                    Send
                  </button>
                </div>

                {/* API Key Display */}
                <div className="mt-3 flex items-center gap-2 text-xs text-slate-500">
                  <span>Unity API Key:</span>
                  <code className="bg-slate-800 px-2 py-1 rounded">
                    {selectedProject.apiKey.substring(0, 20)}...
                  </code>
                  <button
                    onClick={() => navigator.clipboard.writeText(selectedProject.apiKey)}
                    className="text-emerald-400 hover:text-emerald-300"
                  >
                    Copy
                  </button>
                  <span className="mx-2">|</span>
                  <span className="text-emerald-400">
                    {AI_PROVIDERS[selectedProvider].icon} {selectedModel}
                  </span>
                </div>
              </div>
            </div>
          </div>
        ) : (
          <UnityDashboard
            projectId={selectedProject.id}
            apiKey={selectedProject.apiKey}
          />
        )}
      </main>
    </div>
  );
}
