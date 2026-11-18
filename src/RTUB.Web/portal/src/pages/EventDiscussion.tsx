import { useEffect, useState } from 'react';
import { portal } from '../data/content';
import { eventsApi, dateLabel, type EventSummary, type EventDiscussion as Discussion } from '../data/eventsApi';
import { Notice, SignInNotice } from '../components/Notice';
import { Composer } from '../components/EventDiscussionComposer';
import { Post } from '../components/EventDiscussionPost';
import { Loading } from '../components/ui/Loading';
import { BackLink } from '../components/ui/BackLink';
import { PageHead } from '../components/ui/PageHead';
import { EmptyBlock } from '../components/ui/EmptyNote';

// EventContacts.tsx imports `when` from here.

/**
 * /events/{id}/discussion - the event's conversation. Signed-in members
 * post notes, comment, and offer or organise lifts ("boleias"); everything is one feed, pinned posts first
 * and then whatever moved last. Every rule is the server's (GET /api/events/{id}/discussion says what the
 * caller may do); each write answers the whole conversation, so the page never drifts.
 */
export default function EventDiscussion({ eventId }: { eventId: number }) {
  const [event, setEvent] = useState<EventSummary | 'missing' | null>();
  const [talk, setTalk] = useState<Discussion | 'signin' | null>();

  useEffect(() => {
    eventsApi.event(eventId).then((o) => setEvent(o.kind === 'ok' ? o.data.event : o.kind === 'notfound' ? 'missing' : null));
    eventsApi.discussion(eventId).then((o) => setTalk(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  }, [eventId]);

  useEffect(() => {
    document.title = event && event !== 'missing' ? `Discussão · ${event.name} · RTUB` : 'Discussão · RTUB';
  }, [event]);

  const back = (
    <BackLink href={portal.event(eventId)}>{event && event !== 'missing' ? event.name : 'Atuação'}</BackLink>
  );

  if (event === 'missing') {
    return (
      <section className="page wrap talk-page">
        <BackLink href={portal.events}>Agenda</BackLink>
        <Notice>Esta atuação não existe ou já foi apagada.</Notice>
      </section>
    );
  }

  return (
    <section className="page wrap talk-page" aria-labelledby="talk-title">
      {back}
      <PageHead
        plain
        className="talk-page__head"
        eyebrow="Conversa da atuação"
        title="Discussão"
        titleId="talk-title"
        lead={event && `${event.name} · ${dateLabel(event)} · ${event.location}`}
      />

      {talk === undefined ? (
        <Loading label="A carregar a conversa…" />
      ) : talk === 'signin' ? (
        <SignInNotice path={`/events/${eventId}/discussion`}>A discussão é da área de membros.</SignInNotice>
      ) : talk === null ? (
        <Notice>Não conseguimos abrir a conversa agora. Tenta outra vez daqui a pouco.</Notice>
      ) : (
        <Feed eventId={eventId} talk={talk} onChange={setTalk} />
      )}
    </section>
  );
}

function Feed({ eventId, talk, onChange }: { eventId: number; talk: Discussion; onChange: (d: Discussion) => void }) {
  return (
    <div className="talk">
      <Composer eventId={eventId} onPosted={onChange} />
      {talk.posts.length === 0 ? (
        <EmptyBlock className="talk__empty" icon="chat">
          <p className="talk__empty-title">Ainda não há mensagens nesta atuação.</p>
          <p>Deixa uma nota, combina detalhes ou partilha uma dúvida.</p>
        </EmptyBlock>
      ) : (
        <ol className="talk__feed" aria-label="Mensagens">
          {talk.posts.map((p) => (
            <li key={p.id}>
              <Post eventId={eventId} post={p} canModerate={talk.canModerate} onChange={onChange} />
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
