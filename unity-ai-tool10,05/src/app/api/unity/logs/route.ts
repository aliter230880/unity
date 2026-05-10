// Unity plugin sends console logs here
import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, consoleLogs, sessions } from "@/db/schema";
import { eq, desc } from "drizzle-orm";

interface LogEntry {
  logType: string;
  message: string;
  stackTrace?: string;
  isCompilationError?: boolean;
}

export async function POST(req: NextRequest) {
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

  const body = await req.json() as { logs: LogEntry[] };

  // Get most recent session for this project
  const [latestSession] = await db
    .select()
    .from(sessions)
    .where(eq(sessions.projectId, project.id))
    .orderBy(desc(sessions.createdAt))
    .limit(1);

  for (const log of body.logs) {
    await db.insert(consoleLogs).values({
      projectId: project.id,
      sessionId: latestSession?.id ?? null,
      logType: log.logType,
      message: log.message,
      stackTrace: log.stackTrace ?? null,
      isCompilationError: log.isCompilationError ?? false,
    });
  }

  return NextResponse.json({ ok: true, received: body.logs.length });
}

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

  const logs = await db
    .select()
    .from(consoleLogs)
    .where(eq(consoleLogs.projectId, project.id))
    .orderBy(desc(consoleLogs.createdAt))
    .limit(50);

  return NextResponse.json({ logs });
}
