import { NextRequest, NextResponse } from "next/server";
import { db } from "@/db";
import { projects, pendingCommands } from "@/db/schema";
import { eq, and } from "drizzle-orm";
import { getPendingCommands, markCommandCompleted } from "@/lib/orchestrator";

export const dynamic = "force-dynamic";

// Unity plugin polls for pending commands
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const apiKey = searchParams.get("apiKey");

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

    const commands = await getPendingCommands(proj[0].id);

    // Mark commands as sent
    for (const cmd of commands) {
      await db
        .update(pendingCommands)
        .set({ status: "sent" })
        .where(eq(pendingCommands.id, cmd.id));
    }

    return NextResponse.json({ commands });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

// Unity reports command completion
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { apiKey, commandId, success, result } = body;

    if (!apiKey || !commandId) {
      return NextResponse.json(
        { error: "apiKey and commandId required" },
        { status: 400 }
      );
    }

    const proj = await db
      .select()
      .from(projects)
      .where(eq(projects.apiKey, apiKey))
      .limit(1);

    if (proj.length === 0) {
      return NextResponse.json({ error: "Invalid API key" }, { status: 401 });
    }

    await db
      .update(pendingCommands)
      .set({
        status: success ? "completed" : "failed",
        result,
        completedAt: new Date(),
      })
      .where(eq(pendingCommands.id, commandId));

    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
