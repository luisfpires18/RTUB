import { useEffect } from 'react';
import type { CurrentUser } from './api';
import { useCurrentUser } from './App';
import { legacy, loginToProfile, portal } from './content';
import { Icon, type IconName } from './icons';

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/**
 * /portal/perfil - the members-only corner of a public portal. RTUB has no public accounts: the
 * tuna creates its members' logins, so this page states that first and keeps the public portal
 * one tap away. Signing in itself stays on the Blazor /login.
 */
export default function Profile() {
  const { user, failed, retry } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  useEffect(() => {
    document.title = 'Área de membros · RTUB';
  }, []);

  return (
    <section className="page wrap" aria-labelledby="perfil-title">
      <header className="page__head">
        <p className="eyebrow">{user ? 'Área de membros' : 'RTUB'}</p>
        <h1 id="perfil-title" className="page__title">
          {!user ? 'Área de membros' : signedIn ? 'A minha conta' : 'Área reservada a membros'}
        </h1>
      </header>

      <div className="members">
        <div className="members__main">
          {failed ? (
            <div className="notice" role="status">
              <p>Não conseguimos confirmar a sessão agora.</p>
              <button type="button" className="btn btn--ghost btn--sm" onClick={retry}>
                Tentar novamente
              </button>
            </div>
          ) : !user ? (
            <SessionSkeleton />
          ) : user.authenticated ? (
            <SignedIn user={user} />
          ) : (
            <SignedOut />
          )}
        </div>
        <PublicShortcuts />
      </div>
    </section>
  );
}

/** Same footprint as the account card, so nothing jumps when the session arrives. */
function SessionSkeleton() {
  return (
    <div className="account__card account__card--skeleton" role="status">
      <span className="skeleton skeleton--avatar" aria-hidden="true" />
      <span className="skeleton__lines" aria-hidden="true">
        <span className="skeleton skeleton--line" />
        <span className="skeleton skeleton--line skeleton--short" />
      </span>
      <span className="sr-only">A verificar a sessão…</span>
    </div>
  );
}

function SignedIn({ user }: { user: Extract<CurrentUser, { authenticated: true }> }) {
  return (
    <div className="account">
      <div className="account__card">
        <img
          className="account__avatar"
          src={user.avatarUrl}
          alt=""
          width="96"
          height="96"
          onError={(e) => {
            if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
          }}
        />
        <div className="account__who">
          <p className="account__name">{user.displayName}</p>
          {user.fullName && user.fullName !== user.displayName && <p className="account__full">{user.fullName}</p>}
          {user.categories.length > 0 ? (
            <ul className="chips" aria-label="Categoria na tuna">
              {user.categories.map((c) => (
                <li key={c} className="chip">
                  {c}
                </li>
              ))}
            </ul>
          ) : (
            <p className="note">Ainda sem categoria atribuída.</p>
          )}
        </div>
      </div>
      <div className="account__actions">
        <a className="btn btn--primary" href={legacy.home}>
          <Icon name="arrow" />
          Abrir a área de membros
        </a>
        <a className="btn btn--ghost" href={legacy.profile}>
          <Icon name="person" />
          Editar o perfil
        </a>
      </div>
      <p className="note">Terminar a sessão faz-se no menu da área de membros.</p>
    </div>
  );
}

function SignedOut() {
  return (
    <div className="account">
      <div className="account__card account__card--info">
        <Icon name="lock" className="account__lock" />
        <div className="account__who">
          <p>
            Esta área serve apenas os membros da RTUB. O acesso é criado pela própria tuna; não há registo público.
          </p>
          <p className="account__muted">Se és membro, entra para consultar ensaios, atuações e mensagens.</p>
        </div>
      </div>
      <div className="account__actions">
        <a className="btn btn--ghost" href={loginToProfile}>
          <Icon name="login" />
          Entrar como membro
        </a>
      </div>
    </div>
  );
}

const shortcuts: { href: string; icon: IconName; label: string }[] = [
  { href: portal.request, icon: 'send', label: 'Pedir uma atuação' },
  { href: '/portal#atuacoes', icon: 'calendar', label: 'Próximas atuações' },
  { href: '/portal#musica', icon: 'music', label: 'Discografia' },
  { href: '/portal#orgaos', icon: 'bank', label: 'Órgãos Sociais' },
  { href: '/portal#galeria', icon: 'images', label: 'Galeria' },
];

/** The public portal, always within reach: the main paths for everyone who is not a member. */
function PublicShortcuts() {
  return (
    <nav className="shortcuts" aria-labelledby="shortcuts-title">
      <h2 id="shortcuts-title" className="request__title">
        No portal
      </h2>
      <ul>
        {shortcuts.map((s, i) => (
          <li key={s.href}>
            <a className={i === 0 ? 'shortcut shortcut--primary' : 'shortcut'} href={s.href}>
              <Icon name={s.icon} />
              {s.label}
              <Icon name="arrow" className="shortcut__go" />
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}
