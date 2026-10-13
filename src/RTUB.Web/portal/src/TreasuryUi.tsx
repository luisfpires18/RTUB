import { useEffect, useId, useState, type ReactNode } from 'react';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { loginTo } from './musicApi';
import { treasuryApi, type Person } from './treasuryApi';

// Shared pieces of the React /treasury pages (React track 024).

export const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** The four Tesouraria areas, as the old report page's buttons ("Ver Calotes", "Ver MBWAY", "Nerba"). */
export function TreasuryTabs({ current, fiscalYear }: { current: 'reports' | 'calotes' | 'mbway' | 'nerba'; fiscalYear?: string }) {
  const tabs = [
    { key: 'reports', label: 'Relatórios', href: portal.treasury },
    { key: 'calotes', label: 'Calotes', href: fiscalYear ? `${portal.treasuryCalotes}?fy=${encodeURIComponent(fiscalYear)}` : portal.treasuryCalotes },
    { key: 'mbway', label: 'MBWAY', href: portal.treasuryMbway },
    { key: 'nerba', label: 'Nerba', href: portal.treasuryNerba },
  ];
  return (
    <nav className="tr-tabs" aria-label="Tesouraria">
      {tabs.map((t) => (
        <a key={t.key} href={t.href} className="tr-tabs__tab" aria-current={t.key === current ? 'page' : undefined}>
          {t.label}
        </a>
      ))}
    </nav>
  );
}

/** What a page shows when it cannot show its data: sign in, not for you, failed. */
export function Refusal({ state, path, onRetry }: { state: 'signin' | 'forbidden' | 'failed' | 'missing'; path: string; onRetry: () => void }) {
  if (state === 'signin') {
    return (
      <div className="notice" role="status">
        <p>A tesouraria é da área de membros.</p>
        <a className="btn btn--primary btn--sm" href={loginTo(path)}>
          <Icon name="login" />
          Entrar
        </a>
      </div>
    );
  }
  if (state === 'forbidden') {
    return (
      <div className="notice" role="status">
        <p>A tesouraria não está disponível para Caloiros e Leitões. Os teus calotes estão em Calotes.</p>
        <a className="btn btn--ghost btn--sm" href={portal.treasuryCalotes}>
          Os meus calotes
        </a>
      </div>
    );
  }
  if (state === 'missing') {
    return (
      <div className="notice" role="status">
        <p>Isto não existe ou já foi apagado.</p>
      </div>
    );
  }
  return (
    <div className="notice" role="status">
      <p>Não conseguimos abrir a tesouraria agora.</p>
      <button type="button" className="btn btn--ghost btn--sm" onClick={onRetry}>
        Tentar novamente
      </button>
    </div>
  );
}

/** Search members (managers only): names and avatars; the server also matches e-mail, never returns it. */
export function MemberPicker({ forDebts, label, onPick }: { forDebts: boolean; label: string; onPick: (m: Person) => void }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Person[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => treasuryApi.members(query.trim(), forDebts).then((o) => setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => clearTimeout(timer);
  }, [query, forDebts]);

  return (
    <div className="lx-picker">
      <label className="sr-only" htmlFor={id}>
        {label}
      </label>
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha, nome ou e-mail" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {found && found.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {found && found.length > 0 && (
        <ul className="talk__picks">
          {found.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  onPick(m);
                  setQuery('');
                }}
              >
                <img src={m.avatarUrl ?? DEFAULT_AVATAR} alt="" width="28" height="28" loading="lazy" />
                <span>
                  {m.displayName}
                  {m.fullName && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export const PersonChip = ({ person, onRemove }: { person: Person; onRemove?: () => void }) => (
  <span className="chip chip--removable">
    <img src={person.avatarUrl ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
    {person.displayName}
    {onRemove && (
      <button type="button" className="chip__remove" onClick={onRemove}>
        <Icon name="close" />
        <span className="sr-only">Escolher outro membro</span>
      </button>
    )}
  </span>
);

/** A yes / no question with a danger (or primary) action; the action returns an error message or nothing. */
export function Confirm({
  title,
  action,
  danger = true,
  onClose,
  onConfirm,
  children,
}: {
  title: string;
  action: string;
  danger?: boolean;
  onClose: () => void;
  onConfirm: () => Promise<string | void>;
  children: ReactNode;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const run = async () => {
    setBusy(true);
    const failed = await onConfirm();
    setBusy(false);
    if (failed) setError(failed);
    else onClose();
  };
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className={danger ? 'btn btn--danger' : 'btn btn--primary'} onClick={run} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {action}
          </button>
        </>
      }
    >
      {children}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

/** Form field with its error under it. */
export function Field({ id, label, error, children }: { id: string; label: string; error?: string; children: ReactNode }) {
  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={id}>{label}</label>
      {children}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

/** Dialog footer: Cancelar + a submit button for the form `formId`. */
export const SaveFooter = ({ formId, busy, disabled, onClose, label = 'Guardar' }: { formId: string; busy: boolean; disabled?: boolean; onClose: () => void; label?: string }) => (
  <>
    <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
      Cancelar
    </button>
    <button type="submit" form={formId} className="btn btn--primary" disabled={busy || disabled}>
      {busy && <span className="spinner spinner--small" aria-hidden="true" />}
      {label}
    </button>
  </>
);
