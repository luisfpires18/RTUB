// Public-portal pilot data. Portal copy is written for the portal: the current site is a source
// of facts (dates, names, album titles, positions), never of sentences - PortalCopyOriginalityTests
// enforces that. Nothing here is read from the database; see docs/react-portal-pilot.md.

/** Blazor routes the portal links out to: temporary bridges until each module has a React version. */
export const legacy = {
  forgotPassword: '/forgot-password',
  events: '/events',
  gallery: '/gallery',
  memberProfile: '/member/profile',
  memberGovernance: '/member/roles',
} as const;

/** React-owned routes (Program.cs maps exactly these; the old /portal... URLs redirect here). */
export const portal = {
  home: '/',
  privacy: '/privacy',
  profile: '/profile',
  request: '/request',
  music: '/music',
  login: '/login',
  roles: '/roles',
} as const;

/** Sign in, then come back to the React profile. */
export const loginToProfile = `${portal.login}?returnUrl=${encodeURIComponent(portal.profile)}`;

// FACT: the store listing the Blazor PlayStorePrompt links to; package matches
// wwwroot/.well-known/assetlinks.json (the Android app is a TWA of this site).
export const playStoreUrl = 'https://play.google.com/store/apps/details?id=ipb.pt.rtub.app';

export const contactEmail = 'realtunab@gmail.com';

export const social = [
  { name: 'Spotify', icon: 'spotify', href: 'https://open.spotify.com/artist/0h6qse9m0OpJ2K6H0IFAgD' },
  { name: 'YouTube', icon: 'youtube', href: 'https://www.youtube.com/@RTUBOficial' },
  { name: 'Instagram', icon: 'instagram', href: 'https://www.instagram.com/rtub1991/' },
  { name: 'Facebook', icon: 'facebook', href: 'https://www.facebook.com/rtub.tuna.braganca' },
] as const;

// FACT: the published discography on /music (title, year, number of tracks).
export const albums = [
  { title: 'Tunalidades', year: '1995', tracks: 11 },
  { title: '50% Música 51% Álcool', year: '1998', tracks: 16 },
  { title: 'Boémios e Trovadores', year: '2007', tracks: 15 },
  { title: 'É Esta a Tuna', year: '2013', tracks: 11 },
];

// FACT: the instruments the tuna plays.
export const instruments = ['Guitarra', 'Bandolim', 'Cavaquinho', 'Voz'];

// ILLUSTRATIVE captions, labelled as such on the page: the tiles are artwork, not photographs.
export const galleryTiles = [
  { caption: 'Serenata ao luar', icon: 'music', tone: 'a' },
  { caption: 'Casa cheia', icon: 'play', tone: 'b' },
  { caption: 'Noites de FITAB', icon: 'calendar', tone: 'c' },
  { caption: 'De estrada em estrada', icon: 'geo', tone: 'd' },
  { caption: 'Ensaio geral', icon: 'clock', tone: 'e' },
] as const;

// FACT: the bodies and positions of the Órgãos Sociais; holders live on /roles (GET /api/public/governance).
export const governingBodies = [
  { name: 'Direção', roles: ['Magister', 'Vice-Magister', 'Secretário', '1.º Tesoureiro', '2.º Tesoureiro'] },
  { name: 'Mesa da Assembleia', roles: ['Presidente', '1.º Secretário', '2.º Secretário'] },
  { name: 'Conselho Fiscal', roles: ['Presidente', '1.º Relator', '2.º Relator'] },
  { name: 'Conselho de Veteranos', roles: ['Presidente'] },
];
