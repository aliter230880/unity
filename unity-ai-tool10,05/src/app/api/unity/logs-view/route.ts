import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const projectId = searchParams.get("projectId");

  if (!projectId) {
    return NextResponse.json({ error: "projectId required" }, { status: 400 });
  }

  const logs = await db
    .select({
      logType: consoleLogs.logType,
      message: consoleLogs.message,
      createdAt: consoleLogs.createdAt,
    })
    .from(consoleLogs)
    .where(eq(consoleLogs.projectId, projectId))
    .orderBy(desc(consoleLogs.createdAt))
    .limit(100);

  return NextResponse.json(logs);
}
