import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { sessions, messages } from "@/db/schema";
import { eq, asc } from "drizzle-orm";
import { v4 as uuidv4 } from "uuid";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const projectId = searchParams.get("projectId");
  const sessionId = searchParams.get("sessionId");

  if (sessionId) {
    // Get messages for a session
    const msgs = await db
      .select()
      .from(messages)
      .where(eq(messages.sessionId, sessionId))
      .orderBy(asc(messages.createdAt));
    return NextResponse.json(msgs);
  }

  if (!projectId) {
    return NextResponse.json({ error: "projectId required" }, { status: 400 });
  }

  const rows = await db
    .select()
    .from(sessions)
    .where(eq(sessions.projectId, projectId))
    .orderBy(asc(sessions.createdAt));

  return NextResponse.json(rows);
}

export async function POST(req: NextRequest) {
  const body = await req.json() as { projectId: string; title?: string };
  const { projectId, title } = body;

  if (!projectId) {
    return NextResponse.json({ error: "projectId required" }, { status: 400 });
  }

  const [session] = await db
    .insert(sessions)
    .values({ id: uuidv4(), projectId, title: title ?? "New Session" })
    .returning();

  return NextResponse.json(session, { status: 201 });
}
