interface CardProps {
  children: React.ReactNode;
  className?: string;
  hover?: boolean;
}

export default function Card({ children, className = "", hover = false }: CardProps) {
  return (
    <div
      className={`bg-surface-light rounded-xl border border-[#E8DFD3] overflow-hidden shadow-sm ${
        hover ? "transition-shadow hover:shadow-md cursor-pointer" : ""
      } ${className}`}
    >
      {children}
    </div>
  );
}
