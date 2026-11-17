// One pending-actions panel with its own filter and page.
import { CalendarOfferDialog, eventActivity, rehearsalActivity } from './CalendarOffer';
import { type CalendarActivity } from '../data/calendar';
import { eventsApi } from '../data/eventsApi';
import { rehearsalsApi } from '../data/rehearsalsApi';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { pendingActionsApi, type ActionButton, type ActionRow, type Answer, type InstrumentOption, type Panel } from '../data/pendingActionsApi';
import { EMPTY_PANEL, DONE, EMPTY } from '../data/pendingActionsView';
import { AnswerDialog, Row } from './PendingActionRows';

type PanelState = { panel: Panel; busy: boolean; failed: boolean; notice: string | null };

/**
 * One panel with its own filter and page. Every answer from the server replaces the panel (rows, counts, and the page,
 * already clamped there: an emptied last page shows the one before it). A response that arrives after a newer request
 * left is dropped, so fast clicking never shows an older page.
 */
export function ActionPanel({
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
