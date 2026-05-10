import { pgTable, text, timestamp, jsonb, integer, boolean, serial } from "drizzle-orm/pg-core";

// Unity Projects registered via plugin
export const projects = pgTable("projects", {
  id: text("id").primaryKey(),
  name: text("name").notNull(),
  unityVersion: text("unity_version"),
  apiKey: text("api_key").notNull().unique(),
  lastSeen: timestamp("last_seen").defaultNow(),
  createdAt: timestamp("created_at").defaultNow(),
});

// Chat sessions per project
export const sessions = pgTable("sessions", {
  id: text("id").primaryKey(),
  projectId: text("project_id").notNull().references(() => projects.id),
  title: text("title").notNull().default("New Session"),
  createdAt: timestamp("created_at").defaultNow(),
  updatedAt: timestamp("updated_at").defaultNow(),
});

// Messages in a session
export const messages = pgTable("messages", {
  id: serial("id").primaryKey(),
  sessionId: text("session_id").notNull().references(() => sessions.id),
  role: text("role").notNull(), // user | assistant | tool | system
  content: text("content"),
  toolCalls: jsonb("tool_calls"),
  toolCallId: text("tool_call_id"),
  toolName: text("tool_name"),
  createdAt: timestamp("created_at").defaultNow(),
});

// Project file index (maintained by Unity plugin)
export const projectFiles = pgTable("project_files", {
  id: serial("id").primaryKey(),
  projectId: text("project_id").notNull().references(() => projects.id),
  path: text("path").notNull(),
  type: text("type").notNull(), // cs | scene | prefab | asset | shader | etc
  content: text("content"),
  size: integer("size").default(0),
  lastModified: timestamp("last_modified").defaultNow(),
});

// Pending commands to be picked up by Unity plugin
export const pendingCommands = pgTable("pending_commands", {
  id: serial("id").primaryKey(),
  projectId: text("project_id").notNull().references(() => projects.id),
  sessionId: text("session_id").references(() => sessions.id),
  command: text("command").notNull(), // tool name: create_script | modify_script | read_console | etc
  payload: jsonb("payload").notNull(),
  status: text("status").notNull().default("pending"), // pending | executing | done | error
  result: text("result"),
  createdAt: timestamp("created_at").defaultNow(),
  executedAt: timestamp("executed_at"),
});

// Console logs sent by Unity plugin
export const consoleLogs = pgTable("console_logs", {
  id: serial("id").primaryKey(),
  projectId: text("project_id").notNull().references(() => projects.id),
  sessionId: text("session_id").references(() => sessions.id),
  logType: text("log_type").notNull(), // log | warning | error | exception
  message: text("message").notNull(),
  stackTrace: text("stack_trace"),
  isCompilationError: boolean("is_compilation_error").default(false),
  acknowledged: boolean("acknowledged").default(false),
  createdAt: timestamp("created_at").defaultNow(),
});
