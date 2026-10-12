import { useEffect, useRef, useState, type FormEvent } from 'react';
import { accountApi, takeLinkQuery } from './accountApi';
import { portal } from './content';
import { Icon } from './icons';

const MIN = 8;
const MAX = 100;

// The emailed link: /reset-password?code=…&email=…. Read once when the page loads, then gone from the address bar;
// held here (never in storage or React state) until the reset succeeds.
let link = takeLinkQuery('code', 'email');

type Errors = { password?: string; confirmPassword?: string };

/**
 * /reset-password (task 036; was a Blazor page). The emailed link opens it; the member chooses a new password. The server
 * checks the token and the account as before and signs nobody in.
 */
export default function ResetPassword() {
  const [valid] = useState(() => Boolean(link.code && link.email));
  const email = link.email ?? '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [done, setDone] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const doneRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = 'Redefinir palavra-passe · RTUB';
  }, []);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (submitting) return;

    const found: Errors = {};
    if (!password) found.password = 'A palavra-passe é obrigatória';
    else if (password.length < MIN || password.length > MAX) found.password = `A palavra-passe deve ter pelo menos ${MIN} caracteres.`;
    if (!confirm) found.confirmPassword = 'A confirmação de palavra-passe é obrigatória';
    else if (confirm !== password) found.confirmPassword = 'A palavra-passe e a confirmação não coincidem.';
    setErrors(found);
    if (found.password || found.confirmPassword) return;

    setSubmitting(true);
    setBanner(undefined);
    const o = await accountApi.resetPassword(email, link.code ?? '', password, confirm);
    setSubmitting(false);
    if (o.kind === 'ok') {
      link = { code: null, email: null };
      setPassword('');
      setConfirm('');
      setDone(o.data.message);
      requestAnimationFrame(() => doneRef.current?.focus());
    } else if (o.kind === 'invalid') {
      const { password: p, confirmPassword: c, ...rest } = o.errors;
      setErrors({ password: p, confirmPassword: c });
      const other = Object.values(rest)[0];
      if (other) setBanner(other);
    } else {
      setBanner('Não foi possível redefinir a palavra-passe agora. Tenta de novo daqui a pouco.');
    }
  };

  return (
    <section className="page wrap" aria-labelledby="reset-title">
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="reset-title" className="page__title">
          Redefinir a palavra-passe
        </h1>
      </header>

      <div className="request__form-card account-card">
        {!valid ? (
          <div className="account-done account-done--error" role="alert">
            <Icon name="warning" />
            <p>Link de redefinição de palavra-passe inválido.</p>
            <a className="btn btn--ghost" href={portal.forgotPassword}>
              Pedir um novo link
            </a>
          </div>
        ) : done ? (
          <div ref={doneRef} tabIndex={-1} className="account-done" role="status">
            <Icon name="checkCircle" />
            <p>{done}</p>
            <a className="btn btn--primary" href={portal.login}>
              <Icon name="login" />
              Ir para o login
            </a>
          </div>
        ) : (
          <form className="form" noValidate onSubmit={onSubmit}>
            {banner && (
              <div className="form__banner" role="alert">
                {banner}
              </div>
            )}
            <div className="form__field">
              <label htmlFor="reset-email">Email</label>
              <input id="reset-email" type="email" autoComplete="username" value={email} readOnly />
            </div>
            <div className={errors.password ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor="reset-password">Nova palavra-passe</label>
              <input
                id="reset-password"
                type="password"
                autoComplete="new-password"
                maxLength={MAX}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                aria-invalid={errors.password ? true : undefined}
                aria-describedby="reset-password-hint"
              />
              <p id="reset-password-hint" className="form__hint">
                Pelo menos {MIN} caracteres.
              </p>
              {errors.password && <p className="form__error">{errors.password}</p>}
            </div>
            <div className={errors.confirmPassword ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor="reset-confirm">Confirmar a palavra-passe</label>
              <input
                id="reset-confirm"
                type="password"
                autoComplete="new-password"
                maxLength={MAX}
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                aria-invalid={errors.confirmPassword ? true : undefined}
              />
              {errors.confirmPassword && <p className="form__error">{errors.confirmPassword}</p>}
            </div>
            <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
              {submitting ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="lock" />}
              Redefinir
            </button>
            <p className="note">
              <a href={portal.login}>Voltar ao login</a>
            </p>
          </form>
        )}
      </div>
    </section>
  );
}
