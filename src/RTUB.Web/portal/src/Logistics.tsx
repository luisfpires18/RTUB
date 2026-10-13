import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { day, logisticsApi, problem, type BoardSummary, type Boards, type LogisticsEvent } from './logisticsApi';
import { loginTo } from './musicApi';

/**
 * /logistics - Logística (React track 023; was the Blazor page). Signed-in members, Leitões excepted. Active boards,
 * then completed ones, newest first, with search. Mod, Admin and Owner create, edit, finish, reopen and delete boards.
 */
export default function Logistics() {
  const [boards, setBoards] = useState<Boards | 'signin' | 'forbidden' | null>();
  const [text, setText] = useState('');
  const [search, setSearch] = useState('');
  const [form, setForm] = useState<{ board?: BoardSummary }>();
  const [deleting, setDeleting] = useState<BoardSummary>();
  const [message, setMessage] = useState<string>();
  const id = useId();

  useEffect(() => {
    document.title = 'Logística · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setSearch(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const load = () =>
    logisticsApi.boards(search).then((o) =>
      setBoards(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null),
    );

  useEffect(() => {
    load();
  }, [search]);

  const data = boards && typeof boards === 'object' ? boards : null;

  const setState = async (board: BoardSummary, completed: boolean) => {
    setMessage(undefined);
    const o = await logisticsApi.setBoardState(board.id, completed);
    if (o.kind === 'ok') load();
    else setMessage(problem(o, completed ? 'concluir o quadro' : 'reabrir o quadro'));
  };

  const grid = (list: BoardSummary[]) => (
    <ul className="lx-boards">
      {list.map((b) => (
        <li key={b.id} className={b.isCompleted ? 'lx-board lx-board--done' : 'lx-board'}>
          <a className="lx-board__open" href={`/logistics/${b.id}`}>
            <span className="lx-board__icon" aria-hidden="true">
              <Icon name={b.isCompleted ? 'check' : 'pin'} />
            </span>
            <strong>{b.name}</strong>
            {b.description && <span className="lx-board__desc">{b.description}</span>}
            {b.event && (
              <span className="lx-chip">
                <Icon name="calendar" />
                {b.event.name}
              </span>
            )}
            {b.isCompleted && b.completedAt && <small>Concluído a {day(b.completedAt)}</small>}
          </a>
          {data?.canManage && (
            <span className="lx-board__tools">
              <button type="button" className="icon-btn" onClick={() => setForm({ board: b })}>
                <Icon name="pencil" />
                <span className="sr-only">Editar {b.name}</span>
              </button>
              <button type="button" className="icon-btn" onClick={() => setState(b, !b.isCompleted)}>
                <Icon name={b.isCompleted ? 'restore' : 'check'} />
                <span className="sr-only">
                  {b.isCompleted ? 'Reabrir' : 'Concluir'} {b.name}
                </span>
              </button>
              <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting(b)}>
                <Icon name="trash" />
                <span className="sr-only">Eliminar {b.name}</span>
              </button>
            </span>
          )}
        </li>
      ))}
    </ul>
  );

  return (
    <section className="page wrap events-page lx-page" aria-labelledby="lx-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="lx-title" className="page__title">
            Logística
          </h1>
          <p className="page__lead">Os quadros de tarefas da tuna: o que falta fazer para cada atuação e para o dia a dia.</p>
        </div>
        {data?.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Criar quadro
            </button>
          </div>
        )}
      </header>

      {boards === 'signin' ? (
        <div className="notice" role="status">
          <p>A logística é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.logistics)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : boards === 'forbidden' ? (
        <div className="notice" role="status">
          <p>Os quadros de logística ainda não estão disponíveis para Leitões.</p>
        </div>
      ) : boards === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir os quadros agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar os quadros…" />
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={id}>
              <span className="sr-only">Procurar quadro</span>
              <Icon name="search" />
              <input id={id} type="search" placeholder="Procurar por nome ou descrição" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
          </div>
          {message && (
            <p className="form__banner" role="alert">
              {message}
            </p>
          )}

          <section aria-labelledby={`${id}-active`} className="lx-section">
            <h2 id={`${id}-active`} className="lx-section__title">
              Quadros ativos <small>{data.active.length}</small>
            </h2>
            {data.active.length > 0 ? (
              grid(data.active)
            ) : (
              <p className="note">{search ? 'Nenhum quadro ativo corresponde à pesquisa.' : 'Sem quadros ativos neste momento.'}</p>
            )}
          </section>

          <details className="lx-section lx-completed" open={data.active.length === 0 && data.completed.length > 0}>
            <summary className="lx-section__title">
              Quadros concluídos <small>{data.completed.length}</small>
              <Icon name="chevronDown" className="lx-completed__chevron" />
            </summary>
            {data.completed.length > 0 ? grid(data.completed) : <p className="note">Os quadros concluídos ficam aqui.</p>}
          </details>

          {form && <BoardDialog board={form.board} onClose={() => setForm(undefined)} onSaved={load} />}
          {deleting && <DeleteBoardDialog board={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
        </>
      )}
    </section>
  );
}

/** "Criar quadro" / "Editar quadro": name, description and an optional event, as before. */
function BoardDialog({ board, onClose, onSaved }: { board?: BoardSummary; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(board?.name ?? '');
  const [description, setDescription] = useState(board?.description ?? '');
  const [event, setEvent] = useState<LogisticsEvent | null>(board?.event ?? null);
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<LogisticsEvent[]>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => logisticsApi.events(query.trim()).then((o) => setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => clearTimeout(timer);
  }, [query]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = { name, description, eventId: event?.id ?? null };
    const o = board ? await logisticsApi.updateBoard(board.id, input) : await logisticsApi.createBoard(input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o quadro') });
    onSaved();
    onClose();
  };

  const err = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={board ? 'Editar quadro' : 'Criar quadro'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {err('form')}
        <div className={errors.name ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-name`}>Nome do quadro</label>
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
          {err('name')}
        </div>
        <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-desc`}>Descrição (opcional)</label>
          <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
          {err('description')}
        </div>
        <div className={errors.eventId ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-event`}>Atuação associada (opcional)</label>
          {event ? (
            <span className="chip chip--removable">
              <Icon name="calendar" />
              {event.name} · {day(event.date)}
              <button type="button" className="chip__remove" onClick={() => setEvent(null)}>
                <Icon name="close" />
                <span className="sr-only">Remover atuação</span>
              </button>
            </span>
          ) : (
            <>
              <span className="control control--sm">
                <Icon name="search" />
                <input id={`${fid}-event`} type="search" placeholder="Procurar atuação pelo nome" value={query} onChange={(e) => setQuery(e.target.value)} />
              </span>
              {found && found.length === 0 && <p className="note">Nenhuma atuação encontrada.</p>}
              {found && found.length > 0 && (
                <ul className="talk__picks">
                  {found.map((ev) => (
                    <li key={ev.id}>
                      <button
                        type="button"
                        onClick={() => {
                          setEvent(ev);
                          setQuery('');
                        }}
                      >
                        <Icon name="calendar" />
                        <span>
                          {ev.name}
                          <small>{day(ev.date)}</small>
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
          {err('eventId')}
        </div>
      </form>
    </Dialog>
  );
}

function DeleteBoardDialog({ board, onClose, onDone }: { board: BoardSummary; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const remove = async () => {
    setBusy(true);
    const o = await logisticsApi.removeBoard(board.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar o quadro'));
  };
  return (
    <Dialog
      title="Eliminar quadro"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar o quadro <strong>{board.name}</strong>?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Apaga também as listas e os cartões do quadro. Não dá para desfazer.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
