import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading, useLatest } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { Menu } from './LogisticsBoard';
import { MemberPicker, PersonChip } from './LogisticsDialogs';
import { TemplateDialog } from './LogisticsTemplates';
import { day, logisticsApi, problem, type Assignee, type BoardInput, type BoardSummary, type Boards, type LogisticsEvent, type Visibility } from './logisticsApi';
import { loginTo } from './musicApi';

type Tab = 'active' | 'completed' | 'archived';
const TABS: { key: Tab; label: string }[] = [
  { key: 'active', label: 'Ativos' },
  { key: 'completed', label: 'Concluídos' },
  { key: 'archived', label: 'Arquivados' },
];

/**
 * /logistics - Logística (React track 023; reshaped in task 067). Every signed-in member sees the members-visible boards and
 * the private boards they were given access to (Leitões too); Mod, Admin and Owner see all. Boards are Ativos, Concluídos
 * or Arquivados (read-only), each with its responsible member, visibility and progress: done / total, overdue, without
 * owner, and the caller's own open tasks. Managers create (blank or from a template), edit (incl. access), duplicate,
 * finish, archive and delete.
 */
export default function Logistics() {
  const [boards, setBoards] = useState<Boards | 'signin' | 'forbidden' | null>();
  const [text, setText] = useState('');
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState<Tab>('active');
  const [onlyMine, setOnlyMine] = useState(false);
  const [open, setOpen] = useState<
    { kind: 'form'; board?: BoardSummary } | { kind: 'delete'; board: BoardSummary } | { kind: 'duplicate'; board: BoardSummary } | { kind: 'template' }
  >();
  const [message, setMessage] = useState<string>();
  const id = useId();

  useEffect(() => {
    document.title = 'Logística · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setSearch(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const latest = useLatest();
  const load = () =>
    latest(logisticsApi.boards(search), (o) =>
      setBoards(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null),
    );

  useEffect(() => {
    load();
  }, [search]);

  const data = boards && typeof boards === 'object' ? boards : null;

  const act = async (o: Promise<{ kind: string }>, what: string) => {
    setMessage(undefined);
    const r = (await o) as Parameters<typeof problem>[0];
    if (r.kind !== 'ok') setMessage(problem(r, what));
    load();
  };

  const shown = data ? data[tab].filter((b) => !onlyMine || b.mine > 0) : [];

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
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'template' })}>
              <Icon name="listOl" />A partir de modelo
            </button>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
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
          <p>Não tem acesso à logística.</p>
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
          <div className="lx-boards-tools" role="search">
            <label className="control" htmlFor={id}>
              <span className="sr-only">Procurar quadro</span>
              <Icon name="search" />
              <input id={id} type="search" placeholder="Procurar por nome ou descrição" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <div className="lx-tabs" role="tablist" aria-label="Estado dos quadros">
              {TABS.map((t) => (
                <button key={t.key} type="button" role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}>
                  {t.label} <small>{data[t.key].length}</small>
                </button>
              ))}
            </div>
            <button type="button" className="lx-mine-filter" aria-pressed={onlyMine} onClick={() => setOnlyMine(!onlyMine)}>
              <Icon name="person" />
              Com tarefas minhas
            </button>
          </div>
          {message && (
            <p className="form__banner" role="alert">
              {message}
            </p>
          )}

          {shown.length > 0 ? (
            <ul className="lx-boards" role="tabpanel">
              {shown.map((b) => (
                <BoardTile
                  key={b.id}
                  board={b}
                  canManage={data.canManage}
                  onEdit={() => setOpen({ kind: 'form', board: b })}
                  onDuplicate={() => setOpen({ kind: 'duplicate', board: b })}
                  onDelete={() => setOpen({ kind: 'delete', board: b })}
                  onState={(completed) => act(logisticsApi.setBoardState(b.id, completed), completed ? 'concluir o quadro' : 'reabrir o quadro')}
                  onArchive={(archived) => act(logisticsApi.archiveBoard(b.id, archived), archived ? 'arquivar o quadro' : 'restaurar o quadro')}
                />
              ))}
            </ul>
          ) : (
            <div className="lx-empty" role="tabpanel">
              <p>
                {search || onlyMine
                  ? 'Nenhum quadro corresponde aos filtros.'
                  : tab === 'active'
                    ? 'Sem quadros ativos neste momento.'
                    : tab === 'completed'
                      ? 'Os quadros concluídos ficam aqui.'
                      : 'Os quadros arquivados ficam aqui, só de leitura.'}
              </p>
              {tab === 'active' && data.canManage && !search && !onlyMine && (
                <div className="lx-inline">
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'template' })}>
                    Usar um modelo
                  </button>
                  <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
                    Criar quadro
                  </button>
                </div>
              )}
            </div>
          )}

          {open?.kind === 'form' && <BoardDialog board={open.board} onClose={() => setOpen(undefined)} onSaved={load} />}
          {open?.kind === 'delete' && <DeleteBoardDialog board={open.board} onClose={() => setOpen(undefined)} onDone={load} />}
          {open?.kind === 'duplicate' && <DuplicateBoardDialog board={open.board} onClose={() => setOpen(undefined)} />}
          {open?.kind === 'template' && <TemplateDialog onClose={() => setOpen(undefined)} />}
        </>
      )}
    </section>
  );
}

function BoardTile({
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

type Person = { id: string; name: string; avatar: string | null };
const person = (a: Assignee): Person => ({ id: a.userId ?? '', name: a.displayName, avatar: a.avatarUrl });

/** The access fields shared by the board form and the template dialog: responsible member, visibility and access list. */
export function AccessFields({
  owner,
  setOwner,
  visibility,
  setVisibility,
  members,
  setMembers,
  errors,
}: {
  owner: Person | null;
  setOwner: (p: Person | null) => void;
  visibility: Visibility;
  setVisibility: (v: Visibility) => void;
  members: Person[];
  setMembers: (m: Person[]) => void;
  errors: Record<string, string>;
}) {
  const fid = useId();
  const err = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  return (
    <>
      <fieldset className="form__field lx-visibility">
        <legend>Quem vê o quadro</legend>
        <label className="lx-choice">
          <input type="radio" name={`${fid}-vis`} checked={visibility === 'Members'} onChange={() => setVisibility('Members')} />
          <span>
            <strong>Todos os membros</strong>
            <small>Incluindo Leitões.</small>
          </span>
        </label>
        <label className="lx-choice">
          <input type="radio" name={`${fid}-vis`} checked={visibility === 'Private'} onChange={() => setVisibility('Private')} />
          <span>
            <strong>Privado</strong>
            <small>Só a lista de acesso abaixo. Atribuir um cartão não dá acesso.</small>
          </span>
        </label>
        {err('visibility')}
      </fieldset>
      {visibility === 'Private' && (
        <div className={errors.memberIds ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Lista de acesso</span>
          {members.length > 0 ? (
            <div className="chips">
              {members.map((m) => (
                <PersonChip
                  key={m.id}
                  name={m.name}
                  avatar={m.avatar}
                  onRemove={() => {
                    setMembers(members.filter((x) => x.id !== m.id));
                    if (owner?.id === m.id) setOwner(null);
                  }}
                  removeLabel={`Retirar o acesso a ${m.name}`}
                />
              ))}
            </div>
          ) : (
            <p className="note">Ainda ninguém na lista de acesso.</p>
          )}
          <MemberPicker label="Dar acesso a" exclude={members.map((m) => m.id)} onPick={(m) => setMembers([...members, { id: m.id, name: m.displayName, avatar: m.avatarUrl }])} />
          {err('memberIds')}
        </div>
      )}
      <div className={errors.ownerUserId ? 'form__field form__field--error' : 'form__field'}>
        <span className="form__label">Responsável pelo quadro (opcional)</span>
        {owner ? (
          <PersonChip name={owner.name} avatar={owner.avatar} onRemove={() => setOwner(null)} removeLabel="Remover o responsável" />
        ) : (
          <MemberPicker
            label="Responsável pelo quadro"
            onPick={(m) => {
              const p = { id: m.id, name: m.displayName, avatar: m.avatarUrl };
              setOwner(p);
              if (visibility === 'Private' && !members.some((x) => x.id === m.id)) setMembers([...members, p]);
            }}
          />
        )}
        <small className="note">Responde pelo quadro; não ganha permissões de gestão.</small>
        {err('ownerUserId')}
      </div>
    </>
  );
}

/** "Criar quadro" / "Editar quadro": name, description, event and (task 067) responsible member, visibility and access list. */
function BoardDialog({ board, onClose, onSaved }: { board?: BoardSummary; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(board?.name ?? '');
  const [description, setDescription] = useState(board?.description ?? '');
  const [event, setEvent] = useState<LogisticsEvent | null>(board?.event ?? null);
  const [owner, setOwner] = useState<Person | null>(board?.owner ? person(board.owner) : null);
  const [visibility, setVisibility] = useState<Visibility>(board?.visibility ?? 'Members');
  const [members, setMembers] = useState<Person[]>([]);
  const [ready, setReady] = useState(!board);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  // Editing: the access list comes from the board itself (managers get the ids).
  useEffect(() => {
    if (!board) return;
    logisticsApi.board(board.id).then((o) => {
      if (o.kind === 'ok') setMembers(o.data.members.map(person));
      else setErrors({ form: problem(o, 'abrir o quadro') });
      setReady(true);
    });
  }, [board?.id]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input: BoardInput = {
      name,
      description,
      eventId: event?.id ?? null,
      ownerUserId: owner?.id || null,
      visibility,
      memberIds: visibility === 'Private' ? members.map((m) => m.id) : [],
    };
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
      className="lx-card-modal"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !ready || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      {!ready ? (
        <Loading label="A carregar…" />
      ) : (
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          {err('form')}
          {!board && <p className="note">O quadro começa com as colunas Por fazer, Em curso, Bloqueado e Feito.</p>}
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
          <EventField event={event} setEvent={setEvent} error={errors.eventId} />
          <AccessFields
            owner={owner}
            setOwner={setOwner}
            visibility={visibility}
            setVisibility={setVisibility}
            members={members}
            setMembers={setMembers}
            errors={errors}
          />
        </form>
      )}
    </Dialog>
  );
}

/** The optional event (by name search), shared with the template dialog. */
export function EventField({ event, setEvent, error, label = 'Atuação associada (opcional)' }: { event: LogisticsEvent | null; setEvent: (e: LogisticsEvent | null) => void; error?: string; label?: string }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<LogisticsEvent[]>();
  const fid = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => logisticsApi.events(query.trim()).then((o) => live && setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query]);

  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={`${fid}-event`}>{label}</label>
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
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

/** "Duplicar quadro": says what is copied (and what is not) before creating it. */
function DuplicateBoardDialog({ board, onClose }: { board: BoardSummary; onClose: () => void }) {
  const [name, setName] = useState(`${board.name} (cópia)`.slice(0, 200));
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const create = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await logisticsApi.duplicateBoard(board.id, name.trim());
    setBusy(false);
    if (o.kind === 'ok') location.href = `/logistics/${o.data.id}`;
    else setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : problem(o, 'duplicar o quadro'));
  };

  return (
    <Dialog
      title="Duplicar quadro"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Criar cópia
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={create} noValidate>
        <div className="form__field">
          <label htmlFor={`${fid}-name`}>Nome da cópia</label>
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="lx-copy-preview">
          <p>
            <strong>Copia:</strong> as colunas, os cartões ativos (título, detalhes, etiquetas, prioridade, datas, checklist por fazer), a visibilidade
            {board.visibility === 'Private' ? ' privada e a lista de acesso' : ''} e o responsável.
          </p>
          <p>
            <strong>Não copia:</strong> responsáveis e colaboradores dos cartões, comentários, histórico, anexos, lembretes, cartões arquivados e
            ficheiros do quadro.
          </p>
        </div>
        {error && (
          <p className="form__banner" role="alert">
            {error}
          </p>
        )}
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
        Apaga também as colunas, os cartões e os seus anexos. Para guardar sem apagar, use Arquivar. Não dá para desfazer.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
