import type { PetCareGuide } from "@/lib/types";

interface CareSectionProps {
  careGuide: PetCareGuide;
}

export default function CareSection({ careGuide }: CareSectionProps) {
  const sections = [
    { title: "Housing", content: careGuide.housing, icon: "\ud83c\udfe0" },
    { title: "Diet", content: careGuide.dietAsPet, icon: "\ud83c\udf7d\ufe0f" },
    { title: "Exercise", content: careGuide.exercise, icon: "\ud83c\udfc3" },
    { title: "Grooming", content: careGuide.grooming, icon: "\u2702\ufe0f" },
    { title: "Health Concerns", content: careGuide.healthConcerns, icon: "\ud83c\udfe5" },
    { title: "Training", content: careGuide.training, icon: "\ud83c\udf93" },
    { title: "Temperament", content: careGuide.temperament, icon: "\ud83d\udc9b" },
    { title: "Legal Considerations", content: careGuide.legalConsiderations, icon: "\u2696\ufe0f" },
  ].filter((s) => s.content);

  return (
    <div className="space-y-6">
      {/* Quick compatibility */}
      <div className="flex gap-4 flex-wrap">
        {careGuide.goodWithChildren != null && (
          <div className="flex items-center gap-2 text-sm">
            <span>{careGuide.goodWithChildren ? "\u2705" : "\u274c"}</span>
            <span>Good with children</span>
          </div>
        )}
        {careGuide.goodWithOtherPets != null && (
          <div className="flex items-center gap-2 text-sm">
            <span>{careGuide.goodWithOtherPets ? "\u2705" : "\u274c"}</span>
            <span>Good with other pets</span>
          </div>
        )}
        {careGuide.spaceRequirement && (
          <div className="flex items-center gap-2 text-sm">
            <span>\ud83d\udcd0</span>
            <span>{careGuide.spaceRequirement}</span>
          </div>
        )}
      </div>

      {/* Care sections */}
      {sections.map((section) => (
        <div key={section.title}>
          <h4 className="font-semibold text-text flex items-center gap-2 mb-2">
            <span>{section.icon}</span>
            {section.title}
          </h4>
          <p className="text-text-muted leading-relaxed whitespace-pre-line">{section.content}</p>
        </div>
      ))}
    </div>
  );
}
