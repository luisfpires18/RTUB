import { useEffect, useId, useState, type ReactNode } from 'react';
import type { Outcome } from '../data/eventsApi';
import { Icon, type IconName } from './icons';
import { portal } from '../data/content';
import { Notice, SignInNotice } from './Notice';
import { Loading } from './ui/Loading';
import { EmptyState } from './ui/EmptyState';
import { PageHead } from './ui/PageHead';
import { IconAction } from './ui/IconAction';

/** What an Owner page shows: the data, a sign-in prompt, the refusal, or a failure (null). */
type Loaded<T> = T | 'signin' | 'forbidden' | null | undefined;

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
 * The frame of every Owner tool: heading, then the content once loaded. Anyone but the Owner gets the
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
        <EmptyState
          icon="lock"
          title="Acesso Restrito"
          titleId={titleId}
          heading
          className="mtg-refusal"
          role="status"
          actions={
            <a className="btn btn--ghost btn--sm" href={portal.profile}>
              Voltar ao perfil
            </a>
          }
        >
          <p>Esta página é reservada ao Owner.</p>
        </EmptyState>
      </section>
    );
  }

  return (
    <section className="page wrap events-page owner-page" aria-labelledby={titleId}>
      <PageHead
        eyebrow="Owner"
        titleId={titleId}
        titleClassName="owner-page__title"
        title={
          <>
            <Icon name={icon} />
            {title}
          </>
        }
        lead={lead}
      >
        {actions && state && typeof state === 'object' && <div className="owner-page__actions">{actions}</div>}
      </PageHead>

      {state === undefined ? (
        <Loading label={loadingLabel} />
      ) : state === 'signin' ? (
        <SignInNotice path={path}>Esta página é reservada ao Owner. Entra primeiro.</SignInNotice>
      ) : state === null ? (
        <Notice>Não foi possível carregar esta página. Tenta outra vez daqui a pouco.</Notice>
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
      <IconAction icon="close" label="Fechar a mensagem" onClick={onDismiss} />
    </div>
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
