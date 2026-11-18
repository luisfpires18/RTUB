// Home page agenda (upcoming events) and latest news sections.
import { useEffect, useState } from 'react';
import { EmptyBlock } from './ui/EmptyNote';
import { getLatestNews, getUpcomingEvents, newsDate, type NewsPost, type UpcomingEvent } from '../data/api';
import { portal } from '../data/content';
import { Icon } from './icons';
import { SectionHead, MoreLink } from './HomeParts';
import { Loading } from './ui/Loading';

// ---------- agenda ----------

/** The next events exactly as the agenda has them (GET /api/public/events/upcoming); none invented. */
export function Events() {
  const [events, setEvents] = useState<UpcomingEvent[] | null>();

  useEffect(() => {
    getUpcomingEvents().then(setEvents, () => setEvents(null));
  }, []);

  return (
    <section id="events" className="section section--raise" aria-labelledby="events-title">
      <div className="wrap">
        <SectionHead id="events-title" eyebrow="Agenda" title="Próximas atuações">
          Os próximos palcos, praças e salões onde a tuna vai tocar.
        </SectionHead>
        {events === undefined ? (
          <Loading label="A carregar a agenda…" />
        ) : !events?.length ? (
          <EmptyBlock className="upcoming-empty" icon="calendar">
            <p>
              {events ? 'Ainda não há atuações marcadas.' : 'Não foi possível carregar as próximas datas.'} A agenda
              completa tem as datas confirmadas e o histórico.
            </p>
          </EmptyBlock>
        ) : (
          <ul className="upcoming">
            {events.slice(0, 3).map((e, i) => (
              <li key={`${e.date}-${i}`}>
                <UpcomingCard event={e} />
              </li>
            ))}
          </ul>
        )}
        <aside id="fitab" className="fitab" aria-labelledby="fitab-title">
          <div>
            <p className="eyebrow">O festival da casa</p>
            <h3 id="fitab-title" className="fitab__title">
              FITAB
            </h3>
          </div>
          <div className="fitab__body">
            <p>
              O Festival Internacional de Tunas Académicas de Bragança é organizado pela RTUB todos os anos e traz à
              cidade tunas de várias academias, dentro e fora do país.
            </p>
            <p className="note">As datas de cada edição são anunciadas nas redes da RTUB.</p>
          </div>
        </aside>
        <MoreLink href={portal.events}>Ver a agenda completa</MoreLink>
      </div>
    </section>
  );
}

// ---------- latest news ----------

/**
 * The three newest published posts of /news, right after the agenda. Renders nothing while loading,
 * when there are no posts or when the call fails: the feed is one tap away in the top bar.
 */
export function LatestNews() {
  const [posts, setPosts] = useState<NewsPost[]>([]);

  useEffect(() => {
    getLatestNews(3).then(setPosts, () => setPosts([]));
  }, []);

  if (posts.length === 0) return null;
  return (
    <section id="news" className="section" aria-labelledby="news-title">
      <div className="wrap">
        <SectionHead id="news-title" eyebrow="Novidades" title="Últimas novidades">
          O que a tuna tem andado a fazer, contado pela própria RTUB.
        </SectionHead>
        <ul className="news-latest">
          {posts.map((p) => (
            <li key={p.id}>
              <a className="news-latest__card" href={`${portal.news}#post-${p.id}`}>
                <time className="news-latest__date" dateTime={p.publishedAt ?? undefined}>
                  {p.publishedAt && newsDate(p.publishedAt)}
                </time>
                {p.membersOnly && <span className="tag tag--private news-latest__tag">Só membros</span>}
                {p.title && <h3 className="news-latest__title">{p.title}</h3>}
                <p className="news-latest__text">{p.body}</p>
              </a>
            </li>
          ))}
        </ul>
        <MoreLink href={portal.news}>Ver todas</MoreLink>
      </div>
    </section>
  );
}

const shortMonth = (month: number) =>
  new Intl.DateTimeFormat('pt-PT', { month: 'short' }).format(new Date(2000, month - 1, 1)).replace('.', '');

function UpcomingCard({ event }: { event: UpcomingEvent }) {
  const [, month, day] = event.date.split('-').map(Number);
  const until = event.endDate?.split('-').map(Number);
  const when = [until && `até ${until[2]} ${shortMonth(until[1])}`, event.time && `às ${event.time.replace(':', 'h')}`]
    .filter(Boolean)
    .join(' · ');

  return (
    <article className={event.cancelled ? 'gig gig--cancelled' : 'gig'}>
      <time className="gig__date" dateTime={event.time ? `${event.date}T${event.time}` : event.date}>
        <span className="gig__day">{day}</span>
        <span className="gig__month">{shortMonth(month)}</span>
      </time>
      <div className="gig__body">
        <h3 className="gig__name">{event.name}</h3>
        {when && (
          <p className="gig__meta">
            <Icon name="clock" />
            {when}
          </p>
        )}
        <p className="gig__meta">
          <Icon name="geo" />
          {event.location}
        </p>
        <span className="gig__status">{event.cancelled ? 'Cancelada' : event.type}</span>
      </div>
    </article>
  );
}
