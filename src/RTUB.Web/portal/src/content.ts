// Public-portal pilot data. Portal copy is written for the portal: the current site is a source
// of facts (dates, names, album titles, positions), never of sentences - PortalCopyOriginalityTests
// enforces that. Nothing here is read from the database; see docs/react-portal-pilot.md.

/** Legacy Blazor routes the portal links out to. They stay Blazor-owned during the pilot. */
export const legacy = {
  home: '/',
  login: '/login',
  request: '/request',
  events: '/events',
  music: '/music',
  gallery: '/gallery',
  roles: '/roles',
} as const;

export const contactEmail = 'realtunab@gmail.com';

export const social = [
  { name: 'Spotify', icon: 'spotify', href: 'https://open.spotify.com/artist/0h6qse9m0OpJ2K6H0IFAgD' },
  { name: 'YouTube', icon: 'youtube', href: 'https://www.youtube.com/@RTUBOficial' },
  { name: 'Instagram', icon: 'instagram', href: 'https://www.instagram.com/rtub1991/' },
  { name: 'Facebook', icon: 'facebook', href: 'https://www.facebook.com/rtub.tuna.braganca' },
] as const;

export type PortalEvent = { date: string; title: string; place: string; kind: string };

// ILLUSTRATIVE: plausible dates, labelled as such on the page. The real agenda stays on /events
// until a read-only API exists.
export const upcomingEvents: PortalEvent[] = [
  { date: '2026-10-17', title: 'Receção ao Caloiro', place: 'Bragança', kind: 'Festa académica' },
  { date: '2026-12-01', title: '35.º aniversário da RTUB', place: 'Bragança', kind: 'Aniversário' },
  { date: '2027-03-13', title: 'FITAB · Festival Internacional de Tunas Académicas', place: 'Bragança', kind: 'Festival' },
];

// FACT: the published discography on /music (title, year, number of tracks).
export const albums = [
  { title: 'Tunalidades', year: '1995', tracks: 11 },
  { title: '50% Música 51% Álcool', year: '1998', tracks: 16 },
  { title: 'Boémios e Trovadores', year: '2007', tracks: 15 },
  { title: 'É Esta a Tuna', year: '2013', tracks: 11 },
];

// FACT: the instruments the tuna plays.
export const naipes = ['Guitarra', 'Bandolim', 'Cavaquinho', 'Voz'];

// ILLUSTRATIVE captions; the tiles are artwork, not photographs.
export const galleryTiles = [
  { caption: 'Serenata ao luar', icon: 'music', tone: 'a' },
  { caption: 'Casa cheia', icon: 'play', tone: 'b' },
  { caption: 'Noites de FITAB', icon: 'calendar', tone: 'c' },
  { caption: 'De estrada em estrada', icon: 'geo', tone: 'd' },
  { caption: 'Ensaio geral', icon: 'clock', tone: 'e' },
] as const;

// FACT: the bodies and positions shown on /roles. Holders are not shown in the pilot.
export const orgaosSociais = [
  { name: 'Direção', roles: ['Magister', 'Vice-Magister', 'Secretário', '1.º Tesoureiro', '2.º Tesoureiro'] },
  { name: 'Mesa da Assembleia', roles: ['Presidente', '1.º Secretário', '2.º Secretário'] },
  { name: 'Conselho Fiscal', roles: ['Presidente', '1.º Relator', '2.º Relator'] },
  { name: 'Conselho de Veteranos', roles: ['Presidente'] },
];
