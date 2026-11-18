// The ask-a-question dialog of /questions.
import { useId, useMemo, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MAX_CONTENT, MAX_TITLE, MIN_CONTENT, type Recipient, api, fold, problem } from '../data/questionsApi';
import { Avatar } from './ui/Avatar';

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
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            <Icon name="send" />
            Enviar pergunta
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        <fieldset className={errors.recipientId ? 'form__field form__field--error qs-recipient' : 'form__field qs-recipient'}>
          <legend>Para quem</legend>
          {recipients.length === 0 ? (
            <p className="note">Não há membros nos Órgãos Sociais neste momento.</p>
          ) : chosen ? (
            <div className="qs-chosen">
              <span className="qs-person">
                <Avatar className="who__avatar" src={chosen.avatarUrl} size={36} />
                <span>
                  <strong>{chosen.displayName}</strong>
                  <small className="note">{chosen.role}</small>
                </span>
              </span>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setChosen(undefined)}>
                Mudar
              </button>
            </div>
          ) : (
            <>
              <label className="control" htmlFor={`${id}-who`}>
                <span className="sr-only">Procurar por nome, alcunha ou cargo</span>
                <Icon name="search" />
                <input
                  id={`${id}-who`}
                  type="search"
                  placeholder="Nome, alcunha ou cargo…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>
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
          {errors.recipientId && (
            <p className="form__error" role="alert">
              {errors.recipientId}
            </p>
          )}
        </fieldset>

        <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input
            id={`${id}-title`}
            type="text"
            maxLength={MAX_TITLE}
            placeholder="Resumo da pergunta"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            aria-invalid={errors.title ? true : undefined}
          />
          <p className="form__hint">
            {title.length} / {MAX_TITLE}
          </p>
          {errors.title && <p className="form__error">{errors.title}</p>}
        </div>
        <div className={errors.content ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-content`}>A pergunta</label>
          <textarea
            id={`${id}-content`}
            rows={6}
            maxLength={MAX_CONTENT}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            aria-invalid={errors.content ? true : undefined}
          />
          <p className="form__hint">Pelo menos {MIN_CONTENT} caracteres.</p>
          {errors.content && <p className="form__error">{errors.content}</p>}
        </div>
      </form>
    </Dialog>
  );
}
