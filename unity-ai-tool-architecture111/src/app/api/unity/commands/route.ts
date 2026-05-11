import { db } from "@/db";
import { projects, pendingCommands } from "@/db/schema";
import { eq, and } from "drizzle-orm";

export const dynamic = "force-dynamic";

// GET /api/unity/commands?apiKey=... — Unity plugin polls for pending commands
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");

    if (!apiKey) {
      return Response.json({ commands: [] });
    }

    // Find project
    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey));

    if (projectRows.length === 0) {
      return Response.json({ commands: [] });
    }

    const projectId = projectRows[0].id;

    // Get pending commands
    const cmds = await db
      .select()
      .from(pendingCommands)
      .where(
        and(
          eq(pendingCommands.projectId, projectId),
          eq(pendingCommands.status, "pending")
        )
      )
      .orderBy(pendingCommands.createdAt)
      .limit(20);

    if (cmds.length === 0) {
      return Response.json({ commands: [] });
    }

    // Mark as executing
    for (const cmd of cmds) {
      await db
        .update(pendingCommands)
        .set({ status: "executing" })
        .where(eq(pendingCommands.id, cmd.id));
    }

    const commands = cmds.map((cmd) => ({
      id: cmd.id,
      type: cmd.type,
      ...(cmd.payload as Record<string, unknown>),
    }));

    return Response.json({ commands });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// POST /api/unity/commands/complete — Unity reports command result
export async function POST(req: Request) {
  try {
    const body = await req.json() as {
      apiKey?: string;
      commandId?: string;
      success?: boolean;
      result?: string;
    };

    const { apiKey, commandId, success, result } = body;

    if (!apiKey || !commandId) {
      return Response.json({ error: "apiKey and commandId required" }, { status: 400 });
    }

    // Verify API key
    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey));

    if (projectRows.length === 0) {
      return Response.json({ error: "Invalid API key" }, { status: 401 });
    }

    await db
      .update(pendingCommands)
      .set({
        status: success ? "done" : "error",
        result: result || "",
        executedAt: new Date(),
      })
      .where(eq(pendingCommands.id, commandId));

    return Response.json({ ok: true });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
