import { db } from "@/db";
import { projects, projectFiles, consoleLogs } from "@/db/schema";
import { eq } from "drizzle-orm";
import { NextRequest } from "next/server";
import { getProjectByApiKey } from "@/lib/auth";

export const dynamic = "force-dynamic";

interface SyncFile {
  path: string;
  type: string;
  size: number;
  content: string;
}

interface SyncBody {
  projectName?: string;
  unityVersion?: string;
  scene?: string;
  hierarchy?: string;
  files?: SyncFile[];
  logs?: { logType: string; message: string; stackTrace: string }[];
}

export async function POST(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body: SyncBody = await req.json();
    const files = body.files ?? [];

    // Upsert project metadata
    await db
      .update(projects)
      .set({
        name: body.projectName ?? project.name,
        unityVersion: body.unityVersion ?? project.unityVersion,
        activeScene: body.scene ?? project.activeScene,
        sceneHierarchy: (body.hierarchy ?? "").substring(0, 20000),
        fileCount: files.length,
        lastSyncAt: new Date(),
        updatedAt: new Date(),
      })
      .where(eq(projects.id, project.id));

    // Replace all project files: delete then bulk insert
    if (files.length > 0) {
      await db
        .delete(projectFiles)
        .where(eq(projectFiles.projectId, project.id));

      // Insert in batches of 100
      for (let i = 0; i < files.length; i += 100) {
        const batch = files.slice(i, i + 100);
        await db.insert(projectFiles).values(
          batch.map((f) => ({
            projectId: project.id,
            path: f.path,
            fileType: f.type ?? "other",
            sizeBytes: f.size ?? 0,
            content: (f.content ?? "").substring(0, 100000),
            updatedAt: new Date(),
          }))
        );
      }
    }

    // Save logs if any
    if (body.logs && body.logs.length > 0) {
      await db.insert(consoleLogs).values(
        body.logs.slice(0, 100).map((l) => ({
          projectId: project.id,
          logType: l.logType ?? "log",
          message: (l.message ?? "").substring(0, 2000),
          stackTrace: (l.stackTrace ?? "").substring(0, 2000),
          createdAt: new Date(),
        }))
      );
    }

    return Response.json({
      ok: true,
      projectId: project.id,
      fileCount: files.length,
    });
  } catch (err) {
    console.error("[sync]", err);
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
