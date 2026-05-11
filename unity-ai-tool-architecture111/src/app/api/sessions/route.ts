import { db } from "@/db";
import { sessions, messages } from "@/db/schema";
import { eq, desc } from "drizzle-orm";

export const dynamic = "force-dynamic";

// GET /api/sessions?projectId=...
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");
    if (!projectId)
      return Response.json({ error: "projectId required" }, { status: 400 });

    const rows = await db
      .select()
      .from(sessions)
      .where(eq(sessions.projectId, projectId))
      .orderBy(desc(sessions.updatedAt));

    return Response.json(rows);
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// POST /api/sessions
export async function POST(req: Request) {
  try {
    const body = await req.json() as { projectId?: string; title?: string };
    if (!body.projectId)
      return Response.json({ error: "projectId required" }, { status: 400 });

    const [session] = await db
      .insert(sessions)
      .values({
        projectId: body.projectId,
        title: body.title || "New Session",
      })
      .returning();

    return Response.json(session, { status: 201 });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// DELETE /api/sessions?id=...
export async function DELETE(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const id = searchParams.get("id");
    if (!id) return Response.json({ error: "id required" }, { status: 400 });

    await db.delete(messages).where(eq(messages.sessionId, id));
    await db.delete(sessions).where(eq(sessions.id, id));
    return Response.json({ ok: true });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
