import { db } from "@/db";
import { projects, projectFiles, sceneSnapshots } from "@/db/schema";
import { eq, and } from "drizzle-orm";

export const dynamic = "force-dynamic";

interface SyncFile {
  path: string;
  type: string;
  size: number;
  content: string;
}

interface SyncPayload {
  apiKey?: string;
  projectName?: string;
  unityVersion?: string;
  scene?: string;
  hierarchy?: string;
  files?: SyncFile[];
}

// POST /api/unity/sync — Unity plugin sends all files here
export async function POST(req: Request) {
  try {
    const body: SyncPayload = await req.json();
    const { apiKey, projectName, unityVersion, scene, hierarchy, files } = body;

    if (!apiKey) {
      return Response.json({ error: "apiKey required" }, { status: 400 });
    }

    // Find project by API key
    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey));

    if (projectRows.length === 0) {
      return Response.json({ error: "Invalid API key" }, { status: 401 });
    }

    const project = projectRows[0];
    const projectId = project.id;

    // Update project info
    await db
      .update(projects)
      .set({
        name: projectName || project.name,
        unityVersion: unityVersion || project.unityVersion || "",
        updatedAt: new Date(),
      })
      .where(eq(projects.id, projectId));

    // Save scene snapshot if provided
    if (scene && hierarchy) {
      await db.insert(sceneSnapshots).values({
        projectId,
        sceneName: scene,
        hierarchy: hierarchy,
      });
    }

    // Upsert files
    if (files && files.length > 0) {
      let upserted = 0;
      for (const file of files) {
        if (!file.path) continue;

        const existing = await db
          .select({ id: projectFiles.id })
          .from(projectFiles)
          .where(
            and(
              eq(projectFiles.projectId, projectId),
              eq(projectFiles.path, file.path)
            )
          );

        if (existing.length > 0) {
          await db
            .update(projectFiles)
            .set({
              type: file.type || "other",
              sizeBytes: file.size || 0,
              content: file.content || "",
              updatedAt: new Date(),
            })
            .where(
              and(
                eq(projectFiles.projectId, projectId),
                eq(projectFiles.path, file.path)
              )
            );
        } else {
          await db.insert(projectFiles).values({
            projectId,
            path: file.path,
            type: file.type || "other",
            sizeBytes: file.size || 0,
            content: file.content || "",
          });
        }
        upserted++;
      }

      return Response.json({
        ok: true,
        projectId,
        filesUpserted: upserted,
        message: `Synced ${upserted} files`,
      });
    }

    return Response.json({ ok: true, projectId });
  } catch (e) {
    console.error("Sync error:", e);
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
