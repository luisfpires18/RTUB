import { useEffect, useState } from 'react';
import { signOut, type CurrentUser } from './api';
import { useCurrentUser } from './App';
import { legacy, loginToProfile, portal } from './content';
import { Icon, type IconName } from './icons';

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/**
 * /profile - the members-only corner of a public portal. RTUB has no public accounts: the
 * tuna creates its members' logins, so this page states that first and keeps the public portal
 * one tap away. Signing in is the React /login.
 */
export default function Profile() {
  const { user, failed, retry } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  useEffect(() => {
    document.title = 'Área de membros · RTUB';
  }, []);

  return (
    <section className="page wrap" aria-labelledby="profile-title">
      <header className="page__head">
        <p className="eyebrow">{user ? 'Área de membros' : 'RTUB'}</p>
        <h1 id="profile-title" className="page__title">
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
        <a className="btn btn--primary" href={portal.events}>
          <Icon name="calendar" />
          Agenda de atuações
        </a>
        <a className="btn btn--ghost" href={portal.myEnrollments}>
          <Icon name="calendar" />
          As minhas inscrições
        </a>
        <a className="btn btn--ghost" href={portal.members}>
          <Icon name="person" />
          Membros
        </a>
        <a className="btn btn--ghost" href={legacy.memberProfile}>
          <Icon name="person" />
          Editar o perfil
        </a>
      </div>
      <SignOut />
    </div>
  );
}

function SignOut() {
  const [state, setState] = useState<'idle' | 'busy' | 'failed'>('idle');
  return (
    <p className="note">
      <button
        type="button"
        className="btn btn--ghost btn--sm"
        disabled={state === 'busy'}
        onClick={async () => {
          setState('busy');
          if (!(await signOut())) setState('failed');
        }}
      >
        <Icon name="login" />
        Terminar sessão
      </button>
      {state === 'failed' && ' Não foi possível terminar a sessão agora. Tente de novo.'}
    </p>
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
  { href: '/#events', icon: 'calendar', label: 'Próximas atuações' },
  { href: portal.music, icon: 'music', label: 'Discografia' },
  { href: portal.roles, icon: 'bank', label: 'Órgãos Sociais' },
  { href: '/#gallery', icon: 'images', label: 'Galeria' },
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
