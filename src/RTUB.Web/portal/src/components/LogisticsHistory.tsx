// Card activity history panel.
import type { CardActivity } from '../data/logisticsApi';
import { at } from '../data/logisticsDates';
import { EmptyNote } from './ui/EmptyNote';

export function History({ activity }: { activity: CardActivity[] }) {
  return (
    <details className="lx-history">
      <summary>Histórico ({activity.length})</summary>
      {activity.length === 0 ? (
        <EmptyNote>Sem alterações registadas.</EmptyNote>
      ) : (
        <ol className="lx-history__list">
          {activity.map((a, i) => (
            <li key={i}>
              <span className="lx-history__what">{a.detail}</span>
              <span className="lx-history__meta">
                {a.actor?.displayName ?? 'Membro removido'} · <time dateTime={a.at}>{at(a.at)}</time>
              </span>
            </li>
          ))}
        </ol>
      )}
    </details>
  );
}
