import { useEffect, useId, useState, type ReactNode } from 'react';
import { Loading } from './App';
import type { Outcome } from './eventsApi';
import { Icon, type IconName } from './icons';
import { loginTo } from './musicApi';
import { portal } from './content';

/** What an Owner page shows: the data, a sign-in prompt, the refusal, or a failure (null). */
export type Loaded<T> = T | 'signin' | 'forbidden' | null | undefined;

/** Loads on mount and whenever `deps` change; `reload()` asks again. */
export function useOwnerLoad<T>(load: () => Promise<Outcome<T>>, deps: unknown[]): [Loaded<T>, () => void, (next: T) => void] {
  const [state, setState] = useState<Loaded<T>>();
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let live = true;
    load().then((o) => {
      if (live) setState(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null);
    });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version]);

  return [state, () => setVersion((v) => v + 1), setState];
}

/**
 * The frame of every Owner tool (task 035): heading, then the content once loaded. Anyone but the Owner gets the
 * refusal (the server already answered them 403); a visitor gets the sign-in prompt.
 */
export function OwnerPage<T>({
  path,
  title,
  lead,
  icon,
  state,
  loadingLabel,
  actions,
  children,
}: {
  path: string;
  title: string;
  lead: string;
  icon: IconName;
  state: Loaded<T>;
  loadingLabel: string;
  actions?: ReactNode;
  children: (data: T) => ReactNode;
}) {
  const titleId = useId();

  useEffect(() => {
    document.title = `${title} · RTUB`;
  }, [title]);

  if (state === 'forbidden') {
    return (
      <section className="page wrap owner-page" aria-labelledby={titleId}>
        <div className="state mtg-refusal" role="status">
          <Icon name="lock" className="state__icon" />
          <h1 id={titleId} className="state__title">
            Acesso Restrito
          </h1>
          <p>Esta página é reservada ao Owner.</p>
          <div className="state__actions">
            <a className="btn btn--ghost btn--sm" href={portal.profile}>
              Voltar ao perfil
            </a>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="page wrap events-page owner-page" aria-labelledby={titleId}>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Owner</p>
          <h1 id={titleId} className="page__title owner-page__title">
            <Icon name={icon} />
            {title}
          </h1>
          <p className="page__lead">{lead}</p>
        </div>
        {actions && state && typeof state === 'object' && <div className="owner-page__actions">{actions}</div>}
      </header>

      {state === undefined ? (
        <Loading label={loadingLabel} />
      ) : state === 'signin' ? (
        <div className="notice" role="status">
          <p>Esta página é reservada ao Owner. Entra primeiro.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(path)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : state === null ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar esta página. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        children(state)
      )}
    </section>
  );
}

/** A result line under a form: green when it worked, red when it did not. Announced to screen readers. */
export function Flash({ message, onDismiss }: { message?: { text: string; error?: boolean }; onDismiss: () => void }) {
  if (!message) return null;
  return (
    <div className={message.error ? 'owner-flash owner-flash--error' : 'owner-flash'} role={message.error ? 'alert' : 'status'}>
      <Icon name={message.error ? 'warning' : 'checkCircle'} />
      <p>{message.text}</p>
      <button type="button" className="icon-btn icon-btn--sm" onClick={onDismiss}>
        <Icon name="close" />
        <span className="sr-only">Fechar a mensagem</span>
      </button>
    </div>
  );
}

/** Pages of a list, with the page sizes the old page offered. */
export function OwnerPager({
  total,
  itemLabel,
  page,
  pageSize,
  sizes,
  onPage,
  onPageSize,
}: {
  total: number;
  itemLabel: string;
  page: number;
  pageSize: number;
  sizes: number[];
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <nav className="mtg-pager" aria-label={`Páginas de ${itemLabel}`}>
      <span className="note">
        {total} {itemLabel}
      </span>
      <span className="mtg-pager__nav">
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className="mtg-pager__size">
        Por página
        <span className="control control--select control--sm">
          <select id={id} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {sizes.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </span>
      </label>
    </nav>
  );
}

/** A search box that filters as you type. */
export function SearchBox({ label, placeholder, value, onChange }: { label: string; placeholder: string; value: string; onChange: (v: string) => void }) {
  const id = useId();
  return (
    <label className="control owner-search" htmlFor={id}>
      <span className="sr-only">{label}</span>
      <Icon name="search" />
      <input id={id} type="search" placeholder={placeholder} value={value} onChange={(e) => onChange(e.target.value)} />
    </label>
  );
}

/** "texto" contains the search, ignoring case and accents. */
export const matches = (q: string, ...fields: (string | null | undefined)[]) => {
  const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
  const needle = fold(q.trim());
  return !needle || fields.some((f) => f && fold(f).includes(needle));
};
