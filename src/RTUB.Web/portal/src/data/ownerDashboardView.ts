// Owner dashboard ranges, page labels and formatting helpers.
import { type IconName } from '../components/icons';

export const RANGES = [
  { days: 7, label: '7 dias' },
  { days: 30, label: '30 dias' },
  { days: 90, label: '90 dias' },
  { days: 365, label: '12 meses' },
] as const;

export const ICONS: Record<string, IconName> = {
  signIns: 'login',
  activeMembers: 'people',
  movements: 'shield',
  uploads: 'upload',
  songPlays: 'music',
  naipePlays: 'naipes',
  enrollments: 'calendarCheck',
  pageViews: 'eye',
};

/** Routes as the server stores them (ids → {id}); anything else (Mensagens included) shows its path. */
export const PAGE_LABELS: Record<string, string> = {
  '/': 'Início',
  '/events': 'Atuações',
  '/events/{id}': 'Atuação',
  '/events/{id}/discussion': 'Discussão de atuação',
  '/rehearsals': 'Ensaios',
  '/rehearsals/{id}': 'Ensaio',
  '/music': 'Música',
  '/music/albums/{id}': 'Álbum',
  '/gallery': 'Galeria',
  '/news': 'Novidades',
  '/shop': 'Loja',
  '/members': 'Membros',
  '/members/hierarchy': 'Hierarquia',
  '/members/map': 'Mapa de membros',
  '/leaderboard': 'Classificação',
  '/inventory': 'Inventário',
  '/documentation': 'Documentação',
  '/logistics': 'Logística',
  '/logistics/{id}': 'Quadro de Logística',
  '/treasury': 'Tesouraria',
  '/treasury/reports': 'Relatórios',
  '/treasury/reports/{id}': 'Relatório',
  '/treasury/calotes': 'Calotes',
  '/treasury/mbway': 'MBWay',
  '/treasury/nerba': 'Nerba',
  '/requests': 'Pedidos de atuação',
  '/questions': 'Perguntas',
  '/hall-of-fame': 'Hall of Fame',
  '/naipes': 'Naipes',
  '/meetings': 'Reuniões',
  '/profile': 'Perfil',
  '/pending-actions': 'As minhas ações pendentes',
  '/roles': 'Órgãos Sociais',
  '/login': 'Entrar',
  '/request': 'Pedir atuação',
  '/privacy': 'Privacidade',
};

export const WEEKDAYS = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];


export const addDays = (day: string, n: number) => {
  const d = new Date(`${day}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};

/** dd/MM, or "seg., 12/10" with the weekday. */
export const shortDay = (day: string, weekday = false) => {
  const d = new Date(`${day}T00:00:00Z`);
  return new Intl.DateTimeFormat('pt-PT', { timeZone: 'UTC', day: '2-digit', month: '2-digit', ...(weekday ? { weekday: 'short' } : {}) }).format(d);
};

export const number = (n: number) => new Intl.NumberFormat('pt-PT').format(n);
