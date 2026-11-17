# React Novidades (`/news`)

A news wall: text posts about what the RTUB has been doing, written by Mod/Admin/Owner. On screen it is **Novidades**;
in code, routes and APIs it is **News**. Publishing sends nothing (no push, email or in-app notification; asserted in
the tests), so nothing can leak.

Code: `Endpoints/NewsEndpoints.cs`, `NewsService`, `Helpers/NewsAuthorization.cs`, `portal/src` news page.

## Page

- `/news` (React): published posts, newest first, 10 per page with "Mostrar mais". Each card is signed "RTUB" (logo as
  avatar) with the date, an optional title and the text (plain text, line breaks kept, never rendered as HTML). Anchor
  `#post-{id}` (the static React shell; there is no per-post endpoint, a guessed id is a 404).
- Writers (Mod and above) also get "Nova publicação" (title <= 150, optional; text <= 5000), saved as a draft
  ("Guardar rascunho") or published ("Publicar"); their drafts above the feed with a "Rascunho" badge; per post
  **Editar** (state unchanged), **Publicar** / **Despublicar**; **Eliminar** (permanent, after confirmation; Admin/Owner
  only). Admin/Owner also see the author ("escrito por ...", or "uma conta eliminada"). `/news?new=1` opens the composer
  for writers (from `/pending-actions` -> Novidades).
- "Novidades" is in the top bar (after Atuações), the mobile menu and the footer. The home page shows "Últimas
  novidades", the 3 newest posts after the Atuações preview, with "Ver todas"; hidden while loading, when empty or on
  failure (`getLatestNews(3)` is the same endpoint and session).

## Permissions

| | Visitor | Member | Mod | Admin | Owner |
| --- | --- | --- | --- | --- | --- |
| Read published posts | yes (public ones) | yes | yes | yes | yes |
| See drafts and authors | no | no | yes | yes | yes |
| Create, edit, publish, unpublish | 401 | 403 | yes | yes | yes |
| Delete | 401 | 403 | 403 | yes | yes (inherits Admin) |

- Sorted by `PublishedAt` descending, then id. Publishing a draft sets `PublishedAt` to now (publishing a published post
  changes nothing); unpublish clears it; re-publishing gives a new date, so the post goes back to the top.
- Title optional (blank = none), text required; both trimmed.
- Every write needs `X-CSRF-TOKEN`.

## API

| Method | Path | Who | Answer |
| --- | --- | --- | --- |
| GET | `/api/news?page=1&pageSize=10&visibility=` | anyone | `{ drafts, posts, hasMore, canManage, canWrite }`; `pageSize` clamped to 1-20; `page` < 1 -> 400; `drafts` only for Mod and above (`canWrite`), first page only |
| POST | `/api/news` | Mod and above | `{ title, body, publish, membersOnly }` -> 201 + the post |
| PUT | `/api/news/{id}` | Mod and above | `{ title, body, membersOnly }` -> the post (state unchanged) |
| POST | `/api/news/{id}/publish` | Mod and above | the post |
| POST | `/api/news/{id}/unpublish` | Mod and above | the post |
| DELETE | `/api/news/{id}` | Admin/Owner | 204 |

A post is `{ id, title, body, publishedAt, authorName }` (plus the visibility flags below): `publishedAt` is UTC (null for
a draft); `authorName` is the display name for Admin/Owner and null for everyone else. No account id, username or email
is ever returned. Validation errors are `400` with `errors.title` / `errors.body`; unknown ids `404`.

## Schema

Table `NewsPosts` (`NewsPost : BaseEntity`; migrations `AddNewsPosts`, `AddNewsPostIsPublic`):

| Column | Type | Notes |
| --- | --- | --- |
| `Title` | TEXT(150), null | optional |
| `Body` | TEXT(5000), not null | plain text |
| `PublishedAt` | TEXT (DateTime), null | null = draft; indexed (`IX_NewsPosts_PublishedAt`) |
| `AuthorId` | TEXT(450), null | FK to `AspNetUsers.Id`, ON DELETE SET NULL (the post survives its author); indexed |
| `IsPublic` | bool | column default `1`; the EF model has no default, so the app always writes its own value |

Posts appear in the audit log like other content (`IsPublic` shows up in create / update changes).

## Visibility

Public vs members only, one rule: `NewsService.VisibleTo`, applied in the query.

- Create: `NewsPostInput.MembersOnly` (`bool?`) absent / false = public, true = members only ("Privado (só membros)").
  Update: true / false set it, absent keeps the current visibility, so no client can declassify a private post by
  omission. Publish / unpublish never touch it.
- A visitor gets public posts only. A `?visibility=` they send is ignored (`private` answers like `all` and reveals
  nothing). A signed-in caller gets everything, or `public` / `private` for the Todas / Públicas / Privadas tabs. Drafts
  follow the same filter for managers. Visitors get `signedIn: false`, `membersOnly: false` and no tabs.
- `GET /api/news` is the only read of `NewsPost`. No search, feed, sitemap or OpenGraph exists; the service worker never
  caches `/api/`.
- UI: `.tag--private` "Só membros" beside the author line, `.news-tabs` pills (signed-in only). The form has a radio
  **Público** / **Apenas RTUB**; a new post has none preselected and the form refuses to save without one; an edit starts
  on the post's own.
- Rollback: the `IsPublic` column is additive and ignored by the previous release, which would show private posts to
  visitors. Roll back the app only together with a hotfix, or accept that window. `Down` drops the column. The default
  `1` keeps every existing post public after a rollback.

Tests: `tests/RTUB.Integration.Tests/Api/NewsApiTests.cs`, `MigrationChainTests`, `tests/RTUB.Web.Tests/ReactPortal/NewsPortalTests.cs`.

## Follow-ups

- Images, comments, likes/reactions, pinned posts.
- A per-post page (`/news/{id}`) with server-rendered OpenGraph tags, so a shared post previews on Facebook/WhatsApp.
- Clickable links in the text; an "editado" marker; push/email on publish.
