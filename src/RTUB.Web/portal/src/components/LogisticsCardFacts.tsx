// Status buttons and priority / date fields of the card workspace side column.
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, PRIORITIES_LOW_FIRST, PRIORITY, STATUS, STATUSES, type Board, type CardDetail, type Priority } from '../data/logisticsApi';
import { day } from '../data/format';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';
import type { Draft } from '../data/logisticsCardDraft';

/** Status buttons of the card workspace side column. */
export function CardStatus({ fid, detail, board, busy, errors, apply }: { fid: string; detail: CardDetail; board: Board; busy?: string; errors: Record<string, string>; apply: Apply }) {
  const card = detail.card;
  // A status is a column; only states the board has columns for are offered.
  const states = STATUSES.filter((s) => board.lists.some((l) => l.state === s));
  return (
      <Section id={`${fid}-status`} title="Estado">
        {detail.canWork ? (
          <div className="lx-segment lx-segment--four" role="group" aria-labelledby={`${fid}-status`}>
            {states.map((s) => (
              <button
                key={s}
                type="button"
                className={`lx-segment--${s.toLowerCase()}`}
                aria-pressed={card.status === s}
                title={`Move o cartão para a coluna ${board.lists.find((l) => l.state === s)?.name ?? ''}`}
                disabled={!!busy}
                onClick={() => card.status !== s && apply(logisticsApi.setStatus(card.id, s), 'status', 'mudar o estado')}
              >
                {STATUS[s].label}
                <small>{STATUS[s].name}</small>
              </button>
            ))}
          </div>
        ) : (
          <p className="lx-view__text">
            {STATUS[card.status].label} · {STATUS[card.status].name}
          </p>
        )}
        <FieldError text={errors.status} />
      </Section>
  );
}

/** Priority, start and due date of the card workspace side column. */
export function CardFacts({ fid, detail, draft, errors, set }: { fid: string; detail: CardDetail; draft: Draft; errors: Record<string, string>; set: (patch: Partial<Draft>) => void }) {
  const card = detail.card;
  return (
      <div className="lx-ws__facts">
        <FormField id={`${fid}-prio`} label="Prioridade" error={errors.priority} alert>
          {detail.canWork ? (
            <span className="control control--select control--sm">
              <select id={`${fid}-prio`} value={draft.priority} onChange={(e) => set({ priority: e.target.value as Priority })}>
                {PRIORITIES_LOW_FIRST.map((p) => (
                  <option key={p} value={p}>
                    {PRIORITY[p]}
                  </option>
                ))}
              </select>
            </span>
          ) : (
            <p id={`${fid}-prio`} className="lx-view__text">
              {PRIORITY[card.priority]}
            </p>
          )}
        </FormField>
        <FormField id={`${fid}-start`} label="Início" error={errors.startDate} alert>
          {detail.canWork ? (
            <input id={`${fid}-start`} type="date" value={draft.startDate} onChange={(e) => set({ startDate: e.target.value })} />
          ) : (
            <p id={`${fid}-start`} className="lx-view__text">
              {day(card.startDate) || '—'}
            </p>
          )}
        </FormField>
        <FormField id={`${fid}-due`} label="Prazo" error={errors.dueDate} alert>
          {detail.canWork ? (
            <input id={`${fid}-due`} type="date" value={draft.dueDate} onChange={(e) => set({ dueDate: e.target.value })} />
          ) : (
            <p id={`${fid}-due`} className="lx-view__text">
              {day(card.dueDate) || '—'}
            </p>
          )}
        </FormField>
      </div>
  );
}
