// Typed client for /api/music (React track 006, Endpoints/MusicEndpoints.cs). The server decides
// what the caller may see and do; `permissions` only spares people buttons that would be refused.

export type MusicPermissions = {
  isMember: boolean;
  canManage: boolean;
  canDelete: boolean;
  canManageExclusive: boolean;
  canSeeStatistics: boolean;
  canSeeDetailedStatistics: boolean;
};

export type AlbumSummary = {
  id: number;
  title: string;
  year: number | null;
  description: string | null;
  coverUrl: string | null;
  isPrivate: boolean;
  isExclusive: boolean;
  songCount: number;
};

export type MusicLink = { kind: 'spotify' | 'youtube'; url: string };

export type Song = {
  id: number;
  title: string;
  trackNumber: number | null;
  lyricAuthor: string | null;
  musicAuthor: string | null;
  adaptation: string | null;
  hasAudio: boolean;
  playCount: number;
  links: MusicLink[];
  videoCount: number | null;
};

export type AlbumDetail = { album: AlbumSummary; songs: Song[]; permissions: MusicPermissions };
export type Member = { id: string; displayName: string; avatarUrl: string };
export type AlbumEdit = {
  id: number;
  title: string;
  year: number | null;
  description: string | null;
  isPrivate: boolean;
  isExclusive: boolean;
  coverUrl: string | null;
  accessMembers: Member[];
};
export type SongEdit = {
  id: number;
  title: string;
  trackNumber: number | null;
  lyricAuthor: string | null;
  musicAuthor: string | null;
  adaptation: string | null;
  spotifyUrl: string | null;
  hasAudio: boolean;
  youTubeUrls: string[];
  lyrics: string | null;
};
export type Lyrics = { pdfUrl: string | null; text: string | null };
export type Video = { id: number; title: string; url: string; mimeType: string; canDelete: boolean };
export type Statistics = {
  songs: { songId: number; title: string; albumId: number; albumTitle: string; playCount: number }[];
  albums: { albumId: number; title: string; year: number | null; playCount: number }[];
  members: {
    displayName: string;
    fullName: string | null;
    avatarUrl: string;
    totalPlays: number;
    songs: { songTitle: string; albumTitle: string; playCount: number }[];
  }[] | null;
};

export type SongInput = {
  title: string;
  trackNumber: number | null;
  lyricAuthor: string;
  musicAuthor: string;
  adaptation: string;
  spotifyUrl: string;
  hasAudio: boolean;
  youTubeUrls: string[];
  lyrics: string;
};

export type AlbumInput = {
  title: string;
  year: string;
  description: string;
  isPrivate: boolean;
  isExclusive: boolean;
  accessUserIds: string[];
};

export type FieldErrors = Record<string, string>;

/** Every answer maps onto one of these; nothing throws. */
export type Outcome<T> =
  | { kind: 'ok'; data: T }
  | { kind: 'invalid'; errors: FieldErrors; title?: string }
  | { kind: 'signin' }
  | { kind: 'forbidden' }
  | { kind: 'notfound'; unavailable: boolean }
  | { kind: 'failed'; title?: string };

let token: Promise<string> | null = null;

/** One antiforgery token per page load (it is bound to the signed-in session). */
function antiforgeryToken(refresh = false): Promise<string> {
  if (!token || refresh) {
    token = fetch('/api/public/antiforgery-token', { credentials: 'same-origin' })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((t: { token: string }) => t.token)
      .catch((e) => {
        token = null;
        throw e;
      });
  }
  return token;
}

async function call<T>(method: string, url: string, body?: unknown, retried = false): Promise<Outcome<T>> {
  try {
    const headers: Record<string, string> = { Accept: 'application/json' };
    let payload: BodyInit | undefined;
    if (method !== 'GET') {
      headers['X-CSRF-TOKEN'] = await antiforgeryToken();
      if (body instanceof FormData) {
        payload = body;
      } else if (body !== undefined) {
        headers['Content-Type'] = 'application/json';
        payload = JSON.stringify(body);
      }
    }

    const r = await fetch(url, { method, headers, body: payload, credentials: 'same-origin' });
    if (r.status === 204) return { kind: 'ok', data: undefined as T };
    if (r.ok) return { kind: 'ok', data: (await r.json()) as T };

    const problem = (await r.json().catch(() => ({}))) as {
      title?: string;
      type?: string;
      errors?: Record<string, string[]>;
    };
    if (r.status === 400 && problem.errors) {
      const errors: FieldErrors = {};
      for (const [k, v] of Object.entries(problem.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
      return { kind: 'invalid', errors, title: problem.title };
    }
    // A refused antiforgery token (expired page, new session): fetch a fresh one and try once more.
    if (r.status === 400 && method !== 'GET' && !retried) {
      await antiforgeryToken(true);
      return call<T>(method, url, body, true);
    }
    if (r.status === 401) return { kind: 'signin' };
    if (r.status === 403) return { kind: 'forbidden' };
    if (r.status === 404) return { kind: 'notfound', unavailable: problem.type === 'music:audio-unavailable' };
    return { kind: 'failed', title: problem.title };
  } catch {
    return { kind: 'failed' };
  }
}

export const musicApi = {
  albums: () => call<{ albums: AlbumSummary[]; permissions: MusicPermissions }>('GET', '/api/music/albums'),
  album: (id: number) => call<AlbumDetail>('GET', `/api/music/albums/${id}`),
  lyrics: (songId: number) => call<Lyrics>('GET', `/api/music/songs/${songId}/lyrics`),
  audio: (songId: number) => call<{ audioUrl: string }>('GET', `/api/music/songs/${songId}/audio`),
  play: (songId: number) =>
    call<{ audioUrl: string; counted: boolean; playCount: number }>('POST', `/api/music/songs/${songId}/plays`),
  videos: (songId: number) => call<Video[]>('GET', `/api/music/songs/${songId}/videos`),
  videoPlayed: (videoId: number) => call<{ counted: boolean }>('POST', `/api/music/videos/${videoId}/plays`),
  statistics: () => call<Statistics>('GET', '/api/music/statistics'),
  members: () => call<Member[]>('GET', '/api/music/members'),
  albumForEdit: (id: number) => call<AlbumEdit>('GET', `/api/music/albums/${id}/edit`),
  songForEdit: (id: number) => call<SongEdit>('GET', `/api/music/songs/${id}/edit`),

  saveAlbum: (id: number | null, input: AlbumInput, cover: Blob | null) => {
    const form = new FormData();
    form.set('title', input.title);
    form.set('year', input.year);
    form.set('description', input.description);
    form.set('isPrivate', String(input.isPrivate));
    form.set('isExclusive', String(input.isExclusive));
    input.accessUserIds.forEach((u) => form.append('accessUserIds', u));
    if (cover) form.set('cover', cover, cover.type === 'image/jpeg' ? 'album-cover.jpg' : 'album-cover.webp');
    return id === null
      ? call<AlbumSummary>('POST', '/api/music/albums', form)
      : call<AlbumSummary>('PUT', `/api/music/albums/${id}`, form);
  },
  deleteAlbum: (id: number) => call<void>('DELETE', `/api/music/albums/${id}`),

  saveSong: (albumId: number, songId: number | null, input: SongInput) =>
    songId === null
      ? call<{ id: number }>('POST', `/api/music/albums/${albumId}/songs`, input)
      : call<{ id: number }>('PUT', `/api/music/songs/${songId}`, input),
  deleteSong: (id: number) => call<void>('DELETE', `/api/music/songs/${id}`),

  uploadVideo: (songId: number, file: File, title: string) => {
    const form = new FormData();
    form.set('file', file);
    form.set('title', title);
    return call<Video>('POST', `/api/music/songs/${songId}/videos`, form);
  },
  deleteVideo: (id: number) => call<void>('DELETE', `/api/music/videos/${id}`),
};

export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;

/** The members' login, coming back to `path` afterwards. */
export const loginTo = (path: string) => `/login?returnUrl=${encodeURIComponent(path)}`;
