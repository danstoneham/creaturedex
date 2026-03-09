"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navLinks = [
  { href: "/", label: "Home" },
  { href: "/animals", label: "Browse Animals" },
  { href: "/matcher", label: "Pet Matcher" },
  { href: "/search", label: "Search" },
];

interface MobileNavProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function MobileNav({ isOpen, onClose }: MobileNavProps) {
  const pathname = usePathname();

  if (!isOpen) return null;

  return (
    <div className="md:hidden border-t border-[#3D2A1D] bg-surface">
      <nav className="px-4 py-3 space-y-1">
        {navLinks.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            onClick={onClose}
            className={`block px-3 py-2 rounded-md text-sm font-medium transition-colors ${
              pathname === link.href
                ? "bg-primary/10 text-primary"
                : "text-[#C4B5A4] hover:bg-[#3D2A1D] hover:text-primary"
            }`}
          >
            {link.label}
          </Link>
        ))}
      </nav>
    </div>
  );
}
