import { NextRequest } from "next/server";
import { db } from "@/db";
import { sessions, messages, projects } from "@/db/schema";
import { eq, asc } from "drizzle-orm";
import { UNITY_TOOLS } from "@/lib/tools";
import { executeTool } from "@/lib/orchestrator";
import type { ToolName } from "@/lib/tools";
import { v4 as uuidv4 } from "uuid";
import OpenAI from "openai";

export const dynamic = "force-dynamic";
export const maxDuration = 120;

function getClient() {
  return new OpenAI({ apiKey: process.env.OPENAI_API_KEY ?? "" });
}

const SYSTEM_PROMPT = `You are AliTerra — an expert AI Unity game developer with FULL ACCESS to the Unity project via tools. You can see, read, write, and modify ANY file.

## Your Tools:
- list_project_files — See ALL project files
- read_file — Read any file's full content  
- write_file — Create or overwrite any file (scripts, shaders, configs)
- create_gameobject — Create GameObjects in the scene
- add_component — Add components to existing GameObjects
- execute_editor_command — Control Unity Editor (Play, Stop, Save, Refresh)
- read_console_logs — Read console to detect/fix errors automatically
- get_scene_hierarchy — See full scene structure with components
- search_in_files — Search text across all project files
- delete_file — Delete files

## Workflow:
1. ALWAYS call list_project_files first to understand the project
2. ALWAYS read existing files before modifying (read_file)
3. ALWAYS call read_console_logs after writing scripts (5-10 sec delay for compilation)
4. Fix compilation errors AUTOMATICALLY without asking
5. Write COMPLETE code — never partial files or pseudocode
6. Complete tasks END-TO-END

## C# Best Practices:
- Include all necessary 'using' statements
- Use [SerializeField] for inspector-exposed private fields
- Add null checks for component references
- Use coroutines for async operations
- Follow Unity naming conventions (PascalCase for classes/methods)

You are a REAL developer who owns the project. Be autonomous and thorough.`;

type OpenAIMessage = OpenAI.Chat.ChatCompletionMessageParam;

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const {
      message,
      sessionId,
      projectId,
      model = "gpt-4o",
    } = body as {
      message: string;
      sessionId: string;
      projectId: string;
      model?: string;
    };

    if (!message || !sessionId || !projectId) {
      return Response.json(
        { error: "message, sessionId, projectId required" },
        { status: 400 }
      );
    }

    // Verify project exists
    const [project] = await db
      .select()
      .from(projects)
      .where(eq(projects.id, projectId))
      .limit(1);

    if (!project) {
      return Response.json({ error: "Project not found" }, { status: 404 });
    }

    // Ensure session exists
    const [session] = await db
      .select()
      .from(sessions)
      .where(eq(sessions.id, sessionId))
      .limit(1);

    if (!session) {
      await db.insert(sessions).values({
        id: sessionId,
        projectId,
        title: message.substring(0, 60),
        createdAt: new Date(),
        updatedAt: new Date(),
      });
    }

    // Save user message
    await db.insert(messages).values({
      sessionId,
      role: "user",
      content: message,
      createdAt: new Date(),
    });

    // Load recent message history
    const history = await db
      .select()
      .from(messages)
      .where(eq(messages.sessionId, sessionId))
      .orderBy(asc(messages.createdAt))
      .limit(30);

    // Build OpenAI messages
    const openaiMsgs: OpenAIMessage[] = [
      { role: "system", content: SYSTEM_PROMPT },
    ];

    for (const m of history) {
      if (m.role === "user") {
        openaiMsgs.push({ role: "user", content: m.content ?? "" });
      } else if (m.role === "assistant") {
        if (m.toolCalls && Array.isArray(m.toolCalls)) {
          openaiMsgs.push({
            role: "assistant",
            content: m.content ?? null,
            tool_calls: m.toolCalls as OpenAI.Chat.ChatCompletionMessageToolCall[],
          });
        } else {
          openaiMsgs.push({
            role: "assistant",
            content: m.content ?? "",
          });
        }
      } else if (m.role === "tool") {
        openaiMsgs.push({
          role: "tool",
          content: m.content ?? "",
          tool_call_id: m.toolCallId ?? "",
        });
      }
    }

    const openai = getClient();

    // Stream SSE response
    const { readable, writable } = new TransformStream();
    const writer = writable.getWriter();
    const enc = new TextEncoder();

    const send = async (data: unknown) => {
      await writer.write(enc.encode(`data: ${JSON.stringify(data)}\n\n`));
    };

    // Run agentic loop in background
    void (async () => {
      let finalContent = "";
      let rounds = 0;
      const MAX_ROUNDS = 12;

      try {
        while (rounds < MAX_ROUNDS) {
          rounds++;

          const resp = await openai.chat.completions.create({
            model,
            messages: openaiMsgs,
            tools: UNITY_TOOLS,
            tool_choice: "auto",
            max_tokens: 4096,
            temperature: 0.1,
          });

          const choice = resp.choices[0];
          if (!choice) break;

          const msg = choice.message;

          if (msg.content) {
            finalContent += msg.content;
            await send({ type: "text", content: msg.content });
          }

          // No tool calls — done
          if (!msg.tool_calls || msg.tool_calls.length === 0) {
            break;
          }

          // Push assistant msg with tool calls to context
          openaiMsgs.push({
            role: "assistant",
            content: msg.content ?? null,
            tool_calls: msg.tool_calls,
          });

          // Save assistant tool-call message to DB
          await db.insert(messages).values({
            sessionId,
            role: "assistant",
            content: msg.content ?? "",
            toolCalls: msg.tool_calls as unknown as Record<string, unknown>[],
            createdAt: new Date(),
          });

          // Execute each tool
          for (const tc of msg.tool_calls) {
            const tcAny = tc as { id: string; function: { name: string; arguments: string } };
            const toolName = tcAny.function.name as ToolName;
            let toolArgs: Record<string, unknown> = {};
            try {
              toolArgs = JSON.parse(tcAny.function.arguments);
            } catch {
              toolArgs = {};
            }

            await send({ type: "tool_call", tool: toolName, args: toolArgs });

            const result = await executeTool(
              toolName,
              toolArgs as Parameters<typeof executeTool>[1],
              projectId
            );

            await send({
              type: "tool_result",
              tool: toolName,
              result: result.substring(0, 600),
            });

            // Add tool result to context
            openaiMsgs.push({
              role: "tool",
              content: result,
              tool_call_id: tcAny.id,
            });

            // Save tool result
            await db.insert(messages).values({
              sessionId,
              role: "tool",
              content: result,
              toolCallId: tcAny.id,
              toolName,
              createdAt: new Date(),
            });
          }

          if (choice.finish_reason === "stop") break;
        }

        // Save final assistant message
        if (finalContent) {
          // Check if we already saved it
          const last = await db
            .select()
            .from(messages)
            .where(eq(messages.sessionId, sessionId))
            .orderBy(asc(messages.createdAt))
            .limit(100);

          const lastMsg = last[last.length - 1];
          if (
            !lastMsg ||
            lastMsg.role !== "assistant" ||
            lastMsg.content !== finalContent
          ) {
            await db.insert(messages).values({
              sessionId,
              role: "assistant",
              content: finalContent,
              createdAt: new Date(),
            });
          }
        }

        await send({ type: "done", content: finalContent });
      } catch (err) {
        console.error("[chat loop]", err);
        await send({ type: "error", error: String(err) });
      } finally {
        await writer.close();
      }
    })();

    return new Response(readable, {
      headers: {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-cache, no-transform",
        Connection: "keep-alive",
        "X-Accel-Buffering": "no",
      },
    });
  } catch (err) {
    console.error("[chat]", err);
    return Response.json({ error: String(err) }, { status: 500 });
  }
}
