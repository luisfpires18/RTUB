import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { ConfirmDialog, problem, typeClass } from './MeetingDialogs';
import { MEETING_PAGE_SIZES, Pager } from './Meetings';
import { Icon, type IconName } from './icons';
import { loginTo } from './musicApi';
import {
  longDateTime,
  meetingsApi,
  type MeetingPoll,
  type MeetingPollAudienceOption,
  type MeetingPollBoard,
} from './meetingsApi';

const MIN_OPTIONS = 2;
const MAX_OPTIONS = 12;

const pollProblem = (outcome: Parameters<typeof problem>[0], what: string) =>
  outcome.kind === 'closed'
    ? 'Esta votação já está encerrada.'
    : outcome.kind === 'notfound'
      ? 'Esta votação já não existe ou deixou de estar disponível.'
      : problem(outcome, what);

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
      <header className="page__head events-page__head">
        <div>
          <a className="back-link" href={portal.meetings}>
            <Icon name="arrowLeft" />
            Reuniões
          </a>
          <h1 id="meeting-polls-title" className="page__title">
            Votações
          </h1>
          <p className="page__lead">Votações das assembleias e dos conselhos, em curso e anteriores.</p>
        </div>
        {data?.createAudiences.length ? (
          <div className="events-page__actions mtg-head-actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" />
              Criar votação
            </button>
          </div>
        ) : null}
      </header>

      {error && (
        <div className="form__banner" role="alert">
          {error}
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => { setError(undefined); refresh(); }}>
            Tentar novamente
          </button>
        </div>
      )}

      {board === undefined ? (
        <Loading label="A carregar as votações…" />
      ) : board === 'signin' ? (
        <div className="notice" role="status">
          <p>As votações são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.meetingPolls + location.search)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
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

function PollCard({ poll, onChanged }: { poll: MeetingPoll; onChanged: () => void }) {
  const [selected, setSelected] = useState<number | null>(poll.myOptionId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmClose, setConfirmClose] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => setSelected(poll.myOptionId), [poll.myOptionId]);

  // The link opens this poll for a signed-in member in its audience; anyone else is asked to sign in or sees nothing.
  const copyLink = async () => {
    const link = `${location.origin}${portal.meetingPolls}?poll=${poll.id}`;
    try {
      await navigator.clipboard.writeText(link);
      setError(undefined);
      setCopied(true);
    } catch {
      // The browser refused the clipboard: show the link to select by hand.
      setError(`Não foi possível copiar. Link: ${link}`);
    }
  };

  const vote = async () => {
    if (selected === null) return;
    setBusy(true);
    setError(undefined);
    const outcome = await meetingsApi.votePoll(poll.id, selected);
    setBusy(false);
    if (outcome.kind !== 'ok') return setError(pollProblem(outcome, 'registar o voto'));
    onChanged();
  };

  const close = async () => {
    setBusy(true);
    const outcome = await meetingsApi.closePoll(poll.id);
    setBusy(false);
    if (outcome.kind !== 'ok') return pollProblem(outcome, 'encerrar a votação');
    onChanged();
  };

  const remove = async () => {
    setBusy(true);
    const outcome = await meetingsApi.removePoll(poll.id);
    setBusy(false);
    if (outcome.kind !== 'ok') return pollProblem(outcome, 'eliminar a votação');
    onChanged();
  };

  return (
    <article className="mtg-poll">
      <div className="mtg-poll__top">
        <span className={typeClass(poll.audience === 'Direcao' ? 'ReuniaoDirecao' : poll.audience)}>{poll.audienceLabel}</span>
        <span className={`mtg-poll__status${poll.closed ? ' is-closed' : ''}`}>{poll.closed ? 'Encerrada' : 'Aberta'}</span>
      </div>
      <h3 className="mtg-poll__question">{poll.question}</h3>
      <p className="mtg-poll__meta">
        {poll.closesAt ? `Termina a ${longDateTime(poll.closesAt)}` : 'Sem data de fecho'}
        {' · '}
        {poll.totalVotes} {poll.totalVotes === 1 ? 'voto' : 'votos'}
      </p>
      <fieldset className="mtg-poll__options" disabled={!poll.canVote || busy}>
        <legend className="sr-only">Opções para {poll.question}</legend>
        {poll.options.map((option) => (
          <label className="mtg-poll__option" key={option.id}>
            {poll.canVote && (
              <input
                type="radio"
                name={`poll-${poll.id}`}
                value={option.id}
                checked={selected === option.id}
                onChange={() => setSelected(option.id)}
              />
            )}
            <span className="mtg-poll__option-text">{option.text}</span>
            <span className="mtg-poll__option-count">{option.votes}</span>
            <progress className="mtg-poll__bar" value={option.votes} max={Math.max(poll.totalVotes, 1)} aria-label={`${option.votes} votos`} />
          </label>
        ))}
      </fieldset>
      {error && <p className="form__error" role="alert">{error}</p>}
      <div className="mtg-poll__actions">
        {poll.canVote && (
          <button type="button" className="btn btn--primary btn--sm" onClick={vote} disabled={selected === null || busy}>
            {busy ? 'A guardar…' : poll.myOptionId === null ? 'Votar' : 'Alterar voto'}
          </button>
        )}
        {poll.closed && poll.myOptionId !== null && <span className="note">O seu voto foi registado.</span>}
        {poll.canManage && !poll.closed && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirmClose(true)} disabled={busy}>
            <Icon name="lock" />
            Encerrar votação
          </button>
        )}
        <button type="button" className="btn btn--ghost btn--sm" onClick={copyLink}>
          <Icon name={copied ? 'check' : 'link'} />
          <span role="status">{copied ? 'Link copiado' : 'Copiar link'}</span>
        </button>
        {poll.canDelete && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirmDelete(true)} disabled={busy}>
            <Icon name="trash" />
            Eliminar
          </button>
        )}
      </div>
      {confirmDelete && (
        <ConfirmDialog
          title="Eliminar votação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={() => setConfirmDelete(false)}
          onConfirm={remove}
        >
          <p>
            A votação "{poll.question}" é eliminada com as suas opções
            {poll.totalVotes > 0 ? ` e ${poll.totalVotes === 1 ? 'o voto já registado' : `os ${poll.totalVotes} votos já registados`}` : ''}. Não
            é possível recuperá-la.
          </p>
        </ConfirmDialog>
      )}
      {confirmClose && (
        <ConfirmDialog
          title="Encerrar votação"
          confirmLabel="Encerrar"
          confirmIcon="lock"
          danger
          onClose={() => setConfirmClose(false)}
          onConfirm={close}
        >
          <p>Depois de encerrada, esta votação deixa de aceitar alterações ou novos votos.</p>
        </ConfirmDialog>
      )}
    </article>
  );
}

function CreatePollDialog({
  audiences,
  onClose,
  onCreated,
}: {
  audiences: MeetingPollAudienceOption[];
  onClose: () => void;
  onCreated: () => void;
}) {
  const [audience, setAudience] = useState(audiences[0]?.value ?? '');
  const [question, setQuestion] = useState('');
  const [options, setOptions] = useState(['', '']);
  const [closesAt, setClosesAt] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const questionId = useId();
  const closeId = useId();

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    const outcome = await meetingsApi.createPoll({
      audience,
      question,
      options,
      closesAt: closesAt || null,
    });
    setBusy(false);
    if (outcome.kind !== 'ok') return setError(pollProblem(outcome, 'criar a votação'));
    onCreated();
  };

  const updateOption = (index: number, value: string) =>
    setOptions((current) => current.map((option, i) => (i === index ? value : option)));

  return (
    <Dialog
      title="Criar votação"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>Cancelar</button>
          <button type="submit" form="meeting-poll-form" className="btn btn--primary" disabled={busy}>
            <Icon name="check" />
            {busy ? 'A criar…' : 'Criar votação'}
          </button>
        </>
      }
    >
      <form id="meeting-poll-form" className="form" onSubmit={submit}>
        {error && <p className="form__banner" role="alert">{error}</p>}
        <label className="form__field">
          <span>Órgão</span>
          <select value={audience} onChange={(event) => setAudience(event.target.value)} required>
            {audiences.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
        </label>
        <label className="form__field" htmlFor={questionId}>
          <span>Pergunta</span>
          <input id={questionId} value={question} onChange={(event) => setQuestion(event.target.value)} maxLength={300} required />
        </label>
        <fieldset className="mtg-poll__create-options">
          <legend>Opções (datas, temas ou outras hipóteses)</legend>
          {options.map((option, index) => (
            <div className="mtg-poll__option-input" key={index}>
              <label className="form__field">
                <span className="sr-only">Opção {index + 1}</span>
                <input
                  value={option}
                  onChange={(event) => updateOption(index, event.target.value)}
                  maxLength={200}
                  required
                  aria-label={`Opção ${index + 1}`}
                />
              </label>
              {options.length > MIN_OPTIONS && (
                <button type="button" className="icon-btn icon-btn--danger" onClick={() => setOptions((current) => current.filter((_, i) => i !== index))} aria-label={`Remover opção ${index + 1}`}>
                  <Icon name="trash" />
                </button>
              )}
            </div>
          ))}
          {options.length < MAX_OPTIONS && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOptions((current) => [...current, ''])}>
              <Icon name="plus" />
              Adicionar opção
            </button>
          )}
        </fieldset>
        <label className="form__field" htmlFor={closeId}>
          <span>Data de fecho (opcional)</span>
          <input id={closeId} type="datetime-local" value={closesAt} onChange={(event) => setClosesAt(event.target.value)} />
        </label>
        <p className="note">Cada membro pode escolher uma opção, alterar o voto enquanto a votação estiver aberta e consultar os totais. Os votos não mostram quem escolheu cada opção.</p>
      </form>
    </Dialog>
  );
}
