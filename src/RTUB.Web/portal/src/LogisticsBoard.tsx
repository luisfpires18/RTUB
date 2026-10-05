import { useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties, type DragEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { day, logisticsApi, problem, STATUS, type Board, type Card, type List, type Status } from './logisticsApi';
import { CardDialog, ConfirmDialog, CreateCardDialog, FilesDialog, ListDialog, MoveCardDialog, ReminderDialog } from './LogisticsDialogs';
import { loginTo } from './musicApi';

const STATUSES: Status[] = ['Todo', 'InProgress', 'Done'];
const HEX = /^#[0-9a-f]{6}$/i;

type Drag = { cardId: number; fromList: number };
type Drop = { listId: number; index: number };
type Open =
  | { kind: 'card'; id: number }
  | { kind: 'create-card'; list: List }
  | { kind: 'list'; list?: List }
  | { kind: 'delete-list'; list: List }
  | { kind: 'delete-card'; card: Card }
  | { kind: 'move'; card: Card; listId: number }
  | { kind: 'reminder' }
  | { kind: 'files' };

/** A small action menu, drawn in fixed position so the board's horizontal scroll never clips it. */
export function Menu({ label, children }: { label: string; children: (close: () => void) => ReactNode }) {
  const [at, setAt] = useState<CSSProperties>();
  const button = useRef<HTMLButtonElement>(null);
  const list = useRef<HTMLDivElement>(null);

  // Below the button (above it near the bottom of the screen); none once the button scrolls out of sight.
  const place = (): CSSProperties | undefined => {
    const r = button.current?.getBoundingClientRect();
    if (!r || r.bottom < 0 || r.top > window.innerHeight || r.right < 0 || r.left > window.innerWidth) return undefined;
    const width = 220;
    const left = Math.max(8, Math.min(r.right - width, window.innerWidth - width - 8));
    return window.innerHeight - r.bottom > 260 ? { left, top: r.bottom + 4, width } : { left, bottom: window.innerHeight - r.top + 4, width };
  };
  const open = !!at;

  useLayoutEffect(() => {
    if (!open) return;
    const close = (e: Event) => {
      if (e.type === 'keydown' && (e as KeyboardEvent).key !== 'Escape') return;
      if (e.type === 'pointerdown' && (list.current?.contains(e.target as Node) || button.current?.contains(e.target as Node))) return;
      setAt(undefined);
    };
    // The board scrolls sideways (and snaps): the menu follows its button instead of vanishing.
    const follow = () => setAt(place());
    document.addEventListener('pointerdown', close);
    document.addEventListener('keydown', close);
    window.addEventListener('scroll', follow, true);
    window.addEventListener('resize', follow);
    list.current?.querySelector<HTMLElement>('button')?.focus({ preventScroll: true });
    return () => {
      document.removeEventListener('pointerdown', close);
      document.removeEventListener('keydown', close);
      window.removeEventListener('scroll', follow, true);
      window.removeEventListener('resize', follow);
    };
  }, [open]);

  const toggle = () => setAt(at ? undefined : place());

  return (
    <>
      <button
        ref={button}
        type="button"
        className="icon-btn icon-btn--sm lx-menu__button"
        aria-haspopup="menu"
        aria-expanded={!!at}
        onClick={(e) => {
          e.stopPropagation();
          toggle();
        }}
      >
        <Icon name="menu" />
        <span className="sr-only">{label}</span>
      </button>
      {at && (
        <div ref={list} className="lx-menu" role="menu" style={at} onClick={(e) => e.stopPropagation()}>
          {children(() => setAt(undefined))}
        </div>
      )}
    </>
  );
}

const labelColour = (c: string) => (HEX.test(c) ? c : '#6f42c1');

/** The position among every card of the list (the dragged one left out) for a drop between visible cards. */
const fullIndex = (list: List, visible: number[], index: number, dragged: number) => {
  const all = list.cards.map((c) => c.id).filter((id) => id !== dragged);
  const shown = visible.filter((id) => id !== dragged);
  if (index < shown.length) return all.indexOf(shown[index]);
  return shown.length ? all.indexOf(shown[shown.length - 1]) + 1 : all.length;
};

/**
 * /logistics/{id} - one board (React track 023; was the Blazor page). Lists side by side in a horizontal scroll, cards
 * by position with a coloured status (TODO red, WIP yellow, DONE green), labels, dates, checklist progress and counts.
 * Search, status and label filters stay in a sticky bar. Mod, Admin and Owner manage everything, drag cards between
 * lists (or use "Mover…") and open card details; every member reads the board and creates reminders.
 */
export default function LogisticsBoard({ boardId }: { boardId: number }) {
  const [board, setBoard] = useState<Board | 'signin' | 'forbidden' | 'missing' | null>();
  const [text, setText] = useState('');
  const [status, setStatus] = useState<Status | ''>('');
  const [label, setLabel] = useState('');
  const [open, setOpen] = useState<Open>();
  const [message, setMessage] = useState<string>();
  const [drag, setDrag] = useState<Drag>();
  const [drop, setDrop] = useState<Drop>();
  const columns = useRef<Record<number, HTMLElement | null>>({});
  // The dragged card, read by the drop handler directly (state may not have re-rendered between dragover and drop).
  const dragged = useRef<Drag | undefined>(undefined);
  const ids = { q: useId(), status: useId(), label: useId() };

  const load = () =>
    logisticsApi.board(boardId).then((o) =>
      setBoard(
        o.kind === 'ok'
          ? o.data
          : o.kind === 'signin'
            ? 'signin'
            : o.kind === 'forbidden'
              ? 'forbidden'
              : o.kind === 'notfound'
                ? 'missing'
                : null,
      ),
    );

  useEffect(() => {
    load();
  }, [boardId]);

  const data = board && typeof board === 'object' ? board : null;

  useEffect(() => {
    document.title = `${data?.name ?? 'Logística'} · RTUB`;
  }, [data?.name]);

  if (board === 'signin' || board === 'forbidden' || board === 'missing' || board === null) {
    return (
      <section className="page wrap events-page lx-page">
        <a className="back-link" href="/logistics">
          <Icon name="arrow" />
          Quadros
        </a>
        <div className="notice" role="status">
          {board === 'signin' ? (
            <>
              <p>A logística é da área de membros.</p>
              <a className="btn btn--primary btn--sm" href={loginTo(`/logistics/${boardId}`)}>
                <Icon name="login" />
                Entrar
              </a>
            </>
          ) : board === 'forbidden' ? (
            <p>Os quadros de logística ainda não estão disponíveis para Leitões.</p>
          ) : board === 'missing' ? (
            <p>Este quadro já não existe.</p>
          ) : (
            <>
              <p>Não conseguimos abrir o quadro agora.</p>
              <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
                Tentar novamente
              </button>
            </>
          )}
        </div>
      </section>
    );
  }

  if (!data) {
    return (
      <section className="page wrap lx-page">
        <Loading label="A carregar o quadro…" />
      </section>
    );
  }

  const q = text.trim().toLocaleLowerCase('pt');
  const filtering = q !== '' || status !== '' || label !== '';
  const visible = (cards: Card[]) =>
    cards.filter(
      (c) =>
        (status === '' || c.status === status) &&
        (label === '' || c.labels.some((l) => l.text === label)) &&
        (q === '' || c.title.toLocaleLowerCase('pt').includes(q) || c.description.toLocaleLowerCase('pt').includes(q)),
    );
  const total = data.lists.reduce((n, l) => n + l.cards.length, 0);

  const run = async (o: Promise<{ kind: string }>, what: string) => {
    setMessage(undefined);
    const r = (await o) as Parameters<typeof problem>[0];
    if (r.kind !== 'ok') setMessage(problem(r, what));
    load();
  };

  const moveCard = (cardId: number, listId: number, position: number) => {
    // Optimistic: the card lands at once; the board reloads from the server either way.
    const lists = data.lists.map((l) => ({ ...l, cards: l.cards.filter((c) => c.id !== cardId) }));
    const card = data.lists.flatMap((l) => l.cards).find((c) => c.id === cardId);
    const target = lists.find((l) => l.id === listId);
    if (card && target) target.cards.splice(Math.min(position, target.cards.length), 0, card);
    setBoard({ ...data, lists });
    run(logisticsApi.moveCard(cardId, listId, position), 'mover o cartão');
  };

  // Where between the column's other cards the pointer is (0 = on top).
  const indexAt = (e: DragEvent<HTMLElement>, cardId: number) => {
    const cards = [...e.currentTarget.querySelectorAll<HTMLElement>('[data-card]')].filter((el) => Number(el.dataset.card) !== cardId);
    const index = cards.findIndex((el) => {
      const r = el.getBoundingClientRect();
      return e.clientY < r.top + r.height / 2;
    });
    return index < 0 ? cards.length : index;
  };

  const onDragOver = (e: DragEvent<HTMLElement>, list: List) => {
    const d = dragged.current;
    if (!d) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    const next = { listId: list.id, index: indexAt(e, d.cardId) };
    if (drop?.listId !== next.listId || drop.index !== next.index) setDrop(next);
  };

  const onDrop = (e: DragEvent<HTMLElement>, list: List) => {
    e.preventDefault();
    const d = dragged.current;
    if (!d) return;
    const position = fullIndex(list, visible(list.cards).map((c) => c.id), indexAt(e, d.cardId), d.cardId);
    dragged.current = undefined;
    setDrag(undefined);
    setDrop(undefined);
    moveCard(d.cardId, list.id, position);
  };

  return (
    <section className="page wrap lx-page lx-board-page" aria-labelledby="lx-board-title">
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
              {data.event && (
                <a className="lx-chip" href={`/events/${data.event.id}`}>
                  <Icon name="calendar" />
                  {data.event.name}
                </a>
              )}
              <span className="lx-head__count">
                {data.lists.length} {data.lists.length === 1 ? 'lista' : 'listas'} · {total} {total === 1 ? 'cartão' : 'cartões'}
              </span>
            </p>
            {data.description && <p className="lx-head__desc">{data.description}</p>}
          </div>
          <div className="lx-head__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'files' })}>
              <Icon name="file" />
              Ficheiros{data.files.length > 0 && ` (${data.files.length})`}
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'reminder' })} disabled={total === 0}>
              <Icon name="bell" />
              Criar lembrete
            </button>
            {data.canManage && (
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'list' })}>
                <Icon name="plus" />
                Adicionar lista
              </button>
            )}
          </div>
        </div>
      </header>

      <div className="lx-toolbar" role="search">
        <label className="control control--sm" htmlFor={ids.q}>
          <span className="sr-only">Procurar cartão</span>
          <Icon name="search" />
          <input id={ids.q} type="search" placeholder="Procurar cartão" value={text} onChange={(e) => setText(e.target.value)} />
        </label>
        <div className="lx-status-filter" role="group" aria-label="Estado">
          <button type="button" aria-pressed={status === ''} onClick={() => setStatus('')}>
            Todos
          </button>
          {STATUSES.map((s) => (
            <button key={s} type="button" className={`lx-status-filter--${s.toLowerCase()}`} aria-pressed={status === s} onClick={() => setStatus(status === s ? '' : s)}>
              {STATUS[s].label}
            </button>
          ))}
        </div>
        {data.labels.length > 0 && (
          <label className="control control--select control--sm" htmlFor={ids.label}>
            <span className="sr-only">Etiqueta</span>
            <select id={ids.label} value={label} onChange={(e) => setLabel(e.target.value)}>
              <option value="">Todas as etiquetas</option>
              {data.labels.map((l) => (
                <option key={l} value={l}>
                  {l}
                </option>
              ))}
            </select>
          </label>
        )}
        {data.lists.length > 1 && (
          <nav className="lx-jump" aria-label="Ir para a lista">
            {data.lists.map((l) => (
              <button key={l.id} type="button" onClick={() => columns.current[l.id]?.scrollIntoView({ behavior: 'smooth', inline: 'start', block: 'nearest' })}>
                {l.name}
              </button>
            ))}
          </nav>
        )}
      </div>

      {message && (
        <p className="form__banner" role="alert">
          {message}
        </p>
      )}

      {data.lists.length === 0 ? (
        <div className="notice" role="status">
          <p>Este quadro ainda não tem listas.</p>
          {data.canManage && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'list' })}>
              <Icon name="plus" />
              Criar a primeira lista
            </button>
          )}
        </div>
      ) : (
        <div className="lx-kanban" aria-label="Listas do quadro">
          {data.lists.map((list, li) => {
            const cards = visible(list.cards);
            const others = cards.filter((c) => c.id !== drag?.cardId);
            return (
              <section
                key={list.id}
                ref={(el) => {
                  columns.current[list.id] = el;
                }}
                className={drop?.listId === list.id ? 'lx-col lx-col--over' : 'lx-col'}
                aria-label={list.name}
              >
                <header className="lx-col__head">
                  <h2 className="lx-col__title">{list.name}</h2>
                  <span className="lx-col__count">{filtering ? `${cards.length}/${list.cards.length}` : list.cards.length}</span>
                  {data.canManage && (
                    <Menu label={`Ações da lista ${list.name}`}>
                      {(close) => (
                        <>
                          <button type="button" role="menuitem" onClick={() => (close(), setOpen({ kind: 'list', list }))}>
                            <Icon name="pencil" />
                            Renomear
                          </button>
                          <button type="button" role="menuitem" disabled={li === 0} onClick={() => (close(), run(logisticsApi.moveList(list.id, li - 1), 'mover a lista'))}>
                            <Icon name="prev" />
                            Mover para a esquerda
                          </button>
                          <button
                            type="button"
                            role="menuitem"
                            disabled={li === data.lists.length - 1}
                            onClick={() => (close(), run(logisticsApi.moveList(list.id, li + 1), 'mover a lista'))}
                          >
                            <Icon name="next" />
                            Mover para a direita
                          </button>
                          <button type="button" role="menuitem" className="lx-menu__danger" onClick={() => (close(), setOpen({ kind: 'delete-list', list }))}>
                            <Icon name="trash" />
                            Eliminar lista
                          </button>
                        </>
                      )}
                    </Menu>
                  )}
                </header>
                <ol
                  className="lx-col__cards"
                  onDragOver={(e) => onDragOver(e, list)}
                  onDrop={(e) => onDrop(e, list)}
                  onDragLeave={(e) => {
                    if (!e.currentTarget.contains(e.relatedTarget as Node)) setDrop(undefined);
                  }}
                >
                  {cards.map((card) => (
                    <li key={card.id} className="lx-col__slot">
                      {drop?.listId === list.id && card.id !== drag?.cardId && drop.index === others.indexOf(card) && (
                        <span className="lx-drop" aria-hidden="true" />
                      )}
                      <CardFace
                        card={card}
                        canManage={data.canManage}
                        dragging={drag?.cardId === card.id}
                        onOpen={() => setOpen({ kind: 'card', id: card.id })}
                        onDragStart={(e) => {
                          e.dataTransfer.effectAllowed = 'move';
                          e.dataTransfer.setData('text/plain', String(card.id));
                          dragged.current = { cardId: card.id, fromList: list.id };
                          setDrag(dragged.current);
                        }}
                        onDragEnd={() => {
                          dragged.current = undefined;
                          setDrag(undefined);
                          setDrop(undefined);
                        }}
                        menu={(close) => (
                          <>
                            {STATUSES.filter((s) => s !== card.status).map((s) => (
                              <button key={s} type="button" role="menuitem" onClick={() => (close(), run(logisticsApi.setStatus(card.id, s), 'mudar o estado'))}>
                                <span className={`lx-dot lx-dot--${s.toLowerCase()}`} aria-hidden="true" />
                                Marcar {STATUS[s].label}
                              </button>
                            ))}
                            <button type="button" role="menuitem" onClick={() => (close(), setOpen({ kind: 'move', card, listId: list.id }))}>
                              <Icon name="arrow" />
                              Mover…
                            </button>
                            <button type="button" role="menuitem" className="lx-menu__danger" onClick={() => (close(), setOpen({ kind: 'delete-card', card }))}>
                              <Icon name="trash" />
                              Eliminar cartão
                            </button>
                          </>
                        )}
                      />
                    </li>
                  ))}
                  {drop?.listId === list.id && drop.index >= others.length && (
                    <li className="lx-col__slot" aria-hidden="true">
                      <span className="lx-drop" />
                    </li>
                  )}
                  {cards.length === 0 && !drop && <li className="lx-col__empty">{filtering ? 'Nada com estes filtros.' : 'Sem cartões.'}</li>}
                </ol>
                {data.canManage && (
                  <button type="button" className="lx-col__add" onClick={() => setOpen({ kind: 'create-card', list })}>
                    <Icon name="plus" />
                    Adicionar cartão
                  </button>
                )}
              </section>
            );
          })}
          {data.canManage && (
            <button type="button" className="lx-col lx-col--new" onClick={() => setOpen({ kind: 'list' })}>
              <Icon name="plus" />
              Adicionar lista
            </button>
          )}
        </div>
      )}

      {open?.kind === 'card' && (
        <CardDialog
          board={data}
          cardId={open.id}
          onClose={() => setOpen(undefined)}
          onChanged={load}
          onDelete={(card) => setOpen({ kind: 'delete-card', card })}
        />
      )}
      {open?.kind === 'create-card' && (
        <CreateCardDialog
          list={open.list}
          onClose={() => setOpen(undefined)}
          onCreated={(id) => {
            load();
            setOpen({ kind: 'card', id });
          }}
        />
      )}
      {open?.kind === 'list' && <ListDialog boardId={data.id} list={open.list} onClose={() => setOpen(undefined)} onSaved={load} />}
      {open?.kind === 'delete-list' && (
        <ConfirmDialog
          title="Eliminar lista"
          what={<>a lista <strong>{open.list.name}</strong></>}
          warning="Os cartões da lista vão com ela. Não dá para desfazer."
          action={() => logisticsApi.removeList(open.list.id)}
          onClose={() => setOpen(undefined)}
          onDone={load}
        />
      )}
      {open?.kind === 'delete-card' && (
        <ConfirmDialog
          title="Eliminar cartão"
          what={<>o cartão <strong>{open.card.title}</strong></>}
          warning="Não dá para desfazer."
          action={() => logisticsApi.removeCard(open.card.id)}
          onClose={() => setOpen(undefined)}
          onDone={load}
        />
      )}
      {open?.kind === 'move' && (
        <MoveCardDialog
          board={data}
          card={open.card}
          listId={open.listId}
          onClose={() => setOpen(undefined)}
          onMove={(listId, position) => {
            setOpen(undefined);
            moveCard(open.card.id, listId, position);
          }}
        />
      )}
      {open?.kind === 'reminder' && <ReminderDialog board={data} onClose={() => setOpen(undefined)} />}
      {open?.kind === 'files' && <FilesDialog board={data} onClose={() => setOpen(undefined)} onChanged={load} />}
    </section>
  );
}

function CardFace({
  card,
  canManage,
  dragging,
  onOpen,
  onDragStart,
  onDragEnd,
  menu,
}: {
  card: Card;
  canManage: boolean;
  dragging: boolean;
  onOpen: () => void;
  onDragStart: (e: DragEvent<HTMLElement>) => void;
  onDragEnd: () => void;
  menu: (close: () => void) => ReactNode;
}) {
  const s = card.status.toLowerCase();
  const progress = card.checklistTotal ? Math.round((card.checklistDone / card.checklistTotal) * 100) : 0;
  return (
    <article
      className={`lx-card lx-card--${s}${dragging ? ' lx-card--dragging' : ''}${canManage ? ' lx-card--open' : ''}`}
      data-card={card.id}
      draggable={canManage}
      onDragStart={canManage ? onDragStart : undefined}
      onDragEnd={canManage ? onDragEnd : undefined}
      // The whole card opens the details (a native drag cannot start on a button); the title button is the keyboard path.
      onClick={canManage ? onOpen : undefined}
    >
      <div className="lx-card__top">
        <span className={`lx-status lx-status--${s}`}>{STATUS[card.status].label}</span>
        {(card.startDate || card.dueDate) && (
          <span className="lx-card__dates">
            <Icon name="calendar" />
            {day(card.startDate)}
            {card.startDate && card.dueDate && ' – '}
            {day(card.dueDate)}
          </span>
        )}
        {canManage && <Menu label={`Ações do cartão ${card.title}`}>{menu}</Menu>}
      </div>
      {canManage ? (
        <h3 className="lx-card__title">
          <button
            type="button"
            className="lx-card__button"
            onClick={(e) => {
              e.stopPropagation();
              onOpen();
            }}
          >
            {card.title}
          </button>
        </h3>
      ) : (
        <h3 className="lx-card__title">{card.title}</h3>
      )}
      {card.description && <p className="lx-card__desc">{card.description}</p>}
      {card.labels.length > 0 && (
        <ul className="lx-labels">
          {card.labels.map((l, i) => (
            <li key={`${l.text}-${i}`} className="lx-label">
              <span className="lx-label__dot" style={{ background: labelColour(l.color) }} aria-hidden="true" />
              {l.text}
            </li>
          ))}
        </ul>
      )}
      {card.checklistTotal > 0 && (
        <div className="lx-progress" title="Checklist">
          <span className="lx-progress__bar">
            <span style={{ width: `${progress}%` }} />
          </span>
          <small>
            {card.checklistDone}/{card.checklistTotal}
          </small>
        </div>
      )}
      {(card.eventName || card.assignedTo || card.assignments > 0 || card.links > 0) && (
        <div className="lx-card__meta">
          {card.eventName && (
            <span>
              <Icon name="calendar" />
              {card.eventName}
            </span>
          )}
          {card.assignedTo && (
            <span title={card.assignedTo.fullName ?? undefined}>
              <Icon name="person" />
              {card.assignedTo.displayName}
            </span>
          )}
          {card.assignments > 0 && (
            <span title="Membros atribuídos">
              <Icon name="person" />
              {card.assignments}
            </span>
          )}
          {card.links > 0 && (
            <span title="Ligações">
              <Icon name="link" />
              {card.links}
            </span>
          )}
        </div>
      )}
    </article>
  );
}
