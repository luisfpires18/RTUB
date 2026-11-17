// The header of a board page: title, chips, actions and the archived banner.
import { Icon } from './icons';
import { logisticsApi, type Board } from '../data/logisticsApi';
import { day } from '../data/format';
import type { Open } from '../data/logisticsBoardTypes';

/** Board title, chips, actions and the archived banner. */
export function LogisticsBoardHead({ data, total, manage, archived, setOpen, run }: { data: Board; total: number; manage: boolean; archived: boolean; setOpen: (o: Open | undefined) => void; run: (o: Promise<{ kind: string }>, what: string) => Promise<void> }) {
  return (
    <header className="lx-head">
      <a className="back-link" href="/logistics">
        <Icon name="arrow" />
        Quadros
      </a>
      <div className="lx-head__row">
        <div className="lx-head__text">
          <h1 id="lx-board-title" className="page__title lx-head__title">
            {data.name}
          </h1>
          <p className="lx-head__meta">
            {data.isCompleted && <span className="lx-chip lx-chip--done">Concluído</span>}
            {data.visibility === 'Private' && (
              <span className="lx-chip lx-chip--private" title={data.members.map((m) => m.displayName).join(', ') || undefined}>
                <Icon name="lock" />
                Privado · {data.members.length} {data.members.length === 1 ? 'membro' : 'membros'}
              </span>
            )}
            <span className={data.owner ? 'lx-chip' : 'lx-chip lx-chip--warn'}>
              <Icon name="person" />
              {data.owner ? `Responsável: ${data.owner.displayName}` : 'Sem responsável'}
            </span>
            {data.event && (
              <a className="lx-chip" href={`/events/${data.event.id}`}>
                <Icon name="calendar" />
                {data.event.name}
              </a>
            )}
            <span className="lx-head__count">
              {data.lists.length} {data.lists.length === 1 ? 'coluna' : 'colunas'} · {total} {total === 1 ? 'cartão' : 'cartões'}
            </span>
          </p>
          {data.description && <p className="lx-head__desc">{data.description}</p>}
        </div>
        <div className="lx-head__actions">
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'files' })}>
            <Icon name="file" />
            Ficheiros{data.files.length > 0 && ` (${data.files.length})`}
          </button>
          <a className="btn btn--ghost btn--sm" href="/logistics/tasks">
            <Icon name="check" />
            As minhas tarefas
          </a>
          {manage && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'list' })}>
              <Icon name="plus" />
              Adicionar coluna
            </button>
          )}
        </div>
      </div>
      {archived && (
        <div className="lx-archived-banner" role="status">
          <p>
            <Icon name="folder" />
            Quadro arquivado a {day(data.archivedAt)}: só leitura.
          </p>
          {data.canManage && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => run(logisticsApi.archiveBoard(data.id, false), 'restaurar o quadro')}>
              <Icon name="restore" />
              Restaurar
            </button>
          )}
        </div>
      )}
    </header>
  );
}
