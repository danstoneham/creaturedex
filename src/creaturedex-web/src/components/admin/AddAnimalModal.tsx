"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { api, type ApiError } from "@/lib/api";

interface AddAnimalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function AddAnimalModal({ isOpen, onClose }: AddAnimalModalProps) {
  const [animalName, setAnimalName] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [duplicateSlug, setDuplicateSlug] = useState<string | null>(null);
  const router = useRouter();

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!animalName.trim()) return;

    setError("");
    setDuplicateSlug(null);
    setLoading(true);
    try {
      const result = await api.admin.generateAnimal(animalName.trim());
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
      setLoading(false);
    }
  };

  const handleReset = () => {
    setAnimalName("");
    setError("");
    setDuplicateSlug(null);
  };

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 px-4" onClick={onClose}>
      <div
        className="bg-surface border border-[#3D2A1D] rounded-xl shadow-2xl w-full max-w-md p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-lg font-bold text-[#F5EDE3] mb-2">Add New Animal</h2>
        <p className="text-sm text-text-muted mb-4">
          Enter the animal name and AI will generate all the content. You can edit it afterwards.
        </p>
        <form onSubmit={handleSubmit}>
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
                    onClick={handleReset}
                    className="text-text-muted hover:text-[#F5EDE3] font-medium"
                  >
                    Try another name
                  </button>
                </div>
              )}
            </div>
          )}
          <input
            type="text"
            value={animalName}
            onChange={(e) => { setAnimalName(e.target.value); setError(""); setDuplicateSlug(null); }}
            placeholder="e.g. Red Fox, Emperor Penguin..."
            className="w-full rounded-lg border border-[#5C3D2E] bg-[#3D2A1D] text-[#F5EDE3] placeholder-[#8B7355] px-3 py-2 text-sm mb-4 focus:ring-2 focus:ring-primary focus:border-primary outline-none"
            disabled={loading}
            autoFocus
          />
          {loading && (
            <div className="bg-primary/10 border border-primary/30 rounded-lg p-3 mb-4 text-sm text-primary flex items-center gap-2">
              <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              Generating {animalName}... This may take a minute.
            </div>
          )}
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2 rounded-lg text-sm text-text-muted hover:text-[#F5EDE3] hover:bg-[#3D2A1D] transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !animalName.trim()}
              className="bg-primary text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {loading ? "Generating..." : "Generate with AI"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
