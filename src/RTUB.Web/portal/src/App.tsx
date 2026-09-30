import { Component, useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { getCurrentUser, type CurrentUser } from './api';
import { contactEmail, legacy, portal, social } from './content';
import { Icon, type IconName } from './icons';

export const sections = [
  { id: 'events', label: 'Atuações' },
  { id: 'music', label: 'Música' },
  { id: 'gallery', label: 'Galeria' },
  { id: 'governance', label: 'Órgãos Sociais' },
  { id: 'request', label: 'Pedidos' },
];

// The top bar and the menu stay short: four public sections plus the "Pedir atuação" call to action.
const navSections = sections.filter((s) => s.id !== 'request');

// Always /portal#id, so the same link scrolls on the home page and navigates from Privacy.
const sectionHref = (id: string) => `/portal#${id}`;

export function Layout({ children }: { children: ReactNode }) {
  useEffect(revealApp, []);

  return (
    <>
      <a className="skip" href="#conteudo">Saltar para o conteúdo</a>
      <PilotBanner />
      <Header />
      <main id="conteudo" tabIndex={-1}>
        {children}
      </main>
      <Footer />
    </>
  );
}

// ---------- launch splash ----------

const LAUNCH_KEY = 'rtub-portal-launched';
let revealed = false;

/**
 * The splash is static markup in index.html, so it covers the only real wait - the bundle
 * loading - and costs nothing extra. Once React has committed, it lifts like a stage curtain on
 * the first load of a session (every PWA launch is a new session) and is removed instantly on
 * later loads or when reduced motion is requested. No timers: nothing waits on purpose.
 */
function revealApp() {
  const splash = document.getElementById('splash');
  if (revealed || !splash) return;
  revealed = true;

  let firstLoad = true;
  try {
    firstLoad = sessionStorage.getItem(LAUNCH_KEY) === null;
    sessionStorage.setItem(LAUNCH_KEY, '1');
  } catch {
    // Storage blocked (private mode, policy): treat as a first load.
  }

  if (!firstLoad || matchMedia('(prefers-reduced-motion: reduce)').matches) {
    splash.remove();
    return;
  }

  splash.classList.add('splash--exit');
  Promise.allSettled(splash.getAnimations({ subtree: true }).map((a) => a.finished)).then(() => splash.remove());
}

// ---------- chrome ----------

function PilotBanner() {
  return (
    <div className="pilot">
      <p className="pilot__text">
        <strong>Pré-visualização</strong>
        <span className="pilot__more"> do novo portal público. Algumas secções usam conteúdo ilustrativo.</span>
      </p>
      <a className="pilot__link" href={legacy.home}>
        Voltar ao site atual
      </a>
    </div>
  );
}

function Brand() {
  return (
    <a className="brand" href="/portal" aria-label="RTUB, início do portal">
      <img className="brand__logo" src="/icons/rtub-logo-192.png" alt="" width="40" height="40" />
      <span className="brand__text" aria-hidden="true">
        <span className="brand__name">RTUB</span>
        <span className="brand__sub">Boémios e Trovadores</span>
      </span>
    </a>
  );
}

function Header() {
  return (
    <header className="header">
      <div className="header__inner wrap">
        <Brand />
        <nav className="header__nav" aria-label="Principal">
          <ul>
            {navSections.map((s) => (
              <li key={s.id}>
                <a href={sectionHref(s.id)}>{s.label}</a>
              </li>
            ))}
          </ul>
        </nav>
        <div className="header__actions">
          <a className="btn btn--primary btn--sm header__cta" href={portal.request}>
            Pedir atuação
          </a>
          <AccountLink className="member-link" />
          <MobileMenu />
        </div>
      </div>
    </header>
  );
}

/** A native modal <dialog>: focus containment, Esc and an inert page come from the platform. */
function MobileMenu() {
  const dialog = useRef<HTMLDialogElement>(null);
  const [open, setOpen] = useState(false);

  const show = () => {
    dialog.current?.showModal();
    setOpen(true);
  };
  const hide = () => dialog.current?.close();

  return (
    <>
      <button
        type="button"
        className="icon-btn header__menu"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls="portal-menu"
        onClick={show}
      >
        <Icon name="menu" />
        <span className="sr-only">Abrir menu</span>
      </button>
      <dialog id="portal-menu" ref={dialog} className="menu" aria-label="Menu" onClose={() => setOpen(false)}>
        <div className="menu__top">
          <Brand />
          <button type="button" className="icon-btn" onClick={hide}>
            <Icon name="close" />
            <span className="sr-only">Fechar menu</span>
          </button>
        </div>
        <nav aria-label="Menu principal">
          <ol className="menu__links">
            {navSections.map((s, i) => (
              <li key={s.id}>
                <a href={sectionHref(s.id)} onClick={hide}>
                  <span className="menu__num" aria-hidden="true">
                    {String(i + 1).padStart(2, '0')}
                  </span>
                  {s.label}
                </a>
              </li>
            ))}
          </ol>
        </nav>
        <div className="menu__actions">
          <a className="btn btn--primary" href={portal.request}>
            <Icon name="send" />
            Pedir uma atuação
          </a>
          <AccountLink className="member-link member-link--menu" signedOutLabel="Área reservada a membros" />
        </div>
      </dialog>
    </>
  );
}

function Footer() {
  return (
    <footer className="footer">
      <div className="wrap footer__grid">
        <div className="footer__about">
          <Brand />
          <p>
            Portal público da RTUB: atuações, música, órgãos sociais, galeria e pedidos de atuação. Música, capa e
            tradição académica em Bragança desde 1991.
          </p>
        </div>
        <nav className="footer__col" aria-label="Portal">
          <h2 className="footer__title">Portal</h2>
          <ul>
            {sections.map((s) => (
              <li key={s.id}>
                <a href={sectionHref(s.id)}>{s.label}</a>
              </li>
            ))}
          </ul>
        </nav>
        <nav className="footer__col" aria-label="RTUB">
          <h2 className="footer__title">RTUB</h2>
          <ul>
            <li>
              <a href={portal.request}>Fazer um pedido</a>
            </li>
            <li>
              <a href="/portal#app">Instalar a app</a>
            </li>
            <li>
              <a href={portal.profile}>Área de membros</a>
            </li>
            <li>
              <a href={portal.privacy}>Política de Privacidade</a>
            </li>
            <li>
              <a href={`mailto:${contactEmail}`}>{contactEmail}</a>
            </li>
          </ul>
        </nav>
        <div className="footer__col">
          <h2 className="footer__title">Redes</h2>
          <ul className="footer__social">
            {social.map((s) => (
              <li key={s.name}>
                <ExternalLink href={s.href} className="icon-btn">
                  <Icon name={s.icon} />
                  <span className="sr-only">{s.name}</span>
                </ExternalLink>
              </li>
            ))}
          </ul>
        </div>
      </div>
      <div className="wrap footer__base">
        <p>© 1991–{new Date().getFullYear()} RTUB · Boémios e Trovadores</p>
        <VersionTag />
      </div>
    </footer>
  );
}

/** Reads the existing GET /api/version: the pilot's one live call to the C# host. */
function VersionTag() {
  const [state, setState] = useState<{ version?: string; failed?: boolean }>({});

  useEffect(() => {
    const abort = new AbortController();
    fetch('/api/version', { signal: abort.signal, headers: { Accept: 'application/json' } })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((body: { version?: unknown }) =>
        setState(typeof body.version === 'string' ? { version: body.version } : { failed: true }),
      )
      .catch(() => {
        if (!abort.signal.aborted) setState({ failed: true });
      });
    return () => abort.abort();
  }, []);

  if (state.failed) return <p className="version">Versão indisponível</p>;
  return (
    <p className="version" aria-busy={!state.version}>
      {state.version ? `Versão ${state.version}` : 'A obter versão…'}
    </p>
  );
}

// ---------- session ----------

/** The signed-in state for this page load; `retry` asks the server again. */
export function useCurrentUser() {
  const [state, setState] = useState<{ user?: CurrentUser; failed?: boolean }>({});

  const load = useCallback((refresh: boolean) => {
    setState({});
    getCurrentUser(refresh).then(
      (user) => setState({ user }),
      () => setState({ failed: true }),
    );
  }, []);

  useEffect(() => load(false), [load]);
  return { ...state, retry: () => load(true) };
}

/**
 * The quiet way into the members-only area. Always /portal/profile, which explains the area is
 * reserved to RTUB members before offering the login - the portal has no public accounts. Reads
 * "Membros" for visitors (and while the session is unknown), "A minha conta" once signed in.
 */
export function AccountLink({ className, signedOutLabel = 'Membros' }: { className: string; signedOutLabel?: string }) {
  const { user } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  return (
    <a className={className} href={portal.profile}>
      <Icon name={signedIn ? 'person' : 'lock'} />
      <span className="member-link__label">{signedIn ? 'A minha conta' : signedOutLabel}</span>
    </a>
  );
}

// ---------- shared pieces ----------

export function ExternalLink({ href, className, children }: { href: string; className?: string; children: ReactNode }) {
  return (
    <a href={href} className={className} target="_blank" rel="noopener noreferrer">
      {children}
      <span className="sr-only"> (abre numa nova janela)</span>
    </a>
  );
}

export function Loading({ label }: { label: string }) {
  return (
    <div className="state" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  );
}

export function EmptyState({ icon, title, children }: { icon: IconName; title: string; children?: ReactNode }) {
  return (
    <div className="state state--empty">
      <Icon name={icon} className="state__icon" />
      <p className="state__title">{title}</p>
      {children}
    </div>
  );
}

/** Catches render errors and failed lazy chunks (e.g. offline) with a way out. */
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="state wrap" role="alert">
        <p className="state__title">Não foi possível carregar esta página.</p>
        <p>Verifica a ligação e tenta outra vez.</p>
        <div className="state__actions">
          <button type="button" className="btn btn--primary" onClick={() => location.reload()}>
            Tentar novamente
          </button>
          <a className="btn btn--ghost" href={legacy.home}>
            Abrir o site atual
          </a>
        </div>
      </div>
    );
  }
}
