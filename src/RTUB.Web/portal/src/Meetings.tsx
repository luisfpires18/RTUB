import { useEffect, useId, useState, type ReactNode } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon, type IconName } from './icons';
import { AtaEditorDialog, AtaViewDialog } from './MeetingAta';
import {
  CancelDialog,
  ConfirmDialog,
  DetailsDialog,
  EmailDialog,
  MeetingFormDialog,
  ParticipantsDialog,
  ParticipationDialog,
  ProposalDialog,
  PushDialog,
  RequestDetailsDialog,
  RequestStatusPill,
  problem,
  typeClass,
} from './MeetingDialogs';
import {
  dayBadge,
  meetingsApi,
  numericDate,
  shortDate,
  timeOf,
  weekdayName,
  type MeetingBoard,
  type MeetingCard,
  type MeetingDraft,
  type MeetingRequest,
  type MeetingRequestPage,
} from './meetingsApi';
import { loginTo } from './musicApi';

const MEETING_PAGE_SIZES = [6, 12, 18, 24];
const REQUEST_PAGE_SIZES = [4, 8, 12, 16, 20];
const UPCOMING_EMPTY =
  'Não existem reuniões agendadas no momento. As assembleias gerais podem ser convocadas por iniciativa da Mesa da Assembleia Geral, ou a pedido da Direcção, do Conselho Fiscal, do Conselho de Veteranos, ou de pelo menos 20% dos Associados Efectivos.';

/** The proposals the page offers (the old "Propor ..." buttons): CV, Direção and, as "Assembleia Geral", an AGE. */
export const PROPOSAL_TYPES = {
  cv: 'ConselhoVeteranos',
  direcao: 'ReuniaoDirecao',
  ag: 'AssembleiaGeralExtraordinaria',
} as const;

type Open =
  | { kind: 'details'; card: MeetingCard }
  | { kind: 'form'; card?: MeetingCard; draft?: MeetingDraft }
  | { kind: 'delete'; card: MeetingCard }
  | { kind: 'respond'; card: MeetingCard; willAttend: boolean }
  | { kind: 'removeOwn'; card: MeetingCard }
  | { kind: 'participants'; card: MeetingCard }
  | { kind: 'email'; card: MeetingCard }
  | { kind: 'push'; card: MeetingCard }
  | { kind: 'cancel'; card: MeetingCard }
  | { kind: 'uncancel'; card: MeetingCard }
  | { kind: 'ata'; card: MeetingCard }
  | { kind: 'viewAta'; card: MeetingCard }
  | { kind: 'propose'; type: string };

type Flash = { tone: 'ok' | 'warn'; text: string };

/**
 * /meetings - Reuniões (task 034; was the Blazor page). Meetings and assemblies: the upcoming and past ones the
 * member sees, "Vou / Não vou", participants, atas, and for those who run them create / edit / notify / cancel; the
 * "Pedidos de Reuniões" section for Veteranos and the Magister. Every rule is the server's (MeetingBoardService): the
 * page only draws what `access` and each card's `can` allow. Signed-in members only; a Leitão is refused.
 */
export default function Meetings() {
  const initial = new URLSearchParams(location.search);
  const [fy, setFy] = useState<string | null>(initial.get('fy') || null);
  const [text, setText] = useState(initial.get('q') ?? '');
  const [q, setQ] = useState(text.trim());
  const [board, setBoard] = useState<MeetingBoard | 'signin' | 'forbidden' | null>();
  const [version, setVersion] = useState(0);
  const [requestsVersion, setRequestsVersion] = useState(0);
  const [upcomingPage, setUpcomingPage] = useState(1);
  const [upcomingSize, setUpcomingSize] = useState(12);
  const [pastPage, setPastPage] = useState(1);
  const [pastSize, setPastSize] = useState(12);
  const [open, setOpen] = useState<Open>();
  const [flash, setFlash] = useState<Flash>();
  const ids = { q: useId(), year: useId(), upcoming: useId(), past: useId() };

  useEffect(() => {
    document.title = 'Reuniões · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  useEffect(() => {
    setUpcomingPage(1);
    setPastPage(1);
  }, [fy, q]);

  useEffect(() => {
    const url = new URL(location.href);
    fy ? url.searchParams.set('fy', fy) : url.searchParams.delete('fy');
    q ? url.searchParams.set('q', q) : url.searchParams.delete('q');
    history.replaceState(null, '', url.pathname + url.search);
    let live = true;
    meetingsApi.board(fy, q).then((o) => {
      if (!live) return;
      if (o.kind === 'invalid' && fy) return setFy(null); // an unknown year in the URL: back to the current one
      setBoard(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null);
    });
    return () => {
      live = false;
    };
  }, [fy, q, version]);

  useEffect(() => {
    if (!flash) return;
    const timer = setTimeout(() => setFlash(undefined), 8000);
    return () => clearTimeout(timer);
  }, [flash]);

  const reload = () => setVersion((v) => v + 1);
  const close = () => setOpen(undefined);
  const data = board && typeof board === 'object' ? board : null;

  if (board === 'forbidden') {
    return (
      <section className="page wrap mtg-page" aria-labelledby="meetings-refused">
        <div className="state mtg-refusal" role="status">
          <Icon name="lock" className="state__icon" />
          <h1 id="meetings-refused" className="state__title">
            Acesso Restrito
          </h1>
          <p>Esta página não está disponível para utilizadores Leitão.</p>
          <div className="state__actions">
            <a className="btn btn--ghost btn--sm" href={portal.profile}>
              Voltar ao perfil
            </a>
          </div>
        </div>
      </section>
    );
  }

  const access = data?.access;
  const selectedYear = data ? (data.fiscalYear ?? 'all') : 'all';
  const years = data
    ? [...new Set([...(data.fiscalYear ? [data.fiscalYear] : []), data.currentFiscalYear, ...data.fiscalYears])].sort((a, b) => b.localeCompare(a))
    : [];

  return (
    <section className="page wrap events-page mtg-page" aria-labelledby="meetings-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Gestão</p>
          <h1 id="meetings-title" className="page__title">
            Reuniões
          </h1>
          <p className="page__lead">Gerir reuniões e assembleias da organização.</p>
        </div>
        {access && (
          <div className="events-page__actions mtg-head-actions">
            {access.canCreate && (
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
                <Icon name="plus" />
                Criar Reunião
              </button>
            )}
            {access.canProposeCv && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'propose', type: PROPOSAL_TYPES.cv })}>
                <Icon name="megaphone" />
                Propor Reunião de CV
              </button>
            )}
            {access.canProposeDirecao && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'propose', type: PROPOSAL_TYPES.direcao })}>
                <Icon name="briefcase" />
                Propor Reunião Direção
              </button>
            )}
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              disabled={!access.canProposeAg}
              title="Propor Assembleia Geral"
              onClick={() => setOpen({ kind: 'propose', type: PROPOSAL_TYPES.ag })}
            >
              <Icon name="people" />
              Propor Assembleia Geral
            </button>
          </div>
        )}
      </header>

      {flash && (
        <div className={`mtg-flash mtg-flash--${flash.tone}`} role="status">
          <Icon name={flash.tone === 'ok' ? 'checkCircle' : 'warning'} />
          <p>{flash.text}</p>
          <button type="button" className="icon-btn icon-btn--sm" onClick={() => setFlash(undefined)}>
            <Icon name="close" />
            <span className="sr-only">Fechar a mensagem</span>
          </button>
        </div>
      )}

      {board === 'signin' ? (
        <div className="notice" role="status">
          <p>As reuniões são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.meetings)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : board === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir as reuniões agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={reload}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar as reuniões…" />
      ) : (
        <>
          <div className="events-tools mtg-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Pesquisar reuniões</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Pesquisar por título ou declaração..." value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.year}>
              <span className="sr-only">Ano</span>
              <select id={ids.year} value={selectedYear} onChange={(e) => setFy(e.target.value === data.currentFiscalYear ? null : e.target.value)}>
                <option value="all">Todos os anos</option>
                {years.map((y) => (
                  <option key={y} value={y}>
                    {y}
                    {y === data.currentFiscalYear ? ' (ATUAL)' : ''}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <MeetingSection
            id={ids.upcoming}
            icon="calendar"
            title="Próximas Reuniões"
            itemLabel="reuniões agendadas"
            cards={data.upcoming}
            page={upcomingPage}
            pageSize={upcomingSize}
            onPage={setUpcomingPage}
            onPageSize={(s) => (setUpcomingSize(s), setUpcomingPage(1))}
            empty={<Empty title="Nenhuma reunião agendada." message={UPCOMING_EMPTY} />}
            onOpen={setOpen}
          />

          <MeetingSection
            id={ids.past}
            icon="calendarCheck"
            title="Reuniões Realizadas"
            itemLabel="reuniões realizadas"
            cards={data.past}
            page={pastPage}
            pageSize={pastSize}
            onPage={setPastPage}
            onPageSize={(s) => (setPastSize(s), setPastPage(1))}
            empty={<Empty title="Nenhuma reunião realizada." />}
            onOpen={setOpen}
          />

          {data.access.canSeeRequests && (
            <RequestsSection
              fy={fy}
              version={requestsVersion}
              onAccepted={(draft) => setOpen({ kind: 'form', draft })}
              onChanged={reload}
            />
          )}
        </>
      )}

      {data && open && (
        <Dialogs
          open={open}
          board={data}
          onClose={close}
          onChanged={reload}
          onFlash={setFlash}
          onRequestsChanged={() => setRequestsVersion((v) => v + 1)}
        />
      )}
    </section>
  );
}

function Dialogs({
  open,
  board,
  onClose,
  onChanged,
  onFlash,
  onRequestsChanged,
}: {
  open: Open;
  board: MeetingBoard;
  onClose: () => void;
  onChanged: () => void;
  onFlash: (f: Flash) => void;
  onRequestsChanged: () => void;
}) {
  const done = (message?: string, warning?: string | null) => {
    onChanged();
    if (warning) onFlash({ tone: 'warn', text: message ? `${message} ${warning}` : warning });
    else if (message) onFlash({ tone: 'ok', text: message });
  };

  switch (open.kind) {
    case 'details':
      return <DetailsDialog card={open.card} onClose={onClose} />;
    case 'form':
      return <MeetingFormDialog card={open.card} draft={open.draft} board={board} onClose={onClose} onSaved={(m) => (done(m), onClose())} />;
    case 'delete':
      return (
        <ConfirmDialog
          title="Confirmar Eliminação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={onClose}
          onConfirm={async () => {
            const o = await meetingsApi.remove(open.card.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar a reunião');
            done('Reunião eliminada.');
          }}
        >
          <p>
            Tem a certeza que quer eliminar a reunião <strong>{open.card.title}</strong>?
          </p>
          <p className="note">Não é possível desfazer.</p>
        </ConfirmDialog>
      );
    case 'respond':
      return <ParticipationDialog card={open.card} willAttend={open.willAttend} onClose={onClose} onSaved={() => (done(), onClose())} />;
    case 'removeOwn':
      return (
        <ConfirmDialog
          title="Confirmar Remoção de Participação"
          confirmLabel="Sim, Remover"
          cancelLabel="Não"
          danger
          onClose={onClose}
          onConfirm={async () => {
            const mine = open.card.myParticipation;
            if (!mine) return 'Já não tem uma resposta nesta reunião.';
            const o = await meetingsApi.removeParticipation(open.card.id, mine.id);
            if (o.kind !== 'ok') return problem(o, 'remover a participação');
            done('Participação removida.');
          }}
        >
          <p>Tem certeza que deseja remover sua participação nesta reunião?</p>
        </ConfirmDialog>
      );
    case 'participants':
      return <ParticipantsDialog card={open.card} onClose={onClose} onChanged={onChanged} />;
    case 'email':
      return <EmailDialog card={open.card} onClose={onClose} onSent={(r) => (done(`Email enviado a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.`, r.warning), onClose())} />;
    case 'push':
      return (
        <PushDialog card={open.card} onClose={onClose} onSent={(r) => (done(`Notificação push enviada a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.`, r.warning), onClose())} />
      );
    case 'cancel':
      return (
        <CancelDialog
          card={open.card}
          onClose={onClose}
          onCancelled={(r, notified) => (
            done(notified ? `Reunião cancelada. Email enviado a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.` : 'Reunião cancelada.', r.warning), onClose()
          )}
        />
      );
    case 'uncancel':
      return (
        <ConfirmDialog
          title="Reverter cancelamento"
          confirmLabel="Reverter"
          confirmIcon="restore"
          onClose={onClose}
          onConfirm={async () => {
            const o = await meetingsApi.uncancel(open.card.id);
            if (o.kind !== 'ok') return problem(o, 'reverter o cancelamento');
            done('A reunião voltou a ficar agendada.');
          }}
        >
          <p>
            A reunião <strong>{open.card.title}</strong> volta a ficar agendada.
          </p>
        </ConfirmDialog>
      );
    case 'ata':
      return <AtaEditorDialog card={open.card} onClose={onClose} onChanged={onChanged} />;
    case 'viewAta':
      return <AtaViewDialog card={open.card} onClose={onClose} />;
    case 'propose':
      return (
        <ProposalDialog
          type={open.type}
          onClose={onClose}
          onSent={() => {
            onRequestsChanged();
            onFlash({ tone: 'ok', text: 'Proposta enviada.' });
            onClose();
          }}
        />
      );
  }
}

function Empty({ title, message }: { title: string; message?: string }) {
  return (
    <div className="mtg-empty">
      <Icon name="calendar" className="mtg-empty__icon" />
      <p className="mtg-empty__title">{title}</p>
      {message && <p className="mtg-empty__text">{message}</p>}
    </div>
  );
}

function MeetingSection({
  id,
  icon,
  title,
  itemLabel,
  cards,
  page,
  pageSize,
  onPage,
  onPageSize,
  empty,
  onOpen,
}: {
  id: string;
  icon: IconName;
  title: string;
  itemLabel: string;
  cards: MeetingCard[];
  page: number;
  pageSize: number;
  onPage: (p: number) => void;
  onPageSize: (s: number) => void;
  empty: ReactNode;
  onOpen: (o: Open) => void;
}) {
  const pages = Math.max(1, Math.ceil(cards.length / pageSize));
  const current = Math.min(page, pages);
  const shown = cards.slice((current - 1) * pageSize, current * pageSize);

  return (
    <section className="mtg-section" aria-labelledby={id}>
      <h2 id={id} className="mtg-section__title">
        <Icon name={icon} />
        {title}
      </h2>
      {cards.length === 0 ? (
        empty
      ) : (
        <>
          <ul className="mtg-grid">
            {shown.map((card) => (
              <li key={card.id}>
                <MeetingCardView card={card} onOpen={onOpen} />
              </li>
            ))}
          </ul>
          {cards.length > MEETING_PAGE_SIZES[0] && (
            <Pager
              total={cards.length}
              itemLabel={itemLabel}
              page={current}
              pages={pages}
              pageSize={pageSize}
              sizes={MEETING_PAGE_SIZES}
              onPage={onPage}
              onPageSize={onPageSize}
            />
          )}
        </>
      )}
    </section>
  );
}

function MeetingCardView({ card, onOpen }: { card: MeetingCard; onOpen: (o: Open) => void }) {
  const status = card.cancelled ? 'Cancelada' : card.past ? 'Realizada' : 'Agendada';
  const statusClass = card.cancelled ? 'mtg-status mtg-status--cancelled' : card.past ? 'mtg-status mtg-status--past' : 'mtg-status mtg-status--upcoming';
  const mine = card.myParticipation;
  const soon = card.cancelled ? null : dayBadge(card.date);

  return (
    <article className={card.cancelled ? 'mtg-card is-cancelled' : 'mtg-card'}>
      <div className="mtg-card__top">
        <span className="mtg-card__badges">
          <span className={statusClass}>{status}</span>
          {mine && (
            <span className={mine.willAttend ? 'pill pill--yes' : 'pill pill--no'}>
              <Icon name={mine.willAttend ? 'checkCircle' : 'xCircle'} />
              {mine.willAttend ? (card.completed ? 'FUI' : 'VOU') : card.completed ? 'NÃO FUI' : 'NÃO VOU'}
            </span>
          )}
          {soon && <span className="mtg-soon">{soon}</span>}
        </span>
        {card.can.manage && (
          <span className="mtg-card__tools">
            <button type="button" className="icon-btn icon-btn--sm" onClick={() => onOpen({ kind: 'form', card })} title="Editar">
              <Icon name="pencil" />
              <span className="sr-only">Editar reunião {card.title}</span>
            </button>
            <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onOpen({ kind: 'delete', card })} title="Eliminar">
              <Icon name="trash" />
              <span className="sr-only">Eliminar reunião {card.title}</span>
            </button>
          </span>
        )}
      </div>

      <div className="mtg-card__head">
        <h3 className="mtg-card__title">{card.title}</h3>
        <span className={typeClass(card.type)}>{card.typeLabel}</span>
      </div>

      <dl className="mtg-facts">
        <div>
          <dt>
            <Icon name="calendar" />
            <span className="sr-only">Data</span>
          </dt>
          <dd>
            {shortDate(card.date)} ({weekdayName(card.date)})
          </dd>
        </div>
        <div>
          <dt>
            <Icon name="clock" />
            <span className="sr-only">Hora</span>
          </dt>
          <dd>{timeOf(card.date)}</dd>
        </div>
        {card.location && (
          <div>
            <dt>
              <Icon name="geo" />
              <span className="sr-only">Local</span>
            </dt>
            <dd className="mtg-break">{card.location}</dd>
          </div>
        )}
        {card.organizerPosition && (
          <div>
            <dt>
              <Icon name="personBadge" />
              <span className="sr-only">Organizador</span>
            </dt>
            <dd>
              Organizador: {card.organizerName}, {card.organizerPosition}
            </dd>
          </div>
        )}
        {card.tunoRepresentative && (
          <div>
            <dt className="mtg-facts__gold">
              <Icon name="personBadge" />
              <span className="sr-only">Representante</span>
            </dt>
            <dd>Representante dos Tunos: {card.tunoRepresentative}</dd>
          </div>
        )}
      </dl>

      {(card.can.participants || card.can.respond || card.can.removeOwn) && (
        <div className="mtg-answer">
          {card.can.participants && (
            <button type="button" className="mtg-count" onClick={() => onOpen({ kind: 'participants', card })} title="Participantes">
              <Icon name="people" />
              <span>{card.goingCount}</span>
              <span className="sr-only">{card.goingCount === 1 ? 'participante — ver participantes' : 'participantes — ver participantes'}</span>
            </button>
          )}
          {card.can.respond && (
            <span className="mtg-answer__choices">
              <button
                type="button"
                className={mine?.willAttend === true ? 'mtg-choice mtg-choice--yes is-on' : 'mtg-choice mtg-choice--yes'}
                aria-pressed={mine?.willAttend === true}
                onClick={() => onOpen({ kind: 'respond', card, willAttend: true })}
                title={mine?.willAttend === true ? 'Editar participação' : 'Vou participar'}
              >
                <Icon name="checkCircle" />
                Vou
              </button>
              <button
                type="button"
                className={mine?.willAttend === false ? 'mtg-choice mtg-choice--no is-on' : 'mtg-choice mtg-choice--no'}
                aria-pressed={mine?.willAttend === false}
                onClick={() => onOpen({ kind: 'respond', card, willAttend: false })}
                title={mine?.willAttend === false ? 'Editar impossibilidade' : 'Não vou participar'}
              >
                <Icon name="xCircle" />
                Não vou
              </button>
            </span>
          )}
          {card.can.removeOwn && mine && (
            <button type="button" className="btn btn--ghost btn--sm mtg-remove-own" onClick={() => onOpen({ kind: 'removeOwn', card })}>
              <Icon name="xCircle" />
              Remover participação
            </button>
          )}
        </div>
      )}

      <div className="mtg-card__actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={() => onOpen({ kind: 'details', card })}>
          <Icon name="eye" />
          Ver Detalhes
        </button>
        {card.can.writeAta && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'ata', card })} title="Criar/Editar Ata">
            <Icon name="file" />
            Ata
          </button>
        )}
        {card.can.viewAta && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'viewAta', card })}>
            <Icon name="eye" />
            Ver Ata
            {card.ataStatus === 'draft' && <span className="mtg-tag">Rascunho</span>}
          </button>
        )}
        {card.can.notify && (
          <>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'email', card })} title="Enviar notificação por email">
              <Icon name="envelope" />
              Email
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'push', card })} title="Enviar notificação push">
              <Icon name="bell" />
              Push
            </button>
            <button type="button" className="btn btn--danger btn--sm" onClick={() => onOpen({ kind: 'cancel', card })} title="Cancelar reunião">
              <Icon name="xCircle" />
              Cancelar
            </button>
          </>
        )}
        {card.can.uncancel && (
          <button type="button" className="btn btn--gold btn--sm" onClick={() => onOpen({ kind: 'uncancel', card })} title="Reverter cancelamento">
            <Icon name="restore" />
            Reverter
          </button>
        )}
      </div>
    </article>
  );
}

export function Pager({
  total,
  itemLabel,
  page,
  pages,
  pageSize,
  sizes,
  onPage,
  onPageSize,
}: {
  total: number;
  itemLabel: string;
  page: number;
  pages: number;
  pageSize: number;
  sizes: number[];
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  return (
    <nav className="mtg-pager" aria-label={`Páginas de ${itemLabel}`}>
      <span className="note">
        {total} {itemLabel}
      </span>
      <span className="mtg-pager__nav">
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className="mtg-pager__size">
        Por página
        <span className="control control--select control--sm">
          <select id={id} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {sizes.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </span>
      </label>
    </nav>
  );
}

// ---------- Pedidos de Reuniões ----------

const requestIcon = (type: string): IconName => (type === 'ConselhoVeteranos' ? 'megaphone' : type === 'ReuniaoDirecao' ? 'briefcase' : 'people');

type RequestAction = { kind: 'details' | 'delete' | 'remind'; request: MeetingRequest };

/** "Pedidos de Reuniões": every type, the page's year, newest proposed date first, paged by the server. */
function RequestsSection({
  fy,
  version,
  onAccepted,
  onChanged,
}: {
  fy: string | null;
  version: number;
  onAccepted: (draft: MeetingDraft) => void;
  onChanged: () => void;
}) {
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(REQUEST_PAGE_SIZES[0]);
  const [data, setData] = useState<MeetingRequestPage | null>();
  const [mine, setMine] = useState(0);
  const [busy, setBusy] = useState<number>();
  const [action, setAction] = useState<RequestAction>();
  const [success, setSuccess] = useState<string>();
  const [error, setError] = useState<string>();
  const ids = { title: useId(), status: useId() };

  useEffect(() => setPage(1), [fy, status]);

  useEffect(() => {
    let live = true;
    meetingsApi.requests(status, fy, page, pageSize).then((o) => {
      if (!live) return;
      if (o.kind === 'ok' && o.data.items.length === 0 && o.data.total > 0 && page > 1) return setPage(Math.ceil(o.data.total / pageSize));
      setData(o.kind === 'ok' ? o.data : null);
    });
    return () => {
      live = false;
    };
  }, [status, fy, page, pageSize, version, mine]);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(undefined), 3000);
    return () => clearTimeout(timer);
  }, [success]);

  const reload = () => setMine((v) => v + 1);

  const decide = async (request: MeetingRequest, accept: boolean) => {
    setBusy(request.id);
    setError(undefined);
    if (accept) {
      const o = await meetingsApi.accept(request.id);
      setBusy(undefined);
      reload();
      if (o.kind !== 'ok') return setError(problem(o, 'aceitar o pedido'));
      onAccepted(o.data); // the create form opens prefilled with the request, as before
    } else {
      const o = await meetingsApi.reject(request.id);
      setBusy(undefined);
      reload();
      if (o.kind !== 'ok') return setError(problem(o, 'rejeitar o pedido'));
    }
    onChanged();
  };

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <section className="mtg-section mtg-requests" aria-labelledby={ids.title}>
      <h2 id={ids.title} className="mtg-section__title">
        <Icon name="inbox" />
        Pedidos de Reuniões
      </h2>
      <div className="mtg-requests__tools">
        <label className="control control--select" htmlFor={ids.status}>
          <span className="sr-only">Estado</span>
          <select id={ids.status} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Todos os Estados</option>
            <option value="Pending">Pendente</option>
            <option value="Confirmed">Aceite</option>
            <option value="Rejected">Rejeitado</option>
          </select>
        </label>
      </div>

      {success && (
        <div className="mtg-flash mtg-flash--ok" role="status">
          <Icon name="checkCircle" />
          <p>{success}</p>
          <button type="button" className="icon-btn icon-btn--sm" onClick={() => setSuccess(undefined)}>
            <Icon name="close" />
            <span className="sr-only">Fechar a mensagem</span>
          </button>
        </div>
      )}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}

      {data === undefined ? (
        <Loading label="A carregar os pedidos…" />
      ) : data === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir os pedidos agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={reload}>
            Tentar novamente
          </button>
        </div>
      ) : data.items.length === 0 ? (
        <div className="mtg-empty">
          <Icon name="inbox" className="mtg-empty__icon" />
          <p className="mtg-empty__title">Nenhum pedido de reunião encontrado.</p>
        </div>
      ) : (
        <>
          <ul className="mtg-grid mtg-grid--requests">
            {data.items.map((r) => (
              <li key={r.id}>
                <article className="mtg-request">
                  <div className="mtg-request__top">
                    <span className="mtg-request__icon" aria-hidden="true">
                      <Icon name={requestIcon(r.type)} />
                    </span>
                    <span className={typeClass(r.type)}>{r.typeLabel}</span>
                    {(r.canDelete || r.canRemind) && (
                      <span className="mtg-card__tools">
                        {r.canRemind && (
                          <button type="button" className="icon-btn icon-btn--sm" onClick={() => setAction({ kind: 'remind', request: r })} title="Enviar Lembrete">
                            <Icon name="bell" />
                            <span className="sr-only">Enviar lembrete sobre {r.title}</span>
                          </button>
                        )}
                        {r.canDelete && (
                          <button
                            type="button"
                            className="icon-btn icon-btn--sm icon-btn--danger"
                            onClick={() => setAction({ kind: 'delete', request: r })}
                            title="Eliminar"
                          >
                            <Icon name="trash" />
                            <span className="sr-only">Eliminar pedido {r.title}</span>
                          </button>
                        )}
                      </span>
                    )}
                  </div>
                  <h3 className="mtg-request__title">{r.title}</h3>
                  <dl className="mtg-facts">
                    <div>
                      <dt>
                        <Icon name="person" />
                        <span className="sr-only">Autor</span>
                      </dt>
                      <dd>
                        <strong>{r.authorName}</strong>
                      </dd>
                    </div>
                    <div>
                      <dt>
                        <Icon name="calendar" />
                        <span className="sr-only">Data proposta</span>
                      </dt>
                      <dd>{numericDate(r.proposedDate)}</dd>
                    </div>
                    {r.location && (
                      <div>
                        <dt>
                          <Icon name="geo" />
                          <span className="sr-only">Local</span>
                        </dt>
                        <dd className="mtg-break">{r.location}</dd>
                      </div>
                    )}
                  </dl>
                  <RequestStatusPill status={r.status} />
                  <div className="mtg-card__actions">
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAction({ kind: 'details', request: r })}>
                      <Icon name="eye" />
                      Ver Detalhes
                    </button>
                    {r.canDecide && (
                      <>
                        <button type="button" className="btn btn--primary btn--sm" onClick={() => decide(r, true)} disabled={busy === r.id} title="Aceitar">
                          <Icon name="checkCircle" />
                          Aceitar
                        </button>
                        <button type="button" className="btn btn--danger btn--sm" onClick={() => decide(r, false)} disabled={busy === r.id} title="Rejeitar">
                          <Icon name="xCircle" />
                          Rejeitar
                        </button>
                      </>
                    )}
                  </div>
                </article>
              </li>
            ))}
          </ul>
          <Pager
            total={data.total}
            itemLabel="pedidos"
            page={data.page}
            pages={pages}
            pageSize={data.pageSize}
            sizes={REQUEST_PAGE_SIZES}
            onPage={setPage}
            onPageSize={(s) => (setPageSize(s), setPage(1))}
          />
        </>
      )}

      {action?.kind === 'details' && <RequestDetailsDialog request={action.request} onClose={() => setAction(undefined)} />}
      {action?.kind === 'delete' && (
        <ConfirmDialog
          title="Confirmar Eliminação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={() => setAction(undefined)}
          onConfirm={async () => {
            const o = await meetingsApi.removeRequest(action.request.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar o pedido');
            reload();
          }}
        >
          <p>
            Tem a certeza que quer eliminar o pedido <strong>{action.request.title}</strong>?
          </p>
          <p className="note">Não é possível desfazer.</p>
        </ConfirmDialog>
      )}
      {action?.kind === 'remind' && (
        <ConfirmDialog
          title="Enviar Lembrete"
          confirmLabel="Enviar"
          confirmIcon="bell"
          onClose={() => setAction(undefined)}
          onConfirm={async () => {
            const o = await meetingsApi.remind(action.request.id);
            if (o.kind !== 'ok') return problem(o, 'enviar o lembrete');
            setSuccess('Notificação Enviada!');
          }}
        >
          <p>
            Tem a certeza que quer enviar um lembrete sobre o pedido <strong>{action.request.title}</strong>?
          </p>
          <p className="note">Será enviada uma notificação push para os responsáveis.</p>
        </ConfirmDialog>
      )}
    </section>
  );
}
