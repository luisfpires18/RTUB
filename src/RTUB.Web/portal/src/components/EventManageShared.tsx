// Pieces shared by the event management dialogs: the failure banner, the event line, member counts, the audience hook.

import { useEffect, useState } from 'react';
import { dateLabel, eventsApi, timeLabel, type EventSummary, type NoticeAudience, type Outcome } from '../data/eventsApi';

/** The banner for a refused or failed call; `what` names the action ("guardar", "cancelar"...). */
export const failure = (o: Outcome<unknown>, what: string) =>
  o.kind === 'forbidden' || o.kind === 'signin'
    ? 'Não tem permissão para gerir atuações.'
    : o.kind === 'notfound'
      ? 'Esta atuação já não existe.'
      : o.kind === 'closed'
        ? 'Esta atuação já passou ou mudou entretanto. Recarregue a página.'
        : `Não foi possível ${what}. Tente outra vez daqui a pouco.`;
/** Name, date and place, at the top of every confirmation. */
export function EventLine({ event }: { event: EventSummary }) {
  const time = timeLabel(event.time);
  return (
    <p className="answer-event">
      <strong>{event.name}</strong>
      <span>
        {dateLabel(event)}
        {time && ` · ${time}`} · {event.location}
      </span>
    </p>
  );
}

export const members = (n: number) => (n === 1 ? '1 membro' : `${n} membros`);

/** The counts of a notice's audience, once loaded. */
export function useAudience(eventId: number) {
  const [audience, setAudience] = useState<NoticeAudience | null>();
  useEffect(() => {
    eventsApi.noticeAudience(eventId).then((o) => setAudience(o.kind === 'ok' ? o.data : null));
  }, [eventId]);
  return audience;
}
