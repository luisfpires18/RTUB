// The rehearsal page's attendee pieces: instrument counts, attendee groups and the add-attendee form.

import { useEffect, useId, useState } from 'react';
import type { MemberOption, Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { MemberBadge, badgeKind } from './MemberBadge';
import { rehearsalsApi, type Attendee, type InstrumentCount } from '../data/rehearsalsApi';

export function Counts({ counts }: { counts: InstrumentCount[] }) {
  return (
    <ul className="chips">
      {counts.map((c) => (
        <li key={c.instrument} className="chip">
          {c.instrument} · {c.count}
        </li>
      ))}
    </ul>
  );
}

/** Avatar-led tiles, as "Quem vai" on events; Admin/Owner confirm or remove, a member removes their own. */
export function Group({ title, people, rehearsalId, onChanged }: { title: string; people: Attendee[]; rehearsalId: number; onChanged: () => void }) {
  const [removing, setRemoving] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  if (people.length === 0) return null;

  const run = async (call: Promise<Outcome<void>>) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setRemoving(undefined);
    if (o.kind === 'ok') onChanged();
    else setError(o.kind === 'closed' ? 'Este ensaio já não permite isto.' : 'Não foi possível guardar. Tenta outra vez.');
  };

  return (
    <div className="presence-group">
      {title && (
        <h3 className="who__group">
          {title} · {people.length}
        </h3>
      )}
      <ul className="who">
        {people.map((p) => (
          <li key={p.id} className="who__person" title={p.member.fullName ?? undefined}>
            <img className="who__avatar" src={p.member.avatarUrl} alt="" loading="lazy" width="72" height="72" />
            <p className="who__name">{p.member.name}</p>
            {p.member.badge && <MemberBadge kind={badgeKind(p.member.badge)}>{p.member.badge}</MemberBadge>}
            {p.status !== 'notGoing' && (
              <span className={p.status === 'approved' ? 'pill pill--yes' : 'pill pill--wait'}>{p.status === 'approved' ? 'Confirmada' : 'Pendente'}</span>
            )}
            {p.instrument && <p className="who__meta">{p.instrument}</p>}
            {p.notes && <p className="who__note">“{p.notes}”</p>}
            {(p.canApprove || p.canRemove) && (
              <div className="presence-actions">
                {p.canApprove && (
                  <button type="button" className="btn btn--primary btn--sm" disabled={busy} onClick={() => run(rehearsalsApi.approve(rehearsalId, p.id))}>
                    <Icon name="check" />
                    Confirmar
                  </button>
                )}
                {p.canApprove && (
                  <button type="button" className="btn btn--ghost btn--sm" disabled={busy} onClick={() => run(rehearsalsApi.refuse(rehearsalId, p.id))}>
                    <Icon name="close" />
                    Recusar
                  </button>
                )}
                {p.canRemove &&
                  (removing === p.id ? (
                    <>
                      <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(rehearsalsApi.removeAttendee(rehearsalId, p.id))}>
                        Remover
                      </button>
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoving(undefined)}>
                        Não
                      </button>
                    </>
                  ) : (
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setRemoving(p.id)} aria-label={`Remover a presença de ${p.member.name}`}>
                      <Icon name="trash" />
                    </button>
                  ))}
              </div>
            )}
          </li>
        ))}
      </ul>
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}

/** Admin/Owner, once the rehearsal can be approved: add someone who was there (confirmed, primary instrument). */
export function AddAttendee({ rehearsalId, onAdded }: { rehearsalId: number; onAdded: () => void }) {
  const [query, setQuery] = useState('');
  const [members, setMembers] = useState<MemberOption[]>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setMembers(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      rehearsalsApi.members(rehearsalId, query.trim()).then((o) => live && setMembers(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [rehearsalId, query]);

  const add = async (m: MemberOption) => {
    setBusy(true);
    const o = await rehearsalsApi.addAttendee(rehearsalId, m.id);
    setBusy(false);
    if (o.kind !== 'ok') return setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : 'Não foi possível acrescentar. Tenta outra vez.');
    setError(undefined);
    setQuery('');
    onAdded();
  };

  return (
    <div className="talk__seat-picker presence-add">
      <label className="control control--sm" htmlFor={id}>
        <span className="sr-only">Acrescentar presença</span>
        <Icon name="search" />
        <input id={id} type="search" placeholder="Acrescentar presença (alcunha ou nome)" value={query} onChange={(e) => setQuery(e.target.value)} />
      </label>
      {members && members.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {members && members.length > 0 && (
        <ul className="talk__picks">
          {members.map((m) => (
            <li key={m.id}>
              <button type="button" disabled={busy} onClick={() => add(m)}>
                <img src={m.avatarUrl} alt="" width="28" height="28" loading="lazy" />
                <span>
                  {m.name}
                  {m.fullName && m.fullName !== m.name && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}
