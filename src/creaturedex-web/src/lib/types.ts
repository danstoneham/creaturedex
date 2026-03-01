export interface AnimalCard {
  id: string;
  slug: string;
  commonName: string;
  scientificName: string | null;
  summary: string;
  categorySlug: string;
  categoryName: string;
  isPet: boolean;
  imageUrl: string | null;
  conservationStatus: string | null;
  difficultyRating: number | null;
}

export interface AnimalProfile {
  animal: Animal;
  taxonomy: Taxonomy | null;
  careGuide: PetCareGuide | null;
  characteristics: AnimalCharacteristic[];
  tags: string[];
  categoryName: string;
  categorySlug: string;
  isReviewed: boolean;
}

export interface Animal {
  id: string;
  slug: string;
  commonName: string;
  scientificName: string | null;
  summary: string;
  description: string;
  categoryId: string;
  taxonomyId: string | null;
  isPet: boolean;
  imageUrl: string | null;
  conservationStatus: string | null;
  nativeRegion: string | null;
  habitat: string | null;
  diet: string | null;
  lifespan: string | null;
  sizeInfo: string | null;
  behaviour: string | null;
  funFacts: string | null;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Taxonomy {
  id: string;
  kingdom: string;
  phylum: string | null;
  class: string | null;
  taxOrder: string | null;
  family: string | null;
  genus: string | null;
  species: string | null;
  subspecies: string | null;
}

export interface PetCareGuide {
  id: string;
  animalId: string;
  difficultyRating: number;
  costRangeMin: number | null;
  costRangeMax: number | null;
  costCurrency: string;
  spaceRequirement: string | null;
  timeCommitment: string | null;
  housing: string | null;
  dietAsPet: string | null;
  exercise: string | null;
  grooming: string | null;
  healthConcerns: string | null;
  training: string | null;
  goodWithChildren: boolean | null;
  goodWithOtherPets: boolean | null;
  temperament: string | null;
  legalConsiderations: string | null;
}

export interface AnimalCharacteristic {
  id: string;
  animalId: string;
  characteristicName: string;
  characteristicValue: string;
  sortOrder: number;
}

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  iconName: string | null;
  animalCount: number;
}

export interface SearchResult {
  animal: AnimalCard;
  relevanceScore: number;
  snippet: string | null;
}

export interface MatcherRequest {
  livingSpace: string;
  experienceLevel: string;
  timeAvailable: string;
  budgetRange: string;
  hasChildren: boolean;
  hasOtherPets: boolean;
  preferences: string[];
}

export interface MatcherResult {
  recommendations: MatcherRecommendation[];
}

export interface MatcherRecommendation {
  animal: AnimalCard;
  explanation: string;
  matchScore: number;
}
