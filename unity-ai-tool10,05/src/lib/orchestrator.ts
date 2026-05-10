// AI Orchestrator - processes tool calls against the database
import { db } from "@/db";
import { pendingCommands, projectFiles, consoleLogs } from "@/db/schema";
import { eq, and, desc, inArray } from "drizzle-orm";
import type { ToolName } from "./tools";

export interface ToolCallResult {
  success: boolean;
  data?: unknown;
  error?: string;
}

export async function executeToolCall(
  toolName: ToolName,
  args: Record<string, unknown>,
  projectId: string,
  sessionId: string
): Promise<ToolCallResult> {
  try {
    switch (toolName) {
      case "list_project_files": {
        const filterType = args.filter_type as string | undefined;
        const rows = await db
          .select({
            path: projectFiles.path,
            type: projectFiles.type,
            size: projectFiles.size,
            lastModified: projectFiles.lastModified,
          })
          .from(projectFiles)
          .where(eq(projectFiles.projectId, projectId));
        const filtered = filterType
          ? rows.filter((r) => r.type === filterType)
          : rows;

        return {
          success: true,
          data: {
            files: filtered,
            total: filtered.length,
            summary: `Project has ${filtered.length} files${filterType ? ` of type '${filterType}'` : ""}`,
          },
        };
      }

      case "read_script": {
        const path = args.path as string;
        const [file] = await db
          .select()
          .from(projectFiles)
          .where(
            and(eq(projectFiles.projectId, projectId), eq(projectFiles.path, path))
          )
          .limit(1);

        if (!file) {
          return {
            success: false,
            error: `File not found: ${path}. Use list_project_files to see available files.`,
          };
        }

        return {
          success: true,
          data: {
            path: file.path,
            content: file.content ?? "(empty file)",
            type: file.type,
            size: file.size,
          },
        };
      }

      case "read_console_logs": {
        const logTypes = (args.log_types as string[]) ?? ["error", "exception"];
        const limit = (args.limit as number) ?? 20;

        const rows = await db
          .select()
          .from(consoleLogs)
          .where(
            and(
              eq(consoleLogs.projectId, projectId),
              logTypes.length > 0
                ? inArray(consoleLogs.logType, logTypes)
                : undefined
            )
          )
          .orderBy(desc(consoleLogs.createdAt))
          .limit(limit);

        return {
          success: true,
          data: {
            logs: rows.map((l) => ({
              type: l.logType,
              message: l.message,
              stackTrace: l.stackTrace,
              isCompilationError: l.isCompilationError,
              time: l.createdAt,
            })),
            count: rows.length,
          },
        };
      }

      case "create_script":
      case "modify_script":
      case "set_object_property":
      case "create_scriptable_object":
      case "execute_editor_command": {
        // Queue command for Unity plugin to pick up
        const [cmd] = await db
          .insert(pendingCommands)
          .values({
            projectId,
            sessionId,
            command: toolName,
            payload: args,
            status: "pending",
          })
          .returning();

        // Wait for Unity to execute the command (polling with timeout)
        const result = await waitForCommandResult(cmd.id, 30000);
        return result;
      }

      default:
        return { success: false, error: `Unknown tool: ${toolName}` };
    }
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : String(err),
    };
  }
}

async function waitForCommandResult(
  commandId: number,
  timeoutMs: number
): Promise<ToolCallResult> {
  const start = Date.now();
  const interval = 500;

  while (Date.now() - start < timeoutMs) {
    await new Promise((r) => setTimeout(r, interval));

    const [cmd] = await db
      .select()
      .from(pendingCommands)
      .where(eq(pendingCommands.id, commandId))
      .limit(1);

    if (!cmd) {
      return { success: false, error: "Command record not found" };
    }

    if (cmd.status === "done") {
      return {
        success: true,
        data: {
          message: cmd.result ?? "Command executed successfully",
          commandId: cmd.id,
        },
      };
    }

    if (cmd.status === "error") {
      return {
        success: false,
        error: cmd.result ?? "Unity reported an error",
      };
    }

    // Still pending or executing — keep waiting
  }

  return {
    success: false,
    error:
      "Timeout: Unity plugin did not respond within 30 seconds. Make sure the plugin is running in the Unity Editor.",
  };
}
