// The create-poll dialog of /meetings/polls.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingPollAudienceOption } from '../data/meetingsTypes';
import { pollProblem } from './MeetingPollCard';

const MIN_OPTIONS = 2;
const MAX_OPTIONS = 12;

export function CreatePollDialog({
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
