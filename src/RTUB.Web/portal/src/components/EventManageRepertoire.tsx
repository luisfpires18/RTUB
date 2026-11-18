// Dialog to manage an event's repertoire by day.

import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { dayOf, monthShort, eventsApi, type RepertoireManage, type RepertoireSong, type EventSummary } from '../data/eventsApi';
import type { Outcome } from '../data/api';
import { Icon } from './icons';
import { failure, EventLine } from './EventManageShared';
import { Loading } from './ui/Loading';
import { Segmented } from './ui/Segmented';
import { FieldError } from './ui/FieldError';
import { AnswerActions } from './ui/AnswerActions';
import { EmptyNote } from './ui/EmptyNote';
import { SearchField } from './ui/SearchTools';

/**
 * The event's repertoire for Admin/Owner: one tab per day (the event's days
 * and any other day that already has songs), songs appended after the day's last, Subir / Descer,
 * remove one song or clear the day. A song goes once per event; the picker only offers songs Music
 * shows the caller that are not in the event yet. `onChanged` refreshes the page behind.
 */
export function RepertoireManagerDialog({ event, onClose, onChanged }: { event: EventSummary; onClose: () => void; onChanged: () => void }) {
  const [data, setData] = useState<RepertoireManage | null>();
  const [day, setDay] = useState<string>();
  const [query, setQuery] = useState('');
  const [songs, setSongs] = useState<RepertoireSong[] | null>();
  const [clearing, setClearing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [version, setVersion] = useState(0); // a new search after each write: the added song leaves the list
  const searchId = useId();

  useEffect(() => {
    eventsApi.repertoire(event.id).then((o) => {
      setData(o.kind === 'ok' ? o.data : null);
      if (o.kind === 'ok') setDay((d) => d ?? (o.data.days.find((x) => x.items.length > 0) ?? o.data.days[0])?.date);
    });
  }, [event.id]);

  useEffect(() => {
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      eventsApi.repertoireSongs(event.id, query.trim()).then((o) => live && setSongs(o.kind === 'ok' ? o.data : null));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [event.id, query, version]);

  const run = async (call: Promise<Outcome<RepertoireManage>>) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setClearing(false);
    if (o.kind === 'ok') {
      setData(o.data);
      setVersion((v) => v + 1);
      onChanged();
    } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
    else setError(failure(o, 'guardar o repertório'));
  };

  const current = data?.days.find((d) => d.date === day);

  const move = (index: number, by: -1 | 1) => {
    if (!current || !day) return;
    const order = current.items.map((i) => i.id);
    [order[index], order[index + by]] = [order[index + by], order[index]];
    run(eventsApi.reorderRepertoire(event.id, day, order));
  };

  return (
    <Dialog title="Gerir repertório" onClose={onClose} size="lg">
      <div className="answer">
        <EventLine event={event} />
        {data === undefined ? (
          <Loading label="A carregar o repertório…" />
        ) : data === null || !current ? (
          <FieldError text="Não foi possível carregar o repertório desta atuação." alert={false} />
        ) : (
          <>
            {data.days.length > 1 && (
              <Segmented label="Dia" role="tablist" wrap>
                {data.days.map((d) => (
                  <button
                    key={d.date}
                    type="button"
                    role="tab"
                    aria-selected={d.date === day}
                    className={d.date === day ? 'segmented__option is-on' : 'segmented__option'}
                    onClick={() => {
                      setDay(d.date);
                      setClearing(false);
                      setError(undefined);
                    }}
                  >
                    {dayOf(d.date)} {monthShort(d.date)}
                    <span className="segmented__count">{d.items.length}</span>
                  </button>
                ))}
              </Segmented>
            )}

            {current.items.length === 0 ? (
              <EmptyNote>Ainda sem músicas {data.days.length > 1 ? 'neste dia' : 'nesta atuação'}.</EmptyNote>
            ) : (
              <ol className="prize-manager__list repertoire-manager__list">
                {current.items.map((item, i) => (
                  <li key={item.id} className="prize-manager__row">
                    <span className="repertoire-manager__pos">{i + 1}.</span>
                    <span className="prize-manager__name">{item.title}</span>
                    <button type="button" className="icon-btn icon-btn--sm" onClick={() => move(i, -1)} disabled={busy || i === 0} title="Subir">
                      <Icon name="up" />
                      <span className="sr-only">Subir {item.title}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-btn icon-btn--sm"
                      onClick={() => move(i, 1)}
                      disabled={busy || i === current.items.length - 1}
                      title="Descer"
                    >
                      <Icon name="down" />
                      <span className="sr-only">Descer {item.title}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-btn icon-btn--sm icon-btn--danger"
                      onClick={() => run(eventsApi.removeFromRepertoire(event.id, item.id))}
                      disabled={busy}
                      title="Tirar do repertório"
                    >
                      <Icon name="close" />
                      <span className="sr-only">Tirar {item.title} do repertório</span>
                    </button>
                  </li>
                ))}
              </ol>
            )}

            {current.items.length > 0 &&
              (clearing ? (
                <div className="notice-confirm" role="alert">
                  <p>
                    <strong>{current.items.length === 1 ? 'Tirar a música deste dia?' : `Tirar as ${current.items.length} músicas deste dia?`}</strong> As músicas continuam na Música.
                  </p>
                  <AnswerActions cancelLabel="Voltar" onCancel={() => setClearing(false)} label="Limpar o dia" onConfirm={() => run(eventsApi.clearRepertoireDay(event.id, current.date))} busy={busy} danger small spinner={false} />
                </div>
              ) : (
                <button type="button" className="btn btn--ghost btn--sm repertoire-manager__clear" onClick={() => setClearing(true)} disabled={busy}>
                  <Icon name="trash" />
                  Limpar {data.days.length > 1 ? 'este dia' : 'o repertório'}
                </button>
              ))}

            <div className="repertoire-manager__add">
              <p className="eyebrow">Juntar música</p>
              <SearchField id={searchId} label="Procurar música" placeholder="Procurar pelo título" value={query} onChange={setQuery} />
              {songs === undefined ? (
                <Loading label="A procurar…" />
              ) : songs === null ? (
                <FieldError text="Não foi possível procurar músicas agora." alert={false} />
              ) : songs.length === 0 ? (
                <EmptyNote>{query.trim() ? 'Nenhuma música com esse título (ou já está no repertório).' : 'Todas as músicas já estão no repertório.'}</EmptyNote>
              ) : (
                <ul className="prize-manager__list">
                  {songs.map((s) => (
                    <li key={s.id} className="prize-manager__row">
                      <span className="prize-manager__name">
                        {s.title}
                        {s.album && <span className="repertoire-manager__album"> · {s.album}</span>}
                      </span>
                      <button
                        type="button"
                        className="btn btn--primary btn--sm"
                        onClick={() => run(eventsApi.addToRepertoire(event.id, s.id, current.date))}
                        disabled={busy}
                      >
                        <Icon name="plus" />
                        Juntar
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}
        <FieldError text={error} />
      </div>
    </Dialog>
  );
}
