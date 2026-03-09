"use client";

import { useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import Card from "@/components/ui/Card";

// ---------------------------------------------------------------------------
// Static category data — replace with api.categories.getAll() once API is live
// ---------------------------------------------------------------------------
const categories = [
  { name: "Dogs", slug: "dogs", icon: "🐕", description: "Domestic dog breeds and working dogs" },
  { name: "Cats", slug: "cats", icon: "🐱", description: "Domestic cat breeds" },
  { name: "Small Mammals", slug: "small-mammals", icon: "🐰", description: "Rabbits, hamsters, guinea pigs, and more" },
  { name: "Reptiles & Amphibians", slug: "reptiles", icon: "🦎", description: "Lizards, snakes, turtles, frogs" },
  { name: "Birds", slug: "birds", icon: "🐦", description: "Parrots, finches, birds of prey" },
  { name: "Fish & Aquatic", slug: "fish", icon: "🐠", description: "Freshwater, saltwater, and aquarium fish" },
  { name: "Insects & Arachnids", slug: "insects", icon: "🦋", description: "Beetles, butterflies, spiders" },
  { name: "Farm Animals", slug: "farm", icon: "🐴", description: "Horses, goats, chickens, and livestock" },
  { name: "Wild Mammals", slug: "wild-mammals", icon: "🦁", description: "Lions, elephants, wolves, bears" },
  { name: "Ocean Life", slug: "ocean", icon: "🐋", description: "Whales, dolphins, sharks, and sea creatures" },
  { name: "Primates", slug: "primates", icon: "🐵", description: "Monkeys, apes, and lemurs" },
];

// ---------------------------------------------------------------------------
// Design tokens — Safari Sunset theme
// ---------------------------------------------------------------------------
const PRIMARY = "#D4882B";
const PRIMARY_DARK = "#A8611A";
const PRIMARY_LIGHT = "#E8A84C";
const SECONDARY = "#3B7A57";
const SECONDARY_DARK = "#2D6044";
const BG = "#FFF8F0";

export default function HomePage() {
  const router = useRouter();
  const [query, setQuery] = useState("");
  const [loadingRandom, setLoadingRandom] = useState(false);
  const [randomError, setRandomError] = useState<string | null>(null);

  // Search bar submission
  const handleSearch = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      const trimmed = query.trim();
      if (!trimmed) return;
      router.push(`/search?q=${encodeURIComponent(trimmed)}`);
    },
    [query, router],
  );

  // Random animal discovery
  const handleRandom = useCallback(async () => {
    setLoadingRandom(true);
    setRandomError(null);
    try {
      const res = await fetch("/api/animals/random");
      if (!res.ok) throw new Error("Could not fetch a random animal.");
      const animal = await res.json();
      router.push(`/animals/${animal.slug}`);
    } catch {
      setRandomError("Could not load a random animal. Please try again.");
      setLoadingRandom(false);
    }
  }, [router]);

  return (
    <div className="min-h-screen" style={{ backgroundColor: BG, fontFamily: "var(--font-geist-sans, sans-serif)" }}>

      {/* ------------------------------------------------------------------ */}
      {/* HERO                                                                */}
      {/* ------------------------------------------------------------------ */}
      <section
        className="relative overflow-hidden"
        style={{
          background: `linear-gradient(135deg, ${PRIMARY_DARK} 0%, #5C3310 40%, ${PRIMARY} 70%, ${PRIMARY_LIGHT} 100%)`,
        }}
      >
        {/* Decorative texture rings */}
        <div
          aria-hidden
          className="pointer-events-none absolute -top-32 -right-32 w-[480px] h-[480px] rounded-full opacity-10"
          style={{ background: "radial-gradient(circle, #FFE08A 0%, transparent 70%)" }}
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -bottom-24 -left-24 w-[360px] h-[360px] rounded-full opacity-10"
          style={{ background: "radial-gradient(circle, #FFE08A 0%, transparent 70%)" }}
        />
        {/* Subtle grid overlay */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 opacity-[0.04]"
          style={{
            backgroundImage:
              "repeating-linear-gradient(0deg, transparent, transparent 39px, #fff 39px, #fff 40px), repeating-linear-gradient(90deg, transparent, transparent 39px, #fff 39px, #fff 40px)",
          }}
        />

        <div className="relative mx-auto max-w-5xl px-6 py-6 md:py-10 flex flex-col items-center text-center">
          {/* Logo hero */}
          <div className="mb-3 drop-shadow-2xl">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src="/images/logo-wide.png"
              alt="Creaturedex — The Animal Encyclopedia"
              className="w-[320px] md:w-[480px] lg:w-[560px] h-auto"
            />
          </div>

          {/* Tagline */}
          <p
            className="mb-10 max-w-xl text-lg leading-relaxed md:text-xl"
            style={{ color: "rgba(255,255,255,0.88)" }}
          >
            An intelligent encyclopedia of every creature on Earth — from backyard pets to deep-ocean giants, powered by AI.
          </p>

          {/* Search bar */}
          <form
            onSubmit={handleSearch}
            className="w-full max-w-2xl"
            role="search"
          >
            <div
              className="flex items-center rounded-2xl overflow-hidden shadow-2xl"
              style={{ backgroundColor: "#fff" }}
            >
              <span className="pl-5 text-2xl select-none" aria-hidden>
                🔍
              </span>
              <input
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search animals, e.g. &ldquo;golden retriever&rdquo;…"
                className="flex-1 bg-transparent py-4 px-4 text-base outline-none placeholder:text-[#B8A898]"
                style={{ color: "#3D2B1F" }}
                aria-label="Search for animals"
              />
              <button
                type="submit"
                className="m-1.5 shrink-0 rounded-xl px-6 py-3 text-sm font-semibold transition-opacity hover:opacity-90 active:scale-95"
                style={{ backgroundColor: PRIMARY, color: "#fff" }}
              >
                Search
              </button>
            </div>
          </form>

          {/* Quick links */}
          <div className="mt-5 flex flex-wrap justify-center gap-3 text-sm" style={{ color: "rgba(255,255,255,0.7)" }}>
            <span>Try:</span>
            {["Axolotl", "Snow Leopard", "Mantis Shrimp", "Border Collie"].map((term) => (
              <button
                key={term}
                onClick={() => router.push(`/search?q=${encodeURIComponent(term)}`)}
                className="underline underline-offset-2 hover:text-white transition-colors"
                style={{ color: "rgba(255,255,255,0.75)" }}
              >
                {term}
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* STATS BAR                                                           */}
      {/* ------------------------------------------------------------------ */}
      <div
        className="border-b"
        style={{ backgroundColor: "#FFFAF5", borderColor: "#E8DFD3" }}
      >
        <div className="mx-auto max-w-5xl px-6 py-4 flex flex-wrap justify-center gap-8 text-sm font-medium" style={{ color: "#6B5B4E" }}>
          <span className="flex items-center gap-2">
            <span className="text-base">🐾</span>
            <strong style={{ color: PRIMARY }}>500+</strong> animals catalogued
          </span>
          <span className="hidden sm:block" style={{ color: "#D4C9BC" }}>|</span>
          <span className="flex items-center gap-2">
            <span className="text-base">📂</span>
            <strong style={{ color: PRIMARY }}>11</strong> categories
          </span>
          <span className="hidden sm:block" style={{ color: "#D4C9BC" }}>|</span>
          <span className="flex items-center gap-2">
            <span className="text-base">✨</span>
            Powered by AI — always growing
          </span>
        </div>
      </div>

      {/* ------------------------------------------------------------------ */}
      {/* MAIN CONTENT                                                        */}
      {/* ------------------------------------------------------------------ */}
      <main className="mx-auto max-w-5xl px-6 py-16 space-y-20">

        {/* -------------------------------------------------------------- */}
        {/* CATEGORY GRID                                                   */}
        {/* -------------------------------------------------------------- */}
        <section aria-labelledby="categories-heading">
          <div className="mb-8 flex flex-col gap-1">
            <h2
              id="categories-heading"
              className="text-3xl font-bold tracking-tight"
              style={{ color: "#3D2B1F", letterSpacing: "-0.02em" }}
            >
              Explore by Category
            </h2>
            <p style={{ color: "#8B7355" }} className="text-base">
              Dive into the world&apos;s most fascinating animal groups.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
            {categories.map((cat) => (
              <Link
                key={cat.slug}
                href={`/categories/${cat.slug}`}
                className="group block focus:outline-none focus-visible:ring-2 rounded-xl"
                style={{ ["--ring-color" as string]: PRIMARY }}
              >
                <Card hover className="h-full transition-transform group-hover:-translate-y-0.5 group-active:translate-y-0">
                  <div className="p-4 flex flex-col gap-2">
                    <span className="text-3xl leading-none" role="img" aria-label={cat.name}>
                      {cat.icon}
                    </span>
                    <div>
                      <p
                        className="font-semibold text-sm leading-snug"
                        style={{ color: "#3D2B1F" }}
                      >
                        {cat.name}
                      </p>
                      <p
                        className="mt-0.5 text-xs leading-snug line-clamp-2"
                        style={{ color: "#8B7355" }}
                      >
                        {cat.description}
                      </p>
                    </div>
                    <span
                      className="mt-auto text-xs font-medium transition-colors group-hover:underline"
                      style={{ color: PRIMARY }}
                    >
                      Browse →
                    </span>
                  </div>
                </Card>
              </Link>
            ))}
          </div>
        </section>

        {/* -------------------------------------------------------------- */}
        {/* RANDOM ANIMAL + PET MATCHER  — side-by-side CTAs               */}
        {/* -------------------------------------------------------------- */}
        <section aria-label="Discovery actions" className="grid grid-cols-1 gap-6 md:grid-cols-2">

          {/* Random Animal */}
          <div
            className="relative overflow-hidden rounded-2xl p-8 flex flex-col gap-4"
            style={{
              background: `linear-gradient(135deg, ${PRIMARY} 0%, ${PRIMARY_DARK} 100%)`,
              color: "#fff",
            }}
          >
            {/* decorative blob */}
            <div
              aria-hidden
              className="pointer-events-none absolute -top-8 -right-8 w-40 h-40 rounded-full opacity-20"
              style={{ backgroundColor: PRIMARY_LIGHT }}
            />
            <div className="relative">
              <span className="text-4xl" role="img" aria-label="dice">🎲</span>
              <h3 className="mt-3 text-xl font-bold leading-snug">
                Discover a Random Animal
              </h3>
              <p className="mt-1 text-sm" style={{ color: "rgba(255,255,255,0.78)" }}>
                Let chance guide you to your next favorite creature. Every click is a surprise.
              </p>
              {randomError && (
                <p className="mt-2 text-xs font-medium" style={{ color: "#FFD166" }}>
                  {randomError}
                </p>
              )}
              <button
                onClick={handleRandom}
                disabled={loadingRandom}
                className="mt-5 inline-flex items-center gap-2 rounded-xl px-5 py-2.5 text-sm font-semibold transition-opacity hover:opacity-90 disabled:opacity-60 active:scale-95"
                style={{ backgroundColor: "#fff", color: PRIMARY_DARK }}
              >
                {loadingRandom ? (
                  <>
                    <span className="inline-block w-3.5 h-3.5 rounded-full border-2 border-current border-t-transparent animate-spin" />
                    Finding…
                  </>
                ) : (
                  <>Surprise me ✦</>
                )}
              </button>
            </div>
          </div>

          {/* Pet Matcher CTA */}
          <Link
            href="/matcher"
            className="group relative overflow-hidden rounded-2xl p-8 flex flex-col gap-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
            style={{
              background: `linear-gradient(135deg, ${SECONDARY} 0%, ${SECONDARY_DARK} 100%)`,
              color: "#fff",
            }}
          >
            {/* decorative blob */}
            <div
              aria-hidden
              className="pointer-events-none absolute -top-8 -right-8 w-40 h-40 rounded-full opacity-25"
              style={{ backgroundColor: "#A8E6C3" }}
            />
            <div className="relative">
              <span className="text-4xl" role="img" aria-label="heart">💚</span>
              <h3 className="mt-3 text-xl font-bold leading-snug">
                Find Your Perfect Pet
              </h3>
              <p className="mt-1 text-sm" style={{ color: "rgba(255,255,255,0.85)" }}>
                Answer a few questions and our AI matches you with pets that fit your lifestyle, space, and personality.
              </p>
              <span
                className="mt-5 inline-flex items-center gap-2 rounded-xl px-5 py-2.5 text-sm font-semibold transition-opacity group-hover:opacity-90 active:scale-95"
                style={{ backgroundColor: "rgba(255,255,255,0.2)", border: "1px solid rgba(255,255,255,0.35)", color: "#fff" }}
              >
                Take the quiz →
              </span>
            </div>
          </Link>
        </section>

        {/* -------------------------------------------------------------- */}
        {/* FOOTER TAGLINE                                                  */}
        {/* -------------------------------------------------------------- */}
        <div className="text-center text-sm pb-4" style={{ color: "#B8A898" }}>
          Creaturedex — every creature has a story worth knowing.
        </div>
      </main>
    </div>
  );
}
