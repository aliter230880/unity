import { db } from "@/db";
import { projects, projectFiles, consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";
import { NextRequest } from "next/server";
import { getProjectByApiKey } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const files = await db
      .select({
        path: projectFiles.path,
        fileType: projectFiles.fileType,
        sizeBytes: projectFiles.sizeBytes,
      })
      .from(projectFiles)
      .where(eq(projectFiles.projectId, project.id))
      .limit(1000);

    const recentLogs = await db
      .select()
      .from(consoleLogs)
      .where(eq(consoleLogs.projectId, project.id))
      .orderBy(desc(consoleLogs.createdAt))
      .limit(20);

    return Response.json({
      ok: true,
      project: {
        id: project.id,
        name: project.name,
        unityVersion: project.unityVersion,
        activeScene: project.activeScene,
        fileCount: project.fileCount,
        lastSyncAt: project.lastSyncAt,
      },
      files,
      recentLogs,
    });
  } catch (err) {
    console.error("[state]", err);
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
