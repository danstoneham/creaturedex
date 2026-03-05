import AnimalCard from "./AnimalCard";
import Skeleton from "@/components/ui/Skeleton";
import type { AnimalCard as AnimalCardType } from "@/lib/types";

interface AnimalGridProps {
  animals: AnimalCardType[];
  loading?: boolean;
}

export default function AnimalGrid({ animals, loading }: AnimalGridProps) {
  if (loading) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
        {Array.from({ length: 12 }, (_, i) => (
          <div key={i} className="rounded-xl border border-gray-700 overflow-hidden">
            <Skeleton className="aspect-[4/3]" />
            <div className="p-4 space-y-2">
              <Skeleton className="h-5 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
              <Skeleton className="h-10 w-full" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (animals.length === 0) {
    return (
      <div className="text-center py-12">
        <span className="text-4xl">🔍</span>
        <h3 className="mt-4 text-lg font-medium text-text">No animals found</h3>
        <p className="mt-1 text-text-muted">Try adjusting your filters or search terms.</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
      {animals.map((animal) => (
        <AnimalCard key={animal.id} animal={animal} />
      ))}
    </div>
  );
}
