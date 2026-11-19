// Leaderboard comments list and delete dialog.
import { ConfirmDialog } from './ui/ConfirmDialog';
import { EmptyNote } from './ui/EmptyNote';
import { FieldError } from './ui/FieldError';
import { Banner } from './ui/Banner';
import { Spinner } from './ui/Spinner';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Icon } from './icons';
import { type Comment, api, MAX_COMMENT, problem, ago } from '../data/leaderboardApi';
import { Loading } from './ui/Loading';
import { Avatar } from './ui/Avatar';
import { IconAction } from './ui/IconAction';

export function Comments({ memberId }: { memberId: string }) {
  const id = useId();
  const [list, setList] = useState<Comment[] | null>();
  const [text, setText] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<Comment>();

  const load = () => api.comments(memberId).then((o) => setList(o.kind === 'ok' ? o.data : null));
  useEffect(() => {
    load();
  }, [memberId]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return setError('Escreva o comentário.');
    setBusy(true);
    const o = await api.comment(memberId, text);
    setBusy(false);
    if (o.kind === 'ok') {
      setList(o.data);
      setText('');
      setError(undefined);
    } else setError(problem(o, 'publicar o comentário'));
  };

  const like = async (c: Comment) => {
    const o = await api.like(c.id);
    if (o.kind === 'ok') load();
    else setError(problem(o, 'gostar do comentário'));
  };

  return (
    <section className="member-section" aria-labelledby={`${id}-title`}>
      <h3 id={`${id}-title`} className="member-section__title">
        Comentários
      </h3>
      <form className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={id} className="sr-only">
            Comentário
          </label>
          <textarea
            id={id}
            rows={3}
            maxLength={MAX_COMMENT}
            placeholder="Deixa um comentário…"
            value={text}
            onChange={(e) => setText(e.target.value)}
            aria-invalid={error ? true : undefined}
          />
          <p className="form__hint">
            {text.length} / {MAX_COMMENT}
          </p>
          <FieldError text={error} />
        </div>
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !text.trim()}>
          {busy && <Spinner small />}
          Publicar
        </button>
      </form>

      {list === undefined ? (
        <Loading label="A carregar os comentários…" />
      ) : list === null ? (
        <Banner text="Não foi possível carregar os comentários." />
      ) : list.length === 0 ? (
        <EmptyNote>Ainda não há comentários.</EmptyNote>
      ) : (
        <ul className="member-rows leaderboard-comments">
          {list.map((c) => (
            <li key={c.id} className="leaderboard-comment">
              <span className="leaderboard-comment__head">
                <Avatar className="who__avatar" src={c.authorAvatarUrl} size={36} />
                <strong>{c.authorName}</strong>
                <small className="note">{ago(c.createdAt)}</small>
                {c.canDelete && (
                  <IconAction icon="trash" label="Eliminar comentário" onClick={() => setDeleting(c)} large danger />
                )}
              </span>
              <p className="leaderboard-comment__text">{c.text}</p>
              <span className="leaderboard-comment__likes">
                <button type="button" className={c.likedByMe ? 'btn btn--ghost btn--sm is-liked' : 'btn btn--ghost btn--sm'} aria-pressed={c.likedByMe} onClick={() => like(c)}>
                  <Icon name="star" />
                  {c.likedByMe ? 'Gostas' : 'Gostar'} · {c.likes}
                </button>
                {c.likedBy.length > 0 && <small className="note">{c.likedBy.join(', ')}</small>}
              </span>
            </li>
          ))}
        </ul>
      )}

      {deleting && <DeleteComment comment={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
    </section>
  );
}

function DeleteComment({ comment, onClose, onDone }: { comment: Comment; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const remove = async () => {
    setBusy(true);
    const o = await api.remove(comment.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar o comentário'));
  };

  return (
    <ConfirmDialog title="Eliminar comentário" confirmLabel="Eliminar" busy={busy} error={error} onConfirm={remove} onClose={onClose}>
      <p>Eliminar o comentário de {comment.authorName}? Deixa de aparecer para todos.</p>
    </ConfirmDialog>
  );
}
