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
  isPublished: boolean;
}

export interface ConservationStatus {
  code: string;
  name: string;
  description: string;
  severity: number;
  colour: string;
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
  conservationStatusRef: ConservationStatus | null;
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
  gbifTaxonKey?: number;
  gbifCanonicalName?: string;
  mapTileUrlTemplate?: string;
  mapObservationCount?: number;
  mapMinLat?: number;
  mapMaxLat?: number;
  mapMinLng?: number;
  mapMaxLng?: number;
  imageLicense?: string;
  imageRightsHolder?: string;
  imageSource?: string;

  // Structured fields (v2 pipeline)
  wikipediaUrl: string | null;
  conservationStatusCode: string | null;
  populationTrend: string | null;
  populationEstimate: string | null;
  dietTypeCode: string | null;
  activityPatternCode: string | null;
  domesticationStatusCode: string | null;
  weightMinKg: number | null;
  weightMaxKg: number | null;
  lengthMinCm: number | null;
  lengthMaxCm: number | null;
  speedMaxKph: number | null;
  lifespanWildMinYears: number | null;
  lifespanWildMaxYears: number | null;
  lifespanCaptivityMinYears: number | null;
  lifespanCaptivityMaxYears: number | null;
  gestationMinDays: number | null;
  gestationMaxDays: number | null;
  litterSizeMin: number | null;
  litterSizeMax: number | null;
  alsoKnownAs: string | null;
  distinguishingFeatures: string | null;
  legalProtections: string | null;
  coloursJson: string | null;
  habitatTypesJson: string | null;
  dataSourceVersion: number;
  lastDataFetchAt: string | null;
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
  colTaxonId?: string;
  authorship?: string;
  synonyms?: string;
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

export interface AuthUser {
  id: string;
  username: string;
  displayName: string;
  role: string;
}

export interface UpdateAnimalRequest {
  commonName: string;
  scientificName: string | null;
  summary: string;
  description: string;
  categoryId: string;
  isPet: boolean;
  conservationStatus: string | null;
  nativeRegion: string | null;
  habitat: string | null;
  diet: string | null;
  lifespan: string | null;
  sizeInfo: string | null;
  behaviour: string | null;
  funFacts: string | null;
  tags: string[];
}

export interface ReviewSuggestion {
  field: string;
  severity: "info" | "warning";
  message: string;
  currentValue: string;
  suggestedValue: string;
}

export interface SpeciesSuggestion {
  taxonKey: number;
  scientificName: string;
  commonName?: string;
  rank?: string;
  status?: string;
  family?: string;
  order?: string;
}
