import type {
  AnimalCard,
  AnimalProfile,
  AuthUser,
  CategoryDto,
  SearchResult,
  MatcherRequest,
  MatcherResult,
  UpdateAnimalRequest,
  ReviewSuggestion,
  SpeciesSuggestion,
} from "./types";

export interface ApiError extends Error {
  status: number;
  body: Record<string, unknown> | null;
}

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "";

async function fetchApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    credentials: "include",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!res.ok) {
    let body: Record<string, unknown> | null = null;
    try { body = await res.json(); } catch { /* ignore */ }
    const err = new Error(body?.error ? String(body.error) : `API error: ${res.status} ${res.statusText}`);
    (err as ApiError).body = body;
    (err as ApiError).status = res.status;
    throw err;
  }

  return res.json();
}

export const api = {
  animals: {
    browse: async (params?: Record<string, string>) => {
      const query = params ? `?${new URLSearchParams(params)}` : "";
      const res = await fetchApi<{ animals: AnimalCard[]; totalCount: number }>(`/api/animals${query}`);
      return res;
    },
    getBySlug: (slug: string) =>
      fetchApi<AnimalProfile>(`/api/animals/${slug}`),
    getRandom: () => fetchApi<AnimalCard>("/api/animals/random"),
  },
  categories: {
    getAll: () => fetchApi<CategoryDto[]>("/api/categories"),
    getBySlug: (slug: string) =>
      fetchApi<CategoryDto>(`/api/categories/${slug}`),
  },
  search: (query: string, type?: string) => {
    const params = new URLSearchParams({ q: query });
    if (type) params.set("type", type);
    return fetchApi<SearchResult[]>(`/api/search?${params}`);
  },
  matcher: (request: MatcherRequest) =>
    fetchApi<MatcherResult>("/api/matcher", {
      method: "POST",
      body: JSON.stringify(request),
    }),
  auth: {
    login: (username: string, password: string) =>
      fetchApi<AuthUser>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ username, password }),
      }),
    logout: () =>
      fetchApi<{ message: string }>("/api/auth/logout", {
        method: "POST",
      }),
    me: () => fetchApi<AuthUser>("/api/auth/me"),
  },
  admin: {
    updateAnimal: (id: string, data: UpdateAnimalRequest) =>
      fetchApi<{ message: string; id: string }>(`/api/admin/animals/${id}`, {
        method: "PUT",
        body: JSON.stringify(data),
      }),
    uploadImage: async (id: string, file: File) => {
      const formData = new FormData();
      formData.append("file", file);
      const res = await fetch(`${API_BASE}/api/admin/animals/${id}/image/upload`, {
        method: "POST",
        body: formData,
        credentials: "include",
      });
      if (!res.ok) throw new Error(`Upload failed: ${res.status}`);
      return res.json() as Promise<{ imageUrl: string }>;
    },
    generateImage: (id: string) =>
      fetchApi<{ imageUrl: string; animalName: string }>(`/api/admin/image/generate/${id}`, {
        method: "POST",
      }),
    generateAnimal: (animalName: string, taxonKey?: number, scientificName?: string) =>
      fetchApi<{ id: string; slug: string; message: string }>("/api/admin/generate", {
        method: "POST",
        body: JSON.stringify({ animalName, skipImage: true, taxonKey, scientificName }),
      }),
    searchSpecies: (query: string) =>
      fetchApi<SpeciesSuggestion[]>(`/api/admin/species/search?q=${encodeURIComponent(query)}`),
    fetchWikipediaImage: (id: string) =>
      fetchApi<{ imageUrl: string; source: string; license: string }>(`/api/admin/animals/${id}/wikipedia-image`, {
        method: "POST",
      }),
    reviewAnimal: (id: string) =>
      fetchApi<{ suggestions: ReviewSuggestion[] }>(`/api/admin/animals/${id}/review`, {
        method: "POST",
      }),
    markReviewed: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/review/${id}`, {
        method: "PUT",
      }),
    regenerateAnimal: (id: string) =>
      fetchApi<{ id: string; slug: string; message: string }>(`/api/admin/animals/${id}/regenerate`, {
        method: "POST",
      }),
    deleteAnimal: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/animals/${id}`, {
        method: "DELETE",
      }),
    publishAnimal: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/publish/${id}`, {
        method: "PUT",
      }),
    unpublishAnimal: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/unpublish/${id}`, {
        method: "PUT",
      }),
  },
};
