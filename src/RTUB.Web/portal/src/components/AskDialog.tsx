// The ask-a-question dialog of /questions.
import { PersonLine } from './ui/PersonLine';
import { useId, useMemo, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MAX_CONTENT, MAX_TITLE, MIN_CONTENT, type Recipient, api, problem } from '../data/questionsApi';
import { Avatar } from './ui/Avatar';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';
import { FormField, fieldProps } from './ui/FormField';
import { Spinner } from './ui/Spinner';
import { fold } from '../data/format';
import { SearchField } from './ui/SearchTools';

export function AskDialog({ recipients, onClose, onAsked }: { recipients: Recipient[]; onClose: () => void; onAsked: (id: number) => void }) {
  const id = useId();
  const [search, setSearch] = useState('');
  const [chosen, setChosen] = useState<Recipient>();
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);

  const found = useMemo(() => {
    const term = fold(search.trim());
    if (!term) return recipients;
    return recipients.filter((r) => fold(`${r.displayName} ${r.fullName ?? ''} ${r.role}`).includes(term));
  }, [recipients, search]);

  const valid = chosen && title.trim().length > 0 && title.trim().length <= MAX_TITLE && content.trim().length >= MIN_CONTENT;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!chosen) return setErrors({ recipientId: 'Escolha um membro dos Órgãos Sociais.' });
    setBusy(true);
    const o = await api.ask({ title, content, recipientId: chosen.id, position: chosen.position });
    setBusy(false);
    if (o.kind === 'ok') onAsked(o.data.question.id);
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'enviar a pergunta'));
  };

  return (
    <Dialog
      title="Nova pergunta"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !valid}>
            {busy && <Spinner small />}
            <Icon name="send" />
            Enviar pergunta
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={banner} />
        <fieldset className={errors.recipientId ? 'form__field form__field--error qs-recipient' : 'form__field qs-recipient'}>
          <legend>Para quem</legend>
          {recipients.length === 0 ? (
            <p className="note">Não há membros nos Órgãos Sociais neste momento.</p>
          ) : chosen ? (
            <div className="qs-chosen">
              <PersonLine name={chosen.displayName} avatar={chosen.avatarUrl} size={36} note={chosen.role} />
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setChosen(undefined)}>
                Mudar
              </button>
            </div>
          ) : (
            <>
              <SearchField id={`${id}-who`} label="Procurar por nome, alcunha ou cargo" placeholder="Nome, alcunha ou cargo…" value={search} onChange={setSearch} />
              <ul className="qs-pick" aria-label="Membros dos Órgãos Sociais">
                {found.map((r) => (
                  <li key={`${r.id}-${r.position}`}>
                    <button type="button" className="qs-pick__option" onClick={() => setChosen(r)}>
                      <Avatar className="who__avatar" src={r.avatarUrl} size={32} />
                      <span>
                        <strong>{r.displayName}</strong>
                        <small className="note">{r.role}</small>
                      </span>
                    </button>
                  </li>
                ))}
                {found.length === 0 && <li className="note">Ninguém corresponde à pesquisa.</li>}
              </ul>
            </>
          )}
          <FieldError text={errors.recipientId} />
        </fieldset>

        <FormField id={`${id}-title`} label="Título" error={errors.title} hint={`${title.length} / ${MAX_TITLE}`}>
          <input
            {...fieldProps(`${id}-title`, errors.title, 'x')}
            type="text"
            maxLength={MAX_TITLE}
            placeholder="Resumo da pergunta"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />
        </FormField>
        <FormField id={`${id}-content`} label="A pergunta" error={errors.content} hint={`Pelo menos ${MIN_CONTENT} caracteres.`}>
          <textarea
            {...fieldProps(`${id}-content`, errors.content, 'x')}
            rows={6}
            maxLength={MAX_CONTENT}
            value={content}
            onChange={(e) => setContent(e.target.value)}
          />
        </FormField>
      </form>
    </Dialog>
  );
}
