import { pgTable, text, timestamp, uuid, boolean, integer } from 'drizzle-orm/pg-core';

export const projects = pgTable('projects', {
  id: uuid('id').primaryKey().defaultRandom(),
  name: text('name').notNull(),
  createdAt: timestamp('created_at').defaultNow().notNull(),
});

export const messages = pgTable('messages', {
  id: uuid('id').primaryKey().defaultRandom(),
  projectId: uuid('project_id').references(() => projects.id).notNull(),
  role: text('role').notNull(), // 'user', 'assistant', 'system'
  content: text('content').notNull(),
  isErrorFix: boolean('is_error_fix').default(false).notNull(),
  createdAt: timestamp('created_at').defaultNow().notNull(),
});

export const actions = pgTable('actions', {
  id: uuid('id').primaryKey().defaultRandom(),
  messageId: uuid('message_id').references(() => messages.id),
  projectId: uuid('project_id').references(() => projects.id).notNull(),
  type: text('type').notNull(), // 'CREATE_SCRIPT', 'MODIFY_PROPERTY', 'EXECUTE_COMMAND'
  parameters: text('parameters').notNull(), // JSON string
  status: text('status').default('pending').notNull(), // 'pending', 'success', 'failed'
  createdAt: timestamp('created_at').defaultNow().notNull(),
});

export const settings = pgTable('settings', {
  id: uuid('id').primaryKey().defaultRandom(),
  key: text('key').unique().notNull(),
  value: text('value').notNull(),
  updatedAt: timestamp('updated_at').defaultNow().notNull(),
});
