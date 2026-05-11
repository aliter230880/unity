import { db } from "@/db";
import { projects } from "@/db/schema";
import { eq } from "drizzle-orm";
import { NextRequest } from "next/server";

export async function getProjectByApiKey(req: NextRequest) {
  const apiKey =
    req.headers.get("x-api-key") ||
    req.nextUrl.searchParams.get("api_key") ||
    "";
  if (!apiKey) return null;
  const [project] = await db
    .select()
    .from(projects)
    .where(eq(projects.apiKey, apiKey))
    .limit(1);
  return project ?? null;
}
