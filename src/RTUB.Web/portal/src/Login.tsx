import { useEffect, useRef, useState, type FormEvent } from 'react';
import { signIn, type SignInFailure } from './api';
import { legacy, portal } from './content';
import { Icon } from './icons';

type Banner = SignInFailure;
type Errors = { username?: string; password?: string };

const messages: Record<Banner, string> = {
  invalid: 'Nome de utilizador ou palavra-passe incorretos.',
  locked: 'Esta conta está bloqueada de momento. Tenta mais tarde ou recupera a palavra-passe.',
  expelled: 'Esta conta está suspensa. Se achas que é um engano, fala com a direção da RTUB.',
  throttled: 'Demasiadas tentativas seguidas a partir deste dispositivo. Espera uns minutos e tenta de novo.',
  expired: 'O formulário expirou. Tenta entrar de novo.',
  failed: 'Não foi possível entrar agora. Tenta de novo daqui a pouco.',
};

// Older links and the plain form fallback still land here as /login?error=Invalid|Locked|Expelled.
const queryErrors: Record<string, Banner> = { invalid: 'invalid', locked: 'locked', expelled: 'expelled' };

/** The cookie handler sends ReturnUrl, portal links send returnUrl: read either. */
function queryValue(name: string): string {
  for (const [key, value] of new URLSearchParams(location.search)) {
    if (key.toLowerCase() === name) return value;
  }
  return '';
}

/**
 * /login - members only. RTUB has no public accounts: the tuna creates and manages each member's
 * access, and everything public stays open without signing in. Signed-in members never reach this
 * page (Program.cs sends them to /events). Signing in is the existing POST /auth/login.
 */
export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<Banner | null>(() => queryErrors[queryValue('error').toLowerCase()] ?? null);
  const [submitting, setSubmitting] = useState(false);
  const bannerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = 'Entrar · RTUB';
  }, []);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (submitting) return;

    const found: Errors = {};
    if (!username.trim()) found.username = 'Indica o nome de utilizador ou o email.';
    if (!password) found.password = 'Indica a palavra-passe.';
    setErrors(found);
    if (found.username || found.password) {
      document.getElementById(found.username ? 'login-username' : 'login-password')?.focus();
      return;
    }

    setSubmitting(true);
    setBanner(null);
    const outcome = await signIn(username.trim(), password, rememberMe, queryValue('returnurl'));

    if (outcome.kind === 'signedIn') {
      // Keep the busy state: the page is about to change.
      location.assign(outcome.redirect);
      return;
    }

    setSubmitting(false);
    setPassword('');
    setBanner(outcome.kind);
    requestAnimationFrame(() => bannerRef.current?.focus());
  };

  const describedBy = (key: keyof Errors) => (errors[key] ? `login-${key}-error` : undefined);

  return (
    <section className="page wrap" aria-labelledby="login-title">
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="login-title" className="page__title">
          Entrar
        </h1>
        <p className="page__lead">Acesso reservado aos membros da RTUB, com a conta que a tuna te atribuiu.</p>
      </header>

      <div className="request">
        <div className="request__form-card">
          <form className="form" noValidate onSubmit={onSubmit}>
            {banner && (
              <div ref={bannerRef} tabIndex={-1} className={`form__banner form__banner--${banner}`} role="alert">
                {messages[banner]}
              </div>
            )}

            <div className={errors.username ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor="login-username">Nome de utilizador ou email</label>
              <input
                id="login-username"
                name="username"
                type="text"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                aria-invalid={errors.username ? true : undefined}
                aria-describedby={describedBy('username')}
              />
              {errors.username && (
                <p id="login-username-error" className="form__error">
                  {errors.username}
                </p>
              )}
            </div>

            <div className={errors.password ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor="login-password">Palavra-passe</label>
              <input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                aria-invalid={errors.password ? true : undefined}
                aria-describedby={describedBy('password')}
              />
              {errors.password && (
                <p id="login-password-error" className="form__error">
                  {errors.password}
                </p>
              )}
            </div>

            <label className="form__check">
              <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
              Manter a sessão iniciada neste dispositivo
            </label>

            <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
              {submitting ? (
                <>
                  <span className="spinner spinner--small" aria-hidden="true" />A entrar…
                </>
              ) : (
                <>
                  <Icon name="login" />
                  Entrar
                </>
              )}
            </button>

            <p className="note">
              <a href={legacy.forgotPassword}>Esqueceste a palavra-passe?</a>
            </p>
          </form>
        </div>

        <aside className="request__go" aria-labelledby="login-about-title">
          <h2 id="login-about-title" className="request__title">
            Só para membros
          </h2>
          <p>
            Não há registo público. As contas são criadas e geridas pela própria tuna; se és membro e ainda não tens
            acesso, fala com a direção.
          </p>
          <p className="request__alt">Não és membro?</p>
          <p>O portal é aberto a todos: atuações, música e pedidos não precisam de conta.</p>
          <a className="btn btn--ghost" href={portal.home}>
            <Icon name="arrow" />
            Voltar ao portal
          </a>
        </aside>
      </div>
    </section>
  );
}
