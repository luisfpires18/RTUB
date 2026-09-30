# 0002 — React public-portal pilot alongside Blazor

- **Status:** Proposed
- **Date:** 2026-09-30

## Context

RTUB is Blazor Interactive Server end to end. The unit 032 UI audit (in git history since the
2026-09-30 rollback) recorded the costs of the live-circuit model - a server round-trip per
interaction, reconnect prompts, form loss when a backgrounded PWA's circuit expires - but judged
them not enough on their own to justify a stack change. The owner has decided to test that
trade-off with React on the lowest-risk surface, the public portal, without putting the
member/admin application at risk.

Constraints: one ASP.NET Core host and origin (cookies, CSP, PWA scope, Android asset links); an
enforced CSP with no `unsafe-inline`; a single installed-app identity (manifest id `/`) and one
service-worker registration; authorization services that depend on Blazor `AuthenticationState`;
no public read API yet.

## Decision

Run a bounded pilot: a React 19 + Vite public portal at `/portal` and `/portal/privacy`, served
by the existing host from committed static output (`wwwroot/portal`), with route ownership declared
explicitly in `Program.cs`. Blazor keeps every other route, including `/`, login and the public
request form, which the portal links to. No new API, no database change, no business logic in
React. Details: `docs/react-portal-pilot.md`.

## Consequences

- Blazor and React coexist; each owned path has exactly one owner, and moving a path is a
  one-line, test-pinned change.
- A second front-end toolchain is now part of the repo; CI must build and type-check it and
  prove the committed bundle matches its source (not yet wired - see the pilot doc).
- Content that needs data (agenda, albums, gallery) is illustrative until a read-only public API
  exists. Promoting the portal to `/` also needs a decision on the PWA `start_url`.
- Migrating authenticated pages is not covered: it needs an authorization model that does not
  depend on Blazor `AuthenticationState`. This ADR should be accepted, amended or superseded after
  the pilot is reviewed on DEV.
