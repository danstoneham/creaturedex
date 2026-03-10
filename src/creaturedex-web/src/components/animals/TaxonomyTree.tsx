import type { Taxonomy } from "@/lib/types";

interface TaxonomyTreeProps {
  taxonomy: Taxonomy;
}

const ranks = [
  { key: "kingdom", label: "Kingdom" },
  { key: "phylum", label: "Phylum" },
  { key: "class", label: "Class" },
  { key: "taxOrder", label: "Order" },
  { key: "family", label: "Family" },
  { key: "genus", label: "Genus" },
  { key: "species", label: "Species" },
  { key: "subspecies", label: "Subspecies" },
] as const;

export default function TaxonomyTree({ taxonomy }: TaxonomyTreeProps) {
  // Parse synonyms from JSON string if present
  let synonyms: string[] = [];
  try {
    if (taxonomy.synonyms) {
      synonyms = JSON.parse(taxonomy.synonyms);
    }
  } catch {
    // Not valid JSON, ignore
  }

  return (
    <div className="space-y-1">
      {ranks.map(({ key, label }, i) => {
        const value = taxonomy[key as keyof Taxonomy];
        if (!value) return null;

        const isSpecies = key === "species";

        return (
          <div key={key} className="flex items-center" style={{ paddingLeft: `${i * 16}px` }}>
            <span className="text-primary mr-2">&rsaquo;</span>
            <span className="text-xs font-medium text-text-muted uppercase w-20">{label}</span>
            <span className="text-sm text-text italic">{value as string}</span>
            {isSpecies && taxonomy.authorship && (
              <span className="text-sm text-text-muted italic ml-1.5">
                ({taxonomy.authorship})
              </span>
            )}
          </div>
        );
      })}
      {synonyms.length > 0 && (
        <div className="mt-3 pt-3 border-t border-[#D4C4B0]" style={{ paddingLeft: "16px" }}>
          <span className="text-xs font-medium text-text-muted uppercase">Synonyms</span>
          <div className="mt-1 space-y-0.5">
            {synonyms.map((syn, i) => (
              <p key={i} className="text-sm text-text-muted italic">{syn}</p>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
