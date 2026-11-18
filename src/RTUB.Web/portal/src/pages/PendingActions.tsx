import { useCallback, useEffect, useState } from 'react';
import { Icon } from '../components/icons';
import { pendingActionsApi, type Category, type PendingActions as Data } from '../data/pendingActionsApi';
import { SignInNotice } from '../components/Notice';
import { CATEGORIES, longDate } from '../data/pendingActionsView';
import { ActionPanel } from '../components/PendingActionPanel';
import { Loading } from '../components/ui/Loading';

/**
 * /pending-actions - "As minhas ações pendentes" (docs/react-pending-actions.md). A page of grouped actions, not
 * an inbox: one panel per module, each paged on its own (three rows, previous / next), with the action buttons on the
 * row (Vou / Não vou, Fui / Não fui answered in place). The server decides which panels, rows and buttons a member gets. Coming back to the page (back button, PWA
 * resume) asks every panel again for the page it shows, so something answered elsewhere disappears.
 */
export default function PendingActions() {
  const [data, setData] = useState<Data | 'signin' | null>();
  const [category, setCategory] = useState<Category | 'all'>('all');
  const [resumed, setResumed] = useState(0);

  const load = useCallback(
    () =>
      pendingActionsApi.page().then((o) => {
        // The page is not for a Reformado (404, and no menu entry): the agenda is where they answer.
        if (o.kind === 'notfound') return window.location.replace('/events');
        setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null);
      }),
    [],
  );

  useEffect(() => {
    document.title = 'As minhas ações pendentes · RTUB';
    load();
    // bfcache restore (back from an event page) or the installed app coming back: re-check every panel in place.
    const again = (e: Event) => {
      if (e.type === 'pageshow' ? (e as PageTransitionEvent).persisted : document.visibilityState === 'visible') setResumed((n) => n + 1);
    };
    window.addEventListener('pageshow', again);
    document.addEventListener('visibilitychange', again);
    return () => {
      window.removeEventListener('pageshow', again);
      document.removeEventListener('visibilitychange', again);
    };
  }, [load]);

  const page = data && typeof data === 'object' ? data : null;
  const categories = page ? CATEGORIES.filter((c) => c.key === 'all' || page.panels.some((p) => p.category === c.key)) : [];
  const shown = page ? page.panels.filter((p) => category === 'all' || p.category === category) : [];

  return (
    <section className="page wrap pa-page" aria-labelledby="pa-title">
      <header className="pa-head">
        <div className="pa-head__text">
          <p className="eyebrow">Área de membros</p>
          <h1 id="pa-title" className="page__title pa-head__title">
            As minhas ações pendentes
          </h1>
          <p className="page__lead">Responde às atividades e trata do que está por fazer.</p>
        </div>
        {page && (
          <div className="pa-head__meta">
            {page.status === 'active' && <span className="badge pa-status">No ativo</span>}
            <time className="pa-today" dateTime={page.today}>
              <Icon name="calendar" />
              {longDate(page.today)}
            </time>
          </div>
        )}
      </header>

      {data === 'signin' ? (
        <SignInNotice path={'/pending-actions'}>As ações pendentes são da área de membros.</SignInNotice>
      ) : data === null ? (
        <div className="notice" role="alert">
          <p>Não conseguimos abrir as tuas ações pendentes agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setData(undefined), load())}>
            Tentar novamente
          </button>
        </div>
      ) : !page ? (
        <Loading label="A carregar as ações pendentes…" />
      ) : page.panels.length === 0 ? (
        <div className="pa-none">
          <Icon name="checkCircle" />
          <p>Não tens ações pendentes.</p>
          <a className="btn btn--ghost btn--sm" href="/events">
            Ver a agenda
          </a>
        </div>
      ) : (
        <>
          {categories.length > 2 && (
            <div className="lx-filters pa-categories" role="group" aria-label="Mostrar">
              {categories.map((c) => (
                <button key={c.key} type="button" aria-pressed={category === c.key} onClick={() => setCategory(c.key)}>
                  {c.label}
                </button>
              ))}
            </div>
          )}
          <div className="pa-grid">
            {shown.map((p) => (
              <ActionPanel key={p.key} initial={p} today={page.today} resumed={resumed} instruments={page.instruments} defaultInstrument={page.defaultInstrument} />
            ))}
          </div>
        </>
      )}
    </section>
  );
}
