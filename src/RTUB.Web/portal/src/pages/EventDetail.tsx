import { useEffect, useRef, useState } from 'react';
import { EnrollmentDialog } from '../components/EventAnswer';
import { PrizesDialog } from '../components/EventPrizesDialog';
import { CancelEventDialog, ReactivateEventDialog, NoticeDialog, ParticipantsManagerDialog, RepertoireManagerDialog, VideoManagerDialog } from '../components/EventManageLazy';
import { RetryNotice } from '../components/Notice';
import { portal } from '../data/content';
import { eventsApi, type EventDetail as Detail } from '../data/eventsApi';
import { Icon } from '../components/icons';
import { Body } from '../components/EventDetailBody';
import { Loading } from '../components/ui/Loading';
import { BackLink } from '../components/ui/BackLink';

/**
 * ?respond=1 (from an old link) opens the answer modal once. Read
 * without side effects: React may render and discard a first attempt (lazy pages), so the URL is
 * only cleaned once the modal actually opens.
 */
const wantsToRespond = () => new URLSearchParams(location.search).get('respond') === '1';

/** ?repertoire=1 (from "As minhas ações pendentes") opens the repertoire editor once, if the member may use it. */
const wantsRepertoire = () => new URLSearchParams(location.search).get('repertoire') === '1';

function dropRespondFlag() {
  const url = new URL(location.href);
  url.searchParams.delete('respond');
  history.replaceState(null, '', url.pathname + url.search);
}

/**
 * /events/{id} - one event. Visitors get the public facts, prizes and videos; signed-in members
 * also get the description, their own answer (a modal, never another page), the counts, the
 * repertoire and who is going, all on this page.
 */
export default function EventDetail({ eventId }: { eventId: number }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  const [answering, setAnswering] = useState(false);
  const [prizes, setPrizes] = useState(false);
  const [managing, setManaging] = useState<'notice' | 'cancel' | 'reactivate'>();
  const [managingVideos, setManagingVideos] = useState(false);
  const [managingRepertoire, setManagingRepertoire] = useState(false);
  const [managingParticipants, setManagingParticipants] = useState(false);
  const respond = useRef(wantsToRespond());
  const repertoire = useRef(wantsRepertoire());

  const load = (quiet = false) => {
    if (!quiet) setDetail(undefined);
    eventsApi.event(eventId).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  };
  useEffect(() => load(), [eventId]);

  useEffect(() => {
    document.title = detail && detail !== 'missing' ? `${detail.event.name} · Atuações · RTUB` : 'Atuações · RTUB';
    if (detail && detail !== 'missing' && respond.current) {
      respond.current = false;
      dropRespondFlag();
      setAnswering(true);
    }
    if (detail && detail !== 'missing' && repertoire.current) {
      repertoire.current = false;
      const url = new URL(location.href);
      url.searchParams.delete('repertoire');
      history.replaceState(null, '', url.pathname + url.search);
      if (detail.canManageRepertoire) setManagingRepertoire(true);
    }
  }, [detail]);

  const loaded = detail && detail !== 'missing' ? detail : null;
  // Admin/Owner, upcoming only; the server refuses everyone else.
  const manage = loaded?.canManage && !loaded.event.past ? loaded.event : null;

  return (
    <section className="page wrap event-page" aria-labelledby="event-title">
      <div className="event-topbar">
        <BackLink href={portal.events}>Agenda</BackLink>
        <span className="event-topbar__actions">
          {manage && !manage.cancelled && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setManaging('notice')}>
              <Icon name="bell" />
              Enviar aviso
            </button>
          )}
          {manage && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => setManaging(manage.cancelled ? 'reactivate' : 'cancel')}
            >
              <Icon name={manage.cancelled ? 'restore' : 'ban'} />
              {manage.cancelled ? 'Reativar' : 'Cancelar atuação'}
            </button>
          )}
          {loaded && (loaded.event.trophies.length > 0 || loaded.canManagePrizes) && (
            <button type="button" className="btn btn--prizes btn--sm" onClick={() => setPrizes(true)}>
              <Icon name="trophy" />
              Prémios
            </button>
          )}
        </span>
      </div>
      {detail === undefined ? (
        <Loading label="A carregar a atuação…" />
      ) : detail === 'missing' ? (
        <div className="notice" role="status">
          <p id="event-title">Esta atuação não existe ou foi removida.</p>
          <a className="btn btn--ghost btn--sm" href={portal.events}>
            Ver a agenda
          </a>
        </div>
      ) : detail === null ? (
        <RetryNotice id="event-title" onRetry={() => load()}>
          Não conseguimos abrir esta atuação agora.
        </RetryNotice>
      ) : (
        <Body
          detail={detail}
          onAnswer={() => setAnswering(true)}
          // Admin/Owner: uploads go to past events; existing videos stay manageable anywhere.
          onManageVideos={detail.canManage && (detail.event.past || detail.videos.length > 0) ? () => setManagingVideos(true) : undefined}
          // Admin/Owner: the repertoire of any event.
          onManageRepertoire={detail.canManageRepertoire ? () => setManagingRepertoire(true) : undefined}
          // Admin/Owner: add a member who has not answered, remove any answer.
          onManageParticipants={detail.canManage ? () => setManagingParticipants(true) : undefined}
        />
      )}
      {answering && <EnrollmentDialog eventId={eventId} onClose={() => setAnswering(false)} onSaved={() => load(true)} />}
      {prizes && loaded && (
        <PrizesDialog
          current={loaded.event}
          // Admin/Owner manage the prizes here: a past festival takes new ones; existing ones stay editable anywhere.
          manage={loaded.canManage ? { canAdd: loaded.canManagePrizes, onChanged: () => load(true) } : undefined}
          onClose={() => setPrizes(false)}
        />
      )}
      {manage && managing === 'notice' && <NoticeDialog event={manage} onClose={() => setManaging(undefined)} />}
      {manage && managing === 'cancel' && (
        <CancelEventDialog event={manage} onClose={() => setManaging(undefined)} onDone={() => load(true)} />
      )}
      {manage && managing === 'reactivate' && (
        <ReactivateEventDialog event={manage} onClose={() => setManaging(undefined)} onDone={() => load(true)} />
      )}
      {managingParticipants && loaded && (
        <ParticipantsManagerDialog event={loaded.event} onClose={() => setManagingParticipants(false)} onChanged={() => load(true)} />
      )}
      {managingRepertoire && loaded && (
        <RepertoireManagerDialog event={loaded.event} onClose={() => setManagingRepertoire(false)} onChanged={() => load(true)} />
      )}
      {managingVideos && loaded && (
        <VideoManagerDialog event={loaded.event} onClose={() => setManagingVideos(false)} onChanged={() => load(true)} />
      )}
    </section>
  );
}
