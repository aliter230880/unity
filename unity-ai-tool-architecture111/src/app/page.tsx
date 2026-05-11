"use client";

import { useState, useEffect, useRef, useCallback } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────
interface Project {
  id: string;
  name: string;
  unityVersion: string | null;
  apiKey: string;
  createdAt: string;
  updatedAt: string;
}

interface Session {
  id: string;
  projectId: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

interface Message {
  id: string;
  sessionId: string;
  role: "user" | "assistant" | "tool";
  content: string;
  toolCalls?: unknown;
  toolName?: string;
  createdAt: string;
}

interface FileEntry {
  id: number;
  path: string;
  type: string;
  sizeBytes: number | null;
  updatedAt: string;
}

interface ConsoleLog {
  id: number;
  logType: string;
  message: string;
  stackTrace: string | null;
  createdAt: string;
}

interface ProjectStatus {
  project: { id: string; name: string; unityVersion: string | null; updatedAt: string };
  stats: {
    fileCount: number;
    errorCount: number;
    pendingCommands: number;
    currentScene: string | null;
    lastSync: string;
  };
  recentLogs: ConsoleLog[];
}

// ─── Helpers ─────────────────────────────────────────────────────────────────
function formatTime(iso: string) {
  const d = new Date(iso);
  return d.toLocaleTimeString("ru", { hour: "2-digit", minute: "2-digit" });
}

function formatRelative(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  if (diff < 60000) return "just now";
  if (diff < 3600000) return Math.floor(diff / 60000) + "m ago";
  if (diff < 86400000) return Math.floor(diff / 3600000) + "h ago";
  return Math.floor(diff / 86400000) + "d ago";
}

function renderMarkdown(text: string): string {
  return text
    .replace(/```(\w*)\n([\s\S]*?)```/g, (_, lang, code) =>
      `<pre><code class="language-${lang}">${escapeHtml(code.trim())}</code></pre>`
    )
    .replace(/`([^`]+)`/g, `<code>$1</code>`)
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\*([^*]+)\*/g, "<em>$1</em>")
    .replace(/^### (.+)$/gm, "<h3>$1</h3>")
    .replace(/^## (.+)$/gm, "<h2>$1</h2>")
    .replace(/^# (.+)$/gm, "<h1>$1</h1>")
    .replace(/^- (.+)$/gm, "<li>$1</li>")
    .replace(/(<li>.*<\/li>\n?)+/g, "<ul>$&</ul>")
    .replace(/\n\n+/g, "</p><p>")
    .replace(/\n/g, "<br/>")
    .replace(/^(?!<[huplb])(.+)$/gm, "<p>$1</p>");
}

function escapeHtml(s: string) {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// ─── Chat Message Component ───────────────────────────────────────────────────
function ChatMessage({ msg, isStreaming }: { msg: Message; isStreaming?: boolean }) {
  const isUser = msg.role === "user";
  const isTool = msg.role === "tool";

  if (isTool) {
    return (
      <div className="animate-slide-in" style={{ margin: "4px 0 4px 40px" }}>
        <div style={{
          background: "rgba(79, 155, 255, 0.06)",
          border: "1px solid rgba(79, 155, 255, 0.2)",
          borderRadius: "8px",
          padding: "8px 12px",
          fontSize: "12px",
        }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
            <span style={{ color: "var(--accent)" }}>🔧</span>
            <span style={{ color: "var(--accent)", fontWeight: 600 }}>{msg.toolName || "tool"}</span>
          </div>
          <pre style={{
            fontSize: "11px",
            color: "var(--text-secondary)",
            whiteSpace: "pre-wrap",
            wordBreak: "break-word",
            maxHeight: "120px",
            overflow: "auto",
          }}>
            {msg.content.substring(0, 800)}{msg.content.length > 800 ? "..." : ""}
          </pre>
        </div>
      </div>
    );
  }

  return (
    <div className="animate-fade-in" style={{
      display: "flex",
      justifyContent: isUser ? "flex-end" : "flex-start",
      margin: "8px 0",
    }}>
      {!isUser && (
        <div style={{
          width: 28, height: 28, borderRadius: "50%",
          background: "linear-gradient(135deg, #4f9bff, #7c3aed)",
          display: "flex", alignItems: "center", justifyContent: "center",
          fontSize: "14px", flexShrink: 0, marginRight: 8, marginTop: 2,
        }}>🤖</div>
      )}
      <div style={{
        maxWidth: "75%",
        background: isUser
          ? "linear-gradient(135deg, #1d4ed8, #2563eb)"
          : "var(--bg-card)",
        border: isUser ? "none" : "1px solid var(--border)",
        borderRadius: isUser ? "18px 18px 4px 18px" : "4px 18px 18px 18px",
        padding: "10px 14px",
        position: "relative",
      }}>
        {isStreaming && (
          <div style={{
            position: "absolute", top: 8, right: 10,
            width: 8, height: 8, borderRadius: "50%",
            background: "var(--accent)", animation: "pulse 1s infinite",
          }} />
        )}
        <div
          className="msg-content"
          style={{ color: isUser ? "white" : "var(--text-primary)", fontSize: "13.5px", lineHeight: 1.6 }}
          dangerouslySetInnerHTML={{ __html: renderMarkdown(msg.content) }}
        />
        <div style={{ fontSize: "10px", color: isUser ? "rgba(255,255,255,0.5)" : "var(--text-muted)", marginTop: 4, textAlign: "right" }}>
          {formatTime(msg.createdAt)}
        </div>
      </div>
      {isUser && (
        <div style={{
          width: 28, height: 28, borderRadius: "50%",
          background: "var(--bg-hover)",
          border: "1px solid var(--border)",
          display: "flex", alignItems: "center", justifyContent: "center",
          fontSize: "13px", flexShrink: 0, marginLeft: 8, marginTop: 2,
        }}>👤</div>
      )}
    </div>
  );
}

// ─── Sidebar ──────────────────────────────────────────────────────────────────
function Sidebar({
  projects, sessions, selectedProject, selectedSession,
  onSelectProject, onSelectSession, onCreateProject, onCreateSession,
  onDeleteSession, status,
}: {
  projects: Project[];
  sessions: Session[];
  selectedProject: Project | null;
  selectedSession: Session | null;
  onSelectProject: (p: Project) => void;
  onSelectSession: (s: Session) => void;
  onCreateProject: () => void;
  onCreateSession: () => void;
  onDeleteSession: (id: string) => void;
  status: ProjectStatus | null;
}) {
  return (
    <div style={{
      width: 240, background: "var(--bg-secondary)", borderRight: "1px solid var(--border)",
      display: "flex", flexDirection: "column", height: "100%", flexShrink: 0,
    }}>
      {/* Header */}
      <div style={{
        padding: "16px 14px 12px",
        borderBottom: "1px solid var(--border)",
        background: "linear-gradient(180deg, #0d1520 0%, var(--bg-secondary) 100%)",
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 2 }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background: "linear-gradient(135deg, #4f9bff, #7c3aed)",
            display: "flex", alignItems: "center", justifyContent: "center", fontSize: "16px",
          }}>🤖</div>
          <div>
            <div style={{ fontWeight: 700, fontSize: "14px", color: "white" }}>AliTerra AI</div>
            <div style={{ fontSize: "10px", color: "var(--accent)" }}>Unity Fullstack Dev</div>
          </div>
        </div>
      </div>

      {/* Project selector */}
      <div style={{ padding: "10px 10px 6px", borderBottom: "1px solid var(--border)" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
          <span style={{ fontSize: "11px", color: "var(--text-muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Project</span>
          <button onClick={onCreateProject} style={{
            background: "none", border: "none", color: "var(--accent)", cursor: "pointer",
            fontSize: "16px", padding: "0 2px", lineHeight: 1,
          }} title="New project">+</button>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {projects.map(p => (
            <button key={p.id} onClick={() => onSelectProject(p)} style={{
              background: selectedProject?.id === p.id ? "var(--accent-glow)" : "none",
              border: `1px solid ${selectedProject?.id === p.id ? "rgba(79,155,255,0.4)" : "transparent"}`,
              borderRadius: 6, padding: "6px 8px", cursor: "pointer", textAlign: "left",
              color: selectedProject?.id === p.id ? "var(--accent)" : "var(--text-secondary)",
              fontSize: "12px", transition: "all 0.15s",
            }}>
              <div style={{ fontWeight: 600 }}>🎮 {p.name}</div>
              {p.unityVersion && <div style={{ fontSize: "10px", opacity: 0.6 }}>Unity {p.unityVersion}</div>}
            </button>
          ))}
        </div>
      </div>

      {/* Status panel */}
      {status && (
        <div style={{ padding: "8px 10px", borderBottom: "1px solid var(--border)" }}>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
            <div style={{
              background: "var(--bg-card)", border: "1px solid var(--border)",
              borderRadius: 6, padding: "4px 8px", fontSize: "11px", flex: 1,
            }}>
              <div style={{ color: "var(--text-muted)" }}>Files</div>
              <div style={{ color: "var(--cyan)", fontWeight: 700 }}>{status.stats.fileCount}</div>
            </div>
            <div style={{
              background: "var(--bg-card)", border: "1px solid var(--border)",
              borderRadius: 6, padding: "4px 8px", fontSize: "11px", flex: 1,
            }}>
              <div style={{ color: "var(--text-muted)" }}>Errors</div>
              <div style={{ color: status.stats.errorCount > 0 ? "var(--red)" : "var(--green)", fontWeight: 700 }}>
                {status.stats.errorCount}
              </div>
            </div>
            <div style={{
              background: "var(--bg-card)", border: "1px solid var(--border)",
              borderRadius: 6, padding: "4px 8px", fontSize: "11px", flex: 1,
            }}>
              <div style={{ color: "var(--text-muted)" }}>Cmds</div>
              <div style={{ color: status.stats.pendingCommands > 0 ? "var(--yellow)" : "var(--text-secondary)", fontWeight: 700 }}>
                {status.stats.pendingCommands}
              </div>
            </div>
          </div>
          {status.stats.currentScene && (
            <div style={{ fontSize: "10px", color: "var(--text-muted)", marginTop: 4 }}>
              🎬 {status.stats.currentScene}
            </div>
          )}
        </div>
      )}

      {/* Sessions */}
      <div style={{ flex: 1, overflow: "hidden", display: "flex", flexDirection: "column" }}>
        <div style={{ padding: "8px 10px 4px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <span style={{ fontSize: "11px", color: "var(--text-muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Sessions</span>
          <button onClick={onCreateSession} style={{
            background: "none", border: "none", color: "var(--accent)", cursor: "pointer",
            fontSize: "16px", padding: "0 2px", lineHeight: 1,
          }} title="New session">+</button>
        </div>
        <div style={{ flex: 1, overflowY: "auto", padding: "0 6px 8px" }}>
          {sessions.map(s => (
            <div key={s.id} style={{
              display: "flex", alignItems: "center", gap: 4,
              background: selectedSession?.id === s.id ? "var(--accent-glow)" : "none",
              border: `1px solid ${selectedSession?.id === s.id ? "rgba(79,155,255,0.3)" : "transparent"}`,
              borderRadius: 6, margin: "2px 0", padding: "2px 2px 2px 6px",
            }}>
              <button onClick={() => onSelectSession(s)} style={{
                background: "none", border: "none", cursor: "pointer", flex: 1,
                textAlign: "left", color: selectedSession?.id === s.id ? "var(--accent)" : "var(--text-secondary)",
                fontSize: "12px", padding: "4px 0",
              }}>
                <div style={{ fontWeight: 500, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: 155 }}>
                  💬 {s.title}
                </div>
                <div style={{ fontSize: "10px", color: "var(--text-muted)" }}>{formatRelative(s.updatedAt)}</div>
              </button>
              <button onClick={() => onDeleteSession(s.id)} style={{
                background: "none", border: "none", color: "var(--text-muted)", cursor: "pointer",
                fontSize: "12px", padding: "4px", opacity: 0.5, flexShrink: 0,
              }} title="Delete">×</button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── Files Panel ──────────────────────────────────────────────────────────────
function FilesPanel({ projectId, onReadFile }: { projectId: string; onReadFile: (path: string) => void }) {
  const [files, setFiles] = useState<FileEntry[]>([]);
  const [filter, setFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!projectId) return;
    setLoading(true);
    fetch(`/api/unity/files?projectId=${projectId}`)
      .then(r => r.json())
      .then(data => { setFiles(Array.isArray(data) ? data : []); setLoading(false); })
      .catch(() => setLoading(false));
  }, [projectId]);

  const types = ["all", "script", "scene", "prefab", "material", "shader", "config", "other"];

  const filtered = files.filter(f => {
    if (typeFilter !== "all" && f.type !== typeFilter) return false;
    if (filter && !f.path.toLowerCase().includes(filter.toLowerCase())) return false;
    return true;
  });

  const typeIcons: Record<string, string> = {
    script: "📜", scene: "🎬", prefab: "🧊", material: "🎨",
    shader: "✨", config: "⚙️", other: "📄", audio: "🔊", model: "📦",
  };

  const grouped: Record<string, FileEntry[]> = {};
  for (const f of filtered) {
    if (!grouped[f.type]) grouped[f.type] = [];
    grouped[f.type].push(f);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", background: "var(--bg-secondary)", borderLeft: "1px solid var(--border)" }}>
      <div style={{ padding: "12px 10px", borderBottom: "1px solid var(--border)" }}>
        <div style={{ fontWeight: 600, fontSize: "12px", color: "var(--text-secondary)", marginBottom: 8 }}>
          📁 PROJECT FILES {files.length > 0 ? `(${files.length})` : ""}
        </div>
        <input
          value={filter}
          onChange={e => setFilter(e.target.value)}
          placeholder="Search files..."
          style={{
            width: "100%", background: "var(--bg-card)", border: "1px solid var(--border)",
            borderRadius: 6, padding: "5px 8px", color: "var(--text-primary)", fontSize: "11px",
            outline: "none", marginBottom: 6,
          }}
        />
        <div style={{ display: "flex", flexWrap: "wrap", gap: 3 }}>
          {types.map(t => (
            <button key={t} onClick={() => setTypeFilter(t)} style={{
              background: typeFilter === t ? "var(--accent-glow)" : "var(--bg-card)",
              border: `1px solid ${typeFilter === t ? "rgba(79,155,255,0.4)" : "var(--border)"}`,
              borderRadius: 4, padding: "2px 6px", color: typeFilter === t ? "var(--accent)" : "var(--text-muted)",
              fontSize: "10px", cursor: "pointer",
            }}>{t}</button>
          ))}
        </div>
      </div>

      <div style={{ flex: 1, overflowY: "auto", padding: "6px 0" }}>
        {loading ? (
          <div style={{ padding: 20, textAlign: "center", color: "var(--text-muted)", fontSize: "12px" }}>
            Loading files...
          </div>
        ) : Object.entries(grouped).length === 0 ? (
          <div style={{ padding: 16, textAlign: "center", color: "var(--text-muted)", fontSize: "12px" }}>
            {files.length === 0 ? "No files synced yet.\nSync from Unity plugin." : "No matches."}
          </div>
        ) : (
          Object.entries(grouped).map(([type, typeFiles]) => (
            <div key={type}>
              <div style={{ padding: "4px 10px 2px", fontSize: "10px", color: "var(--text-muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                {typeIcons[type] || "📄"} {type} ({typeFiles.length})
              </div>
              {typeFiles.map(f => (
                <button key={f.id} onClick={() => onReadFile(f.path)} style={{
                  width: "100%", background: "none", border: "none", padding: "4px 10px",
                  textAlign: "left", cursor: "pointer", color: "var(--text-secondary)",
                  fontSize: "11px", display: "flex", alignItems: "center", gap: 6,
                  transition: "background 0.1s",
                }} onMouseEnter={e => (e.currentTarget.style.background = "var(--bg-hover)")}
                  onMouseLeave={e => (e.currentTarget.style.background = "none")}>
                  <span style={{ flexShrink: 0 }}>{typeIcons[type] || "📄"}</span>
                  <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={f.path}>
                    {f.path.split("/").pop()}
                  </span>
                  <span style={{ marginLeft: "auto", fontSize: "10px", color: "var(--text-muted)", flexShrink: 0 }}>
                    {f.sizeBytes ? Math.round(f.sizeBytes / 1024) + "KB" : ""}
                  </span>
                </button>
              ))}
            </div>
          ))
        )}
      </div>
    </div>
  );
}

// ─── Logs Panel ───────────────────────────────────────────────────────────────
function LogsPanel({ logs }: { logs: ConsoleLog[] }) {
  const colors: Record<string, string> = {
    error: "var(--red)", warning: "var(--yellow)", log: "var(--text-secondary)",
    compiler_error: "var(--red)", exception: "var(--red)",
  };
  const icons: Record<string, string> = {
    error: "❌", warning: "⚠️", log: "ℹ️", compiler_error: "🔴", exception: "💥",
  };

  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <div style={{ padding: "8px 12px", borderBottom: "1px solid var(--border)", fontSize: "11px", color: "var(--text-muted)", textTransform: "uppercase" }}>
        Unity Console ({logs.length})
      </div>
      <div style={{ flex: 1, overflowY: "auto", fontSize: "11px" }}>
        {logs.length === 0 ? (
          <div style={{ padding: 12, color: "var(--text-muted)", textAlign: "center" }}>No logs yet</div>
        ) : logs.map(l => (
          <div key={l.id} style={{
            padding: "4px 10px", borderBottom: "1px solid rgba(255,255,255,0.03)",
            display: "flex", gap: 6, alignItems: "flex-start",
          }}>
            <span>{icons[l.logType] || "ℹ️"}</span>
            <div style={{ flex: 1, overflow: "hidden" }}>
              <div style={{ color: colors[l.logType] || "var(--text-secondary)", wordBreak: "break-word" }}>
                {l.message}
              </div>
              {l.stackTrace && (
                <div style={{ color: "var(--text-muted)", fontSize: "10px", marginTop: 2, wordBreak: "break-word" }}>
                  {l.stackTrace.substring(0, 150)}
                </div>
              )}
            </div>
            <div style={{ fontSize: "9px", color: "var(--text-muted)", flexShrink: 0 }}>
              {formatTime(l.createdAt)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Main App ─────────────────────────────────────────────────────────────────
export default function App() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [selectedProject, setSelectedProject] = useState<Project | null>(null);
  const [selectedSession, setSelectedSession] = useState<Session | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [streamingMsg, setStreamingMsg] = useState<string | null>(null);
  const [toolActivity, setToolActivity] = useState("");
  const [status, setStatus] = useState<ProjectStatus | null>(null);
  const [logs, setLogs] = useState<ConsoleLog[]>([]);
  const [rightPanel, setRightPanel] = useState<"files" | "logs" | "setup">("setup");
  const [showApiKey, setShowApiKey] = useState(false);
  const [setupDone, setSetupDone] = useState(false);

  const chatEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // ─── Load projects ───────────────────────────────────────────────────────
  useEffect(() => {
    fetch("/api/projects")
      .then(r => r.json())
      .then((data: Project[]) => {
        if (Array.isArray(data) && data.length > 0) {
          setProjects(data);
          setSelectedProject(data[0]);
          setSetupDone(true);
        }
      });
  }, []);

  // ─── Load sessions when project changes ──────────────────────────────────
  useEffect(() => {
    if (!selectedProject) return;
    fetch(`/api/sessions?projectId=${selectedProject.id}`)
      .then(r => r.json())
      .then((data: Session[]) => {
        setSessions(Array.isArray(data) ? data : []);
        if (data.length > 0) setSelectedSession(data[0]);
        else setSelectedSession(null);
      });
  }, [selectedProject]);

  // ─── Load messages when session changes ──────────────────────────────────
  useEffect(() => {
    if (!selectedSession) { setMessages([]); return; }
    fetch(`/api/messages?sessionId=${selectedSession.id}`)
      .then(r => r.json())
      .then((data: Message[]) => setMessages(Array.isArray(data) ? data : []));
  }, [selectedSession]);

  // ─── Poll status + logs ───────────────────────────────────────────────────
  useEffect(() => {
    if (!selectedProject) return;
    const poll = async () => {
      try {
        const [statusRes, logsRes] = await Promise.all([
          fetch(`/api/unity/status?projectId=${selectedProject.id}`),
          fetch(`/api/unity/logs?apiKey=${selectedProject.apiKey}&limit=50`),
        ]);
        if (statusRes.ok) setStatus(await statusRes.json());
        if (logsRes.ok) {
          const logsData = await logsRes.json();
          setLogs(Array.isArray(logsData) ? logsData : []);
        }
      } catch { /* ignore */ }
    };
    poll();
    const interval = setInterval(poll, 5000);
    return () => clearInterval(interval);
  }, [selectedProject]);

  // ─── Scroll chat to bottom ────────────────────────────────────────────────
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, streamingMsg]);

  // ─── Create project ───────────────────────────────────────────────────────
  const handleCreateProject = useCallback(async () => {
    const name = prompt("Project name:", "My Unity Project");
    if (!name) return;
    const res = await fetch("/api/projects", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    });
    const project: Project = await res.json();
    setProjects(prev => [...prev, project]);
    setSelectedProject(project);
    setSetupDone(true);
    setRightPanel("setup");
  }, []);

  // ─── Create session ───────────────────────────────────────────────────────
  const handleCreateSession = useCallback(async () => {
    if (!selectedProject) return;
    const res = await fetch("/api/sessions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ projectId: selectedProject.id, title: "New Session" }),
    });
    const session: Session = await res.json();
    setSessions(prev => [session, ...prev]);
    setSelectedSession(session);
    setMessages([]);
  }, [selectedProject]);

  // ─── Delete session ───────────────────────────────────────────────────────
  const handleDeleteSession = useCallback(async (id: string) => {
    await fetch(`/api/sessions?id=${id}`, { method: "DELETE" });
    setSessions(prev => prev.filter(s => s.id !== id));
    if (selectedSession?.id === id) {
      setSelectedSession(null);
      setMessages([]);
    }
  }, [selectedSession]);

  // ─── Send message ─────────────────────────────────────────────────────────
  const handleSend = useCallback(async () => {
    const msg = input.trim();
    if (!msg || isLoading || !selectedSession || !selectedProject) return;

    setInput("");
    setIsLoading(true);
    setStreamingMsg("");
    setToolActivity("");

    const userMsg: Message = {
      id: `tmp-${Date.now()}`,
      sessionId: selectedSession.id,
      role: "user",
      content: msg,
      createdAt: new Date().toISOString(),
    };
    setMessages(prev => [...prev, userMsg]);

    try {
      const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          message: msg,
          sessionId: selectedSession.id,
          projectId: selectedProject.id,
        }),
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }

      const reader = res.body?.getReader();
      if (!reader) throw new Error("No response body");

      const decoder = new TextDecoder();
      let buffer = "";
      let finalContent = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        const lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          const data = line.slice(6).trim();
          if (data === "[DONE]") break;
          try {
            const parsed = JSON.parse(data) as { type: string; content: string };
            if (parsed.type === "tool") {
              setToolActivity(prev => prev + parsed.content);
            } else if (parsed.type === "final") {
              finalContent = parsed.content;
              setStreamingMsg(finalContent);
            } else if (parsed.type === "error") {
              finalContent = `❌ Error: ${parsed.content}`;
              setStreamingMsg(finalContent);
            }
          } catch { /* ignore parse errors */ }
        }
      }

      // Reload messages from DB to get tool messages too
      const freshMsgs = await fetch(`/api/messages?sessionId=${selectedSession.id}`)
        .then(r => r.json()) as Message[];
      setMessages(Array.isArray(freshMsgs) ? freshMsgs : []);

      // Refresh sessions to update title
      const freshSessions = await fetch(`/api/sessions?projectId=${selectedProject.id}`)
        .then(r => r.json()) as Session[];
      setSessions(Array.isArray(freshSessions) ? freshSessions : []);
      const updated = freshSessions.find(s => s.id === selectedSession.id);
      if (updated) setSelectedSession(updated);

    } catch (e) {
      const errMsg: Message = {
        id: `err-${Date.now()}`,
        sessionId: selectedSession.id,
        role: "assistant",
        content: `❌ Error: ${String(e)}`,
        createdAt: new Date().toISOString(),
      };
      setMessages(prev => [...prev, errMsg]);
    } finally {
      setIsLoading(false);
      setStreamingMsg(null);
      setToolActivity("");
    }
  }, [input, isLoading, selectedSession, selectedProject]);

  // ─── Read file into chat ──────────────────────────────────────────────────
  const handleReadFile = useCallback((path: string) => {
    setInput(`Read and explain the file: ${path}`);
    textareaRef.current?.focus();
  }, []);

  // ─── Setup Panel ──────────────────────────────────────────────────────────
  const SetupPanel = () => (
    <div style={{
      flex: 1, display: "flex", flexDirection: "column", alignItems: "center",
      justifyContent: "flex-start", padding: 20, overflowY: "auto", background: "var(--bg-secondary)", borderLeft: "1px solid var(--border)",
    }}>
      <div style={{ width: "100%", maxWidth: 420 }}>
        <div style={{ textAlign: "center", marginBottom: 20 }}>
          <div style={{ fontSize: 32, marginBottom: 8 }}>🎮</div>
          <div style={{ fontWeight: 700, fontSize: 16, color: "white" }}>Connect Unity Plugin</div>
          <div style={{ fontSize: "12px", color: "var(--text-muted)", marginTop: 4 }}>
            Download and install the plugin in your Unity project
          </div>
        </div>

        {selectedProject && (
          <>
            {/* API Key */}
            <div style={{
              background: "var(--bg-card)", border: "1px solid var(--border)",
              borderRadius: 10, padding: 14, marginBottom: 12,
            }}>
              <div style={{ fontSize: "11px", color: "var(--text-muted)", marginBottom: 6, textTransform: "uppercase" }}>API Key</div>
              <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <code style={{
                  flex: 1, background: "var(--bg-primary)", border: "1px solid var(--border)",
                  borderRadius: 6, padding: "6px 10px", fontSize: "11px",
                  color: "var(--cyan)", wordBreak: "break-all",
                }}>
                  {showApiKey ? selectedProject.apiKey : selectedProject.apiKey.substring(0, 8) + "●".repeat(20)}
                </code>
                <button onClick={() => setShowApiKey(v => !v)} style={{
                  background: "var(--bg-hover)", border: "1px solid var(--border)",
                  borderRadius: 6, padding: "6px 10px", color: "var(--text-secondary)",
                  cursor: "pointer", fontSize: "12px",
                }}>{showApiKey ? "🙈" : "👁"}</button>
                <button onClick={() => navigator.clipboard.writeText(selectedProject.apiKey)} style={{
                  background: "var(--bg-hover)", border: "1px solid var(--border)",
                  borderRadius: 6, padding: "6px 10px", color: "var(--text-secondary)",
                  cursor: "pointer", fontSize: "12px",
                }}>📋</button>
              </div>
            </div>

            {/* Download plugin */}
            <a href={`/api/plugin/download?projectId=${selectedProject.id}`} download="AliTerraAI.cs" style={{
              display: "block", background: "linear-gradient(135deg, #1d4ed8, #4f9bff)",
              borderRadius: 10, padding: "12px 16px", textDecoration: "none", marginBottom: 12,
              textAlign: "center", color: "white", fontWeight: 600, fontSize: "14px",
            }}>
              ⬇️ Download AliTerraAI.cs Plugin
            </a>

            {/* Instructions */}
            <div style={{
              background: "var(--bg-card)", border: "1px solid var(--border)",
              borderRadius: 10, padding: 14, fontSize: "12px", color: "var(--text-secondary)",
            }}>
              <div style={{ fontWeight: 600, color: "var(--text-primary)", marginBottom: 10 }}>📋 Installation Steps</div>
              {[
                "1. Download AliTerraAI.cs above",
                "2. Place it in Assets/Editor/ folder in Unity",
                "3. Unity compiles the plugin automatically",
                "4. Open Window → AliTerra → AI Coder (Ctrl+Shift+A)",
                "5. Click '🔄 Sync All Files' in Fullstack tab",
                "6. Enable Polling checkbox",
                "7. Come back here and start chatting!",
              ].map((step, i) => (
                <div key={i} style={{ padding: "4px 0", display: "flex", gap: 8, alignItems: "flex-start" }}>
                  <span style={{ color: "var(--accent)", flexShrink: 0 }}>→</span>
                  <span>{step}</span>
                </div>
              ))}
            </div>

            {/* Connection status */}
            {status && (
              <div style={{
                background: "var(--bg-card)", border: `1px solid ${status.stats.fileCount > 0 ? "rgba(62,207,142,0.4)" : "var(--border)"}`,
                borderRadius: 10, padding: 12, marginTop: 12,
                display: "flex", alignItems: "center", gap: 10,
              }}>
                <div className={`status-dot ${status.stats.fileCount > 0 ? "green" : "gray"}`} />
                <div>
                  <div style={{ fontSize: "12px", fontWeight: 600, color: status.stats.fileCount > 0 ? "var(--green)" : "var(--text-secondary)" }}>
                    {status.stats.fileCount > 0 ? `✅ Connected — ${status.stats.fileCount} files synced` : "⏳ Waiting for plugin connection..."}
                  </div>
                  {status.stats.currentScene && (
                    <div style={{ fontSize: "10px", color: "var(--text-muted)" }}>Scene: {status.stats.currentScene}</div>
                  )}
                </div>
              </div>
            )}

            {/* Context file */}
            <a href="/CONTEXT.md" target="_blank" style={{
              display: "block", marginTop: 10, padding: "8px 12px",
              background: "rgba(167, 139, 250, 0.1)", border: "1px solid rgba(167, 139, 250, 0.3)",
              borderRadius: 8, color: "var(--purple)", textDecoration: "none",
              fontSize: "12px", textAlign: "center",
            }}>
              📄 View CONTEXT.md (Architecture Guide)
            </a>
          </>
        )}

        {!selectedProject && (
          <button onClick={handleCreateProject} style={{
            width: "100%", background: "linear-gradient(135deg, #1d4ed8, #4f9bff)",
            border: "none", borderRadius: 10, padding: "12px 16px",
            color: "white", fontWeight: 600, fontSize: "14px", cursor: "pointer",
          }}>
            + Create First Project
          </button>
        )}
      </div>
    </div>
  );

  // ─── Render ───────────────────────────────────────────────────────────────
  return (
    <div style={{ display: "flex", height: "100vh", overflow: "hidden" }}>
      {/* Sidebar */}
      <Sidebar
        projects={projects}
        sessions={sessions}
        selectedProject={selectedProject}
        selectedSession={selectedSession}
        onSelectProject={p => { setSelectedProject(p); setSetupDone(true); }}
        onSelectSession={setSelectedSession}
        onCreateProject={handleCreateProject}
        onCreateSession={handleCreateSession}
        onDeleteSession={handleDeleteSession}
        status={status}
      />

      {/* Main chat area */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        {/* Top bar */}
        <div style={{
          height: 48, display: "flex", alignItems: "center", padding: "0 16px",
          borderBottom: "1px solid var(--border)", background: "var(--bg-secondary)",
          gap: 12, flexShrink: 0,
        }}>
          <div style={{ flex: 1, overflow: "hidden" }}>
            {selectedSession ? (
              <div style={{ fontWeight: 600, fontSize: "13px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {selectedSession.title}
              </div>
            ) : (
              <div style={{ color: "var(--text-muted)", fontSize: "13px" }}>
                {selectedProject ? "Select or create a session" : "Create a project to get started"}
              </div>
            )}
          </div>

          {/* Right panel tabs */}
          <div style={{ display: "flex", gap: 4 }}>
            {(["setup", "files", "logs"] as const).map(panel => (
              <button key={panel} onClick={() => setRightPanel(panel)} style={{
                background: rightPanel === panel ? "var(--accent-glow)" : "none",
                border: `1px solid ${rightPanel === panel ? "rgba(79,155,255,0.4)" : "var(--border)"}`,
                borderRadius: 6, padding: "4px 10px",
                color: rightPanel === panel ? "var(--accent)" : "var(--text-muted)",
                cursor: "pointer", fontSize: "11px",
              }}>
                {panel === "setup" ? "🔌 Setup" : panel === "files" ? "📁 Files" : "🖥 Logs"}
              </button>
            ))}
          </div>
        </div>

        {/* Messages */}
        <div style={{ flex: 1, overflowY: "auto", padding: "16px 20px" }}>
          {!selectedSession ? (
            <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", gap: 16 }}>
              <div style={{ fontSize: 48 }}>🤖</div>
              <div style={{ textAlign: "center" }}>
                <div style={{ fontSize: "20px", fontWeight: 700, color: "white" }}>AliTerra AI</div>
                <div style={{ fontSize: "14px", color: "var(--text-muted)", marginTop: 6, maxWidth: 400 }}>
                  Your Unity fullstack developer. Reads, writes, and controls your entire Unity project through AI.
                </div>
              </div>
              {selectedProject ? (
                <button onClick={handleCreateSession} style={{
                  background: "linear-gradient(135deg, #1d4ed8, #4f9bff)",
                  border: "none", borderRadius: 10, padding: "10px 20px",
                  color: "white", fontWeight: 600, cursor: "pointer", fontSize: "13px",
                }}>
                  + New Chat Session
                </button>
              ) : (
                <button onClick={handleCreateProject} style={{
                  background: "linear-gradient(135deg, #1d4ed8, #4f9bff)",
                  border: "none", borderRadius: 10, padding: "10px 20px",
                  color: "white", fontWeight: 600, cursor: "pointer", fontSize: "13px",
                }}>
                  + Create Project
                </button>
              )}

              {/* Quick prompts */}
              {selectedSession && (
                <div style={{ display: "flex", flexWrap: "wrap", gap: 8, justifyContent: "center", maxWidth: 500 }}>
                  {[
                    "Show me all scripts in the project",
                    "Create an enemy AI with patrol behavior",
                    "Add a health system to the player",
                    "Fix all compilation errors",
                    "Create a basic inventory system",
                    "Add a day-night cycle",
                  ].map(prompt => (
                    <button key={prompt} onClick={() => setInput(prompt)} style={{
                      background: "var(--bg-card)", border: "1px solid var(--border)",
                      borderRadius: 20, padding: "6px 12px", color: "var(--text-secondary)",
                      cursor: "pointer", fontSize: "12px", transition: "all 0.15s",
                    }} onMouseEnter={e => {
                      e.currentTarget.style.borderColor = "rgba(79,155,255,0.4)";
                      e.currentTarget.style.color = "var(--accent)";
                    }} onMouseLeave={e => {
                      e.currentTarget.style.borderColor = "var(--border)";
                      e.currentTarget.style.color = "var(--text-secondary)";
                    }}>
                      {prompt}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <>
              {messages.length === 0 && !isLoading && (
                <div style={{ textAlign: "center", padding: 30, color: "var(--text-muted)", fontSize: "13px" }}>
                  <div style={{ fontSize: 24, marginBottom: 8 }}>💬</div>
                  <div>Ask me anything about your Unity project</div>
                  <div style={{ marginTop: 12, display: "flex", flexWrap: "wrap", gap: 8, justifyContent: "center" }}>
                    {[
                      "List all scripts in the project",
                      "Create an enemy AI with patrol behavior",
                      "Add a health system to the player",
                      "Fix all compilation errors",
                    ].map(prompt => (
                      <button key={prompt} onClick={() => setInput(prompt)} style={{
                        background: "var(--bg-card)", border: "1px solid var(--border)",
                        borderRadius: 20, padding: "6px 12px", color: "var(--text-secondary)",
                        cursor: "pointer", fontSize: "12px",
                      }}>
                        {prompt}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {messages
                .filter(m => m.role === "user" || m.role === "assistant" || m.role === "tool")
                .map(m => (
                  <ChatMessage key={m.id} msg={m} />
                ))}

              {/* Tool activity */}
              {isLoading && toolActivity && (
                <div className="animate-fade-in" style={{
                  margin: "6px 0 6px 40px",
                  background: "rgba(79,155,255,0.05)",
                  border: "1px solid rgba(79,155,255,0.15)",
                  borderRadius: 8, padding: "8px 12px",
                }}>
                  <div style={{ fontSize: "11px", color: "var(--accent)", marginBottom: 4 }}>
                    🔧 AI is working...
                  </div>
                  <pre style={{ fontSize: "10px", color: "var(--text-muted)", whiteSpace: "pre-wrap", wordBreak: "break-word", maxHeight: 80, overflow: "hidden" }}>
                    {toolActivity.substring(toolActivity.length - 400)}
                  </pre>
                </div>
              )}

              {/* Streaming */}
              {isLoading && streamingMsg !== null && (
                <div className="animate-fade-in" style={{ display: "flex", gap: 8, margin: "8px 0" }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: "50%",
                    background: "linear-gradient(135deg, #4f9bff, #7c3aed)",
                    display: "flex", alignItems: "center", justifyContent: "center", fontSize: "14px", flexShrink: 0,
                  }}>🤖</div>
                  <div style={{
                    background: "var(--bg-card)", border: "1px solid var(--border)",
                    borderRadius: "4px 18px 18px 18px", padding: "10px 14px", maxWidth: "75%",
                  }}>
                    <div className="msg-content" dangerouslySetInnerHTML={{ __html: renderMarkdown(streamingMsg || "⏳") }} />
                    <div style={{ width: 6, height: 14, background: "var(--accent)", display: "inline-block", animation: "pulse 1s infinite", marginLeft: 2 }} />
                  </div>
                </div>
              )}

              {/* Loading spinner */}
              {isLoading && streamingMsg === null && !toolActivity && (
                <div className="animate-fade-in" style={{ display: "flex", alignItems: "center", gap: 10, margin: "8px 0", padding: "0 0 0 36px" }}>
                  <div style={{ display: "flex", gap: 4 }}>
                    {[0, 1, 2].map(i => (
                      <div key={i} style={{
                        width: 6, height: 6, borderRadius: "50%", background: "var(--accent)",
                        animation: `pulse 1.4s ease ${i * 0.2}s infinite`,
                      }} />
                    ))}
                  </div>
                  <span style={{ fontSize: "12px", color: "var(--text-muted)" }}>AI is thinking...</span>
                </div>
              )}

              <div ref={chatEndRef} />
            </>
          )}
        </div>

        {/* Input area */}
        <div style={{
          padding: "12px 16px", borderTop: "1px solid var(--border)",
          background: "var(--bg-secondary)", flexShrink: 0,
        }}>
          <div style={{
            display: "flex", gap: 10, alignItems: "flex-end",
            background: "var(--bg-card)", border: "1px solid var(--border-bright)",
            borderRadius: 12, padding: "8px 8px 8px 14px",
            transition: "border-color 0.2s",
          }}>
            <textarea
              ref={textareaRef}
              value={input}
              onChange={e => {
                setInput(e.target.value);
                e.target.style.height = "auto";
                e.target.style.height = Math.min(e.target.scrollHeight, 160) + "px";
              }}
              onKeyDown={e => {
                if (e.key === "Enter" && !e.shiftKey) {
                  e.preventDefault();
                  handleSend();
                }
              }}
              placeholder={
                !selectedProject ? "Create a project first..." :
                !selectedSession ? "Create a session to start chatting..." :
                "Ask AI to build, fix, or explain anything in your Unity project... (Shift+Enter for newline)"
              }
              disabled={!selectedSession || isLoading}
              rows={1}
              style={{
                flex: 1, background: "none", border: "none", outline: "none",
                color: "var(--text-primary)", fontSize: "13.5px", lineHeight: 1.5,
                resize: "none", minHeight: 24, maxHeight: 160,
                fontFamily: "inherit", caretColor: "var(--accent)",
              }}
            />
            <button
              onClick={handleSend}
              disabled={!selectedSession || isLoading || !input.trim()}
              style={{
                width: 36, height: 36, borderRadius: 8, border: "none", cursor: "pointer",
                background: !selectedSession || isLoading || !input.trim()
                  ? "var(--bg-hover)" : "linear-gradient(135deg, #1d4ed8, #4f9bff)",
                color: !selectedSession || isLoading || !input.trim() ? "var(--text-muted)" : "white",
                display: "flex", alignItems: "center", justifyContent: "center",
                fontSize: "16px", flexShrink: 0, transition: "all 0.15s",
              }}
            >
              {isLoading ? (
                <div style={{ width: 14, height: 14, border: "2px solid currentColor", borderTopColor: "transparent", borderRadius: "50%", animation: "spin 1s linear infinite" }} />
              ) : "▶"}
            </button>
          </div>
          <div style={{ fontSize: "10px", color: "var(--text-muted)", marginTop: 6, textAlign: "center" }}>
            AI reads all Unity files • writes scripts • creates GameObjects • fixes errors automatically
          </div>
        </div>
      </div>

      {/* Right panel */}
      <div style={{ width: 280, flexShrink: 0, height: "100%", overflow: "hidden", display: "flex", flexDirection: "column" }}>
        {rightPanel === "setup" && <SetupPanel />}
        {rightPanel === "files" && selectedProject && (
          <FilesPanel projectId={selectedProject.id} onReadFile={handleReadFile} />
        )}
        {rightPanel === "logs" && (
          <LogsPanel logs={logs} />
        )}
        {rightPanel === "files" && !selectedProject && (
          <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "100%", color: "var(--text-muted)", fontSize: "12px", borderLeft: "1px solid var(--border)" }}>
            Select a project first
          </div>
        )}
      </div>
    </div>
  );
}
