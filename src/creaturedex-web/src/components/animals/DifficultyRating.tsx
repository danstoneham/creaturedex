import PawRating from "@/components/ui/PawRating";

interface DifficultyRatingProps {
  rating: number;
}

const labels = ["Very Easy", "Easy", "Moderate", "Challenging", "Expert Only"];

export default function DifficultyRating({ rating }: DifficultyRatingProps) {
  return (
    <div className="flex items-center gap-2">
      <PawRating rating={rating} />
      <span className="text-sm text-text-muted">{labels[rating - 1] || ""}</span>
    </div>
  );
}
