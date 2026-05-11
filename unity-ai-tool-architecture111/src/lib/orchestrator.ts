import OpenAI from "openai";
import { db } from "@/db";
import {
  projectFiles,
  consoleLogs,
  pendingCommands,
  sceneSnapshots,
  messages,
  sessions,
} from "@/db/schema";
import { eq, desc, and } from "drizzle-orm";
import { UNITY_TOOLS } from "./tools";
import type {
  ChatCompletionMessageParam,
  ChatCompletionToolMessageParam,
  ChatCompletionAssistantMessageParam,
} from "openai/resources/chat/completions";

// ─── Lazy OpenAI client ───────────────────────────────────────────────────────
let _openai: OpenAI | null = null;
function getOpenAI() {
  if (!_openai) {
    _openai = new OpenAI({ apiKey: process.env.OPENAI_API_KEY });
  }
  return _openai;
}

// ─── System Prompt ────────────────────────────────────────────────────────────
const SYSTEM_PROMPT = `You are AliTerra AI — a senior Unity fullstack developer with deep expertise in:
- C# scripting (Unity-specific patterns, coroutines, MonoBehaviour lifecycle)
- Unity Editor scripting (EditorWindow, custom inspectors, ScriptableObjects)
- Unity ECS/DOTS, physics, animation, UI Toolkit, UGUI
- Scene management, prefabs, asset management
- Game architecture patterns (state machines, event systems, object pooling)
- Shader Graph, VFX Graph, particle systems
- NavMesh, pathfinding, AI behaviors

You have DIRECT ACCESS to the Unity project via tools:
1. ALWAYS call list_project_files first to understand the project structure
2. ALWAYS call read_file before modifying any existing script
3. After writing files, call read_console_logs to check for compilation errors and fix them
4. Use write_file to create/modify C# scripts, shaders, configuration files
5. Use create_gameobject to add objects to the scene
6. Use read_scene_hierarchy to understand the current scene

Working principles:
- Write complete, production-quality C# code — no placeholders, no TODO comments
- Add proper using statements, null checks, and error handling
- Follow Unity best practices (avoid FindObjectOfType in Update, cache references, etc.)
- When creating systems, always consider existing project structure to avoid conflicts
- After writing code, proactively check for errors and fix them in the same response
- Explain what you're doing and why — be a senior developer mentor, not just a code generator

IMPORTANT: You are operating on a REAL Unity project. Every file you write will be applied to the actual project. Be precise and careful.`;

// ─── Tool Executor ────────────────────────────────────────────────────────────
export async function executeTool(
  toolName: string,
  args: Record<string, unknown>,
  projectId: string,
  sessionId: string
): Promise<string> {
  switch (toolName) {
    case "list_project_files": {
      const filterType = (args.filter_type as string) || "all";
      const search = (args.search as string) || "";

      let files = await db
        .select({
          path: projectFiles.path,
          type: projectFiles.type,
          sizeBytes: projectFiles.sizeBytes,
        })
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));

      if (filterType !== "all") {
        files = files.filter((f) => f.type === filterType);
      }

      if (search) {
        files = files.filter((f) =>
          f.path.toLowerCase().includes(search.toLowerCase())
        );
      }

      if (files.length === 0) {
        return "No files found in project. The Unity plugin may not have synced yet. Ask the user to click 'Sync All Files' in the AliTerra plugin.";
      }

      const grouped: Record<string, typeof files> = {};
      for (const f of files) {
        if (!grouped[f.type]) grouped[f.type] = [];
        grouped[f.type].push(f);
      }

      let result = `PROJECT FILES (${files.length} total):\n\n`;
      for (const [type, typeFiles] of Object.entries(grouped)) {
        result += `[${type.toUpperCase()}] (${typeFiles.length} files)\n`;
        for (const f of typeFiles.slice(0, 50)) {
          result += `  ${f.path} (${Math.round((f.sizeBytes || 0) / 1024)}KB)\n`;
        }
        if (typeFiles.length > 50)
          result += `  ... and ${typeFiles.length - 50} more\n`;
        result += "\n";
      }
      return result;
    }

    case "read_file": {
      const path = args.path as string;
      const allFiles = await db
        .select()
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));

      const exact = allFiles.find((f) => f.path === path);
      if (exact) {
        if (!exact.content || exact.content.trim() === "") {
          return `File exists but has no text content (binary or empty): ${path} (${exact.sizeBytes} bytes)`;
        }
        return `FILE: ${exact.path}\n${"─".repeat(60)}\n${exact.content}`;
      }

      // Try partial match
      const pathLower = path.toLowerCase();
      const match = allFiles.find((f) =>
        f.path
          .toLowerCase()
          .includes(pathLower.split("/").pop() || pathLower)
      );
      if (match?.content) {
        return `FILE: ${match.path}\n${"─".repeat(60)}\n${match.content}`;
      }

      return `File not found: ${path}. Use list_project_files to see available files.`;
    }

    case "write_file": {
      const path = args.path as string;
      const content = args.content as string;
      const description = (args.description as string) || "";

      // Queue command for Unity plugin
      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "write_file",
        payload: { path, content, description },
        status: "pending",
      });

      // Update our file index optimistically
      const allFiles = await db
        .select({ id: projectFiles.id })
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));
      const existing = allFiles.find(
        async (_) =>
          (
            await db
              .select()
              .from(projectFiles)
              .where(
                and(
                  eq(projectFiles.projectId, projectId),
                  eq(projectFiles.path, path)
                )
              )
          ).length > 0
      );

      const existingRows = await db
        .select({ id: projectFiles.id })
        .from(projectFiles)
        .where(
          and(eq(projectFiles.projectId, projectId), eq(projectFiles.path, path))
        );

      if (existingRows.length > 0) {
        await db
          .update(projectFiles)
          .set({ content, sizeBytes: content.length, updatedAt: new Date() })
          .where(
            and(
              eq(projectFiles.projectId, projectId),
              eq(projectFiles.path, path)
            )
          );
      } else {
        const ext = path.split(".").pop()?.toLowerCase() || "";
        const type =
          ext === "cs"
            ? "script"
            : ext === "unity"
            ? "scene"
            : ext === "prefab"
            ? "prefab"
            : ext === "mat"
            ? "material"
            : ext === "shader"
            ? "shader"
            : "other";
        await db.insert(projectFiles).values({
          projectId,
          path,
          type,
          sizeBytes: content.length,
          content,
        });
      }

      void existing; // suppress unused warning

      return `✅ File queued for writing: ${path}\n${description}\n\nThe Unity plugin will apply this file within ~3 seconds (polling interval). After compilation, call read_console_logs to check for errors.`;
    }

    case "delete_file": {
      const path = args.path as string;

      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "delete_file",
        payload: { path },
        status: "pending",
      });

      await db
        .delete(projectFiles)
        .where(
          and(
            eq(projectFiles.projectId, projectId),
            eq(projectFiles.path, path)
          )
        );

      return `✅ File deletion queued: ${path}`;
    }

    case "create_gameobject": {
      const { name, primitive = "empty", position, components, parent, color } =
        args as Record<string, string>;

      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "create_gameobject",
        payload: { name, primitive, position, components, parent, color },
        status: "pending",
      });

      return `✅ Create GameObject queued: "${name}" (${primitive})${components ? `, components: ${components}` : ""}${position ? `, at ${position}` : ""}`;
    }

    case "add_component": {
      const gameobjectName = args.gameobject_name as string;
      const component = args.component as string;

      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "add_component",
        payload: { name: gameobjectName, components: component },
        status: "pending",
      });

      return `✅ Add component queued: ${component} → "${gameobjectName}"`;
    }

    case "read_console_logs": {
      const logType = args.log_type as string;
      const limit = (args.limit as number) || 30;

      const allLogs = await db
        .select()
        .from(consoleLogs)
        .where(eq(consoleLogs.projectId, projectId))
        .orderBy(desc(consoleLogs.createdAt))
        .limit(100);

      const filtered =
        logType === "all"
          ? allLogs
          : allLogs.filter((l) => l.logType === logType);

      const logs = filtered.slice(0, limit);

      if (logs.length === 0) {
        return logType === "error" || logType === "compiler_error"
          ? "✅ No errors found in Unity console! Code compiled successfully."
          : "No console logs found yet. The Unity plugin may not be connected.";
      }

      const formatted = [...logs]
        .reverse()
        .map((l) => {
          const time = l.createdAt
            ? new Date(l.createdAt).toLocaleTimeString()
            : "";
          const stack =
            l.stackTrace && l.stackTrace !== ""
              ? `\n  Stack: ${l.stackTrace.substring(0, 200)}`
              : "";
          return `[${l.logType.toUpperCase()}] ${time}: ${l.message}${stack}`;
        })
        .join("\n");

      return `UNITY CONSOLE LOGS (${logs.length} entries):\n${"─".repeat(60)}\n${formatted}`;
    }

    case "read_scene_hierarchy": {
      const snapshots = await db
        .select()
        .from(sceneSnapshots)
        .where(eq(sceneSnapshots.projectId, projectId))
        .orderBy(desc(sceneSnapshots.createdAt))
        .limit(1);

      const snapshot = snapshots[0];

      if (!snapshot) {
        return "No scene snapshot available. Make sure the Unity plugin is connected and has synced. Ask the user to sync from the Fullstack tab.";
      }

      return `SCENE: ${snapshot.sceneName}\n${"─".repeat(60)}\n${snapshot.hierarchy}`;
    }

    case "execute_editor_command": {
      const command = args.command as string;
      const argument = (args.argument as string) || "";

      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "execute_editor_command",
        payload: { command, message: argument },
        status: "pending",
      });

      const descriptions: Record<string, string> = {
        play: "Starting Play Mode",
        stop: "Stopping Play Mode",
        save_scene: "Saving the current scene",
        refresh_assets: "Refreshing AssetDatabase",
        open_scene: `Opening scene: ${argument}`,
      };

      return `✅ Editor command queued: ${descriptions[command] || command}`;
    }

    case "create_scriptable_object": {
      const scriptClass = args.script_class as string;
      const assetPath = args.asset_path as string;

      await db.insert(pendingCommands).values({
        projectId,
        sessionId,
        type: "create_scriptable_object",
        payload: { script_class: scriptClass, asset_path: assetPath },
        status: "pending",
      });

      return `✅ ScriptableObject creation queued: ${scriptClass} → ${assetPath}`;
    }

    case "search_in_files": {
      const query = (args.query as string).toLowerCase();
      const fileType = (args.file_type as string) || "all";

      let files = await db
        .select({
          path: projectFiles.path,
          type: projectFiles.type,
          content: projectFiles.content,
        })
        .from(projectFiles)
        .where(eq(projectFiles.projectId, projectId));

      if (fileType !== "all") {
        files = files.filter((f) => f.type === fileType);
      }

      const results: Array<{ path: string; line: number; text: string }> = [];

      for (const file of files) {
        if (!file.content) continue;
        const lines = file.content.split("\n");
        for (let i = 0; i < lines.length; i++) {
          if (lines[i].toLowerCase().includes(query)) {
            results.push({ path: file.path, line: i + 1, text: lines[i].trim() });
            if (results.length >= 50) break;
          }
        }
        if (results.length >= 50) break;
      }

      if (results.length === 0) {
        return `No results found for "${query}" in ${fileType} files.`;
      }

      const formatted = results
        .map((r) => `${r.path}:${r.line}  →  ${r.text}`)
        .join("\n");

      return `SEARCH RESULTS for "${query}" (${results.length} matches):\n${"─".repeat(60)}\n${formatted}`;
    }

    default:
      return `Unknown tool: ${toolName}`;
  }
}

// ─── Main Orchestrator Loop ───────────────────────────────────────────────────
export async function runOrchestrator(
  userMessage: string,
  sessionId: string,
  projectId: string,
  onToken?: (delta: string) => void
): Promise<string> {
  const openai = getOpenAI();

  // Load chat history
  const historyRows = await db
    .select()
    .from(messages)
    .where(eq(messages.sessionId, sessionId))
    .orderBy(messages.createdAt)
    .limit(30);

  // Build OpenAI message array
  const chatMessages: ChatCompletionMessageParam[] = [
    { role: "system", content: SYSTEM_PROMPT },
  ];

  // Add history
  for (const row of historyRows) {
    if (row.role === "user") {
      chatMessages.push({ role: "user", content: row.content });
    } else if (row.role === "assistant") {
      if (row.toolCalls) {
        const assistantMsg: ChatCompletionAssistantMessageParam = {
          role: "assistant",
          content: row.content || null,
          tool_calls: (row.toolCalls as unknown as ChatCompletionAssistantMessageParam["tool_calls"]),
        };
        chatMessages.push(assistantMsg);
      } else {
        chatMessages.push({ role: "assistant", content: row.content });
      }
    } else if (row.role === "tool") {
      chatMessages.push({
        role: "tool",
        tool_call_id: row.toolCallId || "",
        content: row.content,
      } as ChatCompletionToolMessageParam);
    }
  }

  // Add current user message
  chatMessages.push({ role: "user", content: userMessage });

  // Save user message
  await db.insert(messages).values({
    sessionId,
    role: "user",
    content: userMessage,
  });

  // Update session title if first message
  if (historyRows.length === 0) {
    const title =
      userMessage.substring(0, 60) + (userMessage.length > 60 ? "..." : "");
    await db
      .update(sessions)
      .set({ title, updatedAt: new Date() })
      .where(eq(sessions.id, sessionId));
  }

  // ─── Agentic loop ─────────────────────────────────────────────────────────
  let finalResponse = "";
  let iterationCount = 0;
  const MAX_ITERATIONS = 10;

  while (iterationCount < MAX_ITERATIONS) {
    iterationCount++;

    const response = await openai.chat.completions.create({
      model: "gpt-4o",
      messages: chatMessages,
      tools: UNITY_TOOLS,
      tool_choice: "auto",
      max_tokens: 4096,
      temperature: 0.1,
    });

    const choice = response.choices[0];
    const assistantMessage = choice.message;

    // Add assistant message to history
    chatMessages.push(assistantMessage as ChatCompletionMessageParam);

    // If no tool calls, we're done
    if (
      !assistantMessage.tool_calls ||
      assistantMessage.tool_calls.length === 0
    ) {
      finalResponse = assistantMessage.content || "";

      // Save final assistant message
      await db.insert(messages).values({
        sessionId,
        role: "assistant",
        content: finalResponse,
      });

      break;
    }

    // Save assistant message with tool calls
    await db.insert(messages).values({
      sessionId,
      role: "assistant",
      content: assistantMessage.content || "",
      toolCalls: assistantMessage.tool_calls as unknown as Record<string, unknown>[],
    });

    // Execute all tool calls
    const toolResults: ChatCompletionToolMessageParam[] = [];

    for (const toolCall of assistantMessage.tool_calls) {
      if (toolCall.type !== "function") continue;

      let args: Record<string, unknown> = {};
      try {
        args = JSON.parse(toolCall.function.arguments);
      } catch {
        args = {};
      }

      // Notify streaming (tool is being called)
      if (onToken) {
        onToken(
          `\n🔧 **${toolCall.function.name}**(${JSON.stringify(args).substring(0, 100)})\n`
        );
      }

      const result = await executeTool(
        toolCall.function.name,
        args,
        projectId,
        sessionId
      );

      // Save tool result message
      await db.insert(messages).values({
        sessionId,
        role: "tool",
        content: result,
        toolCallId: toolCall.id,
        toolName: toolCall.function.name,
      });

      toolResults.push({
        role: "tool",
        tool_call_id: toolCall.id,
        content: result,
      });
    }

    // Add tool results to message history
    for (const tr of toolResults) {
      chatMessages.push(tr);
    }
  }

  // Update session updated_at
  await db
    .update(sessions)
    .set({ updatedAt: new Date() })
    .where(eq(sessions.id, sessionId));

  return finalResponse;
}
