# RTUB UI Refactor

Audit and polish contract for the product-wide UI/UX refinement of RTUB (Task 001, branch
`chore/032/ui-refactor-audit`). This document is the input for every later UI refactor task. It
does not implement anything.

**Evidence tags** used throughout. Every finding carries at least one.

| Tag | Meaning |
| --- | --- |
| **V** | Seen in the running application (screenshot or interaction in a browser). |
| **R** | Measured in the running application (DOM, computed style, accessibility tree). |
| **S** | Source inspection only. |
| **A** | Automated tooling (build, test suite, Impeccable detector). |
| **I** | Inference. Not verified; states what would verify it. |

Priority labels: **BLOCKER/STRUCTURAL** (resolve before broad polish), **HIGH** (repeated, hurts
important workflows), **MEDIUM** (real usability or consistency problem), **LOW** (polish).

---

## 1. Purpose

- **Improvement-only.** Make the existing RTUB more coherent, polished, responsive, accessible and
  pleasant. No new product features, no new workflows, no business-rule changes.
- **Preserve the colors.** The dark near-black + purple identity stays. Colors may be formalized as
  tokens and applied more consistently; where a pair fails contrast, the fix uses the smallest
  adjustment, preferably a color RTUB already uses (section 13.2).
- **Preserve business behavior.** Domain logic, validation semantics, permissions, roles, routing
  semantics, persistence and APIs are out of scope.
- **Preserve Blazor.** Blazor Interactive Server on .NET 10 remains the architecture. Nothing here
  recommends another frontend stack.
- **Preserve and strengthen RTUB.Shared** as the single home of reusable UI.
- **Web and installed PWA are both first-class.** Mobile is designed as an app, not as a narrow
  desktop.

## 2. Audit Method

### 2.1 What was done

| Step | Detail |
| --- | --- |
| Repository state | `dev` clean at `3399d533`; audit branch `chore/032/ui-refactor-audit` created from it. |
| Source | `App.razor`, `MainLayout.razor`, all 62 routable pages (routes, render modes, auth attributes), the whole of `src/RTUB.Shared` (89 Razor components, 3 base classes), the CSS layer (`site.css` + 76 imported files + 88 scoped `.razor.css`), the global scripts, the PWA files (manifest, `sw-register.js`, `offline.html`, `ReconnectModal`), and the test projects. |
| Build and tests | `dotnet restore`, `dotnet build --no-restore --configuration Release`, `dotnet test --no-build --configuration Release` (the CI commands). Results in section 16.1. |
| Running app | Local Development run on `https://localhost:58869` against the local `src/RTUB.Web/app.db` (108 users, 45 events, 71 rehearsals, 7 meetings, 7 albums - a sanitized snapshot, all emails `@rtub.pt`). Safety overrides for the run, all process-local environment variables, nothing committed: `EmailSettings__SmtpServer=disabled.invalid` (no mail can leave), `Cloudflare__R2__Bucket=<non-existent audit bucket>` (no storage write can reach the production bucket; also satisfies the production-bucket startup guard), `DatabaseBackup__Enabled=false`, `DevelopmentDataReset__Enabled=false`. |
| Accounts | Anonymous, then an **Owner** account on the local database (credentials supplied by the owner for local development; not recorded here). A plain-member view was not signed in. |
| Browser | Claude desktop in-app browser (Chromium). Viewports emulated: **1440×900, 1024×768, 820×1180, 390×844, 360×780**. |
| Deterministic scan | Impeccable detector over `Pages`, `Shared`, `Components`, RTUB.Shared components and `wwwroot/css` (section 12.3). |
| Skills used | `impeccable` (audit and critique criteria, heuristics, detector); the built-in browser for inspection; project `CLAUDE.md` / `docs/frontend-practices.md` / `docs/pwa-practices.md` / `.github/agents/rtub-frontend-agent.md`. |

Only navigation, dialog opening, filter changes and form typing were performed. **No save, delete,
confirmation or submission was made**, except one empty login submit to see native validation. One
edit form was dirtied and cancelled to test discard behavior.

### 2.2 Coverage

| Area | Coverage | Viewports |
| --- | --- | --- |
| App shell, navbar, offcanvas drawer, footer | A - visually inspected | 1440, 1024, 820, 390 |
| Home / portal (anonymous and Owner) | A | 1440, 390 |
| Login (incl. empty-submit validation) | A | 1440 |
| Events list (anonymous + Owner) | A | 1440, 360 |
| Rehearsals list (empty + populated), details dialog, edit dialog (dirty + cancel), delete confirmation (opened, cancelled) | A | 1440, 1024, 820, 390 |
| Members directory | A | 1440, 820, 390 |
| Profile | A | 390 |
| Meetings (empty states, create dialog opened, not submitted) | A | 390 |
| Finance list, Finance report | A | 1440, 360 |
| Messages inbox (list only; message content deliberately not recorded) | A | 390 |
| MyTuno home, Leaderboard, Logistics list + one board, Music albums, Gallery | A | 390 and/or 1024/1440 |
| Event enrollments/contacts/discussion, Songs, Naipes, Hall of Fame, Hierarchy, Roles, Calotes, Requests, Questions, Nerba, MBWAY, Inventory, Shop, Bets, mini-games, MyTuno arena/stage/boss/survive, Owner/Admin operations (Emails, Notifications, Images, Labels, Users, Tracing, DB viewer) | B - source only | - |
| Installed PWA (standalone display mode), real iOS/Android devices, virtual keyboard, offline mode, SW update toast, reconnect UI, push prompts | B - source only (C for real-device behavior) | - |
| Plain member / Leitão / Caloiro navigation and permissions as rendered | C - not signed in with those roles | - |
| Remote media (album covers, slideshow, most avatars) | C - blocked locally by the Development CSP (see 19) | - |
| Light theme | n/a - RTUB is dark-only; no theme switch exists | - |

---

## 3. Current Architecture

| Topic | Finding | Evidence |
| --- | --- | --- |
| .NET | All projects `net10.0`; central package management (`Directory.Packages.props`). `global.json` sets only `test.runner: Microsoft.Testing.Platform` - there is **no SDK pin**, so any .NET 10 SDK builds it (the system index says it pins the SDK; recorded as a doc drift in `STATE.md`). | S |
| Blazor mode | Blazor Web App, **Interactive Server, per page**. 54 of 62 routable pages declare `@rendermode InteractiveServer`; 8 are static SSR (`Login`, `ForgotPassword`, `ResetPassword`, `ConfirmEmail`, `Error`, `Privacy`, `Share`, `Hierarchy`). No global render mode. | S |
| Startup | `blazor.web.js` with `autostart="false"`, started by `js/blazorStartup.js`. | S |
| Routing | `App.razor`: `Router` → `AuthorizeRouteView` (default `MainLayout`) + `FocusOnNavigate Selector="h1"`; `RedirectToLogin` for anonymous; inline **English** NotFound/NotAuthorized texts in a Portuguese app. | S |
| Layout | One `MainLayout` (static SSR): Bootstrap `navbar-expand-lg` with `offcanvas-lg` drawer, `container-xxl rtub-wide` content, footer. Interactive islands inside it: `AnnouncementBanner`, `AndroidModeLogger`, `UnreadMessagesBadge`. Nav visibility (Gestão, Operações, Owner items) is computed in the layout's C# from roles and member category. | S, V |
| Page-level chrome | 48 pages hand-write a `page-header-centered` header (left / center / right slots). 38 pages render a page-specific `MobileBottomNav` below 1200px. Pages with an app-style shell (Messages, MyTuno modes) hide or replace the global chrome. | S, V |
| State | Component fields; `MultiModalState` (36 pages) for modal visibility; some filters in the query string (`/rehearsals?fy=2025-2026`); scoped event services (`ProfilePictureUpdateService`). | S, V |
| Forms | 53 `EditForm`, 116 `InputText`, 103 raw `<input>`; DataAnnotations; `ErrorDisplay` (43 uses) for validation. `FormTextField`/`FormTextArea` exist but only the two Logistics pages use them. | S |
| Business logic ownership | Application services via DI directly into pages - no HTTP API layer for UI. Large pages orchestrate side effects themselves (e.g. `Rehearsals.razor` has 16 `@inject`s, 11 of them application services or factories, incl. the push factory, push sender and audit log). | S |
| Page size | Seven pages exceed 2,000 lines: `Meetings` 4,648, `MyTunoHome` 4,294, `Events` 4,282, `Members` 3,550, `Rehearsals` 2,986, `Inbox` 2,114, `Bets` 2,031. `Meetings` hosts 15 `<Modal>` instances. | S |
| CSS | Bootstrap 5.3 (local) → `site.css` importing **76** files in `1-base/2-layout/3-components/4-pages/9-overrides` → `RTUB.styles.css` (88 scoped files). Tokens: ~25 custom properties in `1-base/variables.css`. | S |
| JS | **38** scripts loaded by `MainLayout` on every page, including PixiJS 8 (jsDelivr), three game bundles (~325 KB on disk), Leaflet (unpkg) and Cropper (cdnjs). Interop wrappers in `RTUB.Web/Interop` (3). | S |
| PWA | `manifest.webmanifest` (`id "/"`, standalone, `portrait-primary`, `lang pt`, theme `#3F2A86`); `service-worker.js`; `sw-register.js` with an update toast ("Nova versão disponível!" / Atualizar / Depois); `offline.html`; .NET 10 `ReconnectModal` in Portuguese; `PushNotificationPrompt`, `LoginPopup`, `PlayStorePrompt` on the home page; Android TWA via `assetlinks.json`. | S |
| Auth | ASP.NET Identity cookie. Roles Owner/Admin/Mod/Member plus member categories (Leitão, Caloiro, Tuno, Veterano, Tunossauro). Login is a static SSR form post; logout is a form post to `/auth/logout`. | S, V |
| Error handling | No `ErrorBoundary`, no `#blazor-error-ui`. An unhandled exception in an event handler ends the circuit and surfaces as the reconnect dialog. | S, I |

---

## 4. RTUB.Shared Inventory

RTUB.Shared holds 89 Razor components under `Components/` (Badges, Cards, Common, Discussion,
Email, Forms, Game, Modals, Naipes, Profile, Ranking, Slideshow, Tables, UI, Uploads) and three base
classes. It references **RTUB.Application**, and 31 components reference Application namespaces
or services; several inject Application services directly (`IEventService`, `IEnrollmentService`,
`ISongService`, `IMemberStatusService`, `UserManager<ApplicationUser>`, …), so part of "Shared" is
data-aware feature UI rather than presentation (section 15.B).

Usage counts are *files / occurrences* across `RTUB.Web` and `RTUB.Shared` (S).

### 4.1 Core presentation components

| Component | Uses | Purpose | Strengths | Problems | Refine? API enough? |
| --- | --- | --- | --- | --- | --- |
| `Modals/Modal` | 53 / 129 | Base dialog; full-screen sheet on phones, dialog on desktop. | Widely adopted; phone sheet with bottom action bar is app-like (V); scroll lock; backdrop click does **not** close (safe for forms) (V). | No `role="dialog"`, `aria-modal`, `aria-labelledby` (R); focus not moved in, not trapped, not restored (R); **Escape only works when focus is already inside**, so it normally does nothing (V); background stays in the accessibility tree (R); three "Voltar" buttons in one dialog (hidden mobile back, header arrow, default footer) (R); close arrow sits on the right on desktop (V); no dirty-form hook; not integrated with browser/hardware Back (V); desktop dialogs top-anchored (V). | **Yes, first.** API needs: `aria` wiring, initial-focus target, `CanClose`/dirty guard callback, optional history integration. |
| `Modals/ConfirmDialog` | 30 / 58 | Yes/no and destructive confirmation on top of `Modal`. | Clear title, names the object ("eliminar o ensaio de 09 Jul 2026?"), secondary Cancel + red confirm (V). | Info icon even for destructive actions (V); `text-danger` warning at small size is 4.23:1 (R); no busy state - confirm runs while buttons stay enabled unless the page passes `Disabled` (S); focus not placed on the safe action (R). | Yes: `Variant` (danger/neutral), `IsBusy`, focus Cancel. |
| `Modals/DetailsModal` + `InfoSection` | 11 / 12, 10 / 36 | Read-only detail sheet with grouped fields. | Consistent label/value rhythm, icons (V). | Inherits `Modal` issues; large placeholder hero icon when no image (V). | Light refinement. |
| `Modals/CrudModalManager` | 5 / 5 | Generic create/edit + delete dialog pair. | Removes boilerplate where used. | Low adoption; inherits `Modal`. | Keep; adopt where it fits, do not force. |
| `Common/EmptyState` | 48 / 92 | Empty lists. | Most consistent state pattern in the app (V). | Default title is English ("No items found") (S); two visual variants in use (icon+title vs icon+title+paragraph) with different icons (`info-circle`, `calendar-x`) (V); `role=""` rendered when not clickable (S). | Yes: PT defaults, `Variant` (inline/section), tone. |
| `UI/LoadingSpinner` | 46 / 67 | Loading indicator with message. | `role="status"` + visually hidden label (S). | **188 raw `spinner-border` usages** bypass it (S); no skeleton variant except `SongCardSkeleton`. | Yes: inline/button/section sizes; replace raw spinners over time. |
| `Common/LoadableContent`, `PaginatedList` | 2 / 3, **0** | Loading/empty/content switch; + pagination. | Right idea. | English defaults; `PaginatedList` unused (S). | Either adopt or delete in a later cleanup task (not in this refactor). |
| `Common/ErrorDisplay` | 30 / 43 | Validation summary card. | Hooks `EditContext` (S). | Summary only - no per-field message pattern; not announced (no live region) (S). | Pair with a field wrapper (13.4). |
| `UI/Alert` | 20 / 38 | Inline success/error/info/warning messages. | Variants incl. `Purple`. | Dismiss button labelled "Close" (English) (S); used as ad-hoc success toast with `Task.Delay` auto-hide in pages (28 `Task.Delay` in pages) (S). | Keep for inline; add a shared toast (14). |
| `Tables/SearchBar` | 30 / 49 | Debounced search input. | Consistent pill look (V). | `type="text"`, no label/`aria-label`, placeholder is the only name (R); clear button labelled only by `title` (S). | Yes: `Label` (visually hidden), `type="search"`. |
| `UI/FilterDropdown` | 16 / 29 | Custom listbox select. | Keyboard handling, `aria-haspopup`/`aria-expanded`, `role="listbox"` (S). | Trigger has no name for *what* is filtered ("2026-2027" only) (R); styles differ from the native `<select>` used for page size and Gallery filters (V). | Yes: `Label` parameter. |
| `Tables/TablePagination` | 34 / 65 | Pager + page size. | Consistent. | Page size control is a native select with different styling (V). | Light. |
| `Tables/SortableTableHeader` | 1 / 5 | Sortable `th`. | - | Only 2 `<table>` elements exist in the whole app (S). | Keep. |
| `UI/MobileBottomNav` | 38 / 39 | Fixed bottom bar below 1200px. | Thumb-reachable, icon + label, 65px targets (R). | Used for **three different jobs** - section navigation (home), page actions (Rehearsals: Adicionar/Vários/Estatísticas/Presenças), filter tabs mixed with actions (Music: Públicos/Privados + Estatísticas + Adicionar) (V); always `aria-label="Navegação do portal"` (R); `aria-selected` on plain buttons (invalid outside tabs) (S); labels 10.4px uppercase (R); `padding-bottom: env(safe-area-inset-bottom, …)` collapses to 0 on devices without an inset (S); docs in the component say ≤767px, CSS says <1200px (S); page header actions and bottom-bar items are two hand-maintained lists with different labels (S). | **Yes, structural** (14). |
| `Badges/*` (Category, Position, Status, Role, LoginStatus, LockStatus, Person) | 10/36, 9/14, 4/4, … | Member category/position/status chips. | Recognizable RTUB vocabulary (TUNO, VETERANO, TUNOSSAURO) (V). | `bg-warning` + white text is 2.19:1 (R); Status colors come from a helper, not tokens (S). | Token pass only. |
| `Forms/FormTextField`, `FormTextArea` | 2 / 3, 2 / 2 | Label + input + help text. | Right shape. | Label not associated (no `for`/`id`), required is a visual `*` only, no validation slot, `@onchange` only (S). | Yes - becomes the field wrapper (13.4). |
| `Forms/MonthYearPicker` | 2 / 6 | Month/year selection. | - | Not visually inspected. | Audit when touched. |
| `Profile/ProfileField`, `ProfileSection`, `ProfileHeader` | 11/70, 1/3, 1/1 | Profile read/edit sections. | Clear sectioning with collapse + edit affordance (V). | Mostly single-page. | Light. |
| `Uploads/ImageCropper`, `ImageUploadManager`, `MediaUploadManager` | 7/8, 4/4, 2/2 | Upload + crop flows. | Shared, reused. | Not exercised (no uploads performed). | Audit when touched. |
| `UI/LabelEditButton` | 6 / 47 | Inline Owner/Admin content-label editing. | Reused. | Icon-only admin affordance in public content. | Light. |
| `UI/PushNotificationPrompt`, `PushNotificationToggle`, `PlayStorePrompt`, `LoginPopup` | 1 each | PWA/app prompts. | - | Three prompts on the home page can compete on first visit (S, I). | Sequence them (Phase 6). |
| `Base/MultiModalState` | 36 files | Modal visibility state. | Replaced boolean-flag sprawl. | - | Keep. |
| `Base/CrudTablePageBase` | 7 files | Search/sort/paginate base. | - | - | Keep. |
| `Base/ManagedModalPageBase` | **0** | - | - | Unused (S). | Cleanup candidate (not this refactor). |

### 4.2 Domain cards (mostly single-consumer)

`EventCard`, `RehearsalCard`, `AvatarCard`, `MemberCardLite`, `SongCard`, `ReportCard`,
`TransactionCard`, `MeetingCard`, `BoardCard`, `DocumentCard`, `FolderCard`, `NaipeCard`,
`BetCard`, `GameCard`, `StageEnemyCard`, `LeaderboardCard`, `RankCard`, `MbwayTransferCard`,
`NerbaOrderCard`, … Most have one consuming page. They are page-specific business components living
in Shared; that is acceptable when they are tested there (40 bUnit component test files exist), but
**they should not be treated as the design system**. They share a repeated, local pattern: a
decorative header area (placeholder icon when there is no image) with admin icon buttons overlaid
(edit, delete, cancel, notify), metadata rows, and a row of equal-weight purple action buttons (V).
`MemberListItem` (compact member row) exists with tests but has **no consumer** (S) - it is exactly
the mobile member-list row the Members page lacks (7.4).

### 4.3 Duplicated local UI that should use or become shared

| Pattern | Where | Count | Shared direction |
| --- | --- | --- | --- |
| Page header (title, icon, subtitle, desktop actions slot) | `page-header-centered` markup | 48 pages | New `PageHeader` (14.1). |
| Desktop header actions duplicated as bottom-bar items | header `d-none d-xl-flex` + `MobileBottomNav` | ~38 pages | One action list rendered twice (`PageActions`, 14.2). |
| Raw spinners | `spinner-border` markup | 188 | `LoadingSpinner` variants. |
| Filter toolbar (search + dropdowns + switch in a flex row) | hand-built wrappers (`filter-search-container`, `filter-dropdown-container`) | ~30 pages | `FilterToolbar` layout (14.6). |
| Card admin overlay (edit/delete/cancel/notify icon buttons) | Event, Rehearsal, Avatar, Board, Album cards | 5+ card types | `CardActions` with overflow menu (14.4). |
| Label + input + help + error | `<label class="form-label">` + input | 379 labels, 83 with `for` | `FormField` (evolve `FormTextField`, 13.4). |
| Success feedback | per-page `…SuccessMessage` + `Alert` + `Task.Delay` | ~37 `…SuccessMessage` references | Shared toast host (14.5). |

---

## 5. Product / Route Inventory

62 routable pages (S). Navigation groups as rendered in `MainLayout` (S, V for Owner and anonymous).

| Area | Routes | Primary task | Primary action | Notable UI | Seen |
| --- | --- | --- | --- | --- | --- |
| **Portal / public** | `/` (home), `/request`, `/roles`, `/privacy`, `/gallery`, `/music`, `/events` (anonymous), `/calotes` | Learn about RTUB; contact | "Ver todas as Atuações", "Pedidos" | Carousel, scroll-reveal sections, sticky section pills (desktop) / section bottom bar (phone) | V (home, events, music, gallery) |
| **Account** | `/login`, `/forgot-password`, `/reset-password`, `/confirm-email`, `/profile` | Sign in; manage own profile | Entrar; edit section | Static SSR forms; profile sections with inline edit | V (login, profile) |
| **Performances (Atuações)** | `/events`, `/events/{id}/enrollments`, `/contacts`, `/discussion` | See upcoming events, sign up | **"Vou participar"** (member) / "Adicionar Atuação" (admin) | Event cards with up to 11 actions; stats, prizes, my enrollments dialogs | V (list) |
| **Rehearsals (Ensaios)** | `/rehearsals` | See rehearsals, attendance | Attendance / "Adicionar Ensaio" (admin) | Fiscal-year filter in URL; details, edit, cancel, delete, stats, "Minhas Presenças" dialogs | V |
| **Sections (Naipes)** | `/naipes`, `/naipes/config` | Section info/media | - | - | S |
| **Members** | `/members`, `/hierarchy`, `/roles`, `/hall-of-fame`, `/member/map` | Find members | "Ver Detalhes" | 108-member card grid, category/instrument filters, map, birthdays | V (members) |
| **Ranking** | `/leaderboard` | Compare XP | - | Collapsible cards instead of the standard page header | V |
| **Games** | `/my-tuno` (+ arena, stages, boss, survive, all characters), `/games` (+4 mini-games), `/bets`, `/inventory`, `/shop` | Play | Game-specific | Own visual world (PixiJS, game HUDs) | V (MyTuno home) |
| **Management (Gestão)** | `/meetings`, `/documentation`, `/logistics`, `/logistics/{id}`, `/finance`, `/finance/report/{id}`, `/mbway`, `/nerba/*`, `/requests`, `/questions` | Run the association | Create meeting / report / board | Dense dashboards, kanban, 15-dialog Meetings page | V (meetings, finance, report, logistics) |
| **Operations (Admin/Owner)** | `/emails`, `/notifications`, `/images`, `/labels`, `/users`, `/owner/tracing`, `/owner/db`, `/owner/stage-enemies`, `/owner/weapon-drink-config` | Administer | - | Tools; low traffic | S |
| **Messages** | `/messages`, `/messages/{id}` | Chat | New message | App-style full-screen shell on phone | V (list) |

---

## 6. Existing Visual System

### 6.1 Color (preserve)

Declared tokens (`1-base/variables.css`, S):

| Role | Value | Notes |
| --- | --- | --- |
| Brand primary | `#6f42c1` | Buttons, active nav, accents. White text on it: 6.5:1 (R). |
| Primary hover | `#5a379c` | Hard-coded in `buttons.css`, not a token. |
| Link / link hover | `#a88ee5` / `#c7a7ff` | 6.99:1 on the page background (R). |
| Page background | `#0f0f10` | |
| Surfaces | `#151516`, `#1a1a1b`, `#1c1c1d` (input), `#212121` (select) | Plus off-token surfaces `#1a1a2e` (23×), `#0f0f1c` (13×), `#111114` (bottom bars), `#1e1e1e`. |
| Text | `#e2e2e2` | Headings. |
| Border | `#2f2f2f` | |
| Success / warning / danger / info | `#00bc8c` / `#f39c12` / `#e74c3c` / `#007bff` | Buttons use Bootstrap's `#28a745`; badges use `#198754`; many rules hard-code `#dc3545`. |
| Theme color (browser/OS) | `#3F2A86` | Manifest and `<meta name="theme-color">`. |
| Other purples in CSS | `#6e56cf` (25×), `#7c4dff` (15×), `#8a2be2` (12×), `#651fff` (7×), `#5a32a3`, `#8e6fc7`, `#7c4ddb` | Drift, not identity: all read as "RTUB purple" but differ. |
| Gold / bronze | `#ffd700`, `#cd7f32` | Ranking medals - legitimate semantic accents. |

CSS-wide totals (S): **1,481 hex literals and 1,444 `rgb/rgba` literals vs 1,012 `var(--…)` uses**;
314 `!important`.

### 6.2 Typography

- One system font stack (`system-ui, -apple-system, "Segoe UI", Roboto, …`); `Cinzel` in two game
  rules; no web fonts (S). Keep: it is fast and neutral, and the identity lives in color and
  vocabulary.
- Measured on Rehearsals at 1440 (R): h1 40/48 w500 `#e2e2e2`; subtitle 20/30 w300 **pure white**
  (brighter than the title it sits under); section h2 28px; body 16/24.
- **73 distinct `font-size` values** (S); the most frequent are 1rem, .75rem, .875rem, 1.25rem,
  1.5rem, 1.1rem, .9rem, .8rem, .85rem, .95rem, .7rem, .65rem, .6rem. Sizes below .75rem are common
  in card metadata and bottom-bar labels (10.4px) (R).
- Global rule makes every `h1` `inline-flex` with `min-height: 44px` (S).

### 6.3 Spacing, shape, elevation

- Spacing: Bootstrap spacer utilities, but `1-base/mobile.css` **redefines `.mt-4`, `.mb-4`,
  `.mt-5`, `.mb-5`, `.py-4`, `.py-5` with `!important` on phones** and forces `.card { padding:
  .875rem }` at ≤1024px (S) - global utility semantics change by viewport.
- Radius: **42 distinct `border-radius` values** (S); top: 50%, 8px, .5rem, 12px, .375rem, 4px, 6px,
  .75rem, 999px, 1rem, .25rem, 10px, 14px.
- Shadows: **168 distinct `box-shadow` declarations** (S). Gradients: 220 `linear-gradient`;
  `backdrop-filter` 15 (S).
- Breakpoints: 20+ different `@media` widths (768, 767, 767.98, 769, 576, 575.98, 480, 375, 991,
  991.98, 992, 1023, 1024, 1025, 1199.98, 1200, 1400, 1600, …) (S).

### 6.4 Actions (buttons)

`3-components/buttons.css` defines `.btn { background-color: var(--bs-primary); border…; padding
…; font-size: 1rem }` (S). Because it sets the property directly on the base class, it overrides
Bootstrap's variant variables. Measured in the running app (R):

| Class | Renders as |
| --- | --- |
| `btn-primary`, `btn` (no variant) | Solid purple |
| `btn-outline-primary` (49 uses), `btn-outline-secondary` (71), `btn-outline-danger` (30), `btn-warning`, `btn-link` (6) | **Solid purple** - identical to primary |
| `btn-secondary` | `#343a40` |
| `btn-success` (74) | `#28a745` - white text 3.13:1 |
| `btn-danger` (93) | `#e74c3c` - white text 3.82:1 |
| `btn-sm` (267) | Still 16px at base; only some local rules shrink it |

This single rule is the root of the "everything has the same weight" look (7.1).

### 6.5 Other patterns

- **Icons:** Bootstrap Icons (local), used everywhere; sizes vary per page. Buttons: 95
  carry a `title`, 40 an `aria-label`; icon-only buttons commonly rely on `title` alone (S, R).
- **Lists/tables:** almost everything is a card grid; only 2 `<table>` elements exist (S).
- **Navigation:** top navbar (desktop) / right offcanvas (below 992px); page-level bottom bars below
  1200px (V).
- **States:** see section 12.2.
- **Focus:** `.btn:focus` and `.form-control:focus` draw a 4px `rgba(111,66,193,.5)` halo on
  `:focus` (not `:focus-visible`); 20 rules remove outlines (S). Halo vs page background ≈1.6:1 (R).
- **Motion:** 262 transitions, 28 keyframe animations, **0 `prefers-reduced-motion` rules** (S).
  Home sections start at `opacity: 0` until JavaScript adds `.in-view` (S, V).
- **Themes:** dark only. Two `prefers-color-scheme: dark` rules exist but no light theme (S). No
  theme work is proposed.

---

## 7. UX Findings

### 7.1 BLOCKER/STRUCTURAL - base button rule flattens all hierarchy

Every outline/secondary-intent button renders as solid primary purple (6.4, R). Pages that already
chose the right variant (e.g. Rehearsals card: "Ver" = `btn-outline-primary`, attendees =
`btn-outline-secondary`) still show two identical purple buttons (V). Page headers show 4 colored
buttons (2 green + 2 purple) with equal weight (V, Rehearsals/Events/Members at 1440). This must be
fixed at the foundation before any page-level hierarchy work, or every page fix will fight it.

### 7.2 HIGH - card action overload and destructive actions next to routine ones

- Event card: **11 actions** (edit, delete, push-notify, email-notify, cancel event, details,
  participants, repertoire, discussion, "Vou participar", "Não vou participar") (R). Two are
  destructive (delete, cancel) and sit beside routine ones.
- The member's **primary task** on an event - declaring participation - is two small icon-only
  buttons at the bottom of the card, below four purple icons (V, 360 and 1440).
- Rehearsal card: cancel (red), edit (white), delete (red) on every card header; 10 cards on screen
  = 20 red icons (V). Members directory: edit + delete on each of 108 member cards (V).
- Finance report activity rows: lock/edit/delete per row (V).

### 7.3 HIGH - equal-weight, repetitive calls to action in grids

Every member card has a full-width primary "Ver Detalhes" (5 identical primary buttons per row on
desktop; wraps to two lines on phones) (V). The card itself is not the link (S).

### 7.4 MEDIUM - low information density on phones

Rehearsal cards are ~313px tall each at 390px, most of it a decorative icon header (R, V). Members
use a two-column grid of ~330px cards for 108 people (V). A compact row (`MemberListItem`, already
in Shared, unused) would show 6-8 members per screen.

### 7.5 MEDIUM - inconsistent page composition

- Most pages: centered h1 + centered subtitle, actions floating right on a separate line (V).
  Leaderboard: no page header at all, collapsible cards instead (V). Report: back button + title
  (V). Messages: app shell (V).
- Section headings under the page title repeat it ("Membros" under "Membros") (V).
- Mixed icon colors on section headings (green check, purple calendar) (V, Meetings).

### 7.6 MEDIUM - terminology and language drift

- Nav "Tesouraria" opens a page titled "Finanças" (V).
- "Cancelar" closes a form; "Cancelar ensaio" cancels a rehearsal - same verb for a harmless and a
  consequential action, adjacent on the same screen (R).
- English in a Portuguese UI: NotFound/NotAuthorized texts, shared defaults "No items found",
  "Close"; MyTuno "Daily Reward", "Enemies", "Config" (S, V).
- "Sem Conexão" (offline page) vs "Ligação Perdida" (reconnect dialog) (S).
- Money formatted `€5,377.23` / `€-1,774.30` (English format) in a pt-PT product (V).

### 7.7 MEDIUM - navigation affordances that are not links

Finance report card is a clickable `div` without role or tabindex (R); Logistics "Abrir Quadro" is a
`<button>` that navigates (R). About 37 clickable non-interactive elements exist (S, approximate).
They cannot be opened in a new tab and are not keyboard reachable.

### 7.8 LOW - smaller observations

- Success is rarely confirmed: e.g. rehearsal edit closes the dialog and reloads with no message
  (S).
- The currently selected filter value is not visually distinguished in the open `FilterDropdown`
  list (V, fiscal year).
- Decorative placeholder art (calendar, music-note, board icons) fills card headers when there is
  no image (V).

---

## 8. Responsive Findings

### 8.1 Desktop (1440)

- **STRUCTURAL:** as Owner the navbar needs ~1,486px; the page scrolls horizontally and the user
  menu is off-screen (R: `scrollWidth 1486` at 1440). Owner sees 11 top-level menu entries plus the category badge and the user menu (R).
- Header actions sit right-aligned on their own line under a centered title (V).
- Single-card sections leave most of the row empty (Events current year) (V).
- Desktop dialogs anchor at the top and keep a small width for 4-field forms (V).

### 8.2 Tablet (1024, 820)

- **STRUCTURAL at 1024:** the navbar is expanded (≥992px) and overflows by ~445px: Gestão,
  Operações and the user menu are unreachable without horizontal scrolling (R: `scrollWidth 1469`).
  A Tuno member sees ~10 menu entries plus badge and user menu (S); overflow is likely (I - verify with a member
  account).
- Between 992 and 1199px the expanded top nav **and** the page bottom bar are both shown, while the
  header actions are hidden (V at 1024).
- 820: hamburger + drawer, three-column member grid, bottom bar; composition is acceptable (V).
- Installed Android PWA is locked to `portrait-primary` by the manifest (S): tablets in landscape
  cannot rotate the installed app (I - device check).

### 8.3 Phone (390, 360)

- **HIGH - Finance report at 360:** the page title is clipped ("…2025 - 202"), stat tiles and their
  edit buttons run past the right edge, activity-row badges wrap one character per line and one
  badge renders vertically (V). Content is *hidden*, not scrollable, because `html, body {
  overflow-x: clip }` (mobile.css) masks the overflow (R).
- **MEDIUM - footer hidden behind the bottom bar:** at max scroll the footer (copyright, privacy
  link) sits under the fixed bar (R: footer 767-844, bar from 773). The 4.5rem clearance rule is
  overridden by the footer's Bootstrap `py-3 !important` (R: computed `padding-bottom: 16px`).
- Primary actions wrap ("Ver Todas as Atuações", "Ver Detalhes") (V).
- No horizontal page overflow on the phone pages checked, other than content masked by the clip (R).

---

## 9. PWA / Mobile App Experience

| Topic | Finding | Evidence | Priority |
| --- | --- | --- | --- |
| Shell | Top bar with brand + purple circular hamburger (top-right); navbar scrolls away; right-side offcanvas drawer lists all items. Primary navigation is in the hardest one-handed reach zone. | V | MEDIUM |
| Best existing pattern | Messages: full-screen app shell, compact rows, bottom bar, own safe-area handling. The rest of the app should converge towards this level of intent. | V, S | - |
| Bottom bar semantics | Same component used as section nav, action bar and filter tabs; "Voltar" appears as a bar item on some pages (Report, Messages) and not on others. | V | HIGH |
| Touch targets | Card icon buttons 32-38px (R); bottom bar 65px (R); nav links 44px (R). | R | MEDIUM |
| Tap feedback | Global purple tap highlight (two different alphas declared); no pressed state on cards. Server round-trip per tap (Blazor Server) makes missing immediate feedback more noticeable. | S, I | MEDIUM |
| Safe areas | `apple-mobile-web-app-status-bar-style: black-translucent` without `viewport-fit=cover` in the viewport meta (S). `env(safe-area-inset-*)` is used in ~30 rules but may resolve to 0 without `viewport-fit=cover`, so the navbar can sit under the status bar/notch in the installed iOS app. | S, I (needs a real iPhone) | MEDIUM |
| Theme color | `#3F2A86` status bar over a near-black navbar. | S, I | LOW |
| Dialogs | Full-screen sheets with a bottom action bar (Cancelar left, Guardar right) - consistent and thumb-friendly (V). Back arrow on the right of the header; an extra "Voltar" footer on read-only sheets. | V | MEDIUM |
| Back button | Opening a dialog adds no history entry. Hardware/browser Back with a dialog open changes the page underneath (it removed `?fy=` and re-filtered) and leaves the dialog open (V). From a dialog reached from another page, Back leaves the page and discards the dialog (I). | V, I | HIGH |
| Keyboard / forms | Inputs are 16px (no iOS zoom) (R). No `autocomplete`, `inputmode` or `enterkeyhint` anywhere (S); no `type="tel"`, 3 `type="email"` (S). Virtual keyboard behaviour with the fixed bottom action bar not testable here (C). | R, S | MEDIUM |
| Loading | Page data loads after the circuit connects; empty states can flash before data (I). 38 global scripts incl. PixiJS and 3 game bundles on every page (S) lengthen first load on phones. | S, I | MEDIUM |
| Update | Update toast (PT, `role="alert"`) - good. "Atualizar" reloads immediately; an open unsaved form is discarded (S). | S | MEDIUM |
| Reconnect / offline | Portuguese reconnect dialog with reload; `offline.html` fallback. Server exceptions also surface as "Ligação Perdida" (no error boundary) (S, I). | S | MEDIUM |
| Prompts | Push prompt, login popup and Play Store prompt all mount on the home page (S). Not triggered in this session (C). | S | LOW |
| Pull-to-refresh | Vertical overscroll is not contained; in Android standalone a pull reloads and drops form state (I). | I | LOW |

---

## 10. Accessibility Findings

This is not a WCAG conformance statement. Observed issues only.

| # | Issue | Evidence | Priority |
| --- | --- | --- | --- |
| A1 | Dialogs: no `role="dialog"`/`aria-modal`/`aria-labelledby`; focus not moved/trapped/restored; background not inert; Escape does not close. | R, V | HIGH |
| A2 | Form labels not associated: 296 of 379 `<label>` have no `for` (S). Rehearsal edit: 3 of 4 fields unnamed; Meeting create: all 5 fields unnamed; Login inputs named only by placeholder (R). Required fields not exposed (`aria-required`/`required`) (R). | S, R | HIGH |
| A3 | 41 of 62 routable pages set no `<PageTitle>` and `App.razor` has no default `<title>`; tabs and assistive tech get the raw URL (S, R via tab titles). | S, R | HIGH |
| A4 | Contrast (R): `text-secondary` 1.67:1 (15 uses); white on `btn-success` 3.13:1 (74), on `btn-danger` 3.82:1 (93), on `bg-warning` badges 2.19:1 (28); `text-danger`/`text-success` 4.23:1 on the page background. Focus halo ≈1.6:1. | R | HIGH |
| A5 | `text-muted` is overridden to pure white (240 uses) - no contrast problem, but metadata loses its visual hierarchy. | R, S | MEDIUM |
| A6 | Focus indicator on `:focus`, not `:focus-visible`; 20 rules remove outlines. | S | MEDIUM |
| A7 | No skip link; home page has no `h1` (only h2) while `FocusOnNavigate` targets `h1`. | R | MEDIUM |
| A8 | Icon-only actions commonly named only by `title` (95 buttons carry `title`, 40 `aria-label`); card icon targets 32-38px. | S, R | MEDIUM |
| A9 | `MobileBottomNav`: fixed "Navegação do portal" label on action bars; `aria-selected` on buttons; 10.4px labels. | R, S | MEDIUM |
| A10 | Clickable `div` cards without role/tabindex (Finance report card; ~37 sites). | R, S | MEDIUM |
| A11 | Status changes are not announced: 1 `aria-live` in the whole UI (SW toast); success/error messages rely on visual `Alert`. | S | MEDIUM |
| A12 | Home carousel auto-advances with no pause control. | S | MEDIUM |
| A13 | No `prefers-reduced-motion` handling; scroll-reveal sections are invisible until JS runs. | S, V | LOW |
| A14 | `SearchBar` (`type="text"`, placeholder-only name) and `FilterDropdown` (no filter name) lack labels. | R | MEDIUM |
| A15 | Participation state conveyed by green/red icon buttons without text on the card. | V | LOW |
| A16 | Image failure: several components show raw alt text clipped inside circles/covers, and overlaid admin buttons are cut by the collapsed image box (Members, Leaderboard, Music) (V; triggered locally by CSP-blocked media, but the same happens for any failed image). `loading="lazy"` on 19 of 177 images (S). | V, S | LOW |

---

## 11. Form / Data-Loss Risks

| # | Scenario | Result | Evidence | Priority |
| --- | --- | --- | --- | --- |
| F1 | Dirty edit dialog → **Cancelar** (or the header back arrow) | Closes immediately, edits discarded, no prompt. Re-opening shows the saved values. | V (Rehearsal edit) | HIGH |
| F2 | Dirty dialog → **Escape** | Nothing happens (focus is outside the dialog). Safe but inconsistent. | V | LOW |
| F3 | Dirty dialog → **backdrop click** | Nothing happens. Safe. | V (desktop) | - |
| F4 | Dialog open → **browser/hardware Back** | Page state underneath changes; dialog stays. Leaving the page drops the dialog and its data silently. | V / I | HIGH |
| F5 | **Navigate away / reload / close tab** with a dirty form | No guard anywhere: 0 `NavigationLock`, 0 `beforeunload`, 0 `RegisterLocationChangingHandler`. | S | HIGH |
| F6 | **Circuit loss** (phone backgrounded, network drop) | Blazor Server keeps form state on the server only while the circuit survives; after expiry the reload starts empty. | S, I | MEDIUM |
| F7 | **SW update** "Atualizar" with a form open | Immediate reload, form discarded. | S | MEDIUM |
| F8 | **Double submit** | Inconsistent: 45 `disabled="@is…"` bindings exist, but e.g. Rehearsals `SaveEdit` and `ConfirmDelete` have no busy state or try/catch - a double click runs twice; an exception ends the circuit. | S | HIGH |
| F9 | **Validation** | Mix of native HTML validation (login, English browser bubble), `ErrorDisplay` summaries and ad-hoc messages. No shared per-field error pattern. Not exercised on create forms (submitting could create data and send notifications). | V (login), S | MEDIUM |
| F10 | **Save/Cancel placement** | Consistent: desktop footer right (Cancelar, Guardar); phone bottom bar (Cancelar left, Guardar right). Keep. | V | - |
| F11 | **Success confirmation** | Often absent after save (dialog just closes). | S | MEDIUM |

Guards are **not** implemented in Task 001. Which forms get a discard confirmation is listed as an
owner decision (18).

---

## 12. Component Consistency Issues

### 12.1 Systemic

1. Base `.btn` rule overrides all variants (6.4) - STRUCTURAL.
2. Tokens exist but are bypassed: 2,925 color literals vs 1,012 token uses; 7 near-identical
   purples; 3 greens and 2 reds for the same semantic roles (6.1).
3. 42 radii, 168 shadows, 73 font sizes, 20+ breakpoints (6.2-6.3).
4. `mobile.css` rewrites Bootstrap utilities and all `.card` padding by viewport with `!important`
   (6.3), and clips overflow globally (8.3) - both make page CSS fight the base layer.
5. Page header and bottom bar are re-implemented per page (4.3).
6. Header actions and bottom-bar actions are separate lists (4.1 `MobileBottomNav`).

### 12.2 States

| State | Current | Quality |
| --- | --- | --- |
| Loading | `LoadingSpinner` (67) + raw spinners (188); no skeletons except songs | Inconsistent |
| Empty | `EmptyState` (92) | Good, needs PT defaults and one variant rule |
| Error (validation) | `ErrorDisplay`, native bubbles, ad-hoc text | Inconsistent |
| Error (runtime) | None - circuit ends → reconnect dialog | Missing |
| Success | Occasional inline `Alert` + timer | Mostly missing |
| Disabled | Lighter purple for primary only | Weak |
| Read-only | Lock icons in Finance | Local |
| No permission | English NotAuthorized text; items hidden from nav | Weak |
| Destructive confirmation | `ConfirmDialog` | Good; needs danger variant + busy state |
| Long-running | Per page | Inconsistent |

### 12.3 Deterministic scan (Impeccable detector, A)

19 warnings, 0 errors: 13 "side-stripe" accent borders (`border-left: 3-4px`), 4 layout-property
transitions (`transition: width` / `max-height`), 2 bounce easings. Several are defensible and
should stay: medal colors on Leaderboard ranks (`#FFD700`, `#CD7F32`), quoted-reply bars in
Messages, the history timeline accent. Candidates to revisit: `input-groups-misc.css` (2),
`dynamic-style-classes.css` (2), `misc-components.css`, `questions.css`, `Emails.razor.css`, and
the width transitions in `rank-card.css`, `MyTunoHome.razor.css`, `dynamic-style-classes.css`,
`AuditLog.razor.css`. None is high priority.

---

## 13. Proposed Visual Refinement Direction

RTUB stays a dark, near-black product with a single confident purple. The refinement is about
**discipline**: one primary per context, quiet secondary actions, fewer colors doing more work,
clear surfaces, and mobile screens composed for thumbs. No new palette, no new font, no light
theme, no glassmorphism, no decorative gradients added.

### 13.1 Principles applied to RTUB

- **Task first.** Each page leads with its task (upcoming performances, next rehearsal, the member
  you are looking for), not with admin tooling.
- **One obvious primary.** Purple fill is reserved for the primary action of a context. Everything
  else is outline/ghost.
- **Destructive last.** Delete/cancel move into an overflow menu or the end of a dialog, never
  beside routine actions on a card.
- **Shared over local.** A pattern used on 3+ pages lives in RTUB.Shared.
- **Mobile is an app.** Bottom bars carry the page's actions consistently; lists are dense;
  sheets behave with Back.
- **Accessibility is built into components**, not patched per page.

### 13.2 Color - formalize, do not replace

Introduce semantic tokens that point at **existing** values:

| Token | Value (existing) | Use |
| --- | --- | --- |
| `--rtub-bg` | `#0f0f10` | Page |
| `--rtub-surface-1/2/3` | `#151516` / `#1a1a1b` / `#1c1c1d` | Cards, raised panels, inputs |
| `--rtub-border` | `#2f2f2f` | Dividers |
| `--rtub-text` | `#e2e2e2` | Body and headings |
| `--rtub-text-muted` | `#aaaaaa` (already the footer color; 8.2:1 est.) | Metadata, subtitles |
| `--rtub-primary` / `-hover` | `#6f42c1` / `#5a379c` | Primary fill |
| `--rtub-primary-soft` | `rgba(111,66,193,.15)` (existing usage) | Selected/hover backgrounds |
| `--rtub-accent-text` | `#a88ee5` | Links, active text, focus ring |
| `--rtub-danger-fill` | `#dc3545` (already used 27×; white 4.53:1) | Filled destructive buttons/badges |
| `--rtub-danger-text` | `#e74c3c` (current token; ≈5:1 on bg est.) | Red text on dark |
| `--rtub-success-fill` | `#198754` (already used 13×; white 4.53:1) | Filled success buttons/badges |
| `--rtub-success-text` | `#00bc8c` (current token; ≈7.8:1 est.) | Green text on dark |
| `--rtub-warning` | `#f39c12` with **dark text** `#212529` (≈7:1 est.) | Warning badges |

Smallest contrast fixes, all within the current palette: swap fills to the Bootstrap reds/greens
RTUB already uses; use the lighter existing red/green for text; dark text on warning; muted text
`#aaa` instead of white; `text-secondary` mapped to muted. The seven drifting purples collapse onto
`#6f42c1` / `#5a379c` / `#a88ee5` / `#3F2A86`. Ratios marked "est." are calculated from the hex
values and must be re-measured in Phase 1.

### 13.3 Typography, spacing, shape, elevation

- Keep the system stack. Type scale: 12 / 14 / 16 / 20 / 24 / 32 / 40, weights 400/500/600.
  Nothing interactive below 12px; bottom-bar labels ≥11px.
- Subtitles use `--rtub-text-muted`, never brighter than the title.
- Spacing on the Bootstrap 0.25rem scale; page gutter 16px phone / 24px desktop; section gap 32px;
  card padding 16px. Remove viewport-dependent redefinitions of utilities.
- Radius tokens: 6px (controls), 10px (cards), 16px (sheets/dialogs), 999px (pills/avatars).
- Three elevation levels (flat border, raised card, overlay); surfaces separate by tone before
  shadow.
- Breakpoints: Bootstrap's 576/768/992/1200/1400 only; the bottom-bar breakpoint and the nav
  collapse breakpoint must be the same value.

### 13.4 Actions and forms

- Restore Bootstrap variant behavior (primary = fill; secondary = outline/ghost; danger = fill only
  inside confirmations); `btn-sm` actually small.
- `IconButton` with a required accessible label and a 44px hit area (visual size can stay smaller).
- Cards: at most one visible primary action; routine secondary actions as quiet icon buttons;
  management actions (edit, notify, cancel, delete) in a `⋯` overflow menu with delete last and
  separated.
- Events: participation becomes the visible primary action for members ("Vou" / "Não vou" as a
  labelled segmented control); counts become metadata, not buttons.
- Forms: `FormField` wrapper = label (associated), control, help, per-field error, required marker
  (visual + `aria-required`). Save/Cancel positions stay as they are today (F10).

### 13.5 Lists and tables

- Phones: compact rows for directories (members, meetings, requests) and dense cards for time lists
  (rehearsals, events): date block + title + one line of metadata + one action.
- Desktop: keep card grids where imagery matters (events, albums, gallery); use rows/tables for
  administrative lists (finance activities, users, audit).
- Money in pt-PT format (`5 377,23 €`), with sign and color plus text for negatives.

### 13.6 Dialogs

Phone: full-screen sheet (keep), back arrow on the **left**, title, one bottom action bar; no
duplicate "Voltar" buttons. Desktop: centered, sized by content (sm/md/lg), close on the right.
Both: `role="dialog"`, labelled, focus managed, Escape closes when clean, dirty guard when not, Back
closes the sheet first.

### 13.7 Navigation

- Keep the existing groups and items. Collapse to the drawer below 1400px (`navbar-expand-xxl`) and
  tighten item spacing; move the category badge and nickname into the avatar menu so the bar fits
  at 1440 for Owner.
- Stronger current-location state in the drawer and in dropdown parents (the parent of an active
  child is not marked today) (I).
- Page bottom bars: one role only - the page's primary actions (max 4), consistent order, no
  "Voltar" item (Back lives in the header). Section navigation on the home page becomes a labelled
  tab bar with `aria-current`.

### 13.8 Cards

Consistent padding (16px), one radius, one surface tone, image area only when there is an image
(no placeholder art taking half the card), title → metadata → actions order, whole-card link where
the card opens a detail.

### 13.9 States and motion

Shared loading (spinner + skeleton), empty, error (boundary + inline), success (toast) and
confirmation patterns. Motion only for dialog/sheet enter/exit, menu open, toast, and pressed
feedback, 150-250ms ease-out; every animation has a `prefers-reduced-motion` alternative; content
is never hidden waiting for a scroll-reveal.

### 13.10 Out of the polish scope by default

MyTuno and the mini-games have their own game visual world (HUDs, PixiJS, Cinzel). Only their shell
integration (header, safe areas, bottom bar, titles, language) is in scope unless the owner decides
otherwise (18).

---

## 14. RTUB.Shared Improvement Opportunities

Improve first, create only where repetition proves it.

| Opportunity | Kind | Where duplicated today | Why shared | Reuse proof |
| --- | --- | --- | --- | --- |
| 14.0 `Modal` / `ConfirmDialog` / `DetailsModal` refinement | **Improve existing** | 53 + 30 + 11 files | A11y, dirty guard, Back integration fixed once for 129 dialogs | Already used everywhere |
| 14.1 `PageHeader` (title, icon, subtitle, back, actions slot) | New | `page-header-centered` in 48 pages | One hierarchy and responsive rule for every page | 48 pages |
| 14.2 `PageActions` (one list → desktop header buttons + mobile bottom bar) | New, **wraps existing `MobileBottomNav`** | ~38 pages keep two lists | Ends label/order drift; fixes bar semantics once | ~38 pages |
| 14.3 `IconButton` | New | 95 buttons named by `title` vs 40 by `aria-label`; card overlays | Label + 44px target by construction | 20+ components |
| 14.4 `CardActions` / `OverflowMenu` | New | Event, Rehearsal, Avatar, Board, Album, Report cards | Hierarchy + destructive separation | 6+ cards |
| 14.5 Toast host + scoped feedback service | New | ~37 ad-hoc `…SuccessMessage` references, 28 `Task.Delay` timers | Announced (`aria-live`), consistent success/error feedback | App-wide |
| 14.6 `FilterToolbar` layout | New (layout only) | ~30 pages | Consistent search + filter + toggle arrangement and wrapping | ~30 pages |
| 14.7 `FormField` | **Evolve `FormTextField`/`FormTextArea`** | 379 labels | Label association, required, per-field error | 50+ forms |
| 14.8 `EmptyState`, `LoadingSpinner`, `ErrorDisplay`, `Alert` | **Improve existing** | 92 / 67 (+188 raw) / 43 / 38 | PT defaults, variants, live regions | Existing |
| 14.9 `SearchBar`, `FilterDropdown` | **Improve existing** | 49 / 29 | Labels, `type="search"`, selected state | Existing |
| 14.10 `MemberListItem` | **Adopt existing** | Members page on phones | Dense mobile directory | Members, Hall of Fame, pickers |
| 14.11 Error boundary component | New (thin) | none | Stop exceptions from ending the circuit silently | App-wide |

Not recommended: generic grid/stack/box primitives, a theming abstraction, wrappers around
Bootstrap utilities, or moving single-page domain cards around.

---

## 15. Architecture Assessment

### A. Visual / UI issues (fix with CSS + components)

Base `.btn` override; token bypass and color drift; contrast pairs; focus ring; nav overflow at
1024-1440; global `overflow-x: clip` masking layout bugs; utility redefinition in `mobile.css`;
card action overload; bottom-bar semantics; footer clearance; low mobile density; image-failure
states; motion without reduced-motion; language/terminology drift.

### B. Blazor implementation issues (fix inside Blazor)

- Very large page components (7 pages > 2,000 lines, one with 15 dialogs) that mix layout, dialogs
  and side-effect orchestration (push, audit) - refactor opportunistically **only where a UI task
  already touches the code**; extracting dialog components per page is the natural unit.
- No `ErrorBoundary`; handlers without try/catch/busy state.
- No `NavigationLock` usage.
- Labels not bound to inputs; clickable `div`s; buttons used for navigation.
- `ShouldRender` overrides in shared components (`Modal`, `EmptyState`, `StatusBadge`, …) that
  compare only some parameters - safe today, but a trap when adding parameters (e.g. new `Modal`
  aria/guard parameters must be included).
- RTUB.Shared references RTUB.Application and some shared components fetch data themselves. Keep
  new shared presentation components free of service injection.
- 38 global scripts incl. game engines on every page - load game/map/cropper scripts only on the
  pages that need them.
- 76-file CSS `@import` chain is fetched as 77 separate stylesheet requests on first load;
  consider bundling when the foundation layer is reorganized.

### C. Genuine architecture limitations

- **Blazor Server needs a live circuit.** Every interaction is a server round-trip, and state lives
  on the server. Effects: tap latency on mobile networks, form loss when a backgrounded PWA's
  circuit expires, reconnect dialog on flaky connections. Mitigations inside the current
  architecture: immediate CSS pressed/disabled feedback, busy states, NavigationLock + dirty guards,
  short forms, and (owner decision) optional draft persistence for long forms. This does **not**
  justify a stack change.
- **Static SSR layout with per-page interactivity.** Layout-level UI that needs state (toast host,
  app-level bottom tab bar) must be its own interactive component; state shared across pages goes
  through circuit-scoped services, and enhanced navigation re-renders the static layout. Workable,
  but it must be designed deliberately (Phase 3).

Nothing found prevents the goals of this refactor.

---

## 16. Testing Strategy for Future Refactor Tasks

### 16.1 Baseline (A)

| Command | Result |
| --- | --- |
| `dotnet restore` | OK |
| `dotnet build --no-restore --configuration Release` | Build succeeded, 0 warnings, 0 errors |
| `dotnet test --no-build --configuration Release --results-directory <scratch> --report-xunit-trx` | **4,867 total, 4,801 passed, 0 failed, 66 skipped** (all five projects passed) |

Skipped tests are pre-existing and deliberate. Skip attributes by reason in source: 22 "Modal
renders outside component fragment" (page tests that open dialogs), 14 `UnreadMessagesBadge`
interactive init, 20 `UserManager.Users` IQueryable mocking (Roles, Members), 2 SQLite concurrency,
2 JS/dispatcher prompt flows; plus Linux-only shell self-tests skipped on Windows. The dialog skips
are relevant to this refactor: page-level dialog behavior is currently untested.

### 16.2 What protects the UI today

- 40 bUnit component test files in `RTUB.Shared.Tests` and page tests in `RTUB.Web.Tests`; PWA
  contract tests (`PwaManifestTests`, `OfflineReachabilityTests`, `ServiceWorkerReliabilityTests`).
- **Coupling to markup:** 984 `Markup.Should().Contain(...)` string assertions; selectors on class
  names and titles (`button.modal-close-arrow`, `modal show d-block`, `modal-sm`,
  `button.btn-primary`, `[title='Eliminar']`, `.kanban-card`, …). Refactoring `Modal`, icon buttons
  or card markup will break these tests without a behavior change.
- No browser tests (no Playwright/Selenium), no automated accessibility checks, no visual
  snapshots.

### 16.3 Rules for every later phase

1. Before touching a shared component, move its tests from class/markup strings to behavior:
   roles, accessible names, labels (`FindByRole`-style queries via bUnit + AngleSharp selectors on
   `role`/`aria-*`), and callbacks. Keep assertions on parameters → rendered behavior.
2. New component APIs get bUnit tests for: accessible name, keyboard (Escape/Enter), focus target,
   busy/disabled state, dirty guard callbacks.
3. `dotnet build` + affected test projects per change; full CI command before any "ready" claim.
4. **Visual validation is separate from tests** and mandatory per phase: 1440, 1024, 820, 390, 360;
   populated + empty + dialog open + keyboard focus + one destructive confirmation; record what was
   opened. Use a local run with the safety overrides from 2.1.
5. Re-run the Impeccable detector on changed markup/CSS once per phase.
6. Optional (owner decision): a small committed Playwright smoke (screenshots at 5 viewports +
   axe-core) for the shell and 5 key pages.

---

## 17. Implementation Roadmap

Order follows dependencies: the foundation layer decides how every later screen looks, the shell
decides every page's frame, shared components decide every dialog and form, and only then are
workflows rebuilt on top.

### Phase 1 - Visual foundations (tokens and base-layer repair)

- **Objective:** make the base layer trustworthy: semantic tokens on existing colors (13.2), button
  variants restored, `btn-sm` real, muted/secondary text fixed, focus-visible ring, contrast fixes,
  type/radius/elevation tokens, breakpoint alignment, remove `mobile.css` utility redefinitions and
  the global `overflow-x: clip` mask (replace with per-component fixes).
- **Affected:** `wwwroot/css/1-base/*`, `3-components/buttons.css`, `forms.css`, badges/alerts;
  touch-ups in pages that relied on the old overrides.
- **Dependency:** none. Must come first.
- **UX gain:** hierarchy appears everywhere at once (outline secondaries), readable metadata,
  accessible contrast and focus.
- **Tests:** build + Shared/Web tests; update class-string tests touched.
- **Visual validation:** before/after at 5 viewports on Home, Events, Rehearsals, Members, Finance
  report, one dialog; contrast re-measured.

### Phase 2 - App shell and navigation

- **Objective:** navbar fits at every width (expand at xxl, compact items, badge/nickname into the
  avatar menu), skip link, default `<title>` + `PageTitle` on all 41 missing pages, PT
  NotFound/NotAuthorized, footer clearance, safe-area review (`viewport-fit=cover` decision),
  drawer current-state, theme-color check.
- **Affected:** `App.razor`, `MainLayout.razor`, `2-layout/*`, `1-base/mobile.css`, all pages
  (titles only).
- **Dependency:** Phase 1 tokens.
- **UX gain:** no horizontal scroll at 1024/1440, reachable user menu, orientation for assistive
  tech, correct app chrome in the installed PWA.
- **Tests:** layout/title tests; integration page tests still green.
- **Visual validation:** Owner and a member account at 1440/1200/1024/992/820/390/360; installed
  PWA on one iPhone and one Android device.

### Phase 3 - RTUB.Shared core refinements

- **Objective:** `Modal`/`ConfirmDialog`/`DetailsModal` accessibility + focus + Escape + dirty-guard
  hook + Back integration; `PageHeader`; `PageActions` over `MobileBottomNav`; `IconButton`;
  `OverflowMenu`/`CardActions`; toast host + feedback service; `FormField`; `EmptyState`,
  `LoadingSpinner`, `ErrorDisplay`, `Alert`, `SearchBar`, `FilterDropdown` refinements; error
  boundary.
- **Affected:** `src/RTUB.Shared/Components/**`, `modals.css`, layout (toast host).
- **Dependency:** Phases 1-2.
- **UX gain:** every dialog, header, action bar and field improves at once in later adoption.
- **Tests:** behavior-first rewrites of the affected bUnit tests (16.3); new tests for focus,
  Escape, guards, labels.
- **Visual validation:** component states in one page each (dialog open, confirm, toast, empty,
  loading, error) at 1440 and 390; keyboard-only walkthrough.

### Phase 4 - Forms, dialogs and data safety

- **Objective:** adopt the Phase 3 dialog/field/feedback components in create/edit flows;
  `NavigationLock` on page forms; dirty guard in dialogs (per the owner's policy, 18);
  busy/disabled states and try/catch around saves and deletes; success feedback; consistent
  validation display; `autocomplete`/`inputmode`/`type` on relevant fields.
- **Affected:** the highest-traffic forms first - Rehearsals, Events, Meetings, Profile, Finance
  report/activities, Logistics, Requests.
- **Dependency:** Phase 3.
- **UX gain:** no silent data loss, no double submits, clear feedback.
- **Tests:** bUnit for guards/busy states on each migrated form.
- **Visual validation:** dirty-cancel, Escape, Back, reload, double-click, validation error on
  phone and desktop.

### Phase 5 - Primary member workflows

- **Objective:** recompose Events (participation as the primary action, admin actions to overflow),
  Rehearsals (dense date-first cards, attendance), Members (compact phone list via
  `MemberListItem`, card-as-link on desktop), Profile, Leaderboard (standard page header), Home
  portal (h1, carousel pause, reveal without hiding content).
- **Dependency:** Phases 3-4.
- **UX gain:** the tasks members do weekly become obvious and fast on phones.
- **Tests:** page tests updated to behavior queries.
- **Visual validation:** each page populated + empty at 5 viewports, member and admin accounts.

### Phase 6 - Management and dense screens

- **Objective:** Finance list/report (360px layout, pt-PT money, row actions), Meetings,
  Logistics list/board, Questions, Requests, MBWAY, Nerba, Documentation, Operations pages:
  tables/rows where they fit, filter toolbar, overflow actions.
- **Dependency:** Phases 3-4 (5 for shared card conventions).
- **UX gain:** administrative work is scannable and usable on phones.
- **Tests:** page tests; no business logic change.
- **Visual validation:** 1440 + 360 minimum per page, long text and large data sets.

### Phase 7 - PWA and mobile polish

- **Objective:** pressed/tap feedback, 44px targets, bottom-bar consistency audit across all 38
  pages, reduced motion, update toast deferral while a form is dirty, sequencing of home prompts,
  page-scoped script loading (games/map/cropper), overscroll containment where forms live,
  image-failure fallbacks, `loading="lazy"`.
- **Dependency:** Phases 2-6.
- **UX gain:** the installed app feels deliberate and fast.
- **Tests:** PWA contract tests stay green; JS changes covered where testable.
- **Visual validation:** installed PWA on real iOS and Android (safe areas, keyboard over bottom
  bars, Back, update, offline, reconnect).

### Phase 8 - Consistency and accessibility closeout

- **Objective:** sweep remaining raw spinners, color literals and radii; language/terminology pass;
  detector re-run; contrast and keyboard sweep of every route; document the resulting system in a
  `DESIGN.md`-style reference.
- **Dependency:** all previous phases.
- **UX gain:** coherent product, no leftover local styles.
- **Tests:** full CI suite.
- **Visual validation:** all routes at 1440 and 390; the five-viewport set on key pages.

---

## 18. Deferred Product Decisions

1. **Navigation grouping.** Keep all 11 top-level menu entries (proposed: yes, with a later collapse
   breakpoint), or regroup (e.g. Classificação under Membros, Galeria/Música under a Media group)?
2. **Mobile global navigation.** Keep hamburger + drawer as the global nav (proposed), or introduce
   an app-level bottom tab bar (a new navigation concept)?
3. **Unsaved-changes policy.** Which forms get a discard confirmation (proposed: every create/edit
   with more than one field), and should long forms keep local drafts across circuit loss?
4. **Money and date format.** Switch UI money to pt-PT (`5 377,23 €`)? Affects every finance screen.
5. **MyTuno / games scope.** Shell-only integration (proposed) or full visual alignment?
6. **Installed orientation.** Keep `portrait-primary` in the manifest or allow landscape on tablets?
7. **Browser test tooling.** Commit a Playwright + axe smoke suite (adds a toolchain and CI time) or
   keep visual validation manual?

## 19. Audit Limitations

- **Remote media** (slideshow, album covers, most avatars) did not load locally: the local
  Development Content-Security-Policy allows only this environment's R2 public domain, and the
  sanitized snapshot references production objects. Image-heavy layouts were judged with
  placeholders/fallbacks. Recorded as a deferred item in `STATE.md`.
- Only **Owner** and **anonymous** views were rendered; member/Leitão/Caloiro navigation is
  source-only.
- **No real devices.** Installed standalone mode, iOS safe areas, virtual keyboard, pull-to-refresh,
  hardware Back on Android, push prompts and the SW update toast were not exercised.
- **No writes.** Validation on create forms, success feedback after saves, and error paths were
  inferred from source rather than triggered.
- The fiscal year 2026-2027 had no rehearsals or meetings yet; populated states used 2025-2026.
- Chromium only; no Safari/Firefox rendering check.
- Contrast ratios marked "est." in 13.2 are computed from hex values, not measured on screen.
- Counts from `grep` over single lines (e.g. clickable `div`s, labels) are approximate where tags
  span several lines.

---

## 20. Implemented: Visual Foundations (unit 033, Task 002)

Durable facts from implementing roadmap Phase 1. Sections 1-19 are the Task 001 audit and stay as
recorded; this section is the contract later UI tasks build on.

### 20.1 Root causes

| Problem (audit ref) | Root cause | Fix |
| --- | --- | --- |
| Every variant looked like primary (6.4, 7.1) | `3-components/buttons.css` set `background-color`, `border`, `color`, `padding` and `font-size` directly on `.btn`. Bootstrap 5.3.3 paints variants and sizes through `--bs-btn-*` variables, so the direct properties won over every variant and over `btn-sm`/`btn-lg`. A second rule, `.btn:hover { background-color: #5a379c }`, turned every button purple on hover. | `.btn` is left to Bootstrap; RTUB variants set only `--bs-btn-*` variables (20.3). |
| Page content cut off on phones (8.3) | `<main class="content flex-fill">` is a flex item of `.layout.d-flex`. With the default `min-width: auto` it grew to its widest child, so pages were laid out wider than the screen (392px at 390, 390px at 360), and `overflow-x: clip` on `html`, `body` and `.content-main` hid the result. | `.content-main { min-width: 0 }`; the clip rules are gone (20.5). |
| Badges split one character per line (8.3, Finance report) | Phone-only `.badge { word-break: break-word }` in `3-components/list-groups.css`. | Badges wrap between words only. |
| Footer hidden behind the bottom bar (8.3) | The 4.5rem clearance existed only at ≤768px (the bar shows below 1200px), was overridden by the footer's `py-3 !important`, and a later `@supports` rule reset it to the safe-area inset. | One rule in `2-layout/footer.css` (20.5); footer markup `py-3` → `pt-3`. |
| `MobileBottomNav` and phone dialog footers had no bottom padding | `padding-bottom: env(safe-area-inset-bottom, 0.35rem)`: the fallback applies only when the variable is undefined, and it is `0` on devices without a home indicator. | `max(0.35rem, env(safe-area-inset-bottom, 0px))`; same for the dialog footer. |
| Busy buttons grew on phones | Phone-only `.spinner-border { width/height: 2rem }` also hit `spinner-border-sm` inside buttons. | Rule now skips `.spinner-border-sm`. |

CSS order is unchanged: `bootstrap.min.css` (5.3.3) → `site.css` (76 `@import`s: base, layout,
components, pages, overrides) → `RTUB.styles.css` (scoped bundles). Nothing moved between files.

### 20.2 Foundation tokens (`1-base/variables.css`)

Every value already existed in RTUB CSS or is a Bootstrap 5.3 value RTUB already relies on. No new
color was introduced.

| Token | Value | Source / use |
| --- | --- | --- |
| `--rtub-bg` | `#0f0f10` | = `--bs-body-bg` |
| `--rtub-text` | `#e2e2e2` | = `--bs-body-color` |
| `--rtub-text-muted` | `#aaaaaa` | Existing footer text color; now `text-muted` and `text-secondary` |
| `--rtub-border` | `#2f2f2f` | = `--bs-border-color` |
| `--rtub-primary` / `-hover` / `-active` | `#6f42c1` / `#5a379c` / `#5a32a3` | Existing primary and its two existing hover shades |
| `--rtub-accent` | `#a88ee5` | Link color: purple text and outlines on the dark page |
| `--rtub-accent-border` | `#8e6fc7` | Existing disabled-primary purple; outline-primary border |
| `--rtub-focus-ring` | `#a88ee5` | Keyboard focus ring |
| `--rtub-success-fill` / `--rtub-success-text` | `#198754` / `#00bc8c` | Bootstrap success (white text) / existing RTUB success token (text on dark) |
| `--rtub-danger-fill` / `--rtub-danger-text` | `#dc3545` / `#e74c3c` | Bootstrap danger (white text) / existing RTUB danger token (text on dark) |
| `--rtub-warning` / `-hover` / `--rtub-on-warning` | `#f39c12` / `#e67e22` / `#212529` | Existing warning token, existing orange, Bootstrap dark text |
| `--rtub-neutral-fill` / `-hover` / `--rtub-neutral-border` | `#343a40` / `#495057` / `#6c757d` | Existing `--bs-secondary`, Bootstrap gray-700, existing gray |
| `--rtub-radius-sm` / `--rtub-radius` / `-lg` / `-pill` | `.375rem` / `.5rem` / `.75rem` / `50rem` | The most used radii (6.3); for upcoming shared components |
| `--rtub-bottom-nav-clearance` | `5rem` | Bar is ~71px, ~85px with two-line labels (Meetings) |

Existing literals were **not** migrated; code moves to the tokens when a later task touches it. A
scoped (`.razor.css`) file that uses a new token also gives the literal as fallback, e.g.
`var(--rtub-focus-ring, #a88ee5)`, because scoped bundles are cache-busted and the global
sheets are not (20.7).

### 20.3 Button-system contract

- `.btn` = Bootstrap's shape, type scale, spacing and interaction. **No RTUB rule sets
  `background`, `border`, `color`, `padding` or `font-size` on `.btn` itself** (guarded by
  `VisualFoundationCssTests.BaseButtonRule_LeavesColorsAndSizingToVariants`).
- Color comes only from a variant, which sets `--bs-btn-*` variables. Measured in the running app
  (text contrast against the button, border against the page):

| Variant | Normal | Hover / active | Text contrast |
| --- | --- | --- | --- |
| `btn-primary` | `#6f42c1` fill, white | `#5a379c` / `#5a32a3` | 6.51 |
| `btn-secondary` | `#343a40` fill, white | `#495057` | 11.51 |
| `btn-outline-primary` | `#a88ee5` text, `#8e6fc7` border (4.79) | fills `#6f42c1` | 6.99 |
| `btn-outline-secondary` | `#e2e2e2` text, `#6c757d` border (4.09) | fills `#343a40` | 14.79 |
| `btn-success` / `btn-danger` | Bootstrap `#198754` / `#dc3545`, white | Bootstrap | 4.53 / 4.53 |
| `btn-outline-danger` / `btn-outline-success` | `#e74c3c` / `#00bc8c` text and border | fill with the `-fill` color | 5.01 / 7.82 |
| `btn-warning` | `#f39c12` fill, `#212529` text | `#e67e22` | 7.03 |
| `btn-outline-warning` | `#f39c12` text and border | fills `#f39c12`, dark text | 8.74 |
| `btn-link` | `#a88ee5`, underlined, no box (Bootstrap via `--bs-link-color`) | `#c7a7ff` | 6.99 |
| `btn-light`, `btn-outline-light`, `btn-info`, `btn-outline-info` | unchanged (Bootstrap / existing RTUB rules) | | |
| `btn-purple`, `btn-primary-purple` | legacy primary aliases, unchanged; new code uses `btn-primary` | | 6.51 |

- **Size:** `btn-sm` is Bootstrap's (14px, ~31px tall on desktop; it was 16px / 38px). On phones
  (≤768px) the existing touch rules keep `.btn` ≥44px and `.btn-sm` ≥38px; at ≤375px `btn-sm`
  stays smaller than `.btn`.
- **Disabled:** every variant uses Bootstrap's disabled state (own colors at 0.65 opacity, no
  pointer events). The primary-only special case (`#8e6fc7` at 0.75) is gone.
- **Loading:** `spinner-border-sm` inside a button keeps its size on every viewport.
- **Custom-class buttons** (`music-btn-*`, `admin-btn-*`, `rehearsal-icon-btn`, `enhance-row__btn`,
  …) paint themselves. A class that paints a background must also paint its `:hover`, because
  Bootstrap's `.btn:hover` otherwise applies an undefined `--bs-btn-hover-bg` (transparent). The
  three that relied on the old purple hover got one (`btn-send-reminder`, `btn-pending-approvals`,
  `u-btn-gold-xs`). `btn-pending-approvals` also moved to dark text on its orange fill.
- The navbar "Entrar" link was a bare `.btn` that only looked primary because of the old base
  rule; it is now `btn btn-primary`.

### 20.4 Text, status and focus contract

| Class | Before | After | Contrast (page / card) |
| --- | --- | --- | --- |
| `text-muted` | `#ffffff` (forced) | `--rtub-text-muted` | 19.2 (no hierarchy) → 8.25 / 7.49-7.86 |
| `text-secondary` | `#343a40` | `--rtub-text-muted` | 1.67 → 8.25 |
| `text-danger` | `#dc3545` | `--rtub-danger-text` | 4.23 → 5.01 / 4.78 |
| `text-success` | `#198754` | `--rtub-success-text` | 4.23 → 7.82 |
| `.badge.bg-warning` | white text | `--rtub-on-warning` | 2.19 → 7.03 |
| `btn-success` fill | `#28a745` | `#198754` | 3.13 → 4.53 |
| `btn-danger` fill | `#e74c3c` | `#dc3545` | 3.82 → 4.53 |

Focus (`1-base/global.css`, `3-components/buttons.css`, `3-components/forms.css`):

- Keyboard focus is a **2px `--rtub-focus-ring` outline, offset 2px** (6.99:1 on the page) through
  `:focus-visible`, so pointer clicks show no ring. It applies to every element and is restated for
  the Bootstrap components that swap outlines for blue box-shadows (`.btn`, `.btn-close`,
  `.nav-link`, `.navbar-toggler`, `.page-link`, `.accordion-button`, `.list-group-item-action`,
  `.form-check-input`). Menu items draw it inset (`-2px`).
- Text inputs and selects show focus on click too (the caret goes there): border plus a 1px ring
  in `--rtub-focus-ring`. The phone-only faint purple override was removed.
- A component may remove the outline only if it draws an equal ring in `--rtub-focus-ring`. The
  interactive cards that already did (`avatar-card`, `member-card-lite`, `activity-card-lite`,
  `enrollment-card`, `instrument-circle`, gallery and stage-enemy cards) and the SearchBar /
  FilterDropdown / checkbox focus borders now use the token instead of `--bs-primary` (2.9:1).

### 20.5 Global layout and mobile rules

- **No global horizontal clipping.** `html`, `body` and `.content-main` never get
  `overflow-x: clip|hidden` (guarded by `VisualFoundationCssTests.RootElements_AreNotClippedHorizontally`;
  `ServiceWorkerReliabilityTests` still forbids `hidden`, which makes iOS Safari drift the fixed
  bottom bar). A component that must scroll sideways owns its overflow — e.g. the family tree's
  `.family-tree-scroll` now scrolls inside itself on phones instead of making the page 15,600px
  wide.
- `main` (`.content-main`) has `min-width: 0`, so pages lay out at the viewport width.
- Bootstrap spacing utilities (`mt-4`, `mb-4`, `mt-5`, `mb-5`, `py-4`, `py-5`) mean the same on every
  viewport; the phone-only `!important` redefinitions are removed.
- **Bottom-bar clearance:** below 1200px, on pages that render a `MobileBottomNav`
  (`body:has(.mobile-bottom-nav)`), the footer's bottom padding is
  `1rem + --rtub-bottom-nav-clearance + safe-area inset`; elsewhere `1rem + safe-area inset`.
- Page-level corrections needed once the clip was gone (smallest local fix each): Requests, Shop,
  Inventory and Members gave the search container an unconditional `min-width: 360px`, now from
  992px up (as Rehearsals already did); Finance report activity rows let the balance/date/name group
  shrink and wrap below 768px (containment only; the row redesign stays in Phase 6).

### 20.6 Validation performed

Automated and visual evidence are kept apart.

- **Automated:** the CI command set (16.1), including two new stylesheet-contract tests and one
  updated PWA contract test (it now accepts `env()` inside `max()`).
- **Visual:** a temporary Playwright script (not committed) recorded computed style and contrast
  of every visible `.btn`, `.badge` and muted text, document overflow, and footer-versus-bar
  position for 6 anonymous and 34 Owner routes at 1440, 1024, 820, 390 and 360, before and after
  (200 route × viewport captures each). Also: a rendered specimen of every variant and state at
  1440 and 390; keyboard focus on login, nav, inputs and buttons; a delete confirmation and an
  edit sheet opened and cancelled; Events, Rehearsals, Members, Finance report, Music, Home and
  Hierarchy inspected in the Claude desktop browser.

| Measure (same routes and viewports) | Before | After |
| --- | --- | --- |
| `main` wider than the viewport | 15 | 0 |
| Footer text hidden behind the bottom bar | 110 | 2 (Messages' own full-height layout) |
| Text buttons with contrast < 4.5 (icon-only and social brand buttons excluded) | 218 | 0 |
| Warning badges < 4.5 | 20 | 0 |
| `text-muted` rendered as pure white | 367 | 0 |
| Phone routes wider than the viewport (390 and 360, mobile emulation, final build) | 2 (family tree) | 0 of 80 |

PWA/mobile (emulated, Chromium, touch + mobile viewport): manifest linked and served as
`application/manifest+json` (standalone, `id "/"`, `portrait-primary`); service worker active at
scope `/`; offline navigation serves `offline.html`; bottom bar padding 5.6px; footer clear of the
bar. `display-mode: standalone` could not be emulated in this Chromium (the media query stayed
false), so the standalone-only CSS is source-verified only. Not verifiable without real devices:
iOS safe areas, Safari PWA quirks, virtual keyboard over the bottom bar, backgrounding/suspension,
installed-app lifecycle and OS back navigation.

### 20.7 Deploy note: unversioned `@import`s (needs an owner decision)

`site.css` is cache-busted by `VersionedAsset`, but its 76 `@import`s are requested without a
version, and outside Development static files are served `Cache-Control: public,max-age=2592000`
(30 days, `Program.cs`); the service worker then serves CSS stale-while-revalidate. A returning
browser can keep the **old** global sheets for up to 30 days after this change ships, while the
scoped bundle and markup are new. The in-app browser reproduced the staleness during validation.
The mixed state was reviewed from source (not rendered) and is benign: scoped CSS carries literal
fallbacks, and old global CSS still paints `btn-primary` purple and keeps the footer usable. Options for a decision before
the next production release: version the `@import` URLs at build time; serve unversioned
`/css/**` with `no-cache` (ETag revalidation, 304s); or bundle the global CSS into one versioned
file. Not changed here: it is host caching behavior, outside this task.

**Resolved by unit 034** (versioned per-sheet links, `no-cache` for unversioned CSS/JS): 21.6.

### 20.8 Remaining (for later tasks)

- **Gallery, Leaderboard, Tracing at 820px** overflow the viewport (visible before too; tablets were
  never clipped). Phases 5-6.
- **Messages at 820-1199px**: its full-height layout sits over the footer. Page-owned, pre-existing.
- **`bg-info` badges** (`#007bff`, 85 on MyTuno/Games): 3.98:1 with white, 3.88:1 with dark text.
  No existing RTUB color passes, so this needs a color decision (proposal: Bootstrap's `#0dcaf0`
  with dark text).
- **Social buttons on Home** use brand colors (Facebook 4.23, YouTube 4.0, Spotify 2.59 with white):
  brand identity, left as is.
- **Tablet touch:** `btn-sm` is ~31px at ≥769px even on touch tablets (phone rules are width-based).
  Resolve with `IconButton` / `(pointer: coarse)` sizing in Phase 3/7.
- **Still global, left for component work (Phases 3-5):** `.card` gets extra padding at ≤1024px on
  top of `.card-body`; `2-layout/grid.css` redefines Bootstrap's row/column gutters (so `g-*` does
  not affect column padding); a phone-only rule makes every plain `<a>` `inline-flex` (footer and
  inline links wrap as blocks); the desktop navbar still overflows at 1024-1440px (Phase 2).
- **Detector:** the only finding on the changed files is the pre-existing side stripe at
  `misc-components.css:178` (12.3).

### 20.9 Roadmap adjustments

- Phase 1 is done except the deliberate deferrals in 20.8.
- Before or with Phase 2: decide the cache-busting option in 20.7, so every later CSS phase
  reaches returning users.
- Phase 3's `IconButton` owns touch sizing (44px target on `pointer: coarse`), which also resolves
  the tablet `btn-sm` note.

---

## 21. Implemented: App Shell and Navigation (unit 034, Task 003)

Durable facts from implementing roadmap Phase 2 on `chore/034/ui-app-shell-navigation`. Sections
1-20 stay as recorded; this section is the shell contract later UI tasks build on.

### 21.1 Root causes

| Problem (audit ref) | Root cause | Fix |
| --- | --- | --- |
| Desktop navbar overflowed at 1024-1440 (8.1, 8.2, 20.8) | The bar expanded at 992px (`navbar-expand-lg`) with 11 icon + text items for Owner (~1,200px of items) plus the category badge, the nickname and the avatar: 1,465-1,477px wide. A Tuno needed ~1,345px, a Leitão ~1,235px. `container-xxl` is 100% wide below 1400px, so nothing constrained it. | Expand at 1200px (`navbar-expand-xl`); top-level items text-only on the bar; badge and nickname moved into the account menu (21.3). Owner's items end at 1,051px on a 1200px screen. |
| Expanded top nav and page bottom bar both shown at 992-1199 (8.2) | Nav collapsed at 992, `MobileBottomNav` and the `d-xl-flex` header actions switch at 1200. | One breakpoint: 1200 (21.2). |
| Account menu unreachable on desktop, buried at the end of the drawer on phones | It was the last item of the overflowing bar, and inside the offcanvas below 992. | Account is its own control in the header on every width. |
| Parent menu of the current page not marked (13.7) | Only `NavLink` marks itself; the dropdown toggles are buttons. | `MainLayout.GroupClass` marks a menu toggle `active` while one of its routes is current (presentation only). |
| Keyboard focus on a shell menu item was white on white | `1-base/utilities.css` paints every `.dropdown-menu .dropdown-item:hover/:focus` `#f8f9fa`; the shell forced white text. | Shell menus restate their own backgrounds (scoped to `.navbar-main`). Page dropdowns are unchanged. |
| 41 routes had no `<title>` (A3) | No title contract; 21 pages set one by hand in three formats. | `AppTitle` (21.4). |
| No skip link, no `header` landmark (A7) | - | 21.5. |
| Returning users could run old global CSS for up to 30 days (20.7) | `site.css` was versioned, but a browser fetches `@import` URLs exactly as written: its 76 sub-sheets were unversioned and served `max-age=2592000`; the service worker then revalidated them against that same 30-day HTTP cache. | 21.6. |

### 21.2 Shell structure and breakpoints

- Markup (`Shared/MainLayout.razor`, static SSR): skip link → `<header class="navbar-main">`
  (brand, drawer `#topNav` holding `<nav aria-label="Principal">`, account menu, menu button) →
  `AnnouncementBanner` → `<main id="content" tabindex="-1">` → `<footer>`. The shell CSS lives in
  `2-layout/navbar.css` and `2-layout/footer.css`; the navbar rules that used to be scattered in
  `1-base/mobile.css`, `3-components/misc-components.css` and `list-groups.css` were removed
  (including the dead `.navbar-category-*` classes).
- **One breakpoint, 1200px (Bootstrap xl):**

| Width | Navigation | Page actions |
| --- | --- | --- |
| >= 1200 | Horizontal bar: brand, text-only items, account avatar at the right. | Page header buttons (`d-xl-flex`). |
| < 1200 | Brand, account avatar, menu button; right-hand drawer (`offcanvas-xl`), `min(86vw, 22rem)` wide. | `MobileBottomNav`. |

- The bar fits every role from 1200px up without shrinking type (15px, weight 500). If items
  are ever added beyond the space, the list wraps (`flex-wrap`) instead of widening the page.
- Header ~58px; brand 20px/700, tracking 0.06em; the avatar button, the menu button and the
  drawer close button are 44x44; drawer rows are 48px (sub-items 44px).
- Information architecture unchanged: same items, same order, same menus, same labels, same
  role/category conditions (copied verbatim). Only presentation moved: icons are hidden on the
  desktop bar (kept in menus and the drawer); the category/position badge and nickname moved
  from the bar into the account menu header; Gestão and Operações menus open right-aligned so
  they stay on screen at 1200.

### 21.3 Navigation states

| State | Desktop bar | Menus (desktop) | Drawer |
| --- | --- | --- | --- |
| Idle | `#e2e2e2` text | `#e2e2e2`, accent icon | `#e2e2e2`, accent icon |
| Hover / pointer focus | `rgba(255,255,255,.06)` surface, white text | same | same |
| Keyboard focus | 2px `--rtub-focus-ring` outline (global contract, 20.4); inset in menus | | |
| Current page (`NavLink` `.active` + `aria-current="page"`) | White, weight 600, 2px accent bar under the label | Soft purple surface `rgba(111,66,193,.22)`, weight 600 | Soft purple surface, weight 600, white icon |
| Section of the current page (toggle `.active`) | Same as current page | - | Same surface on the section row |
| Menu open (`.show` / `aria-expanded="true"`) | White text | - | Chevron rotates 180° |

Active is never color alone: weight plus an indicator bar or a surface. The drawer's old
side-stripe was dropped (craft rule: no colored side stripes).

### 21.4 Page titles

- `Components/AppTitle.razor`: `<AppTitle>Ensaios</AppTitle>` renders `Ensaios - RTUB`;
  `<AppTitle />` renders `RTUB - Real Tuna Universitária de Bragança` (home, and the MainLayout
  fallback placed before `@Body` so a page's own title wins). Pages never use `<PageTitle>`.
- All 62 routable pages declare one. Text is the page's own `<h1>` (or its nav label when it has no
  h1: Classificação, Mini Jogos, Mensagens). Detail pages use data they already load:
  `Contactos - <event>`, `Discussão - <event>`, `Inscrições - <event>`, `<board> - Logística`,
  `<event> - Encomendas Nerba`, `<report title>`, `<album> - Música`; each falls back to the
  generic label while loading. No data load was added for a title.
- Titles are set during prerendering (static `HeadOutlet`); a title computed only after the
  interactive circuit loads data stays at its fallback. Owner pages that had English titles now
  use their Portuguese h1 (`Histórico de atividades`); the Router's NotAuthorized/NotFound texts
  are Portuguese with titles `Sem permissão` / `Página não encontrada`.
- Guarded by `AppTitleTests` (rendered title, and every `@page` file uses `AppTitle`) and
  `AppShellTests.Page_HasMeaningfulTitle` (exactly one `<title>` per response).

### 21.5 Accessibility, keyboard and safe areas

- **Skip link** "Saltar para o conteúdo": first focusable element of every page, visible only
  when focused (purple pill, top-left, above everything at z-index 1090). `navOffcanvas.js`
  handles it in the capture phase and focuses `<main>`: a plain `#content` resolves against
  `<base href="/">` and Blazor would navigate to the home page. `main` and the drawer draw no
  focus ring of their own (programmatic focus targets, not controls).
- Blazor's existing `FocusOnNavigate Selector="h1"` (App.razor) still places focus on the page
  `<h1>` after a navigation, so on pages with an h1 the first Tab goes into the content; the skip
  link serves users starting from the top of the document (pages without an h1, browser chrome,
  Shift+Tab). Deliberately kept.
- **Landmarks:** one `header`, `nav` "Principal" (the drawer's dialog wraps it below 1200), one
  `main`, one `footer`. Decorative icons are `aria-hidden`.
- **Drawer:** Bootstrap owns dialog semantics (`role="dialog"`, `aria-modal`, labelled by its
  "Navegação" heading), focus trap, Escape, backdrop close, body scroll lock and focus return to
  the menu button. RTUB adds: Portuguese labels ("Abrir menu de navegação", "Fechar menu") and
  `aria-expanded` on the menu button kept in sync (`navOffcanvas.js`). Escape closes an expanded
  section first, then the drawer. A link inside it closes it before navigating (existing).
- **Menus:** native `<button>` toggles with `aria-expanded` (Bootstrap), arrow keys and Escape
  (focus returns to the toggle). `aria-haspopup` was removed: these are disclosure menus of links,
  not ARIA `menu`s. The account button is named "Conta de <nickname>" (plus the unread count).
- **Safe areas:** `viewport-fit=cover` added to the viewport meta; without it iOS reports every
  `env(safe-area-inset-*)` as 0, so the existing inset rules were inert and, with the
  `black-translucent` status bar, the installed iOS app drew the header under the status bar.
  Insets (all with a `0px` fallback, so nothing changes on devices without them): header top,
  left and right; drawer top, right, bottom; skip link; footer left, right, bottom;
  `MobileBottomNav` left and right (bottom was already done); full-screen phone dialog header top
  (not `modal-sm`). `.content-main` already padded left/right.
- Standalone query below 1200 keeps overscroll containment on the header and drawer; nothing sets
  `overflow-x: hidden` on html/body (20.5).
- Motion: menu/drawer transitions are Bootstrap's; shell transitions are 150ms and switched off
  under `prefers-reduced-motion`.

### 21.6 Static-asset cache contract

- **Global CSS:** `site.css` is now the ordered *list* of global sheets, never linked itself.
  `Components/GlobalStylesheets.razor` reads its `@import` lines (cached, re-read when `site.css`
  changes) and renders one `VersionedAsset` link per sheet, in the same order, so the cascade is
  identical. Each sheet gets its own content hash (`?v=`, ASP.NET Core `IFileVersionProvider`, the
  mechanism already used for every other local asset): a deploy that changes a sheet changes its
  URL, unchanged sheets keep their cached copy. Side effect: the 76 sheets are requested in
  parallel instead of being discovered after `site.css` downloads.
- **Server headers** (`Program.cs`, outside Development): versioned URL (`?v=`) → `public,
  max-age=2592000`; unversioned `.css`/`.js` → `no-cache` (revalidated with ETag/Last-Modified,
  304 when unchanged: e.g. `_framework/blazor.web.js`, the fingerprinted `_content` scoped
  bundle, `offline.css`); other static files unchanged (30 days; icons/manifest 1 hour;
  `service-worker.js` `no-cache`). Nothing is `no-store`.
- **Server response cache:** `UseResponseCaching` keys its in-memory cache on the path unless a
  response names the query keys it varies by, so static responses vary by `v`
  (`IResponseCachingFeature.VaryByQueryKeys`). Without it a stored 30-day `/x.css?v=<hash>`
  answered a later unversioned `/x.css`; the first Deploy • DEV run after the merge (run
  36394016593) failed on exactly that, because test order on Linux put the versioned request
  first. Fixed on `fix/034/app-shell-ci`, guarded by
  `AppShellTests.UnversionedAsset_AfterItsVersionedUrlWasServed_IsStillRevalidated`.
- **Service worker:** CSS/JS stay stale-while-revalidate, keyed by full URL. A new `?v=` is a
  cache miss, so changed CSS arrives on the first load after a deploy. Storing a versioned response
  now first deletes the other versions of the same path (`cache.delete(request, { ignoreSearch:
  true })`), so the runtime cache holds one copy per file instead of one per deploy; the
  pre-034 unversioned copies are removed the same way. `CACHE_VERSION` was not bumped; the offline
  precache (`offline.html`, `offline.css`, `offline.js`) is untouched and still served.
- Why not the alternatives in 20.7: `MapStaticAssets` would move every static file (including
  `/images`, served by `ImagesController`, and the ~180 MB of sprites) onto build-time endpoints and
  change all cache headers at once; `no-cache` for all CSS alone still left the SW one load behind;
  a bundler adds a build pipeline.
- Guarded by `GlobalStylesheetsTests` (site.css holds only imports of existing files, rendered
  links are versioned and in order, MainLayout uses the component) and
  `AppShellTests.StaticAsset_CacheControl_FollowsTheVersioningContract` /
  `GlobalStylesheets_AreLinkedVersioned_InsteadOfSiteCss`.

### 21.7 Validation performed

Automated and visual evidence are kept apart.

- **Automated:** the CI command set (16.1). New: `AppTitleTests` (3), `GlobalStylesheetsTests`
  (5), `AppShellTests` (11, integration: titles, skip link and landmarks, current page and section,
  versioned stylesheets, cache headers). `VersionedAssetTests` no longer expects a `site.css` link.
- **Local run:** Development, against a scratch copy of the local snapshot database with the
  development data reset pointed at a generated password (so no personal password was used and the
  real local database was not touched); SMTP disabled, backups off, non-existent R2 bucket.
  Accounts: anonymous, Owner, Tuno member, Caloiro member, Leitão member.
- **Measured** (temporary Playwright script, not committed; 322 route × viewport × role captures at
  1440/1280/1200/1024/820/390/360 over home, login, events, rehearsals, members, finance,
  meetings, music, gallery, privacy, profile, messages and `/owner/tracing`):

| Measure | Before | After |
| --- | --- | --- |
| Page wider than the viewport | 117 | 1 (Gallery at 820, page-owned, 20.8) |
| Account control not visible in the header (Messages' own phone layout excluded) | 232 | 0 |
| Expanded top nav shown together with a page bottom bar | 37 | 0 |
| Empty document title | 287 | 0 |

- **Keyboard and semantics (Chromium, scripted + screenshots):** skip link is the first Tab stop
  on a page without an h1 and moves focus to `main` without leaving the page; menus open with
  Enter, move with arrows, close with Escape back to the toggle; drawer at 1024/820/390/360 is a
  labelled modal dialog, locks body scroll, closes on Escape (section first) and returns focus to
  the menu button; a drawer link closes it and leaves no backdrop; `aria-expanded` follows the
  drawer; right-aligned menus end at 1,063px (1440) and 1,051px (1200).
- **Visual** (screenshots reviewed at every width): desktop bar and menus, account menu for
  Owner and Leitão, drawer with the current section expanded, phone top bar (signed in and
  anonymous), footer above the bottom bar, focus states, and the unusual layouts - Messages at
  390 (header hidden by its own shell) and 820, MyTuno at 390, Hierarchy (static SSR) at 1440,
  `/owner/db` at 1280, Finance report at 360.
- **PWA (emulated Chromium):** service worker active at `/`; its runtime cache holds the 76
  versioned sheets and no unversioned copy; offline navigation still serves `offline.html`.
- Impeccable detector on the changed shell files: no findings.
- **Not verified (needs real devices):** iOS installed app under the status bar with
  `viewport-fit=cover`, landscape notch insets, Safari standalone quirks, Android system Back
  with the drawer open, virtual keyboard, installed-app lifecycle. `display-mode: standalone`
  cannot be emulated in this Chromium (20.6).

### 21.8 Remaining (for later tasks)

- Unknown URLs return an empty 404 (no status-code page), so the Router's NotFound content never
  renders on a full request. A 404 page is new behavior; not added here.
- `Error.razor` (static error page) still has an English h1 and message.
- `MobileBottomNav` semantics (three jobs, `aria-selected` on buttons, fixed "Navegação do portal"
  label) are unchanged - Phase 3 (`PageActions`).
- The shell dropdowns sit at z-index 1045 as before; dialog layering is Phase 3 (`Modal`).
- Footer copyright year is a literal "2025" (content, not changed).
- `theme-color` `#3F2A86` (purple status bar over the near-black header on Android) kept as brand.

### 21.9 Roadmap adjustments

- Phase 2 is done except the real-device checks in 21.7. 20.7 (cache-busting) is resolved.
- Phase 3 can rely on: the 1200px shell breakpoint (bottom bar, header actions and nav agree),
  `AppTitle` for any new page, versioned global CSS (a new sheet is one `@import` line in
  `site.css`), and the shell's safe-area insets.

---

## 22. Implemented: RTUB.Shared Core Components (unit 035, Task 004)

Durable facts from implementing roadmap Phase 3's first half on
`chore/035/ui-shared-core-components`: the dialog primitives and the page header/action pattern.
Sections 1-21 stay as recorded.

### 22.1 Inventory (before)

| Component / pattern | Use | Finding |
| --- | --- | --- |
| `Modal` | 129 in 53 files | No dialog semantics; focus stayed on the page; Escape only worked if focus was already inside (it never was); focus not restored; the page lost its scroll position on close (phones: always to the top, desktop: partly); close control was a left arrow at the right on every size; a hidden duplicate "Voltar" button; `ShouldRender` could not hide a dialog that was first rendered open. |
| `ConfirmDialog` | 58 in 30 files | Intent from the confirm class only: 46 `btn-danger`, 6 primary, 5 success, 1 warning. Info icon for every intent, including destructive. 9 callers pass `Disabled`; nothing else stopped a double click from confirming twice. 20 use custom body content (some as plain info dialogs). |
| `DetailsModal`, `CrudModalManager`, `ParticipationModal`, `RepertoireModal`, `MeetingParticipationModal` | 12 / 5 / 1 / 1 / 1 | All built on `Modal` - no parallel dialog implementation. |
| `page-header-centered` markup | 43 pages | The same left / centered title / right structure written by hand; desktop-only back buttons; right-slot actions hidden below 1200px. |
| Desktop actions + `MobileBottomNav` items | 33 pages | Two hand-maintained lists per page: different labels ("Propor..." vs "Convocar..."), different conditions (`AuthorizeView` vs code flags) and different visibility (Meetings' AG action disabled on desktop, hidden on phones). |
| `MobileBottomNav` | 38 pages | Classified: navigation 1 (Home: app bar when signed in, section bar when not), links to sub-pages 1 (MyTuno), section jumps mixed with actions 1 (Albums), own shell 1 (Messages), page actions 34 (16 of them with a "Voltar" item; WeaponDrinkConfig's bar was only "Voltar"). No tabs. Always labelled "Navegação do portal"; `aria-selected` on buttons. |

### 22.2 Modal contract

- **Semantics:** `role="dialog"`, `aria-modal="true"`, named by its title (`<h2 class="modal-title">`
  via `aria-labelledby`), or by `aria-label` when `HeaderActions` replace the title; optional
  `AriaDescribedBy`.
- **Focus (modalHelper.js dialog stack):** on open, focus moves to the element marked
  `data-autofocus`, else the dialog itself (no ring on the container). Tab and Shift+Tab stay in the
  top dialog. On close, focus returns to the element that opened it if it is still on the page (a
  delete button whose card was removed cannot get it back; focus then stays on the page).
- **Stacking:** dialogs opened from dialogs form a stack; only the top one gets Tab and Escape;
  closing it returns focus into the one below; the body stays locked until the last closes.
- **Escape:** handled once, on `window`, after document-level handlers, so a widget that handles
  Escape itself (an open dropdown) keeps it. It closes the top dialog when `CloseOnEscape` (default:
  `ShowCloseButton`) - dialogs without a close button stay non-dismissible, as before.
- **Dismissal hook:** the close button, Escape and the default footer all go through one path that
  first awaits `CanClose` (`Func<Task<bool>>`). Returning false keeps the dialog open. This is the
  hook the forms/data-safety phase uses for "discard changes?"; no policy is implemented here.
  Backdrop clicks still never close a dialog.
- **Scroll:** the lock keeps the page where it was (the body is offset while it is `position:
  fixed` below 1025px and the position is restored on close). Measured 900 → 900 at 1440 and 390
  (before: 900 → 194 and 700 → 0).
- **Look:** Bootstrap modal variables on RTUB tokens (surface `--rtub-surface-alt`, border
  `--rtub-border`, radius `--rtub-radius-lg`, 1.25rem padding, overlay shadow); title 18px/600; the
  purple-tinted header stays. One close control: an X on the right of a dialog, a back arrow on the
  left of a phone sheet (centered title kept). Footer buttons spaced by a gap. Enter motion only
  (dialog 180ms rise, phone sheet 220ms slide-up), none under `prefers-reduced-motion`.
- **Phones:** full-screen sheet with the bottom action bar (kept); its labels are 11px sentence
  case, like the page action bar. Small dialogs stay centered boxes; their two buttons split the
  width, confirm on the right. Top inset for the installed iOS app (21.5).

### 22.3 Browser Back with a dialog open - not changed

Back still navigates the page underneath while a dialog stays open (7.x, F4). A generic fix means
pushing a history entry per dialog and popping it on close, which interacts with Blazor's
`NavigationManager`, enhanced navigation, pages that keep filters in the query string
(`/rehearsals?fy=`), dialogs that navigate, and nested dialogs. That is a navigation/history
decision tied to unsaved-change protection (`NavigationLock`), so it belongs to Phase 4 (forms,
dialogs and data safety). No history hack was added.

### 22.4 ConfirmDialog

- **Intent from the existing `ConfirmButtonClass`** (no consumer changed): `btn-danger` =
  destructive (red warning triangle), `btn-warning` = caution (orange), anything else = a plain
  confirmation (purple question mark). No info icon remains.
- Cancel (outline, first, initial focus) then the confirm action (last). Escape cancels. The message
  is the dialog's description.
- **Busy:** while `OnConfirm` runs both buttons are disabled, the confirm button shows a spinner
  and `aria-busy`, and Escape/close are refused - a second click cannot confirm twice. The dialog
  closes when `OnConfirm` completes (unchanged); `Disabled` still works for callers that manage
  their own state.
- Custom-body uses (receipts, "Erro", info) keep their content.

### 22.5 Dialog wrappers

`DetailsModal` and the other wrappers inherit the whole contract through `Modal`; `DetailsModal`'s
name heading is now an `h3` (under the dialog's `h2`). No wrapper needed API changes.

### 22.6 PageHeader

```razor
<PageHeader Title="Ensaios" Icon="bi-music-note-list" Subtitle="..." BackHref="/..." or OnBack="GoBack">
    <ChildContent>extra context under the title (badges)</ChildContent>
    <Actions><PageAction ... /></Actions>
</PageHeader>
```

- The page's single `h1` (icon `aria-hidden`), subtitle in `--rtub-text-muted`, optional back link
  or button (44px, `aria-label="Voltar"`), optional context, actions.
- Composition: title block at the start, actions at the end from 1200px (wrapping when needed); below
  1200px the actions are in the bottom bar. Title 24-32px (`clamp`), 600; long titles wrap.
  Replaces the centered title + separate right-aligned action row.
- **Adopted by 41 pages** (all `page-header-centered` pages except two). Exceptions: **Songs** keeps
  its album-cover hero (it uses `PageActions` for its action and shows its back button on every
  size); **Albums** keeps its header because its phone bar mixes section jumps with actions (see
  22.8).

### 22.7 PageActions

- One definition, two presentations: `PageAction` children render as header buttons from 1200px
  and as a fixed bottom bar below (1200px = the shell breakpoint). Each is a labelled
  `role="group"` ("Ações da página"), never `nav`. The bar hides itself when no action applies to
  the current user; footer clearance covers it (`body:has(.page-action-bar__item)`).
- `PageAction`: `Label` (header, accessible name), `ShortLabel` (bar), `Icon`, `Intent`
  (Secondary default / Primary / Danger), `OnClick` or `Href` (a real link), `Disabled`, `Busy`
  (spinner, `aria-busy`, not clickable). Visibility stays in the page (`@if`, `AuthorizeView`) -
  the component never decides authorization.
- Hierarchy: at most one Primary (purple fill; accent in the bar) - the page's create action; the
  rest are quiet outlines; destructive actions are Danger (red outline / red bar item) and last.
  Existing action order kept for familiarity.
- **30 pages, 55 actions.** Measured: every page shows the same action set in the header (1440)
  and in the bar (390) for Owner and for a Tuno member (0 mismatches); the member sees none of the
  admin-only actions.
- Reconciled divergences (the desktop version won, since it was the reviewed one): labels and
  conditions come from the desktop markup (`AuthorizeView` roles); Meetings' "Propor Assembleia
  Geral" is shown disabled on phones too (was hidden there); Inventory's add action is disabled
  while loading on phones too; AllCharacters' "Reset Data" is shown disabled on phones too (it is
  still turned off). Icon-only desktop actions (EventEnrollments) gained visible labels.
- "Voltar" moved from the bar to the header's back affordance on 15 pages, now visible at every size
  (same targets: each page's existing `GoBack`/`NavigateBack` or the same URL). The tracing page's
  "Voltar" (to `/`) was dropped: the brand link does the same.
- Component-owned actions (login statistics, chat/status sync, enrollment statistics) are triggered
  through the pages' existing hidden `@ref` instances; the sync buttons expose `IsSyncing` so the
  action shows its busy state as the old desktop button did.

### 22.8 MobileBottomNav responsibility

Navigation only: between app sections (Home, signed in), between a page's own sections (Home,
public; Albums' "Públicos/Privados"), or a page's views. It is a `nav` with its own `AriaLabel`, the
current item carries `aria-current`, labels are 11px sentence case. Remaining consumers: Home (2),
Albums, Messages. **Albums** is a documented exception (its bar also holds two actions; splitting
it needs a second phone bar or a new section control - a design decision for the Music page task).
**Messages** keeps its own full-screen shell and state-driven bar. Sub-page links (MyTuno's
Classificação/Enemies/Config, Report's Calotes/MBWAY/Nerba) are page actions: `Href` links or
their existing navigation methods.

### 22.9 Supporting primitives

None added beyond `PageHeader`, `PageActions`/`PageAction` (with `PageActionIntent`). An
`IconButton` was not created: the repeated icon-only buttons are card/admin overlays (Phase 5/6
card work), not shell or page-level controls, so it would have had no consumer in this task.

### 22.10 Validation performed

- **Automated:** the CI command set (16.1). New/rewritten: `ModalTests` (+7: dialog name, header
  actions name, Escape default and override, Escape path, `CanClose` veto, leaving the stack),
  `ConfirmDialogTests` (+5 and 2 moved from class strings to behaviour: danger intent without info
  icon, plain intent, description, busy guard, Escape cancels, cancel focus), `PageHeaderTests` (10:
  heading, back link/button, groups not navigation, both presentations, intents, click from either,
  busy, `Href`, `MobileBottomNav` naming and `aria-current`). Test stubs moved from
  `lockBodyScroll`/`unlockBodyScroll` to `openDialog`/`closeDialog`; two page tests moved from the
  old bottom-nav markup to the action bar.
- **Browser (Chromium, Playwright, local scratch database, Owner and Tuno):** 410 route x viewport x
  role captures (41 routes x 1440/1024/820/390/360): one `h1` per page, no actions inside `nav`, no
  header actions below 1200 and no bar at 1200+, no bar together with a navigation bar, no footer
  behind the bar, header/bar parity. Only findings: Messages at 820-1199 and Gallery at 820
  (both pre-existing, 20.8). Dialogs: semantics, initial focus (dialog / Cancel), Escape, focus
  return, Tab containment (0 escapes in 25 Tabs), scroll kept, and the dialog stack exercised in
  the page (non-dismissible top dialog blocks Escape, focus returns into the lower dialog, lock held
  until the last closes). Screens reviewed at 1440 and 390: Rehearsals, Events, Members, Meetings,
  Finance report, Logistics board, Songs, MyTuno, tracing; edit, stats, details and delete
  confirmation dialogs.
- Impeccable detector on the changed components and CSS: no findings.
- **Not verified:** real devices (virtual keyboard over sheets, iOS standalone safe areas, Android
  Back with a dialog open), screen-reader output (the semantics were checked in the DOM only).

### 22.11 Remaining

- Browser/hardware Back with a dialog open (22.3) - Phase 4.
- Albums' mixed phone bar (22.8); Messages' own shell.
- When a dialog's opener is removed (deleting the item it belonged to), focus stays on the page
  instead of a nearby element.
- `ConfirmDialog` is still used for a few informational dialogs ("Erro", receipts, prizes); a plain
  `Modal` fits them better (Phase 4/6 when those pages are touched).
- The `page-header-centered` CSS stays for Albums and Songs.

### 22.12 Roadmap adjustments

- Phase 3 is done for dialogs, page headers and page actions. Still open from Phase 3: `IconButton`
  and `OverflowMenu`/`CardActions` (with the card work), toast host, `FormField`, the
  `EmptyState`/`LoadingSpinner`/`ErrorDisplay`/`Alert`/`SearchBar`/`FilterDropdown` refinements and
  the error boundary.
- Phase 4 can build dirty-form guards on `Modal.CanClose` and decide the dialog/Back behaviour.
