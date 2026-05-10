'use client';

import React, { useState, useEffect } from 'react';

interface Project {
  id: string;
  name: string;
}

interface Message {
  id: string;
  role: string;
  content: string;
  isErrorFix: boolean;
}

export default function ProjectManager() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchProjects();
  }, []);

  useEffect(() => {
    if (selectedProjectId) {
      fetchMessages(selectedProjectId);
    }
  }, [selectedProjectId]);

  async function fetchProjects() {
    const res = await fetch('/api/projects');
    const data = await res.json();
    setProjects(data);
    setLoading(false);
  }

  async function fetchMessages(projectId: string) {
    const res = await fetch(`/api/projects/${projectId}/messages`);
    const data = await res.json();
    setMessages(data);
  }

  if (loading) return <div>Loading projects...</div>;

  return (
    <div className="flex gap-8">
      <div className="w-1/3 border-r pr-4">
        <h2 className="text-xl font-semibold mb-4">Projects</h2>
        <ul className="space-y-2">
          {projects.map((p) => (
            <li 
              key={p.id} 
              onClick={() => setSelectedProjectId(p.id)}
              className={`p-2 cursor-pointer rounded ${selectedProjectId === p.id ? 'bg-blue-100 text-blue-700' : 'hover:bg-gray-100'}`}
            >
              {p.name}
            </li>
          ))}
        </ul>
      </div>
      <div className="w-2/3">
        {selectedProjectId ? (
          <div>
            <h2 className="text-xl font-semibold mb-4">Conversation History</h2>
            <div className="space-y-4 overflow-y-auto max-h-[80vh]">
              {messages.map((m) => (
                <div key={m.id} className={`p-4 rounded-lg ${m.role === 'user' ? 'bg-gray-100 ml-12' : 'bg-blue-50 mr-12 border border-blue-200'}`}>
                  <div className="text-xs font-bold uppercase mb-1 text-gray-500">
                    {m.role === 'user' ? 'Unity User' : 'AI Assistant'} 
                    {m.isErrorFix && <span className="ml-2 text-red-500">[Auto-Fix]</span>}
                  </div>
                  <pre className="whitespace-pre-wrap font-sans text-sm">{m.content}</pre>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="text-gray-500 italic">Select a project to view history</div>
        )}
      </div>
    </div>
  );
}
