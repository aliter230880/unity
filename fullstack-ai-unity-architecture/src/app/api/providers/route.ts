import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

// Available AI providers
const PROVIDERS = {
  groq: {
    name: "Groq (FREE)",
    models: ["llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768"],
    icon: "⚡",
    description: "Бесплатный, очень быстрый",
    supportsTools: true,
  },
  deepseek: {
    name: "DeepSeek",
    models: ["deepseek-chat", "deepseek-coder"],
    icon: "🧠",
    description: "Отличный для кода, дешёвый",
    supportsTools: true,
  },
  grok: {
    name: "Grok (xAI)",
    models: ["grok-beta", "grok-2"],
    icon: "🤖",
    description: "От Elon Musk",
    supportsTools: true,
  },
  openai: {
    name: "OpenAI",
    models: ["gpt-4o-mini", "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo"],
    icon: "✨",
    description: "Премиум качество",
    supportsTools: true,
  },
  together: {
    name: "Together AI",
    models: ["meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1"],
    icon: "🔗",
    description: "Open-source модели",
    supportsTools: true,
  },
  ollama: {
    name: "Ollama (Local)",
    models: ["llama3.1", "codellama", "mistral", "qwen2.5-coder"],
    icon: "🏠",
    description: "Локальный, бесплатный",
    supportsTools: false,
  },
  custom: {
    name: "Custom",
    models: ["any-model"],
    icon: "⚙️",
    description: "Любой OpenAI-совместимый API",
    supportsTools: true,
  },
};

// Check which providers have API keys configured
export async function GET() {
  const providerStatus = Object.entries(PROVIDERS).map(([key, provider]) => {
    let hasKey = false;
    if (key !== "custom" && key !== "ollama") {
      const envKey = `${key.toUpperCase()}_API_KEY`;
      hasKey = !!process.env[envKey];
    }
    return {
      key,
      ...provider,
      hasKey,
    };
  });

  return NextResponse.json({
    providers: providerStatus,
  });
}
