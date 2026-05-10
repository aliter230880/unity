import { pgTable, uuid, text, timestamp, jsonb, boolean, integer } from "drizzle-orm/pg-core";

// Projects — each Unity project gets its own API key
export const projects = pgTable("projects", {
  id: uuid("id").defaultRandom().primaryKey(),
  name: text("name").notNull(),
  apiKey: text("api_key").notNull().unique(),
  createdAt: timestamp("created_at").defaultNow().notNull(),
});

// Chat sessions
export const sessions = pgTable("sessions", {
  id: uuid("id").defaultRandom().primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  title: text("title").default("New Session"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// Messages with tool calls
export const messages = pgTable("messages", {
  id: uuid("id").defaultRandom().primaryKey(),
  sessionId: uuid("session_id")
    .references(() => sessions.id, { onDelete: "cascade" })
    .notNull(),
  role: text("role").notNull(), // "user" | "assistant" | "tool" | "system"
  content: text("content"),
  toolCalls: jsonb("tool_calls"), // OpenAI tool_calls array
  toolCallId: text("tool_call_id"), // For tool response messages
  createdAt: timestamp("created_at").defaultNow().notNull(),
});

// Project file index — the "Project Map"
export const projectFiles = pgTable("project_files", {
  id: uuid("id").defaultRandom().primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  filePath: text("file_path").notNull(),
  fileType: text("file_type").notNull(), // "script" | "shader" | "scene" | "prefab" | "other"
  content: text("content"), // Cached content (optional, for hot files)
  lastSynced: timestamp("last_synced").defaultNow().notNull(),
});

// Pending commands queue for Unity
export const pendingCommands = pgTable("pending_commands", {
  id: uuid("id").defaultRandom().primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  commandType: text("command_type").notNull(), // "create_script" | "modify_script" | "set_property" | etc.
  payload: jsonb("payload").notNull(), // Command-specific data
  status: text("status").default("pending").notNull(), // "pending" | "sent" | "completed" | "failed"
  result: jsonb("result"), // Unity's response
  createdAt: timestamp("created_at").defaultNow().notNull(),
  completedAt: timestamp("completed_at"),
});

// Console logs from Unity
export const consoleLogs = pgTable("console_logs", {
  id: uuid("id").defaultRandom().primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  logType: text("log_type").notNull(), // "log" | "warning" | "error"
  message: text("message").notNull(),
  stackTrace: text("stack_trace"),
  timestamp: timestamp("timestamp").defaultNow().notNull(),
});
