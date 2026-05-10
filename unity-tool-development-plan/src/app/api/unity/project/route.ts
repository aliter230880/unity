import { NextResponse } from 'next/server';
import { db } from '@/db';
import { projects } from '@/db/schema';
import { eq } from 'drizzle-orm';

export async function POST(req: Request) {
  try {
    const { name } = await req.json();

    if (!name) {
      return NextResponse.json({ error: 'Project name is required' }, { status: 400 });
    }

    // Check if project already exists
    const existingProject = await db.query.projects.findFirst({
      where: eq(projects.name, name),
    });

    if (existingProject) {
      return NextResponse.json({ projectId: existingProject.id });
    }

    // Create new project
    const [newProject] = await db.insert(projects).values({ name }).returning();
    return NextResponse.json({ projectId: newProject.id });
  } catch (error) {
    console.error('Project API Error:', error);
    return NextResponse.json({ error: 'Internal Server Error' }, { status: 500 });
  }
}
