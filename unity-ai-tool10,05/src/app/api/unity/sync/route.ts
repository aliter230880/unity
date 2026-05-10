// Unity plugin calls this to sync project file index
import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, projectFiles } from "@/db/schema";
import { eq, and } from "drizzle-orm";

interface FileEntry {
  path: string;
  type: string;
  content?: string;
  size?: number;
}

export async function POST(req: NextRequest) {
  const apiKey = req.headers.get("x-api-key");
  if (!apiKey) {
    return NextResponse.json({ error: "Missing x-api-key header" }, { status: 401 });
  }

  const [project] = await db
    .select()
    .from(projects)
    .where(eq(projects.apiKey, apiKey))
    .limit(1);

  if (!project) {
    return NextResponse.json({ error: "Invalid API key" }, { status: 403 });
  }

  // Update last seen
  await db
    .update(projects)
    .set({ lastSeen: new Date() })
    .where(eq(projects.id, project.id));

  const body = await req.json() as { files: FileEntry[]; unityVersion?: string };
  const { files, unityVersion } = body;

  if (unityVersion) {
    await db
      .update(projects)
      .set({ unityVersion })
      .where(eq(projects.id, project.id));
  }

  // Upsert files
  for (const file of files) {
    const existing = await db
      .select()
      .from(projectFiles)
      .where(
        and(
          eq(projectFiles.projectId, project.id),
          eq(projectFiles.path, file.path)
        )
      )
      .limit(1);

    if (existing.length > 0) {
      await db
        .update(projectFiles)
        .set({
          content: file.content ?? null,
          size: file.size ?? 0,
          type: file.type,
          lastModified: new Date(),
        })
        .where(
          and(
            eq(projectFiles.projectId, project.id),
            eq(projectFiles.path, file.path)
          )
        );
    } else {
      await db.insert(projectFiles).values({
        projectId: project.id,
        path: file.path,
        type: file.type,
        content: file.content ?? null,
        size: file.size ?? 0,
      });
    }
  }

  return NextResponse.json({
    ok: true,
    projectName: project.name,
    filesIndexed: files.length,
  });
}
