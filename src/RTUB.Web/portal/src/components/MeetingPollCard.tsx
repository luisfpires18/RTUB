// One poll card on /meetings/polls (vote, close, remove); /trintuna reuses it for its date poll (onVote).
import { useEffect, useState } from 'react';
import { portal } from '../data/content';
import { shareLink } from '../data/apiSession';
import { problem, typeClass } from './MeetingUi';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingPoll } from '../data/meetingsTypes';
import type { Outcome } from '../data/api';
import { longDateTime } from '../data/meetingsDates';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { FieldError } from './ui/FieldError';

export const pollProblem = (outcome: Parameters<typeof problem>[0], what: string) =>
  outcome.kind === 'closed'
    ? 'Esta votação já está encerrada.'
    : outcome.kind === 'notfound'
      ? 'Esta votação já não existe ou deixou de estar disponível.'
      : problem(outcome, what);

/** `onVote`: another area's vote call (Trintuna); the card then has no "Copiar link" (the link opens /meetings/polls). */
export function PollCard({ poll, onChanged, onVote }: { poll: MeetingPoll; onChanged: () => void; onVote?: (optionId: number) => Promise<Outcome<unknown>> }) {
  const [selected, setSelected] = useState<number | null>(poll.myOptionId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmClose, setConfirmClose] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => setSelected(poll.myOptionId), [poll.myOptionId]);

  // The link opens this poll for a signed-in member in its audience; anyone else is asked to sign in or sees nothing.
  const copyLink = async () => {
    const link = await shareLink(`${portal.meetingPolls}?poll=${poll.id}`);
    try {
      await navigator.clipboard.writeText(link);
      setError(undefined);
      setCopied(true);
    } catch {
      // The browser refused the clipboard: show the link to select by hand.
      setError(`Não foi possível copiar. Link: ${link}`);
    }
  };

  const vote = async () => {
    if (selected === null) return;
    setBusy(true);
    setError(undefined);
    const outcome = await (onVote ? onVote(selected) : meetingsApi.votePoll(poll.id, selected));
    setBusy(false);
    if (outcome.kind !== 'ok') return setError(pollProblem(outcome, 'registar o voto'));
    onChanged();
  };

  const close = async () => {
    setBusy(true);
    const outcome = await meetingsApi.closePoll(poll.id);
    setBusy(false);
    if (outcome.kind !== 'ok') return pollProblem(outcome, 'encerrar a votação');
    onChanged();
  };

  const remove = async () => {
    setBusy(true);
    const outcome = await meetingsApi.removePoll(poll.id);
    setBusy(false);
    if (outcome.kind !== 'ok') return pollProblem(outcome, 'eliminar a votação');
    onChanged();
  };

  return (
    <article className="mtg-poll">
      <div className="mtg-poll__top">
        <span className={typeClass(poll.audience === 'Direcao' ? 'ReuniaoDirecao' : poll.audience)}>{poll.audienceLabel}</span>
        <span className={`mtg-poll__status${poll.closed ? ' is-closed' : ''}`}>{poll.closed ? 'Encerrada' : 'Aberta'}</span>
      </div>
      <h3 className="mtg-poll__question">{poll.question}</h3>
      <p className="mtg-poll__meta">
        {poll.closesAt ? `Termina a ${longDateTime(poll.closesAt)}` : 'Sem data de fecho'}
        {' · '}
        {poll.totalVotes} {poll.totalVotes === 1 ? 'voto' : 'votos'}
      </p>
      <fieldset className="mtg-poll__options" disabled={!poll.canVote || busy}>
        <legend className="sr-only">Opções para {poll.question}</legend>
        {poll.options.map((option) => (
          <label className="mtg-poll__option" key={option.id}>
            {poll.canVote && (
              <input
                type="radio"
                name={`poll-${poll.id}`}
                value={option.id}
                checked={selected === option.id}
                onChange={() => setSelected(option.id)}
              />
            )}
            <span className="mtg-poll__option-text">{option.text}</span>
            <span className="mtg-poll__option-count">{option.votes}</span>
            <progress className="mtg-poll__bar" value={option.votes} max={Math.max(poll.totalVotes, 1)} aria-label={`${option.votes} votos`} />
          </label>
        ))}
      </fieldset>
      <FieldError text={error} />
      <div className="mtg-poll__actions">
        {poll.canVote && (
          <button type="button" className="btn btn--primary btn--sm" onClick={vote} disabled={selected === null || busy}>
            {busy ? 'A guardar…' : poll.myOptionId === null ? 'Votar' : 'Alterar voto'}
          </button>
        )}
        {poll.closed && poll.myOptionId !== null && <span className="note">O seu voto foi registado.</span>}
        {poll.canManage && !poll.closed && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirmClose(true)} disabled={busy}>
            <Icon name="lock" />
            Encerrar votação
          </button>
        )}
        {!onVote && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={copyLink}>
            <Icon name={copied ? 'check' : 'link'} />
            <span role="status">{copied ? 'Link copiado' : 'Copiar link'}</span>
          </button>
        )}
        {poll.canDelete && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirmDelete(true)} disabled={busy}>
            <Icon name="trash" />
            Eliminar
          </button>
        )}
      </div>
      {confirmDelete && (
        <AsyncConfirmDialog
          title="Eliminar votação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={() => setConfirmDelete(false)}
          onConfirm={remove}
        >
          <div className="mtg-confirm">
            <p>
              A votação "{poll.question}" é eliminada com as suas opções
              {poll.totalVotes > 0 ? ` e ${poll.totalVotes === 1 ? 'o voto já registado' : `os ${poll.totalVotes} votos já registados`}` : ''}. Não
              é possível recuperá-la.
            </p>
          </div>
        </AsyncConfirmDialog>
      )}
      {confirmClose && (
        <AsyncConfirmDialog
          title="Encerrar votação"
          confirmLabel="Encerrar"
          confirmIcon="lock"
          danger
          onClose={() => setConfirmClose(false)}
          onConfirm={close}
        >
          <div className="mtg-confirm">
            <p>Depois de encerrada, esta votação deixa de aceitar alterações ou novos votos.</p>
          </div>
        </AsyncConfirmDialog>
      )}
    </article>
  );
}
