import { useEffect, useRef, useState, type FormEvent } from 'react';
import { accountApi } from './accountApi';
import { portal } from './content';
import { Icon } from './icons';

/**
 * /forgot-password (task 036; was a Blazor page). Anyone asks for a reset link by email. The answer is the same whether
 * or not the address has an account; the server sends the link only to a confirmed account.
 */
export default function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string>();
  const [banner, setBanner] = useState<string>();
  const [sent, setSent] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const doneRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = 'Recuperar palavra-passe · RTUB';
  }, []);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (submitting) return;
    if (!email.trim()) {
      setError('O email é obrigatório');
      document.getElementById('forgot-email')?.focus();
      return;
    }

    setSubmitting(true);
    setBanner(undefined);
    const o = await accountApi.forgotPassword(email.trim());
    setSubmitting(false);
    if (o.kind === 'ok') {
      setError(undefined);
      setSent(o.data.message);
      requestAnimationFrame(() => doneRef.current?.focus());
    } else if (o.kind === 'invalid') {
      setError(o.errors.email ?? Object.values(o.errors)[0]);
    } else {
      setBanner('Não foi possível enviar o pedido agora. Tenta de novo daqui a pouco.');
    }
  };

  return (
    <section className="page wrap" aria-labelledby="forgot-title">
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="forgot-title" className="page__title">
          Esqueceste a palavra-passe?
        </h1>
        <p className="page__lead">Indica o email da tua conta e enviamos-te um link para escolheres uma nova.</p>
      </header>

      <div className="request__form-card account-card">
        {sent ? (
          <div ref={doneRef} tabIndex={-1} className="account-done" role="status">
            <Icon name="envelope" />
            <p>{sent}</p>
            <a className="btn btn--ghost" href={portal.login}>
              <Icon name="arrowLeft" />
              Voltar ao login
            </a>
          </div>
        ) : (
          <form className="form" noValidate onSubmit={onSubmit}>
            {banner && (
              <div className="form__banner" role="alert">
                {banner}
              </div>
            )}
            <div className={error ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor="forgot-email">Email</label>
              <input
                id="forgot-email"
                name="email"
                type="email"
                autoComplete="email"
                autoCapitalize="none"
                spellCheck={false}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                aria-invalid={error ? true : undefined}
                aria-describedby={error ? 'forgot-email-error' : undefined}
              />
              {error && (
                <p id="forgot-email-error" className="form__error">
                  {error}
                </p>
              )}
            </div>
            <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
              {submitting ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="send" />}
              Enviar o link
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
