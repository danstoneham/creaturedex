"use client";

import { use, useState, useEffect, useRef } from "react";
import Link from "next/link";
import Badge from "@/components/ui/Badge";
import Tabs from "@/components/ui/Tabs";
import QuickFacts from "@/components/animals/QuickFacts";
import TaxonomyTree from "@/components/animals/TaxonomyTree";
import CareSection from "@/components/animals/CareSection";
import ConservationBadge from "@/components/animals/ConservationBadge";
import DifficultyRating from "@/components/animals/DifficultyRating";
import EditToolbar from "@/components/admin/EditToolbar";
import ReviewPanel from "@/components/admin/ReviewPanel";
import ImageAttribution from "@/components/animals/ImageAttribution";
import { AnimalHabitatMap } from "@/components/animals/HabitatMapWrapper";
import type { AnimalProfile, ReviewSuggestion } from "@/lib/types";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";

export default function AnimalProfilePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);
  const [profile, setProfile] = useState<AnimalProfile | null>(null);
  const [loading, setLoading] = useState(true);

  const { isLoggedIn } = useAuth();
  const [isEditing, setIsEditing] = useState(false);
  const [editData, setEditData] = useState<Record<string, string | boolean | null>>({});
  const [editTags, setEditTags] = useState<string[]>([]);
  const [newTag, setNewTag] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [isGeneratingImage, setIsGeneratingImage] = useState(false);
  const [isReviewing, setIsReviewing] = useState(false);
  const [isFetchingWikiImage, setIsFetchingWikiImage] = useState(false);
  const [isRegenerating, setIsRegenerating] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [reviewSuggestions, setReviewSuggestions] = useState<ReviewSuggestion[] | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const fetchProfile = async () => {
      setLoading(true);
      try {
        const data = await api.animals.getBySlug(slug);
        setProfile(data);
      } catch (err) {
        console.error("Failed to fetch animal:", err);
        setProfile(null);
      } finally {
        setLoading(false);
      }
    };
    fetchProfile();
  }, [slug]);

  const handleEdit = () => {
    if (!profile) return;
    const { animal } = profile;
    setEditData({
      commonName: animal.commonName,
      scientificName: animal.scientificName,
      summary: animal.summary,
      description: animal.description,
      categoryId: animal.categoryId,
      isPet: animal.isPet,
      conservationStatus: animal.conservationStatus,
      nativeRegion: animal.nativeRegion,
      habitat: animal.habitat,
      diet: animal.diet,
      lifespan: animal.lifespan,
      sizeInfo: animal.sizeInfo,
      behaviour: animal.behaviour,
      funFacts: animal.funFacts,
    });
    setEditTags([...profile.tags]);
    setIsEditing(true);
  };

  const handleCancel = () => {
    setIsEditing(false);
    setEditData({});
    setEditTags([]);
  };

  const handleSave = async () => {
    if (!profile) return;
    setIsSaving(true);
    try {
      await api.admin.updateAnimal(profile.animal.id, {
        commonName: (editData.commonName as string) || "",
        scientificName: (editData.scientificName as string) || null,
        summary: (editData.summary as string) || "",
        description: (editData.description as string) || "",
        categoryId: (editData.categoryId as string) || profile.animal.categoryId,
        isPet: editData.isPet as boolean,
        conservationStatus: (editData.conservationStatus as string) || null,
        nativeRegion: (editData.nativeRegion as string) || null,
        habitat: (editData.habitat as string) || null,
        diet: (editData.diet as string) || null,
        lifespan: (editData.lifespan as string) || null,
        sizeInfo: (editData.sizeInfo as string) || null,
        behaviour: (editData.behaviour as string) || null,
        funFacts: (editData.funFacts as string) || null,
        tags: editTags,
      });
      // Refresh the profile
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
      setIsEditing(false);
    } catch (err) {
      console.error("Failed to save:", err);
      alert("Failed to save changes");
    } finally {
      setIsSaving(false);
    }
  };

  const handleGenerateImage = async () => {
    if (!profile) return;
    setIsGeneratingImage(true);
    try {
      await api.admin.generateImage(profile.animal.id);
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
    } catch (err) {
      console.error("Failed to generate image:", err);
      alert("Failed to generate image");
    } finally {
      setIsGeneratingImage(false);
    }
  };

  const handleUploadImage = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !profile) return;
    setIsGeneratingImage(true);
    try {
      await api.admin.uploadImage(profile.animal.id, file);
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
    } catch (err) {
      console.error("Failed to upload image:", err);
      alert("Failed to upload image");
    } finally {
      setIsGeneratingImage(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const handleRegenerate = async () => {
    if (!profile) return;
    if (!window.confirm(`Regenerate "${profile.animal.commonName}"? This will delete the current version and create a new one from scratch.`)) return;
    setIsRegenerating(true);
    try {
      const result = await api.admin.regenerateAnimal(profile.animal.id);
      // Redirect to the new animal's page
      window.location.href = `/animals/${result.slug}`;
    } catch (err) {
      console.error("Failed to regenerate:", err);
      alert("Failed to regenerate animal");
      setIsRegenerating(false);
    }
  };

  const handleDelete = async () => {
    if (!profile) return;
    if (!window.confirm(`Permanently delete "${profile.animal.commonName}"? This cannot be undone.`)) return;
    setIsDeleting(true);
    try {
      await api.admin.deleteAnimal(profile.animal.id);
      window.location.href = "/animals";
    } catch (err) {
      console.error("Failed to delete:", err);
      alert("Failed to delete animal");
      setIsDeleting(false);
    }
  };

  const handleFetchWikipediaImage = async () => {
    if (!profile) return;
    setIsFetchingWikiImage(true);
    try {
      await api.admin.fetchWikipediaImage(profile.animal.id);
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
    } catch (err) {
      console.error("Failed to fetch Wikipedia image:", err);
      alert("No Wikipedia image found for this animal");
    } finally {
      setIsFetchingWikiImage(false);
    }
  };

  const handleReview = async () => {
    if (!profile) return;
    setIsReviewing(true);
    try {
      const result = await api.admin.reviewAnimal(profile.animal.id);
      setReviewSuggestions(result.suggestions);
    } catch (err) {
      console.error("Failed to review:", err);
      alert("Failed to get AI review");
    } finally {
      setIsReviewing(false);
    }
  };

  const handleAcceptSuggestion = async (suggestion: ReviewSuggestion) => {
    if (!profile) return;
    const { animal } = profile;

    // Build full update payload with the suggestion applied
    const updateData = {
      commonName: animal.commonName,
      scientificName: animal.scientificName,
      summary: animal.summary,
      description: animal.description,
      categoryId: animal.categoryId,
      isPet: animal.isPet,
      conservationStatus: animal.conservationStatus,
      nativeRegion: animal.nativeRegion,
      habitat: animal.habitat,
      diet: animal.diet,
      lifespan: animal.lifespan,
      sizeInfo: animal.sizeInfo,
      behaviour: animal.behaviour,
      funFacts: animal.funFacts,
      tags: [...profile.tags],
    };
    // Safety check: warn if the suggestion would dramatically shorten a field
    const currentVal = (updateData as Record<string, unknown>)[suggestion.field];
    if (typeof currentVal === "string" && typeof suggestion.suggestedValue === "string"
        && currentVal.length > 100 && suggestion.suggestedValue.length < currentVal.length * 0.3) {
      const confirmed = window.confirm(
        `Warning: accepting this suggestion will shorten "${suggestion.field}" from ${currentVal.length} to ${suggestion.suggestedValue.length} characters (${Math.round(suggestion.suggestedValue.length / currentVal.length * 100)}% of original). This may be an AI error. Continue?`
      );
      if (!confirmed) return;
    }

    (updateData as Record<string, unknown>)[suggestion.field] = suggestion.suggestedValue;

    try {
      await api.admin.updateAnimal(profile.animal.id, updateData);
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
    } catch (err) {
      console.error("Failed to apply suggestion:", err);
      alert("Failed to apply suggestion");
    }

    setReviewSuggestions(prev =>
      prev ? prev.filter(s => s !== suggestion) : null
    );
  };

  const handleDismissSuggestion = (index: number) => {
    setReviewSuggestions(prev =>
      prev ? prev.filter((_, i) => i !== index) : null
    );
  };

  // Auto-mark as reviewed when all suggestions have been accepted/dismissed
  const reviewedRef = useRef(false);
  useEffect(() => {
    if (reviewSuggestions && reviewSuggestions.length === 0 && profile && !reviewedRef.current) {
      reviewedRef.current = true;
      api.admin.markReviewed(profile.animal.id).then(async () => {
        const data = await api.animals.getBySlug(slug);
        setProfile(data);
        setReviewSuggestions(null);
        reviewedRef.current = false;
      }).catch(err => {
        console.error("Failed to mark as reviewed:", err);
        reviewedRef.current = false;
      });
    }
  }, [reviewSuggestions]);

  const handleTogglePublish = async () => {
    if (!profile) return;
    try {
      if (profile.animal.isPublished) {
        await api.admin.unpublishAnimal(profile.animal.id);
      } else {
        await api.admin.publishAnimal(profile.animal.id);
      }
      const data = await api.animals.getBySlug(slug);
      setProfile(data);
    } catch (err) {
      console.error("Failed to toggle publish:", err);
    }
  };

  const handleAddTag = () => {
    const tag = newTag.trim().toLowerCase();
    if (tag && !editTags.includes(tag)) {
      setEditTags(prev => [...prev, tag]);
      setNewTag("");
    }
  };

  const handleRemoveTag = (tag: string) => {
    setEditTags(prev => prev.filter(t => t !== tag));
  };

  const renderEditableText = (field: string, currentValue: string | null, multiline = false) => {
    if (!isEditing) {
      return <p className="text-text leading-relaxed whitespace-pre-line">{currentValue || ""}</p>;
    }
    if (multiline) {
      return (
        <textarea
          value={(editData[field] as string) ?? currentValue ?? ""}
          onChange={(e) => setEditData(prev => ({ ...prev, [field]: e.target.value }))}
          className="w-full rounded-lg border border-[#5C3D2E] bg-[#3D2A1D] text-[#F5EDE3] px-3 py-2 text-sm min-h-[120px] focus:ring-primary focus:border-primary"
        />
      );
    }
    return (
      <input
        type="text"
        value={(editData[field] as string) ?? currentValue ?? ""}
        onChange={(e) => setEditData(prev => ({ ...prev, [field]: e.target.value }))}
        className="w-full rounded-lg border border-[#5C3D2E] bg-[#3D2A1D] text-[#F5EDE3] px-3 py-2 text-sm focus:ring-primary focus:border-primary"
      />
    );
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-12 text-center">
        <p className="text-text-muted">Loading...</p>
      </div>
    );
  }

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

  // Compute habitat map center and zoom from bbox
  const hasMap = !!animal.mapTileUrlTemplate;
  let mapCenterLat = 20;
  let mapCenterLng = 0;
  let mapZoom = 2;
  if (hasMap && animal.mapMinLat != null && animal.mapMaxLat != null && animal.mapMinLng != null && animal.mapMaxLng != null) {
    mapCenterLat = (animal.mapMinLat + animal.mapMaxLat) / 2;
    mapCenterLng = (animal.mapMinLng + animal.mapMaxLng) / 2;
    const latSpan = animal.mapMaxLat - animal.mapMinLat;
    const lngSpan = animal.mapMaxLng - animal.mapMinLng;
    const maxSpan = Math.max(latSpan, lngSpan);
    if (maxSpan > 150) mapZoom = 2;
    else if (maxSpan > 80) mapZoom = 3;
    else if (maxSpan > 40) mapZoom = 4;
    else if (maxSpan > 20) mapZoom = 5;
    else if (maxSpan > 10) mapZoom = 6;
    else mapZoom = 7;
  }

  const contentTabs = [
    {
      id: "overview",
      label: "Overview",
      content: (
        <div className="prose max-w-none">
          {renderEditableText("description", animal.description, true)}
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
    ...(animal.habitat || isEditing
      ? [
          {
            id: "habitat",
            label: "Habitat",
            content: renderEditableText("habitat", animal.habitat, true),
          },
        ]
      : []),
    ...(hasMap
      ? [
          {
            id: "range-map",
            label: "Range Map",
            content: (
              <AnimalHabitatMap
                tileUrlTemplate={animal.mapTileUrlTemplate!}
                centerLat={mapCenterLat}
                centerLng={mapCenterLng}
                zoom={mapZoom}
                observationCount={animal.mapObservationCount}
              />
            ),
          },
        ]
      : []),
    ...(animal.behaviour || isEditing
      ? [
          {
            id: "behaviour",
            label: "Behaviour",
            content: renderEditableText("behaviour", animal.behaviour, true),
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
      {/* Hidden file input for image upload */}
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileChange}
        accept=".png,.jpg,.jpeg,.webp"
        className="hidden"
      />

      {/* Admin toolbar */}
      {isLoggedIn && (
        <EditToolbar
          isEditing={isEditing}
          isPublished={animal.isPublished}
          isSaving={isSaving}
          isGeneratingImage={isGeneratingImage}
          isReviewing={isReviewing}
          isFetchingWikiImage={isFetchingWikiImage}
          isRegenerating={isRegenerating}
          isDeleting={isDeleting}
          onToggleEdit={handleEdit}
          onSave={handleSave}
          onCancel={handleCancel}
          onGenerateImage={handleGenerateImage}
          onUploadImage={handleUploadImage}
          onFetchWikipediaImage={handleFetchWikipediaImage}
          onRegenerate={handleRegenerate}
          onDelete={handleDelete}
          onReview={handleReview}
          onTogglePublish={handleTogglePublish}
        />
      )}

      {/* AI Review Panel */}
      {reviewSuggestions !== null && (
        <ReviewPanel
          suggestions={reviewSuggestions}
          onAccept={handleAcceptSuggestion}
          onDismiss={handleDismissSuggestion}
          onClose={() => setReviewSuggestions(null)}
          animal={profile.animal}
        />
      )}

      {/* Draft badge */}
      {isLoggedIn && !animal.isPublished && (
        <div className="bg-amber-50 border border-amber-300 rounded-lg p-3 mb-6 text-sm text-amber-700 font-medium">
          Draft — this animal is not published yet
        </div>
      )}

      {/* Disclaimer for unreviewed content */}
      {!isReviewed && (
        <div className="bg-yellow-50 border border-yellow-300 rounded-lg p-3 mb-6 text-sm text-yellow-700">
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
            {isEditing ? (
              <input
                type="text"
                value={(editData.commonName as string) ?? animal.commonName}
                onChange={(e) => setEditData(prev => ({ ...prev, commonName: e.target.value }))}
                className="text-3xl font-bold text-text w-full border-b border-[#D4C4B0] bg-transparent focus:border-primary focus:outline-none"
              />
            ) : (
              <h1 className="text-3xl font-bold text-text">{animal.commonName}</h1>
            )}
            {isEditing ? (
              <input
                type="text"
                value={(editData.scientificName as string) ?? animal.scientificName ?? ""}
                onChange={(e) => setEditData(prev => ({ ...prev, scientificName: e.target.value }))}
                placeholder="Scientific name"
                className="text-lg text-text-muted italic mt-1 w-full border-b border-[#D4C4B0] bg-transparent focus:border-primary focus:outline-none"
              />
            ) : (
              animal.scientificName && <p className="text-lg text-text-muted italic mt-1">{animal.scientificName}</p>
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
            {isEditing ? (
              <div className="flex gap-1.5 mt-3 flex-wrap items-center">
                {editTags.map((tag) => (
                  <span key={tag} className="inline-flex items-center gap-1 bg-[#F5EDE3] text-text-muted text-xs px-2 py-1 rounded-full">
                    {tag}
                    <button onClick={() => handleRemoveTag(tag)} className="text-text-muted hover:text-red-500">&times;</button>
                  </span>
                ))}
                <div className="flex items-center gap-1">
                  <input
                    type="text"
                    value={newTag}
                    onChange={(e) => setNewTag(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), handleAddTag())}
                    placeholder="Add tag..."
                    className="text-xs border border-[#D4C4B0] bg-white text-text rounded px-2 py-1 w-24 focus:ring-primary focus:border-primary"
                  />
                  <button onClick={handleAddTag} className="text-xs text-primary hover:underline">Add</button>
                </div>
              </div>
            ) : (
              tags.length > 0 && (
                <div className="flex gap-1.5 mt-3 flex-wrap">
                  {tags.map((tag) => (
                    <Badge key={tag}>{tag}</Badge>
                  ))}
                </div>
              )
            )}
          </div>

          {/* Hero image */}
          {animal.imageUrl ? (
            <div className="mb-8">
              <div className="aspect-video rounded-xl overflow-hidden bg-surface">
                <img
                  src={animal.imageUrl}
                  alt={animal.commonName}
                  className={`w-full h-full ${animal.imageUrl.startsWith("http") ? "object-contain" : "object-cover"}`}
                />
              </div>
              <ImageAttribution
                license={animal.imageLicense}
                rightsHolder={animal.imageRightsHolder}
                source={animal.imageSource}
              />
            </div>
          ) : (
            <div className="aspect-video bg-gradient-to-br from-primary-light/20 to-primary/10 rounded-xl flex items-center justify-center mb-8">
              <span className="text-8xl">{"\ud83d\udc3e"}</span>
            </div>
          )}

          {/* Summary */}
          {isEditing ? (
            <textarea
              value={(editData.summary as string) ?? animal.summary}
              onChange={(e) => setEditData(prev => ({ ...prev, summary: e.target.value }))}
              className="w-full text-lg text-text leading-relaxed rounded-lg border border-[#5C3D2E] bg-[#3D2A1D] text-[#F5EDE3] px-3 py-2 min-h-[80px] focus:ring-primary focus:border-primary"
            />
          ) : (
            <p className="text-lg text-text leading-relaxed mb-8">{animal.summary}</p>
          )}

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
