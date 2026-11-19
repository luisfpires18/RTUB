// Chat pieces: conversation face, composer, and the thread header, notice and message.
import { useEffect, useId, useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Icon } from './icons';
import { MAX_BODY, clock, dayLabel, type Detail, type MessageItem } from '../data/messagesApi';
import { Avatar } from './ui/Avatar';
import { IconAction } from './ui/IconAction';

/** Only "Notificações RTUB" wears the app logo (the one the portal header uses); groups a plain chat icon, people their photo. */
const RTUB_LOGO = '/icons/rtub-logo-352.webp';

export function ChatFace({ kind, avatarUrl, size }: { kind: string; avatarUrl: string; size: number }) {
  if (kind !== 'group') return <Avatar className="who__avatar" src={kind === 'system' ? RTUB_LOGO : avatarUrl} size={size} />;
  return (
    <span className="chat-face" style={{ width: size, height: size }} aria-hidden="true">
      <Icon name="people" />
    </span>
  );
}

export function Composer({
  replyTo,
  onCancelReply,
  onSend,
}: {
  replyTo?: MessageItem;
  onCancelReply: () => void;
  onSend: (body: string) => Promise<boolean>;
}) {
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const field = useRef<HTMLTextAreaElement>(null);
  const fieldId = useId();

  useEffect(() => {
    if (replyTo) field.current?.focus();
  }, [replyTo]);

  // Grow with the text up to a few lines.
  useLayoutEffect(() => {
    const el = field.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`;
  }, [text]);

  const submit = async (e?: FormEvent) => {
    e?.preventDefault();
    const body = text.trim();
    if (!body || busy) return;
    // Cleared at once, so whatever is typed while this one is on its way is kept; put back only if it failed.
    setText('');
    setBusy(true);
    if (!(await onSend(body))) setText((typed) => typed || body);
    setBusy(false);
    field.current?.focus();
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      submit();
    }
  };

  return (
    <form className="composer" onSubmit={submit}>
      {replyTo && (
        <div className="composer__reply">
          <div>
            <strong>A responder a {replyTo.isMine ? 'ti' : (replyTo.sender?.displayName ?? 'Mensagem')}</strong>
            <span>{replyTo.body}</span>
          </div>
          <IconAction icon="close" label="Cancelar a resposta" onClick={onCancelReply} />
        </div>
      )}
      <div className="composer__row">
        <label className="sr-only" htmlFor={fieldId}>
          Mensagem
        </label>
        <textarea
          id={fieldId}
          ref={field}
          rows={1}
          maxLength={MAX_BODY}
          placeholder="Escreve uma mensagem"
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={onKeyDown}
        />
        <button type="submit" className="composer__send" disabled={busy || !text.trim()}>
          <Icon name="sendFill" />
          <span className="sr-only">Enviar</span>
        </button>
      </div>
      {text.length > MAX_BODY - 200 && (
        <p className="composer__count">
          {text.length}/{MAX_BODY}
        </p>
      )}
    </form>
  );
}

/** The conversation's top bar: face, title, subtitle, mute switch and (direct chats) remove. */
export function ThreadHeader({
  detail,
  onBack,
  onToggleMute,
  onRemove,
}: {
  detail: Detail;
  onBack: () => void;
  onToggleMute: () => void;
  onRemove: () => void;
}) {
  const c = detail.conversation;
  const subtitle =
    c.kind === 'group'
      ? `${detail.participants.length + 1} membros`
      : c.kind === 'system'
        ? 'Avisos da RTUB'
        : (detail.participants[0]?.fullName ?? null);
  return (
    <header className="chat-thread__head">
      <IconAction icon="arrowLeft" label="Voltar às conversas" onClick={onBack} large className="chat-thread__back" />
      <ChatFace kind={c.kind} avatarUrl={c.avatarUrl} size={40} />
      <div className="chat-thread__who">
        <h2 className="chat-thread__title">{c.title}</h2>
        {subtitle && <p className="chat-thread__subtitle">{subtitle}</p>}
      </div>
      <button
        type="button"
        className={`icon-btn${c.muted ? ' chat-thread__mute--on' : ''}`}
        aria-pressed={c.muted}
        onClick={onToggleMute}
        title={c.muted ? 'Ativar o som' : 'Silenciar'}
      >
        <Icon name={c.muted ? 'bellSlash' : 'bell'} />
        <span className="sr-only">{c.muted ? 'Ativar o som desta conversa' : 'Silenciar esta conversa'}</span>
      </button>
      {c.kind === 'direct' && <IconAction icon="trash" label="Remover conversa" title="Remover conversa" onClick={onRemove} large danger />}
    </header>
  );
}

/** Shown instead of a thread that could not be opened. */
export function ThreadNotice({ reason, onBack }: { reason: 'notfound' | 'signin' | 'failed'; onBack: () => void }) {
  return (
    <div className="chat-thread chat-thread--empty">
      <p>
        {reason === 'notfound'
          ? 'Esta conversa não existe ou não fazes parte dela.'
          : reason === 'signin'
            ? 'A sessão terminou. Entra outra vez.'
            : 'Não foi possível abrir a conversa. Tenta outra vez daqui a pouco.'}
      </p>
      <button type="button" className="btn btn--ghost btn--sm chat-thread__back-text" onClick={onBack}>
        <Icon name="arrowLeft" />
        Voltar às conversas
      </button>
    </div>
  );
}

/** One message: its day heading, bubble (or system note), reactions and the reply / react actions. */
export function ThreadMessage({
  message: m,
  newDay,
  kind,
  canSend,
  canReact,
  showNames,
  reactionChoices,
  pickerOpen,
  onReply,
  onTogglePicker,
  onReact,
}: {
  message: MessageItem;
  newDay: boolean;
  kind: string;
  canSend: boolean;
  canReact: boolean;
  showNames: boolean;
  reactionChoices: string[];
  pickerOpen: boolean;
  onReply: () => void;
  onTogglePicker: () => void;
  onReact: (emoji: string) => void;
}) {
  const name = m.sender?.displayName ?? (m.isSystem ? null : 'Membro removido');
  return (
    <li className="chat-messages__item">
      {newDay && (
        <p className="chat-day">
          <span>{dayLabel(m.createdAt)}</span>
        </p>
      )}
      {m.isSystem && kind !== 'system' ? (
        <p className="chat-note">{m.body}</p>
      ) : (
        <div className={`bubble-row${m.isMine ? ' bubble-row--mine' : ''}`}>
          <div className="bubble">
            {showNames && !m.isMine && name && <p className="bubble__name">{name}</p>}
            {m.replyTo && (
              <blockquote className="bubble__quote">
                <strong>{m.replyTo.senderName ?? 'Mensagem'}</strong>
                <span>{m.replyTo.body}</span>
              </blockquote>
            )}
            <p className="bubble__text">{m.body}</p>
            {m.link && (
              <a className="bubble__link" href={m.link}>
                Abrir
                <Icon name="arrow" />
              </a>
            )}
            <p className="bubble__meta">
              <time dateTime={m.createdAt}>{clock(m.createdAt)}</time>
              {m.isMine && m.seen && <span> · Vista</span>}
            </p>
            {m.reactions.length > 0 && (
              <ul className="bubble__reactions" aria-label="Reações">
                {m.reactions.map((r) => (
                  <li key={r.emoji}>
                    <button
                      type="button"
                      className={`reaction${r.mine ? ' reaction--mine' : ''}`}
                      aria-pressed={r.mine}
                      disabled={!canReact || !reactionChoices.includes(r.emoji)}
                      onClick={() => onReact(r.emoji)}
                    >
                      {r.emoji} {r.count}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          {(canSend || canReact) && (
            <div className="bubble__actions">
              {canSend && <IconAction icon="reply" label="Responder" onClick={onReply} />}
              {canReact && (
                <button type="button" className="icon-btn icon-btn--sm" aria-expanded={pickerOpen} onClick={onTogglePicker}>
                  <Icon name="smile" />
                  <span className="sr-only">Reagir</span>
                </button>
              )}
            </div>
          )}
          {pickerOpen && (
            <div className="reaction-picker" role="group" aria-label="Escolhe uma reação">
              {reactionChoices.map((e) => (
                <button key={e} type="button" className="reaction-picker__choice" onClick={() => onReact(e)}>
                  {e}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </li>
  );
}
