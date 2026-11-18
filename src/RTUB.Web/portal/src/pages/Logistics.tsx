// /logistics: the list of boards (Ativos, Concluídos, Arquivados).
import { useEffect, useId, useState } from 'react';
import { useLatest } from '../components/AppShared';
import { Icon } from '../components/icons';
import { Notice, RetryNotice, SignInNotice } from '../components/Notice';
import { logisticsApi, problem, type Boards, type BoardSummary } from '../data/logisticsApi';
import { portal } from '../data/content';
import { TemplateDialog } from '../components/LogisticsTemplates';
import { BoardTile } from '../components/LogisticsBoardTile';
import { BoardDialog } from '../components/LogisticsBoardForm';
import { DeleteBoardDialog, DuplicateBoardDialog } from '../components/LogisticsBoardCopyDelete';
import { Loading } from '../components/ui/Loading';

type Tab = 'active' | 'completed' | 'archived';
const TABS: { key: Tab; label: string }[] = [
  { key: 'active', label: 'Ativos' },
  { key: 'completed', label: 'Concluídos' },
  { key: 'archived', label: 'Arquivados' },
];

/**
 * /logistics - Logística. Every signed-in member sees the members-visible boards and
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
        {data && (
          <div className="events-page__actions">
            <a className="btn btn--ghost btn--sm" href="/logistics/tasks">
              <Icon name="check" />
              As minhas tarefas
            </a>
            {data.canManage && (
              <>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'template' })}>
              <Icon name="listOl" />A partir de modelo
            </button>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
              <Icon name="plus" />
              Criar quadro
            </button>
              </>
            )}
          </div>
        )}
      </header>

      {boards === 'signin' ? (
        <SignInNotice path={portal.logistics}>A logística é da área de membros.</SignInNotice>
      ) : boards === 'forbidden' ? (
        <Notice>Não tem acesso à logística.</Notice>
      ) : boards === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir os quadros agora.</RetryNotice>
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
