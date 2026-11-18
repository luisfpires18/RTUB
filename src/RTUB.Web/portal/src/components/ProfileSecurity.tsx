// The /profile passkey and security sections.
import { FormField } from './ui/FormField';
import { FieldError } from './ui/FieldError';
import { Banner } from './ui/Banner';
import { useEffect, useId, useState, type FormEvent, type ReactNode, type Ref } from 'react';
import { Icon } from './icons';
import { memberAreaApi } from '../data/memberAreaApi';
import { shortDate } from '../data/membersApi';
import { passkeyApi, passkeysSupported, type MyPasskey } from '../data/passkey';
import { FormButtons, Section, problem, useFormState } from './ProfileShared';
import { Loading } from './ui/Loading';

export function PasskeySection() {
  const [list, setList] = useState<MyPasskey[] | 'failed'>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();
  const [password, setPassword] = useState('');
  const [passwordError, setPasswordError] = useState<string>();
  const id = useId();
  const supported = passkeysSupported();

  useEffect(() => {
    passkeyApi.list().then((o) => setList(o.kind === 'ok' ? o.data : 'failed'));
  }, []);

  const add = async (e: FormEvent) => {
    e.preventDefault();
    setError(undefined);
    setMessage(undefined);
    setPasswordError(undefined);
    if (!password) {
      setPasswordError('Indica a palavra-passe atual.');
      return;
    }
    setBusy(true);
    const o = await passkeyApi.add(password);
    setBusy(false);
    if (o.kind === 'invalid') setPasswordError(Object.values(o.errors)[0]);
    else if (o.kind === 'ok') {
      setPassword('');
      setList((l) => [...(Array.isArray(l) ? l : []), o.data]);
      setMessage('Ativado. Da próxima vez, entra com "Entrar com Face ID / impressão digital".');
    } else if (o.kind === 'exists') setError('Este dispositivo já está ativado.');
    else if (o.kind === 'full') setError('Já tens o máximo de dispositivos. Remove um antes de juntar outro.');
    else if (o.kind === 'signin') setError('A sessão terminou. Entra outra vez.');
    else if (o.kind === 'failed') setError('Não foi possível ativar neste dispositivo. Tenta outra vez.');
  };

  const remove = async (passkey: MyPasskey) => {
    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    const o = await passkeyApi.remove(passkey.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') setList((l) => (Array.isArray(l) ? l.filter((p) => p.id !== passkey.id) : l));
    else setError(problem(o, 'remover'));
  };

  let body: ReactNode;
  if (!list) body = <Loading label="A carregar os dispositivos…" />;
  else if (list === 'failed') body = <p>Não foi possível ver os dispositivos ativados.</p>;
  else if (list.length === 0) body = <p>Ainda sem dispositivos ativados.</p>;
  else
    body = (
      <ul className="passkey-list">
        {list.map((p) => (
          <li key={p.id}>
            <span>
              <strong>{p.name}</strong>
              <span className="note"> · ativado a {shortDate(p.createdAt)}</span>
            </span>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => remove(p)} disabled={busy}>
              <Icon name="trash" />
              Remover
            </button>
          </li>
        ))}
      </ul>
    );

  return (
    <Section id="passkeys" title="Face ID / impressão digital" icon="shield">
      <p className="note">
        Entra sem palavra-passe, com o desbloqueio do telemóvel ou do computador (Face ID, impressão digital, Windows
        Hello). Ativa-se em cada dispositivo; a palavra-passe continua a funcionar.
      </p>
      <div className="push-state" role="status" aria-live="polite">
        {body}
      </div>
      {supported ? (
        <form className="form profile-form" onSubmit={add} noValidate>
          <FormField id={`${id}-password`} label="Palavra-passe atual" error={passwordError} alert>
            <input
              id={`${id}-password`}
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              aria-invalid={passwordError ? true : undefined}
            />
          </FormField>
          <p className="form__hint">Pedida só para ativar, para que ninguém o faça com a tua sessão aberta.</p>
          <div className="push-actions">
            <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
              <Icon name="shield" />
              Ativar neste dispositivo
            </button>
          </div>
        </form>
      ) : (
        <p className="note">Este navegador não permite entrar com Face ID ou impressão digital.</p>
      )}
      {message && (
        <p className="note" role="status">
          {message}
        </p>
      )}
      <FieldError text={error} />
    </Section>
  );
}

// ---------- Segurança ----------

export function SecuritySection({ ref, onChanged }: { ref: Ref<HTMLElement>; onChanged: () => void }) {
  const id = useId();
  const blank = { currentPassword: '', newPassword: '', confirmPassword: '' };
  const [form, setForm] = useState(blank);
  const [done, setDone] = useState(false);
  const f = useFormState();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    f.setBusy(true);
    f.setBanner(undefined);
    setDone(false);
    const o = await memberAreaApi.password(form.currentPassword, form.newPassword, form.confirmPassword);
    f.setBusy(false);
    if (o.kind === 'ok') {
      setForm(blank);
      f.setErrors({});
      setDone(true);
      onChanged();
    } else if (o.kind === 'invalid') f.setErrors(o.errors);
    else {
      f.setErrors({});
      f.setBanner(problem(o, 'mudar a palavra-passe'));
    }
  };

  const secret = (key: keyof typeof blank, label: string, autoComplete: string) => (
    <div className={f.field(key)}>
      <label htmlFor={`${id}-${key}`}>{label}</label>
      <input
        id={`${id}-${key}`}
        type="password"
        autoComplete={autoComplete}
        value={form[key]}
        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
        aria-invalid={f.errors[key] ? true : undefined}
      />
      {f.error(key)}
    </div>
  );

  return (
    <Section id="security" title="Segurança" icon="lock" sectionRef={ref}>
      <form className="form profile-form" onSubmit={submit} noValidate>
        <Banner text={f.banner} />
        {secret('currentPassword', 'Palavra-passe atual', 'current-password')}
        <div className="form__row">
          {secret('newPassword', 'Nova palavra-passe', 'new-password')}
          {secret('confirmPassword', 'Repetir a nova', 'new-password')}
        </div>
        <p className="form__hint">Pelo menos 8 caracteres. Continuas com a sessão aberta depois de mudar.</p>
        {done && (
          <p className="profile-saved" role="status">
            <Icon name="check" />
            Palavra-passe mudada.
          </p>
        )}
        <FormButtons busy={f.busy} label="Mudar a palavra-passe" />
      </form>
    </Section>
  );
}

// ---------- signed out ----------
