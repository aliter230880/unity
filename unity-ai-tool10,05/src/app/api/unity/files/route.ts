import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projectFiles } from "@/db/schema";
import { eq } from "drizzle-orm";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const projectId = searchParams.get("projectId");

  if (!projectId) {
    return NextResponse.json({ error: "projectId required" }, { status: 400 });
  }

  const files = await db
    .select({ path: projectFiles.path, type: projectFiles.type })
    .from(projectFiles)
    .where(eq(projectFiles.projectId, projectId));

  return NextResponse.json(files);
}
