// Form state and helpers of the ata editor: server ata to form values and back, PDF download, error text.
import type { MeetingAgendaPoint, MeetingAtaEditor, MeetingAtaInput } from './meetingsTypes';

export type Point = {
  key: number;
  title: string;
  discussion: string;
  decision: string;
  votesFor: string;
  votesAgainst: string;
  votesAbstain: string;
  result: string;
};

export type Values = {
  ataNumber: string;
  actualStartTime: string;
  actualEndTime: string;
  location: string;
  quorumBasis: string;
  firstSecretaryId: string;
  secondSecretaryId: string;
  closingText: string;
  points: Point[];
};

let nextKey = 1;

export const toPoint = (p: Partial<MeetingAgendaPoint> & { title: string }): Point => ({
  key: nextKey++,
  title: p.title,
  discussion: p.discussion ?? '',
  decision: p.decision ?? '',
  votesFor: p.votesFor == null ? '' : String(p.votesFor),
  votesAgainst: p.votesAgainst == null ? '' : String(p.votesAgainst),
  votesAbstain: p.votesAbstain == null ? '' : String(p.votesAbstain),
  result: p.result ?? '',
});

export const fromEditor = (e: MeetingAtaEditor): Values => ({
  ataNumber: e.ataNumber ?? '',
  actualStartTime: e.actualStartTime,
  actualEndTime: e.actualEndTime ?? '',
  location: e.location,
  quorumBasis: e.quorumBasis,
  firstSecretaryId: e.firstSecretaryId ?? '',
  secondSecretaryId: e.secondSecretaryId ?? '',
  closingText: e.closingText ?? '',
  points: e.agendaPoints.map(toPoint),
});

export const votes = (value: string) => {
  if (!value.trim()) return null;
  const n = Number(value);
  return Number.isFinite(n) ? Math.trunc(n) : null;
};

const blank = (value: string) => (value.trim() ? value : null);

export const toInput = (v: Values, kind: MeetingAtaEditor['kind']): MeetingAtaInput => ({
  ataNumber: blank(v.ataNumber),
  actualStartTime: v.actualStartTime,
  actualEndTime: blank(v.actualEndTime),
  location: v.location,
  quorumBasis: kind === 'ag' ? v.quorumBasis : null,
  firstSecretaryId: blank(v.firstSecretaryId),
  secondSecretaryId: kind === 'cv' ? null : blank(v.secondSecretaryId),
  agendaPoints: v.points.map((p) => ({
    title: p.title,
    discussion: blank(p.discussion),
    decision: blank(p.decision),
    votesFor: votes(p.votesFor),
    votesAgainst: votes(p.votesAgainst),
    votesAbstain: votes(p.votesAbstain),
    result: blank(p.result),
  })),
  closingText: blank(v.closingText),
});

/** Downloads a file the server answers as an attachment (the ata PDF), without leaving the page. */
export function download(url: string) {
  const link = document.createElement('a');
  link.href = url;
  link.download = '';
  document.body.append(link);
  link.click();
  link.remove();
}

export const editorProblem = (kind: string, fallback: string) =>
  kind === 'forbidden'
    ? 'Não pode escrever a ata desta reunião.'
    : kind === 'closed'
      ? 'A ata desta reunião não pode ser alterada: já foi publicada, a reunião foi cancelada ou ainda não aconteceu.'
      : fallback;
