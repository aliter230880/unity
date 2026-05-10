import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";
import { getOrCreateProject, syncProjectFiles, getProjectMap } from "@/lib/orchestrator";

export const dynamic = "force-dynamic";

// Unity plugin syncs project files
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { apiKey, files, projectName } = body;

    if (!apiKey) {
      return NextResponse.json({ error: "apiKey required" }, { status: 401 });
    }

    // Validate API key
    const proj = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey))
      .limit(1);

    if (proj.length === 0) {
      return NextResponse.json({ error: "Invalid API key" }, { status: 401 });
    }

    const project = proj[0];

    // Sync files
    if (files && Array.isArray(files)) {
      await syncProjectFiles(project.id, files);
    }

    // Return updated project map
    const projectMap = await getProjectMap(project.id);

    return NextResponse.json({
      success: true,
      projectMap,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Get project map
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");

    if (!apiKey) {
      return NextResponse.json({ error: "apiKey required" }, { status: 401 });
    }

    const proj = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey))
      .limit(1);

    if (proj.length === 0) {
      return NextResponse.json({ error: "Invalid API key" }, { status: 401 });
    }

    const projectMap = await getProjectMap(proj[0].id);

    return NextResponse.json({ projectMap });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
