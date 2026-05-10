import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";

export const dynamic = "force-dynamic";

// Get console logs for UI display
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");
    const projectId = searchParams.get("projectId");
    const type = searchParams.get("type") || "all";
    const limit = parseInt(searchParams.get("limit") || "100");

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

    let logs = await db
      .select()
      .from(consoleLogs)
      .where(eq(consoleLogs.projectId, targetProjectId))
      .orderBy(desc(consoleLogs.timestamp))
      .limit(limit);

    if (type && type !== "all") {
      logs = logs.filter((l) => l.logType === type);
    }

    // Count by type
    const allLogs = await db
      .select()
      .from(consoleLogs)
      .where(eq(consoleLogs.projectId, targetProjectId));

    const counts = {
      error: allLogs.filter((l) => l.logType === "error").length,
      warning: allLogs.filter((l) => l.logType === "warning").length,
      log: allLogs.filter((l) => l.logType === "log").length,
    };

    return NextResponse.json({
      logs,
      counts,
      total: logs.length,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Clear console logs
export async function DELETE(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const projectId = searchParams.get("projectId");
    const apiKey = searchParams.get("apiKey");

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

    await db
      .delete(consoleLogs)
      .where(eq(consoleLogs.projectId, targetProjectId));

    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
