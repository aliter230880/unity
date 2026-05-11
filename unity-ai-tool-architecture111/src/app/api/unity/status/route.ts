import { db } from "@/db";
import { projects, projectFiles, consoleLogs, pendingCommands, sceneSnapshots } from "@/db/schema";
import { eq, desc, count } from "drizzle-orm";

export const dynamic = "force-dynamic";

// GET /api/unity/status?projectId=...
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");

    if (!projectId) {
      return Response.json({ error: "projectId required" }, { status: 400 });
    }

    const [projectRows, fileCount, recentLogs, pendingCmds, latestScene] =
      await Promise.all([
        db.select().from(projects).where(eq(projects.id, projectId)),
        db
          .select({ count: count() })
          .from(projectFiles)
          .where(eq(projectFiles.projectId, projectId)),
        db
          .select()
          .from(consoleLogs)
          .where(eq(consoleLogs.projectId, projectId))
          .orderBy(desc(consoleLogs.createdAt))
          .limit(10),
        db
          .select()
          .from(pendingCommands)
          .where(eq(pendingCommands.projectId, projectId))
          .orderBy(desc(pendingCommands.createdAt))
          .limit(10),
        db
          .select()
          .from(sceneSnapshots)
          .where(eq(sceneSnapshots.projectId, projectId))
          .orderBy(desc(sceneSnapshots.createdAt))
          .limit(1),
      ]);

    if (projectRows.length === 0) {
      return Response.json({ error: "Project not found" }, { status: 404 });
    }

    const project = projectRows[0];
    const errorCount = recentLogs.filter(
      (l) => l.logType === "error" || l.logType === "compiler_error"
    ).length;

    return Response.json({
      project: {
        id: project.id,
        name: project.name,
        unityVersion: project.unityVersion,
        updatedAt: project.updatedAt,
      },
      stats: {
        fileCount: fileCount[0]?.count || 0,
        errorCount,
        pendingCommands: pendingCmds.filter((c) => c.status === "pending").length,
        currentScene: latestScene[0]?.sceneName || null,
        lastSync: project.updatedAt,
      },
      recentLogs: recentLogs.slice(0, 5),
      pendingCommands: pendingCmds.filter((c) =>
        ["pending", "executing"].includes(c.status || "")
      ),
    });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
