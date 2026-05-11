import React, { useState, useEffect, useRef } from "react";
import { 
  Box, 
  FolderTree, 
  FileCode, 
  Settings, 
  Plus, 
  Trash2, 
  MessageSquare, 
  Play, 
  Search,
  Code2,
  BoxSelect,
  Layers,
  ChevronRight,
  ChevronDown,
  Info,
  Terminal,
  Send,
  Cpu
} from "lucide-react";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { Card } from "@/components/ui/card";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ProjectState, UnityFile, UnityObject } from "./types";
import { chatWithAI } from "./services/geminiService";
import { motion, AnimatePresence } from "motion/react";

export default function UnityAIStudio() {
  const [project, setProject] = useState<ProjectState | null>(null);
  const [selectedObjectId, setSelectedObjectId] = useState<string | null>(null);
  const [activeFilePath, setActiveFilePath] = useState<string | null>(null);
  const [chatInput, setChatInput] = useState("");
  const [messages, setMessages] = useState<{ role: 'user' | 'ai', text: string }[]>([
    { role: 'ai', text: "Welcome to Unity AI Studio. I'm Muse, your AI development assistant. How can I help you today?" }
  ]);
  const [isAiLoading, setIsAiLoading] = useState(false);
  const chatEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetchProject();
  }, []);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const fetchProject = async () => {
    try {
      const res = await fetch("/api/project");
      const data = await res.json();
      setProject(data);
    } catch (err) {
      console.error("Failed to fetch project", err);
    }
  };

  const handleAction = async (call: any) => {
    console.log("AI Action Called:", call);
    let apiAction = "";
    let payload = {};

    if (call.name === "add_game_object") {
      apiAction = "ADD_OBJECT";
      payload = { name: call.args.name, components: call.args.components };
    } else if (call.name === "create_asset") {
      apiAction = "CREATE_FILE";
      payload = { path: call.args.path, content: call.args.content };
    } else if (call.name === "update_file") {
      apiAction = "UPDATE_FILE";
      payload = { path: call.args.path, content: call.args.content };
    } else if (call.name === "delete_object") {
      apiAction = "DELETE_OBJECT";
      payload = { id: call.args.id };
    }

    if (apiAction) {
      const res = await fetch("/api/project/action", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: apiAction, payload })
      });
      const updated = await res.json();
      if (updated.status === "success") {
        setProject(updated.projectState);
      }
    }
  };

  const onSendChat = async () => {
    if (!chatInput.trim() || !project) return;

    const userText = chatInput;
    setChatInput("");
    setMessages(prev => [...prev, { role: 'user', text: userText }]);
    setIsAiLoading(true);

    try {
      const aiResponse = await chatWithAI(userText, project, handleAction);
      if (aiResponse) {
        setMessages(prev => [...prev, { role: 'ai', text: aiResponse }]);
      }
    } catch (err) {
      setMessages(prev => [...prev, { role: 'ai', text: "Sorry, I encountered an error while processing your request." }]);
    } finally {
      setIsAiLoading(false);
    }
  };

  const activeFile = project?.files.find(f => f.path === activeFilePath);
  const selectedObject = project?.hierarchy.find(obj => obj.id === selectedObjectId);

  if (!project) return <div className="flex h-screen items-center justify-center bg-[#151619] text-white">Initializing Unity AI Studio...</div>;

  return (
    <TooltipProvider>
      <div className="flex h-screen flex-col bg-[#121212] text-gray-200 font-sans selection:bg-blue-500/30">
        {/* Top Navbar */}
        <header className="h-12 border-b border-white/10 bg-[#1e1e1e] flex items-center justify-between px-6">
          <div className="flex items-center gap-4">
            <div className="w-6 h-6 bg-white/90 rounded-sm flex items-center justify-center">
              <div className="w-3 h-3 bg-black rotate-45"></div>
            </div>
            <span className="font-medium tracking-tight text-white flex items-center">
              Unity AI Fullstack Developer 
              <span className="text-blue-400 font-mono text-[10px] ml-3 bg-blue-400/10 px-2 py-0.5 rounded border border-blue-400/20">v2.7.0-pre.3</span>
            </span>
          </div>
          <div className="flex items-center gap-4">
            <div className="hidden md:flex bg-black/40 rounded px-2 py-1 gap-4 text-[10px] font-mono border border-white/5">
              <span className="text-green-400 flex items-center gap-1.5"><div className="w-1.5 h-1.5 rounded-full bg-green-400 shadow-[0_0_8px_rgba(74,222,128,0.5)]"></div> GPU: READY</span>
              <span className="text-blue-400 flex items-center gap-1.5"><div className="w-1.5 h-1.5 rounded-full bg-blue-400 shadow-[0_0_8px_rgba(96,165,250,0.5)]"></div> AI AGENT: CONNECTED</span>
            </div>
            <div className="flex items-center gap-2">
              <Button size="sm" className="bg-blue-600 hover:bg-blue-500 text-white text-[11px] font-semibold h-7 px-4 rounded transition-all active:scale-95 shadow-lg shadow-blue-600/20">BUILD ASSETS</Button>
              <Button size="icon" variant="ghost" className="h-8 w-8 text-gray-400 hover:text-white hover:bg-white/5"><Settings className="h-4 w-4" /></Button>
            </div>
          </div>
        </header>

        {/* Main Content Area */}
        <main className="flex-1 overflow-hidden bg-[#121212]">
          <ResizablePanelGroup direction="horizontal">
            {/* Left Column: Project/Hierarchy */}
            <ResizablePanel defaultSize={20} minSize={15}>
              <ResizablePanelGroup direction="vertical">
                <ResizablePanel defaultSize={40}>
                  <div className="flex h-full flex-col border-r border-white/10 bg-[#181818]">
                    <div className="p-3 text-[10px] uppercase tracking-widest text-gray-500 font-bold border-b border-white/5 flex items-center gap-2">
                      <Layers className="h-3 w-3" /> Hierarchy
                    </div>
                    <ScrollArea className="flex-1 p-2">
                      {project.hierarchy.map(obj => (
                        <div 
                          key={obj.id} 
                          onClick={() => setSelectedObjectId(obj.id)}
                          className={`
                            group flex items-center gap-2 px-2 py-1.5 text-xs cursor-pointer rounded transition-colors
                            ${selectedObjectId === obj.id ? 'bg-blue-600/20 text-blue-300 border border-blue-500/30' : 'text-gray-400 hover:bg-white/5'}
                          `}
                        >
                          <BoxSelect className={`h-3.5 w-3.5 ${selectedObjectId === obj.id ? 'text-blue-300' : 'text-gray-600'}`} />
                          <span className="flex-1 truncate">{obj.name}</span>
                        </div>
                      ))}
                    </ScrollArea>
                  </div>
                </ResizablePanel>
                <ResizableHandle className="bg-white/10 h-1" />
                <ResizablePanel defaultSize={60}>
                  <div className="flex h-full flex-col border-r border-white/10 bg-[#181818]">
                    <div className="p-3 text-[10px] uppercase tracking-widest text-gray-500 font-bold border-b border-white/5 flex items-center gap-2">
                      <FolderTree className="h-3 w-3" /> Project Explorer
                    </div>
                    <ScrollArea className="flex-1 p-2 space-y-1">
                      <div className="flex items-center gap-2 p-1 text-[11px] text-gray-500 font-bold uppercase tracking-tighter opacity-80">
                        <ChevronDown className="h-3 w-3" /> Assets
                      </div>
                      <div className="pl-3 space-y-0.5">
                        {project.files.map(file => (
                          <div 
                            key={file.path} 
                            onClick={() => {
                              setActiveFilePath(file.path);
                              setSelectedObjectId(null);
                            }}
                            className={`
                              flex items-center gap-2 px-2 py-1.5 text-xs cursor-pointer rounded transition-colors
                              ${activeFilePath === file.path ? 'bg-blue-600/20 text-blue-300 border border-blue-500/30' : 'text-gray-400 hover:bg-white/5'}
                            `}
                          >
                            <span className={`text-[10px] font-mono ${activeFilePath === file.path ? 'text-blue-300' : 'text-blue-500'}`}>#</span>
                            <span className="truncate">{file.path.split('/').pop()}</span>
                          </div>
                        ))}
                      </div>
                    </ScrollArea>
                    <div className="p-4 border-t border-white/10 bg-black/20">
                      <div className="text-[9px] text-gray-500 uppercase font-bold mb-2 tracking-widest">Memory usage</div>
                      <div className="h-1 bg-white/5 rounded-full overflow-hidden">
                        <div className="w-1/3 h-full bg-blue-500 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></div>
                      </div>
                    </div>
                  </div>
                </ResizablePanel>
              </ResizablePanelGroup>
            </ResizablePanel>

            <ResizableHandle className="bg-white/10 w-1" />

            {/* Middle Column: Central Workspace */}
            <ResizablePanel defaultSize={55}>
              <div className="flex h-full flex-col bg-[#121212]">
                <Tabs defaultValue="scene" className="flex h-full flex-col">
                  <div className="flex items-center justify-between border-b border-white/10 px-4 h-10 bg-[#1e1e1e]">
                    <TabsList className="bg-transparent h-full p-0 flex gap-6">
                      <TabsTrigger value="scene" className="h-full px-0 rounded-none border-b-2 border-transparent data-[state=active]:border-blue-500 data-[state=active]:bg-transparent text-[10px] uppercase font-bold tracking-[0.2em] text-gray-500 data-[state=active]:text-white">Scene</TabsTrigger>
                      <TabsTrigger value="game" className="h-full px-0 rounded-none border-b-2 border-transparent data-[state=active]:border-blue-500 data-[state=active]:bg-transparent text-[10px] uppercase font-bold tracking-[0.2em] text-gray-500 data-[state=active]:text-white">Game</TabsTrigger>
                      {activeFilePath && (
                         <TabsTrigger value="code" className="h-full px-0 rounded-none border-b-2 border-transparent data-[state=active]:border-blue-500 data-[state=active]:bg-transparent text-[10px] uppercase font-bold tracking-[0.2em] text-gray-500 data-[state=active]:text-white">AISystem.cs</TabsTrigger>
                      )}
                    </TabsList>
                  </div>
                  <TabsContent value="scene" className="flex-1 p-0 m-0 relative">
                    <div className="h-full w-full bg-[#1e1e1e] flex items-center justify-center group overflow-hidden">
                       <div className="absolute inset-0 unity-grid opacity-30"></div>
                       <div className="absolute top-6 left-6 flex flex-col gap-2 p-1.5 bg-[#121212]/60 backdrop-blur-md rounded border border-white/10 shadow-2xl">
                         <Tooltip><TooltipTrigger asChild><Button size="icon" variant="ghost" className="h-8 w-8 text-gray-400 hover:text-white hover:bg-white/5"><BoxSelect className="h-4 w-4" /></Button></TooltipTrigger><TooltipContent side="right">Select (Q)</TooltipContent></Tooltip>
                         <Tooltip><TooltipTrigger asChild><Button size="icon" variant="ghost" className="h-8 w-8 text-gray-400 hover:text-white hover:bg-white/5"><Plus className="h-4 w-4" /></Button></TooltipTrigger><TooltipContent side="right">Move (W)</TooltipContent></Tooltip>
                       </div>
                       <div className="text-center z-10 space-y-4">
                          <div className="relative inline-block">
                             <Cpu className="h-16 w-16 text-blue-500/40 animate-pulse" />
                             <div className="absolute inset-0 bg-blue-500/10 blur-2xl rounded-full"></div>
                          </div>
                          <p className="text-[11px] font-mono tracking-[0.4em] text-blue-400/60 uppercase">System Rendering Context</p>
                       </div>
                    </div>
                  </TabsContent>
                  <TabsContent value="game" className="flex-1 p-0 m-0 bg-black flex items-center justify-center">
                      <span className="text-gray-600 text-xs font-mono uppercase tracking-[0.3em]">Press Build to initialize simulation</span>
                  </TabsContent>
                  <TabsContent value="code" className="flex-1 p-0 m-0 flex flex-col overflow-hidden bg-[#1e1e1e]">
                    {activeFile ? (
                      <>
                        <div className="bg-[#181818] px-6 py-3 border-b border-white/5 flex justify-between items-center">
                          <div className="flex items-center gap-3">
                            <span className="text-blue-400 font-mono text-xs">#</span>
                            <span className="text-[11px] font-mono text-gray-400 tracking-tight">{activeFile.path}</span>
                          </div>
                          <Badge className="bg-blue-600/10 text-blue-400 border border-blue-500/20 text-[9px] uppercase tracking-widest font-bold">Read-Write</Badge>
                        </div>
                        <ScrollArea className="flex-1 bg-[#121212] p-8 font-mono text-[13px] leading-relaxed">
                          <pre className="text-blue-100/90 whitespace-pre-wrap">
                             <code>{activeFile.content}</code>
                          </pre>
                        </ScrollArea>
                      </>
                    ) : (
                      <div className="flex-1 flex items-center justify-center text-gray-600 italic text-xs">Waiting for selection...</div>
                    )}
                  </TabsContent>
                </Tabs>
              </div>
            </ResizablePanel>

            <ResizableHandle className="bg-white/10 w-1" />

            {/* Right Column: Inspector */}
            <ResizablePanel defaultSize={25} minSize={20}>
              <div className="flex h-full flex-col border-l border-white/10 bg-[#181818]">
                <div className="p-4 text-[10px] uppercase tracking-widest text-gray-500 font-bold border-b border-white/5">AI Inspector</div>
                <ScrollArea className="flex-1">
                  {selectedObject ? (
                    <div className="p-6 space-y-8">
                       <div className="space-y-3">
                          <label className="text-[10px] text-gray-500 uppercase font-bold tracking-widest">Entity Properties</label>
                          <div className="flex items-center gap-3 p-3 bg-[#252525] border border-white/5 rounded-lg focus-within:border-blue-500/50 transition-colors">
                             <BoxSelect className="h-4 w-4 text-blue-400" />
                             <Input defaultValue={selectedObject.name} className="flex-1 h-6 bg-transparent border-none text-xs font-bold focus-visible:ring-0 p-0 text-white" />
                          </div>
                       </div>
                      
                      <div className="space-y-4">
                        <label className="text-[10px] text-gray-500 uppercase font-bold tracking-widest">Components</label>
                        {selectedObject.components.map((comp, idx) => (
                          <motion.div initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: idx * 0.05 }} key={idx}>
                            <Card className="bg-[#1e1e1e] border-white/10 rounded overflow-hidden shadow-xl shadow-black/20">
                               <div className="bg-white/5 px-3 py-2 flex items-center justify-between border-b border-white/5">
                                  <span className="text-[10px] font-bold uppercase text-gray-300 flex items-center gap-2">
                                     <div className="w-1.5 h-1.5 bg-blue-500 rounded-full"></div>
                                     {comp}
                                  </span>
                                  <ChevronDown className="h-3 w-3 text-gray-500" />
                               </div>
                               <div className="p-4 space-y-3">
                                  <div className="grid grid-cols-3 gap-3 items-center text-[10px] font-mono">
                                     <span className="text-gray-500">Transform</span>
                                     <div className="col-span-2 flex gap-1.5">
                                        <div className="bg-[#121212] flex-1 px-2 py-1 border border-white/5 rounded text-gray-300"><span className="text-rose-500 mr-2 opacity-50">X</span>0</div>
                                        <div className="bg-[#121212] flex-1 px-2 py-1 border border-white/5 rounded text-gray-300"><span className="text-emerald-500 mr-2 opacity-50">Y</span>0</div>
                                        <div className="bg-[#121212] flex-1 px-2 py-1 border border-white/5 rounded text-gray-300"><span className="text-sky-500 mr-2 opacity-50">Z</span>0</div>
                                     </div>
                                  </div>
                               </div>
                            </Card>
                          </motion.div>
                        ))}
                      </div>

                      <Button variant="outline" className="w-full h-9 text-[10px] font-bold bg-[#252525] border-white/10 hover:bg-white/5 text-gray-300 uppercase tracking-widest">ADD COMPONENT</Button>
                    </div>
                  ) : (
                    <div className="p-6 space-y-8">
                       <div className="space-y-4">
                          <label className="text-[10px] text-gray-500 uppercase font-bold tracking-widest">Agent Persona</label>
                          <div className="bg-[#252525] border border-white/10 rounded-lg p-2 text-xs text-white flex justify-between items-center cursor-pointer hover:border-blue-500/40 transition-colors">
                             Fullstack Engineer
                             <ChevronDown className="h-3.5 w-3.5 text-gray-500" />
                          </div>
                       </div>
                       
                       <div className="space-y-4 pt-4 border-t border-white/5">
                          <label className="text-[10px] text-gray-500 uppercase font-bold tracking-widest">Context Awareness</label>
                          <div className="space-y-3 mt-4">
                             {[
                                { name: "Local Assets", status: "ONLINE", color: "text-green-500" },
                                { name: "Unity Registry", status: "CONNECTED", color: "text-blue-500" },
                                { name: "Neural Cache", status: "READY", color: "text-purple-500" }
                             ].map((ctx, i) => (
                                <div key={i} className="flex items-center justify-between text-[11px]">
                                   <span className="text-gray-400">{ctx.name}</span>
                                   <span className={ctx.color + " font-mono font-bold text-[9px]"}>{ctx.status}</span>
                                </div>
                             ))}
                          </div>
                       </div>

                       <div className="mt-8">
                          <div className="p-4 bg-blue-600/10 border border-blue-500/20 rounded-lg text-[11px] text-blue-200 leading-relaxed shadow-lg">
                             <p className="font-bold mb-2 text-blue-400 flex items-center gap-2 uppercase tracking-widest text-[9px]"><Info className="h-3 w-3" /> System Intelligence</p>
                             Architecture refactored to <span className="text-white italic underline underline-offset-4">Service Locator</span> pattern for Muse v2.7 compatibility.
                          </div>
                       </div>
                    </div>
                  )}
                </ScrollArea>
              </div>
            </ResizablePanel>
          </ResizablePanelGroup>
        </main>

        {/* Global Console / AI Interaction Layer */}
        <footer className="h-64 border-t border-white/10 bg-[#151619] flex shadow-[0_-10px_30px_rgba(0,0,0,0.5)]">
           {/* Terminal Output */}
           <div className="flex-1 flex flex-col border-r border-white/10">
              <div className="flex h-8 items-center bg-[#1e1e1e] px-4 border-b border-white/5">
                <span className="text-[9px] uppercase tracking-[0.3em] font-black text-gray-500 flex items-center gap-2">
                  <Terminal className="h-3 w-3" /> System_Logs
                </span>
              </div>
              <ScrollArea className="flex-1 p-4 font-mono text-[11px] text-gray-500/80 leading-relaxed">
                 <div className="text-green-500/60 font-bold">[14:42:01] INITIALIZING MUSE AGENT CORE...</div>
                 <div className="pl-4 border-l border-white/5 my-1">
                   <div>&gt; CHECKING LOCAL ASSETS... OK</div>
                   <div>&gt; SYNCING UNITY_VERSION: 2022.3 LTS... OK</div>
                 </div>
                 <div className="text-blue-400/60 font-bold mt-2">[14:42:05] ASSISTANT ARCHITECTURE LOADED.</div>
                 <div className="text-white/40 mt-1">&gt; Ready for spatial queries or script generation.</div>
              </ScrollArea>
           </div>
           
           {/* AI Chat Interface */}
           <div className="w-[500px] flex flex-col bg-[#181818]">
              <div className="flex h-8 items-center px-4 border-b border-white/5 justify-between">
                <div className="flex items-center gap-2">
                  <div className="w-2 h-2 rounded-sm bg-gradient-to-br from-blue-500 to-purple-600"></div>
                  <span className="text-[10px] uppercase tracking-[0.2em] font-bold text-blue-400">Muse AI Agent</span>
                </div>
                <div className="flex items-center gap-2">
                   <div className="w-1.5 h-1.5 rounded-full bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.5)]"></div>
                   <span className="text-[9px] font-mono text-gray-500">v2.7.0b</span>
                </div>
              </div>
              <ScrollArea className="flex-1 p-6">
                 <div className="space-y-6">
                    <AnimatePresence>
                    {messages.map((m, i) => (
                      <motion.div 
                        initial={{ opacity: 0, x: m.role === 'user' ? 20 : -20 }}
                        animate={{ opacity: 1, x: 0 }}
                        key={i} 
                        className={`flex gap-4 ${m.role === 'user' ? 'justify-end' : ''}`}
                      >
                         {m.role === 'ai' && (
                           <div className="w-7 h-7 rounded bg-gradient-to-br from-blue-600 to-indigo-800 flex-shrink-0 flex items-center justify-center border border-white/10 shadow-lg">
                             <div className="w-3 h-3 bg-white/20 rounded-full blur-[2px]"></div>
                           </div>
                         )}
                         <div className={`
                            max-w-[85%] rounded-xl px-4 py-3 text-xs leading-relaxed shadow-2xl transition-all
                            ${m.role === 'user' 
                              ? 'bg-blue-600 text-white rounded-tr-none border border-blue-400/30' 
                              : 'bg-[#252525] text-gray-200 rounded-tl-none border border-white/10'}
                         `}>
                            {m.text}
                         </div>
                      </motion.div>
                    ))}
                    </AnimatePresence>
                    {isAiLoading && (
                      <div className="flex gap-4">
                         <div className="w-7 h-7 rounded bg-gradient-to-br from-blue-600 to-indigo-800 flex-shrink-0 flex items-center justify-center opacity-50">
                            <div className="w-2 h-2 bg-white rounded-full animate-ping"></div>
                         </div>
                         <div className="bg-[#252525]/50 rounded-xl px-4 py-3 text-xs text-gray-500 flex items-center gap-3 italic border border-white/5">
                           Thinking...
                         </div>
                      </div>
                    )}
                    <div ref={chatEndRef} />
                 </div>
              </ScrollArea>
              <div className="p-4 bg-[#121212] border-t border-white/10">
                 <div className="relative group max-w-xl mx-auto">
                    <Input 
                      value={chatInput}
                      onChange={e => setChatInput(e.target.value)}
                      onKeyDown={e => e.key === 'Enter' && onSendChat()}
                      placeholder="Describe the system you want to build..." 
                      className="w-full bg-[#1e1e1e] border border-white/5 rounded-full py-5 px-6 text-xs text-white focus-visible:ring-blue-500/50 focus-visible:border-blue-500/50 transition-all placeholder:text-gray-600 pr-24"
                    />
                    <Button 
                      disabled={isAiLoading} 
                      onClick={onSendChat} 
                      className="absolute right-2 top-1.5 h-7 px-4 rounded-full bg-white text-black hover:bg-gray-200 text-[10px] font-black uppercase tracking-widest transition-all active:scale-95"
                    >
                      PROMPT AI
                    </Button>
                 </div>
              </div>
           </div>
        </footer>

        {/* Status Bar */}
        <footer className="h-6 bg-[#007acc] text-white flex items-center justify-between px-4 text-[10px] font-bold tracking-tight">
          <div className="flex gap-6">
            <span className="flex items-center gap-2">READY <div className="w-1 h-1 bg-white rounded-full opacity-50"></div> Unity 2022.3 LTS</span>
            <span className="flex items-center gap-2 opacity-80"><Layers className="h-3 w-3" /> Master Branch</span>
          </div>
          <div className="flex gap-6">
            <span className="font-mono">UTF-8</span>
            <span className="flex items-center gap-2">AI LATENCY: <span className="font-mono text-green-300">45ms</span></span>
          </div>
        </footer>
      </div>
    </TooltipProvider>
  );
}
