// Owner tracing user field, select and live announcement.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { ownerApi, ownerProblem } from '../data/ownerApi';
import { Flash } from './OwnerUi';

/** A member field that suggests usernames as you type (native datalist: no list of 100 options to scroll). */
export function UserField({
  id,
  label,
  placeholder,
  value,
  users,
  onChange,
}: {
  id: string;
  label: string;
  placeholder: string;
  value: string;
  users: string[];
  onChange: (v: string) => void;
}) {
  return (
    <div className="form__field">
      <label htmlFor={id}>{label}</label>
      <span className="control control--sm">
        <Icon name="person" />
        <input id={id} type="search" list={`${id}-list`} autoComplete="off" placeholder={placeholder} value={value} onChange={(e) => onChange(e.target.value)} />
      </span>
      <datalist id={`${id}-list`}>
        {users.map((u) => (
          <option key={u} value={u} />
        ))}
      </datalist>
    </div>
  );
}

export function Select({
  id,
  label,
  all,
  value,
  items,
  text = (i) => i,
  onChange,
}: {
  id: string;
  label: string;
  all: string;
  value: string;
  items: string[];
  text?: (item: string) => string;
  onChange: (v: string) => void;
}) {
  return (
    <div className="form__field">
      <label htmlFor={id}>{label}</label>
      <span className="control control--sm control--select">
        <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">{all}</option>
          {items.map((i) => (
            <option key={i} value={i}>
              {text(i)}
            </option>
          ))}
        </select>
      </span>
    </div>
  );
}

/** A message shown at once, as a pop-up, to the users connected right now; nothing is stored. */
export function Announcement() {
  const id = useId();
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; error?: boolean }>();

  const send = async () => {
    setBusy(true);
    const o = await ownerApi.announce(text);
    setBusy(false);
    if (o.kind === 'ok') {
      setText('');
      setMessage({ text: 'Anúncio transmitido a todos os utilizadores ligados.' });
    } else setMessage({ text: ownerProblem(o, 'transmitir o anúncio'), error: true });
  };

  return (
    <section className="owner-card" aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="owner-card__title">
        <Icon name="megaphone" />
        Anúncio de sistema
      </h2>
      <Flash message={message} onDismiss={() => setMessage(undefined)} />
      <div className="form__field">
        <label htmlFor={`${id}-text`} className="sr-only">
          Anúncio
        </label>
        <textarea
          id={`${id}-text`}
          rows={2}
          maxLength={500}
          value={text}
          placeholder="Ex.: O servidor vai ser reiniciado para atualização. Por favor, aguarda."
          onChange={(e) => setText(e.target.value)}
        />
      </div>
      <div className="owner-actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={send} disabled={busy || !text.trim()}>
          {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="megaphone" />}
          {busy ? 'A enviar…' : 'Transmitir a todos'}
        </button>
      </div>
    </section>
  );
}
