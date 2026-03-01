interface CardProps {
  children: React.ReactNode;
  className?: string;
  hover?: boolean;
}

export default function Card({ children, className = "", hover = false }: CardProps) {
  return (
    <div
      className={`bg-surface rounded-xl border border-gray-200 overflow-hidden ${
        hover ? "transition-shadow hover:shadow-lg cursor-pointer" : ""
      } ${className}`}
    >
      {children}
    </div>
  );
}
