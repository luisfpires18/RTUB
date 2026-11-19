// Public header, mobile menu, footer and version tag.
import { useRef, useState } from 'react';
import { portal, social } from '../data/content';
import { Icon } from './icons';
import { ReleaseNotesDialog } from './ReleaseNotes';
import { releaseNotesApi, type ReleaseNote } from '../data/releaseNotesApi';
import { MemberCategory, MemberDrawer, MemberIdentity, MemberInbox, type Member } from './MemberShell';
import { navSections, sectionHref } from '../data/sections';
import { useCurrentUser, ExternalLink } from './AppShared';
import { IconAction } from './ui/IconAction';

// ---------- chrome ----------

function Brand() {
  return (
    <a className="brand" href={portal.home} aria-label="RTUB, início do portal">
      <img className="brand__logo" src="/icons/rtub-logo-352.webp" alt="" width="40" height="40" />
      <span className="brand__text" aria-hidden="true">
        <span className="brand__name">RTUB</span>
        <span className="brand__sub">Boémios e Trovadores</span>
      </span>
    </a>
  );
}

/**
 * Visitors: the public sections, "Pedir atuação" and Login. Members: no public call to action - who is signed in, and
 * the inbox, the profile chip and the member menu (a drawer from the right, behind the menu button).
 */
export function Header({ shell, member }: { shell: boolean; member?: Member }) {
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
              {member && <MemberInbox />}
              {member && <MemberCategory member={member} />}
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
export function LoginLink({ className }: { className: string }) {
  return (
    <a className={className} href={portal.login}>
      <Icon name="login" />
      <span className="member-link__label">Login</span>
    </a>
  );
}

/** A native modal <dialog>: focus containment, Esc and an inert page come from the platform. */
export function MobileMenu() {
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
          <IconAction icon="close" label="Fechar menu" onClick={hide} large />
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

export function Footer() {
  return (
    <footer className="footer">
      <div className="wrap footer__grid">
        <div className="footer__about">
          <Brand />
          <p>
            Portal público da RTUB: atuações, novidades, música, órgãos sociais, galeria, loja e pedidos de atuação.
            Música, capa e tradição académica em Bragança desde 1991.
          </p>
        </div>
        <div className="footer__social-block">
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
        <a className="footer__legal" href={portal.privacy}>
          Política de Privacidade
        </a>
        <VersionTag />
      </div>
    </footer>
  );
}

/**
 * The build the footer shows. It rides on /api/account/me, so a page load asks nothing extra for it. A
 * signed-in member can reopen the running release's notes from here: read on click only, and
 * reading never touches the acknowledgement.
 */
function VersionTag() {
  const { user, failed } = useCurrentUser();
  const [notes, setNotes] = useState<ReleaseNote[] | 'none' | 'failed'>();

  if (failed) return <p className="version">Versão indisponível</p>;
  const member = user?.authenticated === true;
  const open = async () => {
    setNotes(undefined);
    const o = await releaseNotesApi.state();
    if (o.kind !== 'ok') return setNotes('failed');
    setNotes(o.data.current ? [o.data.current] : 'none');
  };
  return (
    <p className="version" aria-busy={!user}>
      {user ? `Versão ${user.version}` : 'A obter versão…'}
      {member && (
        <>
          {' · '}
          <button type="button" className="version__notes" onClick={open}>
            Ver novidades da versão
          </button>
        </>
      )}
      {Array.isArray(notes) && <ReleaseNotesDialog notes={notes} onClose={() => setNotes(undefined)} />}
      {notes === 'none' && (
        <span role="status" className="version__hint">
          Esta versão não tem novidades para mostrar.
        </span>
      )}
      {notes === 'failed' && (
        <span role="status" className="version__hint">
          Não foi possível ver as novidades.
        </span>
      )}
    </p>
  );
}
