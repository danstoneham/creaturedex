"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navLinks = [
  { href: "/", label: "Home" },
  { href: "/animals", label: "Browse" },
  { href: "/matcher", label: "Pet Matcher" },
];

export default function Navigation() {
  const pathname = usePathname();

  return (
    <nav className="hidden md:flex items-center gap-6">
      {navLinks.map((link) => (
        <Link
          key={link.href}
          href={link.href}
          className={`text-sm font-medium transition-colors ${
            pathname === link.href
              ? "text-primary"
              : "text-text-muted hover:text-primary"
          }`}
        >
          {link.label}
        </Link>
      ))}
    </nav>
  );
}
