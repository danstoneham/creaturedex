# Admin Content Management & Authentication — Design Document

**Date:** 2026-03-04
**Status:** Approved

## Overview

Add self-service content management features to Creaturedex so admins (currently two users) can create, edit, and manage animal content directly within the site. Includes authentication, inline editing on public pages, AI content generation, manual image upload, and AI-powered content review.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Auth approach | Simple username/password + JWT | Small family project, no external dependencies needed |
| Token storage | HttpOnly secure cookie | XSS-safe, works with Next.js SSR |
| Admin UX | Inline editing on public pages | See the page as the public would, with edit controls overlaid |
| User roles | Equal permissions (both full admin) | Only two users currently; designed for future role expansion |
| AI review | Quality check + actionable suggestions | Returns concrete replacement text per field, accept/dismiss individually |
| Creation flow | AI generates text (no image), user edits inline | One input (animal name), AI does heavy lifting, user polishes |
| Auto-review on save | No | Manual trigger only — keeps saves fast and simple |

## Future Extensibility

The auth system is designed with future expansion in mind:
- `Users` table includes a `Role` column (defaults to "Admin") for future role-based access
- Auth middleware reads role from JWT claims, ready for `[Authorize(Roles = "Admin")]` differentiation
- No user management UI now, but the API structure supports adding it later
- Password reset, user creation UI, and editor roles are planned future features

---

## 1. Authentication

### Database

New `Users` table:

```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Admin',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

- Passwords hashed with BCrypt
- Seed migration creates two admin accounts with temporary passwords (changed on first use or set via environment variable)
- `Role` column included for future RBAC expansion

### Backend

**New `AuthController`:**
- `POST /api/auth/login` — validates credentials, returns JWT in HttpOnly cookie (`creaturedex_token`)
- `POST /api/auth/logout` — clears the cookie
- `GET /api/auth/me` — returns current user info `{ id, username, displayName, role }` or 401

**JWT Configuration:**
- Claims: `userId`, `username`, `displayName`, `role`
- Expiry: 7 days (suitable for family project)
- ASP.NET Core JWT bearer middleware configured to read from cookie
- `[Authorize]` attribute added to all `/api/admin/*` endpoints

### Frontend

- `/login` page — simple username/password form, redirects to home on success
- Next.js middleware reads cookie, makes auth state available to server components
- `useAuth()` client hook exposes `{ user, isLoggedIn, logout }`
- Header shows "Login" link when unauthenticated; shows display name + "Logout" when authenticated

---

## 2. Inline Editing on Animal Profile Pages

### Edit Toolbar

When logged in and viewing `/animals/[slug]`, an edit toolbar appears at the top:
- **Edit** toggle — switches page into edit mode
- **Generate Image** — calls existing image generation endpoint
- **Upload Image** — opens file picker for manual upload
- **AI Review** — triggers content quality review
- **Publish / Unpublish** — toggles publication status
- **Save** — appears in edit mode, saves all changes
- **Cancel** — discards changes, returns to view mode

### Edit Mode

- Text fields (summary, description, habitat, diet, fun facts, behaviour, lifespan, size info, native region) become editable inputs/textareas
- **Category** — dropdown selector
- **Conservation status** — dropdown selector
- **Is Pet** — toggle switch
- **Tags** — add/remove tag interface (input + tag chips with X buttons)
- **Scientific name** — editable text input

### Image Management (in edit mode)

- Current image displayed with two action buttons:
  - "Generate with AI" — calls `POST /api/admin/image/generate/{id}`, shows loading spinner
  - "Upload Image" — file picker, uploads via new `POST /api/admin/animals/{id}/image/upload`

### Draft Visibility

- Unpublished animals are visible to authenticated users with a "Draft" badge
- `GET /api/animals/{slug}` modified to return unpublished animals when request has valid auth cookie
- Browse page can optionally show drafts to admins (filter toggle)

### API Endpoints

- `PUT /api/admin/animals/{id}` — update all animal fields (leverages existing `AnimalRepository.UpdateAsync`)
- `POST /api/admin/animals/{id}/image/upload` — accepts multipart form file, saves to image storage
- `PUT /api/admin/animals/{id}/tags` — replace tags for an animal

---

## 3. Creating a New Animal

### Entry Point

When logged in, a "+ Add Animal" button appears on the `/animals` browse page (top right, near filters).

### Flow

1. Click "+ Add Animal" → modal/dialog appears asking for the animal name
2. Submit → calls `POST /api/admin/generate` with `{ animalName, skipImage: true }`
3. Loading state shown ("Generating Golden Eagle...")
4. On completion, redirects to `/animals/[slug]` where the new animal loads in **edit mode** (unpublished, draft badge visible)
5. User reviews/edits AI-generated content inline
6. User generates or uploads image when ready
7. User clicks "Publish" when satisfied

### Backend Changes

- Add `skipImage` parameter to `ContentGeneratorService.GenerateAnimalAsync` / `GenerateAnimalRequest`
- New animals created as `IsPublished = false` (already the case)

---

## 4. AI Content Review

### Trigger

"AI Review" button in the edit toolbar on any animal profile page.

### Flow

1. Click "AI Review" → button shows loading spinner
2. Backend sends animal's full content to Ollama with a structured review prompt
3. Returns a list of suggestions displayed in a **review panel** (slides in from right or appears below toolbar)
4. Each suggestion shows:
   - **Severity icon** — info (blue) or warning (amber)
   - **Field name** — which field the suggestion relates to
   - **Current value** — the existing text
   - **Suggested value** — AI's concrete replacement text
   - **Message** — brief explanation of why
   - **Accept button (tick)** — applies the suggested change to the field in edit mode (unsaved)
   - **Dismiss button (X)** — removes the suggestion
5. User accepts/dismisses suggestions individually, then clicks Save when done

### Review Criteria (Ollama prompt)

- **Accuracy** — are facts correct and verifiable?
- **Completeness** — any missing or thin sections?
- **Tone** — accessible, age-appropriate (teenager+ audience)?
- **Consistency** — do fields contradict each other (e.g., diet vs habitat)?

### API

- `POST /api/admin/animals/{id}/review` → `{ suggestions: [{ field, severity, message, currentValue, suggestedValue }] }`

### Backend

- New `ContentReviewService` in `Creaturedex.AI`:
  - Builds structured review prompt with all animal fields
  - Calls Ollama `gpt-oss:20b`
  - Parses response into typed suggestion objects

---

## 5. Summary of Changes

### Database (new)
- `Users` table with Role column for future RBAC

### Backend (new/modified)
- `AuthController` — login, logout, me
- JWT middleware (cookie-based)
- `[Authorize]` on admin endpoints
- `PUT /api/admin/animals/{id}` — full animal update
- `POST /api/admin/animals/{id}/image/upload` — manual image upload
- `PUT /api/admin/animals/{id}/tags` — tag management
- `POST /api/admin/animals/{id}/review` — AI content review
- `ContentReviewService` — Ollama-powered review
- `skipImage` parameter on content generation
- Auth-aware animal retrieval (show drafts to admins)

### Frontend (new)
- `/login` page
- `useAuth()` hook + Next.js middleware
- Login/logout in header
- "+ Add Animal" on browse page with name input modal
- Edit toolbar on animal profile
- Inline edit mode for all animal fields + tags
- Image upload + AI image generation controls
- AI review panel with accept/dismiss per suggestion
- Draft badge for unpublished animals
- Loading states for AI operations

### Not in Scope
- Password reset flow
- User management UI (accounts seeded via migration)
- Edit history / audit log
- Auto-review on save
- Editor role restrictions (future)
