import { NextRequest, NextResponse } from "next/server";
import OpenAI from "openai";
import { db } from "@/db";
import { messages, sessions, projects, projectFiles } from "@/db/schema";
import { eq, asc } from "drizzle-orm";
import { UNITY_TOOLS } from "@/lib/tools";
import { executeToolCall } from "@/lib/orchestrator";
import type { ToolName } from "@/lib/tools";
import type {
  ChatCompletionMessageParam,
  ChatCompletionToolMessageParam,
  ChatCompletionAssistantMessageParam,
} from "openai/resources/chat/completions";

function getOpenAI() {
  return new OpenAI({
    apiKey: process.env.OPENAI_API_KEY ?? "sk-placeholder",
  });
}

const SYSTEM_PROMPT = `You are AliTerra AI — an expert Unity game developer assistant embedded directly inside the Unity Editor.

You have FULL CONTROL over the Unity project through tools. You don't just suggest code — you write it, apply it, and verify it works.

## Your Workflow:
1. **Analyze** — Always start by calling list_project_files to understand the project structure
2. **Plan** — Break complex tasks into steps (tell the user your plan briefly)
3. **Execute** — Use tools to create/modify scripts, set properties
4. **Verify** — After applying code, call read_console_logs to check for errors
5. **Auto-fix** — If you see compilation errors, immediately fix them and re-apply
6. **Report** — Tell the user what was done and what they should see in Unity

## Rules:
- ALWAYS write complete, compilable C# code — no placeholders, no "TODO: implement"
- ALWAYS include proper using statements at the top of scripts
- ALWAYS use UnityEngine namespace correctly
- After writing code, ALWAYS check console logs for errors
- If an error occurs, fix it automatically without asking the user
- Speak in the user's language (detect from their message)
- Keep explanations concise — users want to see results, not walls of text

## Unity Best Practices:
- Use [SerializeField] for inspector-exposed private fields
- Prefer coroutines over Update() for timed events
- Use ScriptableObjects for data that doesn't change at runtime
- Always null-check GetComponent results
- Use NavMeshAgent for AI movement, not manual position setting`;

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { sessionId, userMessage } = body as {
      sessionId: string;
      userMessage: string;
    };

    if (!sessionId || !userMessage) {
      return NextResponse.json({ error: "Missing sessionId or userMessage" }, { status: 400 });
    }

    // Get session & project
    const [session] = await db
      .select()
      .from(sessions)
      .where(eq(sessions.id, sessionId))
      .limit(1);

    if (!session) {
      return NextResponse.json({ error: "Session not found" }, { status: 404 });
    }

    const [project] = await db
      .select()
      .from(projects)
      .where(eq(projects.id, session.projectId))
      .limit(1);

    if (!project) {
      return NextResponse.json({ error: "Project not found" }, { status: 404 });
    }

    // Save user message
    await db.insert(messages).values({
      sessionId,
      role: "user",
      content: userMessage,
    });

    // Get conversation history
    const history = await db
      .select()
      .from(messages)
      .where(eq(messages.sessionId, sessionId))
      .orderBy(asc(messages.createdAt));

    // Build project context summary
    const files = await db
      .select({ path: projectFiles.path, type: projectFiles.type })
      .from(projectFiles)
      .where(eq(projectFiles.projectId, project.id));

    const projectContext =
      files.length > 0
        ? `\n\n## Current Project: ${project.name} (Unity ${project.unityVersion ?? "unknown"})\nFiles indexed: ${files.length}\nScript files: ${files.filter((f) => f.type === "cs").map((f) => f.path).join(", ") || "none yet"}`
        : `\n\n## Current Project: ${project.name}\nNo files indexed yet. The Unity plugin will index files when connected.`;

    // Build OpenAI messages
    const apiMessages: ChatCompletionMessageParam[] = [
      { role: "system", content: SYSTEM_PROMPT + projectContext },
    ];

    for (const msg of history) {
      if (msg.role === "user") {
        apiMessages.push({ role: "user", content: msg.content ?? "" });
      } else if (msg.role === "assistant") {
        const assistantMsg: ChatCompletionAssistantMessageParam = {
          role: "assistant",
          content: msg.content ?? null,
        };
        if (msg.toolCalls) {
          assistantMsg.tool_calls = msg.toolCalls as ChatCompletionAssistantMessageParam["tool_calls"];
        }
        apiMessages.push(assistantMsg);
      } else if (msg.role === "tool") {
        const toolMsg: ChatCompletionToolMessageParam = {
          role: "tool",
          tool_call_id: msg.toolCallId ?? "",
          content: msg.content ?? "",
        };
        apiMessages.push(toolMsg);
      }
    }

    // Agentic loop
    let iterations = 0;
    const MAX_ITERATIONS = 10;
    let finalContent = "";

    while (iterations < MAX_ITERATIONS) {
      iterations++;

      const response = await getOpenAI().chat.completions.create({
        model: "gpt-4o",
        messages: apiMessages,
        tools: UNITY_TOOLS,
        tool_choice: "auto",
        max_tokens: 4096,
        temperature: 0.3,
      });

      const choice = response.choices[0];
      const assistantMsg = choice.message;

      // Save assistant message to DB
      await db.insert(messages).values({
        sessionId,
        role: "assistant",
        content: assistantMsg.content ?? null,
        toolCalls: assistantMsg.tool_calls
          ? JSON.parse(JSON.stringify(assistantMsg.tool_calls))
          : null,
      });

      // Add to conversation
      apiMessages.push({
        role: "assistant",
        content: assistantMsg.content ?? null,
        tool_calls: assistantMsg.tool_calls,
      } as ChatCompletionAssistantMessageParam);

      // If no tool calls, we're done
      if (!assistantMsg.tool_calls || assistantMsg.tool_calls.length === 0) {
        finalContent = assistantMsg.content ?? "";
        break;
      }

      // Execute each tool call
      for (const toolCall of assistantMsg.tool_calls) {
        const tc = toolCall as { id: string; function: { name: string; arguments: string } };
        const toolName = tc.function.name as ToolName;
        let toolArgs: Record<string, unknown> = {};
        try {
          toolArgs = JSON.parse(tc.function.arguments);
        } catch {
          toolArgs = {};
        }

        const result = await executeToolCall(toolName, toolArgs, project.id, sessionId);

        const resultContent = JSON.stringify(result);

        // Save tool result
        await db.insert(messages).values({
          sessionId,
          role: "tool",
          content: resultContent,
          toolCallId: tc.id,
          toolName: toolName,
        });

        // Add to conversation
        apiMessages.push({
          role: "tool",
          tool_call_id: tc.id,
          content: resultContent,
        } as ChatCompletionToolMessageParam);
      }

      // Continue loop to let AI process tool results
    }

    // Get all messages to return to client
    const allMessages = await db
      .select()
      .from(messages)
      .where(eq(messages.sessionId, sessionId))
      .orderBy(asc(messages.createdAt));

    return NextResponse.json({
      success: true,
      reply: finalContent,
      messages: allMessages,
    });
  } catch (err) {
    console.error("[chat API error]", err);
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "Internal server error" },
      { status: 500 }
    );
  }
}
