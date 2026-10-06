import { Component, useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { getCurrentUser, getVersion, wasSignedIn, type CurrentUser } from './api';
import { contactEmail, portal, social } from './content';
import { Icon, type IconName } from './icons';
import { MemberDrawer, MemberIdentity, MemberRail, type Member } from './MemberShell';

export const sections = [
  { id: 'events', label: 'Atuações' },
  { id: 'news', label: 'Novidades' },
  { id: 'music', label: 'Música' },
  { id: 'gallery', label: 'Galeria' },
  { id: 'governance', label: 'Órgãos Sociais' },
  { id: 'request', label: 'Pedidos' },
];

// The visitors' top bar and menu stay short: five public sections plus the "Pedir atuação" call to action.
// Signed-in members get the member shell instead (MemberShell.tsx): their menu, not the public one.
const navSections = sections.filter((s) => s.id !== 'request');

// The footer also lists the home's other anchors (FITAB, joining), which stay out of the top bar.
const footerSections = [
  { id: 'about', label: 'Quem somos' },
  ...sections,
  { id: 'fitab', label: 'FITAB' },
  { id: 'join', label: 'Junta-te a nós' },
];

// A section with its own React page links there (top bar, menu, footer); the rest are home
// anchors, always /#id so the same link scrolls on the home page and navigates from elsewhere.
const sectionPages: Record<string, string> = {
  events: portal.events,
  news: portal.news,
  music: portal.music,
  governance: portal.roles,
  gallery: portal.gallery,
};
const sectionHref = (id: string) => sectionPages[id] ?? `/#${id}`;

export function Layout({ children }: { children: ReactNode }) {
  useEffect(revealApp, []);
  const session = useSession();
  const [collapsed, toggleRail] = useRailCollapsed();
  const shell = session.kind !== 'visitor';
  const member = session.kind === 'member' ? session.user : undefined;

  return (
    <div className={shell ? `shell shell--member${collapsed ? ' shell--collapsed' : ''}` : 'shell'}>
      <a className="skip" href="#conteudo">Saltar para o conteúdo</a>
      <Header shell={shell} member={member} />
      {shell && <MemberRail member={member} collapsed={collapsed} onToggle={toggleRail} />}
      <div className="shell__body">
        <main id="conteudo" tabIndex={-1}>
          {children}
        </main>
        <Footer />
      </div>
    </div>
  );
}

/**
 * Visitor, member, or a member's page still waiting for /api/account/me. "pending" only when the last load was
 * signed in (wasSignedIn), so a member's page opens in the member shell instead of jumping into it; a visitor never
 * waits. A failed session check falls back to the visitor header (with Login).
 */
type Session = { kind: 'visitor' } | { kind: 'pending' } | { kind: 'member'; user: Member };

function useSession(): Session {
  const { user, failed } = useCurrentUser();
  const [hint] = useState(wasSignedIn);

  if (user?.authenticated) return { kind: 'member', user };
  if (user || failed || !hint) return { kind: 'visitor' };
  return { kind: 'pending' };
}

const RAIL_KEY = 'rtub-member-rail';

/** The rail's collapsed state, kept per browser; storage blocked → expanded on every load. */
function useRailCollapsed(): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(RAIL_KEY) === 'collapsed';
    } catch {
      return false;
    }
  });

  const toggle = () =>
    setCollapsed((c) => {
      try {
        if (c) localStorage.removeItem(RAIL_KEY);
        else localStorage.setItem(RAIL_KEY, 'collapsed');
      } catch {
        // Storage blocked: the choice lasts for this page only.
      }
      return !c;
    });

  return [collapsed, toggle];
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

function Brand() {
  return (
    <a className="brand" href={portal.home} aria-label="RTUB, início do portal">
      <img className="brand__logo" src="/icons/rtub-logo-192.png" alt="" width="40" height="40" />
      <span className="brand__text" aria-hidden="true">
        <span className="brand__name">RTUB</span>
        <span className="brand__sub">Boémios e Trovadores</span>
      </span>
    </a>
  );
}

/**
 * Visitors: the public sections, "Pedir atuação" and Login. Members: no public call to action - who is signed in, and
 * the member menu (the rail on wide screens, the drawer behind the menu button on phones).
 */
function Header({ shell, member }: { shell: boolean; member?: Member }) {
  return (
    <header className="header">
      <div className="header__inner wrap">
        <Brand />
        {!shell && (
          <nav className="header__nav" aria-label="Principal">
            <ul>
              {navSections.map((s) => (
                <li key={s.id}>
                  <a href={sectionHref(s.id)}>{s.label}</a>
                </li>
              ))}
            </ul>
          </nav>
        )}
        <div className="header__actions">
          {shell ? (
            <>
              <MemberIdentity member={member} />
              <MemberDrawer member={member} />
            </>
          ) : (
            <>
              <a className="btn btn--primary btn--sm header__cta" href={portal.request}>
                Pedir atuação
              </a>
              <LoginLink className="member-link" />
              <MobileMenu />
            </>
          )}
        </div>
      </div>
    </header>
  );
}

/** The visitors' way in: the React /login. RTUB has no public accounts; the login page says so. */
function LoginLink({ className }: { className: string }) {
  return (
    <a className={className} href={portal.login}>
      <Icon name="login" />
      <span className="member-link__label">Login</span>
    </a>
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
          <LoginLink className="member-link member-link--menu" />
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
            Portal público da RTUB: atuações, novidades, música, órgãos sociais, galeria e pedidos de atuação.
            Música, capa e tradição académica em Bragança desde 1991.
          </p>
        </div>
        <nav className="footer__col" aria-label="Portal">
          <h2 className="footer__title">Portal</h2>
          <ul>
            {footerSections.map((s) => (
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
              <a href="/#app">Instalar a app</a>
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

function useVersion() {
  const [state, setState] = useState<{ version?: string; failed?: boolean }>({});

  useEffect(() => {
    let live = true;
    getVersion().then(
      (version) => live && setState({ version }),
      () => live && setState({ failed: true }),
    );
    return () => {
      live = false;
    };
  }, []);

  return state;
}

function VersionTag() {
  const state = useVersion();

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
 * The home page's quiet way into the members-only area (the header uses Login and the member shell instead). Always
 * /profile, which explains the area is reserved to RTUB members before offering the login - the portal has no public
 * accounts. Reads `signedOutLabel` for visitors (and while the session is unknown), "A minha conta" once signed in.
 */
export function AccountLink({ className, signedOutLabel }: { className: string; signedOutLabel: string }) {
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
          <a className="btn btn--ghost" href={portal.home}>
            Voltar ao início
          </a>
        </div>
      </div>
    );
  }
}
