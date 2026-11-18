// Create/edit and delete dialogs for a news post.
import { Banner } from './ui/Banner';
import { Spinner } from './ui/Spinner';
import { useId, useState, type FormEvent } from 'react';
import { type NewsPost } from '../data/api';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { api, problem, TITLE_MAX, BODY_MAX } from '../data/newsApi';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

/** "Nova publicação" (save as a draft or publish now) / "Editar publicação" (title and text; the state stays). */
export function FormDialog({ post, onClose, onSaved }: { post?: NewsPost; onClose: () => void; onSaved: () => void }) {
  const [title, setTitle] = useState(post?.title ?? '');
  const [body, setBody] = useState(post?.body ?? '');
  // A new post has no audience until the writer picks Público or Apenas RTUB; an edit keeps its own.
  const [membersOnly, setMembersOnly] = useState<boolean | undefined>(post?.membersOnly);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const save = async (publish: boolean) => {
    if (!body.trim()) return setErrors({ body: 'Escreve o texto da publicação.' });
    if (membersOnly === undefined) return setErrors({ membersOnly: 'Escolhe quem pode ver: Público ou Apenas RTUB.' });
    setBusy(true);
    setBanner(undefined);
    const input = { title, body, publish, membersOnly };
    const o = post ? await api.update(post.id, input) : await api.create(input);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved();
      onClose();
    } else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'guardar'));
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    save(!post);
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={post ? 'Editar publicação' : 'Nova publicação'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          {!post && (
            <button type="button" className="btn btn--ghost" onClick={() => save(false)} disabled={busy}>
              Guardar rascunho
            </button>
          )}
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <Spinner small />}
            {post ? 'Guardar' : 'Publicar'}
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={banner} />
        <div className={field('title')}>
          <label htmlFor={`${fid}-title`}>Título (opcional)</label>
          <input id={`${fid}-title`} type="text" maxLength={TITLE_MAX} value={title} onChange={(e) => setTitle(e.target.value)} />
          {error('title')}
        </div>
        <div className={field('body')}>
          <label htmlFor={`${fid}-body`}>Texto</label>
          <textarea
            id={`${fid}-body`}
            rows={8}
            maxLength={BODY_MAX}
            value={body}
            aria-describedby={`${fid}-count`}
            onChange={(e) => {
              setBody(e.target.value);
              if (errors.body) setErrors({ ...errors, body: '' });
            }}
          />
          <p id={`${fid}-count`} className="form__hint">
            {body.length} / {BODY_MAX} caracteres
          </p>
          {error('body')}
        </div>
        <fieldset className={field('membersOnly')}>
          <legend>Quem pode ver</legend>
          <label className="form__check">
            <input type="radio" name={`${fid}-audience`} checked={membersOnly === false} onChange={() => setMembersOnly(false)} />
            Público: qualquer visitante vê esta publicação
          </label>
          <label className="form__check">
            <input type="radio" name={`${fid}-audience`} checked={membersOnly === true} onChange={() => setMembersOnly(true)} />
            Apenas RTUB: só membros com sessão iniciada
          </label>
          {error('membersOnly')}
        </fieldset>
        {!post && <p className="note">Um rascunho não é visível para os membros até ser publicado.</p>}
      </form>
    </Dialog>
  );
}

export function DeleteDialog({ post, onClose, onDone }: { post: NewsPost; onClose: () => void; onDone: () => void }) {

  const remove = async () => {
    const o = await api.remove(post.id);
    if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, 'eliminar');
    onDone();
  };

  return (
    <AsyncConfirmDialog title="Eliminar publicação" confirmLabel="Eliminar" danger onConfirm={remove} onClose={onClose}>
      <p>
        Eliminar <strong>{post.title ?? 'esta publicação'}</strong>
        {post.publishedAt ? ' das Novidades' : ''}?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Não dá para desfazer.
      </p>
    </AsyncConfirmDialog>
  );
}
