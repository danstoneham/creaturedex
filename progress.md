# Creaturedex — Build Progress

## Session 1 (2026-03-01)

### Completed (15 of 16 tasks)
- [x] GitHub repo: https://github.com/danstoneham/creaturedex
- [x] **Task #1 — Solution Scaffolding**: .NET 10 solution (Api, Core, Data, AI, Shared), Next.js frontend, project refs, NuGet packages, Program.cs, API proxy
- [x] **Task #2 — Database Schema (DACPAC)**: 7 tables, 4 indexes, full-text catalog/index. DACPAC SDK compat issue (non-blocking, SQL files correct)
- [x] **Task #3 — Seed Data (DbUp)**: DbConnectionFactory, MigrationRunner, 001_SeedCategories.sql (11 categories)
- [x] **Task #4 — Ollama AI Integration**: AIConfig, AIService (Complete/Stream), EmbeddingService
- [x] **Task #5 — Core Entities**: 7 POCOs (Animal, Taxonomy, Category, PetCareGuide, AnimalCharacteristic, AnimalTag, AnimalEmbedding)
- [x] **Task #6 — Shared DTOs**: 4 enums, 4 request models, 6 response models. Shared→Core reference added
- [x] **Task #7 — Dapper Repositories**: 8 repositories (Animal, Category, Taxonomy, PetCareGuide, Characteristic, Tag, Embedding, Search)
- [x] **Task #8 — Business Services**: AnimalService, CategoryService, SearchService, MatcherService, ContentGenerationService
- [x] **Task #11 — Next.js Design System**: Tailwind config, globals.css, Header/Footer/Navigation/MobileNav, 5 UI components (Card, Badge, PawRating, Tabs, Skeleton), API client, TypeScript types, useDebounce hook
- [x] **Task #12 — Homepage**: Hero section, category grid, random animal button, pet matcher CTA, stats bar
- [x] **Task #13 — Browse & Category Pages**: AnimalCard, AnimalGrid components, browse page with filter sidebar, category/[slug] page
- [x] **Task #14 — Animal Profile Page**: QuickFacts, TaxonomyTree, CareSection, ConservationBadge, DifficultyRating components, full [slug] page with tabs
- [x] **Task #15 — Search Page**: SearchBar component, search page with keyword/smart toggle
- [x] **Task #16 — Pet Matcher**: MatcherFlow (7-step wizard), MatcherResults, matcher page with intro/loading/results states

- [x] **Task #10 — Content Generation Pipeline**: ContentGeneratorService, SemanticSearchService, MatcherAIService, animals-seed.json (~130 animals)

### Remaining for Session 2
- [x] **Task #9 — API Controllers** (completed in Session 2)
- [x] Wire up Program.cs with full DI registrations
- [x] Full build verification (dotnet build + next build)
- [x] Git commit all work
- [ ] DACPAC .sqlproj SDK compatibility fix (Microsoft.Build.Sql version)
- [ ] Consider: DacpacDeployer utility class

---

## Session 2 (2026-03-01)

### Completed (all planned tasks + extras)
- [x] **Fix EmbeddingService.cs**: Removed invalid `.First()` on `GeneratedEmbeddings` return type
- [x] **Fix TaxonomyTree.tsx**: Removed always-false `key === "id"` check flagged by TypeScript
- [x] **Full DI wiring in Program.cs**: DbConnectionFactory, 8 repositories (scoped), AIConfig + OllamaSharp clients (chat + embeddings), 5 AI services, 5 business services, DbUp MigrationRunner, 3 health endpoints (`/health`, `/health/db`, `/health/ai`)
- [x] **Task #9 — API Controllers**: 5 controllers with 17+ endpoints
  - `AnimalsController` — Browse (paginated/filtered), GetBySlug, GetRandom
  - `CategoriesController` — GetAll, GetBySlug
  - `SearchController` — Search (text/semantic/hybrid), GetTags
  - `MatcherController` — Match (POST)
  - `AdminController` — Status, Generate, BatchGenerate, GetUnreviewed, MarkReviewed, Publish, PublishAll
- [x] **Added MarkReviewedAsync** to AnimalRepository
- [x] **Added OllamaSharp + Microsoft.Extensions.AI** packages to Api csproj
- [x] **Build verification**: dotnet build 0 errors/0 warnings, next build 0 errors
- [x] **Wired AdminController** generate endpoints to ContentGeneratorService (Ollama AI pipeline)
- [x] **Fixed Ollama model config**: Updated AIConfig + appsettings.json to use available models (`gpt-oss:20b` chat, `nomic-embed-text:latest` embeddings)
- [x] **Fixed Next.js API proxy**: Port 5032 → 5000 in next.config.ts
- [x] **Fixed api.ts browse response**: Corrected shape to match `{animals, totalCount}`
- [x] **Wired browse page**: Added useEffect data fetching with category/pet/sort filters
- [x] **Wired animal profile page**: Added API fetch for [slug] page
- [x] **Database deployed**: Created Creaturedex DB on LocalDB, 7 tables + indexes via sqlcmd (full-text not supported on LocalDB)
- [x] **15 animals AI-generated via Ollama**: Full pipeline — LLM generates JSON, parsed into taxonomy, characteristics, tags, pet care guides, and embeddings
- [x] **All animals published** and visible in frontend

### Animals Generated (via Ollama gpt-oss:20b)
African Elephant, Bearded Dragon, Bottlenose Dolphin, Budgerigar, Chimpanzee, Clownfish, Emperor Penguin, Golden Retriever, Highland Cow, Holland Lop Rabbit, Maine Coon, Monarch Butterfly, Red Panda, Siamese Cat, Snow Leopard

### Commits
- `283eaaa` — Wire up API controllers, full DI, and fix build errors (10 files, +267)
- `5367c3d` — Wire up AI generation pipeline and frontend data fetching (7 files, +75/-25)

### Remaining for Session 3
- [ ] DACPAC .sqlproj SDK compatibility fix / DacpacDeployer utility
- [ ] Full-text search (requires SQL Server, not LocalDB)
- [ ] Semantic search wiring (needs embeddings + vector similarity)
- [ ] Wire MatcherService to MatcherAIService
- [ ] Image generation / image URLs for animals
- [ ] More animals (batch generate from animals-seed.json)
- [ ] Search page wiring to API
- [ ] Category page wiring to API
- [ ] Homepage dynamic data (animal count, random animal)
- [ ] Review workflow (mark reviewed before publish)
- [ ] Mobile responsive testing
- [ ] Production deployment configuration

### Notes
- Ollama server at http://10.1.1.71:11436 with `gpt-oss:20b` and `nomic-embed-text:latest`
- LocalDB does not support full-text search catalogs
- DACPAC deploy commented out in Program.cs (TODO: .NET 10 SDK compat)
- Schema deployed manually via sqlcmd; need DacpacDeployer for automated startup
