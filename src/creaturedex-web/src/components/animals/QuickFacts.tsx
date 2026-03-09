import Card from "@/components/ui/Card";
import PawRating from "@/components/ui/PawRating";
import type { Animal, PetCareGuide, AnimalCharacteristic } from "@/lib/types";

interface QuickFactsProps {
  animal: Animal;
  careGuide: PetCareGuide | null;
  characteristics: AnimalCharacteristic[];
}

export default function QuickFacts({ animal, careGuide, characteristics }: QuickFactsProps) {
  const facts = [
    { label: "Lifespan", value: animal.lifespan },
    { label: "Native Region", value: animal.nativeRegion },
    { label: "Diet", value: animal.diet ? (animal.diet.length > 80 ? animal.diet.substring(0, 80) + "..." : animal.diet) : null },
    ...characteristics.map((c) => ({ label: c.characteristicName, value: c.characteristicValue })),
  ].filter((f) => f.value);

  return (
    <Card className="p-5">
      <h3 className="font-bold text-text mb-4">Quick Facts</h3>
      <dl className="space-y-3">
        {facts.map((fact, i) => (
          <div key={i}>
            <dt className="text-xs font-medium text-text-muted uppercase tracking-wide">{fact.label}</dt>
            <dd className="text-sm text-text mt-0.5">{fact.value}</dd>
          </div>
        ))}
      </dl>

      {careGuide && (
        <div className="mt-6 pt-4 border-t border-[#E8DFD3]">
          <h4 className="font-semibold text-sm text-text mb-3">Pet Suitability</h4>
          <div className="space-y-3">
            <div>
              <span className="text-xs text-text-muted uppercase tracking-wide">Difficulty</span>
              <PawRating rating={careGuide.difficultyRating} size="sm" />
            </div>
            {careGuide.costRangeMin != null && careGuide.costRangeMax != null && (
              <div>
                <dt className="text-xs text-text-muted uppercase tracking-wide">Annual Cost</dt>
                <dd className="text-sm text-text">
                  {careGuide.costCurrency === "GBP" ? "\u00a3" : "$"}
                  {careGuide.costRangeMin} – {careGuide.costCurrency === "GBP" ? "\u00a3" : "$"}
                  {careGuide.costRangeMax}
                </dd>
              </div>
            )}
            {careGuide.timeCommitment && (
              <div>
                <dt className="text-xs text-text-muted uppercase tracking-wide">Time Needed</dt>
                <dd className="text-sm text-text">{careGuide.timeCommitment}</dd>
              </div>
            )}
          </div>
        </div>
      )}
    </Card>
  );
}
