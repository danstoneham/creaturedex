interface ConservationBadgeProps {
  status: string;
}

const statusColors: Record<string, string> = {
  "Least Concern": "bg-green-100 text-green-800",
  "Near Threatened": "bg-yellow-100 text-yellow-800",
  "Vulnerable": "bg-orange-100 text-orange-800",
  "Endangered": "bg-red-100 text-red-800",
  "Critically Endangered": "bg-red-200 text-red-900",
  "Extinct in the Wild": "bg-gray-200 text-gray-800",
  "Extinct": "bg-gray-300 text-gray-900",
  "Data Deficient": "bg-blue-100 text-blue-800",
};

export default function ConservationBadge({ status }: ConservationBadgeProps) {
  const colorClass = statusColors[status] || "bg-gray-100 text-gray-800";

  return (
    <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${colorClass}`}>
      {status}
    </span>
  );
}
