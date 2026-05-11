import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";
import { v4 as uuidv4 } from "uuid";

export const dynamic = "force-dynamic";

// GET /api/projects — list all projects
export async function GET() {
  try {
    const rows = await db
      .select()
      .from(projects)
      .orderBy(projects.createdAt);
    return Response.json(rows);
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// POST /api/projects — create project
export async function POST(req: Request) {
  try {
    const body = await req.json() as { name?: string };
    const name = body.name || "My Unity Project";
    const apiKey = `ak_${uuidv4().replace(/-/g, "")}`;

    const [project] = await db
      .insert(projects)
      .values({ name, apiKey })
      .returning();

    return Response.json(project, { status: 201 });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// DELETE /api/projects?id=... — delete project
export async function DELETE(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const id = searchParams.get("id");
    if (!id) return Response.json({ error: "id required" }, { status: 400 });

    await db.delete(projects).where(eq(projects.id, id));
    return Response.json({ ok: true });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
