import { NextResponse } from 'next/server';
import { db } from '@/db';

export async function GET() {
  try {
    const projects = await db.query.projects.findMany();
    return NextResponse.json(projects);
  } catch (error) {
    console.error('Get Projects Error:', error);
    return NextResponse.json({ error: 'Internal Server Error' }, { status: 500 });
  }
}
