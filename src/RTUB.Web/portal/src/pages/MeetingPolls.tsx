import { useEffect, useId, useState } from 'react';
import { portal } from '../data/content';
import { MEETING_PAGE_SIZES } from '../data/meetingsTypes';
import { Icon, type IconName } from '../components/icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingPoll, MeetingPollBoard } from '../data/meetingsTypes';
import { SignInNotice } from '../components/Notice';
import { pollProblem, PollCard } from '../components/MeetingPollCard';
import { CreatePollDialog } from '../components/MeetingPollCreateDialog';
import { Loading } from '../components/ui/Loading';
import { Pager } from '../components/ui/Pager';
import { BackLink } from '../components/ui/BackLink';
import { PageHead } from '../components/ui/PageHead';
import { Banner } from '../components/ui/Banner';

/**
 * /meetings/polls: the Votações of the meeting bodies, open ones and closed ones in two paged grids. The server decides
 * which polls the member sees and what they may do (MeetingPollBoardService); search, body filter and paging are local.
 */
export default function MeetingPolls() {
  const [version, setVersion] = useState(0);
  const [board, setBoard] = useState<MeetingPollBoard | 'signin' | null>();
  const [createOpen, setCreateOpen] = useState(false);
  const [error, setError] = useState<string>();
  const [text, setText] = useState('');
  const [audience, setAudience] = useState('all');
  const [openPage, setOpenPage] = useState(1);
  const [openSize, setOpenSize] = useState(12);
  const [closedPage, setClosedPage] = useState(1);
  const [closedSize, setClosedSize] = useState(12);
  const ids = { q: useId(), audience: useId(), open: useId(), closed: useId() };
  // ?poll={id} (a copied link): show that poll alone. The board only holds what the server lets this member see, so a
  // poll outside their audience is simply not there.
  const [linked, setLinked] = useState<number | null>(() => Number(new URLSearchParams(location.search).get('poll')) || null);
  const showAll = () => {
    setLinked(null);
    history.replaceState(null, '', location.pathname);
  };

  useEffect(() => {
    document.title = 'Votações · RTUB';
  }, []);

  useEffect(() => {
    let live = true;
    meetingsApi.polls().then((outcome) => {
      if (!live) return;
      setBoard(outcome.kind === 'ok' ? outcome.data : outcome.kind === 'signin' ? 'signin' : null);
      if (outcome.kind !== 'ok' && outcome.kind !== 'signin') setError(pollProblem(outcome, 'carregar as votações'));
    });
    return () => {
      live = false;
    };
  }, [version]);

  const refresh = () => setVersion((value) => value + 1);
  const created = () => {
    setCreateOpen(false);
    setError(undefined);
    refresh();
  };

  const data = board && board !== 'signin' ? board : undefined;
  // The filter offers only the bodies the member actually has polls for.
  const audiences = [...new Map((data?.polls ?? []).map((poll) => [poll.audience, poll.audienceLabel])).entries()];
  const query = text.trim().toLowerCase();
  const polls = (data?.polls ?? []).filter((poll) =>
    linked !== null
      ? poll.id === linked
      : (audience === 'all' || poll.audience === audience) && (!query || poll.question.toLowerCase().includes(query)),
  );

  return (
    <section className="page wrap events-page mtg-page" aria-labelledby="meeting-polls-title">
      <PageHead
        before={
          <BackLink href={portal.meetings} icon="arrowLeft">
            Reuniões
          </BackLink>
        }
        title="Votações"
        titleId="meeting-polls-title"
        lead="Votações das assembleias e dos conselhos, em curso e anteriores."
      >
        {data?.createAudiences.length ? (
          <div className="events-page__actions mtg-head-actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" />
              Criar votação
            </button>
          </div>
        ) : null}
      </PageHead>

      {error && (
        <Banner block text={error}>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => { setError(undefined); refresh(); }}>
            Tentar novamente
          </button>
        </Banner>
      )}

      {board === undefined ? (
        <Loading label="A carregar as votações…" />
      ) : board === 'signin' ? (
        <SignInNotice path={portal.meetingPolls + location.search}>As votações são da área de membros.</SignInNotice>
      ) : !data ? null : (
        <>
          {linked !== null && (
            <div className="notice" role="status">
              <p>{polls.length ? 'A mostrar a votação do link.' : 'Esta votação não existe ou não tem acesso.'}</p>
              <button type="button" className="btn btn--ghost btn--sm" onClick={showAll}>
                Ver todas as votações
              </button>
            </div>
          )}
          {linked === null && (
          <div className="events-tools mtg-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Pesquisar votações</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Pesquisar por pergunta..." value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            {audiences.length > 1 && (
              <label className="control control--select" htmlFor={ids.audience}>
                <span className="sr-only">Órgão</span>
                <select id={ids.audience} value={audience} onChange={(e) => setAudience(e.target.value)}>
                  <option value="all">Todos os órgãos</option>
                  {audiences.map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>
          )}

          <PollSection
            id={ids.open}
            icon="chart"
            title="Votações em curso"
            itemLabel="votações em curso"
            polls={polls.filter((poll) => !poll.closed)}
            page={openPage}
            pageSize={openSize}
            onPage={setOpenPage}
            onPageSize={(size) => (setOpenSize(size), setOpenPage(1))}
            empty="Nenhuma votação em curso."
            onChanged={refresh}
          />

          <PollSection
            id={ids.closed}
            icon="lock"
            title="Votações anteriores"
            itemLabel="votações anteriores"
            polls={polls.filter((poll) => poll.closed)}
            page={closedPage}
            pageSize={closedSize}
            onPage={setClosedPage}
            onPageSize={(size) => (setClosedSize(size), setClosedPage(1))}
            empty="Nenhuma votação anterior."
            onChanged={refresh}
          />
        </>
      )}

      {createOpen && <CreatePollDialog audiences={data?.createAudiences ?? []} onClose={() => setCreateOpen(false)} onCreated={created} />}
    </section>
  );
}

/** One paged grid of polls: the same shape as the meetings sections (Pager and page sizes included). */
function PollSection({
  id,
  icon,
  title,
  itemLabel,
  polls,
  page,
  pageSize,
  onPage,
  onPageSize,
  empty,
  onChanged,
}: {
  id: string;
  icon: IconName;
  title: string;
  itemLabel: string;
  polls: MeetingPoll[];
  page: number;
  pageSize: number;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
  empty: string;
  onChanged: () => void;
}) {
  const pages = Math.max(1, Math.ceil(polls.length / pageSize));
  const current = Math.min(page, pages);
  const shown = polls.slice((current - 1) * pageSize, current * pageSize);

  return (
    <section className="mtg-section" aria-labelledby={id}>
      <h2 id={id} className="mtg-section__title">
        <Icon name={icon} />
        {title}
      </h2>
      {polls.length === 0 ? (
        <div className="mtg-empty">
          <Icon name="chart" className="mtg-empty__icon" />
          <p className="mtg-empty__title">{empty}</p>
        </div>
      ) : (
        <>
          <ul className="mtg-polls__grid">
            {shown.map((poll) => (
              <li key={poll.id}>
                <PollCard poll={poll} onChanged={onChanged} />
              </li>
            ))}
          </ul>
          {polls.length > MEETING_PAGE_SIZES[0] && (
            <Pager
              total={polls.length}
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
