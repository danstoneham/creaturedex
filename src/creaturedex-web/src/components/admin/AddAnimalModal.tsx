"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";

interface AddAnimalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function AddAnimalModal({ isOpen, onClose }: AddAnimalModalProps) {
  const [animalName, setAnimalName] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const router = useRouter();

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!animalName.trim()) return;

    setError("");
    setLoading(true);
    try {
      const result = await api.admin.generateAnimal(animalName.trim());
      router.push(`/animals/${result.slug}`);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate animal");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 px-4">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-text mb-4">Add New Animal</h2>
        <p className="text-sm text-text-muted mb-4">
          Enter the animal name and AI will generate all the content. You can edit it afterwards.
        </p>
        <form onSubmit={handleSubmit}>
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 mb-4 text-sm text-red-800">
              {error}
            </div>
          )}
          <input
            type="text"
            value={animalName}
            onChange={(e) => setAnimalName(e.target.value)}
            placeholder="e.g. Red Fox, Emperor Penguin..."
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm mb-4 focus:ring-primary focus:border-primary"
            disabled={loading}
            autoFocus
          />
          {loading && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-4 text-sm text-blue-800">
              Generating {animalName}... This may take a minute.
            </div>
          )}
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2 rounded-lg text-sm text-text-muted hover:bg-gray-100"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !animalName.trim()}
              className="bg-primary text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
            >
              {loading ? "Generating..." : "Generate with AI"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
