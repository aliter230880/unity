import { db } from "@/db";
import { projects, sessions, messages, projectFiles, pendingCommands, consoleLogs } from "@/db/schema";
import { eq, desc, and } from "drizzle-orm";
import { v4 as uuidv4 } from "uuid";

// System prompt that defines the AI's role
export const SYSTEM_PROMPT = `You are AliTerra AI — a fullstack Unity developer assistant. You work INSIDE Unity through a plugin that executes your commands.

YOUR ROLE:
- You are an expert Unity/C# developer
- You can create scripts, modify code, read console logs, configure objects, and manage the project
- You use TOOLS (function calls) to perform actions — never just write code in chat
- After making changes, you should run execute_editor_command("compile") to trigger compilation
- You should check console logs after compilation to catch and fix errors automatically

YOUR WORKFLOW:
1. Understand the user's request
2. Use list_project_files to understand the current project structure
3. Plan your approach
4. Execute actions using tools (create_script, modify_script, etc.)
5. Run execute_editor_command("compile") to compile
6. Check read_console_logs("error") for any errors
7. Fix any errors automatically
8. Report what you've done

IMPORTANT RULES:
- Always check project files before creating new ones (avoid duplicates)
- Always compile after making changes
- Always check for errors after compilation
- Use proper C# conventions (PascalCase for methods, camelCase for fields)
- Add necessary using statements at the top of scripts
- Keep scripts focused on single responsibilities
- Comment your code for clarity

You speak to the user in a friendly, helpful manner. Explain what you're doing and why.`;

// Get or create project by API key
export async function getOrCreateProject(apiKey: string, name?: string) {
  const existing = await db
    .select()
    .from(projects)
    .where(eq(projects.apiKey, apiKey))
    .limit(1);

  if (existing.length > 0) {
    return existing[0];
  }

  const [project] = await db
    .insert(projects)
    .values({
      name: name || "Unity Project",
      apiKey: apiKey,
    })
    .returning();

  return project;
}

// Create a new session
export async function createSession(projectId: string, title?: string) {
  const [session] = await db
    .insert(sessions)
    .values({
      projectId,
      title: title || "New Session",
    })
    .returning();

  return session;
}

// Get session with messages
export async function getSession(sessionId: string) {
  const session = await db
    .select()
    .from(sessions)
    .where(eq(sessions.id, sessionId))
    .limit(1);

  if (session.length === 0) return null;

  const msgs = await db
    .select()
    .from(messages)
    .where(eq(messages.sessionId, sessionId))
    .orderBy(messages.createdAt);

  return {
    ...session[0],
    messages: msgs,
  };
}

// Add message to session
export async function addMessage(
  sessionId: string,
  role: string,
  content: string | null,
  toolCalls?: any,
  toolCallId?: string
) {
  const [msg] = await db
    .insert(messages)
    .values({
      sessionId,
      role,
      content,
      toolCalls: toolCalls || null,
      toolCallId: toolCallId || null,
    })
    .returning();

  return msg;
}

// Get project file map (cached index)
export async function getProjectMap(projectId: string) {
  const files = await db
    .select({
      filePath: projectFiles.filePath,
      fileType: projectFiles.fileType,
      lastSynced: projectFiles.lastSynced,
    })
    .from(projectFiles)
    .where(eq(projectFiles.projectId, projectId));

  // Group by type
  const map: Record<string, typeof files> = {
    script: [],
    shader: [],
    scene: [],
    prefab: [],
    other: [],
  };

  for (const file of files) {
    const type = file.fileType || "other";
    if (map[type]) {
      map[type].push(file);
    }
  }

  return {
    totalFiles: files.length,
    filesByType: map,
    allFiles: files,
  };
}

// Get pending commands for Unity
export async function getPendingCommands(projectId: string) {
  const cmds = await db
    .select()
    .from(pendingCommands)
    .where(
      and(
        eq(pendingCommands.projectId, projectId),
        eq(pendingCommands.status, "pending")
      )
    )
    .orderBy(pendingCommands.createdAt);

  return cmds;
}

// Mark command as sent
export async function markCommandSent(commandId: string) {
  await db
    .update(pendingCommands)
    .set({ status: "sent" })
    .where(eq(pendingCommands.id, commandId));
}

// Mark command as completed
export async function markCommandCompleted(commandId: string, result?: any) {
  await db
    .update(pendingCommands)
    .set({
      status: "completed",
      result,
      completedAt: new Date(),
    })
    .where(eq(pendingCommands.id, commandId));
}

// Store console logs from Unity
export async function storeConsoleLogs(
  projectId: string,
  logs: Array<{ type: string; message: string; stackTrace?: string }>
) {
  if (logs.length === 0) return;

  await db
    .insert(consoleLogs)
    .values(
      logs.map((log) => ({
        projectId,
        logType: log.type,
        message: log.message,
        stackTrace: log.stackTrace || null,
      }))
    );
}

// Get recent console logs
export async function getRecentLogs(projectId: string, limit: number = 50, type?: string) {
  const logs = await db
    .select()
    .from(consoleLogs)
    .where(eq(consoleLogs.projectId, projectId))
    .orderBy(desc(consoleLogs.timestamp))
    .limit(limit);

  if (type && type !== "all") {
    return logs.filter((l) => l.logType === type);
  }

  return logs;
}

// Sync project files from Unity
export async function syncProjectFiles(
  projectId: string,
  files: Array<{ path: string; type: string; content?: string }>
) {
  for (const file of files) {
    await db
      .insert(projectFiles)
      .values({
        projectId,
        filePath: file.path,
        fileType: file.type,
        content: file.content || null,
      })
      .onConflictDoUpdate({
        target: [projectFiles.projectId, projectFiles.filePath],
        set: {
          fileType: file.type,
          content: file.content || null,
          lastSynced: new Date(),
        },
      });
  }
}

// Build context for AI (project map + recent activity)
export async function buildAIContext(projectId: string, sessionId: string) {
  const projectMap = await getProjectMap(projectId);
  const recentLogs = await getRecentLogs(projectId, 20);
  const session = await getSession(sessionId);

  const context = `
## Current Project Structure (${projectMap.totalFiles} files):

### Scripts (${projectMap.filesByType.script.length}):
${projectMap.filesByType.script.map((f) => `- ${f.filePath}`).join("\n") || "No scripts yet"}

### Shaders (${projectMap.filesByType.shader.length}):
${projectMap.filesByType.shader.map((f) => `- ${f.filePath}`).join("\n") || "No shaders"}

### Scenes (${projectMap.filesByType.scene.length}):
${projectMap.filesByType.scene.map((f) => `- ${f.filePath}`).join("\n") || "No scenes"}

### Prefabs (${projectMap.filesByType.prefab.length}):
${projectMap.filesByType.prefab.map((f) => `- ${f.filePath}`).join("\n") || "No prefabs"}

## Recent Console Activity:
${recentLogs.length > 0 ? recentLogs.map((l) => `[${l.logType}] ${l.message}`).join("\n") : "No recent logs"}
`;

  return context;
}

// Generate API key
export function generateApiKey(): string {
  return `alterra_${uuidv4().replace(/-/g, "")}`;
}
