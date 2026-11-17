// The /profile instruments and newsletter-subscription sections.
import { useId, useState } from 'react';
import type { Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { memberAreaApi, type MyProfile } from '../data/memberAreaApi';
import { type MemberInstrument } from '../data/membersApi';
import { InstrumentPic } from './ProfileTuna';
import { Section, problem } from './ProfileShared';

export function InstrumentsSection({ profile, onChange }: { profile: MyProfile; onChange: (list: MemberInstrument[]) => void }) {
  const id = useId();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const list = profile.instruments;
  const pictures = profile.instrumentPictures;
  const available = profile.instrumentOptions.filter((o) => !list.some((i) => i.instrument === o.value));

  const run = async (action: () => Promise<Outcome<MemberInstrument[]>>) => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data);
      setError(undefined);
    } else setError(problem(o, 'guardar o instrumento'));
  };

  return (
    <Section id="instruments" title="Instrumentos" icon="music">
      {list.length > 0 ? (
        <ul className="instrument-cards" aria-label="Os teus instrumentos">
          {list.map((i) => (
            <li key={i.id} className={i.primary ? 'instrument-card is-primary' : 'instrument-card'}>
              <InstrumentPic url={pictures[i.instrument]} large />
              <span className="instrument-card__name">{i.label}</span>
              <label className="instrument-card__primary">
                <input
                  type="radio"
                  name={`${id}-primary`}
                  checked={i.primary}
                  onChange={() => run(() => memberAreaApi.primaryInstrument(i.id))}
                  disabled={busy}
                />
                {i.primary ? 'Principal' : 'Tornar principal'}
              </label>
              <button
                type="button"
                className="icon-btn icon-btn--danger instrument-card__remove"
                onClick={() => run(() => memberAreaApi.removeInstrument(i.id))}
                disabled={busy}
              >
                <Icon name="trash" />
                <span className="sr-only">Remover {i.label}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="note">Ainda não juntaste nenhum instrumento. O primeiro fica como principal.</p>
      )}
      {available.length > 0 && (
        <div className="instrument-add">
          <h3 id={`${id}-add`} className="tuna-info__title">
            Juntar um instrumento
          </h3>
          <ul aria-labelledby={`${id}-add`}>
            {available.map((o) => (
              <li key={o.value}>
                <button type="button" onClick={() => run(() => memberAreaApi.addInstrument(o.value))} disabled={busy}>
                  <InstrumentPic url={pictures[o.value]} />
                  <span>{o.label}</span>
                  <Icon name="plus" />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </Section>
  );
}

// ---------- Foto (in the header) ----------

export function SubscriptionSection({ profile, onChange }: { profile: MyProfile; onChange: (subscribed: boolean) => void }) {
  const id = useId();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string }>();

  const toggle = async (subscribed: boolean) => {
    setBusy(true);
    setMessage(undefined);
    const o = await memberAreaApi.subscription(subscribed);
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data.subscribed);
      setMessage({ ok: true, text: o.data.subscribed ? 'Vais receber os emails da RTUB.' : 'Deixas de receber os emails da RTUB.' });
    } else setMessage({ ok: false, text: problem(o, 'mudar esta preferência') });
  };

  return (
    <Section id="email" title="Notificações por email" icon="envelope">
      <label className="form__check profile-switch" htmlFor={id}>
        <input id={id} type="checkbox" role="switch" checked={profile.subscribed} onChange={(e) => toggle(e.target.checked)} disabled={busy} />
        Receber por email os avisos da tuna (atuações, cancelamentos e novidades)
      </label>
      {message && (
        <p className={message.ok ? 'profile-saved' : 'form__error'} role="status">
          {message.text}
        </p>
      )}
    </Section>
  );
}

// ---------- Notificações push ----------
