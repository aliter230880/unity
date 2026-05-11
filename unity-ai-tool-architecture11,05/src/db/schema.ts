import {
  pgTable,
  text,
  integer,
  boolean,
  timestamp,
  bigint,
  jsonb,
  serial,
} from "drizzle-orm/pg-core";

// ── Projects ──────────────────────────────────────────────────────────
export const projects = pgTable("projects", {
  id: text("id").primaryKey(),
  name: text("name").notNull(),
  unityVersion: text("unity_version").default(""),
  apiKey: text("api_key").notNull().unique(),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
  lastSyncAt: timestamp("last_sync_at"),
  fileCount: integer("file_count").default(0),
  activeScene: text("active_scene").default(""),
  sceneHierarchy: text("scene_hierarchy").default(""),
});

// ── Project Files (full file map) ─────────────────────────────────────
export const projectFiles = pgTable("project_files", {
  id: serial("id").primaryKey(),
  projectId: text("project_id")
    .notNull()
    .references(() => projects.id, { onDelete: "cascade" }),
  path: text("path").notNull(),
  fileType: text("file_type").default("other"),
  sizeBytes: bigint("size_bytes", { mode: "number" }).default(0),
  content: text("content").default(""),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// ── Chat Sessions ─────────────────────────────────────────────────────
export const sessions = pgTable("sessions", {
  id: text("id").primaryKey(),
  projectId: text("project_id")
    .notNull()
    .references(() => projects.id, { onDelete: "cascade" }),
  title: text("title").default("New Session"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// ── Messages ──────────────────────────────────────────────────────────
export const messages = pgTable("messages", {
  id: serial("id").primaryKey(),
  sessionId: text("session_id")
    .notNull()
    .references(() => sessions.id, { onDelete: "cascade" }),
  role: text("role").notNull(), // user | assistant | tool
  content: text("content").default(""),
  toolCalls: jsonb("tool_calls"),
  toolCallId: text("tool_call_id"),
  toolName: text("tool_name"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
});

// ── Pending Commands (Unity polls this) ───────────────────────────────
export const pendingCommands = pgTable("pending_commands", {
  id: text("id").primaryKey(),
  projectId: text("project_id")
    .notNull()
    .references(() => projects.id, { onDelete: "cascade" }),
  type: text("type").notNull(),
  payload: jsonb("payload").notNull(),
  status: text("status").default("pending"), // pending | done | error
  result: text("result").default(""),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  executedAt: timestamp("executed_at"),
});

// ── Console Logs (from Unity) ─────────────────────────────────────────
export const consoleLogs = pgTable("console_logs", {
  id: serial("id").primaryKey(),
  projectId: text("project_id")
    .notNull()
    .references(() => projects.id, { onDelete: "cascade" }),
  logType: text("log_type").default("log"), // log | warning | error | exception
  message: text("message").notNull(),
  stackTrace: text("stack_trace").default(""),
  createdAt: timestamp("created_at").defaultNow().notNull(),
});
