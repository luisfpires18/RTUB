// The RTUB's prize history dialog.

import { useEffect, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { PrizeManager } from './EventManageLazy';
import { portal } from '../data/content';
import { eventsApi, yearOf, type EventSummary } from '../data/eventsApi';
import { Icon } from './icons';

// ---------- prizes ----------

/**
 * The RTUB's prize history, newest festival first (festivals without prizes left out). With
 * `current`, that event's prizes lead. The history comes from the agenda when not given.
 */
export function PrizesDialog({
  history,
  current,
  manage,
  onClose,
}: {
  history?: EventSummary[];
  current?: EventSummary;
  /** Admin/Owner on the event page (012B): the current event's prizes become editable. */
  manage?: { canAdd: boolean; onChanged: () => void };
  onClose: () => void;
}) {
  const [past, setPast] = useState<EventSummary[] | null | undefined>(history);

  useEffect(() => {
    if (history) return;
    eventsApi.agenda().then((o) => setPast(o.kind === 'ok' ? o.data.past : null));
  }, [history]);

  const won = past?.filter((e) => e.trophies.length > 0 && e.id !== current?.id) ?? [];
  const total = (past ?? []).reduce((n, e) => n + e.trophies.length, 0);

  return (
    <Dialog title="Prémios" onClose={onClose} size="lg">
      {current && manage && <PrizeManager eventId={current.id} canAdd={manage.canAdd} onChanged={manage.onChanged} />}
      {current && !manage && current.trophies.length > 0 && (
        <section className="prizes-current" aria-label="Prémios desta atuação">
          <p className="eyebrow">Nesta atuação</p>
          <ul className="trophies">
            {current.trophies.map((t, i) => (
              <li key={i}>
                <Icon name="trophy" />
                {t}
              </li>
            ))}
          </ul>
        </section>
      )}
      {past === undefined ? (
        <Loading label="A carregar o palmarés…" />
      ) : past === null ? (
        <p>Não foi possível carregar o palmarés agora.</p>
      ) : won.length === 0 ? (
        !current && <p>Ainda não há prémios registados.</p>
      ) : (
        <section aria-label="Palmarés">
          <p className="eyebrow prizes__summary">
            {current ? 'Palmarés' : `${total === 1 ? '1 prémio' : `${total} prémios`} em ${won.length === 1 ? '1 festival' : `${won.length} festivais`}`}
          </p>
          <ul className="prizes__list">
            {won.map((e) => (
              <li key={e.id} className="prizes__festival">
                <a href={portal.event(e.id)} className="prizes__event">
                  {e.name}
                  <span>{yearOf(e.date)}</span>
                </a>
                <ul className="prizes__items">
                  {e.trophies.map((t, i) => (
                    <li key={i}>{t}</li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </section>
      )}
    </Dialog>
  );
}
