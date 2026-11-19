import { useEffect, useRef, useState, type FormEvent } from 'react';
import { accountApi } from '../data/accountApi';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { FormField, fieldProps } from '../components/ui/FormField';
import { Banner } from '../components/ui/Banner';
import { Spinner } from '../components/ui/Spinner';
import { AccountDone } from '../components/ui/AccountDone';
import { PageHead } from '../components/ui/PageHead';

/**
 * /forgot-password. Anyone asks for a reset link by email. The answer is the same whether
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
      <PageHead plain eyebrow="Área de membros" title="Esqueceste a palavra-passe?" titleId="forgot-title" lead="Indica o email da tua conta e enviamos-te um link para escolheres uma nova." />

      <div className="request__form-card account-card">
        {sent ? (
          <AccountDone focusRef={doneRef} icon="envelope" message={sent}>
            <a className="btn btn--ghost" href={portal.login}>
              <Icon name="arrowLeft" />
              Voltar ao login
            </a>
          </AccountDone>
        ) : (
          <form className="form" noValidate onSubmit={onSubmit}>
            <Banner block text={banner} />
            <FormField id="forgot-email" label="Email" error={error}>
              <input
                {...fieldProps('forgot-email', error)}
                name="email"
                type="email"
                autoComplete="email"
                autoCapitalize="none"
                spellCheck={false}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </FormField>
            <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
              {submitting ? <Spinner small /> : <Icon name="send" />}
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
