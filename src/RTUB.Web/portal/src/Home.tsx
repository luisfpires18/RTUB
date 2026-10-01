import { useEffect, useState, type ReactNode } from 'react';
import { getGalleryPreview, getUpcomingEvents, type GalleryItem, type UpcomingEvent } from './api';
import { AccountLink, ExternalLink, Loading } from './App';
import { albums, contactEmail, governingBodies, playStoreUrl, portal, social } from './content';
import { Icon } from './icons';

export function Home() {
  return (
    <>
      <Hero />
      <About />
      <Events />
      <Music />
      <Gallery />
      <JoinUs />
      <Governance />
      <Doors />
      <InstallApp />
      <NewsTeaser />
    </>
  );
}

function SectionHead({
  id,
  eyebrow,
  title,
  note,
  children,
}: {
  id: string;
  eyebrow: string;
  title: string;
  note?: string;
  children?: ReactNode;
}) {
  return (
    <header className="section__head">
      <p className="eyebrow">{eyebrow}</p>
      <h2 id={id} className="section__title">
        {title}
      </h2>
      {children && <p className="section__lead">{children}</p>}
      {note && <p className="note">{note}</p>}
    </header>
  );
}

function MoreLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a className="more" href={href}>
      {children}
      <Icon name="arrow" />
    </a>
  );
}

// ---------- hero ----------

function Hero() {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="wrap hero__grid">
        <div className="hero__copy">
          <div className="hero__eyebrow">
            {/* Phones get a small badge here instead of the large emblem: the splash already shows it. */}
            <img className="hero__badge" src="/icons/rtub-logo-192.png" alt="" width="52" height="52" />
            <p className="eyebrow">Bragança · desde 1991</p>
          </div>
          <h1 id="hero-title" className="hero__title">
            <span className="hero__kicker">Real Tuna Universitária de Bragança</span>
            Boémios <em>e</em> Trovadores
          </h1>
          <p className="hero__lead">
            Serenatas, repertório próprio e muita estrada. Há mais de trinta anos que a RTUB leva a tradição académica
            de Bragança a quem a quiser ouvir.
          </p>
          <div className="hero__cta">
            <a className="btn btn--primary btn--lg" href={portal.request}>
              <Icon name="send" />
              Pedir uma atuação
            </a>
            <a className="btn btn--ghost btn--lg" href="#music">
              <Icon name="play" />
              Ouvir a RTUB
            </a>
          </div>
        </div>
        <div className="hero__art">
          <img
            src="/icons/rtub-logo-512.png"
            width="512"
            height="512"
            alt="Emblema da RTUB: o Castelo de Bragança sob a lua, com guitarra e bandolim"
          />
        </div>
      </div>
      <dl className="wrap facts">
        <div>
          <dt>Ano de fundação</dt>
          <dd>1991</dd>
        </div>
        <div>
          <dt>Primeiro álbum</dt>
          <dd>1995</dd>
        </div>
        <div>
          <dt>O festival da casa</dt>
          <dd>FITAB</dd>
        </div>
      </dl>
      <Skyline patternId="merlons-hero" />
    </section>
  );
}

/** Crenellated walls and keep - a nod to the castle in the emblem. Decorative. */
function Skyline({ patternId }: { patternId: string }) {
  const merlons = `url(#${patternId})`;
  return (
    <svg className="skyline" viewBox="0 0 1440 160" preserveAspectRatio="xMidYMax slice" aria-hidden="true" focusable="false">
      <defs>
        <pattern id={patternId} width="28" height="14" patternUnits="userSpaceOnUse">
          <rect width="16" height="14" fill="currentColor" />
        </pattern>
      </defs>
      <circle className="skyline__moon" cx="1236" cy="34" r="18" />
      <rect y="120" width="1440" height="40" fill="currentColor" />
      <rect y="106" width="1440" height="14" fill={merlons} />
      <rect x="170" y="72" width="72" height="48" fill="currentColor" />
      <rect x="170" y="58" width="72" height="14" fill={merlons} />
      <rect x="812" y="22" width="156" height="98" fill="currentColor" />
      <rect x="812" y="8" width="156" height="14" fill={merlons} />
      <rect x="800" y="0" width="22" height="46" fill="currentColor" />
      <rect x="958" y="0" width="22" height="46" fill="currentColor" />
      <rect x="1060" y="64" width="64" height="56" fill="currentColor" />
      <rect x="1060" y="50" width="64" height="14" fill={merlons} />
    </svg>
  );
}

// ---------- about ----------

function About() {
  return (
    <section id="about" className="section section--raise about" aria-labelledby="about-title">
      <div className="wrap about__grid">
        <SectionHead id="about-title" eyebrow="Quem somos" title="A tuna masculina de Bragança">
          Fundada em 1991 no então IPB, hoje UPB, a RTUB junta estudantes à volta da guitarra, do bandolim e da capa
          negra, e leva essa música para a rua.
        </SectionHead>
        <ul className="about__points">
          <li>
            <h3 className="about__point">Serenatas</h3>
            <p>À janela, na praça ou no palco, como a tradição académica pede.</p>
          </li>
          <li>
            <h3 className="about__point">Vida académica</h3>
            <p>Receções, festas e noites que marcam o percurso de quem estuda em Bragança.</p>
          </li>
          <li>
            <h3 className="about__point">Bragança</h3>
            <p>O castelo no emblema diz de onde vimos e para onde voltamos sempre.</p>
          </li>
        </ul>
      </div>
    </section>
  );
}

// ---------- agenda ----------

/** The next events exactly as the agenda has them (GET /api/public/events/upcoming); none invented. */
function Events() {
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
          <div className="upcoming-empty">
            <Icon name="calendar" />
            <p>
              {events ? 'Ainda não há atuações marcadas.' : 'Não foi possível carregar as próximas datas.'} A agenda
              completa tem as datas confirmadas e o histórico.
            </p>
          </div>
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

// ---------- music ----------

function Music() {
  const listen = social.filter((s) => s.name === 'Spotify' || s.name === 'YouTube');
  const latest = albums[albums.length - 1];

  return (
    <section id="music" className="section" aria-labelledby="music-title">
      <div className="wrap split">
        <div>
          <SectionHead id="music-title" eyebrow="Discografia" title={`${albums.length} álbuns para ouvir e recordar`}>
            Temas tradicionais e originais, gravados entre 1995 e 2013 por sucessivas gerações de tunos.
          </SectionHead>
          <ol className="discs">
            {albums.map((a) => (
              <li key={a.title} className="disc">
                <span className="disc__year">{a.year}</span>
                <span className="disc__title">{a.title}</span>
                <span className="disc__tracks">{a.tracks} temas</span>
              </li>
            ))}
          </ol>
          <div className="stream">
            {listen.map((s, i) => (
              <ExternalLink key={s.name} href={s.href} className={`btn ${i === 0 ? 'btn--primary' : 'btn--ghost'}`}>
                <Icon name={s.icon} />
                Ouvir no {s.name}
              </ExternalLink>
            ))}
          </div>
          <MoreLink href={portal.music}>Ouvir os álbuns e ler as letras</MoreLink>
        </div>
        <div className="record-wrap" aria-hidden="true">
          <div className="sleeve">
            <span className="sleeve__title">{latest.title}</span>
            <span className="sleeve__sub">
              RTUB · {latest.year}
            </span>
          </div>
          <div className="record">
            <img src="/icons/rtub-logo-192.png" alt="" width="192" height="192" />
          </div>
        </div>
      </div>
    </section>
  );
}

// ---------- gallery ----------

const PREVIEW_PHOTOS = 5;

/** The latest public photos (GET /api/gallery?public=true): members-only ones never show here. */
function Gallery() {
  const [photos, setPhotos] = useState<GalleryItem[] | null>();

  useEffect(() => {
    getGalleryPreview(PREVIEW_PHOTOS).then(
      (items) => setPhotos(items.filter((i) => i.type === 'image' && !i.membersOnly)),
      () => setPhotos(null),
    );
  }, []);

  return (
    <section id="gallery" className="section section--raise" aria-labelledby="gallery-title">
      <div className="wrap">
        <SectionHead id="gallery-title" eyebrow="Galeria" title="Em palco e fora dele" />
        {photos === undefined ? (
          <ul className="photos" aria-hidden="true">
            {Array.from({ length: PREVIEW_PHOTOS }, (_, i) => (
              <li key={i}>
                <span className="photo photo--skeleton" />
              </li>
            ))}
          </ul>
        ) : photos?.length ? (
          <ul className="photos">
            {photos.map((p) => (
              <li key={p.id}>
                <PreviewPhoto photo={p} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="note">As fotografias estão na Galeria.</p>
        )}
        <MoreLink href={portal.gallery}>Abrir a galeria</MoreLink>
      </div>
    </section>
  );
}

/** Opens the photo in the Galeria; a missing or broken file becomes a quiet placeholder. */
function PreviewPhoto({ photo }: { photo: GalleryItem }) {
  const [broken, setBroken] = useState(!photo.url);
  return (
    <a className="photo" href={`${portal.gallery}?item=${photo.id}`}>
      {broken ? (
        <span className="photo__missing">
          <Icon name="images" />
        </span>
      ) : (
        <img src={photo.url!} alt="" loading="lazy" decoding="async" onError={() => setBroken(true)} />
      )}
      <span className="photo__caption">{photo.title}</span>
    </a>
  );
}

// ---------- join ----------

function JoinUs() {
  return (
    <section id="join" className="section join" aria-labelledby="join-title">
      <div className="wrap split">
        <div>
          <SectionHead id="join-title" eyebrow="Novos elementos" title="Há sempre lugar para mais uma voz">
            Estudas na UPB e gostas de música, de noites longas e de boa companhia? Vem a um ensaio e conhece a
            tuna por dentro.
          </SectionHead>
          <p className="join__quote">Ninguém nasce a tocar instrumentos. Aprende-se aqui.</p>
        </div>
        <dl className="join__facts">
          <div>
            <dt>
              <Icon name="clock" />
              Ensaios
            </dt>
            <dd>Às terças e quintas, das 21h00 à meia-noite.</dd>
          </div>
          <div>
            <dt>
              <Icon name="geo" />
              Local
            </dt>
            <dd>Quinta de Santa Apolónia, no campus da Universidade Politécnica de Bragança.</dd>
          </div>
          <div>
            <dt>
              <Icon name="arrow" />
              Primeiro passo
            </dt>
            <dd>Basta aparecer. Se preferires, fala connosco antes pelo Instagram ou pelo Facebook.</dd>
          </div>
          <div>
            <dt>
              <Icon name="person" />
              Percurso
            </dt>
            <dd>Entra-se como Leitão, passa-se a Caloiro e depois a Tuno; os cordões do traje mostram cada etapa.</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}

// ---------- órgãos sociais ----------

function Governance() {
  return (
    <section id="governance" className="section section--raise" aria-labelledby="governance-title">
      <div className="wrap">
        {/* The cards say what each body is; the heading stays for screen readers and the menu anchor. */}
        <h2 id="governance-title" className="sr-only">
          Órgãos Sociais
        </h2>
        <p className="section__lead governance__lead">
          Quatro órgãos, renovados a cada ano letivo, e um Ensaiador que dá o tom aos ensaios.
        </p>
        <ul className="governance">
          {governingBodies.map((o) => (
            <li key={o.name} className="governance__card">
              <Icon name="bank" className="governance__icon" />
              <h3 className="governance__name">{o.name}</h3>
              <ul className="governance__roles">
                {o.roles.map((r) => (
                  <li key={r}>{r}</li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
        <MoreLink href={portal.roles}>Ver quem ocupa os cargos</MoreLink>
      </div>
    </section>
  );
}

// ---------- request + members ----------

function Doors() {
  return (
    <section id="request" className="section" aria-labelledby="request-title">
      <div className="wrap doors">
        <div className="door door--request">
          <p className="eyebrow">Pedidos</p>
          <h2 id="request-title" className="section__title">
            Leve a RTUB ao seu evento
          </h2>
          <p className="door__lead">
            Serenatas, casamentos, batizados, aniversários, arraiais ou festivais. Conte-nos o que tem em mente.
          </p>
          <ol className="steps">
            <li>
              <strong>Preencha o pedido</strong> com a data, o local e o tipo de evento.
            </li>
            <li>
              <strong>Vemos a disponibilidade</strong> e damos-lhe uma resposta.
            </li>
            <li>
              <strong>No dia combinado,</strong> a RTUB leva a música até si.
            </li>
          </ol>
          <div className="door__actions">
            <a className="btn btn--light btn--lg" href={portal.request}>
              <Icon name="send" />
              Fazer um pedido
            </a>
            <a className="door__mail" href={`mailto:${contactEmail}`}>
              <Icon name="envelope" />
              {contactEmail}
            </a>
          </div>
        </div>
        <div className="door door--members">
          <p className="eyebrow">Área de membros</p>
          <h2 className="door__title">Só para a tuna</h2>
          <p className="door__lead">
            Ensaios, atuações e mensagens dos membros da RTUB. O acesso é criado pela própria tuna; não há registo
            público.
          </p>
          <AccountLink className="member-link" signedOutLabel="Entrar como membro" />
          <Skyline patternId="merlons-members" />
        </div>
      </div>
    </section>
  );
}

// ---------- install ----------

function InstallApp() {
  return (
    <section id="app" className="section section--raise" aria-labelledby="app-title">
      <div className="wrap">
        <SectionHead id="app-title" eyebrow="No telemóvel" title="Instalar a app">
          A RTUB também vive no ecrã principal do telemóvel. Escolhe o caminho do teu aparelho.
        </SectionHead>
        <ol className="install">
          <li className="install__step">
            <Icon name="googlePlay" className="install__icon" />
            <h3 className="install__title">Android</h3>
            <p>Instala a app RTUB a partir da Google Play.</p>
            <ExternalLink href={playStoreUrl} className="more">
              Abrir na Google Play
            </ExternalLink>
          </li>
          <li className="install__step">
            <Icon name="apple" className="install__icon" />
            <h3 className="install__title">iPhone e iPad</h3>
            <p>
              Abre este site no Safari, toca em <strong>Partilhar</strong> e escolhe{' '}
              <strong>Adicionar ao ecrã principal</strong>.
            </p>
          </li>
          <li className="install__step">
            <Icon name="phone" className="install__icon" />
            <h3 className="install__title">Android, pelo navegador</h3>
            <p>
              No Chrome, abre o menu <strong>⋮</strong> e escolhe <strong>Instalar app</strong> ou{' '}
              <strong>Adicionar ao ecrã principal</strong>, se a opção aparecer.
            </p>
          </li>
          <li className="install__step">
            <Icon name="laptop" className="install__icon" />
            <h3 className="install__title">Computador</h3>
            <p>No Chrome ou no Edge, o ícone de instalar surge na barra de endereço quando o navegador o permite.</p>
          </li>
        </ol>
      </div>
    </section>
  );
}

// ---------- news (placeholder only) ----------

/**
 * "Novidades" is not built yet: no route, API or storage. This strip only says it is coming and
 * stays deliberately small; the plan (future /news) is in docs/react-portal-pilot.md.
 */
function NewsTeaser() {
  return (
    <section className="news-teaser" aria-labelledby="news-title">
      <div className="wrap news-teaser__inner">
        <h2 id="news-title" className="news-teaser__title">
          Novidades <span className="tag">Em breve</span>
        </h2>
        <p>Anúncios, crónicas de atuações e fotografias, publicados pela própria RTUB.</p>
      </div>
    </section>
  );
}
