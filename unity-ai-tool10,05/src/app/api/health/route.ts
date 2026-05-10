import { db } from "@/db";
import { sql } from "drizzle-orm";
import { NextResponse } from "next/server";

export async function GET() {
  await db.execute(sql`select 1`);
  return NextResponse.json({ ok: true });
}
