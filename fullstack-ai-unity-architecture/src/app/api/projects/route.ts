import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, sessions } from "@/db/schema";
import { eq } from "drizzle-orm";
import { generateApiKey } from "@/lib/orchestrator";

export const dynamic = "force-dynamic";

// List all projects
export async function GET() {
  try {
    const allProjects = await db
      .select({
        id: projects.id,
        name: projects.name,
        apiKey: projects.apiKey,
        createdAt: projects.createdAt,
      })
      .from(projects)
      .orderBy(projects.createdAt);

    return NextResponse.json({ projects: allProjects });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Create new project
export async function POST(req: NextRequest) {
  try {
    const { name } = await req.json();
    const apiKey = generateApiKey();

    const [project] = await db
      .insert(projects)
      .values({
        name: name || "Unity Project",
        apiKey,
      })
      .returning();

    return NextResponse.json({ project });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Delete project
export async function DELETE(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const id = searchParams.get("id");

    if (!id) {
      return NextResponse.json({ error: "id required" }, { status: 400 });
    }

    await db.delete(projects).where(eq(projects.id, id));

    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
