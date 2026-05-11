import { db } from "@/db";
import { projectFiles, consoleLogs, pendingCommands, projects } from "@/db/schema";
import { eq, desc, and, ilike } from "drizzle-orm";
import { ToolName } from "./tools";
import { v4 as uuidv4 } from "uuid";

interface ToolArgs {
  filter_type?: string;
  search?: string;
  path?: string;
  content?: string;
  name?: string;
  primitive?: string;
  components?: string;
  position?: string;
  color?: string;
  parent?: string;
  command?: string;
  message?: string;
  limit?: number;
  query?: string;
  file_type?: string;
}

export async function executeTool(
  toolName: ToolName,
  args: ToolArgs,
  projectId: string
): Promise<string> {
  try {
    switch (toolName) {
      case "list_project_files":
        return await listProjectFiles(projectId, args.filter_type, args.search);

      case "read_file":
        return await readFile(projectId, args.path ?? "");

      case "write_file":
        return await writeFile(projectId, args.path ?? "", args.content ?? "");

      case "create_gameobject":
        return await enqueueUnityCommand(projectId, "create_gameobject", {
          name: args.name ?? "NewObject",
          primitive: args.primitive ?? "Empty",
          components: args.components ?? "",
          position: args.position ?? "0,0,0",
          color: args.color ?? "",
          parent: args.parent ?? "",
        });

      case "add_component":
        return await enqueueUnityCommand(projectId, "add_component", {
          name: args.name ?? "",
          components: args.components ?? "",
        });

      case "execute_editor_command":
        return await enqueueUnityCommand(projectId, "execute_editor_command", {
          command: args.command ?? "refresh",
          message: args.message ?? "",
        });

      case "read_console_logs":
        return await readConsoleLogs(
          projectId,
          args.filter_type,
          args.limit ?? 30
        );

      case "get_scene_hierarchy":
        return await getSceneHierarchy(projectId);

      case "search_in_files":
        return await searchInFiles(projectId, args.query ?? "", args.file_type);

      case "delete_file":
        return await deleteFile(projectId, args.path ?? "");

      default:
        return `Unknown tool: ${toolName}`;
    }
  } catch (err) {
    return `Tool error: ${String(err)}`;
  }
}

async function listProjectFiles(
  projectId: string,
  filterType?: string,
  search?: string
): Promise<string> {
  let query = db
    .select({
      path: projectFiles.path,
      fileType: projectFiles.fileType,
      sizeBytes: projectFiles.sizeBytes,
    })
    .from(projectFiles)
    .where(eq(projectFiles.projectId, projectId))
    .$dynamic();

  const results = await query.limit(500);

  let filtered = results;

  if (filterType && filterType !== "all") {
    const typeMap: Record<string, string[]> = {
      scripts: ["script", "cs"],
      scenes: ["scene", "unity"],
      prefabs: ["prefab"],
      shaders: ["shader"],
      materials: ["material", "mat"],
      configs: ["config", "json", "yaml", "xml"],
    };
    const allowed = typeMap[filterType] ?? [];
    filtered = filtered.filter((f) =>
      allowed.some(
        (t) =>
          (f.fileType ?? "").toLowerCase().includes(t) ||
          (f.path ?? "").toLowerCase().endsWith("." + t)
      )
    );
  }

  if (search) {
    const s = search.toLowerCase();
    filtered = filtered.filter((f) =>
      (f.path ?? "").toLowerCase().includes(s)
    );
  }

  if (filtered.length === 0) {
    return "No files found. The project may not be synced yet. Please sync from the Unity plugin first.";
  }

  // Group by directory
  const grouped: Record<string, typeof filtered> = {};
  for (const f of filtered) {
    const dir = (f.path ?? "").split("/").slice(0, -1).join("/") || "root";
    if (!grouped[dir]) grouped[dir] = [];
    grouped[dir].push(f);
  }

  const lines: string[] = [`📁 Project Files (${filtered.length} total):`];
  for (const [dir, files] of Object.entries(grouped).slice(0, 50)) {
    lines.push(`\n📂 ${dir}/`);
    for (const f of files.slice(0, 20)) {
      const name = (f.path ?? "").split("/").pop() ?? "";
      const size = f.sizeBytes ? ` (${Math.round(Number(f.sizeBytes) / 1024)}KB)` : "";
      const icon =
        (f.fileType ?? "").includes("script") ? "📜" :
        (f.fileType ?? "").includes("scene") ? "🎬" :
        (f.fileType ?? "").includes("prefab") ? "🧊" :
        (f.fileType ?? "").includes("shader") ? "✨" : "📄";
      lines.push(`  ${icon} ${name}${size} → ${f.path}`);
    }
  }

  return lines.join("\n");
}

async function readFile(projectId: string, path: string): Promise<string> {
  if (!path) return "Error: path is required";

  const [file] = await db
    .select()
    .from(projectFiles)
    .where(
      and(
        eq(projectFiles.projectId, projectId),
        eq(projectFiles.path, path)
      )
    )
    .limit(1);

  if (!file) {
    // Try partial match
    const allFiles = await db
      .select({ path: projectFiles.path })
      .from(projectFiles)
      .where(eq(projectFiles.projectId, projectId))
      .limit(500);

    const match = allFiles.find((f) =>
      (f.path ?? "").toLowerCase().includes(path.toLowerCase().split("/").pop() ?? path)
    );

    if (match) {
      return await readFile(projectId, match.path);
    }

    return `File not found: ${path}\nHint: Use list_project_files to find the correct path.`;
  }

  const content = file.content ?? "";
  if (content.length > 50000) {
    return `📄 ${path} (truncated to 50000 chars)\n\n${content.substring(0, 50000)}\n...[truncated]`;
  }

  return `📄 ${path}\n\`\`\`\n${content}\n\`\`\``;
}

async function writeFile(
  projectId: string,
  path: string,
  content: string
): Promise<string> {
  if (!path) return "Error: path is required";
  if (!content) return "Error: content is required";

  // Queue the write_file command for Unity to execute
  await enqueueUnityCommand(projectId, "write_file", { path, content });

  // Also update our DB cache immediately
  const existing = await db
    .select({ id: projectFiles.id })
    .from(projectFiles)
    .where(
      and(
        eq(projectFiles.projectId, projectId),
        eq(projectFiles.path, path)
      )
    )
    .limit(1);

  if (existing.length > 0) {
    await db
      .update(projectFiles)
      .set({
        content,
        sizeBytes: content.length,
        updatedAt: new Date(),
      })
      .where(eq(projectFiles.id, existing[0].id));
  } else {
    const ext = path.split(".").pop()?.toLowerCase() ?? "";
    const fileType =
      ext === "cs" ? "script" :
      ext === "unity" ? "scene" :
      ext === "prefab" ? "prefab" :
      ext === "shader" ? "shader" :
      ext === "mat" ? "material" : "other";

    await db.insert(projectFiles).values({
      projectId,
      path,
      fileType,
      sizeBytes: content.length,
      content,
      updatedAt: new Date(),
    });
  }

  return `✅ File queued for writing: ${path}\nThe Unity plugin will write this file on next poll (within 3 seconds). Use read_console_logs after ~5 seconds to check for compilation errors.`;
}

async function enqueueUnityCommand(
  projectId: string,
  type: string,
  payload: Record<string, unknown>
): Promise<string> {
  const id = uuidv4();
  await db.insert(pendingCommands).values({
    id,
    projectId,
    type,
    payload,
    status: "pending",
    createdAt: new Date(),
  });

  return `✅ Command "${type}" queued (id: ${id}). Unity plugin will execute it within 3 seconds.`;
}

async function readConsoleLogs(
  projectId: string,
  filterType?: string,
  limit = 30
): Promise<string> {
  let logsQuery = db
    .select()
    .from(consoleLogs)
    .where(eq(consoleLogs.projectId, projectId))
    .orderBy(desc(consoleLogs.createdAt))
    .$dynamic();

  const allLogs = await logsQuery.limit(Math.min(limit, 100));

  let filtered = allLogs;
  if (filterType && filterType !== "all") {
    filtered = allLogs.filter(
      (l) => (l.logType ?? "").toLowerCase() === filterType.toLowerCase()
    );
  }

  if (filtered.length === 0) {
    return "No console logs found. The Unity plugin may not be running or no logs have been captured yet.";
  }

  const icons: Record<string, string> = {
    error: "🔴",
    exception: "💥",
    warning: "🟡",
    log: "⚪",
  };

  const lines = filtered.map((l) => {
    const icon = icons[l.logType ?? "log"] ?? "⚪";
    const time = l.createdAt
      ? new Date(l.createdAt).toLocaleTimeString()
      : "";
    const stack = l.stackTrace
      ? `\n   Stack: ${l.stackTrace.substring(0, 200)}`
      : "";
    return `${icon} [${time}] ${l.message}${stack}`;
  });

  const errors = filtered.filter((l) =>
    l.logType === "error" || l.logType === "exception"
  );

  return [
    `📋 Console Logs (${filtered.length} entries, ${errors.length} errors):`,
    ...lines,
  ].join("\n");
}

async function getSceneHierarchy(projectId: string): Promise<string> {
  const [project] = await db
    .select({
      activeScene: projects.activeScene,
      sceneHierarchy: projects.sceneHierarchy,
    })
    .from(projects)
    .where(eq(projects.id, projectId))
    .limit(1);

  if (!project) return "Project not found";

  const hierarchy = project.sceneHierarchy ?? "";
  if (!hierarchy || hierarchy.trim().length < 10) {
    return "Scene hierarchy not available. Make sure the Unity plugin is connected and sync is enabled.";
  }

  return `🎬 Scene: ${project.activeScene ?? "Unknown"}\n\n${hierarchy}`;
}

async function searchInFiles(
  projectId: string,
  query: string,
  fileType?: string
): Promise<string> {
  if (!query) return "Error: query is required";

  const allFiles = await db
    .select({
      path: projectFiles.path,
      content: projectFiles.content,
      fileType: projectFiles.fileType,
    })
    .from(projectFiles)
    .where(eq(projectFiles.projectId, projectId))
    .limit(500);

  const matches: { path: string; lines: string[] }[] = [];
  const q = query.toLowerCase();

  for (const f of allFiles) {
    if (fileType && !(f.path ?? "").toLowerCase().endsWith(fileType.toLowerCase())) {
      continue;
    }

    const content = f.content ?? "";
    if (!content.toLowerCase().includes(q)) continue;

    const lines = content.split("\n");
    const matchLines: string[] = [];
    lines.forEach((line, idx) => {
      if (line.toLowerCase().includes(q)) {
        matchLines.push(`  Line ${idx + 1}: ${line.trim().substring(0, 150)}`);
      }
    });

    if (matchLines.length > 0) {
      matches.push({
        path: f.path ?? "",
        lines: matchLines.slice(0, 5),
      });
    }
  }

  if (matches.length === 0) {
    return `No matches found for "${query}"${fileType ? ` in *${fileType} files` : ""}`;
  }

  const lines = [`🔍 Search results for "${query}" (${matches.length} files):`];
  for (const m of matches.slice(0, 20)) {
    lines.push(`\n📄 ${m.path}`);
    lines.push(...m.lines);
  }

  return lines.join("\n");
}

async function deleteFile(projectId: string, path: string): Promise<string> {
  if (!path) return "Error: path is required";

  // Queue deletion command for Unity
  await enqueueUnityCommand(projectId, "delete_file", { path });

  // Remove from DB
  await db
    .delete(projectFiles)
    .where(
      and(
        eq(projectFiles.projectId, projectId),
        eq(projectFiles.path, path)
      )
    );

  return `✅ File "${path}" queued for deletion. Unity plugin will execute on next poll.`;
}
