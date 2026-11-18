// /logistics/{id}: one board page - state, filters, drag and drop; the pieces live in components/Logistics*.
import { useEffect, useId, useRef, useState, type DragEvent } from 'react';
import { Icon } from '../components/icons';
import { ListView, Summary } from '../components/LogisticsViews';
import { logisticsApi, problem, STATUSES, type Board, type Card, type List, type Priority, type Status } from '../data/logisticsApi';
import { lisbonToday } from '../data/format';
import type { Drag, Drop, Due, Open, View } from '../data/logisticsBoardTypes';
import { readUrl, writeUrl } from '../data/logisticsBoardUrl';
import { isLate, sortRows, type Row, type Sort } from '../data/logisticsRows';
import { LogisticsBoardHead } from '../components/LogisticsBoardHead';
import { LogisticsBoardToolbar } from '../components/LogisticsBoardToolbar';
import { LogisticsKanban } from '../components/LogisticsKanban';
import { LogisticsBoardDialogs } from '../components/LogisticsBoardDialogs';
import { ArchivedCards } from '../components/LogisticsArchivedCards';
import { BoardNotice } from '../components/LogisticsBoardNotice';
import { CalendarView } from '../components/LogisticsCalendar';
import { Loading } from '../components/ui/Loading';
import { Banner } from '../components/ui/Banner';

const VIEWS: { key: View; label: string; icon: 'kanban' | 'listOl' | 'calendar' }[] = [
  { key: 'board', label: 'Quadro', icon: 'kanban' },
  { key: 'list', label: 'Lista', icon: 'listOl' },
  { key: 'calendar', label: 'Calendário', icon: 'calendar' },
];


/** The position among every card of the list (the dragged one left out) for a drop between visible cards. */
const fullIndex = (list: List, visible: number[], index: number, dragged: number) => {
  const all = list.cards.map((c) => c.id).filter((id) => id !== dragged);
  const shown = visible.filter((id) => id !== dragged);
  if (index < shown.length) return all.indexOf(shown[index]);
  return shown.length ? all.indexOf(shown[shown.length - 1]) + 1 : all.length;
};

/**
 * /logistics/{id} - one board. Lists side by side in a horizontal scroll, cards
 * by position with a coloured status (TODO red, WIP yellow, DONE green), labels, dates, checklist progress and counts.
 * Search, status, label and "Só os meus" filters stay in a sticky bar. Mod, Admin and Owner manage everything and drag
 * cards between lists (or use "Mover…"); every member (Leitões too) opens any card to read it, and the members
 * a card is assigned to tick its checklist and change its status there. Reminders live on the card; ?card=<id> opens a card (links and pushes).
 */
export default function LogisticsBoard({ boardId }: { boardId: number }) {
  const [board, setBoard] = useState<Board | 'signin' | 'forbidden' | 'missing' | null>();
  const [files, setFiles] = useState<Board['files']>();
  const [initial] = useState(readUrl);
  const [view, setView] = useState<View>(initial.view);
  const [text, setText] = useState(initial.q);
  const [status, setStatus] = useState<Status | ''>(initial.status);
  const [label, setLabel] = useState(initial.label);
  const [who, setWho] = useState(initial.who);
  const [prio, setPrio] = useState<Priority | ''>(initial.prio);
  const [due, setDue] = useState<Due>(initial.due);
  const [sort, setSort] = useState<Sort>(initial.sort);
  const [month, setMonth] = useState(initial.month);
  // ?card=<id>: a link or a push opens that card; the address follows the open card, so it can be shared.
  const [open, setOpen] = useState<Open | undefined>(() => (initial.card ? { kind: 'card', id: initial.card } : undefined));
  const pushNext = useRef(false);
  // Phones: the extra filters fold behind "Filtros", so the sticky bar stays one or two rows high.
  const [moreFilters, setMoreFilters] = useState(false);
  const [message, setMessage] = useState<string>();
  const [drag, setDrag] = useState<Drag>();
  const [drop, setDrop] = useState<Drop>();
  const columns = useRef<Record<number, HTMLElement | null>>({});
  // The dragged card, read by the drop handler directly (state may not have re-rendered between dragover and drop).
  const dragged = useRef<Drag | undefined>(undefined);
  const ids = { q: useId(), status: useId(), label: useId(), who: useId(), prio: useId(), due: useId(), sort: useId() };

  // The files are a storage listing (one slow R2 round trip): fetched on open and after a file change only, never
  // again on each card / list change. The board renders as soon as it arrives; the files join when they do.
  const loadFiles = () => logisticsApi.fileList(boardId).then((f) => f.kind === 'ok' && setFiles(f.data));
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
    loadFiles();
    load();
  }, [boardId]);

  // The address follows the view, filters, order, month and open card. Changing the view adds a history
  // entry, so Back returns to the previous view; everything else replaces the current one.
  const cardOpen = open?.kind === 'card' ? open.id : undefined;
  useEffect(() => {
    writeUrl({ view, q: text.trim(), status, label, who, prio, due, sort, month, card: cardOpen }, pushNext.current);
    pushNext.current = false;
  }, [view, text, status, label, who, prio, due, sort, month, cardOpen]);

  useEffect(() => {
    const back = () => {
      const u = readUrl();
      setView(u.view);
      setText(u.q);
      setStatus(u.status);
      setLabel(u.label);
      setWho(u.who);
      setPrio(u.prio);
      setDue(u.due);
      setSort(u.sort);
      setMonth(u.month);
      setOpen(u.card ? { kind: 'card', id: u.card } : undefined);
    };
    window.addEventListener('popstate', back);
    return () => window.removeEventListener('popstate', back);
  }, []);
  const switchView = (v: View) => {
    if (v === view) return;
    pushNext.current = true;
    setView(v);
  };

  const data = board && typeof board === 'object' ? { ...board, files: files ?? board.files } : null;

  useEffect(() => {
    document.title = `${data?.name ?? 'Logística'} · RTUB`;
  }, [data?.name]);

  if (board === 'signin' || board === 'forbidden' || board === 'missing' || board === null) {
    return <BoardNotice board={board} boardId={boardId} retry={() => (loadFiles(), load())} />;
  }

  if (!data) {
    return (
      <section className="page wrap lx-page">
        <Loading label="A carregar o quadro…" />
      </section>
    );
  }

  const q = text.trim().toLocaleLowerCase('pt');
  const filtering = q !== '' || status !== '' || label !== '' || who !== '' || prio !== '' || due !== '';
  const today = lisbonToday();
  const soon = (() => {
    const d = new Date(`${today}T00:00:00Z`);
    d.setUTCDate(d.getUTCDate() + 7);
    return d.toISOString().slice(0, 10);
  })();
  // A restored person filter stays selectable even when that member has no card left, so the select shows what filters.
  const named = who !== '' && who !== 'mine' && who !== 'none' ? [who] : [];
  const people = [...new Set([...named, ...data.lists.flatMap((l) => l.cards.flatMap((c) => (c.assignedTo ? [c.assignedTo.displayName] : [])))]).values()].sort((a, b) =>
    a.localeCompare(b, 'pt-PT'),
  );
  // One filter for every view: state, label, responsible member, priority, deadline, title / details.
  const visible = (cards: Card[]) =>
    cards.filter(
      (c) =>
        (status === '' || c.status === status) &&
        (who === '' || (who === 'mine' ? c.mine : who === 'none' ? !c.assignedTo : c.assignedTo?.displayName === who)) &&
        (prio === '' || c.priority === prio) &&
        (due === '' ||
          (due === 'late'
            ? isLate(c, today)
            : due === 'none'
              ? !c.dueDate
              : c.status !== 'Done' && !!c.dueDate && c.dueDate.slice(0, 10) >= today && c.dueDate.slice(0, 10) <= soon)) &&
        (label === '' || c.labels.some((l) => l.text === label)) &&
        (q === '' || c.title.toLocaleLowerCase('pt').includes(q) || c.description.toLocaleLowerCase('pt').includes(q)),
    );
  const allCards = data.lists.flatMap((l) => l.cards);
  const total = allCards.length;
  const rows: Row[] = data.lists.flatMap((list) => visible(list.cards).map((card) => ({ card, list })));
  const clear = () => (setText(''), setStatus(''), setLabel(''), setWho(''), setPrio(''), setDue(''));
  // An archived board is read-only for everyone; managers only restore it.
  const archived = !!data.archivedAt;
  const manage = data.canManage && !archived;
  const states = STATUSES.filter((s) => data.lists.some((l) => l.state === s));

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
      <LogisticsBoardHead data={data} total={total} manage={manage} archived={archived} setOpen={setOpen} run={run} />

      <div className="lx-views" role="group" aria-label="Vista">
        {VIEWS.map((v) => (
          <button key={v.key} type="button" aria-pressed={view === v.key} onClick={() => switchView(v.key)}>
            <Icon name={v.icon} />
            {v.label}
          </button>
        ))}
      </div>

      <LogisticsBoardToolbar
        ids={ids} data={data} view={view} text={text} setText={setText} status={status} setStatus={setStatus} label={label} setLabel={setLabel}
        who={who} setWho={setWho} prio={prio} setPrio={setPrio} due={due} setDue={setDue} sort={sort} setSort={setSort}
        moreFilters={moreFilters} setMoreFilters={setMoreFilters} people={people} filtering={filtering} clear={clear} columns={columns}
      />

      <Banner text={message} />

      {data.lists.length > 0 && <Summary all={allCards} shown={rows.length} filtering={filtering} />}

      {data.lists.length > 0 && view !== 'board' && filtering && rows.length === 0 ? (
        <div className="lx-empty" role="status">
          <p>Nenhum cartão corresponde aos filtros.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={clear}>
            Limpar filtros
          </button>
        </div>
      ) : data.lists.length > 0 && view === 'list' ? (
        rows.length === 0 ? (
          <div className="lx-empty" role="status">
            <p>Este quadro ainda não tem cartões.</p>
          </div>
        ) : (
          <ListView rows={sortRows(rows, sort)} sort={sort} onSort={setSort} onOpen={(id) => setOpen({ kind: 'card', id })} />
        )
      ) : data.lists.length > 0 && view === 'calendar' ? (
        <CalendarView rows={rows} month={month} onMonth={setMonth} onOpen={(id) => setOpen({ kind: 'card', id })} />
      ) : data.lists.length === 0 ? (
        <div className="notice" role="status">
          <p>Este quadro ainda não tem listas.</p>
          {manage && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'list' })}>
              <Icon name="plus" />
              Criar a primeira coluna
            </button>
          )}
        </div>
      ) : (
        <LogisticsKanban
          data={data} visible={visible} drag={drag} drop={drop} setDrag={setDrag} setDrop={setDrop} dragged={dragged} columns={columns}
          filtering={filtering} manage={manage} archived={archived} states={states} run={run} setOpen={setOpen} onDragOver={onDragOver} onDrop={onDrop}
        />
      )}

      <ArchivedCards data={data} manage={manage} run={run} setOpen={setOpen} />

      <LogisticsBoardDialogs open={open} setOpen={setOpen} data={data} load={load} loadFiles={loadFiles} moveCard={moveCard} />
    </section>
  );
}
