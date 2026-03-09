interface SkeletonProps {
  className?: string;
}

export default function Skeleton({ className = "" }: SkeletonProps) {
  return (
    <div className={`animate-pulse bg-[#E8DFD3] rounded ${className}`} />
  );
}
