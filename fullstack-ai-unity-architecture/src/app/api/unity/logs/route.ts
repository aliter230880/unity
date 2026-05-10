import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, consoleLogs } from "@/db/schema";
import { eq, desc } from "drizzle-orm";
import { storeConsoleLogs, getRecentLogs } from "@/lib/orchestrator";

export const dynamic = "force-dynamic";

// Unity sends console logs
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { apiKey, logs } = body;

    if (!apiKey) {
      return NextResponse.json({ error: "apiKey required" }, { status: 401 });
    }

    const proj = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey))
      .limit(1);

    if (proj.length === 0) {
      return NextResponse.json({ error: "Invalid API key" }, { status: 401 });
    }

    if (logs && Array.isArray(logs)) {
      await storeConsoleLogs(proj[0].id, logs);
    }

    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Get console logs
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");
    const type = searchParams.get("type") || "all";
    const limit = parseInt(searchParams.get("limit") || "50");

    if (!apiKey) {
      return NextResponse.json({ error: "apiKey required" }, { status: 401 });
    }

    const proj = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey))
      .limit(1);

    if (proj.length === 0) {
      return NextResponse.json({ error: "Invalid API key" }, { status: 401 });
    }

    const logs = await getRecentLogs(proj[0].id, limit, type);

    return NextResponse.json({ logs });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
