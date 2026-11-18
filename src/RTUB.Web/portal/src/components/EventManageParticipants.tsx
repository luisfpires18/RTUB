// Dialog to manage other members' answers (inscrições) on an event.

import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { eventsApi, type EnrollmentList, type MemberOption, type EventSummary, type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { failure, EventLine } from './EventManageShared';

/**
 * Other members' answers for Admin/Owner: every answer
 * grouped as "Quem vai" shows it, remove any of them, and add a member who has not answered yet (as
 * going, with their primary instrument, without a notification). Nobody's answer is edited here: each
 * member changes their own in the answer modal. `onChanged` refreshes the page behind.
 */
export function ParticipantsManagerDialog({ event, onClose, onChanged }: { event: EventSummary; onClose: () => void; onChanged: () => void }) {
  const [list, setList] = useState<EnrollmentList | null>();
  const [query, setQuery] = useState('');
  const [members, setMembers] = useState<MemberOption[]>();
  const [removing, setRemoving] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [version, setVersion] = useState(0); // search again after each write: who answered changed
  const searchId = useId();

  useEffect(() => {
    eventsApi.enrollments(event.id).then((o) => setList(o.kind === 'ok' ? o.data : null));
  }, [event.id]);

  useEffect(() => {
    if (!query.trim()) return setMembers(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      eventsApi.enrollmentMembers(event.id, query.trim()).then((o) => live && setMembers(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [event.id, query, version]);

  const run = async (call: Promise<Outcome<EnrollmentList>>, done: () => void = () => undefined) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setRemoving(undefined);
    if (o.kind === 'ok') {
      setList(o.data);
      setVersion((v) => v + 1);
      done();
      onChanged();
    } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
    else if (o.kind === 'closed') setError('Esta atuação foi cancelada: não leva inscrições novas.');
    else setError(failure(o, 'guardar a inscrição'));
  };

  const groups: [string, EnrollmentList['going']][] = list
    ? [
        [event.past ? 'Foram' : 'Vão', list.going],
        ['Leitões', list.leitoes],
        [event.past ? 'Não foram' : 'Não vão', list.notGoing],
      ]
    : [];

  return (
    <Dialog title="Gerir inscrições" onClose={onClose} size="lg">
      <div className="answer">
        <EventLine event={event} />
        {list === undefined ? (
          <Loading label="A carregar as inscrições…" />
        ) : list === null ? (
          <p className="form__error">Não foi possível carregar as inscrições desta atuação.</p>
        ) : (
          <>
            {groups.every(([, g]) => g.length === 0) && <p className="note">Ainda ninguém respondeu.</p>}
            {groups
              .filter(([, g]) => g.length > 0)
              .map(([label, g]) => (
                <section key={label} className="participants-manager__group" aria-label={label}>
                  <p className="eyebrow">
                    {label} · {g.length}
                  </p>
                  <ul className="prize-manager__list">
                    {g.map(({ id, participant: p }) => (
                      <li key={id} className="prize-manager__row">
                        <img className="participants-manager__avatar" src={p.avatarUrl} alt="" width="36" height="36" loading="lazy" />
                        <span className="prize-manager__name">
                          {removing === id ? (
                            <>Tirar a inscrição de {p.name}?</>
                          ) : (
                            <>
                              <strong>{p.name}</strong>
                              <span className="participants-manager__meta">{[p.badge, p.instrument].filter(Boolean).join(' · ')}</span>
                              {p.notes && <span className="participants-manager__meta participants-manager__note">“{p.notes}”</span>}
                            </>
                          )}
                        </span>
                        {removing === id ? (
                          <>
                            <button type="button" className="btn btn--danger btn--sm" onClick={() => run(eventsApi.removeMemberEnrollment(event.id, id))} disabled={busy}>
                              Tirar
                            </button>
                            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoving(undefined)} disabled={busy}>
                              Voltar
                            </button>
                          </>
                        ) : (
                          <button
                            type="button"
                            className="icon-btn icon-btn--sm icon-btn--danger"
                            onClick={() => setRemoving(id)}
                            disabled={busy}
                            title="Tirar a inscrição"
                          >
                            <Icon name="close" />
                            <span className="sr-only">Tirar a inscrição de {p.name}</span>
                          </button>
                        )}
                      </li>
                    ))}
                  </ul>
                </section>
              ))}

            {list.canAdd && (
              <div className="repertoire-manager__add">
                <p className="eyebrow">Inscrever um membro</p>
                <label className="control" htmlFor={searchId}>
                  <span className="sr-only">Procurar membro</span>
                  <Icon name="search" />
                  <input id={searchId} type="search" placeholder="Procurar pelo nome" value={query} onChange={(e) => setQuery(e.target.value)} />
                </label>
                <p className="form__hint">Fica como «vai», com o instrumento principal. Não é enviada notificação.</p>
                {members && members.length === 0 && <p className="note">Nenhum membro por responder com esse nome.</p>}
                {members && members.length > 0 && (
                  <ul className="prize-manager__list">
                    {members.map((m) => (
                      <li key={m.id} className="prize-manager__row">
                        <img className="participants-manager__avatar" src={m.avatarUrl} alt="" width="36" height="36" loading="lazy" />
                        <span className="prize-manager__name">
                          <strong>{m.name}</strong>
                          {m.fullName && m.fullName !== m.name && <span className="participants-manager__meta">{m.fullName}</span>}
                        </span>
                        <button type="button" className="btn btn--primary btn--sm" onClick={() => run(eventsApi.addEnrollment(event.id, m.id))} disabled={busy}>
                          <Icon name="plus" />
                          Inscrever
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </>
        )}
        {error && (
          <p className="form__error" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}
