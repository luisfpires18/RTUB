// The participants dialog of /meetings (who goes, who does not, add members).
import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import type { Outcome } from '../data/api';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCandidate, MeetingCard, MeetingParticipant, MeetingParticipants } from '../data/meetingsTypes';
import { MemberBadge, badgeKind } from './MemberBadge';
import { problem } from './MeetingUi';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';
import { Avatar } from './ui/Avatar';
import { Spinner } from './ui/Spinner';

/** "Participantes - {título}": who goes and who does not, with notes; Admin / Owner add members. */
export function ParticipantsDialog({ card, onClose, onChanged }: { card: MeetingCard; onClose: () => void; onChanged: () => void }) {
  const [list, setList] = useState<MeetingParticipants | null>();
  const [removing, setRemoving] = useState<MeetingParticipant>();
  const [adding, setAdding] = useState(false);
  const [query, setQuery] = useState('');
  const [candidates, setCandidates] = useState<MeetingCandidate[]>();
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; text: string }>();
  const [loadError, setLoadError] = useState<string>();
  const [version, setVersion] = useState(0);
  const searchId = useId();

  useEffect(() => {
    meetingsApi.participants(card.id).then((o) => {
      if (o.kind === 'ok') setList(o.data);
      else {
        setList(null);
        setLoadError(problem(o, 'carregar os participantes'));
      }
    });
  }, [card.id]);

  useEffect(() => {
    if (!adding || !query.trim()) return setCandidates(undefined);
    let live = true;
    const timer = setTimeout(() => {
      meetingsApi.candidates(card.id, query.trim()).then((o) => live && setCandidates(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [card.id, adding, query, version]);

  const run = async (action: Promise<Outcome<MeetingParticipants>>, success: string, what: string) => {
    setBusy(true);
    setFeedback(undefined);
    const o = await action;
    setBusy(false);
    setRemoving(undefined);
    if (o.kind === 'ok') {
      setList(o.data);
      setVersion((v) => v + 1);
      setFeedback({ ok: true, text: success });
      onChanged();
    } else setFeedback({ ok: false, text: problem(o, what) });
  };

  const group = (title: string, people: MeetingParticipant[], tone: 'yes' | 'no') =>
    people.length > 0 && (
      <section className="mtg-who" aria-label={title}>
        <h3 className={`mtg-who__title mtg-who__title--${tone}`}>
          {title} ({people.length})
        </h3>
        <ul className="who mtg-who__list">
          {people.map((p) => (
            <li key={p.id} className="who__person mtg-who__person" title={p.fullName || undefined}>
              <Avatar className="mtg-face" src={p.avatarUrl} size={64} />
              <p className="who__name">{p.nickname || p.fullName}</p>
              {p.fullName && p.fullName !== p.nickname && <p className="who__meta">{p.fullName}</p>}
              {p.badge && <MemberBadge kind={badgeKind(p.badge)}>{p.badge}</MemberBadge>}
              {p.notes && <p className="who__note">“{p.notes}”</p>}
              {p.canRemove && (
                <button
                  type="button"
                  className="icon-btn icon-btn--sm icon-btn--danger mtg-who__remove"
                  onClick={() => setRemoving(p)}
                  disabled={busy}
                  title="Remover participação"
                >
                  <Icon name="trash" />
                  <span className="sr-only">Remover a participação de {p.nickname || p.fullName}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      </section>
    );

  return (
    <Dialog
      title={`Participantes - ${card.title}`}
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Voltar Atrás
        </button>
      }
    >
      {list === undefined ? (
        <Loading label="A carregar os participantes…" />
      ) : list === null ? (
        <Banner text={loadError} />
      ) : (
        <div className="mtg-participants">
          {removing && (
            <div className="mtg-inline-confirm" role="alertdialog" aria-label="Confirmar Eliminação">
              <p>
                Tem a certeza que quer remover a participação de <strong>{removing.nickname || 'este membro'}</strong>?
              </p>
              <p className="note">Não é possível desfazer.</p>
              <span className="mtg-inline-confirm__actions">
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoving(undefined)} disabled={busy}>
                  Cancelar
                </button>
                <button
                  type="button"
                  className="btn btn--danger btn--sm"
                  onClick={() => run(meetingsApi.removeParticipation(card.id, removing.id), 'Participação removida com sucesso.', 'remover a participação')}
                  disabled={busy}
                >
                  {busy && <Spinner small />}
                  Eliminar
                </button>
              </span>
            </div>
          )}
          {feedback && <p className={feedback.ok ? 'mtg-feedback mtg-feedback--ok' : 'form__banner'} role={feedback.ok ? 'status' : 'alert'}>{feedback.text}</p>}
          {list.going.length === 0 && list.notGoing.length === 0 ? (
            <div className="mtg-empty">
              <Icon name="people" className="mtg-empty__icon" />
              <p className="mtg-empty__title">Nenhuma participação ainda para esta reunião</p>
            </div>
          ) : (
            <>
              {group('Vão participar', list.going, 'yes')}
              {group('Não vão participar', list.notGoing, 'no')}
            </>
          )}

          {list.canAdd && (
            <div className="mtg-add">
              {!adding ? (
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAdding(true)}>
                  <Icon name="plus" />
                  Adicionar Membro
                </button>
              ) : (
                <>
                  <label className="control" htmlFor={searchId}>
                    <span className="sr-only">Pesquisar membro</span>
                    <Icon name="search" />
                    <input
                      id={searchId}
                      type="search"
                      placeholder="Pesquisar membro por nome..."
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                      autoFocus
                    />
                  </label>
                  <p className="form__hint">Fica como «Vou». Não é enviada notificação.</p>
                  {candidates && candidates.length === 0 && <p className="note">Nenhum membro encontrado.</p>}
                  {candidates && candidates.length > 0 && (
                    <ul className="mtg-candidates">
                      {candidates.map((c) => (
                        <li key={c.id} className="mtg-candidate">
                          <Avatar className="mtg-face" src={c.avatarUrl} size={36} />
                          <span className="mtg-candidate__name">{c.name}</span>
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            onClick={() => run(meetingsApi.addParticipant(card.id, c.id), `Participação de ${c.name} adicionada com sucesso.`, 'adicionar o membro')}
                            disabled={busy}
                          >
                            <Icon name="plus" />
                            Adicionar
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm mtg-add__cancel"
                    onClick={() => {
                      setAdding(false);
                      setQuery('');
                    }}
                  >
                    <Icon name="xCircle" />
                    Cancelar
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}
