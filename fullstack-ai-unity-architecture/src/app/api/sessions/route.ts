import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { sessions, projects } from "@/db/schema";
import { eq } from "drizzle-orm";

export const dynamic = "force-dynamic";

// List sessions for a project
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");
    const apiKey = searchParams.get("apiKey");

    let targetProjectId = projectId;

    // If API key provided, look up project
    if (!targetProjectId && apiKey) {
      const proj = await db
        .select()
        .from(projects)
        .where(eq(projects.apiKey, apiKey))
        .limit(1);
      if (proj.length > 0) {
        targetProjectId = proj[0].id;
      }
    }

    if (!targetProjectId) {
      return NextResponse.json(
        { error: "projectId or apiKey required" },
        { status: 400 }
      );
    }

    const projectSessions = await db
      .select()
      .from(sessions)
      .where(eq(sessions.projectId, targetProjectId))
      .orderBy(sessions.updatedAt);

    return NextResponse.json({ sessions: projectSessions });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Create new session
export async function POST(req: NextRequest) {
  try {
    const { projectId, title } = await req.json();

    if (!projectId) {
      return NextResponse.json({ error: "projectId required" }, { status: 400 });
    }

    const [session] = await db
      .insert(sessions)
      .values({
        projectId,
        title: title || "New Session",
      })
      .returning();

    return NextResponse.json({ session });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Delete session
export async function DELETE(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const id = searchParams.get("id");

    if (!id) {
      return NextResponse.json({ error: "id required" }, { status: 400 });
    }

    await db.delete(sessions).where(eq(sessions.id, id));

    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
