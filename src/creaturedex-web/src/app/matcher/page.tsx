"use client";

import { useState } from "react";
import MatcherFlow from "@/components/matcher/MatcherFlow";
import MatcherResults from "@/components/matcher/MatcherResults";
import type { MatcherAnswers } from "@/components/matcher/MatcherFlow";
import type { MatcherRecommendation } from "@/lib/types";

type MatcherState = "intro" | "questions" | "loading" | "results";

export default function MatcherPage() {
  const [state, setState] = useState<MatcherState>("intro");
  const [results, setResults] = useState<MatcherRecommendation[]>([]);

  const handleComplete = async (answers: MatcherAnswers) => {
    setState("loading");

    // TODO: Call POST /api/matcher with answers
    // For now, simulate a delay and show empty results
    await new Promise((resolve) => setTimeout(resolve, 2000));
    setResults([]);
    setState("results");
  };

  const handleReset = () => {
    setState("intro");
    setResults([]);
  };

  if (state === "intro") {
    return (
      <div className="max-w-2xl mx-auto px-4 py-16 text-center">
        <span className="text-6xl">🐾</span>
        <h1 className="text-3xl font-bold text-text mt-6">Find Your Perfect Pet</h1>
        <p className="text-lg text-text-muted mt-3 max-w-lg mx-auto">
          Answer a few quick questions about your lifestyle and preferences, and
          our AI will recommend the best pets for you.
        </p>
        <button
          onClick={() => setState("questions")}
          className="mt-8 px-8 py-3 bg-primary text-white text-lg rounded-full hover:bg-primary-dark transition-colors"
        >
          Get Started
        </button>
      </div>
    );
  }

  if (state === "loading") {
    return (
      <div className="max-w-2xl mx-auto px-4 py-16 text-center">
        <div className="animate-bounce text-6xl">🔍</div>
        <h2 className="text-xl font-bold text-text mt-6">Finding your perfect match...</h2>
        <p className="text-text-muted mt-2">Our AI is analysing your preferences</p>
      </div>
    );
  }

  if (state === "results") {
    return (
      <div className="px-4 py-8">
        <MatcherResults recommendations={results} onReset={handleReset} />
      </div>
    );
  }

  return (
    <div className="px-4 py-8">
      <MatcherFlow onComplete={handleComplete} />
    </div>
  );
}
