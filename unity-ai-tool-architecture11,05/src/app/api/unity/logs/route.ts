import { db } from "@/db";
import { consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";
import { NextRequest } from "next/server";
import { getProjectByApiKey } from "@/lib/auth";

export const dynamic = "force-dynamic";

// POST — Unity sends console logs
export async function POST(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body = await req.json();
    const logs: { logType: string; message: string; stackTrace: string }[] =
      body.logs ?? [];

    if (logs.length > 0) {
      await db.insert(consoleLogs).values(
        logs.slice(0, 200).map((l) => ({
          projectId: project.id,
          logType: l.logType ?? "log",
          message: (l.message ?? "").substring(0, 2000),
          stackTrace: (l.stackTrace ?? "").substring(0, 2000),
          createdAt: new Date(),
        }))
      );
    }

    return Response.json({ ok: true });
  } catch (err) {
    console.error("[logs POST]", err);
    return Response.json({ error: String(err) }, { status: 500 });
  }
}

// GET — Retrieve recent logs for a project
export async function GET(req: NextRequest) {
  try {
    const project = await getProjectByApiKey(req);
    if (!project) {
      return Response.json({ logs: [] }, { status: 401 });
    }

    const limit = parseInt(
      req.nextUrl.searchParams.get("limit") ?? "50",
      10
    );

    const logs = await db
      .select()
      .from(consoleLogs)
      .where(eq(consoleLogs.projectId, project.id))
      .orderBy(desc(consoleLogs.createdAt))
      .limit(Math.min(limit, 200));

    return Response.json({ logs });
  } catch (err) {
    console.error("[logs GET]", err);
    return Response.json({ logs: [] }, { status: 500 });
  }
}
