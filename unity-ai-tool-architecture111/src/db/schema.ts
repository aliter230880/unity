import {
  pgTable,
  text,
  integer,
  boolean,
  timestamp,
  bigint,
  jsonb,
  uuid,
  serial,
} from "drizzle-orm/pg-core";

// ─── Projects ────────────────────────────────────────────────────────────────
export const projects = pgTable("projects", {
  id: uuid("id").primaryKey().defaultRandom(),
  name: text("name").notNull(),
  unityVersion: text("unity_version").default(""),
  apiKey: text("api_key").notNull().unique(),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// ─── Project Files (full index from Unity) ───────────────────────────────────
export const projectFiles = pgTable("project_files", {
  id: serial("id").primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  path: text("path").notNull(),       // relative: Assets/Scripts/Player.cs
  type: text("type").notNull(),       // script | scene | prefab | material | shader | config | other
  sizeBytes: bigint("size_bytes", { mode: "number" }).default(0),
  content: text("content").default(""),  // full text for text files
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// ─── Chat Sessions ────────────────────────────────────────────────────────────
export const sessions = pgTable("sessions", {
  id: uuid("id").primaryKey().defaultRandom(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  title: text("title").default("New Session"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  updatedAt: timestamp("updated_at").defaultNow().notNull(),
});

// ─── Messages ─────────────────────────────────────────────────────────────────
export const messages = pgTable("messages", {
  id: uuid("id").primaryKey().defaultRandom(),
  sessionId: uuid("session_id")
    .references(() => sessions.id, { onDelete: "cascade" })
    .notNull(),
  role: text("role").notNull(), // user | assistant | tool
  content: text("content").notNull(),
  toolCalls: jsonb("tool_calls"),    // OpenAI tool_calls array
  toolCallId: text("tool_call_id"), // for role=tool responses
  toolName: text("tool_name"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
});

// ─── Pending Commands (Unity polls this) ─────────────────────────────────────
export const pendingCommands = pgTable("pending_commands", {
  id: uuid("id").primaryKey().defaultRandom(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  sessionId: uuid("session_id"),
  type: text("type").notNull(), // write_file | create_gameobject | add_component | execute_editor_command | delete_file
  payload: jsonb("payload").notNull(),
  status: text("status").default("pending"), // pending | executing | done | error
  result: text("result"),
  createdAt: timestamp("created_at").defaultNow().notNull(),
  executedAt: timestamp("executed_at"),
});

// ─── Console Logs (from Unity) ────────────────────────────────────────────────
export const consoleLogs = pgTable("console_logs", {
  id: serial("id").primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  logType: text("log_type").notNull(), // log | warning | error | exception | compiler_error
  message: text("message").notNull(),
  stackTrace: text("stack_trace").default(""),
  createdAt: timestamp("created_at").defaultNow().notNull(),
});

// ─── Scene State (live Unity scene snapshot) ──────────────────────────────────
export const sceneSnapshots = pgTable("scene_snapshots", {
  id: serial("id").primaryKey(),
  projectId: uuid("project_id")
    .references(() => projects.id, { onDelete: "cascade" })
    .notNull(),
  sceneName: text("scene_name").notNull(),
  hierarchy: text("hierarchy").default(""),   // text tree of objects
  createdAt: timestamp("created_at").defaultNow().notNull(),
});
