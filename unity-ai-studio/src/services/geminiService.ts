import { GoogleGenAI, Type } from "@google/genai";
import { ProjectState } from "./types";

const ai = new GoogleGenAI({ apiKey: process.env.GEMINI_API_KEY! });

export const UNITY_SYSTEM_INSTRUCTION = `You are "Muse", an elite AI Unity Engineer. You specialized in high-performance C#, DOTS, ShaderGraph, and advanced Editor scripting.
Your mission is to act as a senior partner to the user, helping them architect and build complex Unity applications and games.

CORE PRINCIPLES:
- Performance First: Always consider GC allocation and CPU/GPU overhead.
- Unity Best Practices: Use composition over inheritance, proper event handling (UnityEvent/C# events), and scriptable objects for data.
- Modern Unity: You are aware of Unity 6 Features, updated APIs, and the latest Render Pipelines (URP/HDRP).
- Clear Documentation: Every script you generate must be well-commented.

TOOL USAGE:
- When asked to create logic, generate a full C# script and use 'create_asset'.
- When asked to layout a scene, use 'add_game_object' with appropriate component names.
- For architectural advice, provide detailed text explanations alongside tool calls.

Project Context is automatically synced with your knowledge base.`;

export async function chatWithAI(prompt: string, projectState: ProjectState, onAction: (action: any) => void) {
  const model = "gemini-3.1-pro-preview"; // Use Pro for complex coding

  const tools = [
    {
      functionDeclarations: [
        {
          name: "add_game_object",
          description: "Adds a new GameObject to the scene hierarchy",
          parameters: {
            type: Type.OBJECT,
            properties: {
              name: { type: Type.STRING, description: "Name of the GameObject" },
              components: { 
                type: Type.ARRAY, 
                items: { type: Type.STRING },
                description: "Initial components list (e.g. ['Transform', 'MeshRenderer'])"
              }
            },
            required: ["name"]
          }
        },
        {
          name: "create_asset",
          description: "Creates a new asset file (Script, Shader, etc.) in the project",
          parameters: {
            type: Type.OBJECT,
            properties: {
              path: { type: Type.STRING, description: "Path relative to Assets/ (e.g. Assets/Scripts/NewScript.cs)" },
              content: { type: Type.STRING, description: "Full content of the file" }
            },
            required: ["path", "content"]
          }
        },
        {
          name: "update_file",
          description: "Updates an existing file's content",
          parameters: {
            type: Type.OBJECT,
            properties: {
              path: { type: Type.STRING, description: "Path to the file to update" },
              content: { type: Type.STRING, description: "New content for the file" }
            },
            required: ["path", "content"]
          }
        },
        {
          name: "delete_object",
          description: "Removes a GameObject from the scene",
          parameters: {
            type: Type.OBJECT,
            properties: {
              id: { type: Type.STRING, description: "The unique ID of the object to delete" }
            },
            required: ["id"]
          }
        }
      ]
    }
  ];

  const fullPrompt = `Project State:
Hierarchy: ${JSON.stringify(projectState.hierarchy)}
Files: ${projectState.files.map(f => f.path).join(", ")}

User Request: ${prompt}`;

  const response = await ai.models.generateContent({
    model,
    contents: [{ role: "user", parts: [{ text: fullPrompt }] }],
    config: {
      systemInstruction: UNITY_SYSTEM_INSTRUCTION,
      tools
    }
  });

  const functionCalls = response.functionCalls;
  if (functionCalls) {
    for (const call of functionCalls) {
      onAction(call);
    }
  }

  return response.text;
}
