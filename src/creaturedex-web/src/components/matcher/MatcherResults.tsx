import Link from "next/link";
import type { MatcherRecommendation } from "@/lib/types";

interface MatcherResultsProps {
  recommendations: MatcherRecommendation[];
  onReset: () => void;
}

export default function MatcherResults({ recommendations, onReset }: MatcherResultsProps) {
  if (recommendations.length === 0) {
    return (
      <div className="text-center py-12">
        <span className="text-5xl">🤔</span>
        <h2 className="mt-4 text-xl font-bold text-text">No matches found</h2>
        <p className="mt-2 text-text-muted">Try adjusting your preferences for better results.</p>
        <button
          onClick={onReset}
          className="mt-4 px-6 py-2 bg-primary text-white rounded-lg hover:bg-primary-dark transition-colors"
        >
          Try Again
        </button>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto">
      <div className="text-center mb-8">
        <h2 className="text-2xl font-bold text-text">Your Perfect Matches!</h2>
        <p className="text-text-muted mt-1">Based on your preferences, we recommend these pets:</p>
      </div>

      <div className="space-y-6">
        {recommendations.map((rec, i) => (
          <div key={rec.animal.id} className="bg-surface rounded-xl border border-gray-700 p-6">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold">
                #{i + 1}
              </div>
              <div className="flex-1">
                <div className="flex items-center justify-between">
                  <Link href={`/animals/${rec.animal.slug}`} className="text-xl font-bold text-primary hover:underline">
                    {rec.animal.commonName}
                  </Link>
                  <span className="text-sm font-medium text-secondary">
                    {rec.matchScore}% match
                  </span>
                </div>
                <p className="mt-2 text-text-muted">{rec.explanation}</p>
                <Link
                  href={`/animals/${rec.animal.slug}`}
                  className="inline-block mt-3 text-sm text-primary hover:underline"
                >
                  View full profile →
                </Link>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="text-center mt-8">
        <button
          onClick={onReset}
          className="px-6 py-2 bg-gray-800 text-text rounded-lg hover:bg-gray-700 transition-colors"
        >
          Try Again
        </button>
      </div>
    </div>
  );
}
