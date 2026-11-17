// Home page hero banner with the decorative castle skyline.
import { portal } from '../data/content';
import { Icon } from './icons';

// ---------- hero ----------

export function Hero() {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="wrap hero__grid">
        <div className="hero__copy">
          <div className="hero__eyebrow">
            {/* Phones get a small badge here instead of the large emblem: the splash already shows it. */}
            <img className="hero__badge" src="/icons/rtub-logo-352.webp" alt="" width="52" height="52" />
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
          {/* Hidden on phones (max-width: 599px), but a hidden <img> still downloads: there it gets the splash's
              already-cached 34 KB WebP instead of the full logo. */}
          <picture>
            <source media="(max-width: 599px)" srcSet="/icons/rtub-logo-352.webp" />
            <img
              src="/images/rtub_logo-512.webp"
              srcSet="/images/rtub_logo-512.webp 1x, /images/rtub_logo.webp 2x"
              fetchPriority="high"
              width="512"
              height="512"
              alt="Emblema da RTUB: o Castelo de Bragança sob a lua, com guitarra e bandolim"
            />
          </picture>
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
export function Skyline({ patternId }: { patternId: string }) {
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
