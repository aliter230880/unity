import { NextResponse } from 'next/server';
import { db } from '@/db';
import { messages } from '@/db/schema';
import { eq, desc } from 'drizzle-orm';

export async function GET(
  req: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id: projectId } = await params;
    const projectMessages = await db.query.messages.findMany({
      where: eq(messages.projectId, projectId),
      orderBy: [desc(messages.createdAt)],
    });
    return NextResponse.json(projectMessages);
  } catch (error) {
    console.error('Get Messages Error:', error);
    return NextResponse.json({ error: 'Internal Server Error' }, { status: 500 });
  }
}
