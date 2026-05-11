import { db } from "@/db";
import { projects, consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";

export const dynamic = "force-dynamic";

interface LogEntry {
  logType: string;
  message: string;
  stackTrace?: string;
}

// POST /api/unity/logs — Unity plugin sends console logs
export async function POST(req: Request) {
  try {
    const body = await req.json() as {
      apiKey?: string;
      logs?: LogEntry[];
    };

    const { apiKey, logs } = body;

    if (!apiKey) {
      return Response.json({ error: "apiKey required" }, { status: 400 });
    }

    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey));

    if (projectRows.length === 0) {
      return Response.json({ error: "Invalid API key" }, { status: 401 });
    }

    const projectId = projectRows[0].id;

    if (logs && logs.length > 0) {
      for (const log of logs) {
        await db.insert(consoleLogs).values({
          projectId,
          logType: log.logType || "log",
          message: log.message || "",
          stackTrace: log.stackTrace || "",
        });
      }

      // Keep only last 500 logs per project (cleanup old ones)
      const allLogs = await db
        .select({ id: consoleLogs.id })
        .from(consoleLogs)
        .where(eq(consoleLogs.projectId, projectId))
        .orderBy(desc(consoleLogs.createdAt));

      if (allLogs.length > 500) {
        const toDelete = allLogs.slice(500);
        for (const log of toDelete) {
          await db.delete(consoleLogs).where(eq(consoleLogs.id, log.id));
        }
      }
    }

    return Response.json({ ok: true, received: logs?.length || 0 });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}

// GET /api/unity/logs?apiKey=...&limit=50 — UI can read logs too
export async function GET(req: Request) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");
    const limit = parseInt(searchParams.get("limit") || "50");

    if (!apiKey) {
      return Response.json({ error: "apiKey required" }, { status: 400 });
    }

    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey));

    if (projectRows.length === 0) {
      return Response.json({ error: "Invalid API key" }, { status: 401 });
    }

    const projectId = projectRows[0].id;
    const logs = await db
      .select()
      .from(consoleLogs)
      .where(eq(consoleLogs.projectId, projectId))
      .orderBy(desc(consoleLogs.createdAt))
      .limit(limit);

    return Response.json(logs.reverse());
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
