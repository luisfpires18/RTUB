// Pending actions categories, texts and date labels.
import { type Category } from './pendingActionsApi';

export const CATEGORIES: { key: Category | 'all'; label: string }[] = [
  { key: 'all', label: 'Tudo' },
  { key: 'participation', label: 'Participação' },
  { key: 'tasks', label: 'Tarefas' },
  { key: 'responsibilities', label: 'Responsabilidades' },
];

// Each module's own note limit (EventAgendaService.MaxNotesLength, RehearsalAgendaService.NotesMax); the server checks it too.
export const NOTES_MAX: Record<string, number> = { 'event.response': 1000, 'rehearsal.response': 500 };

export const EMPTY_PANEL: Record<string, string> = {
  logistics: 'Não tens cartões por concluir.',
  questions: 'Não tens questões abertas.',
  secretary: 'Nada por criar nas pastas deste ano.',
  meetings: 'Todas as reuniões passadas têm ata.',
  approvals: 'Nenhuma presença por aprovar.',
  repertoire: 'As próximas atuações têm repertório.',
  members: 'Nenhum membro com informação em falta.',
};

/** What a saved command says (072 answers: the default). */
export const DONE: Record<string, string> = {
  done: 'Cartão concluído.',
  close: 'Questão fechada.',
  create: 'Criado.',
  none: 'Marcado sem movimentos.',
  reopen: 'Reaberto: volta a «Por classificar».',
  approve: 'Presença aprovada.',
  refuse: 'Presença recusada.',
};

export const EMPTY: Record<string, string> = {
  upcoming: 'Nada por responder nas próximas.',
  past: 'Nada por responder neste ano.',
  open: 'Todos os eventos do ano estão classificados.',
  none: 'Nenhum evento marcado sem movimentos.',
};

/** "yyyy-mm-dd" as a calendar day (no time zone shift). */
const asDay = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
};

export const longDate = (iso: string) =>
  new Intl.DateTimeFormat('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(asDay(iso));

export const rowDate = (iso: string, today: string) =>
  new Intl.DateTimeFormat('pt-PT', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    ...(iso.slice(0, 4) !== today.slice(0, 4) ? { year: 'numeric' } : {}),
  }).format(asDay(iso));
