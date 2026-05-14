export interface McpTool {
  name: string;
  description: string;
  category: string;
  params: string[];
}

export interface SkillCard {
  id: string;
  title: string;
  description: string;
  icon: string;
  command: string;
}

export interface ConnectionConfig {
  url: string;
  token: string;
  status: 'disconnected' | 'connecting' | 'connected' | 'error';
}

export type TabId = 'dashboard' | 'tools' | 'skills' | 'infrastructure' | 'terminal';
