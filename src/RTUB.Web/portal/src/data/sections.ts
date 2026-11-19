// The public site sections and the page or home anchor each one links to.
import { portal } from './content';

export const sections = [
  { id: 'music', label: 'Música' },
  { id: 'news', label: 'Novidades' },
  { id: 'gallery', label: 'Galeria' },
  { id: 'events', label: 'Atuações' },
  { id: 'governance', label: 'Órgãos Sociais' },
  { id: 'shop', label: 'Loja' },
  { id: 'request', label: 'Pedidos' },
];

// The visitors' top bar and menu stay short: six public sections plus the "Pedir atuação" call to action.
// Signed-in members get the member shell instead (MemberShell.tsx): their menu, not the public one.
export const navSections = sections.filter((s) => s.id !== 'request');

// A section with its own React page links there (top bar, menu); the rest are home
// anchors, always /#id so the same link scrolls on the home page and navigates from elsewhere.
const sectionPages: Record<string, string> = {
  events: portal.events,
  news: portal.news,
  music: portal.music,
  governance: portal.roles,
  gallery: portal.gallery,
  shop: portal.shop,
};
export const sectionHref = (id: string) => sectionPages[id] ?? `/#${id}`;
