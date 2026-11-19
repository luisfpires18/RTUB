// Cards linked to this card panel.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, type Board, type CardDetail } from '../data/logisticsApi';
import { FieldError } from './ui/FieldError';
import { EmptyNote } from './ui/EmptyNote';
import { IconAction } from './ui/IconAction';

/** Links to other cards of this board ("Cartões ligados"), distinct from the attachments. */
export function LinkedCards({ detail, board, apply, errors }: { detail: CardDetail; board: Board; apply: Apply; errors: Record<string, string> }) {
  const [pick, setPick] = useState('');
  const id = useId();
  const cardId = detail.card.id;
  const linked = detail.links.map((l) => l.cardId).filter((x): x is number => x !== null);
  const others = board.lists.flatMap((l) => l.cards.map((c) => ({ ...c, list: l.name }))).filter((c) => c.id !== cardId && !linked.includes(c.id));
  if (!detail.canManage && detail.links.length === 0) return null;

  return (
    <Section id={`${id}-links`} title="Cartões ligados">
      {detail.links.length > 0 ? (
        <ul className="lx-files">
          {detail.links.map((l, i) => (
            <li key={`${l.cardId}-${i}`}>
              <Icon name="kanban" />
              <span className="lx-files__name">{l.name}</span>
              {detail.canManage && (
                <IconAction icon="trash" label={`Desligar ${l.name}`} onClick={() => apply(logisticsApi.setLinks(cardId, linked.filter((x) => x !== l.cardId)), 'cardIds', 'remover a ligação')} />
              )}
            </li>
          ))}
        </ul>
      ) : (
        <EmptyNote>Sem cartões ligados.</EmptyNote>
      )}
      {detail.canManage && others.length > 0 && (
        <div className="lx-inline">
          <span className="control control--select control--sm">
            <select aria-label="Cartão a ligar" value={pick} onChange={(e) => setPick(e.target.value)}>
              <option value="">Ligar a outro cartão…</option>
              {others.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.list} · {c.title}
                </option>
              ))}
            </select>
          </span>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={!pick}
            onClick={async () => (await apply(logisticsApi.setLinks(cardId, [...linked, Number(pick)]), 'cardIds', 'ligar o cartão')) && setPick('')}
          >
            Ligar
          </button>
        </div>
      )}
      <FieldError text={errors.cardIds} />
    </Section>
  );
}
