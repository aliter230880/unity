import { db } from "@/db";
import { projects, projectFiles } from "@/db/schema";
import { eq, and } from "drizzle-orm";

export const dynamic = "force-dynamic";

// GET /api/unity/files?projectId=...&type=script
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");
    const type = searchParams.get("type") || "all";

    if (!projectId) {
      return Response.json({ error: "projectId required" }, { status: 400 });
    }

    let files = await db
      .select({
        id: projectFiles.id,
        path: projectFiles.path,
        type: projectFiles.type,
        sizeBytes: projectFiles.sizeBytes,
        updatedAt: projectFiles.updatedAt,
      })
      .from(projectFiles)
      .where(eq(projectFiles.projectId, projectId));

    if (type !== "all") {
      files = files.filter((f) => f.type === type);
    }

    return Response.json(files);
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// GET /api/unity/files/content?projectId=...&path=...
// (handled separately below as we need it for file content)
export async function POST(req: Request) {
  try {
    const body = await req.json() as { projectId?: string; path?: string };
    const { projectId, path } = body;

    if (!projectId || !path) {
      return Response.json({ error: "projectId and path required" }, { status: 400 });
    }

    const rows = await db
      .select()
      .from(projectFiles)
      .where(
        and(
          eq(projectFiles.projectId, projectId),
          eq(projectFiles.path, path)
        )
      );

    if (rows.length === 0) {
      return Response.json({ error: "File not found" }, { status: 404 });
    }

    return Response.json(rows[0]);
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
