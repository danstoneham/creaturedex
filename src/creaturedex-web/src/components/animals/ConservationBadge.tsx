interface ConservationBadgeProps {
  status: string;
}

const statusColors: Record<string, string> = {
  "Least Concern": "bg-green-900/60 text-green-300",
  "Near Threatened": "bg-yellow-900/30 text-yellow-300",
  "Vulnerable": "bg-orange-900/40 text-orange-300",
  "Endangered": "bg-red-900/40 text-red-300",
  "Critically Endangered": "bg-red-900/60 text-red-200",
  "Extinct in the Wild": "bg-gray-800 text-gray-300",
  "Extinct": "bg-gray-700 text-gray-200",
  "Data Deficient": "bg-blue-900/40 text-blue-300",
};

export default function ConservationBadge({ status }: ConservationBadgeProps) {
  const colorClass = statusColors[status] || "bg-gray-800 text-gray-300";

  return (
    <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${colorClass}`}>
      {status}
    </span>
  );
}
