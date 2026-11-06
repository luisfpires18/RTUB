import { useEffect, useState } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { deadline } from './LogisticsCard';
import { cardUrl, day, logisticsApi, PRIORITY, STATUS, type MyTask, type MyTasks } from './logisticsApi';
import { loginTo } from './musicApi';

type Filter = 'open' | 'late' | 'soon' | 'blocked' | 'unowned' | 'done';

/** Today in Portugal, "yyyy-mm-dd". */
const lisbonToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date());
const daysTo = (due: string) => Math.round((Date.parse(due.slice(0, 10)) - Date.parse(lisbonToday())) / 86_400_000);

const FILTERS: { key: Filter; label: string; test: (t: MyTask) => boolean; managers?: boolean }[] = [
  { key: 'open', label: 'Por fazer', test: (t) => t.role !== null && t.status !== 'Done' },
  { key: 'late', label: 'Atrasadas', test: (t) => t.role !== null && t.status !== 'Done' && !!t.dueDate && daysTo(t.dueDate) < 0 },
  { key: 'soon', label: 'Próximos 7 dias', test: (t) => t.role !== null && t.status !== 'Done' && !!t.dueDate && daysTo(t.dueDate) >= 0 && daysTo(t.dueDate) <= 7 },
  { key: 'blocked', label: 'Bloqueadas', test: (t) => t.role !== null && t.status === 'Blocked' },
  { key: 'unowned', label: 'Sem responsável', test: (t) => t.role === null, managers: true },
  { key: 'done', label: 'Feitas', test: (t) => t.role !== null && t.status === 'Done' },
];

const EMPTY: Record<Filter, string> = {
  open: 'Não tens tarefas por fazer. Quando te atribuírem um cartão, aparece aqui.',
  late: 'Nada atrasado.',
  soon: 'Nada com prazo nos próximos 7 dias.',
  blocked: 'Nenhuma tarefa bloqueada.',
  unowned: 'Todos os cartões abertos têm responsável.',
  done: 'As tarefas que concluíres ficam aqui.',
};

/**
 * /logistics/tasks - "As minhas tarefas" (task 069): every card the member works on (owner or collaborator), across the
 * boards they see, archived work left out; managers also see the open cards without a responsible member. Filters and
 * counts are over that list; each task opens its card on its board.
 */
export default function LogisticsTasks() {
  const [data, setData] = useState<MyTasks | 'signin' | 'forbidden' | null>();
  const [filter, setFilter] = useState<Filter>('open');

  const load = () =>
    logisticsApi.myTasks().then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null));

  useEffect(() => {
    document.title = 'As minhas tarefas · RTUB';
    load();
  }, []);

  const tasks = data && typeof data === 'object' ? data : null;
  const filters = FILTERS.filter((f) => !f.managers || tasks?.canManage);
  const shown = tasks ? tasks.tasks.filter(FILTERS.find((f) => f.key === filter)!.test) : [];

  return (
    <section className="page wrap events-page lx-page" aria-labelledby="lx-tasks-title">
      <a className="back-link" href="/logistics">
        <Icon name="arrow" />
        Quadros
      </a>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Logística</p>
          <h1 id="lx-tasks-title" className="page__title">
            As minhas tarefas
          </h1>
          <p className="page__lead">Os cartões de que és responsável ou em que colaboras, em todos os quadros.</p>
        </div>
      </header>

      {data === 'signin' ? (
        <div className="notice" role="status">
          <p>A logística é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo('/logistics/tasks')}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : data === 'forbidden' ? (
        <div className="notice" role="status">
          <p>Não tens acesso à logística.</p>
        </div>
      ) : data === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir as tuas tarefas agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setData(undefined), load())}>
            Tentar novamente
          </button>
        </div>
      ) : !tasks ? (
        <Loading label="A carregar as tarefas…" />
      ) : (
        <>
          <div className="lx-filters" role="group" aria-label="Filtrar tarefas">
            {filters.map((f) => (
              <button key={f.key} type="button" aria-pressed={filter === f.key} onClick={() => setFilter(f.key)}>
                {f.label} <small>{tasks.tasks.filter(f.test).length}</small>
              </button>
            ))}
          </div>

          {shown.length > 0 ? (
            <ul className="lx-tasks">
              {shown.map((t) => (
                <TaskRow key={t.cardId} task={t} />
              ))}
            </ul>
          ) : (
            <div className="lx-empty">
              <p>{EMPTY[filter]}</p>
              {filter === 'open' && (
                <a className="btn btn--ghost btn--sm" href="/logistics">
                  Ver os quadros
                </a>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
}

function TaskRow({ task: t }: { task: MyTask }) {
  const due = deadline(t.dueDate, t.status);
  return (
    <li className={`lx-task lx-task--${t.status.toLowerCase()}`}>
      <a className="lx-task__open" href={cardUrl(t.boardId, t.cardId)}>
        <span className="lx-task__top">
          <span className={`lx-status lx-view__status--${t.status.toLowerCase()}`}>{STATUS[t.status].label}</span>
          <strong className="lx-task__title">{t.title}</strong>
        </span>
        <span className="lx-task__meta">
          <span>
            <Icon name="kanban" />
            {t.boardName}
          </span>
          {t.role === 'owner' && <span className="lx-chip lx-chip--mine">Responsável</span>}
          {t.role === 'collaborator' && <span className="lx-chip">Colaborador</span>}
          {t.role === null && <span className="lx-chip lx-chip--warn">Sem responsável</span>}
          {t.priority !== 'Normal' && <span className={`lx-prio lx-prio--${t.priority.toLowerCase()}`}>{PRIORITY[t.priority]}</span>}
          {due ? (
            <span className={`lx-due lx-due--${due.tone}`}>
              {due.text} · {day(t.dueDate)}
            </span>
          ) : (
            t.dueDate && <span className="lx-due">{day(t.dueDate)}</span>
          )}
          {t.checklistTotal > 0 && (
            <span>
              <Icon name="check" />
              {t.checklistDone}/{t.checklistTotal}
            </span>
          )}
        </span>
      </a>
    </li>
  );
}
