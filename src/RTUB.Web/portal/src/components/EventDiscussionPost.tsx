// One discussion post with its editor, lift offer and comments.

import { useId, useState } from 'react';
import { eventsApi, type EventPost, type Outcome, type EventDiscussion as Discussion } from '../data/eventsApi';
import { Icon } from './icons';
import { BODY_MAX, problem, Text, showsTitle, Who, submitOnCtrlEnter } from './EventDiscussionShared';
import { Lift } from './EventDiscussionLift';
import { Comments } from './EventDiscussionComments';

// ---------- one post ----------

export function Post({ eventId, post, canModerate, onChange }: { eventId: number; post: EventPost; canModerate: boolean; onChange: (d: Discussion) => void }) {
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      done?.();
      onChange(o.data);
    } else setError(problem(o));
  };

  return (
    <article className={`talk__post${post.pinned ? ' talk__post--pinned' : ''}${post.transport ? ' talk__post--lift' : ''}`} aria-label={post.title}>
      <Who author={post.author} at={post.createdAt} edited={post.edited}>
        <div className="talk__flags">
          {post.pinned && (
            <span className="talk__flag" title="Fixado no topo">
              <Icon name="pin" />
              <span className="sr-only">Fixado</span>
            </span>
          )}
          {post.locked && (
            <span className="talk__flag" title="Comentários fechados">
              <Icon name="lock" />
              <span className="sr-only">Fechado</span>
            </span>
          )}
        </div>
      </Who>

      {editing && !post.transport ? (
        <PostEditor
          post={post}
          busy={busy}
          onCancel={() => setEditing(false)}
          onSave={(title, body) => run(eventsApi.editPost(eventId, post.id, title, body), () => setEditing(false))}
        />
      ) : post.transport ? (
        <Lift eventId={eventId} post={post} editing={editing} onDone={() => setEditing(false)} onChange={onChange} />
      ) : (
        <div className="talk__body">
          {showsTitle(post) && <h2 className="talk__title">{post.title}</h2>}
          <Text>{post.body}</Text>
        </div>
      )}

      {(post.canEdit || post.canDelete || canModerate) && !editing && (
        <div className="talk__actions">
          {post.canEdit && (
            <button type="button" className="link-btn" onClick={() => setEditing(true)}>
              <Icon name="pencil" />
              Editar
            </button>
          )}
          {canModerate && (
            <>
              <button type="button" className="link-btn" disabled={busy} onClick={() => run(eventsApi.setPostFlags(eventId, post.id, !post.pinned, post.locked))}>
                <Icon name="pin" />
                {post.pinned ? 'Desafixar' : 'Fixar'}
              </button>
              <button type="button" className="link-btn" disabled={busy} onClick={() => run(eventsApi.setPostFlags(eventId, post.id, post.pinned, !post.locked))}>
                <Icon name={post.locked ? 'unlock' : 'lock'} />
                {post.locked ? 'Reabrir comentários' : 'Fechar comentários'}
              </button>
            </>
          )}
          {post.canDelete &&
            (confirming ? (
              <span className="talk__confirm">
                Apagar esta publicação?
                <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(eventsApi.deletePost(eventId, post.id))}>
                  Apagar
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(false)}>
                  Cancelar
                </button>
              </span>
            ) : (
              <button type="button" className="link-btn link-btn--danger" onClick={() => setConfirming(true)}>
                <Icon name="trash" />
                Apagar
              </button>
            ))}
        </div>
      )}
      {error && <p className="form__error">{error}</p>}

      <Comments eventId={eventId} post={post} onChange={onChange} />
    </article>
  );
}

function PostEditor({ post, busy, onCancel, onSave }: { post: EventPost; busy: boolean; onCancel: () => void; onSave: (title: string, body: string) => void }) {
  const [title, setTitle] = useState(showsTitle(post) ? post.title : '');
  const [body, setBody] = useState(post.body);
  const ids = { title: useId(), body: useId() };
  return (
    <form
      className="talk__edit"
      onSubmit={(e) => {
        e.preventDefault();
        onSave(title, body);
      }}
    >
      <label htmlFor={ids.title} className="sr-only">
        Título
      </label>
      <input id={ids.title} className="talk__title-input" placeholder="Título (opcional)" maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} />
      <label htmlFor={ids.body} className="sr-only">
        Mensagem
      </label>
      <textarea id={ids.body} rows={4} maxLength={BODY_MAX} value={body} onChange={(e) => setBody(e.target.value)} onKeyDown={submitOnCtrlEnter} />
      <div className="talk__composer-foot">
        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !body.trim()}>
          Guardar
        </button>
      </div>
    </form>
  );
}
