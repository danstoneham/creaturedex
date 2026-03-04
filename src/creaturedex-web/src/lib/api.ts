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
} from "./types";

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
    throw new Error(`API error: ${res.status} ${res.statusText}`);
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
    generateAnimal: (animalName: string) =>
      fetchApi<{ id: string; slug: string; message: string }>("/api/admin/generate", {
        method: "POST",
        body: JSON.stringify({ animalName, skipImage: true }),
      }),
    reviewAnimal: (id: string) =>
      fetchApi<{ suggestions: ReviewSuggestion[] }>(`/api/admin/animals/${id}/review`, {
        method: "POST",
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
