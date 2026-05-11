import { db } from "@/db";
import { sessions, messages } from "@/db/schema";
import { eq, asc, desc } from "drizzle-orm";
import { NextRequest } from "next/server";

export const dynamic = "force-dynamic";

// GET sessions for a project
export async function GET(req: NextRequest) {
  try {
    const projectId = req.nextUrl.searchParams.get("projectId");
    const sessionId = req.nextUrl.searchParams.get("sessionId");

    if (sessionId) {
      // Get messages for specific session
      const msgs = await db
        .select()
        .from(messages)
        .where(eq(messages.sessionId, sessionId))
        .orderBy(asc(messages.createdAt))
        .limit(100);

      return Response.json({ messages: msgs });
    }

    if (!projectId) {
      return Response.json({ error: "projectId required" }, { status: 400 });
    }

    const all = await db
      .select()
      .from(sessions)
      .where(eq(sessions.projectId, projectId))
      .orderBy(desc(sessions.updatedAt))
      .limit(50);

    return Response.json({ sessions: all });
  } catch (err) {
    return Response.json({ error: String(err) }, { status: 500 });
  }
}

// DELETE session
export async function DELETE(req: NextRequest) {
  try {
    const id = req.nextUrl.searchParams.get("id");
    if (!id) {
      return Response.json({ error: "id required" }, { status: 400 });
    }

    await db.delete(sessions).where(eq(sessions.id, id));
    return Response.json({ ok: true });
  } catch (err) {
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
