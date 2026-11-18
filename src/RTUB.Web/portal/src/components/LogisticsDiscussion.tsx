// Card discussion panel (comments and @mentions).
import { useId, useState } from 'react';
import { MemberPicker } from './LogisticsFields';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, type CardComment, type CardDetail } from '../data/logisticsApi';
import { DEFAULT_AVATAR } from '../data/membersApi';
import { at } from '../data/logisticsDates';
import { FieldError } from './ui/FieldError';
import { PersonChip } from './ui/PersonChip';

/** The discussion: plain text (never HTML), @mentions picked from members, Ctrl/⌘+Enter sends. */
export function Discussion({ boardId, detail, apply, busy, errors }: { boardId: number; detail: CardDetail; apply: Apply; busy?: string; errors: Record<string, string> }) {
  const [body, setBody] = useState('');
  const [mentions, setMentions] = useState<{ id: string; name: string }[]>([]);
  const [picking, setPicking] = useState(false);
  const [editing, setEditing] = useState<{ id: number; body: string }>();
  const [confirm, setConfirm] = useState<number>();
  const id = useId();
  const cardId = detail.card.id;

  const send = async () => {
    if (!body.trim()) return;
    if (await apply(logisticsApi.comment(cardId, body.trim(), mentions.map((m) => m.id)), 'body', 'publicar o comentário')) {
      setBody('');
      setMentions([]);
    }
  };

  return (
    <Section id={`${id}-talk`} title="Discussão" extra={detail.comments.length > 0 && <small>{detail.comments.length}</small>}>
      {detail.comments.length === 0 ? (
        <p className="note">{detail.canWork ? 'Ainda sem comentários. Combine aqui os próximos passos.' : 'Ainda sem comentários.'}</p>
      ) : (
        <ol className="lx-talk">
          {detail.comments.map((c: CardComment) => (
            <li key={c.id} className="lx-talk__item">
              <img className="lx-talk__avatar" src={c.author?.avatarUrl ?? DEFAULT_AVATAR} alt="" width="32" height="32" loading="lazy" />
              <div className="lx-talk__body">
                <p className="lx-talk__who">
                  <strong title={c.author?.fullName ?? undefined}>{c.author?.displayName ?? 'Membro removido'}</strong>
                  <time dateTime={c.createdAt}>{at(c.createdAt)}</time>
                  {c.editedAt && <span className="note"> · editado</span>}
                </p>
                {editing?.id === c.id ? (
                  <div className="lx-talk__edit">
                    <textarea
                      aria-label="Editar o comentário"
                      rows={3}
                      maxLength={2000}
                      value={editing.body}
                      onChange={(e) => setEditing({ id: c.id, body: e.target.value })}
                    />
                    <div className="lx-inline">
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)}>
                        Cancelar
                      </button>
                      <button
                        type="button"
                        className="btn btn--primary btn--sm"
                        disabled={!editing.body.trim() || !!busy}
                        onClick={async () => (await apply(logisticsApi.editComment(c.id, editing.body.trim()), `comment-${c.id}`, 'editar o comentário')) && setEditing(undefined)}
                      >
                        Guardar
                      </button>
                    </div>
                  </div>
                ) : (
                  <p className="lx-talk__text">{c.body}</p>
                )}
                {c.mentions.length > 0 && <p className="lx-talk__mentions">Menciona: {c.mentions.join(', ')}</p>}
                {(c.canEdit || c.canDelete) && editing?.id !== c.id && (
                  <p className="lx-talk__actions">
                    {c.canEdit && (
                      <button type="button" className="link-btn" onClick={() => setEditing({ id: c.id, body: c.body })}>
                        Editar
                      </button>
                    )}
                    {c.canDelete &&
                      (confirm === c.id ? (
                        <button
                          type="button"
                          className="link-btn link-btn--danger"
                          onClick={async () => (setConfirm(undefined), await apply(logisticsApi.removeComment(c.id), `comment-${c.id}`, 'apagar o comentário'))}
                        >
                          Confirmar: apagar
                        </button>
                      ) : (
                        <button type="button" className="link-btn" onClick={() => setConfirm(c.id)}>
                          Apagar
                        </button>
                      ))}
                  </p>
                )}
                <FieldError text={errors[`comment-${c.id}`]} />
              </div>
            </li>
          ))}
        </ol>
      )}
      {detail.canWork && (
        <div className="lx-talk__compose">
          <label className="sr-only" htmlFor={`${id}-new`}>
            Novo comentário
          </label>
          <textarea
            id={`${id}-new`}
            rows={3}
            maxLength={2000}
            placeholder="Escreva um comentário"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && (e.ctrlKey || e.metaKey) && (e.preventDefault(), send())}
          />
          {mentions.length > 0 && (
            <div className="chips">
              {mentions.map((m) => (
                <PersonChip key={m.id} name={`@${m.name}`} avatar={null} onRemove={() => setMentions(mentions.filter((x) => x.id !== m.id))} removeLabel={`Não mencionar ${m.name}`} />
              ))}
            </div>
          )}
          {picking && (
            <MemberPicker
              label="Mencionar membro"
              boardId={boardId}
              exclude={mentions.map((m) => m.id)}
              onPick={(m) => (setMentions([...mentions, { id: m.id, name: m.displayName }]), setPicking(false))}
            />
          )}
          <div className="lx-inline lx-talk__send">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setPicking(!picking)} aria-expanded={picking} disabled={mentions.length >= 20}>
              @ Mencionar
            </button>
            <small className="note">{body.length}/2000</small>
            <button type="button" className="btn btn--primary btn--sm" disabled={!body.trim() || busy === 'body'} onClick={send}>
              {busy === 'body' && <span className="spinner spinner--small" aria-hidden="true" />}
              Comentar
            </button>
          </div>
          <FieldError text={errors.body ?? errors.mentionUserIds} />
        </div>
      )}
    </Section>
  );
}
