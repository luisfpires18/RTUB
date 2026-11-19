// A lift offer (transport) post: its details, passengers and editor.

import { useEffect, useId, useState } from 'react';
import { FieldError } from './ui/FieldError';
import { eventsApi, type EventPost, type MemberOption, type EventDiscussion as Discussion } from '../data/eventsApi';
import type { Outcome } from '../data/api';
import { Icon } from './icons';
import { problem } from './EventDiscussionShared';
import { EmptyNote } from './ui/EmptyNote';
import { Avatar } from './ui/Avatar';
import { SearchField } from './ui/SearchTools';

// ---------- a lift offer ----------

export function Lift({ eventId, post, editing, onDone, onChange }: { eventId: number; post: EventPost; editing: boolean; onDone: () => void; onChange: (d: Discussion) => void }) {
  const t = post.transport!;
  const [form, setForm] = useState({ vehicle: t.vehicle, seats: t.totalSeats, notes: t.notes ?? '' });
  const [query, setQuery] = useState('');
  const [members, setMembers] = useState<MemberOption[]>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ids = { vehicle: useId(), seats: useId(), notes: useId(), search: useId() };
  const free = t.totalSeats - t.passengers.length;

  useEffect(() => {
    if (!t.canManage || !query.trim()) return setMembers(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      eventsApi.passengerMembers(eventId, post.id, query.trim()).then((o) => live && setMembers(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [eventId, post.id, query, t.canManage, t.passengers.length]);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      setErrors({});
      done?.();
      onChange(o.data);
    } else setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o) });
  };

  if (editing) {
    return (
      <form
        className="talk__edit"
        onSubmit={(e) => {
          e.preventDefault();
          run(eventsApi.editTransport(eventId, post.id, form.vehicle, form.seats, form.notes), onDone);
        }}
      >
        <div className="talk__lift-fields">
          <div className="form__field">
            <label htmlFor={ids.vehicle}>Viatura</label>
            <input id={ids.vehicle} maxLength={200} value={form.vehicle} onChange={(e) => setForm({ ...form, vehicle: e.target.value })} />
            <FieldError text={errors.vehicle} alert={false} />
          </div>
          <div className="form__field">
            <label htmlFor={ids.seats}>Lugares</label>
            <input id={ids.seats} type="number" min={2} max={20} value={form.seats} onChange={(e) => setForm({ ...form, seats: Number(e.target.value) })} />
            <FieldError text={errors.seats} alert={false} />
          </div>
        </div>
        <div className="form__field">
          <label htmlFor={ids.notes}>Notas</label>
          <textarea id={ids.notes} rows={2} maxLength={500} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
          <FieldError text={errors.notes} alert={false} />
        </div>
        <FieldError text={errors.form} alert={false} />
        <div className="talk__composer-foot">
          <button type="button" className="btn btn--ghost btn--sm" onClick={onDone}>
            Cancelar
          </button>
          <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
            Guardar
          </button>
        </div>
      </form>
    );
  }

  return (
    <div className="talk__lift">
      <div className="talk__lift-head">
        <Icon name="car" />
        <div>
          <p className="talk__lift-title">Boleia · {t.vehicle}</p>
          {t.notes && <p className="talk__lift-notes">{t.notes}</p>}
        </div>
        <span className={`talk__seats${free === 0 ? ' is-full' : ''}`}>
          {t.passengers.length}/{t.totalSeats} lugares
        </span>
      </div>
      {t.passengers.length === 0 ? (
        <EmptyNote>Ainda ninguém nesta boleia.</EmptyNote>
      ) : (
        <ul className="talk__passengers" aria-label="Quem vai nesta boleia">
          {t.passengers.map((p) => (
            <li key={p.id} className="talk__passenger" title={p.member.fullName ?? undefined}>
              <Avatar src={p.member.avatarUrl} size={28} />
              {p.member.name}
              {p.canRemove && (
                <button
                  type="button"
                  className="icon-btn icon-btn--sm"
                  disabled={busy}
                  onClick={() => run(eventsApi.removePassenger(eventId, post.id, p.id))}
                  aria-label={p.mine ? 'Sair desta boleia' : `Tirar ${p.member.name} da boleia`}
                >
                  <Icon name="close" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {t.canManage && free > 0 && (
        <div className="talk__seat-picker">
          <SearchField small id={ids.search} label="Juntar um membro à boleia" placeholder="Juntar alguém (alcunha ou nome)" value={query} onChange={setQuery} />
          {members && members.length === 0 && <EmptyNote>Ninguém encontrado.</EmptyNote>}
          {members && members.length > 0 && (
            <ul className="talk__picks">
              {members.map((m) => (
                <li key={m.id}>
                  <button type="button" disabled={busy} onClick={() => run(eventsApi.addPassenger(eventId, post.id, m.id), () => setQuery(''))}>
                    <Avatar src={m.avatarUrl} size={28} />
                    <span>
                      {m.name}
                      {m.fullName && m.fullName !== m.name && <small>{m.fullName}</small>}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {(errors.userId || errors.form) && <p className="form__error">{errors.userId ?? errors.form}</p>}
    </div>
  );
}
