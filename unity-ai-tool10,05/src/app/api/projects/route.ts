import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, sessions } from "@/db/schema";
import { eq } from "drizzle-orm";
import { v4 as uuidv4 } from "uuid";

export async function GET() {
  const rows = await db.select().from(projects).orderBy(projects.createdAt);
  return NextResponse.json(rows);
}

export async function POST(req: NextRequest) {
  const body = await req.json() as { name: string; unityVersion?: string };
  const { name, unityVersion } = body;

  if (!name) {
    return NextResponse.json({ error: "Project name required" }, { status: 400 });
  }

  const id = uuidv4();
  const apiKey = `unity_${uuidv4().replace(/-/g, "")}`;

  const [project] = await db
    .insert(projects)
    .values({ id, name, unityVersion: unityVersion ?? "2022.3", apiKey })
    .returning();

  // Create default session
  const sessionId = uuidv4();
  await db.insert(sessions).values({
    id: sessionId,
    projectId: id,
    title: "Session 1",
  });

  return NextResponse.json({ project, sessionId }, { status: 201 });
}

export async function DELETE(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const id = searchParams.get("id");
  if (!id) return NextResponse.json({ error: "Missing id" }, { status: 400 });

  await db.delete(projects).where(eq(projects.id, id));
  return NextResponse.json({ ok: true });
}
