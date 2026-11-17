// The optional event (by name search) of a board.
import { useEffect, useId, useState } from 'react';
import { Icon } from './icons';
import { logisticsApi, type LogisticsEvent } from '../data/logisticsApi';
import { day } from '../data/format';

/** The optional event (by name search), shared with the template dialog. */
export function EventField({ event, setEvent, error, label = 'Atuação associada (opcional)' }: { event: LogisticsEvent | null; setEvent: (e: LogisticsEvent | null) => void; error?: string; label?: string }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<LogisticsEvent[]>();
  const fid = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => logisticsApi.events(query.trim()).then((o) => live && setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query]);

  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={`${fid}-event`}>{label}</label>
      {event ? (
        <span className="chip chip--removable">
          <Icon name="calendar" />
          {event.name} · {day(event.date)}
          <button type="button" className="chip__remove" onClick={() => setEvent(null)}>
            <Icon name="close" />
            <span className="sr-only">Remover atuação</span>
          </button>
        </span>
      ) : (
        <>
          <span className="control control--sm">
            <Icon name="search" />
            <input id={`${fid}-event`} type="search" placeholder="Procurar atuação pelo nome" value={query} onChange={(e) => setQuery(e.target.value)} />
          </span>
          {found && found.length === 0 && <p className="note">Nenhuma atuação encontrada.</p>}
          {found && found.length > 0 && (
            <ul className="talk__picks">
              {found.map((ev) => (
                <li key={ev.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setEvent(ev);
                      setQuery('');
                    }}
                  >
                    <Icon name="calendar" />
                    <span>
                      {ev.name}
                      <small>{day(ev.date)}</small>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
