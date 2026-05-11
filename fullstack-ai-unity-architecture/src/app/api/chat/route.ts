import { NextRequest, NextResponse } from "next/server";
import OpenAI from "openai";
import { db } from "@/db";
import { messages, sessions, projects } from "@/db/schema";
import { eq } from "drizzle-orm";
import { UNITY_TOOLS, executeToolCall } from "@/lib/tools";
import {
  SYSTEM_PROMPT,
  getSession,
  addMessage,
  buildAIContext,
  getOrCreateProject,
  createSession,
} from "@/lib/orchestrator";
import { getProviderConfig, createAIClient, providerSupportsTools } from "@/lib/ai-providers";

export const dynamic = "force-dynamic";

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { 
      sessionId, 
      message, 
      apiKey, 
      projectName,
      // AI Provider settings
      provider,
      customBaseUrl,
      customApiKey,
      customModel,
    } = body;

    // Validate API key and get/create project
    const project = await getOrCreateProject(
      apiKey || "default",
      projectName
    );

    // Get or create session
    let session;
    if (sessionId) {
      const s = await db
        .select()
        .from(sessions)
        .where(eq(sessions.id, sessionId))
        .limit(1);
      if (s.length > 0) {
        session = s[0];
      }
    }
    if (!session) {
      session = await createSession(project.id, "AI Unity Session");
    }

    // Add user message
    await addMessage(session.id, "user", message);

    // Build context
    const context = await buildAIContext(project.id, session.id);

    // Get AI provider config
    const aiConfig = getProviderConfig(provider, customBaseUrl, customApiKey, customModel);
    
    // Create AI client
    const aiClient = createAIClient(aiConfig);
    
    // Check if provider supports tools
    const supportsTools = providerSupportsTools(provider || "openai");

    // Get conversation history
    const sessionData = await getSession(session.id);
    const chatMessages: OpenAI.ChatCompletionMessageParam[] = [
      {
        role: "system",
        content: SYSTEM_PROMPT + "\n\n" + context,
      },
    ];

    // Add conversation history (last 20 messages to stay within token limits)
    const recentMessages = sessionData?.messages.slice(-20) || [];
    for (const msg of recentMessages) {
      if (msg.role === "user") {
        chatMessages.push({ role: "user", content: msg.content || "" });
      } else if (msg.role === "assistant") {
        // Groq/OpenAI require: if tool_calls exists, content can be null or string
        // if tool_calls doesn't exist, content must be string and tool_calls should not be present
        const assistantMsg: any = {
          role: "assistant",
          content: msg.content || "",
        };
        // Only add tool_calls if they exist and are not null
        if (msg.toolCalls && Array.isArray(msg.toolCalls) && msg.toolCalls.length > 0) {
          assistantMsg.tool_calls = msg.toolCalls;
          assistantMsg.content = msg.content || null;
        }
        chatMessages.push(assistantMsg);
      } else if (msg.role === "tool") {
        chatMessages.push({
          role: "tool",
          content: msg.content || "",
          tool_call_id: msg.toolCallId || "",
        });
      }
    }

    // Call AI with or without tools
    const completionParams: any = {
      model: aiConfig.model,
      messages: chatMessages,
      temperature: 0.7,
      max_tokens: 4096,
    };

    // Only add tools if provider supports them
    if (supportsTools) {
      completionParams.tools = UNITY_TOOLS;
      completionParams.tool_choice = "auto";
    }

    let responseMessage;
    try {
      const completion = await aiClient.chat.completions.create(completionParams);
      responseMessage = completion.choices[0].message;
    } catch (error: any) {
      // If tool use fails, try without tools
      if (error.message?.includes("tool") && supportsTools) {
        console.log("Tool use failed, retrying without tools...");
        const completion = await aiClient.chat.completions.create({
          model: aiConfig.model,
          messages: chatMessages,
          temperature: 0.7,
          max_tokens: 4096,
        });
        responseMessage = completion.choices[0].message;
      } else {
        throw error;
      }
    }

    // Handle tool calls
    if (responseMessage.tool_calls && responseMessage.tool_calls.length > 0) {
      // Save assistant message with tool calls
      await addMessage(
        session.id,
        "assistant",
        responseMessage.content,
        responseMessage.tool_calls
      );

      const toolResults = [];

      for (const toolCall of responseMessage.tool_calls) {
        if (toolCall.type !== "function") continue;
        const args = JSON.parse(toolCall.function.arguments);

        // Execute tool
        const result = await executeToolCall(
          toolCall.function.name,
          args,
          project.id,
          db
        );

        // Save tool result
        await addMessage(
          session.id,
          "tool",
          result,
          undefined,
          toolCall.id
        );

        toolResults.push({
          tool_call_id: toolCall.id,
          role: "tool" as const,
          content: result,
        });
      }

      // Get follow-up response after tool execution
      const followUpMessages: OpenAI.ChatCompletionMessageParam[] = [
        ...chatMessages,
        responseMessage,
        ...toolResults,
      ];

      const followUp = await aiClient.chat.completions.create({
        model: aiConfig.model,
        messages: followUpMessages,
        tools: supportsTools ? UNITY_TOOLS : undefined,
        tool_choice: supportsTools ? "auto" : undefined,
        temperature: 0.7,
        max_tokens: 4096,
      });

      const followUpMessage = followUp.choices[0].message;

      // If there are more tool calls, process them recursively
      if (followUpMessage.tool_calls && followUpMessage.tool_calls.length > 0) {
        // Save and process additional tool calls
        await addMessage(
          session.id,
          "assistant",
          followUpMessage.content,
          followUpMessage.tool_calls
        );

        for (const toolCall of followUpMessage.tool_calls) {
          if (toolCall.type !== "function") continue;
          const args = JSON.parse(toolCall.function.arguments);
          const result = await executeToolCall(
            toolCall.function.name,
            args,
            project.id,
            db
          );
          await addMessage(session.id, "tool", result, undefined, toolCall.id);
        }

        // Final response
        const finalMessages: OpenAI.ChatCompletionMessageParam[] = [
          ...followUpMessages,
          followUpMessage,
          ...followUpMessage.tool_calls.map((tc: any) => ({
            tool_call_id: tc.id,
            role: "tool" as const,
            content: "Command executed successfully",
          })),
        ];

        const final = await aiClient.chat.completions.create({
          model: aiConfig.model,
          messages: finalMessages,
          temperature: 0.7,
          max_tokens: 2048,
        });

        const finalContent = final.choices[0].message.content;
        await addMessage(session.id, "assistant", finalContent);

        return NextResponse.json({
          sessionId: session.id,
          response: finalContent,
          toolCalls: [...responseMessage.tool_calls, ...followUpMessage.tool_calls],
          provider: aiConfig.name,
          model: aiConfig.model,
        });
      }

      // Save final response
      await addMessage(session.id, "assistant", followUpMessage.content);

      return NextResponse.json({
        sessionId: session.id,
        response: followUpMessage.content,
        toolCalls: responseMessage.tool_calls,
        provider: aiConfig.name,
        model: aiConfig.model,
      });
    }

    // No tool calls — just save and return response
    await addMessage(session.id, "assistant", responseMessage.content);

    return NextResponse.json({
      sessionId: session.id,
      response: responseMessage.content,
      toolCalls: [],
      provider: aiConfig.name,
      model: aiConfig.model,
    });
  } catch (error: any) {
    console.error("Chat error:", error);
    return NextResponse.json(
      { error: error.message || "Internal server error" },
      { status: 500 }
    );
  }
}

// Get chat history
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const sessionId = searchParams.get("sessionId");

    if (!sessionId) {
      return NextResponse.json({ error: "sessionId required" }, { status: 400 });
    }

    const sessionData = await getSession(sessionId);
    if (!sessionData) {
      return NextResponse.json({ error: "Session not found" }, { status: 404 });
    }

    return NextResponse.json({
      session: sessionData,
      messages: sessionData.messages,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
