"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import {
  BotMessageSquare,
  FolderOpen,
  Terminal,
  Plus,
  Cpu,
  ChevronRight,
  Copy,
  Check,
  AlertTriangle,
  CheckCircle2,
  Loader2,
  Send,
  Code2,
  Wrench,
  Zap,
  FileCode2,
  RefreshCw,
  X,
  Settings,
  Book,
  PackageOpen,
} from "lucide-react";

interface Project {
  id: string;
  name: string;
  unityVersion: string | null;
  apiKey: string;
  lastSeen: Date | string | null;
  createdAt: Date | string | null;
  defaultSessionId: string | null;
}

interface Message {
  id: number;
  sessionId: string;
  role: string;
  content: string | null;
  toolCalls: unknown;
  toolCallId: string | null;
  toolName: string | null;
  createdAt: Date | string | null;
}

interface Session {
  id: string;
  projectId: string;
  title: string;
  createdAt: Date | string | null;
}

export function UnityDashboard({ initialProjects }: { initialProjects: Project[] }) {
  const [projects, setProjects] = useState<Project[]>(initialProjects);
  const [activeProject, setActiveProject] = useState<Project | null>(
    initialProjects[0] ?? null
  );
  const [activeSessionId, setActiveSessionId] = useState<string | null>(
    initialProjects[0]?.defaultSessionId ?? null
  );
  const [sessions, setSessions] = useState<Session[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"chat" | "files" | "logs" | "setup">("chat");
  const [showNewProject, setShowNewProject] = useState(false);
  const [newProjectName, setNewProjectName] = useState("");
  const [newProjectVersion, setNewProjectVersion] = useState("2022.3");
  const [projectFiles, setProjectFiles] = useState<{ path: string; type: string }[]>([]);
  const [consoleLogs, setConsoleLogs] = useState<{ logType: string; message: string; createdAt: string | Date | null }[]>([]);
  const [copiedKey, setCopiedKey] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  useEffect(() => {
    if (activeProject) {
      loadSessions(activeProject.id);
      loadFiles(activeProject.id);
      loadLogs(activeProject.id);
    }
  }, [activeProject]);

  useEffect(() => {
    if (activeSessionId) {
      loadMessages(activeSessionId);
    }
  }, [activeSessionId]);

  async function loadSessions(projectId: string) {
    const res = await fetch(`/api/sessions?projectId=${projectId}`);
    const data = await res.json() as Session[];
    setSessions(data);
  }

  async function loadMessages(sessionId: string) {
    const res = await fetch(`/api/sessions?sessionId=${sessionId}`);
    const data = await res.json() as Message[];
    setMessages(data);
  }

  async function loadFiles(projectId: string) {
    const res = await fetch(`/api/projects?id=${projectId}`);
    if (!res.ok) return;
    // Files come from project files endpoint
    const pRes = await fetch(`/api/unity/files?projectId=${projectId}`);
    if (pRes.ok) {
      const data = await pRes.json() as { path: string; type: string }[];
      setProjectFiles(data);
    }
  }

  async function loadLogs(projectId: string) {
    const res = await fetch(`/api/unity/logs-view?projectId=${projectId}`);
    if (res.ok) {
      const data = await res.json() as { logType: string; message: string; createdAt: string | Date | null }[];
      setConsoleLogs(data);
    }
  }

  async function createProject() {
    if (!newProjectName.trim()) return;
    const res = await fetch("/api/projects", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: newProjectName.trim(), unityVersion: newProjectVersion }),
    });
    if (res.ok) {
      const { project, sessionId } = await res.json() as { project: Project; sessionId: string };
      const p = { ...project, defaultSessionId: sessionId };
      setProjects((prev) => [...prev, p]);
      setActiveProject(p);
      setActiveSessionId(sessionId);
      setShowNewProject(false);
      setNewProjectName("");
    }
  }

  async function createSession() {
    if (!activeProject) return;
    const res = await fetch("/api/sessions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ projectId: activeProject.id, title: `Session ${sessions.length + 1}` }),
    });
    if (res.ok) {
      const session = await res.json() as Session;
      setSessions((prev) => [...prev, session]);
      setActiveSessionId(session.id);
      setMessages([]);
    }
  }

  async function sendMessage() {
    if (!input.trim() || !activeSessionId || isLoading) return;

    const userText = input.trim();
    setInput("");
    setIsLoading(true);

    // Optimistic update
    const tempMsg: Message = {
      id: Date.now(),
      sessionId: activeSessionId,
      role: "user",
      content: userText,
      toolCalls: null,
      toolCallId: null,
      toolName: null,
      createdAt: new Date(),
    };
    setMessages((prev) => [...prev, tempMsg]);

    try {
      const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: activeSessionId, userMessage: userText }),
      });

      if (res.ok) {
        const data = await res.json() as { messages: Message[] };
        setMessages(data.messages);
      }
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
      // Refresh files and logs after AI actions
      if (activeProject) {
        loadFiles(activeProject.id);
        loadLogs(activeProject.id);
      }
    }
  }

  function copyKey() {
    if (activeProject?.apiKey) {
      navigator.clipboard.writeText(activeProject.apiKey);
      setCopiedKey(true);
      setTimeout(() => setCopiedKey(false), 2000);
    }
  }

  const isPluginOnline =
    activeProject?.lastSeen &&
    Date.now() - new Date(activeProject.lastSeen).getTime() < 30000;

  return (
    <div className="flex h-screen" style={{ background: "var(--bg-primary)" }}>
      {/* LEFT SIDEBAR */}
      <div
        className="flex flex-col w-64 border-r flex-shrink-0"
        style={{ background: "var(--bg-panel)", borderColor: "var(--border)" }}
      >
        {/* Logo */}
        <div
          className="flex items-center gap-3 px-4 py-4 border-b"
          style={{ borderColor: "var(--border)" }}
        >
          <div
            className="flex items-center justify-center w-9 h-9 rounded-lg"
            style={{ background: "linear-gradient(135deg, #4f8ef7, #00d4aa)" }}
          >
            <Cpu size={18} className="text-white" />
          </div>
          <div>
            <div className="font-bold text-sm" style={{ color: "var(--text-primary)" }}>
              AliTerra AI
            </div>
            <div className="text-xs" style={{ color: "var(--text-muted)" }}>
              Unity Developer
            </div>
          </div>
        </div>

        {/* Projects */}
        <div className="flex-1 overflow-y-auto p-3">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs font-semibold uppercase tracking-wider" style={{ color: "var(--text-muted)" }}>
              Projects
            </span>
            <button
              onClick={() => setShowNewProject(true)}
              className="flex items-center justify-center w-5 h-5 rounded hover:opacity-80 transition-opacity"
              style={{ background: "var(--accent)", color: "white" }}
            >
              <Plus size={12} />
            </button>
          </div>

          {showNewProject && (
            <div
              className="mb-3 p-3 rounded-lg border animate-fade-in"
              style={{ background: "var(--bg-card)", borderColor: "var(--accent)" }}
            >
              <input
                autoFocus
                placeholder="Project name"
                value={newProjectName}
                onChange={(e) => setNewProjectName(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && createProject()}
                className="w-full text-sm px-2 py-1.5 rounded mb-2 outline-none"
                style={{
                  background: "var(--bg-secondary)",
                  border: "1px solid var(--border)",
                  color: "var(--text-primary)",
                }}
              />
              <input
                placeholder="Unity version"
                value={newProjectVersion}
                onChange={(e) => setNewProjectVersion(e.target.value)}
                className="w-full text-sm px-2 py-1.5 rounded mb-2 outline-none"
                style={{
                  background: "var(--bg-secondary)",
                  border: "1px solid var(--border)",
                  color: "var(--text-primary)",
                }}
              />
              <div className="flex gap-2">
                <button
                  onClick={createProject}
                  className="flex-1 text-xs py-1.5 rounded font-medium transition-opacity hover:opacity-80"
                  style={{ background: "var(--accent)", color: "white" }}
                >
                  Create
                </button>
                <button
                  onClick={() => setShowNewProject(false)}
                  className="px-2 text-xs rounded"
                  style={{ background: "var(--border)", color: "var(--text-secondary)" }}
                >
                  <X size={12} />
                </button>
              </div>
            </div>
          )}

          <div className="space-y-1">
            {projects.map((p) => (
              <button
                key={p.id}
                onClick={() => {
                  setActiveProject(p);
                  setActiveSessionId(p.defaultSessionId);
                  setMessages([]);
                }}
                className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-left text-sm transition-all"
                style={{
                  background: activeProject?.id === p.id ? "var(--bg-card)" : "transparent",
                  color: activeProject?.id === p.id ? "var(--text-primary)" : "var(--text-secondary)",
                  border: activeProject?.id === p.id ? "1px solid var(--border)" : "1px solid transparent",
                }}
              >
                <PackageOpen size={14} />
                <span className="truncate font-medium">{p.name}</span>
              </button>
            ))}

            {projects.length === 0 && (
              <div className="text-center py-8 text-xs" style={{ color: "var(--text-muted)" }}>
                No projects yet.
                <br />
                Click + to create one.
              </div>
            )}
          </div>

          {/* Sessions */}
          {activeProject && sessions.length > 0 && (
            <div className="mt-4">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold uppercase tracking-wider" style={{ color: "var(--text-muted)" }}>
                  Sessions
                </span>
                <button
                  onClick={createSession}
                  className="flex items-center justify-center w-5 h-5 rounded hover:opacity-80"
                  style={{ background: "var(--border)", color: "var(--text-secondary)" }}
                >
                  <Plus size={11} />
                </button>
              </div>
              <div className="space-y-1">
                {sessions.map((s) => (
                  <button
                    key={s.id}
                    onClick={() => setActiveSessionId(s.id)}
                    className="w-full flex items-center gap-2 px-3 py-1.5 rounded text-left text-xs transition-all"
                    style={{
                      background: activeSessionId === s.id ? "rgba(79,142,247,0.15)" : "transparent",
                      color: activeSessionId === s.id ? "var(--accent)" : "var(--text-muted)",
                    }}
                  >
                    <ChevronRight size={10} />
                    <span className="truncate">{s.title}</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Plugin Status */}
        {activeProject && (
          <div
            className="px-3 py-3 border-t"
            style={{ borderColor: "var(--border)" }}
          >
            <div className="flex items-center gap-2">
              <div
                className={`w-2 h-2 rounded-full ${isPluginOnline ? "glow-blue" : ""}`}
                style={{ background: isPluginOnline ? "var(--success)" : "var(--text-muted)" }}
              />
              <span className="text-xs" style={{ color: "var(--text-secondary)" }}>
                Plugin {isPluginOnline ? "Online" : "Offline"}
              </span>
            </div>
          </div>
        )}
      </div>

      {/* MAIN CONTENT */}
      <div className="flex flex-col flex-1 min-w-0">
        {/* Top bar */}
        <div
          className="flex items-center justify-between px-6 py-3 border-b flex-shrink-0"
          style={{ background: "var(--bg-panel)", borderColor: "var(--border)" }}
        >
          <div className="flex items-center gap-2">
            {activeProject ? (
              <>
                <span className="font-semibold" style={{ color: "var(--text-primary)" }}>
                  {activeProject.name}
                </span>
                {activeProject.unityVersion && (
                  <span
                    className="text-xs px-2 py-0.5 rounded"
                    style={{ background: "var(--bg-card)", color: "var(--text-muted)" }}
                  >
                    Unity {activeProject.unityVersion}
                  </span>
                )}
              </>
            ) : (
              <span style={{ color: "var(--text-muted)" }}>No project selected</span>
            )}
          </div>

          {/* Tabs */}
          <div className="flex items-center gap-1">
            {(
              [
                { id: "chat", icon: BotMessageSquare, label: "Chat" },
                { id: "files", icon: FileCode2, label: "Files" },
                { id: "logs", icon: Terminal, label: "Logs" },
                { id: "setup", icon: Settings, label: "Setup" },
              ] as const
            ).map(({ id, icon: Icon, label }) => (
              <button
                key={id}
                onClick={() => setActiveTab(id)}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all"
                style={{
                  background: activeTab === id ? "var(--accent)" : "transparent",
                  color: activeTab === id ? "white" : "var(--text-secondary)",
                }}
              >
                <Icon size={13} />
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* TAB: CHAT */}
        {activeTab === "chat" && (
          <div className="flex flex-col flex-1 min-h-0">
            {!activeProject ? (
              <WelcomeScreen onCreateProject={() => setShowNewProject(true)} />
            ) : (
              <>
                {/* Messages */}
                <div className="flex-1 overflow-y-auto p-6 space-y-4">
                  {messages.length === 0 && (
                    <EmptyChat projectName={activeProject.name} isOnline={!!isPluginOnline} />
                  )}

                  {messages.map((msg, i) => (
                    <MessageBubble key={msg.id ?? i} message={msg} />
                  ))}

                  {isLoading && <ThinkingIndicator />}
                  <div ref={messagesEndRef} />
                </div>

                {/* Input */}
                <div
                  className="border-t p-4 flex-shrink-0"
                  style={{ background: "var(--bg-panel)", borderColor: "var(--border)" }}
                >
                  <div
                    className="flex items-end gap-3 rounded-xl border p-3"
                    style={{ background: "var(--bg-card)", borderColor: "var(--border)" }}
                  >
                    <textarea
                      ref={textareaRef}
                      value={input}
                      onChange={(e) => setInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" && !e.shiftKey) {
                          e.preventDefault();
                          sendMessage();
                        }
                      }}
                      placeholder="Опиши задачу для Unity... (Enter = отправить, Shift+Enter = новая строка)"
                      rows={2}
                      className="flex-1 resize-none outline-none text-sm bg-transparent"
                      style={{ color: "var(--text-primary)", lineHeight: 1.5 }}
                    />
                    <button
                      onClick={sendMessage}
                      disabled={isLoading || !input.trim()}
                      className="flex items-center justify-center w-9 h-9 rounded-lg transition-all flex-shrink-0"
                      style={{
                        background:
                          isLoading || !input.trim() ? "var(--border)" : "var(--accent)",
                        color: "white",
                      }}
                    >
                      {isLoading ? (
                        <Loader2 size={16} className="animate-spin-slow" />
                      ) : (
                        <Send size={16} />
                      )}
                    </button>
                  </div>

                  {/* Quick prompts */}
                  <div className="flex gap-2 mt-2 flex-wrap">
                    {[
                      "Сделай систему инвентаря",
                      "Добавь ИИ для врагов",
                      "Создай систему прыжков",
                      "Почини ошибки в консоли",
                    ].map((prompt) => (
                      <button
                        key={prompt}
                        onClick={() => setInput(prompt)}
                        className="text-xs px-2.5 py-1 rounded-full border transition-all hover:opacity-80"
                        style={{
                          borderColor: "var(--border)",
                          color: "var(--text-muted)",
                          background: "transparent",
                        }}
                      >
                        {prompt}
                      </button>
                    ))}
                  </div>
                </div>
              </>
            )}
          </div>
        )}

        {/* TAB: FILES */}
        {activeTab === "files" && (
          <div className="flex-1 overflow-y-auto p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold" style={{ color: "var(--text-primary)" }}>
                Project Files
              </h2>
              <button
                onClick={() => activeProject && loadFiles(activeProject.id)}
                className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs border transition-all hover:opacity-80"
                style={{ borderColor: "var(--border)", color: "var(--text-secondary)" }}
              >
                <RefreshCw size={12} /> Refresh
              </button>
            </div>

            {projectFiles.length === 0 ? (
              <div
                className="text-center py-16 rounded-xl border"
                style={{ borderColor: "var(--border)", color: "var(--text-muted)" }}
              >
                <FolderOpen size={40} className="mx-auto mb-3 opacity-30" />
                <p className="font-medium">No files indexed</p>
                <p className="text-xs mt-1">Connect the Unity plugin to sync project files</p>
              </div>
            ) : (
              <div className="space-y-1">
                {projectFiles.map((f, i) => (
                  <div
                    key={i}
                    className="flex items-center gap-3 px-4 py-2.5 rounded-lg border"
                    style={{ background: "var(--bg-card)", borderColor: "var(--border)" }}
                  >
                    <FileCode2
                      size={14}
                      style={{
                        color: f.type === "cs" ? "var(--accent)" : f.type === "scene" ? "var(--accent2)" : "var(--text-muted)",
                      }}
                    />
                    <span className="text-sm font-mono flex-1" style={{ color: "var(--text-primary)" }}>
                      {f.path}
                    </span>
                    <span
                      className="text-xs px-2 py-0.5 rounded"
                      style={{ background: "var(--bg-secondary)", color: "var(--text-muted)" }}
                    >
                      .{f.type}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB: LOGS */}
        {activeTab === "logs" && (
          <div className="flex-1 overflow-y-auto p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold" style={{ color: "var(--text-primary)" }}>
                Unity Console
              </h2>
              <button
                onClick={() => activeProject && loadLogs(activeProject.id)}
                className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs border transition-all hover:opacity-80"
                style={{ borderColor: "var(--border)", color: "var(--text-secondary)" }}
              >
                <RefreshCw size={12} /> Refresh
              </button>
            </div>

            {consoleLogs.length === 0 ? (
              <div
                className="text-center py-16 rounded-xl border"
                style={{ borderColor: "var(--border)", color: "var(--text-muted)" }}
              >
                <Terminal size={40} className="mx-auto mb-3 opacity-30" />
                <p className="font-medium">No console logs</p>
                <p className="text-xs mt-1">Logs appear here when the Unity plugin is connected</p>
              </div>
            ) : (
              <div className="space-y-1 font-mono text-xs">
                {consoleLogs.map((log, i) => (
                  <div
                    key={i}
                    className="flex items-start gap-3 px-3 py-2 rounded border"
                    style={{
                      background: "var(--bg-card)",
                      borderColor:
                        log.logType === "error" || log.logType === "exception"
                          ? "rgba(255,77,106,0.3)"
                          : log.logType === "warning"
                          ? "rgba(245,158,11,0.3)"
                          : "var(--border)",
                    }}
                  >
                    <span
                      style={{
                        color:
                          log.logType === "error" || log.logType === "exception"
                            ? "var(--error)"
                            : log.logType === "warning"
                            ? "var(--warning)"
                            : "var(--success)",
                      }}
                      className="flex-shrink-0 mt-0.5"
                    >
                      {log.logType === "error" || log.logType === "exception" ? (
                        <AlertTriangle size={12} />
                      ) : log.logType === "warning" ? (
                        <AlertTriangle size={12} />
                      ) : (
                        <CheckCircle2 size={12} />
                      )}
                    </span>
                    <span className="flex-1" style={{ color: "var(--text-secondary)" }}>
                      {log.message}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB: SETUP */}
        {activeTab === "setup" && activeProject && (
          <div className="flex-1 overflow-y-auto p-6">
            <h2 className="text-lg font-semibold mb-6" style={{ color: "var(--text-primary)" }}>
              Plugin Setup — {activeProject.name}
            </h2>

            {/* API Key */}
            <div
              className="p-4 rounded-xl border mb-6"
              style={{ background: "var(--bg-card)", borderColor: "var(--border)" }}
            >
              <div className="flex items-center gap-2 mb-2">
                <Zap size={16} style={{ color: "var(--accent)" }} />
                <span className="font-semibold text-sm" style={{ color: "var(--text-primary)" }}>
                  API Key
                </span>
              </div>
              <p className="text-xs mb-3" style={{ color: "var(--text-muted)" }}>
                Paste this key into the Unity plugin to connect it to this project.
              </p>
              <div className="flex items-center gap-2">
                <code
                  className="flex-1 px-3 py-2 rounded-lg text-xs font-mono"
                  style={{
                    background: "var(--bg-secondary)",
                    color: "var(--accent)",
                    border: "1px solid var(--border)",
                  }}
                >
                  {activeProject.apiKey}
                </code>
                <button
                  onClick={copyKey}
                  className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs transition-all hover:opacity-80"
                  style={{ background: "var(--accent)", color: "white" }}
                >
                  {copiedKey ? <Check size={13} /> : <Copy size={13} />}
                  {copiedKey ? "Copied!" : "Copy"}
                </button>
              </div>
            </div>

            {/* Installation Guide */}
            <div
              className="p-4 rounded-xl border mb-6"
              style={{ background: "var(--bg-card)", borderColor: "var(--border)" }}
            >
              <div className="flex items-center gap-2 mb-3">
                <Book size={16} style={{ color: "var(--accent2)" }} />
                <span className="font-semibold text-sm" style={{ color: "var(--text-primary)" }}>
                  Installation
                </span>
              </div>
              <ol className="space-y-3 text-sm" style={{ color: "var(--text-secondary)" }}>
                <li className="flex gap-3">
                  <span
                    className="flex-shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: "var(--accent)", color: "white" }}
                  >
                    1
                  </span>
                  <span>
                    Download <strong>AliTerraAI.cs</strong> from the Downloads section below
                  </span>
                </li>
                <li className="flex gap-3">
                  <span
                    className="flex-shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: "var(--accent)", color: "white" }}
                  >
                    2
                  </span>
                  <span>
                    Place it in <code>Assets/Editor/</code> folder in your Unity project
                  </span>
                </li>
                <li className="flex gap-3">
                  <span
                    className="flex-shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: "var(--accent)", color: "white" }}
                  >
                    3
                  </span>
                  <span>
                    In Unity: <strong>Window → AliTerra AI</strong> to open the panel
                  </span>
                </li>
                <li className="flex gap-3">
                  <span
                    className="flex-shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: "var(--accent)", color: "white" }}
                  >
                    4
                  </span>
                  <span>
                    Paste your API Key and Server URL, click <strong>Connect</strong>
                  </span>
                </li>
              </ol>
            </div>

            {/* Download Plugin */}
            <div
              className="p-4 rounded-xl border"
              style={{ background: "var(--bg-card)", borderColor: "var(--border)" }}
            >
              <div className="flex items-center gap-2 mb-3">
                <Code2 size={16} style={{ color: "var(--accent)" }} />
                <span className="font-semibold text-sm" style={{ color: "var(--text-primary)" }}>
                  Download Unity Plugin (C#)
                </span>
              </div>
              <p className="text-xs mb-3" style={{ color: "var(--text-muted)" }}>
                Drop this script into <code>Assets/Editor/</code> in your Unity project.
              </p>
              <a
                href={`/api/plugin/download?projectId=${activeProject.id}`}
                className="flex items-center justify-center gap-2 py-2.5 rounded-lg text-sm font-medium transition-all hover:opacity-80"
                style={{ background: "var(--accent)", color: "white" }}
              >
                <Wrench size={15} />
                Download AliTerraAI.cs
              </a>
            </div>
          </div>
        )}

        {activeTab === "setup" && !activeProject && (
          <div className="flex-1 flex items-center justify-center">
            <p style={{ color: "var(--text-muted)" }}>Select a project first</p>
          </div>
        )}
      </div>
    </div>
  );
}

function WelcomeScreen({ onCreateProject }: { onCreateProject: () => void }) {
  return (
    <div className="flex-1 flex items-center justify-center p-8">
      <div className="text-center max-w-md">
        <div
          className="w-16 h-16 rounded-2xl flex items-center justify-center mx-auto mb-6"
          style={{ background: "linear-gradient(135deg, #4f8ef7, #00d4aa)" }}
        >
          <Cpu size={28} className="text-white" />
        </div>
        <h1 className="text-2xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>
          AliTerra AI
        </h1>
        <p className="text-sm mb-6" style={{ color: "var(--text-secondary)" }}>
          ИИ-разработчик, который живёт внутри Unity. Создаёт скрипты, управляет сценой,
          исправляет ошибки — автоматически.
        </p>
        <button
          onClick={onCreateProject}
          className="flex items-center gap-2 px-6 py-3 rounded-xl font-medium mx-auto transition-all hover:opacity-90"
          style={{ background: "var(--accent)", color: "white" }}
        >
          <Plus size={18} />
          Создать первый проект
        </button>
      </div>
    </div>
  );
}

function EmptyChat({ projectName, isOnline }: { projectName: string; isOnline: boolean }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div
        className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
        style={{ background: "rgba(79,142,247,0.15)", border: "1px solid rgba(79,142,247,0.3)" }}
      >
        <BotMessageSquare size={26} style={{ color: "var(--accent)" }} />
      </div>
      <h3 className="text-lg font-semibold mb-2" style={{ color: "var(--text-primary)" }}>
        {projectName}
      </h3>
      <p className="text-sm max-w-xs" style={{ color: "var(--text-secondary)" }}>
        Опиши, что нужно сделать. ИИ составит план, напишет код и применит его в Unity — сам.
      </p>
      {!isOnline && (
        <div
          className="mt-4 flex items-center gap-2 px-4 py-2 rounded-lg border text-xs"
          style={{
            background: "rgba(245,158,11,0.08)",
            borderColor: "rgba(245,158,11,0.3)",
            color: "var(--warning)",
          }}
        >
          <AlertTriangle size={13} />
          Плагин не подключён. Перейди в Setup для установки.
        </div>
      )}
    </div>
  );
}

function ThinkingIndicator() {
  return (
    <div className="flex items-start gap-3 animate-fade-in">
      <div
        className="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0"
        style={{ background: "linear-gradient(135deg, #4f8ef7, #00d4aa)" }}
      >
        <Cpu size={14} className="text-white" />
      </div>
      <div
        className="px-4 py-3 rounded-2xl rounded-tl-none"
        style={{ background: "var(--bg-card)", border: "1px solid var(--border)" }}
      >
        <div className="flex items-center gap-2">
          <Loader2 size={14} style={{ color: "var(--accent)" }} className="animate-spin-slow" />
          <span className="text-sm" style={{ color: "var(--text-muted)" }}>
            Анализирую проект и составляю план...
          </span>
        </div>
      </div>
    </div>
  );
}

function MessageBubble({ message }: { message: Message }) {
  const [copied, setCopied] = useState(false);

  if (message.role === "tool") return null; // hidden from chat view
  if (message.role === "system") return null;
  if (!message.content && !message.toolCalls) return null;

  const isUser = message.role === "user";
  const hasToolCalls = !!message.toolCalls && (message.toolCalls as unknown[]).length > 0;

  function copyContent() {
    if (message.content) {
      navigator.clipboard.writeText(message.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }

  // Format content with code blocks
  function renderContent(text: string) {
    const parts = text.split(/(```[\s\S]*?```)/g);
    return parts.map((part, i) => {
      if (part.startsWith("```")) {
        const lines = part.split("\n");
        const lang = lines[0].replace("```", "").trim();
        const code = lines.slice(1, -1).join("\n");
        return (
          <pre key={i} className="my-2 relative group">
            {lang && (
              <span
                className="absolute top-2 right-2 text-xs px-2 py-0.5 rounded"
                style={{ background: "var(--bg-card)", color: "var(--text-muted)" }}
              >
                {lang}
              </span>
            )}
            <code className="text-xs" style={{ color: "#a9b1d6", background: "transparent" }}>
              {code}
            </code>
          </pre>
        );
      }
      // Inline formatting
      const formatted = part
        .split(/(`[^`]+`)/g)
        .map((chunk, j) => {
          if (chunk.startsWith("`") && chunk.endsWith("`")) {
            return <code key={j}>{chunk.slice(1, -1)}</code>;
          }
          return <span key={j}>{chunk}</span>;
        });
      return <span key={i}>{formatted}</span>;
    });
  }

  return (
    <div className={`flex items-start gap-3 animate-fade-in ${isUser ? "flex-row-reverse" : ""}`}>
      {/* Avatar */}
      <div
        className="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0"
        style={{
          background: isUser
            ? "linear-gradient(135deg, #667eea, #764ba2)"
            : "linear-gradient(135deg, #4f8ef7, #00d4aa)",
        }}
      >
        {isUser ? (
          <span className="text-white text-xs font-bold">U</span>
        ) : (
          <Cpu size={13} className="text-white" />
        )}
      </div>

      <div className={`max-w-2xl ${isUser ? "items-end" : "items-start"} flex flex-col`}>
        {/* Tool calls badge */}
        {hasToolCalls && !isUser && (
          <ToolCallsBadge toolCalls={message.toolCalls as Array<{ id: string; function?: { name: string } }> } />
        )}

        {/* Content */}
        {message.content && (
          <div
            className="px-4 py-3 rounded-2xl relative group"
            style={{
              background: isUser ? "var(--accent)" : "var(--bg-card)",
              border: isUser ? "none" : "1px solid var(--border)",
              borderRadius: isUser ? "18px 18px 4px 18px" : "4px 18px 18px 18px",
              color: isUser ? "white" : "var(--text-primary)",
            }}
          >
            <div className="text-sm leading-relaxed whitespace-pre-wrap">
              {renderContent(message.content)}
            </div>

            {!isUser && (
              <button
                onClick={copyContent}
                className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity p-1 rounded"
                style={{ background: "var(--bg-secondary)" }}
              >
                {copied ? (
                  <Check size={11} style={{ color: "var(--success)" }} />
                ) : (
                  <Copy size={11} style={{ color: "var(--text-muted)" }} />
                )}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function ToolCallsBadge({ toolCalls }: { toolCalls: Array<{ id: string; function?: { name: string } }> }) {
  const names = toolCalls
    .map((tc) => tc.function?.name ?? "tool")
    .join(", ");

  const toolIcons: Record<string, string> = {
    create_script: "📝",
    modify_script: "✏️",
    read_script: "📖",
    list_project_files: "📂",
    set_object_property: "⚙️",
    read_console_logs: "🖥️",
    create_scriptable_object: "🗃️",
    execute_editor_command: "▶️",
  };

  return (
    <div className="flex flex-wrap gap-1 mb-2">
      {toolCalls.map((tc, i) => {
        const name = tc.function?.name ?? "tool";
        return (
          <span
            key={i}
            className="flex items-center gap-1 text-xs px-2 py-1 rounded-full"
            style={{
              background: "rgba(79,142,247,0.12)",
              border: "1px solid rgba(79,142,247,0.25)",
              color: "var(--accent)",
            }}
          >
            <span>{toolIcons[name] ?? "🔧"}</span>
            <span>{name}</span>
          </span>
        );
      })}
      <span className="text-xs px-2 py-1" style={{ color: "var(--text-muted)" }}>
        → {names}
      </span>
    </div>
  );
}
