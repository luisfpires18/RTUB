import { useEffect, useState } from 'react';
import { trintunaApi, problem, type Board, type Gathering } from '../data/trintunaApi';
import { dayOf, monthShort, yearOf, dateLabel } from '../data/eventsDates';
import { GatheringDialog, AccessDialog, TopDialog } from '../components/TrintunaDialogs';
import { GatheringForm } from '../components/TrintunaForm';
import { Icon } from '../components/icons';
import { Notice, RetryNotice, SignInNotice } from '../components/Notice';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';
import { EmptyBlock, EmptyNote } from '../components/ui/EmptyNote';
import { IconAction } from '../components/ui/IconAction';
import { AsyncConfirmDialog } from '../components/ui/ConfirmDialog';

type Tools = { onEdit?: () => void; onDelete?: () => void };

/** Edit (Owner, or the gathering's organizer) and delete (Owner), over the stretched link. */
function ManageButtons({ g, tools }: { g: Gathering; tools: Tools }) {
  if (!tools.onEdit && !tools.onDelete) return null;
  return (
    <span className="manage">
      {tools.onEdit && <IconAction icon="pencil" title="Editar" label={`Editar ${g.title}`} onClick={tools.onEdit} />}
      {tools.onDelete && <IconAction danger icon="trash" title="Apagar" label={`Apagar ${g.title}`} onClick={tools.onDelete} />}
    </span>
  );
}

/** A Próximos card: the agenda-card markup of /events; the whole card opens the gathering. */
function UpcomingCard({ g, onOpen, tools }: { g: Gathering; onOpen: () => void; tools: Tools }) {
  return (
    <article className="agenda-card">
      {g.date ? (
        <time className="agenda-card__date" dateTime={g.date}>
          <span className="agenda-card__day">{dayOf(g.date)}</span>
          <span className="agenda-card__month">{monthShort(g.date)}</span>
          <span className="agenda-card__year">{yearOf(g.date)}</span>
        </time>
      ) : (
        <span className="agenda-card__date">
          <span className="agenda-card__month">Data por decidir</span>
        </span>
      )}
      <div className="agenda-card__body">
        <div className="agenda-card__tags">
          <span className="tag">{g.title}</span>
          {!g.date && !g.poll.closed && g.poll.options.length > 0 && <span className="tag tag--private">Votação aberta</span>}
          <ManageButtons g={g} tools={tools} />
        </div>
        <h3 className="agenda-card__name">
          <a className="agenda-card__link" href={`#almoco-${g.id}`} onClick={(e) => (e.preventDefault(), onOpen())}>
            {g.place}
          </a>
        </h3>
        <p className="agenda-card__meta">
          <Icon name="clock" />
          <span>{g.date ? dateLabel({ date: g.date, endDate: null }) : 'Data por decidir'}</span>
        </p>
        {g.organizer && (
          <p className="agenda-card__meta">
            <Icon name="person" />
            <span>Organiza: {g.organizer.displayName}</span>
          </p>
        )}
        <div className="agenda-card__foot">
          <span className="agenda-card__count">{g.goingCount === 1 ? '1 vai' : `${g.goingCount} vão`}</span>
          <button
            type="button"
            className={`quick-reply quick-reply--${g.myStatus ?? 'none'}`}
            onClick={onOpen}
            aria-label={`${g.myStatus ? 'Alterar resposta' : 'Responder'}: ${g.title}`}
          >
            {g.myStatus === 'going' ? (
              <>
                <Icon name="check" />
                Vais
              </>
            ) : g.myStatus === 'notGoing' ? (
              <>
                <Icon name="close" />
                Não vais
              </>
            ) : (
              'Responder'
            )}
          </button>
        </div>
      </div>
      <Icon name="arrow" className="agenda-card__go" />
    </article>
  );
}

/** A Passados row: the archive-row markup of /events. */
function PastRow({ g, onOpen, tools }: { g: Gathering; onOpen: () => void; tools: Tools }) {
  return (
    <div className="archive-row">
      {g.date ? (
        <time className="archive-row__date" dateTime={g.date}>
          <span>{dayOf(g.date)}</span> {monthShort(g.date)}
        </time>
      ) : (
        <span className="archive-row__date">
          <span>{g.number}º</span>
        </span>
      )}
      <span className="archive-row__main">
        <a className="archive-row__name" href={`#almoco-${g.id}`} onClick={(e) => (e.preventDefault(), onOpen())}>
          {g.title} · {g.place}
        </a>
        <span className="archive-row__meta">
          {[g.description, g.date && yearOf(g.date), g.organizer && `Organizou: ${g.organizer.displayName}`, g.goingCount > 0 && (g.goingCount === 1 ? '1 foi' : `${g.goingCount} foram`)]
            .filter(Boolean)
            .join(' · ')}
        </span>
      </span>
      <span className="archive-row__badges">
        <ManageButtons g={g} tools={tools} />
      </span>
    </div>
  );
}

/**
 * /trintuna - the Trintuna gatherings ("Nº Almoço"). The Owner and the members the Owner lists only: anyone else gets
 * the "no access" notice (the API answers 404). Próximos with their date poll and Vou / Não vou, then Passados
 * (Vou / Não vou too). "Top 3" opens who went to the most almoços.
 * Everything is decided by GET /api/trintuna.
 */
export default function Trintuna() {
  const [board, setBoard] = useState<Board | 'signin' | 'notfound' | null>();
  const [open, setOpen] = useState<number>();
  const [editing, setEditing] = useState<number | 'new'>();
  const [deleting, setDeleting] = useState<Gathering>();
  const [access, setAccess] = useState(false);
  const [top, setTop] = useState(false);

  useEffect(() => {
    document.title = 'Trintuna · RTUB';
  }, []);

  const load = (quiet = false) => {
    if (!quiet) setBoard(undefined);
    trintunaApi.board().then((o) => {
      if (o.kind === 'ok') setBoard(o.data);
      else if (o.kind === 'signin' || o.kind === 'notfound') setBoard(o.kind);
      else if (!quiet) setBoard(null);
    });
  };
  useEffect(() => load(), []);

  const data = board && typeof board === 'object' ? board : undefined;
  const all = data ? [...data.upcoming, ...data.past] : [];
  const find = (id?: number) => all.find((g) => g.id === id);
  const tools = (g: Gathering): Tools => ({
    onEdit: g.canEdit ? () => setEditing(g.id) : undefined,
    onDelete: data?.canManage ? () => setDeleting(g) : undefined,
  });
  const opened = find(open);
  const edited = editing === 'new' ? undefined : find(editing);

  const remove = async () => {
    if (!deleting) return;
    const o = await trintunaApi.remove(deleting.id);
    if (o.kind !== 'ok') return problem(o, 'apagar o almoço');
    setDeleting(undefined);
    load(true);
  };

  return (
    <section className="page wrap events-page" aria-labelledby="trintuna-title">
      <PageHead eyebrow="Tuna" titleId="trintuna-title" title="Trintuna" lead="Os almoços da Trintuna: onde, quando e quem vai.">
        {data && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setTop(true)}>
              <Icon name="trophy" />
              Top 3
            </button>
            {data.canManage && (
              <>
                <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing('new')}>
                  <Icon name="plus" />
                  Novo almoço
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAccess(true)}>
                  <Icon name="people" />
                  Quem tem acesso
                </button>
              </>
            )}
          </div>
        )}
      </PageHead>

      {board === undefined ? (
        <Loading label="A carregar os almoços…" />
      ) : board === 'signin' ? (
        <SignInNotice path="/trintuna">Entre para ver os almoços da Trintuna.</SignInNotice>
      ) : board === 'notfound' ? (
        <Notice>Não tem acesso a esta página.</Notice>
      ) : board === null ? (
        <RetryNotice onRetry={() => load()}>Não conseguimos abrir os almoços neste momento.</RetryNotice>
      ) : (
        <>
          <section className="events-block" aria-labelledby="upcoming-title">
            <h2 id="upcoming-title" className="events-block__title">
              Próximos
            </h2>
            {board.upcoming.length === 0 ? (
              <EmptyBlock className="upcoming-empty" icon="calendar">
                <p>Sem almoços marcados por agora.</p>
              </EmptyBlock>
            ) : (
              <ul className="agenda">
                {board.upcoming.map((g) => (
                  <li key={g.id}>
                    <UpcomingCard g={g} onOpen={() => setOpen(g.id)} tools={tools(g)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="events-block" aria-labelledby="past-title">
            <h2 id="past-title" className="events-block__title">
              Passados
            </h2>
            {board.past.length === 0 ? (
              <EmptyNote>Ainda não há almoços passados.</EmptyNote>
            ) : (
              <div className="archive">
                <ul className="archive__list">
                  {board.past.map((g) => (
                    <li key={g.id}>
                      <PastRow g={g} onOpen={() => setOpen(g.id)} tools={tools(g)} />
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
        </>
      )}

      {opened && <GatheringDialog gathering={opened} onClose={() => setOpen(undefined)} onChanged={() => load(true)} />}
      {editing !== undefined && (editing === 'new' || edited) && (
        <GatheringForm gathering={edited} canManage={Boolean(data?.canManage)} onClose={() => setEditing(undefined)} onSaved={() => load(true)} />
      )}
      {access && <AccessDialog onClose={() => setAccess(false)} onSaved={() => load(true)} />}
      {top && data && <TopDialog top={data.top} onClose={() => setTop(false)} />}
      {deleting && (
        <AsyncConfirmDialog title="Apagar almoço" confirmLabel="Apagar" confirmIcon="trash" danger onClose={() => setDeleting(undefined)} onConfirm={remove}>
          <p>
            O {deleting.title} ({deleting.place}) é apagado com as datas possíveis, os votos e as respostas. Não é possível recuperá-lo.
          </p>
        </AsyncConfirmDialog>
      )}
    </section>
  );
}
