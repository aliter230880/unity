"use client";

import { useEffect, useState, useRef, useCallback } from "react";
import { v4 as uuidv4 } from "uuid";

// ── Types ──────────────────────────────────────────────────────────────────
interface Project {
  id: string;
  name: string;
  apiKey: string;
  unityVersion: string;
  fileCount: number;
  activeScene: string;
  lastSyncAt: string | null;
  updatedAt: string;
}

interface Session {
  id: string;
  projectId: string;
  title: string;
  createdAt: string;
}

interface ChatMessage {
  id: string;
  role: "user" | "assistant" | "tool" | "system";
  content: string;
  toolName?: string;
  toolArgs?: Record<string, unknown>;
  toolResult?: string;
  isStreaming?: boolean;
}

interface StreamEvent {
  type: "text" | "tool_call" | "tool_result" | "done" | "error";
  content?: string;
  tool?: string;
  args?: Record<string, unknown>;
  result?: string;
  error?: string;
}

const MODELS = [
  { id: "gpt-4o", label: "GPT-4o" },
  { id: "gpt-4o-mini", label: "GPT-4o Mini" },
  { id: "gpt-4.1", label: "GPT-4.1" },
  { id: "gpt-4.1-mini", label: "GPT-4.1 Mini" },
  { id: "o4-mini", label: "o4-mini" },
];

// ── Main App ───────────────────────────────────────────────────────────────
export default function Home() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProject, setSelectedProject] = useState<Project | null>(null);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [selectedSession, setSelectedSession] = useState<Session | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const [model, setModel] = useState("gpt-4o");
  const [newProjectName, setNewProjectName] = useState("");
  const [showNewProject, setShowNewProject] = useState(false);
  const [sidebarTab, setSidebarTab] = useState<"projects" | "sessions" | "files" | "logs">("projects");
  const [projectFiles, setProjectFiles] = useState<{path: string; fileType: string; sizeBytes: number}[]>([]);
  const [consoleLogs, setConsoleLogs] = useState<{id: number; logType: string; message: string; stackTrace: string; createdAt: string}[]>([]);
  const [fileSearch, setFileSearch] = useState("");
  const [fileTypeFilter, setFileTypeFilter] = useState("all");
  const [copied, setCopied] = useState("");
  const [showPluginModal, setShowPluginModal] = useState(false);
  const chatEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  // ── Load projects ──────────────────────────────────────────────────────
  useEffect(() => {
    loadProjects();
  }, []);

  const loadProjects = async () => {
    const res = await fetch("/api/projects");
    if (res.ok) {
      const data = await res.json();
      setProjects(data.projects ?? []);
    }
  };

  const createProject = async () => {
    if (!newProjectName.trim()) return;
    const res = await fetch("/api/projects", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: newProjectName.trim() }),
    });
    if (res.ok) {
      const data = await res.json();
      setProjects((p) => [data.project, ...p]);
      setSelectedProject(data.project);
      setNewProjectName("");
      setShowNewProject(false);
      setSidebarTab("sessions");
      await loadSessions(data.project.id);
    }
  };

  const deleteProject = async (id: string) => {
    if (!confirm("Delete this project and all its data?")) return;
    await fetch(`/api/projects?id=${id}`, { method: "DELETE" });
    setProjects((p) => p.filter((x) => x.id !== id));
    if (selectedProject?.id === id) {
      setSelectedProject(null);
      setSessions([]);
      setSelectedSession(null);
      setMessages([]);
    }
  };

  // ── Load sessions ──────────────────────────────────────────────────────
  const loadSessions = async (projectId: string) => {
    const res = await fetch(`/api/sessions?projectId=${projectId}`);
    if (res.ok) {
      const data = await res.json();
      setSessions(data.sessions ?? []);
    }
  };

  const createSession = async () => {
    if (!selectedProject) return;
    const id = uuidv4();
    const session: Session = {
      id,
      projectId: selectedProject.id,
      title: "New Chat",
      createdAt: new Date().toISOString(),
    };
    setSessions((s) => [session, ...s]);
    setSelectedSession(session);
    setMessages([]);
    setSidebarTab("sessions");
  };

  const selectSession = async (session: Session) => {
    setSelectedSession(session);
    const res = await fetch(`/api/sessions?sessionId=${session.id}`);
    if (res.ok) {
      const data = await res.json();
      const msgs: ChatMessage[] = (data.messages ?? [])
        .filter((m: { role: string }) => m.role === "user" || m.role === "assistant")
        .map((m: { id: number; role: string; content: string }) => ({
          id: String(m.id),
          role: m.role as "user" | "assistant",
          content: m.content ?? "",
        }));
      setMessages(msgs);
    }
  };

  const deleteSession = async (id: string) => {
    await fetch(`/api/sessions?id=${id}`, { method: "DELETE" });
    setSessions((s) => s.filter((x) => x.id !== id));
    if (selectedSession?.id === id) {
      setSelectedSession(null);
      setMessages([]);
    }
  };

  // ── Load project files ─────────────────────────────────────────────────
  const loadProjectFiles = useCallback(async (projectId: string) => {
    const res = await fetch(`/api/unity/state?api_key=${projects.find(p => p.id === projectId)?.apiKey}`);
    if (res.ok) {
      const data = await res.json();
      setProjectFiles(data.files ?? []);
    }
  }, [projects]);

  const loadConsoleLogs = useCallback(async (projectId: string) => {
    const proj = projects.find(p => p.id === projectId);
    if (!proj) return;
    const res = await fetch(`/api/unity/logs?api_key=${proj.apiKey}&limit=100`);
    if (res.ok) {
      const data = await res.json();
      setConsoleLogs(data.logs ?? []);
    }
  }, [projects]);

  useEffect(() => {
    if (selectedProject && sidebarTab === "files") {
      loadProjectFiles(selectedProject.id);
    }
    if (selectedProject && sidebarTab === "logs") {
      loadConsoleLogs(selectedProject.id);
    }
  }, [sidebarTab, selectedProject, loadProjectFiles, loadConsoleLogs]);

  // ── Auto-scroll ────────────────────────────────────────────────────────
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // ── Send message ───────────────────────────────────────────────────────
  const sendMessage = async () => {
    if (!input.trim() || isStreaming || !selectedProject) return;
    if (!selectedSession) {
      await createSession();
      return;
    }

    const userMsg: ChatMessage = {
      id: uuidv4(),
      role: "user",
      content: input.trim(),
    };

    setMessages((m) => [...m, userMsg]);
    const userInput = input.trim();
    setInput("");
    setIsStreaming(true);

    // Add streaming assistant message
    const assistantId = uuidv4();
    const toolMessages: ChatMessage[] = [];
    let assistantContent = "";

    setMessages((m) => [
      ...m,
      { id: assistantId, role: "assistant", content: "", isStreaming: true },
    ]);

    try {
      abortRef.current = new AbortController();
      const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          message: userInput,
          sessionId: selectedSession.id,
          projectId: selectedProject.id,
          model,
        }),
        signal: abortRef.current.signal,
      });

      if (!res.ok) throw new Error(await res.text());

      const reader = res.body!.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          const raw = line.slice(6).trim();
          if (!raw) continue;

          let event: StreamEvent;
          try {
            event = JSON.parse(raw);
          } catch {
            continue;
          }

          if (event.type === "text" && event.content) {
            assistantContent += event.content;
            setMessages((m) =>
              m.map((msg) =>
                msg.id === assistantId
                  ? { ...msg, content: assistantContent }
                  : msg
              )
            );
          } else if (event.type === "tool_call" && event.tool) {
            const tmId = uuidv4();
            const toolMsg: ChatMessage = {
              id: tmId,
              role: "tool",
              content: "",
              toolName: event.tool,
              toolArgs: event.args,
            };
            toolMessages.push(toolMsg);
            setMessages((m) => {
              const idx = m.findIndex((x) => x.id === assistantId);
              if (idx === -1) return [...m, toolMsg];
              return [...m.slice(0, idx), toolMsg, ...m.slice(idx)];
            });
          } else if (event.type === "tool_result" && event.tool) {
            const last = toolMessages.findLast(
              (t) => t.toolName === event.tool
            );
            if (last) {
              last.toolResult = event.result;
              setMessages((m) =>
                m.map((msg) =>
                  msg.id === last.id
                    ? { ...msg, toolResult: event.result }
                    : msg
                )
              );
            }
          } else if (event.type === "done") {
            setMessages((m) =>
              m.map((msg) =>
                msg.id === assistantId
                  ? { ...msg, isStreaming: false }
                  : msg
              )
            );
          } else if (event.type === "error") {
            setMessages((m) =>
              m.map((msg) =>
                msg.id === assistantId
                  ? {
                      ...msg,
                      content: `❌ Error: ${event.error}`,
                      isStreaming: false,
                    }
                  : msg
              )
            );
          }
        }
      }
    } catch (err: unknown) {
      if ((err as Error).name !== "AbortError") {
        setMessages((m) =>
          m.map((msg) =>
            msg.id === assistantId
              ? {
                  ...msg,
                  content: `❌ ${String(err)}`,
                  isStreaming: false,
                }
              : msg
          )
        );
      }
    } finally {
      setIsStreaming(false);
      abortRef.current = null;
    }
  };

  const stopStreaming = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
    setMessages((m) =>
      m.map((msg) =>
        msg.isStreaming ? { ...msg, isStreaming: false } : msg
      )
    );
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      sendMessage();
    }
  };

  const copyText = async (text: string, id: string) => {
    await navigator.clipboard.writeText(text);
    setCopied(id);
    setTimeout(() => setCopied(""), 2000);
  };

  // ── Filtered files ─────────────────────────────────────────────────────
  const filteredFiles = projectFiles.filter((f) => {
    const matchSearch = fileSearch
      ? f.path.toLowerCase().includes(fileSearch.toLowerCase())
      : true;
    const matchType =
      fileTypeFilter === "all" || (f.fileType ?? "").includes(fileTypeFilter);
    return matchSearch && matchType;
  });

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <div className="flex h-screen overflow-hidden" style={{ background: "var(--bg)" }}>
      {/* ── LEFT SIDEBAR ─────────────────────────────────────────────── */}
      <aside
        className="flex flex-col border-r"
        style={{
          width: 280,
          minWidth: 280,
          borderColor: "var(--border)",
          background: "var(--panel)",
        }}
      >
        {/* Logo */}
        <div
          className="flex items-center gap-2 px-4 py-3 border-b"
          style={{ borderColor: "var(--border)" }}
        >
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center text-base font-bold"
            style={{ background: "linear-gradient(135deg, #3b82f6, #8b5cf6)" }}
          >
            A
          </div>
          <div>
            <div className="font-bold text-sm" style={{ color: "var(--text)" }}>
              AliTerra AI
            </div>
            <div className="text-xs" style={{ color: "var(--muted)" }}>
              Unity Developer
            </div>
          </div>
        </div>

        {/* Sidebar tabs */}
        <div className="flex border-b" style={{ borderColor: "var(--border)" }}>
          {(["projects", "sessions", "files", "logs"] as const).map((tab) => (
            <button
              key={tab}
              onClick={() => setSidebarTab(tab)}
              className="flex-1 py-2 text-xs font-medium transition-colors"
              style={{
                color: sidebarTab === tab ? "var(--accent)" : "var(--muted)",
                borderBottom: sidebarTab === tab ? `2px solid var(--accent)` : "2px solid transparent",
                background: "transparent",
                cursor: "pointer",
              }}
            >
              {tab === "projects" ? "🏗️" : tab === "sessions" ? "💬" : tab === "files" ? "📁" : "📋"}
            </button>
          ))}
        </div>

        {/* Sidebar content */}
        <div className="flex-1 overflow-y-auto">
          {/* PROJECTS TAB */}
          {sidebarTab === "projects" && (
            <div className="p-3 flex flex-col gap-2">
              <button
                onClick={() => setShowNewProject(!showNewProject)}
                className="w-full py-2 px-3 rounded-lg text-xs font-semibold flex items-center gap-2 transition-colors"
                style={{
                  background: "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                  color: "white",
                  cursor: "pointer",
                }}
              >
                ＋ New Unity Project
              </button>

              {showNewProject && (
                <div className="flex gap-2 animate-fade">
                  <input
                    autoFocus
                    value={newProjectName}
                    onChange={(e) => setNewProjectName(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && createProject()}
                    placeholder="Project name..."
                    className="flex-1 px-2 py-1 text-xs rounded-md outline-none"
                    style={{
                      background: "#1e2540",
                      border: "1px solid var(--border)",
                      color: "var(--text)",
                    }}
                  />
                  <button
                    onClick={createProject}
                    className="px-3 py-1 text-xs rounded-md font-semibold"
                    style={{ background: "var(--accent)", color: "white", cursor: "pointer" }}
                  >
                    OK
                  </button>
                </div>
              )}

              {projects.map((p) => (
                <div
                  key={p.id}
                  onClick={() => {
                    setSelectedProject(p);
                    loadSessions(p.id);
                    setSidebarTab("sessions");
                  }}
                  className="group rounded-lg p-3 cursor-pointer transition-all"
                  style={{
                    background:
                      selectedProject?.id === p.id
                        ? "rgba(59,130,246,0.12)"
                        : "#1a1f35",
                    border: `1px solid ${selectedProject?.id === p.id ? "rgba(59,130,246,0.4)" : "transparent"}`,
                  }}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium text-sm truncate" style={{ color: "var(--text)" }}>
                      🎮 {p.name}
                    </span>
                    <button
                      onClick={(e) => { e.stopPropagation(); deleteProject(p.id); }}
                      className="opacity-0 group-hover:opacity-100 text-xs px-1 rounded"
                      style={{ color: "var(--red)", cursor: "pointer" }}
                    >
                      ✕
                    </button>
                  </div>
                  <div className="text-xs mt-1 flex gap-2" style={{ color: "var(--muted)" }}>
                    <span>{p.fileCount ?? 0} files</span>
                    {p.activeScene && <span>· {p.activeScene}</span>}
                    {p.lastSyncAt && (
                      <span
                        className="ml-auto"
                        style={{ color: "var(--green)" }}
                      >
                        ⚡ synced
                      </span>
                    )}
                  </div>
                  {selectedProject?.id === p.id && (
                    <div className="mt-2 flex gap-1">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          setShowPluginModal(true);
                        }}
                        className="text-xs px-2 py-1 rounded"
                        style={{
                          background: "rgba(139,92,246,0.2)",
                          color: "#a78bfa",
                          cursor: "pointer",
                          border: "1px solid rgba(139,92,246,0.3)",
                        }}
                      >
                        ↓ Plugin
                      </button>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          copyText(p.apiKey, p.id);
                        }}
                        className="text-xs px-2 py-1 rounded"
                        style={{
                          background: "rgba(59,130,246,0.15)",
                          color: "#60a5fa",
                          cursor: "pointer",
                          border: "1px solid rgba(59,130,246,0.3)",
                        }}
                      >
                        {copied === p.id ? "✓ Copied" : "🔑 Key"}
                      </button>
                    </div>
                  )}
                </div>
              ))}

              {projects.length === 0 && (
                <div className="text-center py-8" style={{ color: "var(--muted)" }}>
                  <div className="text-3xl mb-2">🎮</div>
                  <div className="text-xs">Create your first project</div>
                </div>
              )}
            </div>
          )}

          {/* SESSIONS TAB */}
          {sidebarTab === "sessions" && (
            <div className="p-3 flex flex-col gap-2">
              {selectedProject ? (
                <>
                  <div className="text-xs font-semibold mb-1" style={{ color: "var(--muted)" }}>
                    {selectedProject.name}
                  </div>
                  <button
                    onClick={createSession}
                    className="w-full py-2 px-3 rounded-lg text-xs font-semibold flex items-center gap-2"
                    style={{
                      background: "rgba(59,130,246,0.15)",
                      border: "1px solid rgba(59,130,246,0.3)",
                      color: "#60a5fa",
                      cursor: "pointer",
                    }}
                  >
                    ＋ New Chat Session
                  </button>

                  {sessions.map((s) => (
                    <div
                      key={s.id}
                      onClick={() => selectSession(s)}
                      className="group rounded-lg p-2 cursor-pointer flex items-center justify-between transition-all"
                      style={{
                        background:
                          selectedSession?.id === s.id
                            ? "rgba(59,130,246,0.12)"
                            : "#1a1f35",
                        border: `1px solid ${selectedSession?.id === s.id ? "rgba(59,130,246,0.3)" : "transparent"}`,
                      }}
                    >
                      <div className="flex-1 min-w-0">
                        <div className="text-xs truncate" style={{ color: "var(--text)" }}>
                          💬 {s.title}
                        </div>
                        <div className="text-xs mt-0.5" style={{ color: "var(--muted)" }}>
                          {new Date(s.createdAt).toLocaleDateString()}
                        </div>
                      </div>
                      <button
                        onClick={(e) => { e.stopPropagation(); deleteSession(s.id); }}
                        className="opacity-0 group-hover:opacity-100 text-xs ml-1"
                        style={{ color: "var(--red)", cursor: "pointer" }}
                      >
                        ✕
                      </button>
                    </div>
                  ))}

                  {sessions.length === 0 && (
                    <div className="text-center py-8" style={{ color: "var(--muted)" }}>
                      <div className="text-2xl mb-2">💬</div>
                      <div className="text-xs">No sessions yet</div>
                    </div>
                  )}
                </>
              ) : (
                <div className="text-center py-8" style={{ color: "var(--muted)" }}>
                  <div className="text-xs">Select a project first</div>
                </div>
              )}
            </div>
          )}

          {/* FILES TAB */}
          {sidebarTab === "files" && (
            <div className="p-3 flex flex-col gap-2">
              {selectedProject ? (
                <>
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold" style={{ color: "var(--muted)" }}>
                      {projectFiles.length} files synced
                    </span>
                    <button
                      onClick={() => loadProjectFiles(selectedProject.id)}
                      className="text-xs"
                      style={{ color: "var(--accent)", cursor: "pointer" }}
                    >
                      ↻ Refresh
                    </button>
                  </div>

                  <input
                    value={fileSearch}
                    onChange={(e) => setFileSearch(e.target.value)}
                    placeholder="Search files..."
                    className="w-full px-2 py-1.5 text-xs rounded-md outline-none"
                    style={{
                      background: "#1e2540",
                      border: "1px solid var(--border)",
                      color: "var(--text)",
                    }}
                  />

                  <div className="flex gap-1 flex-wrap">
                    {["all", "script", "scene", "prefab", "shader"].map((t) => (
                      <button
                        key={t}
                        onClick={() => setFileTypeFilter(t)}
                        className="text-xs px-2 py-0.5 rounded-full"
                        style={{
                          background: fileTypeFilter === t ? "var(--accent)" : "#1e2540",
                          color: fileTypeFilter === t ? "white" : "var(--muted)",
                          cursor: "pointer",
                        }}
                      >
                        {t}
                      </button>
                    ))}
                  </div>

                  <div className="flex flex-col gap-0.5 max-h-[calc(100vh-260px)] overflow-y-auto">
                    {filteredFiles.slice(0, 200).map((f, i) => {
                      const name = f.path.split("/").pop() ?? f.path;
                      const icon =
                        (f.fileType ?? "").includes("script") ? "📜" :
                        (f.fileType ?? "").includes("scene") ? "🎬" :
                        (f.fileType ?? "").includes("prefab") ? "🧊" :
                        (f.fileType ?? "").includes("shader") ? "✨" : "📄";
                      return (
                        <div
                          key={i}
                          className="flex items-center gap-1 px-2 py-1 rounded text-xs hover:bg-white/5 cursor-default"
                          title={f.path}
                        >
                          <span>{icon}</span>
                          <span className="truncate" style={{ color: "var(--text)" }}>{name}</span>
                          <span className="ml-auto text-xs" style={{ color: "var(--muted)" }}>
                            {f.sizeBytes ? `${Math.round(Number(f.sizeBytes) / 1024)}k` : ""}
                          </span>
                        </div>
                      );
                    })}
                    {filteredFiles.length === 0 && (
                      <div className="text-center py-4 text-xs" style={{ color: "var(--muted)" }}>
                        {projectFiles.length === 0 ? "No files synced yet.\nSync from Unity plugin first." : "No files match filter"}
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <div className="text-center py-8 text-xs" style={{ color: "var(--muted)" }}>
                  Select a project first
                </div>
              )}
            </div>
          )}

          {/* LOGS TAB */}
          {sidebarTab === "logs" && (
            <div className="p-3 flex flex-col gap-2">
              {selectedProject ? (
                <>
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold" style={{ color: "var(--muted)" }}>
                      Console Logs
                    </span>
                    <button
                      onClick={() => loadConsoleLogs(selectedProject.id)}
                      className="text-xs"
                      style={{ color: "var(--accent)", cursor: "pointer" }}
                    >
                      ↻ Refresh
                    </button>
                  </div>

                  <div className="flex flex-col gap-1 max-h-[calc(100vh-180px)] overflow-y-auto">
                    {consoleLogs.map((log) => {
                      const icon =
                        log.logType === "error" || log.logType === "exception" ? "🔴" :
                        log.logType === "warning" ? "🟡" : "⚪";
                      return (
                        <div
                          key={log.id}
                          className="text-xs p-2 rounded"
                          style={{
                            background: log.logType === "error" || log.logType === "exception"
                              ? "rgba(239,68,68,0.08)"
                              : log.logType === "warning"
                              ? "rgba(234,179,8,0.08)"
                              : "rgba(255,255,255,0.03)",
                            border: `1px solid ${
                              log.logType === "error" || log.logType === "exception"
                                ? "rgba(239,68,68,0.2)"
                                : log.logType === "warning"
                                ? "rgba(234,179,8,0.2)"
                                : "var(--border)"
                            }`,
                          }}
                        >
                          <div className="font-mono" style={{ color: "var(--text)" }}>
                            {icon} {log.message}
                          </div>
                          {log.stackTrace && (
                            <div className="mt-1 font-mono" style={{ color: "var(--muted)", fontSize: "10px" }}>
                              {log.stackTrace.substring(0, 150)}
                            </div>
                          )}
                          <div className="mt-1" style={{ color: "var(--muted)", fontSize: "10px" }}>
                            {new Date(log.createdAt).toLocaleTimeString()}
                          </div>
                        </div>
                      );
                    })}
                    {consoleLogs.length === 0 && (
                      <div className="text-center py-4 text-xs" style={{ color: "var(--muted)" }}>
                        No logs yet. Enable polling in Unity plugin.
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <div className="text-center py-8 text-xs" style={{ color: "var(--muted)" }}>
                  Select a project first
                </div>
              )}
            </div>
          )}
        </div>
      </aside>

      {/* ── MAIN CHAT AREA ────────────────────────────────────────────────── */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {/* Header */}
        <header
          className="flex items-center justify-between px-5 py-3 border-b"
          style={{ borderColor: "var(--border)", background: "var(--panel)" }}
        >
          <div className="flex items-center gap-3">
            {selectedProject ? (
              <>
                <span className="font-semibold text-sm" style={{ color: "var(--text)" }}>
                  🎮 {selectedProject.name}
                </span>
                {selectedProject.lastSyncAt && (
                  <span
                    className="text-xs px-2 py-0.5 rounded-full"
                    style={{
                      background: "rgba(34,197,94,0.1)",
                      color: "var(--green)",
                      border: "1px solid rgba(34,197,94,0.2)",
                    }}
                  >
                    ⚡ {selectedProject.fileCount} files synced
                  </span>
                )}
                {selectedSession && (
                  <span className="text-xs" style={{ color: "var(--muted)" }}>
                    / {selectedSession.title}
                  </span>
                )}
              </>
            ) : (
              <span className="text-sm font-semibold" style={{ color: "var(--text)" }}>
                Select a project →
              </span>
            )}
          </div>

          <div className="flex items-center gap-3">
            {/* Model selector */}
            <select
              value={model}
              onChange={(e) => setModel(e.target.value)}
              className="text-xs px-2 py-1 rounded-md outline-none cursor-pointer"
              style={{
                background: "#1e2540",
                border: "1px solid var(--border)",
                color: "var(--text)",
              }}
            >
              {MODELS.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.label}
                </option>
              ))}
            </select>

            {selectedProject && (
              <button
                onClick={() => setShowPluginModal(true)}
                className="text-xs px-3 py-1.5 rounded-lg font-semibold flex items-center gap-1"
                style={{
                  background: "linear-gradient(135deg, #8b5cf6, #3b82f6)",
                  color: "white",
                  cursor: "pointer",
                }}
              >
                ↓ Unity Plugin
              </button>
            )}
          </div>
        </header>

        {/* Chat messages */}
        <div className="flex-1 overflow-y-auto px-4 py-4">
          {!selectedProject ? (
            <WelcomeScreen />
          ) : !selectedSession ? (
            <div className="flex flex-col items-center justify-center h-full gap-6">
              <div className="text-center">
                <div className="text-5xl mb-4">🤖</div>
                <h2 className="text-xl font-bold mb-2" style={{ color: "var(--text)" }}>
                  AliTerra AI is ready
                </h2>
                <p className="text-sm mb-6" style={{ color: "var(--muted)" }}>
                  Project: <strong style={{ color: "var(--text)" }}>{selectedProject.name}</strong>
                  <br />
                  {selectedProject.fileCount
                    ? `${selectedProject.fileCount} files synced`
                    : "Sync from Unity plugin first"}
                </p>
                <button
                  onClick={createSession}
                  className="px-6 py-3 rounded-xl font-semibold text-sm"
                  style={{
                    background: "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                    color: "white",
                    cursor: "pointer",
                  }}
                >
                  ＋ Start New Chat
                </button>
              </div>

              <div className="grid grid-cols-2 gap-3 max-w-lg w-full">
                {QUICK_PROMPTS.map((qp) => (
                  <button
                    key={qp.label}
                    onClick={async () => {
                      await createSession();
                      setInput(qp.prompt);
                    }}
                    className="p-3 rounded-xl text-left text-xs transition-all hover:scale-[1.02]"
                    style={{
                      background: "#1a1f35",
                      border: "1px solid var(--border)",
                      color: "var(--text)",
                      cursor: "pointer",
                    }}
                  >
                    <div className="text-base mb-1">{qp.icon}</div>
                    <div className="font-semibold">{qp.label}</div>
                    <div style={{ color: "var(--muted)" }}>{qp.desc}</div>
                  </button>
                ))}
              </div>
            </div>
          ) : messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full gap-4">
              <div className="text-center">
                <div className="text-4xl mb-3">💬</div>
                <p className="text-sm" style={{ color: "var(--muted)" }}>
                  Ask me anything about your Unity project.
                  <br />
                  I can see all your files and make changes autonomously.
                </p>
              </div>
              <div className="grid grid-cols-2 gap-2 max-w-md w-full">
                {QUICK_PROMPTS.slice(0, 4).map((qp) => (
                  <button
                    key={qp.label}
                    onClick={() => setInput(qp.prompt)}
                    className="p-2 rounded-lg text-left text-xs"
                    style={{
                      background: "#1a1f35",
                      border: "1px solid var(--border)",
                      color: "var(--muted)",
                      cursor: "pointer",
                    }}
                  >
                    {qp.icon} {qp.label}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className="flex flex-col gap-4 max-w-4xl mx-auto">
              {messages.map((msg) => (
                <MessageBubble
                  key={msg.id}
                  msg={msg}
                  onCopy={(text) => copyText(text, msg.id)}
                  copied={copied === msg.id}
                />
              ))}
              <div ref={chatEndRef} />
            </div>
          )}
        </div>

        {/* Input area */}
        {selectedProject && (
          <div
            className="px-4 pb-4 pt-2 border-t"
            style={{ borderColor: "var(--border)", background: "var(--panel)" }}
          >
            {!selectedSession && (
              <div className="text-xs mb-2 text-center" style={{ color: "var(--muted)" }}>
                Press send to start a new session
              </div>
            )}
            <div
              className="flex gap-3 items-end rounded-xl p-3"
              style={{
                background: "#1a1f35",
                border: "1px solid var(--border)",
              }}
            >
              <textarea
                ref={inputRef}
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Ask AI to create, modify, or analyze your Unity project... (Ctrl+Enter to send)"
                rows={3}
                className="flex-1 resize-none text-sm outline-none bg-transparent"
                style={{ color: "var(--text)", lineHeight: 1.5 }}
              />
              <div className="flex flex-col gap-2">
                {isStreaming ? (
                  <button
                    onClick={stopStreaming}
                    className="px-4 py-2 rounded-lg text-xs font-semibold"
                    style={{
                      background: "rgba(239,68,68,0.2)",
                      color: "var(--red)",
                      border: "1px solid rgba(239,68,68,0.4)",
                      cursor: "pointer",
                    }}
                  >
                    ⛔ Stop
                  </button>
                ) : (
                  <button
                    onClick={sendMessage}
                    disabled={!input.trim()}
                    className="px-4 py-2 rounded-lg text-xs font-semibold transition-all"
                    style={{
                      background: input.trim()
                        ? "linear-gradient(135deg, #3b82f6, #8b5cf6)"
                        : "#1e2540",
                      color: input.trim() ? "white" : "var(--muted)",
                      cursor: input.trim() ? "pointer" : "default",
                    }}
                  >
                    Send ▶
                  </button>
                )}
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── PLUGIN MODAL ──────────────────────────────────────────────────── */}
      {showPluginModal && selectedProject && (
        <PluginModal
          project={selectedProject}
          onClose={() => setShowPluginModal(false)}
        />
      )}
    </div>
  );
}

// ── Message Bubble ─────────────────────────────────────────────────────────
function MessageBubble({
  msg,
  onCopy,
  copied,
}: {
  msg: ChatMessage;
  onCopy: (text: string) => void;
  copied: boolean;
}) {
  const [expanded, setExpanded] = useState(false);

  if (msg.role === "tool") {
    const toolIcons: Record<string, string> = {
      list_project_files: "📁",
      read_file: "📖",
      write_file: "✍️",
      create_gameobject: "🎮",
      add_component: "🔧",
      execute_editor_command: "▶️",
      read_console_logs: "📋",
      get_scene_hierarchy: "🎬",
      search_in_files: "🔍",
      delete_file: "🗑️",
    };

    const icon = toolIcons[msg.toolName ?? ""] ?? "🔧";
    const hasResult = msg.toolResult && msg.toolResult.length > 0;

    return (
      <div className="flex items-start gap-2 animate-slide">
        <div
          className="rounded-full w-6 h-6 flex items-center justify-center text-sm flex-shrink-0 mt-0.5"
          style={{ background: "rgba(139,92,246,0.15)" }}
        >
          {icon}
        </div>
        <div
          className="flex-1 rounded-xl p-3 text-xs"
          style={{
            background: "rgba(139,92,246,0.06)",
            border: "1px solid rgba(139,92,246,0.2)",
          }}
        >
          <div className="flex items-center gap-2 mb-1">
            <span className="font-semibold" style={{ color: "#a78bfa" }}>
              {msg.toolName}
            </span>
            {msg.toolArgs && (
              <span style={{ color: "var(--muted)" }}>
                {Object.entries(msg.toolArgs)
                  .slice(0, 2)
                  .map(([k, v]) => `${k}=${String(v).substring(0, 40)}`)
                  .join(", ")}
              </span>
            )}
          </div>
          {hasResult && (
            <div>
              <button
                onClick={() => setExpanded(!expanded)}
                className="text-xs"
                style={{ color: "var(--muted)", cursor: "pointer" }}
              >
                {expanded ? "▼ hide result" : "▶ show result"}
              </button>
              {expanded && (
                <pre
                  className="mt-2 p-2 rounded text-xs overflow-x-auto"
                  style={{
                    background: "#0a0c12",
                    color: "#86efac",
                    maxHeight: 300,
                    overflowY: "auto",
                    whiteSpace: "pre-wrap",
                    wordBreak: "break-word",
                  }}
                >
                  {msg.toolResult}
                </pre>
              )}
            </div>
          )}
        </div>
      </div>
    );
  }

  if (msg.role === "user") {
    return (
      <div className="flex justify-end animate-fade">
        <div
          className="max-w-[75%] rounded-2xl px-4 py-3 text-sm"
          style={{
            background: "linear-gradient(135deg, rgba(59,130,246,0.9), rgba(139,92,246,0.9))",
            color: "white",
          }}
        >
          {msg.content}
        </div>
      </div>
    );
  }

  // Assistant
  return (
    <div className="flex items-start gap-3 animate-fade">
      <div
        className="w-8 h-8 rounded-full flex items-center justify-center text-sm flex-shrink-0"
        style={{ background: "linear-gradient(135deg, #3b82f6, #8b5cf6)" }}
      >
        A
      </div>
      <div className="flex-1 min-w-0">
        <div
          className="rounded-2xl px-4 py-3 text-sm"
          style={{
            background: "#1a1f35",
            border: "1px solid var(--border)",
            color: "var(--text)",
          }}
        >
          {msg.content ? (
            <FormattedContent content={msg.content} onCopy={onCopy} />
          ) : msg.isStreaming ? (
            <span className="inline-block w-1.5 h-4 cursor-blink" style={{ background: "var(--accent)" }} />
          ) : null}
        </div>
        <div className="flex items-center gap-2 mt-1 px-1">
          {msg.content && !msg.isStreaming && (
            <button
              onClick={() => onCopy(msg.content)}
              className="text-xs"
              style={{ color: "var(--muted)", cursor: "pointer" }}
            >
              {copied ? "✓ Copied" : "⎘ Copy"}
            </button>
          )}
          {msg.isStreaming && (
            <span className="text-xs animate-pulse" style={{ color: "var(--accent)" }}>
              ● thinking...
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Formatted content with code blocks ────────────────────────────────────
function FormattedContent({
  content,
  onCopy,
}: {
  content: string;
  onCopy: (text: string) => void;
}) {
  const parts = content.split(/(```[\s\S]*?```)/g);

  return (
    <div className="flex flex-col gap-2">
      {parts.map((part, i) => {
        if (part.startsWith("```")) {
          const match = part.match(/```(\w*)\n?([\s\S]*?)```/);
          const lang = match?.[1] ?? "";
          const code = match?.[2] ?? part.slice(3, -3);
          return (
            <div key={i} className="rounded-lg overflow-hidden" style={{ border: "1px solid var(--border)" }}>
              <div
                className="flex items-center justify-between px-3 py-1.5"
                style={{ background: "#0d1117" }}
              >
                <span className="text-xs font-mono" style={{ color: "var(--muted)" }}>
                  {lang || "code"}
                </span>
                <button
                  onClick={() => onCopy(code)}
                  className="text-xs"
                  style={{ color: "var(--muted)", cursor: "pointer" }}
                >
                  ⎘ copy
                </button>
              </div>
              <pre
                className="px-4 py-3 text-xs overflow-x-auto"
                style={{
                  background: "#0a0c12",
                  color: "#86efac",
                  whiteSpace: "pre",
                  maxHeight: 500,
                  overflowY: "auto",
                }}
              >
                {code}
              </pre>
            </div>
          );
        }

        // Render plain text with basic markdown
        return (
          <div key={i} className="whitespace-pre-wrap leading-relaxed" style={{ color: "var(--text)" }}>
            {part.split("\n").map((line, j) => {
              if (line.startsWith("## ")) {
                return <div key={j} className="font-bold text-base mt-2" style={{ color: "#93c5fd" }}>{line.slice(3)}</div>;
              }
              if (line.startsWith("# ")) {
                return <div key={j} className="font-bold text-lg mt-2" style={{ color: "#93c5fd" }}>{line.slice(2)}</div>;
              }
              if (line.startsWith("**") && line.endsWith("**")) {
                return <div key={j} className="font-bold">{line.slice(2, -2)}</div>;
              }
              if (line.startsWith("- ") || line.startsWith("• ")) {
                return <div key={j} className="flex gap-2"><span style={{ color: "var(--accent)" }}>•</span><span>{line.slice(2)}</span></div>;
              }
              return <div key={j}>{line || <br />}</div>;
            })}
          </div>
        );
      })}
    </div>
  );
}

// ── Welcome Screen ─────────────────────────────────────────────────────────
function WelcomeScreen() {
  return (
    <div className="flex flex-col items-center justify-center h-full gap-8 px-8">
      <div className="text-center">
        <div
          className="w-20 h-20 rounded-2xl mx-auto mb-4 flex items-center justify-center text-4xl"
          style={{ background: "linear-gradient(135deg, #3b82f6, #8b5cf6)" }}
        >
          🤖
        </div>
        <h1 className="text-3xl font-bold mb-3" style={{ color: "var(--text)" }}>
          AliTerra AI
        </h1>
        <p className="text-base max-w-md" style={{ color: "var(--muted)" }}>
          Your AI Unity developer that <strong style={{ color: "var(--text)" }}>sees</strong>,{" "}
          <strong style={{ color: "var(--text)" }}>reads</strong>, and{" "}
          <strong style={{ color: "var(--text)" }}>edits</strong> every file in your project autonomously
        </p>
      </div>

      <div className="grid grid-cols-3 gap-4 max-w-2xl w-full">
        {[
          { icon: "📁", title: "Full File Access", desc: "Reads all scripts, scenes, prefabs, shaders" },
          { icon: "✍️", title: "Auto-writes Code", desc: "Creates and modifies C# scripts directly" },
          { icon: "📋", title: "Self-debugging", desc: "Reads console errors and fixes them automatically" },
          { icon: "🎮", title: "Scene Control", desc: "Creates GameObjects, adds components in real-time" },
          { icon: "🔄", title: "Tool Use (AI)", desc: "GPT-4o with 10 specialized Unity tools" },
          { icon: "⚡", title: "3s Polling", desc: "Unity plugin polls & executes commands instantly" },
        ].map((f) => (
          <div
            key={f.title}
            className="p-4 rounded-xl"
            style={{ background: "#1a1f35", border: "1px solid var(--border)" }}
          >
            <div className="text-2xl mb-2">{f.icon}</div>
            <div className="font-semibold text-sm mb-1" style={{ color: "var(--text)" }}>
              {f.title}
            </div>
            <div className="text-xs" style={{ color: "var(--muted)" }}>
              {f.desc}
            </div>
          </div>
        ))}
      </div>

      <div
        className="text-sm px-6 py-3 rounded-xl"
        style={{
          background: "rgba(59,130,246,0.08)",
          border: "1px solid rgba(59,130,246,0.2)",
          color: "var(--muted)",
        }}
      >
        ← Create a project in the sidebar to get started
      </div>
    </div>
  );
}

// ── Plugin Modal ───────────────────────────────────────────────────────────
function PluginModal({
  project,
  onClose,
}: {
  project: Project;
  onClose: () => void;
}) {
  const downloadUrl = `/api/plugin/download?projectId=${project.id}`;

  const steps = [
    {
      num: 1,
      title: "Download Plugin",
      desc: "Click the button below to download AliTerraAI.cs with your project API key baked in",
    },
    {
      num: 2,
      title: "Install in Unity",
      desc: "Place the file in your Unity project at: Assets/Editor/AliTerraAI.cs",
    },
    {
      num: 3,
      title: "Open Plugin Window",
      desc: 'In Unity, go to Window → AliTerra → AI Coder (or Ctrl+Shift+A)',
    },
    {
      num: 4,
      title: "Sync Project",
      desc: 'Click "🔄 Sync Project" to upload all your files to the AI server',
    },
    {
      num: 5,
      title: "Enable Polling",
      desc: 'Turn on "● POLL ON" so the AI can write files and create objects',
    },
    {
      num: 6,
      title: "Start Chatting",
      desc: "Come back here and chat with AI. It will control Unity autonomously!",
    },
  ];

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      style={{ background: "rgba(0,0,0,0.8)" }}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div
        className="rounded-2xl p-6 max-w-lg w-full max-h-[90vh] overflow-y-auto"
        style={{ background: "var(--panel)", border: "1px solid var(--border)" }}
      >
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold" style={{ color: "var(--text)" }}>
            🔌 Install Unity Plugin
          </h2>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full flex items-center justify-center"
            style={{ background: "#1e2540", color: "var(--muted)", cursor: "pointer" }}
          >
            ✕
          </button>
        </div>

        <div
          className="rounded-xl p-4 mb-5"
          style={{
            background: "rgba(34,197,94,0.08)",
            border: "1px solid rgba(34,197,94,0.2)",
          }}
        >
          <div className="text-sm font-semibold mb-1" style={{ color: "var(--green)" }}>
            {project.name}
          </div>
          <div className="text-xs font-mono" style={{ color: "var(--muted)" }}>
            API Key: {project.apiKey}
          </div>
        </div>

        <a
          href={downloadUrl}
          download="AliTerraAI.cs"
          className="w-full flex items-center justify-center gap-2 py-3 rounded-xl text-sm font-bold mb-6 transition-all hover:scale-[1.02]"
          style={{
            background: "linear-gradient(135deg, #8b5cf6, #3b82f6)",
            color: "white",
            textDecoration: "none",
            display: "flex",
          }}
        >
          ↓ Download AliTerraAI.cs
        </a>

        <div className="flex flex-col gap-3">
          {steps.map((step) => (
            <div key={step.num} className="flex gap-3">
              <div
                className="w-7 h-7 rounded-full flex items-center justify-center text-sm font-bold flex-shrink-0"
                style={{
                  background: "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                  color: "white",
                }}
              >
                {step.num}
              </div>
              <div>
                <div className="text-sm font-semibold" style={{ color: "var(--text)" }}>
                  {step.title}
                </div>
                <div className="text-xs mt-0.5" style={{ color: "var(--muted)" }}>
                  {step.desc}
                </div>
              </div>
            </div>
          ))}
        </div>

        <div
          className="mt-5 p-3 rounded-xl text-xs"
          style={{
            background: "rgba(59,130,246,0.08)",
            border: "1px solid rgba(59,130,246,0.2)",
            color: "var(--muted)",
          }}
        >
          💡 The plugin creates automatic backups in .vibe-backups/ before overwriting files. Polling happens every 3 seconds.
        </div>
      </div>
    </div>
  );
}

// ── Quick Prompts ──────────────────────────────────────────────────────────
const QUICK_PROMPTS = [
  {
    icon: "👁️",
    label: "Analyze Project",
    desc: "Scan all files & give overview",
    prompt:
      "Analyze my Unity project: list all scripts, scenes and prefabs. Describe the architecture and what kind of game this is.",
  },
  {
    icon: "🐛",
    label: "Fix Errors",
    desc: "Read console & fix all errors",
    prompt:
      "Read the Unity console logs, identify all errors and exceptions, then fix them by modifying the relevant scripts.",
  },
  {
    icon: "✍️",
    label: "Write a Script",
    desc: "Create new C# component",
    prompt:
      "Create a new PlayerController.cs script with WASD movement, jump with Space, and smooth camera follow. Attach it to a Player GameObject.",
  },
  {
    icon: "🎮",
    label: "Create GameObjects",
    desc: "Add objects to the scene",
    prompt:
      "Create a simple level layout: a ground plane, 5 platform cubes at different heights, and a player capsule at position 0,1,0.",
  },
  {
    icon: "🔍",
    label: "Code Review",
    desc: "Find bugs & improvements",
    prompt:
      "Read all C# scripts in my project. Find potential bugs, performance issues, and suggest improvements.",
  },
  {
    icon: "🤖",
    label: "Add Enemy AI",
    desc: "NavMesh-based enemy",
    prompt:
      "Look at my existing scripts and scene, then add enemy AI that follows the player using NavMesh when within detection range, and turns red when chasing.",
  },
  {
    icon: "💾",
    label: "Save System",
    desc: "Player prefs save/load",
    prompt:
      "Create a complete save/load system using PlayerPrefs. Save player position, health, score. Add SaveManager.cs and UI buttons.",
  },
  {
    icon: "📋",
    label: "Project Summary",
    desc: "Full project status report",
    prompt:
      "Give me a full status report: list all files by category, check console for errors, describe scene hierarchy, and suggest next development steps.",
  },
];
