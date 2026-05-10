import ProjectManager from '@/components/ProjectManager';

export default async function Dashboard() {
  return (
    <div className="p-8">
      <h1 className="text-3xl font-bold mb-8">Unity AI Bridge Dashboard</h1>
      <ProjectManager />
    </div>
  );
}
