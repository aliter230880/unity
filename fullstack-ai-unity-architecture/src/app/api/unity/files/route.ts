import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, projectFiles } from "@/db/schema";
import { eq } from "drizzle-orm";

export const dynamic = "force-dynamic";

// Get project files
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");
    const projectId = searchParams.get("projectId");
    const type = searchParams.get("type");

    let targetProjectId = projectId;

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

    let query = db
      .select()
      .from(projectFiles)
      .where(eq(projectFiles.projectId, targetProjectId));

    const files = await query;

    let filtered = files;
    if (type && type !== "all") {
      filtered = files.filter((f) => f.fileType === type);
    }

    return NextResponse.json({
      files: filtered,
      total: filtered.length,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Get file content
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { projectId, filePath } = body;

    if (!projectId || !filePath) {
      return NextResponse.json(
        { error: "projectId and filePath required" },
        { status: 400 }
      );
    }

    const file = await db
      .select()
      .from(projectFiles)
      .where(eq(projectFiles.filePath, filePath))
      .limit(1);

    if (file.length === 0) {
      return NextResponse.json({ error: "File not found" }, { status: 404 });
    }

    return NextResponse.json({ file: file[0] });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
