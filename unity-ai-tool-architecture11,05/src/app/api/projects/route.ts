import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";
import { NextRequest } from "next/server";
import { v4 as uuidv4 } from "uuid";

export const dynamic = "force-dynamic";

// GET all projects
export async function GET() {
  try {
    const all = await db
      .select()
      .from(projects)
      .orderBy(projects.updatedAt)
      .limit(100);

    return Response.json({ projects: all });
  } catch (err) {
    return Response.json({ error: String(err) }, { status: 500 });
  }
}

// POST — create project
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { name } = body as { name: string };

    if (!name) {
      return Response.json({ error: "name required" }, { status: 400 });
    }

    const id = uuidv4();
    const apiKey = `atk_${uuidv4().replace(/-/g, "")}`;

    const [project] = await db
      .insert(projects)
      .values({
        id,
        name,
        apiKey,
        createdAt: new Date(),
        updatedAt: new Date(),
      })
      .returning();

    return Response.json({ project });
  } catch (err) {
    return Response.json({ error: String(err) }, { status: 500 });
  }
}

// DELETE — delete project
export async function DELETE(req: NextRequest) {
  try {
    const id = req.nextUrl.searchParams.get("id");
    if (!id) {
      return Response.json({ error: "id required" }, { status: 400 });
    }

    await db.delete(projects).where(eq(projects.id, id));
    return Response.json({ ok: true });
  } catch (err) {
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
