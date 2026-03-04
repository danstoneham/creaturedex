"use client";

import type { ReviewSuggestion } from "@/lib/types";

interface ReviewPanelProps {
  suggestions: ReviewSuggestion[];
  onAccept: (suggestion: ReviewSuggestion) => void;
  onDismiss: (index: number) => void;
  onClose: () => void;
}

export default function ReviewPanel({ suggestions, onAccept, onDismiss, onClose }: ReviewPanelProps) {
  if (suggestions.length === 0) {
    return (
      <div className="bg-green-50 border border-green-200 rounded-xl p-4 mb-6">
        <div className="flex items-center justify-between">
          <p className="text-sm text-green-800 font-medium">
            No suggestions — content looks good!
          </p>
          <button onClick={onClose} className="text-green-600 hover:text-green-800 text-sm">
            Dismiss
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 mb-6 space-y-3">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-semibold text-blue-900">
          AI Review — {suggestions.length} suggestion{suggestions.length !== 1 ? "s" : ""}
        </h3>
        <button onClick={onClose} className="text-blue-600 hover:text-blue-800 text-sm">
          Close
        </button>
      </div>
      {suggestions.map((s, i) => (
        <div key={i} className="bg-white rounded-lg border border-blue-100 p-3">
          <div className="flex items-start gap-2 mb-2">
            <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${
              s.severity === "warning"
                ? "bg-amber-100 text-amber-800"
                : "bg-blue-100 text-blue-800"
            }`}>
              {s.severity === "warning" ? "Warning" : "Info"}
            </span>
            <span className="text-xs font-medium text-gray-500 uppercase">{s.field}</span>
          </div>
          <p className="text-sm text-gray-700 mb-2">{s.message}</p>
          {s.suggestedValue && (
            <div className="text-sm bg-green-50 border border-green-100 rounded p-2 mb-2">
              <p className="text-xs text-gray-500 mb-1">Suggested:</p>
              <p className="text-gray-800 whitespace-pre-line">{s.suggestedValue}</p>
            </div>
          )}
          <div className="flex gap-2">
            {s.suggestedValue && (
              <button
                onClick={() => onAccept(s)}
                className="text-xs bg-green-100 text-green-800 px-2 py-1 rounded hover:bg-green-200"
              >
                Accept
              </button>
            )}
            <button
              onClick={() => onDismiss(i)}
              className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded hover:bg-gray-200"
            >
              Dismiss
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
