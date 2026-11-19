// The board page's list of archived cards.
import { Icon } from './icons';
import { logisticsApi, type Board } from '../data/logisticsApi';
import type { Open } from '../data/logisticsBoardTypes';

/** Archived cards of the board, with a restore button for managers. */
export function ArchivedCards({ data, manage, run, setOpen }: { data: Board; manage: boolean; run: (o: Promise<{ kind: string }>, what: string) => Promise<void>; setOpen: (o: Open | undefined) => void }) {
  return (
    <>
      {data.archivedCards.length > 0 && (
        <details className="lx-archived">
          <summary>
            Cartões arquivados <small>{data.archivedCards.length}</small>
          </summary>
          <ul className="lx-archived__list">
            {data.archivedCards.map((c) => (
              <li key={c.id}>
                <button type="button" className="lx-files__open" onClick={() => setOpen({ kind: 'card', id: c.id })}>
                  {c.title}
                </button>
                {manage && (
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => run(logisticsApi.archiveCard(c.id, false), 'restaurar o cartão')}>
                    <Icon name="restore" />
                    Restaurar
                  </button>
                )}
              </li>
            ))}
          </ul>
        </details>
      )}
    </>
  );
}
