"use client";

import { useState } from "react";
import type { ReviewSuggestion } from "@/lib/types";

const FIELD_LABELS: Record<string, string> = {
  commonName: "Common Name",
  scientificName: "Scientific Name",
  summary: "Summary",
  description: "Description",
  habitat: "Habitat",
  diet: "Diet",
  lifespan: "Lifespan",
  sizeInfo: "Size Info",
  behaviour: "Behaviour",
  nativeRegion: "Native Region",
  conservationStatus: "Conservation Status",
  funFacts: "Fun Facts",
  general: "General",
};

function fieldLabel(field: string) {
  return FIELD_LABELS[field] || field;
}

function FormatValue({ value, className }: { value: string; className: string }) {
  try {
    const parsed = JSON.parse(value);
    if (Array.isArray(parsed)) {
      return (
        <ul className={`list-disc list-inside space-y-1 ${className}`}>
          {parsed.map((item, i) => (
            <li key={i}>{String(item)}</li>
          ))}
        </ul>
      );
    }
  } catch {
    // Not JSON, render as text
  }
  return <p className={`whitespace-pre-line ${className}`}>{value}</p>;
}

function SuggestionCard({
  suggestion,
  index,
  onAccept,
  onDismiss,
}: {
  suggestion: ReviewSuggestion;
  index: number;
  onAccept: (suggestion: ReviewSuggestion) => void;
  onDismiss: (index: number) => void;
}) {
  return (
    <div className="bg-gray-900 rounded-lg border border-blue-800 p-3">
      <div className="flex items-start gap-2 mb-2">
        <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${
          suggestion.severity === "warning"
            ? "bg-amber-900/40 text-amber-300"
            : "bg-blue-900/60 text-blue-300"
        }`}>
          {suggestion.severity === "warning" ? "Warning" : "Info"}
        </span>
        <span className="text-xs font-medium text-gray-400 uppercase">{suggestion.field}</span>
      </div>
      <p className="text-sm text-gray-300 mb-2">{suggestion.message}</p>
      {suggestion.currentValue && (
        <div className="text-sm bg-red-900/20 border border-red-900 rounded p-2 mb-2">
          <p className="text-xs text-gray-400 mb-1">Current:</p>
          <FormatValue value={suggestion.currentValue} className="text-gray-400 line-through" />
        </div>
      )}
      {suggestion.suggestedValue && (
        <div className="text-sm bg-green-900/40 border border-green-800 rounded p-2 mb-2">
          <p className="text-xs text-gray-400 mb-1">Suggested:</p>
          <FormatValue value={suggestion.suggestedValue} className="text-gray-200" />
        </div>
      )}
      <div className="flex gap-2">
        {suggestion.suggestedValue && (
          <button
            onClick={() => onAccept(suggestion)}
            className="text-xs bg-green-900/60 text-green-300 px-2 py-1 rounded hover:bg-green-900/80"
          >
            Accept
          </button>
        )}
        <button
          onClick={() => onDismiss(index)}
          className="text-xs bg-gray-800 text-gray-400 px-2 py-1 rounded hover:bg-gray-700"
        >
          Dismiss
        </button>
      </div>
    </div>
  );
}

interface ReviewPanelProps {
  suggestions: ReviewSuggestion[];
  onAccept: (suggestion: ReviewSuggestion) => void;
  onDismiss: (index: number) => void;
  onClose: () => void;
}

export default function ReviewPanel({ suggestions, onAccept, onDismiss, onClose }: ReviewPanelProps) {
  // Group suggestions by field, preserving original indices for onDismiss
  const grouped = new Map<string, { suggestion: ReviewSuggestion; originalIndex: number }[]>();
  suggestions.forEach((s, i) => {
    const key = s.field;
    if (!grouped.has(key)) grouped.set(key, []);
    grouped.get(key)!.push({ suggestion: s, originalIndex: i });
  });

  const fields = Array.from(grouped.keys());
  const [activeTab, setActiveTab] = useState(fields[0] || "");

  if (suggestions.length === 0) {
    return (
      <div className="bg-green-900/40 border border-green-700 rounded-xl p-4 mb-6">
        <div className="flex items-center justify-between">
          <p className="text-sm text-green-300 font-medium">
            No suggestions — content looks good!
          </p>
          <button onClick={onClose} className="text-green-400 hover:text-green-300 text-sm">
            Dismiss
          </button>
        </div>
      </div>
    );
  }

  const hasWarning = (items: { suggestion: ReviewSuggestion }[]) =>
    items.some((item) => item.suggestion.severity === "warning");

  const activeSuggestions = grouped.get(activeTab) || [];

  return (
    <div className="bg-blue-900/40 border border-blue-700 rounded-xl p-4 mb-6">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-blue-300">
          AI Review — {suggestions.length} suggestion{suggestions.length !== 1 ? "s" : ""}
        </h3>
        <button onClick={onClose} className="text-blue-400 hover:text-blue-300 text-sm">
          Close
        </button>
      </div>

      {/* Tabs */}
      <div className="border-b border-blue-800 mb-3">
        <nav className="flex gap-1 -mb-px flex-wrap" aria-label="Review tabs">
          {fields.map((field) => {
            const items = grouped.get(field)!;
            const warn = hasWarning(items);
            return (
              <button
                key={field}
                onClick={() => setActiveTab(field)}
                className={`px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
                  activeTab === field
                    ? "border-primary text-primary"
                    : "border-transparent text-text-muted hover:text-primary hover:border-gray-600"
                }`}
              >
                {fieldLabel(field)}
                <span className={`ml-1.5 px-1.5 py-0.5 rounded-full text-[10px] ${
                  warn
                    ? "bg-amber-900/40 text-amber-300"
                    : "bg-blue-900/60 text-blue-300"
                }`}>
                  {items.length}
                </span>
              </button>
            );
          })}
        </nav>
      </div>

      {/* Suggestion cards */}
      <div className="space-y-3">
        {activeSuggestions.map(({ suggestion, originalIndex }) => (
          <SuggestionCard
            key={originalIndex}
            suggestion={suggestion}
            index={originalIndex}
            onAccept={onAccept}
            onDismiss={onDismiss}
          />
        ))}
      </div>
    </div>
  );
}
