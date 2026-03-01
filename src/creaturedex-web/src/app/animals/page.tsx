"use client";

import { useState } from "react";
import AnimalGrid from "@/components/animals/AnimalGrid";
import type { AnimalCard } from "@/lib/types";

const categories = [
  { name: "All", slug: "" },
  { name: "Dogs", slug: "dogs" },
  { name: "Cats", slug: "cats" },
  { name: "Small Mammals", slug: "small-mammals" },
  { name: "Reptiles", slug: "reptiles" },
  { name: "Birds", slug: "birds" },
  { name: "Fish", slug: "fish" },
  { name: "Insects", slug: "insects" },
  { name: "Farm Animals", slug: "farm" },
  { name: "Wild Mammals", slug: "wild-mammals" },
  { name: "Ocean Life", slug: "ocean" },
  { name: "Primates", slug: "primates" },
];

export default function BrowsePage() {
  const [selectedCategory, setSelectedCategory] = useState("");
  const [petsOnly, setPetsOnly] = useState(false);
  const [sortBy, setSortBy] = useState("name");
  const [animals] = useState<AnimalCard[]>([]); // TODO: fetch from API

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-text mb-6">Browse Animals</h1>

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Filter sidebar */}
        <aside className="lg:w-64 flex-shrink-0">
          <div className="bg-surface rounded-xl border border-gray-200 p-4 space-y-6 lg:sticky lg:top-24">
            {/* Categories */}
            <div>
              <h3 className="font-semibold text-sm text-text mb-3">Category</h3>
              <div className="space-y-1">
                {categories.map((cat) => (
                  <button
                    key={cat.slug}
                    onClick={() => setSelectedCategory(cat.slug)}
                    className={`block w-full text-left px-3 py-1.5 rounded text-sm transition-colors ${
                      selectedCategory === cat.slug
                        ? "bg-primary/10 text-primary font-medium"
                        : "text-text-muted hover:bg-gray-100"
                    }`}
                  >
                    {cat.name}
                  </button>
                ))}
              </div>
            </div>

            {/* Pets only toggle */}
            <div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={petsOnly}
                  onChange={(e) => setPetsOnly(e.target.checked)}
                  className="rounded border-gray-300 text-primary focus:ring-primary"
                />
                <span className="text-sm text-text">Pets only</span>
              </label>
            </div>

            {/* Sort */}
            <div>
              <h3 className="font-semibold text-sm text-text mb-2">Sort by</h3>
              <select
                value={sortBy}
                onChange={(e) => setSortBy(e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:ring-primary focus:border-primary"
              >
                <option value="name">A-Z</option>
                <option value="newest">Newest</option>
              </select>
            </div>
          </div>
        </aside>

        {/* Animal grid */}
        <div className="flex-1">
          <AnimalGrid animals={animals} loading={false} />
        </div>
      </div>
    </div>
  );
}
