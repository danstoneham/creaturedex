import type {
  AnimalCard,
  AnimalProfile,
  CategoryDto,
  SearchResult,
  MatcherRequest,
  MatcherResult,
} from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "";

async function fetchApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
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
};
