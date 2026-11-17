import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { ConfirmDialog, problem } from './MeetingDialogs';
import { Icon } from './icons';
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

export default function MeetingPolls() {
  const [version, setVersion] = useState(0);
  const [board, setBoard] = useState<MeetingPollBoard | null>();
  const [createOpen, setCreateOpen] = useState(false);
  const [error, setError] = useState<string>();
  const titleId = useId();

  useEffect(() => {
    let live = true;
    meetingsApi.polls().then((outcome) => {
      if (!live) return;
      setBoard(outcome.kind === 'ok' ? outcome.data : null);
      if (outcome.kind !== 'ok') setError(pollProblem(outcome, 'carregar as votações'));
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

  return (
    <section className="mtg-section mtg-polls" aria-labelledby={titleId}>
      <div className="mtg-polls__heading">
        <h2 id={titleId} className="mtg-section__title">
          <Icon name="chart" />
          Votações
        </h2>
        {board?.createAudiences.length ? (
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setCreateOpen(true)}>
            <Icon name="plus" />
            Criar votação
          </button>
        ) : null}
      </div>

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
      ) : board === null ? null : board.polls.length === 0 ? (
        <div className="mtg-empty">
          <Icon name="chart" className="mtg-empty__icon" />
          <p className="mtg-empty__title">Ainda não existem votações disponíveis.</p>
        </div>
      ) : (
        <ul className="mtg-polls__grid">
          {board.polls.map((poll) => (
            <li key={poll.id}>
              <PollCard poll={poll} onChanged={refresh} />
            </li>
          ))}
        </ul>
      )}

      {createOpen && <CreatePollDialog audiences={board?.createAudiences ?? []} onClose={() => setCreateOpen(false)} onCreated={created} />}
    </section>
  );
}

function PollCard({ poll, onChanged }: { poll: MeetingPoll; onChanged: () => void }) {
  const [selected, setSelected] = useState<number | null>(poll.myOptionId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmClose, setConfirmClose] = useState(false);

  useEffect(() => setSelected(poll.myOptionId), [poll.myOptionId]);

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

  return (
    <article className="mtg-poll">
      <div className="mtg-poll__top">
        <span className="mtg-type">{poll.audienceLabel}</span>
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
      </div>
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
