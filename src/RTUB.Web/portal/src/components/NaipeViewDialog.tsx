// Naipes item view dialog with comments.
import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import type { Outcome } from '../data/api';
import { naipesApi, type NaipeComment, type NaipeItem } from '../data/naipesApi';
import { MemberBadge } from './MemberBadge';
import { MAX_COMMENT, problem } from '../data/naipesShared';
import { Loading } from './ui/Loading';
import { Avatar } from './ui/Avatar';
import { FieldError } from './ui/FieldError';
import { Spinner } from './ui/Spinner';
import { EmptyNote } from './ui/EmptyNote';
import { IconAction } from './ui/IconAction';

/** One item: the video or image, its description and numbers, and the comments. */
export function ViewDialog({ item, onClose, onChanged }: { item: NaipeItem; onClose: () => void; onChanged: () => void }) {
  const [comments, setComments] = useState<NaipeComment[] | null>();
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const played = useRef(false);
  const id = useId();

  useEffect(() => {
    naipesApi.comments(item.id).then((o) => setComments(o.kind === 'ok' ? o.data : null));
  }, [item.id]);

  const onPlay = () => {
    if (played.current) return; // one play per opening
    played.current = true;
    naipesApi.played(item.id).then((o) => o.kind === 'ok' && onChanged());
  };

  const run = async (action: () => Promise<Outcome<NaipeComment[]>>, what: string) => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok') {
      setComments(o.data);
      setError(undefined);
      onChanged();
      return true;
    }
    setError(problem(o, what));
    return false;
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!draft.trim()) return;
    if (await run(() => naipesApi.addComment(item.id, draft.trim()), 'publicar o comentário')) setDraft('');
  };

  return (
    <Dialog title={item.title} size="lg" onClose={onClose}>
      <div className="naipe-view">
        <span className="member-badges">
          <MemberBadge kind="position">{item.instrumentLabel}</MemberBadge>
          <MemberBadge>{item.isVideo ? 'Vídeo' : 'Imagem'}</MemberBadge>
        </span>
        {item.isVideo ? (
          <video className="naipe-view__media" controls playsInline preload="metadata" onPlay={onPlay}>
            <source src={item.url} type={item.mimeType} />
            {item.mimeType === 'video/quicktime' && <source src={item.url} type="video/mp4" />}
            Este navegador não reproduz este formato de vídeo (gravações de iPhone em HEVC podem precisar do Safari).
          </video>
        ) : (
          <img className="naipe-view__media" src={item.url} alt={item.title} />
        )}
        {item.description && <p className="naipe-view__text">{item.description}</p>}
        <p className="note">
          {item.isVideo && `${item.playCount} ${item.playCount === 1 ? 'reprodução' : 'reproduções'} · `}Adicionado por {item.createdBy}
        </p>

        <section className="naipe-comments" aria-labelledby={`${id}-title`}>
          <h3 id={`${id}-title`} className="member-section__title">
            Comentários{comments ? ` (${comments.length})` : ''}
          </h3>
          <form className="form naipe-comments__form" onSubmit={submit}>
            <label htmlFor={`${id}-text`} className="sr-only">
              Comentário
            </label>
            <textarea
              id={`${id}-text`}
              rows={2}
              maxLength={MAX_COMMENT}
              placeholder="Escreve um comentário…"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
            />
            <span className="naipe-comments__row">
              <small className="note">
                {draft.length} / {MAX_COMMENT}
              </small>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !draft.trim()}>
                {busy && <Spinner small />}
                Publicar
              </button>
            </span>
          </form>
          <FieldError text={error} />
          {comments === undefined ? (
            <Loading label="A carregar os comentários…" />
          ) : comments === null ? (
            <p className="note">Não foi possível carregar os comentários.</p>
          ) : comments.length === 0 ? (
            <EmptyNote>Ainda não há comentários.</EmptyNote>
          ) : (
            <ul className="naipe-comments__list">
              {comments.map((c) => (
                <li key={c.id} className="naipe-comment">
                  <Avatar className="who__avatar" src={c.authorAvatarUrl || null} size={36} />
                  <span className="naipe-comment__body">
                    <span className="naipe-comment__who">
                      <strong>{c.authorName}</strong>
                      <small>{new Date(c.createdAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</small>
                    </span>
                    <span className="naipe-comment__text">{c.text}</span>
                  </span>
                  {c.canDelete && (
                    <IconAction icon="trash" label="Eliminar comentário" onClick={() => run(() => naipesApi.removeComment(item.id, c.id), 'eliminar o comentário')} large danger disabled={busy} />
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </Dialog>
  );
}
