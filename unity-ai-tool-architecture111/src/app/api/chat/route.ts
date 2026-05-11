import { runOrchestrator } from "@/lib/orchestrator";
import { db } from "@/db";
import { sessions, projects } from "@/db/schema";
import { eq } from "drizzle-orm";

export const dynamic = "force-dynamic";
export const maxDuration = 120;

export async function POST(req: Request) {
  try {
    const body = await req.json() as {
      message?: string;
      sessionId?: string;
      projectId?: string;
    };

    const { message, sessionId, projectId } = body;

    if (!message || !sessionId || !projectId) {
      return Response.json(
        { error: "message, sessionId, and projectId are required" },
        { status: 400 }
      );
    }

    // Verify session exists
    const sessionRows = await db
      .select()
      .from(sessions)
      .where(eq(sessions.id, sessionId));
    if (sessionRows.length === 0) {
      return Response.json({ error: "Session not found" }, { status: 404 });
    }

    // Verify project exists
    const projectRows = await db
      .select()
      .from(projects)
      .where(eq(projects.id, projectId));
    if (projectRows.length === 0) {
      return Response.json({ error: "Project not found" }, { status: 404 });
    }

    // Use streaming SSE response
    const encoder = new TextEncoder();
    let toolCallLog = "";

    const stream = new ReadableStream({
      async start(controller) {
        try {
          const finalResponse = await runOrchestrator(
            message,
            sessionId,
            projectId,
            (delta) => {
              toolCallLog += delta;
              controller.enqueue(
                encoder.encode(
                  `data: ${JSON.stringify({ type: "tool", content: delta })}\n\n`
                )
              );
            }
          );

          controller.enqueue(
            encoder.encode(
              `data: ${JSON.stringify({ type: "final", content: finalResponse })}\n\n`
            )
          );
        } catch (err) {
          controller.enqueue(
            encoder.encode(
              `data: ${JSON.stringify({ type: "error", content: String(err) })}\n\n`
            )
          );
        } finally {
          controller.enqueue(encoder.encode("data: [DONE]\n\n"));
          controller.close();
        }
      },
    });

    return new Response(stream, {
      headers: {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-cache",
        Connection: "keep-alive",
      },
    });
  } catch (e) {
    return Response.json({ error: String(e) }, { status: 500 });
  }
}
