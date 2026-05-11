import http from "node:http";
import { readFile, writeFile, mkdir, stat } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import crypto from "node:crypto";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, "..");
const DATA = path.join(ROOT, "data");
const PUBLIC = path.join(ROOT, "public");
const PLUGIN = path.join(ROOT, "unity-plugin", "VibeUnityDeveloper.cs");
const PORT = Number(process.env.PORT || 17861);
const MAX_BODY = 60 * 1024 * 1024;
const MAX_CONTEXT_CHARS = 90_000;
const MAX_FILE_CONTEXT_CHARS = 18_000;

const files = {
  settings: path.join(DATA, "settings.json"),
  index: path.join(DATA, "project-index.json"),
  commands: path.join(DATA, "commands.json"),
  proposals: path.join(DATA, "proposals.json"),
  chats: path.join(DATA, "chats.json")
};

await mkdir(DATA, { recursive: true });

function id(prefix) {
  return `${prefix}_${crypto.randomBytes(8).toString("hex")}`;
}

async function readJson(file, fallback) {
  try {
    return JSON.parse(await readFile(file, "utf8"));
  } catch {
    return fallback;
  }
}

async function writeJson(file, value) {
  await writeFile(file, JSON.stringify(value, null, 2), "utf8");
}

function send(res, status, body, headers = {}) {
  const text = typeof body === "string" ? body : JSON.stringify(body);
  res.writeHead(status, {
    "Content-Type": typeof body === "string" ? "text/plain; charset=utf-8" : "application/json; charset=utf-8",
    "Cache-Control": "no-store",
    ...headers
  });
  res.end(text);
}

function sendHtml(res, body) {
  res.writeHead(200, { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store" });
  res.end(body);
}

async function readBody(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > MAX_BODY) throw new Error("Request is too large");
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks).toString("utf8");
  return raw ? JSON.parse(raw) : {};
}

function publicSettings(settings) {
  return {
    baseUrl: settings.baseUrl || "https://api.openai.com/v1",
    model: settings.model || "gpt-5.1",
    hasApiKey: Boolean(settings.apiKey),
    autoApply: Boolean(settings.autoApply)
  };
}

function normalizeCommand(command) {
  const type = String(command.type || command.command || "").trim();
  const clean = {
    id: id("cmd"),
    status: "pending",
    createdAt: new Date().toISOString(),
    type
  };

  for (const key of [
    "path", "content", "name", "primitive", "components", "position", "rotation",
    "scale", "parent", "color", "component", "target", "message"
  ]) {
    if (command[key] !== undefined && command[key] !== null) clean[key] = String(command[key]);
  }

  return clean;
}

function isSafeProjectPath(filePath) {
  const p = String(filePath || "").replaceAll("\\", "/").trim();
  if (!p || p.startsWith("/") || /^[a-zA-Z]:/.test(p) || p.includes("\0")) return false;
  const normalized = path.posix.normalize(p);
  if (normalized.startsWith("../") || normalized === "..") return false;
  return normalized.startsWith("Assets/") || normalized.startsWith("Packages/") || normalized.startsWith("ProjectSettings/");
}

function validateCommands(commands) {
  const allowed = new Set([
    "write_file",
    "create_script",
    "create_gameobject",
    "add_component",
    "set_transform",
    "refresh",
    "save_scene"
  ]);

  const out = [];
  for (const raw of Array.isArray(commands) ? commands : []) {
    const command = normalizeCommand(raw);
    if (!allowed.has(command.type)) continue;
    if ((command.type === "write_file" || command.type === "create_script") && !isSafeProjectPath(command.path)) continue;
    if ((command.type === "write_file" || command.type === "create_script") && !command.content) continue;
    out.push(command);
  }
  return out;
}

function summarizeProject(index) {
  const project = index.project || {};
  const files = Array.isArray(index.files) ? index.files : [];
  const byType = {};
  for (const file of files) byType[file.type || "other"] = (byType[file.type || "other"] || 0) + 1;
  return {
    projectName: project.name || "Unity Project",
    projectPath: project.path || "",
    unityVersion: project.unityVersion || "",
    sceneName: index.scene?.name || "",
    fileCount: files.length,
    byType,
    lastSync: index.syncedAt || null
  };
}

function words(text) {
  return String(text || "")
    .toLowerCase()
    .split(/[^a-zа-я0-9_]+/i)
    .filter((w) => w.length > 2);
}

function buildAiContext(index, userMessage) {
  const project = summarizeProject(index);
  const allFiles = Array.isArray(index.files) ? index.files : [];
  const terms = new Set(words(userMessage));

  const scored = allFiles.map((file) => {
    const p = String(file.path || "");
    const content = String(file.content || "");
    let score = 0;
    for (const term of terms) {
      if (p.toLowerCase().includes(term)) score += 8;
      if (content.toLowerCase().includes(term)) score += 2;
    }
    if (file.type === "script") score += 2;
    if (file.path?.includes("Player") || file.path?.includes("Game") || file.path?.includes("Manager")) score += 1;
    return { file, score };
  }).sort((a, b) => b.score - a.score);

  const fileMap = allFiles
    .map((f) => `${f.path} | ${f.type || "other"} | ${f.isText ? "text" : "binary"} | ${f.size || 0} bytes`)
    .join("\n")
    .slice(0, 35_000);

  let relevant = "";
  for (const { file } of scored.slice(0, 24)) {
    if (!file.isText || !file.content) continue;
    const content = String(file.content).slice(0, MAX_FILE_CONTEXT_CHARS);
    const block = `\n\n--- FILE: ${file.path} ---\n${content}`;
    if ((relevant + block).length > MAX_CONTEXT_CHARS) break;
    relevant += block;
  }

  const scene = index.scene?.hierarchy ? String(index.scene.hierarchy).slice(0, 15_000) : "";

  return `
UNITY PROJECT SUMMARY
${JSON.stringify(project, null, 2)}

ALL UNITY FILES
${fileMap}

CURRENT SCENE
${scene || "No scene hierarchy synced yet."}

RELEVANT FILE CONTENT
${relevant || "No relevant text file content found. If you need a file, ask the user to sync again."}
`.trim();
}

function systemPrompt() {
  return `
You are Vibe Unity Developer, a senior fullstack Unity game developer working through a local Unity Editor plugin.

The user is not a coder. Speak simply and confidently in Russian. Prefer doing the work through commands.

You can propose these commands:
- write_file: replace or create a text file. Fields: path, content, message.
- create_script: same as write_file, normally under Assets/Scripts.
- create_gameobject: create a visible object in the current scene. Fields: name, primitive, position, rotation, scale, components, color, parent.
- add_component: add a component/script to a GameObject. Fields: target, component.
- set_transform: set transform. Fields: target, position, rotation, scale.
- refresh: refresh AssetDatabase.
- save_scene: save current scene.

Rules:
- Return ONLY valid JSON.
- JSON shape: {"reply":"short Russian explanation","commands":[...],"needsUser":false}
- Use project-relative paths only.
- Only write inside Assets/, Packages/, or ProjectSettings/.
- For file edits, provide the COMPLETE final file content, not a patch.
- Do not include secrets or API keys in generated files.
- Keep changes small and coherent. If the project lacks needed context, ask for one clarifying sentence in reply and return no commands.
`.trim();
}

async function callAi(settings, index, message) {
  if (!settings.apiKey) {
    throw new Error("AI key is not configured. Open Settings and paste your provider key.");
  }
  const baseUrl = String(settings.baseUrl || "https://api.openai.com/v1").replace(/\/+$/, "");
  const model = String(settings.model || "gpt-5.1").trim();
  if (!model) throw new Error("Model is not configured. Open Settings and enter a model name.");

  const payload = {
    model,
    messages: [
      { role: "system", content: systemPrompt() },
      { role: "user", content: `${buildAiContext(index, message)}\n\nUSER REQUEST:\n${message}` }
    ]
  };

  if (/^(gpt-5|o[0-9]|o[134]-|o4-|o3-)/i.test(model)) {
    payload.max_completion_tokens = 12000;
  } else {
    payload.temperature = 0.2;
    payload.max_tokens = 12000;
  }

  const response = await fetch(`${baseUrl}/chat/completions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${settings.apiKey}`
    },
    body: JSON.stringify(payload)
  });

  const text = await response.text();
  if (!response.ok) throw new Error(`AI provider error ${response.status}: ${text.slice(0, 800)}`);
  const json = JSON.parse(text);
  const content = json.choices?.[0]?.message?.content || "";
  const match = content.match(/\{[\s\S]*\}/);
  if (!match) throw new Error(`AI returned non-JSON response: ${content.slice(0, 800)}`);
  return JSON.parse(match[0]);
}

async function route(req, res) {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const method = req.method || "GET";

  if (method === "GET" && url.pathname === "/") {
    return sendHtml(res, await readFile(path.join(PUBLIC, "index.html"), "utf8"));
  }

  if (method === "GET" && ["/app.js", "/style.css"].includes(url.pathname)) {
    const file = path.join(PUBLIC, url.pathname.slice(1));
    const type = url.pathname.endsWith(".js") ? "application/javascript; charset=utf-8" : "text/css; charset=utf-8";
    res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-store" });
    return res.end(await readFile(file));
  }

  if (method === "GET" && url.pathname === "/plugin/VibeUnityDeveloper.cs") {
    const body = await readFile(PLUGIN, "utf8");
    return send(res, 200, body, {
      "Content-Type": "text/plain; charset=utf-8",
      "Content-Disposition": "attachment; filename=VibeUnityDeveloper.cs"
    });
  }

  if (method === "GET" && url.pathname === "/api/state") {
    const settings = await readJson(files.settings, {});
    const index = await readJson(files.index, {});
    const commands = await readJson(files.commands, []);
    const proposals = await readJson(files.proposals, []);
    return send(res, 200, {
      settings: publicSettings(settings),
      project: summarizeProject(index),
      pendingCommands: commands.filter((c) => c.status === "pending" || c.status === "sent").length,
      proposals: proposals.filter((p) => p.status === "proposed").slice(-10).reverse()
    });
  }

  if (method === "POST" && url.pathname === "/api/settings") {
    const body = await readBody(req);
    const current = await readJson(files.settings, {});
    const next = {
      ...current,
      baseUrl: String(body.baseUrl || current.baseUrl || "https://api.openai.com/v1").trim(),
      model: String(body.model || current.model || "gpt-5.1").trim(),
      autoApply: Boolean(body.autoApply)
    };
    if (typeof body.apiKey === "string" && body.apiKey.trim()) next.apiKey = body.apiKey.trim();
    await writeJson(files.settings, next);
    return send(res, 200, { ok: true, settings: publicSettings(next) });
  }

  if (method === "POST" && url.pathname === "/api/unity/sync") {
    const body = await readBody(req);
    const index = {
      project: body.project || {},
      scene: body.scene || {},
      files: Array.isArray(body.files) ? body.files : [],
      syncedAt: new Date().toISOString()
    };
    await writeJson(files.index, index);
    return send(res, 200, { ok: true, project: summarizeProject(index) });
  }

  if (method === "GET" && url.pathname === "/api/unity/commands") {
    const commands = await readJson(files.commands, []);
    const now = new Date().toISOString();
    const outgoing = commands.filter((c) => c.status === "pending").slice(0, 25);
    for (const command of outgoing) {
      command.status = "sent";
      command.sentAt = now;
    }
    await writeJson(files.commands, commands);
    return send(res, 200, { commands: outgoing });
  }

  if (method === "POST" && url.pathname === "/api/unity/commands/result") {
    const body = await readBody(req);
    const commands = await readJson(files.commands, []);
    const command = commands.find((c) => c.id === body.id);
    if (command) {
      command.status = body.ok ? "completed" : "failed";
      command.completedAt = new Date().toISOString();
      command.result = body.result || "";
    }
    await writeJson(files.commands, commands);
    return send(res, 200, { ok: true });
  }

  if (method === "POST" && url.pathname === "/api/chat") {
    const body = await readBody(req);
    const message = String(body.message || "").trim();
    if (!message) return send(res, 400, { error: "Message is required" });

    const settings = await readJson(files.settings, {});
    const index = await readJson(files.index, {});
    if (!Array.isArray(index.files) || index.files.length === 0) {
      return send(res, 400, { error: "Unity project is not synced yet. Open the Unity plugin and click Sync All Files." });
    }

    const ai = await callAi(settings, index, message);
    const commands = validateCommands(ai.commands);
    const proposal = {
      id: id("proposal"),
      status: settings.autoApply ? "applied" : "proposed",
      createdAt: new Date().toISOString(),
      userMessage: message,
      reply: String(ai.reply || "Готово."),
      commands
    };

    const proposals = await readJson(files.proposals, []);
    proposals.push(proposal);
    await writeJson(files.proposals, proposals);

    if (settings.autoApply && commands.length > 0) {
      const existing = await readJson(files.commands, []);
      existing.push(...commands);
      await writeJson(files.commands, existing);
    }

    const chats = await readJson(files.chats, []);
    chats.push({ id: id("chat"), at: new Date().toISOString(), message, reply: proposal.reply, proposalId: proposal.id });
    await writeJson(files.chats, chats.slice(-100));

    return send(res, 200, { proposal });
  }

  if (method === "POST" && url.pathname === "/api/proposals/apply") {
    const body = await readBody(req);
    const proposals = await readJson(files.proposals, []);
    const proposal = proposals.find((p) => p.id === body.id);
    if (!proposal) return send(res, 404, { error: "Proposal not found" });
    if (proposal.status !== "applied") {
      const commands = await readJson(files.commands, []);
      commands.push(...proposal.commands.map((c) => ({ ...c, id: id("cmd"), status: "pending", createdAt: new Date().toISOString() })));
      proposal.status = "applied";
      proposal.appliedAt = new Date().toISOString();
      await writeJson(files.commands, commands);
      await writeJson(files.proposals, proposals);
    }
    return send(res, 200, { ok: true, proposal });
  }

  send(res, 404, { error: "Not found" });
}

const server = http.createServer((req, res) => {
  route(req, res).catch((error) => {
    console.error(error);
    send(res, 500, { error: error.message || "Server error" });
  });
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`Vibe Unity Developer running at http://localhost:${PORT}`);
});
