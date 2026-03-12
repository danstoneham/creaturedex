"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { api, type ApiError } from "@/lib/api";
import type { SpeciesSuggestion } from "@/lib/types";

interface AddAnimalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

type Phase = "search" | "generating";

function Spinner({ className = "h-4 w-4" }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  );
}

export default function AddAnimalModal({ isOpen, onClose }: AddAnimalModalProps) {
  const [animalName, setAnimalName] = useState("");
  const [phase, setPhase] = useState<Phase>("search");
  const [suggestions, setSuggestions] = useState<SpeciesSuggestion[]>([]);
  const [searching, setSearching] = useState(false);
  const [searched, setSearched] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [generatingName, setGeneratingName] = useState("");
  const [error, setError] = useState("");
  const [duplicateSlug, setDuplicateSlug] = useState<string | null>(null);
  const router = useRouter();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setAnimalName("");
      setPhase("search");
      setSuggestions([]);
      setSearching(false);
      setSearched(false);
      setGenerating(false);
      setGeneratingName("");
      setError("");
      setDuplicateSlug(null);
    }
  }, [isOpen]);

  const searchSpecies = useCallback(async (query: string) => {
    if (!query.trim()) {
      setSuggestions([]);
      setSearched(false);
      return;
    }
    setSearching(true);
    setError("");
    try {
      const results = await api.admin.searchSpecies(query.trim());
      setSuggestions(results);
      setSearched(true);
    } catch {
      setSuggestions([]);
      setSearched(true);
    } finally {
      setSearching(false);
    }
  }, []);

  // Debounced auto-search
  useEffect(() => {
    if (phase !== "search") return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (!animalName.trim() || animalName.trim().length < 3) {
      setSuggestions([]);
      setSearched(false);
      return;
    }
    debounceRef.current = setTimeout(() => {
      searchSpecies(animalName);
    }, 400);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [animalName, phase, searchSpecies]);

  const handleGenerate = async (name: string, taxonKey?: number, scientificName?: string) => {
    setPhase("generating");
    setGeneratingName(name);
    setGenerating(true);
    setError("");
    setDuplicateSlug(null);
    try {
      const result = await api.admin.generateAnimal(name, taxonKey, scientificName);
      router.push(`/animals/${result.slug}`);
      onClose();
    } catch (err) {
      const apiErr = err as ApiError;
      if (apiErr.status === 409 && apiErr.body?.slug) {
        setError(String(apiErr.body.error));
        setDuplicateSlug(String(apiErr.body.slug));
      } else {
        setError(err instanceof Error ? err.message : "Failed to generate animal");
        setDuplicateSlug(null);
      }
    } finally {
      setGenerating(false);
    }
  };

  const handleSuggestionClick = (suggestion: SpeciesSuggestion) => {
    const displayName = suggestion.commonName || suggestion.scientificName;
    handleGenerate(displayName, suggestion.taxonKey, suggestion.scientificName);
  };

  const handleFallbackGenerate = () => {
    if (!animalName.trim()) return;
    handleGenerate(animalName.trim());
  };

  const handleBackToSearch = () => {
    setPhase("search");
    setError("");
    setDuplicateSlug(null);
    setGenerating(false);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (debounceRef.current) clearTimeout(debounceRef.current);
    searchSpecies(animalName);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 px-4" onClick={onClose}>
      <div
        className="bg-surface border border-[#3D2A1D] rounded-xl shadow-2xl w-full max-w-md p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-lg font-bold text-[#F5EDE3] mb-2">Add New Animal</h2>
        <p className="text-sm text-text-muted mb-4">
          Search for a species and AI will generate all the content. You can edit it afterwards.
        </p>

        {/* Error display */}
        {error && (
          <div className="bg-red-900/40 border border-red-700 rounded-lg p-3 mb-4 text-sm text-red-300">
            <p>{error}</p>
            {duplicateSlug && (
              <div className="mt-2 flex gap-2">
                <Link
                  href={`/animals/${duplicateSlug}`}
                  className="inline-flex items-center gap-1 text-primary hover:text-primary-light font-medium"
                  onClick={onClose}
                >
                  View existing animal &rarr;
                </Link>
                <span className="text-[#5C3D2E]">|</span>
                <button
                  type="button"
                  onClick={handleBackToSearch}
                  className="text-text-muted hover:text-[#F5EDE3] font-medium"
                >
                  Try another name
                </button>
              </div>
            )}
          </div>
        )}

        {/* Phase: Search */}
        {phase === "search" && (
          <>
            <form onSubmit={handleSearchSubmit}>
              <div className="relative mb-3">
                <input
                  type="text"
                  value={animalName}
                  onChange={(e) => setAnimalName(e.target.value)}
                  placeholder="e.g. Bengal Tiger, Red Fox..."
                  className="w-full rounded-lg border border-[#5C3D2E] bg-[#3D2A1D] text-[#F5EDE3] placeholder-[#8B7355] px-3 py-2 text-sm pr-8 focus:ring-2 focus:ring-primary focus:border-primary outline-none"
                  autoFocus
                />
                {searching && (
                  <div className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[#8B7355]">
                    <Spinner className="h-4 w-4" />
                  </div>
                )}
              </div>
            </form>

            {/* Suggestions list */}
            {searched && suggestions.length > 0 && (
              <ul className="max-h-64 overflow-y-auto rounded-lg border border-[#3D2A1D] mb-3 divide-y divide-[#3D2A1D]">
                {suggestions.map((s) => (
                  <li key={s.taxonKey}>
                    <button
                      type="button"
                      onClick={() => handleSuggestionClick(s)}
                      className="w-full text-left px-3 py-2.5 hover:bg-[#3D2A1D] transition-colors group"
                    >
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-[#F5EDE3] group-hover:text-primary transition-colors">
                          {s.commonName || s.scientificName}
                        </span>
                        {s.status === "ACCEPTED" && (
                          <span className="text-[10px] font-medium bg-[#2D6A4F]/20 text-[#2D6A4F] border border-[#2D6A4F]/30 px-1.5 py-0.5 rounded-full leading-none">
                            ACCEPTED
                          </span>
                        )}
                        {s.status && s.status !== "ACCEPTED" && (
                          <span className="text-[10px] font-medium text-[#8B7355] bg-[#8B7355]/10 border border-[#8B7355]/20 px-1.5 py-0.5 rounded-full leading-none">
                            {s.status}
                          </span>
                        )}
                      </div>
                      {s.commonName && (
                        <p className="text-xs italic text-[#8B7355] mt-0.5">{s.scientificName}</p>
                      )}
                      {(s.family || s.order) && (
                        <p className="text-xs text-[#5C3D2E] mt-0.5">
                          {[s.family, s.order].filter(Boolean).join(" · ")}
                        </p>
                      )}
                    </button>
                  </li>
                ))}
              </ul>
            )}

            {/* No results */}
            {searched && !searching && suggestions.length === 0 && animalName.trim() && (
              <div className="text-sm text-[#8B7355] mb-3 px-1">
                No species found for &ldquo;{animalName.trim()}&rdquo;.
              </div>
            )}

            {/* Fallback generate button */}
            {searched && animalName.trim() && (
              <button
                type="button"
                onClick={handleFallbackGenerate}
                className="w-full text-left text-sm text-[#8B7355] hover:text-[#F5EDE3] hover:bg-[#3D2A1D] rounded-lg px-3 py-2 mb-3 transition-colors border border-dashed border-[#5C3D2E]"
              >
                Generate anyway with &ldquo;{animalName.trim()}&rdquo;
              </button>
            )}

            <div className="flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 rounded-lg text-sm text-text-muted hover:text-[#F5EDE3] hover:bg-[#3D2A1D] transition-colors"
              >
                Cancel
              </button>
            </div>
          </>
        )}

        {/* Phase: Generating */}
        {phase === "generating" && (
          <>
            {generating && (
              <div className="bg-primary/10 border border-primary/30 rounded-lg p-3 mb-4 text-sm text-primary flex items-center gap-2">
                <Spinner />
                Generating {generatingName}... This may take a minute.
              </div>
            )}

            <div className="flex gap-2 justify-end">
              {!generating && (
                <button
                  type="button"
                  onClick={handleBackToSearch}
                  className="px-4 py-2 rounded-lg text-sm text-text-muted hover:text-[#F5EDE3] hover:bg-[#3D2A1D] transition-colors"
                >
                  &larr; Back to search
                </button>
              )}
              <button
                type="button"
                onClick={onClose}
                disabled={generating}
                className="px-4 py-2 rounded-lg text-sm text-text-muted hover:text-[#F5EDE3] hover:bg-[#3D2A1D] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Cancel
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
