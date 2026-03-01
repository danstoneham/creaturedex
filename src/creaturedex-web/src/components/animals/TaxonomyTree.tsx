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
  return (
    <div className="space-y-1">
      {ranks.map(({ key, label }, i) => {
        const value = taxonomy[key as keyof Taxonomy];
        if (!value || key === "id") return null;

        return (
          <div key={key} className="flex items-center" style={{ paddingLeft: `${i * 16}px` }}>
            <span className="text-primary mr-2">&rsaquo;</span>
            <span className="text-xs font-medium text-text-muted uppercase w-20">{label}</span>
            <span className="text-sm text-text italic">{value as string}</span>
          </div>
        );
      })}
    </div>
  );
}
