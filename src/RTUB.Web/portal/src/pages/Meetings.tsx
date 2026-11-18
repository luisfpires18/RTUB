import { useEffect, useId, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingBoard } from '../data/meetingsTypes';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { RequestsSection } from '../components/MeetingRequests';
import { type Open, Dialogs } from '../components/MeetingDialogHost';
import { Empty, MeetingSection } from '../components/MeetingSection';
import { Loading } from '../components/ui/Loading';

const UPCOMING_EMPTY =
  'Não existem reuniões agendadas no momento. As assembleias gerais podem ser convocadas por iniciativa da Mesa da Assembleia Geral, ou a pedido da Direcção, do Conselho Fiscal, do Conselho de Veteranos, ou de pelo menos 20% dos Associados Efectivos.';

/** The proposals the page offers ("Propor ..." buttons): CV, Direção and, as "Assembleia Geral", an AGE. */
const PROPOSAL_TYPES = {
  cv: 'ConselhoVeteranos',
  direcao: 'ReuniaoDirecao',
  ag: 'AssembleiaGeralExtraordinaria',
} as const;

export type Flash = { tone: 'ok' | 'warn'; text: string };

/**
 * /meetings - Reuniões. Meetings and assemblies: the upcoming and past ones the
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

  // ?meeting={id} (a push): open that meeting's details once the board has it, then drop the parameter.
  // A meeting the member cannot see, or not on this board, just leaves the list.
  useEffect(() => {
    if (!board || typeof board !== 'object') return;
    const url = new URL(location.href);
    const id = Number(url.searchParams.get('meeting'));
    // From "As minhas ações pendentes": &ata=1 opens the ata editor when the member may write it, and
    // ?new={type} the "Nova reunião" form with that type when the member may create it. The server decides again on save.
    const wantsAta = url.searchParams.get('ata') === '1';
    const newType = url.searchParams.get('new');
    if (!id && !newType) return;
    const card = id ? [...board.upcoming, ...board.past].find((c) => c.id === id) : undefined;
    if (card) setOpen(wantsAta && card.can.writeAta ? { kind: 'ata', card } : { kind: 'details', card });
    else if (newType && board.access.canCreate && board.access.meetingTypes.some((t) => t.value === newType)) {
      setOpen({ kind: 'form', draft: { type: newType, title: '', date: '', location: null, statement: '' } });
    }
    for (const key of ['meeting', 'ata', 'new']) url.searchParams.delete(key);
    history.replaceState(null, '', url.pathname + url.search);
  }, [board]);

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
      <header className="page__head events-page__head mtg-head">
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
            <a className="btn btn--ghost btn--sm" href={portal.meetingPolls}>
              <Icon name="chart" />
              Votações
            </a>
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
        <SignInNotice path={portal.meetings}>As reuniões são da área de membros.</SignInNotice>
      ) : board === null ? (
        <RetryNotice onRetry={reload}>Não conseguimos abrir as reuniões agora.</RetryNotice>
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
