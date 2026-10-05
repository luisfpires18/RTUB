# React Novidades (`/news`, React track 025)

A public news wall: text posts about what the RTUB has been doing, written by Admin/Owner and read by everyone. A new
feature, not a Blazor replacement. On screen it is **Novidades**; in code, routes and APIs it is **News**. The task
was first named "Newsletter"; it is the public news feed. Publishing sends nothing (no email, no push).

## What it does

- **`/news`** (public, React): every published post, newest first, 10 per page with "Mostrar mais". Each post is a
  card signed **"RTUB"** (the logo as avatar), with the publication date, an optional title and the text (plain text,
  line breaks kept, never rendered as HTML). Each card has the anchor `#post-{id}`, so `/news#post-12` scrolls to it.
- **Admin / Owner** also see:
  - **"Nova publicação"**: a dialog with an optional title (≤ 150) and the text (≤ 5000), saved as a draft
    ("Guardar rascunho") or published straight away ("Publicar");
  - their **drafts** above the feed, with a "Rascunho" badge, seen by no one else;
  - on every post: **Editar** (title and text; the state stays), **Publicar** / **Despublicar**, **Eliminar**
    (permanent, after a confirmation);
  - who wrote each post ("escrito por …", or "uma conta eliminada").
- **Navigation:** "Novidades" in the top bar (after Atuações), the mobile menu and the footer.
- **Home page:** "Últimas novidades", the 3 newest published posts right after the Atuações preview, with "Ver todas".
  Hidden while loading, when there are no posts or when the call fails. The old "Novidades · Em breve" strip at the
  bottom of the home page is gone; the home does not end with anything about news.

## Rules (server-side, `NewsService` + `NewsAuthorization`)

| | Visitor | Member (any category) | Mod | Admin | Owner |
| --- | --- | --- | --- | --- | --- |
| Read published posts | yes | yes | yes | yes | yes |
| See drafts and authors | no | no | no | yes | yes |
| Create, edit, publish, unpublish, delete | 401 | 403 | 403 | yes | yes (inherits Admin) |

- Sorted by `PublishedAt` descending, then id. Publish sets `PublishedAt` to now on a draft (publishing a published post
  changes nothing); unpublish clears it; re-publishing gives a new date, so the post goes back to the top.
- Title optional (blank = none), text required; both trimmed.
- Every write needs the antiforgery token (`X-CSRF-TOKEN`); nothing is sent on publish.

## API (`Endpoints/NewsEndpoints.cs`)

| Method | Path | Who | Answer |
| --- | --- | --- | --- |
| GET | `/api/news?page=1&pageSize=10` | anyone | `{ drafts, posts, hasMore, canManage }`; `pageSize` clamped to 1-20; `page` < 1 → 400; `drafts` only for Admin/Owner, first page only |
| POST | `/api/news` | Admin/Owner | `{ title, body, publish }` → 201 + the post |
| PUT | `/api/news/{id}` | Admin/Owner | `{ title, body }` → the post (state unchanged) |
| POST | `/api/news/{id}/publish` | Admin/Owner | the post |
| POST | `/api/news/{id}/unpublish` | Admin/Owner | the post |
| DELETE | `/api/news/{id}` | Admin/Owner | 204 |

A post is `{ id, title, body, publishedAt, authorName }`: `publishedAt` is UTC (null for a draft); `authorName` is the
member's display name for Admin/Owner and null for everyone else. No account id, username or email is ever returned.
Validation errors are `400` with `errors.title` / `errors.body`; unknown ids `404`.

## Schema (approved; migration `AddNewsPosts`)

Table **`NewsPosts`** (`NewsPost : BaseEntity`, so also `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`):

| Column | Type | Notes |
| --- | --- | --- |
| `Title` | TEXT(150), null | optional |
| `Body` | TEXT(5000), not null | plain text |
| `PublishedAt` | TEXT (DateTime), null | null = draft; indexed (`IX_NewsPosts_PublishedAt`) |
| `AuthorId` | TEXT(450), null | FK → `AspNetUsers.Id`, **ON DELETE SET NULL** (the post survives its author); indexed |

Additive only: no existing table changes, so the previous release runs on the migrated database (N-1). Posts are in
the audit log like other content (they hold no private data).

## Tests

- `tests/RTUB.Integration.Tests/Api/NewsApiTests.cs`: public read (published only, newest first, no author, no ids or
  usernames, UTC dates, `/news` public React shell); visitors 401 and Member/Mod 403 on every write; Admin and Owner
  draft → publish → re-publish (no date change) → unpublish → re-publish on top → edit → delete; antiforgery on every
  write; validation and limits; paging; deleting the author's account keeps the post with no author; push and email
  never called.
- `MigrationChainTests.MigratedDatabase_HasNewsPosts_WithAuthorSetNullAndPublishedAtIndex`: the migration's columns,
  `SET NULL` and the index (plus the existing chain-from-zero and no-pending-model-change tests).
- `tests/RTUB.Web.Tests/ReactPortal/NewsPortalTests.cs`: navbar link and route, the home preview right after the agenda
  and never last, no teaser left, post text never rendered as HTML. `PortalRouteTests` pins `/news` as React.

## Not in this version (follow-ups)

- Images, comments, likes/reactions, pinned posts (existing patterns to reuse: the event image upload, Leaderboard
  comments and likes).
- A per-post page (`/news/{id}`) with server-rendered OpenGraph tags, so a shared post previews on Facebook/WhatsApp;
  today a post is shared as `/news#post-{id}`.
- Clickable links in the text; an "editado" marker.
- Push/email on publish.
