"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import SearchBar from "@/components/search/SearchBar";
import AnimalGrid from "@/components/animals/AnimalGrid";
import type { AnimalCard } from "@/lib/types";

function SearchContent() {
  const searchParams = useSearchParams();
  const query = searchParams.get("q") || "";
  const searchType = searchParams.get("type") || "text";
  const results: AnimalCard[] = []; // TODO: fetch from API

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-text mb-6">Search</h1>

      <div className="max-w-2xl mb-8">
        <SearchBar initialQuery={query} size="lg" />
      </div>

      {query && (
        <>
          <div className="flex items-center gap-4 mb-6">
            <span className="text-sm text-text-muted">
              {results.length} results for &quot;{query}&quot;
            </span>
            <div className="flex gap-2">
              <button
                className={`px-3 py-1 rounded-full text-sm ${
                  searchType === "text"
                    ? "bg-primary text-white"
                    : "bg-gray-100 text-text-muted hover:bg-gray-200"
                }`}
              >
                Keyword
              </button>
              <button
                className={`px-3 py-1 rounded-full text-sm ${
                  searchType === "semantic"
                    ? "bg-primary text-white"
                    : "bg-gray-100 text-text-muted hover:bg-gray-200"
                }`}
              >
                Smart Search
              </button>
            </div>
          </div>

          <AnimalGrid animals={results} />
        </>
      )}

      {!query && (
        <div className="text-center py-12">
          <span className="text-5xl">🔍</span>
          <h2 className="mt-4 text-lg font-medium text-text">Start searching</h2>
          <p className="mt-1 text-text-muted">
            Search for any animal by name, habitat, diet, or characteristics.
          </p>
        </div>
      )}
    </div>
  );
}

export default function SearchPage() {
  return (
    <Suspense>
      <SearchContent />
    </Suspense>
  );
}
