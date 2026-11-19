// Typed client for /api/music (Endpoints/MusicEndpoints.cs). The server decides
// what the caller may see and do; `permissions` only spares people buttons that would be refused.

import { call } from './apiCall';

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
  /** Members only: the song has a play-along chart. */
  hasPlayAlong: boolean;
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
type SongEdit = {
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
  hasPlayAlong: boolean;
};

/** The play-along chart, version 1 (server: PlayAlongChart.cs). Times are seconds into the recording. */
type PlayAlongChord = { label: string; name: string; time: number; word: number | null; char: number | null };
export type PlayAlongLine = {
  text: string;
  start: number;
  end: number;
  words: string[];
  wordTimes: number[];
  chords: PlayAlongChord[];
};
export type PlayAlongSection = { name: string; kind: 'verse' | 'refrain' | 'instrumental'; start: number; end: number; lines: PlayAlongLine[] };
export type PlayAlongChart = { version: number; duration: number; bpm: number; beats: number[]; sections: PlayAlongSection[]; picking: boolean };
export type PlayAlong = { songId: number; title: string; albumId: number; albumTitle: string; hasAudio: boolean; chart: PlayAlongChart };
export type LearnSong = { songId: number; title: string; albumId: number; albumTitle: string; year: number | null; views: number; starred: boolean };
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

  learnSongs: () => call<{ songs: LearnSong[]; canManage: boolean }>('GET', '/api/music/playalongs'),
  viewPlayAlong: (songId: number) => call<void>('POST', `/api/music/songs/${songId}/playalong/view`),
  starPlayAlong: (songId: number, starred: boolean) => call<void>(starred ? 'PUT' : 'DELETE', `/api/music/songs/${songId}/playalong/star`),
  notifyPlayAlong: (songId: number, message: string) => call<void>('POST', `/api/music/songs/${songId}/playalong/notify`, { message }),
  playAlong: (songId: number) => call<PlayAlong>('GET', `/api/music/songs/${songId}/playalong`),
  /** `chart` is the parsed chart file as it is; the server validates it. */
  savePlayAlong: (songId: number, chart: unknown) => call<void>('PUT', `/api/music/songs/${songId}/playalong`, chart),
  deletePlayAlong: (songId: number) => call<void>('DELETE', `/api/music/songs/${songId}/playalong`),

  uploadVideo: (songId: number, file: File, title: string) => {
    const form = new FormData();
    form.set('file', file);
    form.set('title', title);
    return call<Video>('POST', `/api/music/songs/${songId}/videos`, form);
  },
  deleteVideo: (id: number) => call<void>('DELETE', `/api/music/videos/${id}`),
};

export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;
