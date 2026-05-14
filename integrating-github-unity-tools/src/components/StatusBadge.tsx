interface Props {
  status: 'disconnected' | 'connecting' | 'connected' | 'error';
}

const config = {
  disconnected: { label: 'Disconnected', dot: 'bg-gray-400', text: 'text-gray-600', bg: 'bg-gray-100' },
  connecting: { label: 'Connecting...', dot: 'bg-yellow-400 animate-pulse', text: 'text-yellow-700', bg: 'bg-yellow-50' },
  connected: { label: 'Connected', dot: 'bg-green-500 animate-pulse', text: 'text-green-700', bg: 'bg-green-50' },
  error: { label: 'Error', dot: 'bg-red-500', text: 'text-red-700', bg: 'bg-red-50' },
};

export default function StatusBadge({ status }: Props) {
  const c = config[status];
  return (
    <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ${c.bg} ${c.text}`}>
      <span className={`w-2 h-2 rounded-full ${c.dot}`} />
      {c.label}
    </span>
  );
}
