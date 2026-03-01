# Creaturedex — Comprehensive Development Plan (v3)

## Project Overview

A public-facing animal encyclopedia powered by AI-generated content. .NET 10 Web API backend (Dapper, SQL Server, DACPAC), Next.js frontend (App Router, Tailwind CSS, TypeScript), Ollama for AI content generation and semantic search. Covers all animals with pet-specific filtering. Content presented in accessible, layman-friendly language.

Backend follows WikiVault coding patterns: primary constructors, concrete DI (interfaces only when justified), Dapper repositories, DACPAC schema, DbUp seeds, OllamaSharp + Microsoft.Extensions.AI. DTOs live in a shared library, not as inner records on controllers.

---

## Architecture

```
Next.js Frontend                .NET 10 Web API
localhost:3000                   localhost:5032
┌──────────────────┐            ┌──────────────────────┐
│ App Router       │──API──────▶│ Controllers           │
│ Server/Client    │            │ Services              │
│ Tailwind CSS     │            │ Repositories (Dapper) │
│ TypeScript       │            │                       │
└──────────────────┘            └──────────┬────────────┘
                                           │
                                    ┌──────┴──────┐
                                    │  SQL Server  │
                                    │  (LocalDB /  │
                                    │   full)      │
                                    └──────┬──────┘
                                           │
                                    ┌──────┴──────┐
                                    │   Ollama     │
                                    │  (4x 3090)   │
                                    └─────────────┘
```

---

## Solution Structure

```
Creaturedex.slnx

src/
  Creaturedex.Api/                    # ASP.NET Web API
    Controllers/
      AnimalsController.cs              # Animal browse, detail, random
      CategoriesController.cs           # Category listing
      SearchController.cs               # Text + semantic search
      MatcherController.cs              # Pet matcher
      ContentGenerationController.cs    # Admin: trigger AI generation
    Services/
      AnimalService.cs
      CategoryService.cs
      SearchService.cs
      MatcherService.cs
      ContentGenerationService.cs
    Program.cs
    appsettings.json

  Creaturedex.Core/                   # Plain POCO entities (DB shape)
    Entities/
      Animal.cs
      Taxonomy.cs
      Category.cs
      PetCareGuide.cs
      AnimalCharacteristic.cs
      AnimalTag.cs
      AnimalEmbedding.cs

  Creaturedex.Shared/                 # Shared DTOs, requests, responses, enums
    Requests/
      BrowseAnimalsRequest.cs
      MatcherRequest.cs
      GenerateAnimalRequest.cs
      BatchGenerateRequest.cs
    Responses/
      AnimalProfileResponse.cs          # Full composite: animal + taxonomy + care + characteristics + tags
      AnimalCardDto.cs                  # Lightweight for browse/grid (no Description, no MAX fields)
      CategoryDto.cs                    # Category with animal count
      SearchResultDto.cs
      MatcherResultResponse.cs
      GenerationStatusResponse.cs
    Enums/
      ConservationStatus.cs
      LivingSpace.cs
      ExperienceLevel.cs
      BudgetRange.cs

  Creaturedex.Data/                   # Dapper repositories
    DbConnectionFactory.cs
    MigrationRunner.cs
    Repositories/
      AnimalRepository.cs
      CategoryRepository.cs
      TaxonomyRepository.cs
      PetCareGuideRepository.cs
      CharacteristicRepository.cs
      TagRepository.cs
      EmbeddingRepository.cs
      SearchRepository.cs
    Scripts/
      001_SeedCategories.sql
      002_SeedSampleAnimals.sql         # Optional: a few hand-crafted entries

  Creaturedex.AI/                     # AI services (separate project)
    AIConfig.cs
    Services/
      AIService.cs                      # Generic LLM completion (wraps IChatClient)
      EmbeddingService.cs               # Generate + store embeddings
      ContentGeneratorService.cs        # Animal profile generation from LLM
      SemanticSearchService.cs          # Embedding similarity search
      MatcherAIService.cs               # Pet recommendation via LLM

  Creaturedex.Database/               # DACPAC SQL project
    Tables/
      Animals.sql
      Taxonomy.sql
      Categories.sql
      PetCareGuides.sql
      AnimalCharacteristics.sql
      AnimalTags.sql
      AnimalEmbeddings.sql
    Indexes/
      IX_Animals_CategoryId.sql
      IX_Animals_Slug.sql
      IX_AnimalTags_Tag.sql
      UQ_Animals_Slug.sql
    FullText/
      Animals_FullTextIndex.sql
      CreaturedexCatalog.sql
    Creaturedex.Database.sqlproj

  creaturedex-web/                   # Next.js frontend
    src/
      app/
        page.tsx                        # Homepage
        layout.tsx                      # Root layout
        animals/
          page.tsx                      # Browse/search all animals
          [slug]/page.tsx               # Individual animal page
        categories/
          [slug]/page.tsx               # Browse by category
        matcher/
          page.tsx                      # Pet matcher
        search/
          page.tsx                      # Search results
      components/
        layout/
          Header.tsx
          Footer.tsx
          Navigation.tsx
          MobileNav.tsx
        animals/
          AnimalCard.tsx                # Card for grid views
          AnimalGrid.tsx                # Responsive grid of cards
          QuickFacts.tsx                # Sidebar quick-facts panel
          CareSection.tsx               # Pet care guide sections
          TaxonomyTree.tsx              # Visual taxonomy display
          DifficultyRating.tsx          # Paw-print rating display
          ConservationBadge.tsx         # Conservation status indicator
        search/
          SearchBar.tsx
          SearchResults.tsx
          FilterPanel.tsx
        matcher/
          MatcherFlow.tsx               # Multi-step questionnaire
          MatcherResults.tsx            # AI recommendation results
        ui/
          Card.tsx
          Badge.tsx
          Tabs.tsx
          Skeleton.tsx
          PawRating.tsx
      lib/
        api.ts                          # API client for .NET backend
        types.ts                        # TypeScript interfaces (mirror Shared DTOs)
      hooks/
        useDebounce.ts
    next.config.ts
    tailwind.config.ts
    package.json
```

**Project references:**
```
Creaturedex.Api     → Core, Data, AI, Shared
Creaturedex.AI      → Core, Data, Shared
Creaturedex.Data    → Core
Creaturedex.Shared  → (no dependencies — pure DTOs and enums)
Creaturedex.Core    → (no dependencies — pure entities)
```

---

## Design Principles

### DTOs

- DTOs are used **when they serve a purpose**: shaping API responses, decoupling entities from wire format, trimming heavyweight fields from listings, composing data from multiple entities
- DTOs live in `Creaturedex.Shared` — a standalone library referenced by any project that needs the contract shapes (API, AI, future admin API, future MCP server)
- DTOs are **never** defined as inner records on controllers
- If an entity maps 1:1 to what the client needs and there's no reason to hide/reshape fields, return the entity directly — don't create a DTO just for the sake of having one
- Key DTOs that earn their keep:
  - `AnimalCardDto` — lightweight card shape for browse grids (excludes `Description`, `Habitat`, and other `NVARCHAR(MAX)` fields)
  - `AnimalProfileResponse` — composite shape joining Animal + Taxonomy + PetCareGuide + Characteristics + Tags into one response
  - `MatcherRequest` / `MatcherResultResponse` — specific to the matcher feature
  - `CategoryDto` — category with computed animal count (doesn't exist on the entity)
  - `SearchResultDto` — animal with relevance score / snippet

### Interfaces

- Interfaces are **not** added by default on every service and repository
- Interfaces are added **only when justified**: multiple implementations, genuine need for mocking in tests, or a clear abstraction boundary
- Concrete classes registered directly in DI: `builder.Services.AddScoped<AnimalService>()`
- If a second implementation or testing need arises later, the interface is added at that point

### Layering

- **Controllers**: HTTP concerns only — routing, status codes, model binding, returning responses. No business logic.
- **Services**: Business logic and orchestration. Services call repositories for data, call AI services for AI features, compose DTOs from multiple data sources.
- **Repositories**: Data access only — SQL queries via Dapper, no business logic. Each repository handles one table or closely related set of tables.
- Each layer only talks to the one below it. Controllers never call repositories directly. Services never execute raw SQL.

---

## Phase 1: Foundation & Infrastructure

### Phase 1.1: Solution Scaffolding

**Goal**: All projects created, building, and communicating.

- [ ] Create solution file `Creaturedex.slnx`
- [ ] Create `Creaturedex.Api` (.NET 10 Web API)
- [ ] Create `Creaturedex.Core` (class library)
- [ ] Create `Creaturedex.Data` (class library)
- [ ] Create `Creaturedex.AI` (class library)
- [ ] Create `Creaturedex.Shared` (class library)
- [ ] Create `Creaturedex.Database` (SQL project / DACPAC)
- [ ] Create `creaturedex-web` (Next.js with App Router, TypeScript, Tailwind CSS)
- [ ] Set up project references (Api → Core, Data, AI, Shared; AI → Core, Data, Shared; Data → Core)
- [ ] Add NuGet packages:
  - **Api**: `Serilog.AspNetCore`, `Microsoft.AspNetCore.OpenApi`
  - **Data**: `Dapper`, `Microsoft.Data.SqlClient`, `dbup-sqlserver`
  - **AI**: `OllamaSharp`, `Microsoft.Extensions.AI`
- [ ] Set up `DbConnectionFactory` (same pattern as WikiVault)
- [ ] Set up `MigrationRunner` (same pattern as WikiVault)
- [ ] Set up `Program.cs` with DI registrations, CORS, Serilog, health endpoints
- [ ] Set up DACPAC build + copy targets in Api .csproj (same pattern as WikiVault)
- [ ] Set up Next.js API proxy (next.config.ts rewrites to .NET API)
- [ ] Verify frontend can call backend health endpoint

**Program.cs** (following WikiVault patterns):
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=Creaturedex;Trusted_Connection=True;";
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<AnimalRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<TaxonomyRepository>();
builder.Services.AddScoped<PetCareGuideRepository>();
builder.Services.AddScoped<CharacteristicRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped<EmbeddingRepository>();
builder.Services.AddScoped<SearchRepository>();

// AI
var aiConfig = builder.Configuration.GetSection("AI").Get<AIConfig>() ?? new AIConfig();
builder.Services.AddSingleton(aiConfig);

var ollamaClient = new OllamaApiClient(new Uri(aiConfig.OllamaEndpoint));
ollamaClient.SelectedModel = aiConfig.ChatModel;
builder.Services.AddSingleton<IChatClient>(ollamaClient);

var ollamaEmbedder = new OllamaApiClient(new Uri(aiConfig.OllamaEndpoint));
ollamaEmbedder.SelectedModel = aiConfig.EmbeddingModel;
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(ollamaEmbedder);

builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<ContentGeneratorService>();
builder.Services.AddScoped<SemanticSearchService>();
builder.Services.AddScoped<MatcherAIService>();

// Services
builder.Services.AddScoped<AnimalService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<MatcherService>();
builder.Services.AddScoped<ContentGenerationService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Deploy DACPAC schema
var dacpacPath = Path.Combine(AppContext.BaseDirectory, "Creaturedex.Database.dacpac");
DacpacDeployer.Deploy(connectionString, dacpacPath);

// Run seed scripts
if (!MigrationRunner.Run(connectionString))
{
    Console.Error.WriteLine("Database seed failed!");
    return;
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/health/db", async (DbConnectionFactory dbFactory) =>
{
    try
    {
        using var conn = dbFactory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", database = "disconnected", error = ex.Message },
            statusCode: 503);
    }
});

app.MapGet("/health/ai", async (AIConfig aiCfg) =>
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await http.GetAsync($"{aiCfg.OllamaEndpoint}/api/tags");
        response.EnsureSuccessStatusCode();
        return Results.Ok(new { status = "healthy", endpoint = aiCfg.OllamaEndpoint });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", endpoint = aiCfg.OllamaEndpoint, error = ex.Message },
            statusCode: 503);
    }
});

app.Run();
```

### Phase 1.2: Database Schema (DACPAC)

**Goal**: All tables defined in SQL project, deployed via DACPAC on startup.

**Tables/Animals.sql**
```sql
CREATE TABLE [dbo].[Animals] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [Slug]               NVARCHAR(300)    NOT NULL,
    [CommonName]         NVARCHAR(300)    NOT NULL,
    [ScientificName]     NVARCHAR(300)    NULL,
    [Summary]            NVARCHAR(MAX)    NOT NULL DEFAULT '',
    [Description]        NVARCHAR(MAX)    NOT NULL DEFAULT '',
    [CategoryId]         UNIQUEIDENTIFIER NOT NULL,
    [TaxonomyId]         UNIQUEIDENTIFIER NULL,
    [IsPet]              BIT              NOT NULL DEFAULT 0,
    [ImageUrl]           NVARCHAR(500)    NULL,
    [ConservationStatus] NVARCHAR(100)    NULL,
    [NativeRegion]       NVARCHAR(500)    NULL,
    [Habitat]            NVARCHAR(MAX)    NULL,
    [Diet]               NVARCHAR(MAX)    NULL,
    [Lifespan]           NVARCHAR(200)    NULL,
    [SizeInfo]           NVARCHAR(MAX)    NULL,
    [Behaviour]          NVARCHAR(MAX)    NULL,
    [FunFacts]           NVARCHAR(MAX)    NULL,      -- JSON array
    [GeneratedAt]        DATETIME2        NULL,
    [ReviewedAt]         DATETIME2        NULL,
    [ReviewedBy]         NVARCHAR(200)    NULL,
    [IsPublished]        BIT              NOT NULL DEFAULT 0,
    [CreatedAt]          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [DeletedAt]          DATETIME2        NULL,
    [Version]            INT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_Animals] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Animals_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [FK_Animals_Taxonomy] FOREIGN KEY ([TaxonomyId]) REFERENCES [dbo].[Taxonomy]([Id])
);
```

**Tables/Taxonomy.sql**
```sql
CREATE TABLE [dbo].[Taxonomy] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [Kingdom]     NVARCHAR(100)    NOT NULL DEFAULT 'Animalia',
    [Phylum]      NVARCHAR(100)    NULL,
    [Class]       NVARCHAR(100)    NULL,
    [TaxOrder]    NVARCHAR(100)    NULL,      -- "Order" is reserved in SQL
    [Family]      NVARCHAR(100)    NULL,
    [Genus]       NVARCHAR(100)    NULL,
    [Species]     NVARCHAR(100)    NULL,
    [Subspecies]  NVARCHAR(100)    NULL,
    CONSTRAINT [PK_Taxonomy] PRIMARY KEY CLUSTERED ([Id])
);
```

**Tables/Categories.sql**
```sql
CREATE TABLE [dbo].[Categories] (
    [Id]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [Name]             NVARCHAR(100)    NOT NULL,
    [Slug]             NVARCHAR(100)    NOT NULL,
    [Description]      NVARCHAR(MAX)    NULL,
    [IconName]         NVARCHAR(100)    NULL,
    [ParentCategoryId] UNIQUEIDENTIFIER NULL,
    [SortOrder]        INT              NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentCategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [UQ_Categories_Slug] UNIQUE ([Slug])
);
```

**Tables/PetCareGuides.sql**
```sql
CREATE TABLE [dbo].[PetCareGuides] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId]            UNIQUEIDENTIFIER NOT NULL,
    [DifficultyRating]    INT              NOT NULL DEFAULT 3,
    [CostRangeMin]        DECIMAL(10,2)    NULL,
    [CostRangeMax]        DECIMAL(10,2)    NULL,
    [CostCurrency]        NVARCHAR(3)      NOT NULL DEFAULT 'GBP',
    [SpaceRequirement]    NVARCHAR(200)    NULL,
    [TimeCommitment]      NVARCHAR(200)    NULL,
    [Housing]             NVARCHAR(MAX)    NULL,
    [DietAsPet]           NVARCHAR(MAX)    NULL,
    [Exercise]            NVARCHAR(MAX)    NULL,
    [Grooming]            NVARCHAR(MAX)    NULL,
    [HealthConcerns]      NVARCHAR(MAX)    NULL,
    [Training]            NVARCHAR(MAX)    NULL,
    [GoodWithChildren]    BIT              NULL,
    [GoodWithOtherPets]   BIT              NULL,
    [Temperament]         NVARCHAR(MAX)    NULL,
    [LegalConsiderations] NVARCHAR(MAX)    NULL,
    CONSTRAINT [PK_PetCareGuides] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PetCareGuides_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
```

**Tables/AnimalCharacteristics.sql**
```sql
CREATE TABLE [dbo].[AnimalCharacteristics] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId]            UNIQUEIDENTIFIER NOT NULL,
    [CharacteristicName]  NVARCHAR(100)    NOT NULL,
    [CharacteristicValue] NVARCHAR(300)    NOT NULL,
    [SortOrder]           INT              NOT NULL DEFAULT 0,
    CONSTRAINT [PK_AnimalCharacteristics] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AnimalCharacteristics_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
```

**Tables/AnimalTags.sql**
```sql
CREATE TABLE [dbo].[AnimalTags] (
    [Id]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId] UNIQUEIDENTIFIER NOT NULL,
    [Tag]      NVARCHAR(100)    NOT NULL,
    CONSTRAINT [PK_AnimalTags] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AnimalTags_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
```

**Tables/AnimalEmbeddings.sql**
```sql
CREATE TABLE [dbo].[AnimalEmbeddings] (
    [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [AnimalId]   UNIQUEIDENTIFIER NOT NULL,
    [Embedding]  VARBINARY(MAX)   NOT NULL,
    [Dimensions] INT              NOT NULL,
    [ModelUsed]  NVARCHAR(100)    NOT NULL,
    [CreatedAt]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_AnimalEmbeddings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AnimalEmbeddings_Animals] FOREIGN KEY ([AnimalId]) REFERENCES [dbo].[Animals]([Id]) ON DELETE CASCADE
);
```

**FullText/CreaturedexCatalog.sql**
```sql
CREATE FULLTEXT CATALOG [CreaturedexCatalog] AS DEFAULT;
```

**FullText/Animals_FullTextIndex.sql**
```sql
CREATE FULLTEXT INDEX ON [dbo].[Animals] ([CommonName], [ScientificName], [Summary], [Description])
KEY INDEX [PK_Animals] ON [CreaturedexCatalog];
```

- [ ] Create all table .sql files
- [ ] Create indexes
- [ ] Create full-text catalog + index
- [ ] Verify DACPAC builds and deploys

### Phase 1.3: Seed Data (DbUp)

**Scripts/001_SeedCategories.sql**
```sql
INSERT INTO Categories (Id, Name, Slug, Description, IconName, SortOrder) VALUES
(NEWID(), 'Dogs',                  'dogs',          'Domestic dog breeds and working dogs',       'dog',       1),
(NEWID(), 'Cats',                  'cats',          'Domestic cat breeds',                        'cat',       2),
(NEWID(), 'Small Mammals',         'small-mammals', 'Rabbits, hamsters, guinea pigs, and more',   'rabbit',    3),
(NEWID(), 'Reptiles & Amphibians', 'reptiles',      'Lizards, snakes, turtles, frogs, and more',  'lizard',    4),
(NEWID(), 'Birds',                 'birds',         'Parrots, finches, birds of prey, and more',  'bird',      5),
(NEWID(), 'Fish & Aquatic',        'fish',          'Freshwater, saltwater, and aquarium fish',    'fish',      6),
(NEWID(), 'Insects & Arachnids',   'insects',       'Beetles, butterflies, spiders, and more',     'bug',       7),
(NEWID(), 'Farm Animals',          'farm',          'Horses, goats, chickens, and livestock',      'barn',      8),
(NEWID(), 'Wild Mammals',          'wild-mammals',  'Lions, elephants, wolves, bears, and more',   'paw',       9),
(NEWID(), 'Ocean Life',            'ocean',         'Whales, dolphins, sharks, and sea creatures',  'waves',    10),
(NEWID(), 'Primates',              'primates',      'Monkeys, apes, and lemurs',                   'monkey',   11);
```

- [ ] Create seed script
- [ ] Verify seeds run on startup via DbUp

### Phase 1.4: Ollama Integration (AI Project)

**Goal**: AI services wired up and tested.

**AIConfig.cs:**
```csharp
namespace Creaturedex.AI;

public class AIConfig
{
    public string OllamaEndpoint { get; set; } = "http://10.1.1.71:11436";
    public string ChatModel { get; set; } = "llama3:70b-instruct";
    public string FastModel { get; set; } = "llama3:8b-instruct";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
```

- [ ] Create `AIConfig.cs`
- [ ] Create `AIService.cs` (same pattern as WikiVault — CompleteAsync, StreamAsync)
- [ ] Create `EmbeddingService.cs` (same pattern as WikiVault — GenerateAsync, GenerateAndStoreAsync)
- [ ] Register in Program.cs with OllamaSharp + Microsoft.Extensions.AI
- [ ] Test basic completion and embedding generation

---

## Phase 2: Content Generation Pipeline

### Phase 2.1: Generation Schema & Prompts

**Goal**: Define the exact JSON output format the LLM must produce, and craft the master prompt.

**Target JSON schema** (what the LLM outputs, maps to our DB tables):
```json
{
  "commonName": "Bearded Dragon",
  "scientificName": "Pogona vitticeps",
  "slug": "bearded-dragon",
  "summary": "A friendly, docile lizard that's become one of the most popular pet reptiles worldwide.",
  "description": "Full 3-4 paragraph description in layman's terms...",
  "categorySlug": "reptiles",
  "isPet": true,
  "conservationStatus": "Least Concern",
  "nativeRegion": "Central Australia",
  "habitat": "Arid and semi-arid woodlands, scrublands, and deserts...",
  "diet": "Omnivorous — eats both insects and plants...",
  "lifespan": "10-15 years",
  "sizeInfo": "45-60 cm (18-24 inches) including tail. Adults weigh 300-600 grams.",
  "behaviour": "Naturally curious and social for a reptile...",
  "funFacts": [
    "They wave their arms to communicate submission",
    "They can change their beard colour to black when threatened",
    "They enter a state called brumation, similar to hibernation"
  ],
  "taxonomy": {
    "kingdom": "Animalia",
    "phylum": "Chordata",
    "class": "Reptilia",
    "order": "Squamata",
    "family": "Agamidae",
    "genus": "Pogona",
    "species": "P. vitticeps",
    "subspecies": null
  },
  "characteristics": [
    { "name": "Weight", "value": "300-600g" },
    { "name": "Length", "value": "45-60 cm" },
    { "name": "Top Speed", "value": "40 km/h" },
    { "name": "Body Temperature", "value": "35-42°C (basking)" }
  ],
  "tags": ["docile", "beginner-friendly", "diurnal", "omnivore", "desert"],
  "petCareGuide": {
    "difficultyRating": 2,
    "costRangeMin": 400,
    "costRangeMax": 800,
    "costCurrency": "GBP",
    "spaceRequirement": "120x60x60 cm vivarium minimum for an adult",
    "timeCommitment": "30-60 minutes daily for feeding, cleaning, and handling",
    "housing": "Detailed housing info...",
    "dietAsPet": "Detailed feeding guide...",
    "exercise": "...",
    "grooming": "...",
    "healthConcerns": "...",
    "training": "...",
    "goodWithChildren": true,
    "goodWithOtherPets": false,
    "temperament": "...",
    "legalConsiderations": "No license required in the UK..."
  }
}
```

- [ ] Write master system prompt with:
  - Role: expert zoologist and animal care writer
  - Audience: teenagers and adults without scientific background, explain technical terms in plain English
  - Tone: warm, enthusiastic, informative, never condescending
  - Accuracy: provide ranges where uncertain, flag uncertainty rather than guess
  - Format: exact JSON schema with field descriptions
  - 2 few-shot examples (one pet, one wild animal)
- [ ] Test with 5-10 diverse animals across different categories
- [ ] Iterate on prompt until output quality is consistently good
- [ ] Create separate prompt variants:
  - Pet animals (includes full care guide in response)
  - Wild animals (no care guide, conservation focus)

### Phase 2.2: Content Generator Service

**Goal**: Service that takes an animal name, generates content via LLM, and stores everything in the database.

- [ ] Implement `ContentGeneratorService` in Creaturedex.AI:
  1. Call LLM with master prompt + animal name
  2. Parse JSON response
  3. Resolve category by slug
  4. Insert Taxonomy row
  5. Insert Animal row
  6. Insert PetCareGuide if isPet
  7. Insert Characteristics
  8. Insert Tags
  9. Generate + store embedding
  10. Return animal ID
- [ ] Implement `BatchGenerateAsync` — loops through animal list, logs progress
- [ ] Create `ContentGenerationController`:
  - `POST /api/admin/generate` — single animal (uses `GenerateAnimalRequest` from Shared)
  - `POST /api/admin/generate/batch` — list of animals (uses `BatchGenerateRequest` from Shared)
  - `GET /api/admin/generation-status` — returns `GenerationStatusResponse`
- [ ] Create the initial animal seed list (JSON file in repo, ~150-200 animals):
  - 30-40 dog breeds
  - 20-30 cat breeds
  - 10-15 small mammals
  - 10-15 reptiles & amphibians
  - 10-15 birds
  - 10-15 fish
  - 10-15 wild mammals
  - 5-10 ocean life
  - 5-10 insects/arachnids
  - 5-10 primates
  - 5-10 farm animals
- [ ] Test single generation end-to-end
- [ ] Run batch generation (kick off overnight)

### Phase 2.3: Content Review

**Goal**: Simple endpoints to review and publish generated content.

- [ ] `GET /api/admin/review` — list unreviewed animals (IsPublished = false)
- [ ] `PUT /api/admin/review/{id}` — mark reviewed, optional inline edits
- [ ] `PUT /api/admin/publish/{id}` — set IsPublished = true
- [ ] `PUT /api/admin/publish/all` — batch publish everything (weekend shortcut, content served with disclaimer)

---

## Phase 3: Backend API

### Phase 3.1: Core Entities

**Goal**: All POCO entities in Creaturedex.Core.

```csharp
// Example: Animal.cs — plain POCO, DB shape
namespace Creaturedex.Core.Entities;

public class Animal
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public bool IsPet { get; set; }
    public string? ImageUrl { get; set; }
    public string? ConservationStatus { get; set; }
    public string? NativeRegion { get; set; }
    public string? Habitat { get; set; }
    public string? Diet { get; set; }
    public string? Lifespan { get; set; }
    public string? SizeInfo { get; set; }
    public string? Behaviour { get; set; }
    public string? FunFacts { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; } = 1;
}
```

- [ ] Create all entity classes: Animal, Taxonomy, Category, PetCareGuide, AnimalCharacteristic, AnimalTag, AnimalEmbedding

### Phase 3.2: Shared DTOs

**Goal**: Request and response models in Creaturedex.Shared.

```csharp
// AnimalCardDto — lightweight, used in browse grids
namespace Creaturedex.Shared.Responses;

public class AnimalCardDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsPet { get; set; }
    public string? ImageUrl { get; set; }
    public string? ConservationStatus { get; set; }
    public int? DifficultyRating { get; set; }       // From PetCareGuide, null if not pet
}

// AnimalProfileResponse — full composite for detail page
namespace Creaturedex.Shared.Responses;

public class AnimalProfileResponse
{
    public Animal Animal { get; set; } = null!;       // Full entity is fine here
    public Taxonomy? Taxonomy { get; set; }
    public PetCareGuide? CareGuide { get; set; }
    public List<AnimalCharacteristic> Characteristics { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public bool IsReviewed { get; set; }
}

// BrowseAnimalsRequest — query params for browse
namespace Creaturedex.Shared.Requests;

public class BrowseAnimalsRequest
{
    public string? Category { get; set; }
    public bool? IsPet { get; set; }
    public string? Tag { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public string SortBy { get; set; } = "name";     // "name" | "newest"
}

// MatcherRequest
namespace Creaturedex.Shared.Requests;

public class MatcherRequest
{
    public string LivingSpace { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
    public string TimeAvailable { get; set; } = string.Empty;
    public string BudgetRange { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
    public bool HasOtherPets { get; set; }
    public List<string> Preferences { get; set; } = [];
}

// MatcherResultResponse
namespace Creaturedex.Shared.Responses;

public class MatcherResultResponse
{
    public List<MatcherRecommendation> Recommendations { get; set; } = [];
}

public class MatcherRecommendation
{
    public AnimalCardDto Animal { get; set; } = null!;
    public string Explanation { get; set; } = string.Empty;
    public int MatchScore { get; set; }
}
```

- [ ] Create all request models in `Shared/Requests/`
- [ ] Create all response models in `Shared/Responses/`
- [ ] Create any shared enums in `Shared/Enums/`

### Phase 3.3: Repositories

**Goal**: Dapper repositories for all data access.

```csharp
// Example: AnimalRepository pattern
namespace Creaturedex.Data.Repositories;

public class AnimalRepository(DbConnectionFactory db)
{
    public async Task<Animal?> GetBySlugAsync(string slug)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Animal>(
            "SELECT * FROM Animals WHERE Slug = @Slug AND DeletedAt IS NULL AND IsPublished = 1",
            new { Slug = slug });
    }

    public async Task<IEnumerable<Animal>> BrowseAsync(Guid? categoryId, bool? isPet, int page, int pageSize)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<Animal>(
            """
            SELECT * FROM Animals
            WHERE DeletedAt IS NULL AND IsPublished = 1
              AND (@CategoryId IS NULL OR CategoryId = @CategoryId)
              AND (@IsPet IS NULL OR IsPet = @IsPet)
            ORDER BY CommonName
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { CategoryId = categoryId, IsPet = isPet, Offset = (page - 1) * pageSize, PageSize = pageSize });
    }

    public async Task<Animal?> GetRandomAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Animal>(
            "SELECT TOP 1 * FROM Animals WHERE DeletedAt IS NULL AND IsPublished = 1 ORDER BY NEWID()");
    }
}
```

- [ ] `AnimalRepository` — GetBySlug, GetById, Browse (paginated + filtered), GetRandom, Create, Update, SoftDelete, Count
- [ ] `CategoryRepository` — GetAll, GetBySlug, GetWithCounts (JOIN to get animal counts)
- [ ] `TaxonomyRepository` — GetById, Create
- [ ] `PetCareGuideRepository` — GetByAnimalId, Create, Update
- [ ] `CharacteristicRepository` — GetByAnimalId, BulkInsert, DeleteByAnimalId
- [ ] `TagRepository` — GetByAnimalId, GetAllUnique, GetByTag, BulkInsert, DeleteByAnimalId
- [ ] `EmbeddingRepository` — GetByAnimalId, Upsert, FindSimilar (cosine similarity), FloatsToBytes/BytesToFloats
- [ ] `SearchRepository` — FullTextSearch (CONTAINS with LIKE fallback, same pattern as WikiVault)

### Phase 3.4: Services

**Goal**: Business logic layer. Services compose data from repositories into DTOs where needed.

- [ ] `AnimalService`:
  - `BrowseAsync(BrowseAnimalsRequest)` → fetches animals, resolves category names, joins difficulty ratings, returns `List<AnimalCardDto>`
  - `GetBySlugAsync(slug)` → fetches animal + taxonomy + care guide + characteristics + tags, composes `AnimalProfileResponse`
  - `GetRandomAsync()` → returns `AnimalCardDto`
- [ ] `CategoryService`:
  - `GetAllAsync()` → returns `List<CategoryDto>` with animal counts
  - `GetBySlugAsync(slug)` → returns single `CategoryDto`
- [ ] `SearchService`:
  - `SearchAsync(query, type)` → delegates to SearchRepository (text) or SemanticSearchService (semantic), returns `List<SearchResultDto>`
- [ ] `MatcherService`:
  - `GetRecommendationsAsync(MatcherRequest)` → pre-filters pets by constraints, calls MatcherAIService, returns `MatcherResultResponse`
- [ ] `ContentGenerationService`:
  - Admin-facing: wraps ContentGeneratorService for controller use

### Phase 3.5: Controllers

**Goal**: Thin controllers — routing and status codes only, all logic in services.

```csharp
// Example: AnimalsController
namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/animals")]
public class AnimalsController(AnimalService animals) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] BrowseAnimalsRequest request) =>
        Ok(await animals.BrowseAsync(request));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var animal = await animals.GetBySlugAsync(slug);
        if (animal == null) return NotFound();
        return Ok(animal);
    }

    [HttpGet("random")]
    public async Task<IActionResult> GetRandom() =>
        Ok(await animals.GetRandomAsync());
}
```

**Complete API endpoint list:**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | App health |
| GET | `/health/db` | Database connectivity |
| GET | `/health/ai` | Ollama connectivity |
| GET | `/api/animals` | Browse animals (paginated, filtered) |
| GET | `/api/animals/{slug}` | Full animal profile |
| GET | `/api/animals/random` | Random animal |
| GET | `/api/categories` | All categories with counts |
| GET | `/api/categories/{slug}` | Category detail |
| GET | `/api/search?q=...&type=text` | Full-text search |
| GET | `/api/search?q=...&type=semantic` | Semantic search |
| GET | `/api/search?q=...` | Hybrid search (default) |
| GET | `/api/tags` | All unique tags with counts |
| POST | `/api/matcher` | Pet matcher recommendations |
| POST | `/api/admin/generate` | Generate single animal |
| POST | `/api/admin/generate/batch` | Batch generate |
| GET | `/api/admin/review` | Unreviewed animals |
| PUT | `/api/admin/review/{id}` | Mark reviewed |
| PUT | `/api/admin/publish/{id}` | Publish animal |
| PUT | `/api/admin/publish/all` | Publish all |
| POST | `/api/admin/embed-all` | Bulk embed all animals |

- [ ] Create all controllers
- [ ] Test all endpoints with sample data

---

## Phase 4: Frontend (Next.js)

### Phase 4.1: Design System & Layout

**Goal**: Visual foundation — warm, accessible, clean.

**Design tokens:**
- Primary: `#2D6A4F` (forest green)
- Primary light: `#74C69D`
- Secondary: `#E09F3E` (warm amber)
- Background: `#FAFAF8` (warm off-white)
- Surface: `#FFFFFF`
- Text: `#2D3436` (dark grey, not black)
- Text muted: `#636E72`
- Category accent colours (per category for visual distinction)

**Typography:** Inter, generous line-height (1.6 for body text).

- [ ] Set up Tailwind config with custom colours, fonts, spacing
- [ ] Build root layout (`app/layout.tsx`) with Header, Footer
- [ ] Build `Header` — logo, nav links (Home, Browse, Pet Matcher), search bar
- [ ] Build `Footer` — simple, links, disclaimer
- [ ] Build responsive `Navigation` with mobile hamburger menu
- [ ] Build base UI components: Card, Badge, PawRating, Tabs, Skeleton
- [ ] WCAG AA contrast check on all components

### Phase 4.2: Homepage

- [ ] Hero section: site name, tagline, prominent search bar
- [ ] "Explore by Category" — grid of category cards with icons
- [ ] "Discover an Animal" — random animal button
- [ ] "Find Your Perfect Pet" — matcher CTA
- [ ] Stats bar: "X animals | Y categories | Powered by AI"
- [ ] Wire up: fetch categories and counts from API

### Phase 4.3: Browse & Category Pages

- [ ] `animals/page.tsx` — full animal grid with filter sidebar
  - Category checkboxes
  - "Pets only" toggle
  - Tag filter chips
  - Sort: A-Z, Newest
- [ ] `AnimalCard` component: image placeholder, names, summary (truncated), badges, paw rating
- [ ] Pagination (page numbers or load-more)
- [ ] `categories/[slug]/page.tsx` — pre-filtered grid with category description header
- [ ] Wire up: `/api/animals` with query params, `/api/categories`

### Phase 4.4: Animal Profile Page

**The core page — simplified, friendlier Wikipedia.**

- [ ] **Header area:**
  - Breadcrumb (Home > Category > Animal)
  - Common name (h1), scientific name (italic)
  - Category badge, pet badge, conservation status badge (colour-coded)
  - Image placeholder

- [ ] **Quick Facts sidebar** (sticky desktop, top card mobile):
  - Lifespan, Size, Native Region, Habitat, Diet
  - If pet: difficulty rating, cost range, time commitment
  - Uses characteristics data

- [ ] **Content sections** (tabbed or anchor-scroll):
  - Overview (main description)
  - Classification (taxonomy tree visual)
  - Habitat & Distribution
  - Diet
  - Behaviour
  - Fun Facts
  - Conservation (if wild animal)
  - Keeping as a Pet (if isPet, separate tab):
    - Suitability, Housing, Diet, Exercise, Grooming, Health, Training, Legal, Cost

- [ ] **Related Animals** section at bottom (same category or similar tags)
- [ ] **Disclaimer banner** for unreviewed content
- [ ] Wire up: `/api/animals/{slug}` composite response

### Phase 4.5: Search

- [ ] `SearchBar` in header — navigates to `/search?q=...` on submit
- [ ] `search/page.tsx`:
  - Search input, toggle between Keyword / Smart Search (semantic)
  - Results as animal cards
  - Semantic results include brief match explanation
- [ ] Wire up: `/api/search`

### Phase 4.6: Pet Matcher

- [ ] `matcher/page.tsx` — multi-step form:
  1. Living situation
  2. Pet experience
  3. Daily time available
  4. Budget
  5. Household (children?)
  6. Other pets
  7. What matters most? (multi-select)
- [ ] Progress indicator
- [ ] Results page:
  - Loading state ("Finding your perfect match...")
  - Top 5 recommendations with cards + AI explanation
  - Links to animal profile pages
  - "Try again" button
- [ ] Wire up: `POST /api/matcher`

---

## Phase 5: AI Features

### Phase 5.1: Embedding Pipeline

- [ ] On animal creation, embed concatenated text (name + summary + description + tags)
- [ ] Store as VARBINARY in AnimalEmbeddings (same pattern as WikiVault NoteEmbeddings)
- [ ] `SemanticSearchService.SearchAsync(query, topK)`:
  1. Embed query
  2. Load all animal embeddings from DB
  3. Cosine similarity in memory
  4. Return top K with scores
- [ ] `POST /api/admin/embed-all` — bulk embed all animals

### Phase 5.2: Matcher AI Logic

- [ ] `MatcherAIService`:
  1. Pre-filter pet animals by hard constraints
  2. Build prompt: user preferences + animal summaries
  3. Call Ollama (fast model) → JSON array of top 5 with explanations
  4. Parse and return
- [ ] Fallback: if LLM fails, score on tag/attribute matching

---

## Phase 6: Polish & Deploy

### Phase 6.1: Content Quality

- [ ] Spot-check 20-30 animal pages for accuracy
- [ ] Fix systematic prompt issues
- [ ] Daughter review pass
- [ ] Disclaimer banner on unreviewed pages

### Phase 6.2: Frontend Polish

- [ ] Responsive testing (mobile, tablet, desktop)
- [ ] Loading states everywhere
- [ ] Error states (API down, no results)
- [ ] 404 page (animal themed)
- [ ] Empty states
- [ ] Accessibility audit (screen reader, keyboard nav, contrast)

### Phase 6.3: Deployment

- [ ] **Backend**: Deploy to IIS, configure SQL Server + Ollama endpoints
- [ ] **Frontend**: Build Next.js, deploy (Vercel, Cloudflare Pages, or self-host behind IIS reverse proxy)
- [ ] DNS + domain + SSL

---

## Phase 7: Future Enhancements

### Phase 7.1: Images
- [ ] Image sourcing strategy
- [ ] Image upload pipeline
- [ ] Gallery support

### Phase 7.2: AI Chat (Per-Animal)
- [ ] Chat widget on animal pages
- [ ] Context: full profile fed to lightweight model
- [ ] SSE streaming (WikiVault ChatService pattern)

### Phase 7.3: Affiliate Shop
- [ ] "What You'll Need" section on pet care pages
- [ ] Product categories per pet type
- [ ] Affiliate link integration

### Phase 7.4: User Accounts & My Pets
- [ ] JWT auth (WikiVault AuthService pattern)
- [ ] "My Pets" collection
- [ ] Quick links to saved pets' care guides

### Phase 7.5: SEO
- [ ] Schema.org structured data
- [ ] Sitemap generation
- [ ] Meta descriptions + OpenGraph

### Phase 7.6: Content Expansion
- [ ] Breed variants as sub-pages
- [ ] Animal comparison tool
- [ ] Seasonal care guides
- [ ] "Animal of the day"
- [ ] Blog / articles section

---

## Weekend Execution Priority

**Friday Evening (3-4 hours):**
1. Phase 1.1: Scaffold solution + all projects
2. Phase 1.2: DACPAC tables
3. Phase 1.3: Seed categories
4. Phase 1.4: Ollama integration
5. Phase 2.1: Craft generation prompt, test with a few animals
6. Kick off Phase 2.2 batch generation overnight

**Saturday Morning (4-5 hours):**
1. Phase 3.1: All entities
2. Phase 3.2: All shared DTOs
3. Phase 3.3: All repositories
4. Phase 3.4: Core services
5. Phase 3.5: Controllers
6. Review overnight generation output

**Saturday Afternoon/Evening (4-5 hours):**
1. Phase 4.1: Design system, layout, base components
2. Phase 4.3: Browse + category pages
3. Phase 4.4: Animal profile page

**Sunday Morning (4-5 hours):**
1. Phase 4.2: Homepage
2. Phase 4.5: Search page
3. Phase 4.6: Pet matcher UI
4. Phase 5.1: Embedding pipeline
5. Phase 5.2: Matcher AI logic

**Sunday Afternoon/Evening (3-4 hours):**
1. Phase 6.1: Content review with daughter
2. Phase 6.2: Polish + responsive fixes
3. Phase 6.3: Deploy

---

## Key Conventions

| Convention | Detail |
|-----------|--------|
| DI style | Primary constructors, concrete classes registered directly |
| Interfaces | Only when justified (multiple implementations, testing) — not by default |
| DTOs | In `Creaturedex.Shared`, used when they serve a purpose (not 1:1 entity mirrors) |
| Request/Response models | In `Shared/Requests/` and `Shared/Responses/` — never inner records on controllers |
| Shared library | `Creaturedex.Shared` referenced by any project needing contract shapes |
| Layering | Controller (HTTP) → Service (business logic) → Repository (data access). Each layer only calls the layer below. |
| Data access | Dapper, raw SQL, DbConnectionFactory |
| Schema management | DACPAC (SQL project, declarative) |
| Seed data | DbUp (embedded SQL scripts) |
| Entities | Plain POCOs in `Creaturedex.Core`, no annotations, auto-properties with defaults |
| IDs | `UNIQUEIDENTIFIER` / `Guid`, `NEWSEQUENTIALID()` |
| Soft delete | `DeletedAt DATETIME2 NULL` |
| Concurrency | `Version INT`, optimistic concurrency in UPDATE WHERE |
| AI | OllamaSharp + Microsoft.Extensions.AI |
| Embeddings | SQL Server `VARBINARY(MAX)`, in-memory cosine similarity |
| Logging | Serilog |
| Frontend | Next.js (App Router), TypeScript, Tailwind CSS |
| Target framework | .NET 10 |
| No auth (MVP) | Admin endpoints unsecured for weekend, JWT auth added later (WikiVault pattern) |
