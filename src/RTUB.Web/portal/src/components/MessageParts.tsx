// Small chat pieces: conversation face and the message composer.
import { useEffect, useId, useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Icon } from './icons';
import { MAX_BODY, type MessageItem } from '../data/messagesApi';
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
