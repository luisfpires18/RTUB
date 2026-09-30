import type { ReactNode } from 'react';
import { AccountLink, EmptyState, ExternalLink } from './App';
import { albums, contactEmail, galleryTiles, legacy, naipes, orgaosSociais, portal, social, upcomingEvents } from './content';
import { Icon } from './icons';

export function Home() {
  return (
    <>
      <Hero />
      <Events />
      <Music />
      <Gallery />
      <JoinUs />
      <Orgaos />
      <Doors />
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
            <a className="btn btn--primary btn--lg" href="#pedidos">
              <Icon name="send" />
              Pedir uma atuação
            </a>
            <a className="btn btn--ghost btn--lg" href="#musica">
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

// ---------- agenda ----------

const MONTHS = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

function Events() {
  return (
    <section id="atuacoes" className="section section--raise" aria-labelledby="atuacoes-title">
      <div className="wrap">
        <SectionHead id="atuacoes-title" eyebrow="Agenda" title="Próximas atuações" note="Datas ilustrativas nesta pré-visualização.">
          Os próximos palcos, praças e salões onde a tuna vai tocar.
        </SectionHead>
        {upcomingEvents.length === 0 ? (
          <EmptyState icon="calendar" title="Nenhuma data marcada para já.">
            <p>A agenda completa tem o histórico e as novidades.</p>
          </EmptyState>
        ) : (
          <ol className="events">
            {upcomingEvents.map((e) => {
              const [year, month, day] = e.date.split('-');
              return (
                <li key={e.date} className="event">
                  <time className="event__date" dateTime={e.date}>
                    <span className="event__day">{day}</span>
                    <span className="event__month">
                      {MONTHS[Number(month) - 1]} {year}
                    </span>
                  </time>
                  <div className="event__body">
                    <h3 className="event__title">{e.title}</h3>
                    <p className="event__meta">
                      <span>
                        <Icon name="geo" />
                        {e.place}
                      </span>
                      <span className="tag">{e.kind}</span>
                    </p>
                  </div>
                </li>
              );
            })}
          </ol>
        )}
        <MoreLink href={legacy.events}>Agenda completa</MoreLink>
      </div>
    </section>
  );
}

// ---------- music ----------

function Music() {
  const listen = social.filter((s) => s.name === 'Spotify' || s.name === 'YouTube');
  const latest = albums[albums.length - 1];

  return (
    <section id="musica" className="section" aria-labelledby="musica-title">
      <div className="wrap split">
        <div>
          <SectionHead id="musica-title" eyebrow="Discografia" title="Quatro discos, uma só voz">
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
          <ul className="chips" aria-label="Instrumentos">
            {naipes.map((n) => (
              <li key={n} className="chip">
                {n}
              </li>
            ))}
          </ul>
          <div className="stream">
            {listen.map((s, i) => (
              <ExternalLink key={s.name} href={s.href} className={`btn ${i === 0 ? 'btn--primary' : 'btn--ghost'}`}>
                <Icon name={s.icon} />
                Ouvir no {s.name}
              </ExternalLink>
            ))}
          </div>
          <MoreLink href={legacy.music}>Álbuns e letras</MoreLink>
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

function Gallery() {
  return (
    <section id="galeria" className="section section--raise" aria-labelledby="galeria-title">
      <div className="wrap">
        <SectionHead
          id="galeria-title"
          eyebrow="Galeria"
          title="Em palco e fora dele"
          note="Ilustrações de pré-visualização: as fotografias estão na galeria atual."
        />
        <ul className="gallery">
          {galleryTiles.map((t) => (
            <li key={t.caption}>
              <figure className={`tile tile--${t.tone}`}>
                <Icon name={t.icon} className="tile__icon" />
                <figcaption className="tile__caption">{t.caption}</figcaption>
              </figure>
            </li>
          ))}
        </ul>
        <MoreLink href={legacy.gallery}>Abrir a galeria</MoreLink>
      </div>
    </section>
  );
}

// ---------- join ----------

function JoinUs() {
  return (
    <section className="section join" aria-labelledby="entrar-title">
      <div className="wrap split">
        <div>
          <SectionHead id="entrar-title" eyebrow="Novos elementos" title="Há sempre lugar para mais uma voz">
            Estudas na UPB e gostas de música, de noites longas e de boa companhia? Vem a um ensaio e conhece a
            tuna por dentro.
          </SectionHead>
          <p className="join__quote">Ninguém nasce a tocar bandolim. Aprende-se aqui.</p>
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
        </dl>
      </div>
    </section>
  );
}

// ---------- órgãos sociais ----------

function Orgaos() {
  return (
    <section id="orgaos" className="section section--raise" aria-labelledby="orgaos-title">
      <div className="wrap">
        <SectionHead id="orgaos-title" eyebrow="Quem conduz a tuna" title="Órgãos Sociais">
          Quatro órgãos, renovados a cada ano letivo, e um Ensaiador que dá o tom aos ensaios. Os nomes do mandato em
          curso estão na página dos Órgãos Sociais.
        </SectionHead>
        <ul className="orgaos">
          {orgaosSociais.map((o) => (
            <li key={o.name} className="orgao">
              <Icon name="bank" className="orgao__icon" />
              <h3 className="orgao__name">{o.name}</h3>
              <ul className="orgao__roles">
                {o.roles.map((r) => (
                  <li key={r}>{r}</li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
        <MoreLink href={legacy.roles}>Ver o mandato atual</MoreLink>
      </div>
    </section>
  );
}

// ---------- pedidos + members ----------

function Doors() {
  return (
    <section id="pedidos" className="section" aria-labelledby="pedidos-title">
      <div className="wrap doors">
        <div className="door door--request">
          <p className="eyebrow">Pedidos</p>
          <h2 id="pedidos-title" className="section__title">
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
          <h2 className="door__title">És tuno?</h2>
          <p className="door__lead">Ensaios, atuações, logística e mensagens da tuna, num só lugar.</p>
          <AccountLink className="btn btn--ghost btn--lg" />
          <Skyline patternId="merlons-members" />
        </div>
      </div>
    </section>
  );
}
