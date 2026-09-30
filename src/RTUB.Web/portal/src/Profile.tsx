import { useEffect } from 'react';
import type { CurrentUser } from './api';
import { Loading, useCurrentUser } from './App';
import { legacy, loginToProfile, portal } from './content';
import { Icon } from './icons';

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** /portal/perfil - who is signed in, and the way into the member area (still Blazor). */
export default function Profile() {
  const { user, failed, retry } = useCurrentUser();

  useEffect(() => {
    document.title = 'A minha conta · RTUB';
  }, []);

  return (
    <section className="page wrap" aria-labelledby="perfil-title">
      <header className="page__head">
        <p className="eyebrow">Área pessoal</p>
        <h1 id="perfil-title" className="page__title">
          A minha conta
        </h1>
      </header>

      {failed ? (
        <div className="state state--empty" role="alert">
          <p className="state__title">Não foi possível confirmar a sessão.</p>
          <p>Pode ser a ligação. Tenta outra vez dentro de instantes.</p>
          <div className="state__actions">
            <button type="button" className="btn btn--primary" onClick={retry}>
              Tentar novamente
            </button>
          </div>
        </div>
      ) : !user ? (
        <Loading label="A verificar a sessão…" />
      ) : user.authenticated ? (
        <SignedIn user={user} />
      ) : (
        <SignedOut />
      )}
    </section>
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
        <a className="btn btn--primary btn--lg" href={legacy.home}>
          <Icon name="arrow" />
          Abrir a área de membros
        </a>
        <a className="btn btn--ghost btn--lg" href={legacy.profile}>
          <Icon name="person" />
          Editar o perfil
        </a>
      </div>
      <p className="note">Terminar a sessão continua a fazer-se no menu da área de membros.</p>
    </div>
  );
}

function SignedOut() {
  return (
    <div className="account">
      <p className="account__lead">
        Ainda não entraste. Com a conta de membro tens à mão os ensaios, as atuações e o dia a dia da tuna.
      </p>
      <div className="account__actions">
        <a className="btn btn--primary btn--lg" href={loginToProfile}>
          <Icon name="login" />
          Entrar
        </a>
        <a className="btn btn--ghost btn--lg" href={portal.request}>
          <Icon name="send" />
          Pedir uma atuação
        </a>
      </div>
      <p className="note">As contas de membro são criadas pela própria tuna.</p>
    </div>
  );
}
