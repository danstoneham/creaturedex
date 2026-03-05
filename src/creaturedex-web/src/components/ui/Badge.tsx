interface BadgeProps {
  children: React.ReactNode;
  variant?: "default" | "primary" | "secondary" | "success" | "warning" | "danger";
  className?: string;
}

const variantStyles = {
  default: "bg-gray-800 text-text-muted",
  primary: "bg-primary/10 text-primary",
  secondary: "bg-secondary/10 text-secondary-dark",
  success: "bg-green-900/60 text-green-300",
  warning: "bg-yellow-900/30 text-yellow-300",
  danger: "bg-red-900/40 text-red-300",
};

export default function Badge({ children, variant = "default", className = "" }: BadgeProps) {
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${variantStyles[variant]} ${className}`}>
      {children}
    </span>
  );
}
