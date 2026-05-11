import { db } from "@/db";
import { pendingCommands } from "@/db/schema";
import { eq, and } from "drizzle-orm";
import { NextRequest } from "next/server";
import { getProjectByApiKey } from "@/lib/auth";

export const dynamic = "force-dynamic";

// GET — Unity polls for pending commands
export async function GET(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ commands: [] }, { status: 401 });
    }

    const cmds = await db
      .select()
      .from(pendingCommands)
      .where(
        and(
          eq(pendingCommands.projectId, project.id),
          eq(pendingCommands.status, "pending")
        )
      )
      .limit(10);

    // Map to Unity plugin expected format
    const commands = cmds.map((c) => {
      const payload = (c.payload ?? {}) as Record<string, unknown>;
      return {
        id: c.id,
        type: c.type,
        ...payload,
      };
    });

    return Response.json({ commands });
  } catch (err) {
    console.error("[commands GET]", err);
    return Response.json({ commands: [] }, { status: 500 });
  }
}

// POST — Unity reports command execution result
export async function POST(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body = await req.json();
    const { commandId, success, result } = body as {
      commandId: string;
      success: boolean;
      result: string;
    };

    if (commandId) {
      await db
        .update(pendingCommands)
        .set({
          status: success ? "done" : "error",
          result: (result ?? "").substring(0, 2000),
          executedAt: new Date(),
        })
        .where(eq(pendingCommands.id, commandId));
    }

    return Response.json({ ok: true });
  } catch (err) {
    console.error("[commands POST]", err);
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
