"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

interface SearchBarProps {
  initialQuery?: string;
  size?: "sm" | "lg";
  className?: string;
}

export default function SearchBar({ initialQuery = "", size = "sm", className = "" }: SearchBarProps) {
  const [query, setQuery] = useState(initialQuery);
  const router = useRouter();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim()) {
      router.push(`/search?q=${encodeURIComponent(query.trim())}`);
    }
  };

  return (
    <form onSubmit={handleSubmit} className={className}>
      <div className="relative">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search animals..."
          className={`w-full rounded-full border border-gray-300 bg-surface focus:ring-2 focus:ring-primary focus:border-primary transition-colors ${
            size === "lg" ? "px-6 py-4 text-lg" : "px-4 py-2 text-sm"
          }`}
        />
        <button
          type="submit"
          className={`absolute right-1 top-1/2 -translate-y-1/2 bg-primary text-white rounded-full hover:bg-primary-dark transition-colors ${
            size === "lg" ? "p-3" : "p-2"
          }`}
        >
          <svg xmlns="http://www.w3.org/2000/svg" className={size === "lg" ? "h-5 w-5" : "h-4 w-4"} fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </button>
      </div>
    </form>
  );
}
