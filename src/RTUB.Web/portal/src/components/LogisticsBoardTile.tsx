// One board card on the Logística list.
import { Icon } from './icons';
import type { BoardSummary } from '../data/logisticsApi';
import { day } from '../data/format';
import { Menu } from './LogisticsMenu';

export function BoardTile({
  board: b,
  canManage,
  onEdit,
  onDuplicate,
  onDelete,
  onState,
  onArchive,
}: {
  board: BoardSummary;
  canManage: boolean;
  onEdit: () => void;
  onDuplicate: () => void;
  onDelete: () => void;
  onState: (completed: boolean) => void;
  onArchive: (archived: boolean) => void;
}) {
  const progress = b.cards ? Math.round((b.done / b.cards) * 100) : 0;
  const archived = !!b.archivedAt;
  return (
    <li className={`lx-board${b.isCompleted ? ' lx-board--done' : ''}${archived ? ' lx-board--archived' : ''}`}>
      <a className="lx-board__open" href={`/logistics/${b.id}`}>
        <span className="lx-board__top">
          <strong>{b.name}</strong>
          {b.visibility === 'Private' && (
            <span className="lx-chip lx-chip--private" title="Só para a lista de acesso">
              <Icon name="lock" />
              Privado
            </span>
          )}
          {archived && <span className="lx-chip">Arquivado</span>}
        </span>
        {b.description && <span className="lx-board__desc">{b.description}</span>}
        <span className="lx-board__meta">
          {b.event && (
            <span>
              <Icon name="calendar" />
              {b.event.name} · {day(b.event.date)}
            </span>
          )}
          <span className={b.owner ? undefined : 'lx-card__unowned'}>
            <Icon name="person" />
            {b.owner ? b.owner.displayName : 'Sem responsável'}
          </span>
        </span>
        {b.cards > 0 ? (
          <>
            <span className="lx-progress">
              <span className="lx-progress__bar">
                <span style={{ width: `${progress}%` }} />
              </span>
              <small>
                {b.done}/{b.cards} feitas
              </small>
            </span>
            {(b.overdue > 0 || b.unowned > 0 || b.mine > 0) && (
              <span className="lx-board__stats">
                {b.overdue > 0 && <span className="lx-due lx-due--late">{b.overdue} {b.overdue === 1 ? 'atrasada' : 'atrasadas'}</span>}
                {b.unowned > 0 && <span>{b.unowned} sem responsável</span>}
                {b.mine > 0 && <span className="lx-board__mine">{b.mine} {b.mine === 1 ? 'tarefa minha' : 'tarefas minhas'}</span>}
              </span>
            )}
          </>
        ) : (
          <small className="note">Sem tarefas.</small>
        )}
        {b.isCompleted && b.completedAt && <small className="note">Concluído a {day(b.completedAt)}</small>}
      </a>
      {canManage && (
        <span className="lx-board__tools">
          <Menu label={`Ações do quadro ${b.name}`}>
            {(close) => (
              <>
                {!archived && (
                  <button type="button" role="menuitem" onClick={() => (close(), onEdit())}>
                    <Icon name="pencil" />
                    Editar e acesso
                  </button>
                )}
                <button type="button" role="menuitem" onClick={() => (close(), onDuplicate())}>
                  <Icon name="kanban" />
                  Duplicar…
                </button>
                {!archived && (
                  <button type="button" role="menuitem" onClick={() => (close(), onState(!b.isCompleted))}>
                    <Icon name={b.isCompleted ? 'restore' : 'check'} />
                    {b.isCompleted ? 'Reabrir' : 'Concluir'}
                  </button>
                )}
                <button type="button" role="menuitem" onClick={() => (close(), onArchive(!archived))}>
                  <Icon name={archived ? 'restore' : 'folder'} />
                  {archived ? 'Restaurar' : 'Arquivar'}
                </button>
                <button type="button" role="menuitem" className="lx-menu__danger" onClick={() => (close(), onDelete())}>
                  <Icon name="trash" />
                  Eliminar
                </button>
              </>
            )}
          </Menu>
        </span>
      )}
    </li>
  );
}
