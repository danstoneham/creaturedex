import Link from "next/link";
import Card from "@/components/ui/Card";
import Badge from "@/components/ui/Badge";
import PawRating from "@/components/ui/PawRating";
import type { AnimalCard as AnimalCardType } from "@/lib/types";

interface AnimalCardProps {
  animal: AnimalCardType;
}

export default function AnimalCard({ animal }: AnimalCardProps) {
  return (
    <Link href={`/animals/${animal.slug}`}>
      <Card hover>
        {animal.imageUrl ? (
          <div className="aspect-[4/3] overflow-hidden">
            <img
              src={animal.imageUrl}
              alt={animal.commonName}
              className="w-full h-full object-cover"
            />
          </div>
        ) : (
          <div className="aspect-[4/3] bg-gradient-to-br from-primary-light/20 to-primary/10 flex items-center justify-center">
            <span className="text-5xl">🐾</span>
          </div>
        )}
        <div className="p-4">
          <h3 className="font-semibold text-text truncate">{animal.commonName}</h3>
          {animal.scientificName && (
            <p className="text-sm text-text-muted italic truncate">{animal.scientificName}</p>
          )}
          <p className="text-sm text-text-muted mt-2 line-clamp-2">{animal.summary}</p>
          <div className="flex items-center gap-2 mt-3 flex-wrap">
            {!animal.isPublished && (
              <Badge variant="warning">Draft</Badge>
            )}
            <Badge variant="primary">{animal.categoryName}</Badge>
            {animal.isPet && <Badge variant="secondary">Pet</Badge>}
            {animal.conservationStatus && (
              <Badge variant={animal.conservationStatus === "Least Concern" ? "success" : "warning"}>
                {animal.conservationStatus}
              </Badge>
            )}
          </div>
          {animal.isPet && animal.difficultyRating != null && (
            <div className="mt-2">
              <PawRating rating={animal.difficultyRating} size="sm" />
            </div>
          )}
        </div>
      </Card>
    </Link>
  );
}
