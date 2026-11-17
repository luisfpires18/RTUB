// Leaderboard comments list and delete dialog.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { type Comment, api, MAX_COMMENT, problem, ago } from '../data/leaderboardApi';

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
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !text.trim()}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Publicar
        </button>
      </form>

      {list === undefined ? (
        <Loading label="A carregar os comentários…" />
      ) : list === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar os comentários.
        </p>
      ) : list.length === 0 ? (
        <p className="note">Ainda não há comentários.</p>
      ) : (
        <ul className="member-rows leaderboard-comments">
          {list.map((c) => (
            <li key={c.id} className="leaderboard-comment">
              <span className="leaderboard-comment__head">
                <MemberFace avatarUrl={c.authorAvatarUrl} size={36} />
                <strong>{c.authorName}</strong>
                <small className="note">{ago(c.createdAt)}</small>
                {c.canDelete && (
                  <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting(c)}>
                    <Icon name="trash" />
                    <span className="sr-only">Eliminar comentário</span>
                  </button>
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
    <Dialog
      title="Eliminar comentário"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>Eliminar o comentário de {comment.authorName}? Deixa de aparecer para todos.</p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
