"use client";

import { useState } from "react";

interface MatcherFlowProps {
  onComplete: (answers: MatcherAnswers) => void;
}

export interface MatcherAnswers {
  livingSpace: string;
  experienceLevel: string;
  timeAvailable: string;
  budgetRange: string;
  hasChildren: boolean;
  hasOtherPets: boolean;
  preferences: string[];
}

const steps = [
  {
    id: "livingSpace",
    title: "What's your living situation?",
    options: [
      { value: "small-flat", label: "Small flat/apartment", icon: "🏢" },
      { value: "large-flat", label: "Large flat/apartment", icon: "🏬" },
      { value: "house-no-garden", label: "House without garden", icon: "🏠" },
      { value: "house-small-garden", label: "House with small garden", icon: "🏡" },
      { value: "house-large-garden", label: "House with large garden", icon: "🌳" },
      { value: "rural", label: "Rural/farm property", icon: "🌾" },
    ],
  },
  {
    id: "experienceLevel",
    title: "What's your pet experience?",
    options: [
      { value: "first-time", label: "First-time pet owner", icon: "🌱" },
      { value: "some", label: "Some experience", icon: "🌿" },
      { value: "experienced", label: "Experienced owner", icon: "🌲" },
      { value: "expert", label: "Expert/professional", icon: "🏆" },
    ],
  },
  {
    id: "timeAvailable",
    title: "How much daily time can you dedicate?",
    options: [
      { value: "minimal", label: "Under 30 minutes", icon: "⏱️" },
      { value: "moderate", label: "30 minutes to 1 hour", icon: "🕐" },
      { value: "good", label: "1-2 hours", icon: "🕑" },
      { value: "plenty", label: "2+ hours", icon: "🕒" },
    ],
  },
  {
    id: "budgetRange",
    title: "What's your budget range?",
    options: [
      { value: "low", label: "Budget-friendly (under £500/year)", icon: "💷" },
      { value: "medium", label: "Moderate (£500-£1500/year)", icon: "💰" },
      { value: "high", label: "Comfortable (£1500-£3000/year)", icon: "💎" },
      { value: "very-high", label: "No budget concerns", icon: "🌟" },
    ],
  },
  {
    id: "hasChildren",
    title: "Do you have children at home?",
    options: [
      { value: "yes", label: "Yes", icon: "👨‍👩‍👧‍👦" },
      { value: "no", label: "No", icon: "🏠" },
    ],
  },
  {
    id: "hasOtherPets",
    title: "Do you have other pets?",
    options: [
      { value: "yes", label: "Yes", icon: "🐾" },
      { value: "no", label: "No", icon: "1️⃣" },
    ],
  },
  {
    id: "preferences",
    title: "What matters most to you? (Pick up to 3)",
    multiSelect: true,
    options: [
      { value: "low-maintenance", label: "Low maintenance", icon: "😌" },
      { value: "cuddly", label: "Cuddly & affectionate", icon: "🤗" },
      { value: "active", label: "Active & playful", icon: "⚡" },
      { value: "quiet", label: "Quiet", icon: "🤫" },
      { value: "long-lived", label: "Long lifespan", icon: "⏳" },
      { value: "unique", label: "Unique & unusual", icon: "✨" },
      { value: "trainable", label: "Trainable", icon: "🎯" },
      { value: "hypoallergenic", label: "Hypoallergenic", icon: "🌿" },
    ],
  },
];

export default function MatcherFlow({ onComplete }: MatcherFlowProps) {
  const [currentStep, setCurrentStep] = useState(0);
  const [answers, setAnswers] = useState<Record<string, string | string[] | boolean>>({});

  const step = steps[currentStep];
  const isMultiSelect = "multiSelect" in step && step.multiSelect;
  const progress = ((currentStep + 1) / steps.length) * 100;

  const handleSelect = (value: string) => {
    if (step.id === "hasChildren" || step.id === "hasOtherPets") {
      setAnswers({ ...answers, [step.id]: value === "yes" });
      goNext({ ...answers, [step.id]: value === "yes" });
    } else if (isMultiSelect) {
      const current = (answers[step.id] as string[]) || [];
      if (current.includes(value)) {
        setAnswers({ ...answers, [step.id]: current.filter((v) => v !== value) });
      } else if (current.length < 3) {
        setAnswers({ ...answers, [step.id]: [...current, value] });
      }
    } else {
      setAnswers({ ...answers, [step.id]: value });
      goNext({ ...answers, [step.id]: value });
    }
  };

  const goNext = (currentAnswers: Record<string, string | string[] | boolean>) => {
    if (currentStep < steps.length - 1) {
      setCurrentStep(currentStep + 1);
    } else {
      onComplete({
        livingSpace: currentAnswers.livingSpace as string,
        experienceLevel: currentAnswers.experienceLevel as string,
        timeAvailable: currentAnswers.timeAvailable as string,
        budgetRange: currentAnswers.budgetRange as string,
        hasChildren: currentAnswers.hasChildren as boolean,
        hasOtherPets: currentAnswers.hasOtherPets as boolean,
        preferences: (currentAnswers.preferences as string[]) || [],
      });
    }
  };

  const selectedMulti = (answers[step.id] as string[]) || [];

  return (
    <div className="max-w-2xl mx-auto">
      {/* Progress bar */}
      <div className="mb-8">
        <div className="flex justify-between text-sm text-text-muted mb-2">
          <span>Step {currentStep + 1} of {steps.length}</span>
          <span>{Math.round(progress)}%</span>
        </div>
        <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
          <div
            className="h-full bg-primary rounded-full transition-all duration-300"
            style={{ width: `${progress}%` }}
          />
        </div>
      </div>

      {/* Question */}
      <h2 className="text-2xl font-bold text-text mb-6">{step.title}</h2>

      {/* Options */}
      <div className={`grid gap-3 ${step.options.length <= 3 ? "grid-cols-1 sm:grid-cols-2" : "grid-cols-1 sm:grid-cols-2"}`}>
        {step.options.map((option) => {
          const isSelected = isMultiSelect
            ? selectedMulti.includes(option.value)
            : answers[step.id] === option.value;

          return (
            <button
              key={option.value}
              onClick={() => handleSelect(option.value)}
              className={`flex items-center gap-3 p-4 rounded-xl border-2 text-left transition-all ${
                isSelected
                  ? "border-primary bg-primary/5"
                  : "border-gray-200 hover:border-primary-light"
              }`}
            >
              <span className="text-2xl">{option.icon}</span>
              <span className="font-medium text-text">{option.label}</span>
            </button>
          );
        })}
      </div>

      {/* Navigation */}
      <div className="flex justify-between mt-8">
        <button
          onClick={() => setCurrentStep(Math.max(0, currentStep - 1))}
          className={`px-4 py-2 text-sm rounded-lg ${
            currentStep === 0 ? "invisible" : "text-text-muted hover:text-primary"
          }`}
        >
          Back
        </button>

        {isMultiSelect && (
          <button
            onClick={() => goNext(answers)}
            disabled={selectedMulti.length === 0}
            className="px-6 py-2 bg-primary text-white rounded-lg hover:bg-primary-dark disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {currentStep === steps.length - 1 ? "Find My Match" : "Continue"}
          </button>
        )}
      </div>
    </div>
  );
}
