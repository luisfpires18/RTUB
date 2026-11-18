import { useEffect, useRef, useState, type FormEvent } from 'react';
import { accountApi, takeLinkQuery } from '../data/accountApi';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { Spinner } from '../components/ui/Spinner';
import { FormField } from '../components/ui/FormField';
import { AccountDone } from '../components/ui/AccountDone';
import { PageHead } from '../components/ui/PageHead';
import { Banner } from '../components/ui/Banner';

const MIN = 8;
const MAX = 100;

// The emailed link: /reset-password?code=…&email=…. Read once when the page loads, then gone from the address bar;
// held here (never in storage or React state) until the reset succeeds.
let link = takeLinkQuery('code', 'email');

type Errors = { password?: string; confirmPassword?: string };

/**
 * /reset-password. The emailed link opens it; the member chooses a new password. The server
 * checks the token and the account and signs nobody in.
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
      <PageHead plain eyebrow="Área de membros" title="Redefinir a palavra-passe" titleId="reset-title" />

      <div className="request__form-card account-card">
        {!valid ? (
          <AccountDone error role="alert" icon="warning" message="Link de redefinição de palavra-passe inválido.">
            <a className="btn btn--ghost" href={portal.forgotPassword}>
              Pedir um novo link
            </a>
          </AccountDone>
        ) : done ? (
          <AccountDone focusRef={doneRef} icon="checkCircle" message={done}>
            <a className="btn btn--primary" href={portal.login}>
              <Icon name="login" />
              Ir para o login
            </a>
          </AccountDone>
        ) : (
          <form className="form" noValidate onSubmit={onSubmit}>
            <Banner text={banner} block />
            <FormField id="reset-email" label="Email">
              <input id="reset-email" type="email" autoComplete="username" value={email} readOnly />
            </FormField>
            <FormField id="reset-password" label="Nova palavra-passe" error={errors.password} hint={`Pelo menos ${MIN} caracteres.`}>
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
            </FormField>
            <FormField id="reset-confirm" label="Confirmar a palavra-passe" error={errors.confirmPassword}>
              <input
                id="reset-confirm"
                type="password"
                autoComplete="new-password"
                maxLength={MAX}
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                aria-invalid={errors.confirmPassword ? true : undefined}
              />
            </FormField>
            <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
              {submitting ? <Spinner small /> : <Icon name="lock" />}
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
