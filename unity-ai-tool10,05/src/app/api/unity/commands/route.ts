// Unity plugin polls this for pending commands
import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, pendingCommands } from "@/db/schema";
import { eq, and } from "drizzle-orm";

export async function GET(req: NextRequest) {
  const apiKey = req.headers.get("x-api-key");
  if (!apiKey) {
    return NextResponse.json({ error: "Missing x-api-key" }, { status: 401 });
  }

  const [project] = await db
    .select()
    .from(projects)
    .where(eq(projects.apiKey, apiKey))
    .limit(1);

  if (!project) {
    return NextResponse.json({ error: "Invalid API key" }, { status: 403 });
  }

  // Update last seen
  await db
    .update(projects)
    .set({ lastSeen: new Date() })
    .where(eq(projects.id, project.id));

  // Get pending commands
  const cmds = await db
    .select()
    .from(pendingCommands)
    .where(
      and(
        eq(pendingCommands.projectId, project.id),
        eq(pendingCommands.status, "pending")
      )
    )
    .limit(5);

  // Mark as executing
  for (const cmd of cmds) {
    await db
      .update(pendingCommands)
      .set({ status: "executing" })
      .where(eq(pendingCommands.id, cmd.id));
  }

  return NextResponse.json({ commands: cmds });
}

export async function POST(req: NextRequest) {
  // Unity reports command result
  const apiKey = req.headers.get("x-api-key");
  if (!apiKey) {
    return NextResponse.json({ error: "Missing x-api-key" }, { status: 401 });
  }

  const [project] = await db
    .select()
    .from(projects)
    .where(eq(projects.apiKey, apiKey))
    .limit(1);

  if (!project) {
    return NextResponse.json({ error: "Invalid API key" }, { status: 403 });
  }

  const body = await req.json() as {
    commandId: number;
    status: "done" | "error";
    result?: string;
  };

  await db
    .update(pendingCommands)
    .set({
      status: body.status,
      result: body.result ?? null,
      executedAt: new Date(),
    })
    .where(
      and(
        eq(pendingCommands.id, body.commandId),
        eq(pendingCommands.projectId, project.id)
      )
    );

  return NextResponse.json({ ok: true });
}
