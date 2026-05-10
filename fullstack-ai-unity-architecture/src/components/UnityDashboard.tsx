"use client";

import { useState, useEffect } from "react";

interface LogEntry {
  id: string;
  logType: string;
  message: string;
  stackTrace: string | null;
  timestamp: string;
}

interface ProjectFile {
  id: string;
  filePath: string;
  fileType: string;
  lastSynced: string;
  content: string | null;
}

interface DashboardProps {
  projectId: string;
  apiKey: string;
}

export function UnityDashboard({ projectId, apiKey }: DashboardProps) {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [files, setFiles] = useState<ProjectFile[]>([]);
  const [selectedFile, setSelectedFile] = useState<ProjectFile | null>(null);
  const [logFilter, setLogFilter] = useState<string>("all");
  const [fileFilter, setFileFilter] = useState<string>("all");
  const [logCounts, setLogCounts] = useState({ error: 0, warning: 0, log: 0 });
  const [isLoading, setIsLoading] = useState(false);

  // Load data on mount
  useEffect(() => {
    loadLogs();
    loadFiles();

    // Auto-refresh logs every 5 seconds
    const interval = setInterval(() => {
      loadLogs();
    }, 5000);

    return () => clearInterval(interval);
  }, [projectId]);

  const loadLogs = async () => {
    try {
      const res = await fetch(
        `/api/unity/logs-view?projectId=${projectId}&type=${logFilter}&limit=100`
      );
      const data = await res.json();
      setLogs(data.logs || []);
      setLogCounts(data.counts || { error: 0, warning: 0, log: 0 });
    } catch (err) {
      console.error("Failed to load logs:", err);
    }
  };

  const loadFiles = async () => {
    try {
      setIsLoading(true);
      const res = await fetch(`/api/unity/files?projectId=${projectId}&type=${fileFilter}`);
      const data = await res.json();
      setFiles(data.files || []);
    } catch (err) {
      console.error("Failed to load files:", err);
    } finally {
      setIsLoading(false);
    }
  };

  const loadFileContent = async (file: ProjectFile) => {
    try {
      const res = await fetch("/api/unity/files", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ projectId, filePath: file.filePath }),
      });
      const data = await res.json();
      setSelectedFile(data.file);
    } catch (err) {
      console.error("Failed to load file content:", err);
    }
  };

  const clearLogs = async () => {
    try {
      await fetch(`/api/unity/logs-view?projectId=${projectId}`, {
        method: "DELETE",
      });
      setLogs([]);
      setLogCounts({ error: 0, warning: 0, log: 0 });
    } catch (err) {
      console.error("Failed to clear logs:", err);
    }
  };

  const getLogTypeIcon = (type: string) => {
    switch (type) {
      case "error":
        return "❌";
      case "warning":
        return "⚠️";
      default:
        return "ℹ️";
    }
  };

  const getFileTypeIcon = (type: string) => {
    switch (type) {
      case "script":
        return "📜";
      case "shader":
        return "🎨";
      case "scene":
        return "🎬";
      case "prefab":
        return "🧊";
      default:
        return "📄";
    }
  };

  // Group files by type
  const filesByType = {
    script: files.filter((f) => f.fileType === "script"),
    shader: files.filter((f) => f.fileType === "shader"),
    scene: files.filter((f) => f.fileType === "scene"),
    prefab: files.filter((f) => f.fileType === "prefab"),
    other: files.filter((f) => !["script", "shader", "scene", "prefab"].includes(f.fileType)),
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 h-[calc(100vh-200px)]">
      {/* Files Panel */}
      <div className="lg:col-span-1 bg-slate-800/50 rounded-xl border border-slate-700 flex flex-col">
        <div className="p-4 border-b border-slate-700">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-semibold">📁 Project Files</h3>
            <button
              onClick={loadFiles}
              className="text-slate-400 hover:text-white text-sm"
              disabled={isLoading}
            >
              🔄 Refresh
            </button>
          </div>

          {/* File Type Filter */}
          <div className="flex gap-2 flex-wrap">
            {["all", "script", "shader", "scene", "prefab"].map((type) => (
              <button
                key={type}
                onClick={() => {
                  setFileFilter(type);
                  loadFiles();
                }}
                className={`px-2 py-1 rounded text-xs ${
                  fileFilter === type
                    ? "bg-emerald-600 text-white"
                    : "bg-slate-700 text-slate-300 hover:bg-slate-600"
                }`}
              >
                {type === "all" ? `All (${files.length})` : type}
              </button>
            ))}
          </div>
        </div>

        {/* File Tree */}
        <div className="flex-1 overflow-y-auto p-2">
          {Object.entries(filesByType).map(([type, typeFiles]) => {
            if (typeFiles.length === 0) return null;
            return (
              <div key={type} className="mb-4">
                <div className="text-xs text-slate-400 uppercase tracking-wider px-2 py-1">
                  {getFileTypeIcon(type)} {type} ({typeFiles.length})
                </div>
                {typeFiles.map((file) => (
                  <button
                    key={file.id}
                    onClick={() => loadFileContent(file)}
                    className={`w-full text-left px-3 py-2 rounded-lg text-sm truncate transition-colors ${
                      selectedFile?.id === file.id
                        ? "bg-emerald-600/20 text-emerald-400"
                        : "hover:bg-slate-700/50 text-slate-300"
                    }`}
                  >
                    {file.filePath.split("/").pop()}
                  </button>
                ))}
              </div>
            );
          })}

          {files.length === 0 && !isLoading && (
            <div className="text-center text-slate-500 py-8">
              <p>No files synced yet</p>
              <p className="text-xs mt-1">Connect Unity plugin to sync files</p>
            </div>
          )}
        </div>
      </div>

      {/* Code Viewer */}
      <div className="lg:col-span-1 bg-slate-800/50 rounded-xl border border-slate-700 flex flex-col">
        <div className="p-4 border-b border-slate-700">
          <h3 className="font-semibold">
            {selectedFile ? (
              <>
                {getFileTypeIcon(selectedFile.fileType)} {selectedFile.filePath.split("/").pop()}
              </>
            ) : (
              "📝 Code Viewer"
            )}
          </h3>
          {selectedFile && (
            <p className="text-xs text-slate-400 mt-1 truncate">{selectedFile.filePath}</p>
          )}
        </div>

        <div className="flex-1 overflow-auto">
          {selectedFile?.content ? (
            <pre className="p-4 text-sm font-mono text-slate-300 whitespace-pre-wrap">
              {selectedFile.content}
            </pre>
          ) : (
            <div className="flex items-center justify-center h-full text-slate-500">
              {selectedFile ? "No content cached" : "Select a file to view"}
            </div>
          )}
        </div>
      </div>

      {/* Console Logs */}
      <div className="lg:col-span-1 bg-slate-800/50 rounded-xl border border-slate-700 flex flex-col">
        <div className="p-4 border-b border-slate-700">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-semibold">🖥️ Console</h3>
            <div className="flex gap-2">
              <button
                onClick={loadLogs}
                className="text-slate-400 hover:text-white text-sm"
              >
                🔄
              </button>
              <button
                onClick={clearLogs}
                className="text-slate-400 hover:text-red-400 text-sm"
              >
                🗑️
              </button>
            </div>
          </div>

          {/* Log Type Filter */}
          <div className="flex gap-2">
            <button
              onClick={() => {
                setLogFilter("all");
                loadLogs();
              }}
              className={`px-2 py-1 rounded text-xs ${
                logFilter === "all"
                  ? "bg-slate-600 text-white"
                  : "bg-slate-700 text-slate-300 hover:bg-slate-600"
              }`}
            >
              All
            </button>
            <button
              onClick={() => {
                setLogFilter("error");
                loadLogs();
              }}
              className={`px-2 py-1 rounded text-xs flex items-center gap-1 ${
                logFilter === "error"
                  ? "bg-red-600/30 text-red-400"
                  : "bg-slate-700 text-slate-300 hover:bg-slate-600"
              }`}
            >
              ❌ {logCounts.error}
            </button>
            <button
              onClick={() => {
                setLogFilter("warning");
                loadLogs();
              }}
              className={`px-2 py-1 rounded text-xs flex items-center gap-1 ${
                logFilter === "warning"
                  ? "bg-yellow-600/30 text-yellow-400"
                  : "bg-slate-700 text-slate-300 hover:bg-slate-600"
              }`}
            >
              ⚠️ {logCounts.warning}
            </button>
            <button
              onClick={() => {
                setLogFilter("log");
                loadLogs();
              }}
              className={`px-2 py-1 rounded text-xs flex items-center gap-1 ${
                logFilter === "log"
                  ? "bg-blue-600/30 text-blue-400"
                  : "bg-slate-700 text-slate-300 hover:bg-slate-600"
              }`}
            >
              ℹ️ {logCounts.log}
            </button>
          </div>
        </div>

        {/* Logs List */}
        <div className="flex-1 overflow-y-auto p-2">
          {logs.length === 0 ? (
            <div className="flex items-center justify-center h-full text-slate-500 text-sm">
              No console logs yet
            </div>
          ) : (
            <div className="space-y-1">
              {logs.map((log) => (
                <div
                  key={log.id}
                  className={`p-2 rounded-lg text-sm ${
                    log.logType === "error"
                      ? "bg-red-900/20 border border-red-800/30"
                      : log.logType === "warning"
                      ? "bg-yellow-900/20 border border-yellow-800/30"
                      : "bg-slate-700/30"
                  }`}
                >
                  <div className="flex items-start gap-2">
                    <span>{getLogTypeIcon(log.logType)}</span>
                    <div className="flex-1 min-w-0">
                      <p
                        className={`${
                          log.logType === "error"
                            ? "text-red-300"
                            : log.logType === "warning"
                            ? "text-yellow-300"
                            : "text-slate-300"
                        }`}
                      >
                        {log.message}
                      </p>
                      {log.stackTrace && (
                        <pre className="mt-1 text-xs text-slate-500 whitespace-pre-wrap overflow-x-auto">
                          {log.stackTrace}
                        </pre>
                      )}
                    </div>
                  </div>
                  <div className="text-xs text-slate-500 mt-1 pl-6">
                    {new Date(log.timestamp).toLocaleTimeString()}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
