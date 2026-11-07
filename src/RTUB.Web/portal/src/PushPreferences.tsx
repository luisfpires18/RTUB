import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { pushApi, type PushPreference } from './push';

/**
 * "O que receber" (task 082, issue #263): the member's account-wide push preferences, from /profile → Notificações
 * push. Separate from this device's ON / OFF above it: a key off here mutes that push on every device, the copy in
 * Mensagens → Notificações RTUB is still kept. The list, labels and the locked key come from the server
 * (/api/push/preferences); saving sends the full set of keys that are off and never touches the device subscription.
 */
export function PushPreferencesDialog({ onClose }: { onClose: () => void }) {
  const [items, setItems] = useState<PushPreference[] | 'failed'>();
  const [disabled, setDisabled] = useState<Set<string>>(new Set());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [saved, setSaved] = useState(false);
  const id = useId();

  const load = () => {
    setItems(undefined);
    setError(undefined);
    pushApi.preferences().then((o) => {
      if (o.kind !== 'ok') return setItems('failed');
      setItems(o.data.items);
      setDisabled(new Set(o.data.items.filter((i) => !i.enabled).map((i) => i.key)));
    });
  };
  useEffect(load, []);

  const toggle = (key: string, on: boolean) => {
    setSaved(false);
    setDisabled((d) => {
      const next = new Set(d);
      if (on) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    setSaved(false);
    const o = await pushApi.savePreferences([...disabled]);
    setBusy(false);
    if (o.kind === 'ok') {
      setItems(o.data.items);
      setDisabled(new Set(o.data.items.filter((i) => !i.enabled).map((i) => i.key)));
      setSaved(true);
    } else setError(failure(o));
  };

  const groups = Array.isArray(items) ? [...new Set(items.map((i) => i.group))] : [];

  return (
    <Dialog title="O que receber" onClose={onClose} className="push-prefs">
      <p className="note">
        Vale para a tua conta, em todos os dispositivos. Desligar um tipo pára o aviso no telemóvel e no computador; a
        cópia em Mensagens → Notificações RTUB fica na mesma. Para receber algo, as notificações têm de estar ligadas
        neste dispositivo.
      </p>
      {items === undefined ? (
        <Loading label="A carregar as preferências…" />
      ) : items === 'failed' ? (
        <div className="push-state" role="status">
          <p>Não foi possível ver as preferências.</p>
          <div className="push-actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
              <Icon name="restore" />
              Tentar outra vez
            </button>
          </div>
        </div>
      ) : (
        <form className="form push-prefs__form" onSubmit={submit} noValidate>
          {groups.map((group) => (
            <fieldset key={group} className="push-prefs__group">
              <legend className="push-prefs__legend">{group}</legend>
              {items
                .filter((i) => i.group === group)
                .map((i) => {
                  const inputId = `${id}-${i.key}`;
                  return (
                    <label key={i.key} className="form__check push-prefs__item" htmlFor={inputId}>
                      <input
                        id={inputId}
                        type="checkbox"
                        checked={i.mandatory || !disabled.has(i.key)}
                        disabled={busy || i.mandatory}
                        aria-disabled={i.mandatory || undefined}
                        onChange={(e) => toggle(i.key, e.target.checked)}
                      />
                      <span className="push-prefs__text">
                        <span className="push-prefs__label">
                          {i.label}
                          {i.mandatory && <span className="push-prefs__locked">Obrigatório</span>}
                        </span>
                        {i.help && <span className="note">{i.help}</span>}
                      </span>
                    </label>
                  );
                })}
            </fieldset>
          ))}
          {saved && (
            <p className="profile-saved" role="status">
              <Icon name="check" />
              Preferências guardadas.
            </p>
          )}
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
          <div className="push-actions push-prefs__actions">
            <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
              <Icon name="check" />
              {busy ? 'A guardar…' : 'Guardar'}
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={onClose} disabled={busy}>
              Fechar
            </button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

function failure(o: Outcome<unknown>): string {
  if (o.kind === 'invalid') return Object.values(o.errors)[0] ?? 'Há campos por corrigir.';
  if (o.kind === 'signin') return 'A sessão terminou. Entra outra vez.';
  return 'Não foi possível guardar. Tenta outra vez daqui a pouco.';
}
