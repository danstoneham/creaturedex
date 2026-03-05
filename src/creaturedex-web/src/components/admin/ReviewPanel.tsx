"use client";

import type { ReviewSuggestion } from "@/lib/types";

function FormatValue({ value, className }: { value: string; className: string }) {
  // Try to parse as JSON array (for funFacts)
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

interface ReviewPanelProps {
  suggestions: ReviewSuggestion[];
  onAccept: (suggestion: ReviewSuggestion) => void;
  onDismiss: (index: number) => void;
  onClose: () => void;
}

export default function ReviewPanel({ suggestions, onAccept, onDismiss, onClose }: ReviewPanelProps) {
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

  return (
    <div className="bg-blue-900/40 border border-blue-700 rounded-xl p-4 mb-6 space-y-3">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-semibold text-blue-300">
          AI Review — {suggestions.length} suggestion{suggestions.length !== 1 ? "s" : ""}
        </h3>
        <button onClick={onClose} className="text-blue-400 hover:text-blue-300 text-sm">
          Close
        </button>
      </div>
      {suggestions.map((s, i) => (
        <div key={i} className="bg-gray-900 rounded-lg border border-blue-800 p-3">
          <div className="flex items-start gap-2 mb-2">
            <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${
              s.severity === "warning"
                ? "bg-amber-900/40 text-amber-300"
                : "bg-blue-900/60 text-blue-300"
            }`}>
              {s.severity === "warning" ? "Warning" : "Info"}
            </span>
            <span className="text-xs font-medium text-gray-400 uppercase">{s.field}</span>
          </div>
          <p className="text-sm text-gray-300 mb-2">{s.message}</p>
          {s.currentValue && (
            <div className="text-sm bg-red-900/20 border border-red-900 rounded p-2 mb-2">
              <p className="text-xs text-gray-400 mb-1">Current:</p>
              <FormatValue value={s.currentValue} className="text-gray-400 line-through" />
            </div>
          )}
          {s.suggestedValue && (
            <div className="text-sm bg-green-900/40 border border-green-800 rounded p-2 mb-2">
              <p className="text-xs text-gray-400 mb-1">Suggested:</p>
              <FormatValue value={s.suggestedValue} className="text-gray-200" />
            </div>
          )}
          <div className="flex gap-2">
            {s.suggestedValue && (
              <button
                onClick={() => onAccept(s)}
                className="text-xs bg-green-900/60 text-green-300 px-2 py-1 rounded hover:bg-green-900/80"
              >
                Accept
              </button>
            )}
            <button
              onClick={() => onDismiss(i)}
              className="text-xs bg-gray-800 text-gray-400 px-2 py-1 rounded hover:bg-gray-700"
            >
              Dismiss
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
