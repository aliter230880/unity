import { db } from "@/db";
import { projects, sessions } from "@/db/schema";
import { eq, desc } from "drizzle-orm";
import { UnityDashboard } from "@/components/UnityDashboard";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  const allProjects = await db
    .select()
    .from(projects)
    .orderBy(desc(projects.createdAt));

  // Get first session for each project
  const projectsWithSessions = await Promise.all(
    allProjects.map(async (p) => {
      const [firstSession] = await db
        .select()
        .from(sessions)
        .where(eq(sessions.projectId, p.id))
        .orderBy(desc(sessions.createdAt))
        .limit(1);
      return { ...p, defaultSessionId: firstSession?.id ?? null };
    })
  );

  return <UnityDashboard initialProjects={projectsWithSessions} />;
}
