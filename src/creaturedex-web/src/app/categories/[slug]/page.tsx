"use client";

import { use } from "react";
import AnimalGrid from "@/components/animals/AnimalGrid";
import type { AnimalCard } from "@/lib/types";

// TODO: Fetch from API
const categoryInfo: Record<string, { name: string; description: string; icon: string }> = {
  dogs: { name: "Dogs", description: "Domestic dog breeds and working dogs", icon: "🐕" },
  cats: { name: "Cats", description: "Domestic cat breeds", icon: "🐱" },
  "small-mammals": { name: "Small Mammals", description: "Rabbits, hamsters, guinea pigs, and more", icon: "🐰" },
  reptiles: { name: "Reptiles & Amphibians", description: "Lizards, snakes, turtles, frogs, and more", icon: "🦎" },
  birds: { name: "Birds", description: "Parrots, finches, birds of prey, and more", icon: "🐦" },
  fish: { name: "Fish & Aquatic", description: "Freshwater, saltwater, and aquarium fish", icon: "🐠" },
  insects: { name: "Insects & Arachnids", description: "Beetles, butterflies, spiders, and more", icon: "🦋" },
  farm: { name: "Farm Animals", description: "Horses, goats, chickens, and livestock", icon: "🐴" },
  "wild-mammals": { name: "Wild Mammals", description: "Lions, elephants, wolves, bears, and more", icon: "🦁" },
  ocean: { name: "Ocean Life", description: "Whales, dolphins, sharks, and sea creatures", icon: "🐋" },
  primates: { name: "Primates", description: "Monkeys, apes, and lemurs", icon: "🐵" },
};

export default function CategoryPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);
  const category = categoryInfo[slug];
  const animals: AnimalCard[] = []; // TODO: fetch from API

  if (!category) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-12 text-center">
        <h1 className="text-2xl font-bold">Category not found</h1>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <span className="text-4xl">{category.icon}</span>
          <h1 className="text-3xl font-bold text-text">{category.name}</h1>
        </div>
        <p className="text-text-muted">{category.description}</p>
      </div>

      <AnimalGrid animals={animals} loading={false} />
    </div>
  );
}
