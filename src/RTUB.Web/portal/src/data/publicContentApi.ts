// Public site content: governance, gallery, upcoming events and news.
import { json } from './apiCall';

/** GET /api/public/governance (Endpoints/GovernanceEndpoints.cs). Public fields only. */
export type GovernanceMember = { displayName: string; fullName: string | null; avatarUrl: string | null };
type GovernancePosition = { title: string; holders: GovernanceMember[] };
type GovernanceBody = { name: string; positions: GovernancePosition[] };
export type Governance = { fiscalYears: string[]; fiscalYear: string | null; bodies: GovernanceBody[] };

/** One mandate; the server picks the default and ignores an unknown year. */
export function getGovernance(fiscalYear?: string): Promise<Governance> {
  const query = fiscalYear ? `?fiscalYear=${encodeURIComponent(fiscalYear)}` : '';
  return fetch(`/api/public/governance${query}`, { headers: { Accept: 'application/json' } }).then((r) =>
    r.ok ? (r.json() as Promise<Governance>) : Promise.reject(new Error(`HTTP ${r.status}`)),
  );
}

/** GET /api/gallery (Endpoints/GalleryEndpoints.cs). Viewer-aware: visitors get public items only. */
export type GalleryPerson = { id: string; name: string };
export type GalleryItem = {
  id: number;
  title: string;
  type: 'image' | 'video';
  url: string | null;
  year: number;
  month: number | null;
  day: number | null;
  membersOnly: boolean;
  people: GalleryPerson[];
  /** The uploader, Admin or Owner: edit and delete; the server enforces it. */
  canEdit: boolean;
};
export type GalleryTimeline = {
  isMember: boolean;
  items: GalleryItem[];
  total: number;
  page: number;
  pageSize: number;
  years: number[];
  people: GalleryPerson[];
};
export type GalleryFilters = { year?: number; q?: string; person?: string };



export function getGallery(page: number, filters: GalleryFilters): Promise<GalleryTimeline> {
  const query = new URLSearchParams({ page: String(page) });
  if (filters.year) query.set('year', String(filters.year));
  if (filters.q) query.set('q', filters.q);
  if (filters.person) query.set('person', filters.person);
  return fetch(`/api/gallery?${query}`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' }).then(
    json<GalleryTimeline>,
  );
}

/** One item for a shared ?item= link; rejects when it is missing or members-only for a visitor. */
export function getGalleryItem(id: number): Promise<GalleryItem> {
  return fetch(`/api/gallery/items/${id}`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' }).then(
    json<GalleryItem>,
  );
}

/** The latest photos visitors can see, whoever is signed in (?public=true): the home preview. */
export function getGalleryPreview(count: number): Promise<GalleryItem[]> {
  return fetch(`/api/gallery?public=true&pageSize=${count}`, { headers: { Accept: 'application/json' } })
    .then(json<GalleryTimeline>)
    .then((t) => t.items);
}

/** GET /api/public/events/upcoming: at most 3, soonest first. Dates are local "yyyy-MM-dd" text. */
export type UpcomingEvent = {
  name: string;
  date: string;
  time: string | null;
  endDate: string | null;
  location: string;
  type: string;
  cancelled: boolean;
};

export function getUpcomingEvents(): Promise<UpcomingEvent[]> {
  return fetch('/api/public/events/upcoming', { headers: { Accept: 'application/json' } }).then(json<UpcomingEvent[]>);
}

// ---------- Novidades ----------

/** One post of GET /api/news. `publishedAt` is UTC ISO, null for a draft; `authorName` only reaches Admin/Owner. */
export type NewsPost = { id: number; title: string | null; body: string; publishedAt: string | null; authorName: string | null; membersOnly: boolean };
export type NewsFeed = { drafts: NewsPost[]; posts: NewsPost[]; hasMore: boolean; canManage: boolean; signedIn: boolean; canWrite: boolean };

/** The latest published posts, for the home preview (drafts are ignored there). */
export function getLatestNews(count: number): Promise<NewsPost[]> {
  return fetch(`/api/news?pageSize=${count}`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
    .then(json<NewsFeed>)
    .then((feed) => feed.posts);
}

/** "5 de outubro de 2026" for a post's UTC publication time, in the reader's time zone. */
export const newsDate = (iso: string) => new Intl.DateTimeFormat('pt-PT', { dateStyle: 'long' }).format(new Date(iso));
