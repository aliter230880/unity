// AI Provider abstraction layer
// Supports any OpenAI-compatible API

export interface AIProviderConfig {
  name: string;
  baseUrl: string;
  apiKey: string;
  model: string;
  supportsToolUse: boolean;
}

// Preset providers
export const PROVIDERS = {
  openai: {
    name: "OpenAI",
    baseUrl: "https://api.openai.com/v1",
    models: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"],
    supportsToolUse: true,
  },
  deepseek: {
    name: "DeepSeek",
    baseUrl: "https://api.deepseek.com/v1",
    models: ["deepseek-chat", "deepseek-coder"],
    supportsToolUse: true, // DeepSeek supports function calling
  },
  grok: {
    name: "Grok (xAI)",
    baseUrl: "https://api.x.ai/v1",
    models: ["grok-beta", "grok-2"],
    supportsToolUse: true,
  },
  groq: {
    name: "Groq (FREE)",
    baseUrl: "https://api.groq.com/openai/v1",
    models: ["llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768"],
    supportsToolUse: true,
  },
  ollama: {
    name: "Ollama (Local)",
    baseUrl: "http://localhost:11434/v1",
    models: ["llama3.1", "codellama", "mistral", "qwen2.5-coder"],
    supportsToolUse: false, // Most Ollama models don't support tools
  },
  together: {
    name: "Together AI",
    baseUrl: "https://api.together.xyz/v1",
    models: ["meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1"],
    supportsToolUse: true,
  },
  custom: {
    name: "Custom (OpenAI-compatible)",
    baseUrl: "",
    models: ["any-model"],
    supportsToolUse: true,
  },
} as const;

export type ProviderName = keyof typeof PROVIDERS;

// Get provider config from environment or user settings
export function getProviderConfig(
  providerName?: string,
  customBaseUrl?: string,
  customApiKey?: string,
  customModel?: string
): AIProviderConfig {
  // If custom provider
  if (providerName === "custom" && customBaseUrl) {
    return {
      name: "Custom",
      baseUrl: customBaseUrl,
      apiKey: customApiKey || "",
      model: customModel || "any",
      supportsToolUse: true,
    };
  }

  // Get preset provider
  const providerKey = (providerName || "openai") as ProviderName;
  const provider = PROVIDERS[providerKey];

  if (!provider) {
    throw new Error(`Unknown provider: ${providerName}`);
  }

  // Get API key from environment
  const envKeyMap: Record<string, string> = {
    openai: "OPENAI_API_KEY",
    deepseek: "DEEPSEEK_API_KEY",
    grok: "GROK_API_KEY",
    groq: "GROQ_API_KEY",
    ollama: "OLLAMA_API_KEY", // usually empty for local
    together: "TOGETHER_API_KEY",
  };

  const apiKey = customApiKey || process.env[envKeyMap[providerKey]] || "";

  return {
    name: provider.name,
    baseUrl: provider.baseUrl,
    apiKey,
    model: customModel || provider.models[0],
    supportsToolUse: provider.supportsToolUse,
  };
}

// Create OpenAI-compatible client for any provider
export function createAIClient(config: AIProviderConfig) {
  // Import OpenAI client (works with any OpenAI-compatible API)
  const OpenAI = require("openai");

  const clientConfig: any = {
    apiKey: config.apiKey || "ollama", // Ollama doesn't need a key
  };

  // Set base URL if not OpenAI
  if (config.baseUrl && config.baseUrl !== "https://api.openai.com/v1") {
    clientConfig.baseURL = config.baseUrl;
  }

  return new OpenAI(clientConfig);
}

// Get available models for a provider
export function getProviderModels(providerName: string): string[] {
  const provider = PROVIDERS[providerName as ProviderName];
  if (!provider) return [];
  return [...provider.models];
}

// Check if provider supports tool use
export function providerSupportsTools(providerName: string): boolean {
  const provider = PROVIDERS[providerName as ProviderName];
  return provider?.supportsToolUse ?? false;
}
