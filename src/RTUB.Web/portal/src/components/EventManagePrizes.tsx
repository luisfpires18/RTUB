// Prize list manager shown on the event page.

import { useEffect, useId, useState, type FormEvent } from 'react';
import { eventsApi, type EventPrize, type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { failure } from './EventManageShared';
import { Loading } from './ui/Loading';
import { FieldError } from './ui/FieldError';

/**
 * The event's prizes for Admin/Owner, inside the Prémios modal: rename or delete each one in
 * place, add a new one when the event takes prizes (a past festival). Every write answers the full
 * list; `onChanged` refreshes the page behind so its Prémios button and history follow.
 */
export function PrizeManager({ eventId, canAdd, onChanged }: { eventId: number; canAdd: boolean; onChanged: () => void }) {
  const [prizes, setPrizes] = useState<EventPrize[] | null>();
  const [name, setName] = useState('');
  const [editing, setEditing] = useState<{ id: number; name: string }>();
  const [deleting, setDeleting] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const ids = { add: useId(), edit: useId() };

  useEffect(() => {
    eventsApi.prizes(eventId).then((o) => setPrizes(o.kind === 'ok' ? o.data : null));
  }, [eventId]);

  const run = async (call: Promise<Outcome<EventPrize[]>>, done: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      setPrizes(o.data);
      done();
      onChanged();
    } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
    else if (o.kind === 'closed') setError('Só se juntam prémios a festivais que já aconteceram.');
    else setError(failure(o, 'guardar o prémio'));
  };

  const add = (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return setError('Indique o nome do prémio.');
    run(eventsApi.addPrize(eventId, name.trim()), () => setName(''));
  };

  const rename = (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    if (!editing.name.trim()) return setError('Indique o nome do prémio.');
    run(eventsApi.renamePrize(eventId, editing.id, editing.name.trim()), () => setEditing(undefined));
  };

  if (prizes === undefined) return <Loading label="A carregar os prémios…" />;
  if (prizes === null) return <p className="form__error">Não foi possível carregar os prémios desta atuação.</p>;

  return (
    <section className="prize-manager" aria-label="Gerir os prémios desta atuação">
      <p className="eyebrow">Nesta atuação · gerir</p>
      {prizes.length === 0 ? (
        <p className="note">Ainda sem prémios nesta atuação.</p>
      ) : (
        <ul className="prize-manager__list">
          {prizes.map((p) => (
            <li key={p.id}>
              {editing?.id === p.id ? (
                <form className="prize-manager__row" onSubmit={rename}>
                  <label className="sr-only" htmlFor={ids.edit}>
                    Novo nome do prémio
                  </label>
                  <input
                    id={ids.edit}
                    type="text"
                    maxLength={200}
                    value={editing.name}
                    onChange={(e) => setEditing({ id: p.id, name: e.target.value })}
                    disabled={busy}
                    autoFocus
                  />
                  <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
                    Guardar
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)} disabled={busy}>
                    Cancelar
                  </button>
                </form>
              ) : deleting === p.id ? (
                <div className="prize-manager__row" role="alert">
                  <span className="prize-manager__name">Apagar «{p.name}»?</span>
                  <button
                    type="button"
                    className="btn btn--danger btn--sm"
                    onClick={() => run(eventsApi.deletePrize(eventId, p.id), () => setDeleting(undefined))}
                    disabled={busy}
                  >
                    Apagar
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDeleting(undefined)} disabled={busy}>
                    Voltar
                  </button>
                </div>
              ) : (
                <div className="prize-manager__row">
                  <Icon name="trophy" />
                  <span className="prize-manager__name">{p.name}</span>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    onClick={() => {
                      setDeleting(undefined);
                      setEditing({ id: p.id, name: p.name });
                    }}
                    disabled={busy}
                    title="Mudar o nome"
                  >
                    <Icon name="pencil" />
                    <span className="sr-only">Mudar o nome de {p.name}</span>
                  </button>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm icon-btn--danger"
                    onClick={() => {
                      setEditing(undefined);
                      setDeleting(p.id);
                    }}
                    disabled={busy}
                    title="Apagar"
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Apagar {p.name}</span>
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {canAdd && (
        <form className="prize-manager__row prize-manager__add" onSubmit={add}>
          <label className="sr-only" htmlFor={ids.add}>
            Nome do novo prémio
          </label>
          <input
            id={ids.add}
            type="text"
            maxLength={200}
            placeholder="Ex.: Melhor Pandeireta"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={busy}
          />
          <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
            <Icon name="plus" />
            Adicionar
          </button>
        </form>
      )}
      <FieldError text={error} />
    </section>
  );
}
