"use client";

import { use } from "react";
import Link from "next/link";
import Badge from "@/components/ui/Badge";
import Tabs from "@/components/ui/Tabs";
import QuickFacts from "@/components/animals/QuickFacts";
import TaxonomyTree from "@/components/animals/TaxonomyTree";
import CareSection from "@/components/animals/CareSection";
import ConservationBadge from "@/components/animals/ConservationBadge";
import DifficultyRating from "@/components/animals/DifficultyRating";
import type { AnimalProfile } from "@/lib/types";

// TODO: Replace with API fetch
const mockProfile: AnimalProfile | null = null;

export default function AnimalProfilePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);

  // TODO: Fetch from /api/animals/{slug}
  const profile = mockProfile;

  if (!profile) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-12 text-center">
        <span className="text-6xl">{"\ud83d\udd0d"}</span>
        <h1 className="text-2xl font-bold mt-4">Animal not found</h1>
        <p className="text-text-muted mt-2">
          We couldn&apos;t find &quot;{slug}&quot;. It may not be in our database yet.
        </p>
        <Link href="/animals" className="inline-block mt-4 text-primary hover:underline">
          Browse all animals
        </Link>
      </div>
    );
  }

  const { animal, taxonomy, careGuide, characteristics, tags, categoryName, categorySlug, isReviewed } = profile;

  // Parse fun facts from JSON string
  let funFacts: string[] = [];
  try {
    if (animal.funFacts) {
      funFacts = JSON.parse(animal.funFacts);
    }
  } catch {
    // Not valid JSON, treat as plain text
  }

  const contentTabs = [
    {
      id: "overview",
      label: "Overview",
      content: (
        <div className="prose max-w-none">
          <p className="text-text leading-relaxed whitespace-pre-line">{animal.description}</p>
        </div>
      ),
    },
    ...(taxonomy
      ? [
          {
            id: "classification",
            label: "Classification",
            content: <TaxonomyTree taxonomy={taxonomy} />,
          },
        ]
      : []),
    ...(animal.habitat
      ? [
          {
            id: "habitat",
            label: "Habitat",
            content: <p className="text-text leading-relaxed whitespace-pre-line">{animal.habitat}</p>,
          },
        ]
      : []),
    ...(animal.behaviour
      ? [
          {
            id: "behaviour",
            label: "Behaviour",
            content: <p className="text-text leading-relaxed whitespace-pre-line">{animal.behaviour}</p>,
          },
        ]
      : []),
    ...(funFacts.length > 0
      ? [
          {
            id: "fun-facts",
            label: "Fun Facts",
            content: (
              <ul className="space-y-3">
                {funFacts.map((fact, i) => (
                  <li key={i} className="flex items-start gap-3">
                    <span className="text-secondary text-lg">{"\u2728"}</span>
                    <span className="text-text">{fact}</span>
                  </li>
                ))}
              </ul>
            ),
          },
        ]
      : []),
    ...(careGuide
      ? [
          {
            id: "pet-care",
            label: "Keeping as a Pet",
            content: <CareSection careGuide={careGuide} />,
          },
        ]
      : []),
  ];

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Disclaimer for unreviewed content */}
      {!isReviewed && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3 mb-6 text-sm text-yellow-800">
          This content was AI-generated and has not yet been reviewed by a human. Information may contain inaccuracies.
        </div>
      )}

      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-text-muted mb-6">
        <Link href="/" className="hover:text-primary">Home</Link>
        <span>&rsaquo;</span>
        <Link href={`/categories/${categorySlug}`} className="hover:text-primary">{categoryName}</Link>
        <span>&rsaquo;</span>
        <span className="text-text">{animal.commonName}</span>
      </nav>

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Main content */}
        <div className="flex-1">
          {/* Header */}
          <div className="mb-6">
            <h1 className="text-3xl font-bold text-text">{animal.commonName}</h1>
            {animal.scientificName && (
              <p className="text-lg text-text-muted italic mt-1">{animal.scientificName}</p>
            )}
            <div className="flex items-center gap-2 mt-3 flex-wrap">
              <Badge variant="primary">{categoryName}</Badge>
              {animal.isPet && <Badge variant="secondary">Pet</Badge>}
              {animal.conservationStatus && (
                <ConservationBadge status={animal.conservationStatus} />
              )}
              {animal.isPet && careGuide && (
                <DifficultyRating rating={careGuide.difficultyRating} />
              )}
            </div>
            {tags.length > 0 && (
              <div className="flex gap-1.5 mt-3 flex-wrap">
                {tags.map((tag) => (
                  <Badge key={tag}>{tag}</Badge>
                ))}
              </div>
            )}
          </div>

          {/* Image placeholder */}
          <div className="aspect-video bg-gradient-to-br from-primary-light/20 to-primary/10 rounded-xl flex items-center justify-center mb-8">
            <span className="text-8xl">{"\ud83d\udc3e"}</span>
          </div>

          {/* Summary */}
          <p className="text-lg text-text leading-relaxed mb-8">{animal.summary}</p>

          {/* Tabbed content */}
          <Tabs tabs={contentTabs} />
        </div>

        {/* Sidebar */}
        <aside className="lg:w-80 flex-shrink-0">
          <div className="lg:sticky lg:top-24">
            <QuickFacts animal={animal} careGuide={careGuide} characteristics={characteristics} />
          </div>
        </aside>
      </div>
    </div>
  );
}
