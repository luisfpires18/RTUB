// The comments under a discussion post.

import { useId, useRef, useState } from 'react';
import { eventsApi, type EventPost, type Outcome, type EventDiscussion as Discussion } from '../data/eventsApi';
import { Icon } from './icons';
import { FieldError } from './ui/FieldError';
import { COMMENT_MAX, problem, Text, Who, submitOnCtrlEnter } from './EventDiscussionShared';

// ---------- comments ----------

export function Comments({ eventId, post, onChange }: { eventId: number; post: EventPost; onChange: (d: Discussion) => void }) {
  const [body, setBody] = useState('');
  const [editing, setEditing] = useState<{ id: number; body: string }>();
  const [confirming, setConfirming] = useState<number>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const inputId = useId();
  const box = useRef<HTMLTextAreaElement>(null);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setConfirming(undefined);
    if (o.kind === 'ok') {
      done?.();
      onChange(o.data);
    } else setError(problem(o));
  };

  return (
    <div className="talk__comments">
      {post.comments.length > 0 && (
        <ol className="talk__thread" aria-label={`Comentários (${post.comments.length})`}>
          {post.comments.map((c) => (
            <li key={c.id} className="talk__comment">
              <Who author={c.author} at={c.createdAt} edited={c.edited}>
                {(c.canEdit || c.canDelete) && editing?.id !== c.id && (
                  <div className="talk__tools">
                    {confirming === c.id ? (
                      <span className="talk__confirm">
                        Apagar?
                        <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(eventsApi.deleteComment(eventId, c.id))}>
                          Apagar
                        </button>
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(undefined)}>
                          Cancelar
                        </button>
                      </span>
                    ) : (
                      <>
                        {c.canEdit && (
                          <button type="button" className="icon-btn icon-btn--sm" title="Editar" aria-label="Editar comentário" onClick={() => setEditing({ id: c.id, body: c.body })}>
                            <Icon name="pencil" />
                          </button>
                        )}
                        {c.canDelete && (
                          <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" title="Apagar" aria-label="Apagar comentário" onClick={() => setConfirming(c.id)}>
                            <Icon name="trash" />
                          </button>
                        )}
                      </>
                    )}
                  </div>
                )}
              </Who>
              {editing?.id === c.id ? (
                <form
                  className="talk__reply"
                  onSubmit={(e) => {
                    e.preventDefault();
                    run(eventsApi.editComment(eventId, c.id, editing.body), () => setEditing(undefined));
                  }}
                >
                  <label htmlFor={`${inputId}-edit`} className="sr-only">
                    Editar comentário
                  </label>
                  <textarea id={`${inputId}-edit`} rows={2} maxLength={COMMENT_MAX} value={editing.body} onChange={(e) => setEditing({ id: c.id, body: e.target.value })} onKeyDown={submitOnCtrlEnter} />
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)}>
                    Cancelar
                  </button>
                  <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !editing.body.trim()}>
                    Guardar
                  </button>
                </form>
              ) : (
                <Text>{c.body}</Text>
              )}
            </li>
          ))}
        </ol>
      )}
      {post.canComment ? (
        <form
          className="talk__reply"
          onSubmit={(e) => {
            e.preventDefault();
            run(eventsApi.addComment(eventId, post.id, body), () => setBody(''));
          }}
        >
          <label htmlFor={inputId} className="sr-only">
            Comentar
          </label>
          <textarea
            id={inputId}
            ref={box}
            rows={1}
            placeholder="Comentar…"
            maxLength={COMMENT_MAX}
            value={body}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={submitOnCtrlEnter}
          />
          <button type="submit" className="talk__send" disabled={busy || !body.trim()} title="Enviar (Ctrl+Enter)">
            <Icon name="send" />
            <span className="sr-only">Enviar comentário</span>
          </button>
        </form>
      ) : (
        <p className="talk__closed">
          <Icon name="lock" />
          Comentários fechados.
        </p>
      )}
      <FieldError text={error} alert={false} />
    </div>
  );
}
