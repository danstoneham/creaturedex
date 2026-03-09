interface PawRatingProps {
  rating: number;
  max?: number;
  size?: "sm" | "md" | "lg";
}

const sizeClasses = {
  sm: "text-sm",
  md: "text-base",
  lg: "text-xl",
};

export default function PawRating({ rating, max = 5, size = "md" }: PawRatingProps) {
  return (
    <div className={`flex gap-0.5 ${sizeClasses[size]}`} aria-label={`Difficulty: ${rating} out of ${max}`}>
      {Array.from({ length: max }, (_, i) => (
        <span key={i} className={i < rating ? "text-secondary" : "text-[#D4C4B0]"}>
          🐾
        </span>
      ))}
    </div>
  );
}
