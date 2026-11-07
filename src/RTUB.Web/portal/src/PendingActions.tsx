import { CalendarOfferDialog, eventActivity, rehearsalActivity } from './CalendarOffer';
import type { CalendarActivity } from './calendar';
import { eventsApi } from './eventsApi';
import { rehearsalsApi } from './rehearsalsApi';
import { useCallback, useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { loginTo } from './musicApi';
import {
  pendingActionsApi,
  type ActionButton,
  type ActionRow,
  type Answer,
  type Category,
  type InstrumentOption,
  type Panel,
  type PendingActions as Data,
} from './pendingActionsApi';

const CATEGORIES: { key: Category | 'all'; label: string }[] = [
  { key: 'all', label: 'Tudo' },
  { key: 'participation', label: 'Participação' },
  { key: 'tasks', label: 'Tarefas' },
  { key: 'responsibilities', label: 'Responsabilidades' },
];

// Each module's own note limit (EventAgendaService.MaxNotesLength, RehearsalAgendaService.NotesMax); the server checks it too.
const NOTES_MAX: Record<string, number> = { 'event.response': 1000, 'rehearsal.response': 500 };

const EMPTY_PANEL: Record<string, string> = {
  logistics: 'Não tens cartões por concluir.',
  questions: 'Não tens questões abertas.',
  secretary: 'Nada por criar nas pastas deste ano.',
  meetings: 'Todas as reuniões passadas têm ata.',
  approvals: 'Nenhuma presença por aprovar.',
  repertoire: 'As próximas atuações têm repertório.',
  members: 'Nenhum membro com informação em falta.',
};

/** What a saved command says (072 answers: the default). */
const DONE: Record<string, string> = {
  done: 'Cartão concluído.',
  close: 'Questão fechada.',
  create: 'Criado.',
  none: 'Marcado sem movimentos.',
  reopen: 'Reaberto: volta a «Por classificar».',
  approve: 'Presença aprovada.',
  refuse: 'Presença recusada.',
};

const EMPTY: Record<string, string> = {
  upcoming: 'Nada por responder nas próximas.',
  past: 'Nada por responder neste ano.',
  open: 'Todos os eventos do ano estão classificados.',
  none: 'Nenhum evento marcado sem movimentos.',
};

/** "yyyy-mm-dd" as a calendar day (no time zone shift). */
const asDay = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
};

const longDate = (iso: string) =>
  new Intl.DateTimeFormat('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(asDay(iso));

const rowDate = (iso: string, today: string) =>
  new Intl.DateTimeFormat('pt-PT', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    ...(iso.slice(0, 4) !== today.slice(0, 4) ? { year: 'numeric' } : {}),
  }).format(asDay(iso));

/**
 * /pending-actions - "As minhas ações pendentes" (task 071, docs/react-pending-actions.md). A page of grouped actions, not
 * an inbox: one panel per module, each paged on its own (three rows, previous / next), with the action buttons on the
 * row (072: Vou / Não vou, Fui / Não fui answered in place). The server decides which panels, rows and buttons a member gets. Coming back to the page (back button, PWA
 * resume) asks every panel again for the page it shows, so something answered elsewhere disappears.
 */
export default function PendingActions() {
  const [data, setData] = useState<Data | 'signin' | null>();
  const [category, setCategory] = useState<Category | 'all'>('all');
  const [resumed, setResumed] = useState(0);

  const load = useCallback(
    () => pendingActionsApi.page().then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null)),
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
            {page.status === 'retired' && <span className="badge pa-status pa-status--retired">Reformado</span>}
            <time className="pa-today" dateTime={page.today}>
              <Icon name="calendar" />
              {longDate(page.today)}
            </time>
          </div>
        )}
      </header>

      {data === 'signin' ? (
        <div className="notice" role="status">
          <p>As ações pendentes são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo('/pending-actions')}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
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
          {page.status === 'retired' && (
            <p className="note">As atuações e os ensaios por responder aparecem aqui enquanto estiveres no ativo. Podes sempre responder na agenda.</p>
          )}
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

type PanelState = { panel: Panel; busy: boolean; failed: boolean; notice: string | null };

/**
 * One panel with its own filter and page. Every answer from the server replaces the panel (rows, counts, and the page,
 * already clamped there: an emptied last page shows the one before it). A response that arrives after a newer request
 * left is dropped, so fast clicking never shows an older page.
 */
function ActionPanel({
  initial,
  today,
  resumed,
  instruments,
  defaultInstrument,
}: {
  initial: Panel;
  today: string;
  resumed: number;
  instruments: InstrumentOption[];
  defaultInstrument: string | null;
}) {
  const [state, setState] = useState<PanelState>({ panel: initial, busy: false, failed: false, notice: null });
  const [asking, setAsking] = useState<{ row: ActionRow; button: ActionButton } | null>(null);
  const [confirming, setConfirming] = useState<{ row: ActionRow; button: ActionButton } | null>(null);
  const [calendar, setCalendar] = useState<{ activity: CalendarActivity; headline: string } | null>(null);
  const latest = useRef(0);
  const heading = useRef<HTMLHeadingElement>(null);
  const { panel } = state;
  const titleId = `pa-${panel.key}-title`;

  const go = useCallback((filter: string | null, page: number, notice: string | null = null) => {
    const ticket = ++latest.current;
    setState((s) => ({ ...s, busy: true, failed: false }));
    return pendingActionsApi.panel(initial.key, filter, page).then((o) => {
      if (ticket !== latest.current) return;
      setState((s) =>
        o.kind === 'ok' ? { panel: o.data, busy: false, failed: false, notice } : { ...s, busy: false, failed: true, notice: null },
      );
    });
  }, [initial.key]);

  // Coming back to the page: the same filter and page, asked again (the server clamps it if rows went away).
  useEffect(() => {
    if (resumed > 0) go(state.panel.filter, state.panel.page);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resumed]);

  const dismiss = async (row: ActionRow) => {
    setState((s) => ({ ...s, busy: true, notice: null }));
    const o = await pendingActionsApi.dismiss(row);
    if (o.kind === 'forbidden') {
      setState((s) => ({ ...s, busy: false, notice: 'Esta ação é obrigatória: não pode ser dispensada.' }));
      return;
    }
    if (o.kind !== 'ok' && o.kind !== 'notfound') {
      setState((s) => ({ ...s, busy: false, notice: 'Não foi possível guardar. Tenta de novo.' }));
      return;
    }
    // Saved (or already gone): the same page again, in place; focus stays in this panel, the page does not move.
    await go(panel.filter, panel.page, 'Dispensado.');
    heading.current?.focus({ preventScroll: true });
  };

  /**
   * An answer (072). Nothing changes on the page until the server says so: saved → the same page again, in place (the
   * row leaves, the counts drop, an emptied last page shows the one before); already answered or changed meanwhile →
   * the page again, with the reason; anything else → the row stays, with the error. Returns the field error, if any,
   * for the instrument dialog.
   */
  const respond = async (row: ActionRow, a: Answer): Promise<string | null> => {
    setState((s) => ({ ...s, busy: true, notice: null, failed: false }));
    const o = await pendingActionsApi.respond(row, a);
    if (o.kind === 'invalid') {
      setState((s) => ({ ...s, busy: false }));
      return Object.values(o.errors)[0] ?? 'Há campos por corrigir.';
    }
    if (o.kind === 'ok' || o.kind === 'notfound' || o.kind === 'conflict' || o.kind === 'closed') {
      const upcoming = panel.filter === 'upcoming';
      await go(panel.filter, panel.page, o.kind === 'ok' ? DONE[a.answer] ?? 'Resposta guardada.' : 'Isto já foi tratado ou mudou entretanto.');
      heading.current?.focus({ preventScroll: true });
      if (o.kind === 'ok' && a.answer === 'yes' && upcoming) offerCalendar(row);
      return null;
    }
    setState((s) => ({
      ...s,
      busy: false,
      notice:
        o.kind === 'signin'
          ? 'A sessão terminou. Entra de novo para responder.'
          : o.kind === 'forbidden'
            ? 'Já não tens permissão para isto.'
            : 'Não foi possível guardar. Verifica a ligação e tenta de novo.',
    }));
    return null;
  };

  /**
   * Task 079: after a saved "Vou" (upcoming only, never "Fui"), the module's own read of the answer gives the calendar
   * details; the offer opens only when that read confirms the member is going to an open, upcoming activity. A failed
   * read just skips the offer: the answer is saved either way.
   */
  const offerCalendar = async (row: ActionRow) => {
    const id = Number(row.sourceKey);
    if (row.kind === 'event.response') {
      const o = await eventsApi.getEnrollment(id);
      if (o.kind === 'ok' && o.data.status === 'going' && o.data.state === 'open' && !o.data.event.past && !o.data.event.cancelled) {
        setCalendar({ activity: eventActivity(o.data.event), headline: 'Inscrição guardada.' });
      }
    } else if (row.kind === 'rehearsal.response') {
      const o = await rehearsalsApi.myAttendance(id);
      if (o.kind === 'ok' && o.data.status === 'pending' && o.data.state === 'open' && !o.data.rehearsal.past && !o.data.rehearsal.cancelled) {
        setCalendar({ activity: rehearsalActivity(o.data.rehearsal), headline: 'Presença registada, pendente de aprovação.' });
      }
    }
  };

  // Vou / Fui asks for the instrument only when there is a choice; otherwise it is sent at once with the primary.
  const answer = (row: ActionRow, button: ActionButton) => {
    if (button.confirm) setConfirming({ row, button });
    else if (button.answer === 'yes' && instruments.length > 1) setAsking({ row, button });
    else respond(row, { answer: button.answer!, instrument: button.answer === 'yes' ? defaultInstrument : null });
  };

  const last = Math.max(1, Math.ceil(panel.total / panel.pageSize));
  const from = panel.total === 0 ? 0 : (panel.page - 1) * panel.pageSize + 1;
  const to = Math.min(panel.total, panel.page * panel.pageSize);
  const pending = panel.filters.reduce((n, f) => n + f.count, 0) || panel.total;

  return (
    <section className="pa-panel" aria-labelledby={titleId} aria-busy={state.busy}>
      <header className="pa-panel__head">
        <h2 id={titleId} ref={heading} tabIndex={-1} className="pa-panel__title">
          {panel.title}
        </h2>
        <span className="pa-count" aria-label={`${pending} por tratar`}>
          {pending}
        </span>
      </header>

      {panel.filters.length > 0 && (
        <div className="pa-tabs" role="group" aria-label={`Filtrar ${panel.title.toLowerCase()}`}>
          {panel.filters.map((f) => (
            <button
              key={f.key}
              type="button"
              aria-pressed={panel.filter === f.key}
              disabled={state.busy && panel.filter !== f.key}
              onClick={() => panel.filter !== f.key && go(f.key, 1)}
            >
              {f.label} <small>{f.count}</small>
            </button>
          ))}
        </div>
      )}

      <div className={`pa-rows${state.busy ? ' is-busy' : ''}`}>
        {panel.rows.length > 0 ? (
          <ul className="pa-list">
            {panel.rows.map((row) => (
              <Row key={`${row.kind}:${row.sourceKey}`} row={row} today={today} busy={state.busy} onDismiss={dismiss} onAnswer={answer} />
            ))}
          </ul>
        ) : (
          <p className="pa-empty">
            <Icon name="check" />
            {EMPTY[panel.filter ?? ''] ?? EMPTY_PANEL[panel.key] ?? 'Nada por tratar.'}
          </p>
        )}
      </div>

      {(state.failed || state.notice) && (
        <p className={state.failed ? 'pa-alert' : 'pa-notice'} role={state.failed ? 'alert' : 'status'}>
          {state.failed ? 'Não foi possível atualizar. Verifica a ligação e tenta de novo.' : state.notice}
        </p>
      )}

      <footer className="pa-pager">
        <button
          type="button"
          className="icon-btn pa-pager__btn"
          disabled={panel.page <= 1 || state.busy}
          onClick={() => go(panel.filter, panel.page - 1)}
        >
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior de {panel.title}</span>
        </button>
        <span className="pa-pager__range" aria-live="polite">
          {panel.total === 0 ? '0' : `${from}–${to}`} de {panel.total}
          {last > 1 && <span className="pa-pager__pages"> · página {panel.page} de {last}</span>}
        </span>
        <button
          type="button"
          className="icon-btn pa-pager__btn"
          disabled={panel.page >= last || state.busy}
          onClick={() => go(panel.filter, panel.page + 1)}
        >
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte de {panel.title}</span>
        </button>
      </footer>

      {calendar && <CalendarOfferDialog activity={calendar.activity} headline={calendar.headline} onClose={() => setCalendar(null)} />}
      {confirming && (
        <Dialog
          title={confirming.row.title}
          onClose={() => setConfirming(null)}
          footer={
            <>
              <button type="button" className="btn btn--ghost" onClick={() => setConfirming(null)} disabled={state.busy}>
                Cancelar
              </button>
              <button
                type="button"
                className="btn btn--primary"
                disabled={state.busy}
                onClick={async () => {
                  const { row, button } = confirming;
                  await respond(row, { answer: button.answer! });
                  setConfirming(null);
                  requestAnimationFrame(() => heading.current?.focus({ preventScroll: true }));
                }}
              >
                {confirming.button.confirmLabel ?? confirming.button.label}
              </button>
            </>
          }
        >
          <p>{confirming.button.confirm}</p>
        </Dialog>
      )}

      {asking && (
        <AnswerDialog
          row={asking.row}
          label={asking.button.label}
          instruments={instruments}
          defaultInstrument={defaultInstrument}
          onClose={() => setAsking(null)}
          onSubmit={async (a) => {
            const error = await respond(asking.row, a);
            if (!error) {
              // The dialog gives focus back to the button it came from, which has just left the page: keep it in this panel.
              setAsking(null);
              requestAnimationFrame(() => heading.current?.focus({ preventScroll: true }));
            }
            return error;
          }}
        />
      )}
    </section>
  );
}

/** Vou / Fui with a choice of instrument (and an optional note), as on the module pages. Cancelling sends nothing. */
function AnswerDialog({
  row,
  label,
  instruments,
  defaultInstrument,
  onClose,
  onSubmit,
}: {
  row: ActionRow;
  label: string;
  instruments: InstrumentOption[];
  defaultInstrument: string | null;
  onClose: () => void;
  onSubmit: (a: Answer) => Promise<string | null>;
}) {
  const [instrument, setInstrument] = useState(defaultInstrument ?? '');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const ids = { instrument: useId(), notes: useId() };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const failed = await onSubmit({ answer: 'yes', instrument: instrument || null, notes: notes.trim() || null });
    setBusy(false);
    setError(failed);
  };

  return (
    <Dialog title={`${label}: ${row.title}`} onClose={onClose}>
      <form className="answer" onSubmit={submit}>
        <div className="form__field">
          <label htmlFor={ids.instrument}>Instrumento</label>
          <span className="control control--select">
            <select id={ids.instrument} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
              <option value="">Sem instrumento</option>
              {instruments.map((i) => (
                <option key={i.value} value={i.value}>
                  {i.label}
                </option>
              ))}
            </select>
          </span>
        </div>
        <div className="form__field">
          <label htmlFor={ids.notes}>Nota (opcional)</label>
          <textarea id={ids.notes} rows={2} maxLength={NOTES_MAX[row.kind] ?? 500} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        {error && (
          <p className="form__error" role="alert">
            {error}
          </p>
        )}
        <div className="answer__actions">
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Confirmar
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function Row({
  row,
  today,
  busy,
  onDismiss,
  onAnswer,
}: {
  row: ActionRow;
  today: string;
  busy: boolean;
  onDismiss: (row: ActionRow) => void;
  onAnswer: (row: ActionRow, button: ActionButton) => void;
}) {
  return (
    <li className="pa-row">
      <div className="pa-row__text">
        <strong className="pa-row__title">
          {row.href ? <a href={row.href}>{row.title}</a> : row.title}
          {row.required && <span className="pa-required">Obrigatório</span>}
        </strong>
        {(row.date || row.context) && (
          <span className="pa-row__meta">
            {row.date && (
              <time dateTime={row.date}>
                {rowDate(row.date, today)}
                {row.time && ` · ${row.time}`}
              </time>
            )}
            {row.context && <span>{row.context}</span>}
          </span>
        )}
      </div>
      <div className="pa-row__actions">
        {row.actions.map((a) =>
          a.answer ? (
            <button
              key={a.answer}
              type="button"
              className={`btn btn--sm ${a.primary ? 'btn--primary' : 'btn--ghost'}`}
              disabled={busy}
              aria-label={`${a.label}: ${row.title}`}
              onClick={() => onAnswer(row, a)}
            >
              {a.label}
            </button>
          ) : (
            <a key={a.href} className={`btn btn--sm ${a.primary ? 'btn--primary' : 'btn--ghost'}`} href={a.href!}>
              {a.label}
            </a>
          ),
        )}
        {row.dismissible && !row.required && (
          <button type="button" className="btn btn--sm btn--ghost" disabled={busy} onClick={() => onDismiss(row)}>
            {row.dismissLabel ?? 'Agora não'}
          </button>
        )}
      </div>
    </li>
  );
}
