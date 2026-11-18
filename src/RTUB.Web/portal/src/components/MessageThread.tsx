// One open conversation: messages, reactions, replies, mute and remove.
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Icon } from './icons';
import { ChatFace, Composer } from './MessageParts';
import { type Detail, type MessageItem, THREAD_POLL_MS, api, clock, dayLabel, problem, sameDay, toDate } from '../data/messagesApi';
import { Loading } from './ui/Loading';
import { ConfirmDialog } from './ui/ConfirmDialog';
import { IconAction } from './ui/IconAction';

export function Thread({ id, onBack, onChanged, onRemoved }: { id: number; onBack: () => void; onChanged: () => void; onRemoved: () => void }) {
  const [removing, setRemoving] = useState<'ask' | 'busy'>();
  const [removeError, setRemoveError] = useState<string>();
  const [detail, setDetail] = useState<Detail | 'notfound' | 'signin' | 'failed'>();
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [replyTo, setReplyTo] = useState<MessageItem>();
  const [picker, setPicker] = useState<number>();
  const [error, setError] = useState<string>();
  const scroller = useRef<HTMLDivElement>(null);
  const stick = useRef(true);
  const older = useRef<{ height: number; top: number } | undefined>(undefined);

  const merge = (incoming: MessageItem[]) =>
    setMessages((prev) => {
      const byId = new Map(prev.map((m) => [m.id, m]));
      for (const m of incoming) byId.set(m.id, m);
      return [...byId.values()].sort((a, b) => a.id - b.id);
    });

  const refresh = useCallback(
    async (initial: boolean) => {
      const o = await api.detail(id);
      if (o.kind !== 'ok') {
        if (initial) setDetail(o.kind === 'notfound' ? 'notfound' : o.kind === 'signin' ? 'signin' : 'failed');
        return;
      }
      setDetail(o.data);
      if (initial) {
        setMessages(o.data.messages);
        setHasMore(o.data.hasMore);
      } else merge(o.data.messages);
      if (o.data.conversation.unreadCount > 0 && (await api.read(id)).kind === 'ok') onChanged();
    },
    [id, onChanged],
  );

  useEffect(() => {
    refresh(true);
    const timer = setInterval(() => document.visibilityState === 'visible' && refresh(false), THREAD_POLL_MS);
    return () => clearInterval(timer);
  }, [refresh]);

  // Keep the newest message in view while the reader is at the bottom; keep the place when older ones load.
  useLayoutEffect(() => {
    const el = scroller.current;
    if (!el) return;
    if (older.current) {
      el.scrollTop = el.scrollHeight - older.current.height + older.current.top;
      older.current = undefined;
    } else if (stick.current) el.scrollTop = el.scrollHeight;
  }, [messages]);

  if (detail === undefined) {
    return (
      <div className="chat-thread">
        <Loading label="A abrir a conversa…" />
      </div>
    );
  }

  if (typeof detail === 'string') {
    return (
      <div className="chat-thread chat-thread--empty">
        <p>
          {detail === 'notfound'
            ? 'Esta conversa não existe ou não fazes parte dela.'
            : detail === 'signin'
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

  const c = detail.conversation;
  const canReact = c.kind !== 'system';
  const showNames = c.kind !== 'direct';

  const loadOlder = async () => {
    const el = scroller.current;
    if (!messages.length) return;
    const o = await api.detail(id, messages[0].id);
    if (o.kind !== 'ok') return setError(problem(o, 'carregar mensagens anteriores'));
    if (el) older.current = { height: el.scrollHeight, top: el.scrollTop };
    setHasMore(o.data.hasMore);
    merge(o.data.messages);
  };

  const send = async (body: string) => {
    const o = await api.send(id, body, replyTo?.id ?? null);
    if (o.kind !== 'ok') {
      setError(problem(o, 'enviar a mensagem'));
      return false;
    }
    stick.current = true;
    setError(undefined);
    setReplyTo(undefined);
    merge([o.data]);
    onChanged();
    return true;
  };

  const react = async (m: MessageItem, emoji: string) => {
    setPicker(undefined);
    const o = m.reactions.find((r) => r.mine)?.emoji === emoji ? await api.unreact(m.id) : await api.react(m.id, emoji);
    if (o.kind === 'ok') merge([o.data]);
    else setError(problem(o, 'reagir'));
  };

  const toggleMute = async () => {
    const o = await api.mute(id, !c.muted);
    if (o.kind !== 'ok') return setError(problem(o, 'mudar o som da conversa'));
    setDetail({ ...detail, conversation: { ...c, muted: o.data.muted } });
    onChanged();
  };

  const subtitle =
    c.kind === 'group'
      ? `${detail.participants.length + 1} membros`
      : c.kind === 'system'
        ? 'Avisos da RTUB'
        : (detail.participants[0]?.fullName ?? null);

  return (
    <div className="chat-thread">
      <header className="chat-thread__head">
        <button type="button" className="icon-btn chat-thread__back" onClick={onBack}>
          <Icon name="arrowLeft" />
          <span className="sr-only">Voltar às conversas</span>
        </button>
        <ChatFace kind={c.kind} avatarUrl={c.avatarUrl} size={40} />
        <div className="chat-thread__who">
          <h2 className="chat-thread__title">{c.title}</h2>
          {subtitle && <p className="chat-thread__subtitle">{subtitle}</p>}
        </div>
        <button
          type="button"
          className={`icon-btn${c.muted ? ' chat-thread__mute--on' : ''}`}
          aria-pressed={c.muted}
          onClick={toggleMute}
          title={c.muted ? 'Ativar o som' : 'Silenciar'}
        >
          <Icon name={c.muted ? 'bellSlash' : 'bell'} />
          <span className="sr-only">{c.muted ? 'Ativar o som desta conversa' : 'Silenciar esta conversa'}</span>
        </button>
        {c.kind === 'direct' && (
          <button type="button" className="icon-btn icon-btn--danger" onClick={() => setRemoving('ask')} title="Remover conversa">
            <Icon name="trash" />
            <span className="sr-only">Remover conversa</span>
          </button>
        )}
      </header>

      <div
        className="chat-thread__scroll"
        ref={scroller}
        onScroll={(e) => {
          const el = e.currentTarget;
          stick.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
        }}
      >
        {hasMore && (
          <button type="button" className="btn btn--ghost btn--sm chat-thread__older" onClick={loadOlder}>
            Mensagens anteriores
          </button>
        )}
        {messages.length === 0 ? (
          <p className="note chat-thread__none">Ainda não há mensagens. Diz olá!</p>
        ) : (
          <ol className="chat-messages" aria-label={`Mensagens com ${c.title}`}>
            {messages.map((m, i) => {
              const newDay = i === 0 || !sameDay(toDate(messages[i - 1].createdAt), toDate(m.createdAt));
              const name = m.sender?.displayName ?? (m.isSystem ? null : 'Membro removido');
              return (
                <li key={m.id} className="chat-messages__item">
                  {newDay && (
                    <p className="chat-day">
                      <span>{dayLabel(m.createdAt)}</span>
                    </p>
                  )}
                  {m.isSystem && c.kind !== 'system' ? (
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
                                  disabled={!canReact || !detail.reactionChoices.includes(r.emoji)}
                                  onClick={() => react(m, r.emoji)}
                                >
                                  {r.emoji} {r.count}
                                </button>
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                      {(c.canSend || canReact) && (
                        <div className="bubble__actions">
                          {c.canSend && (
                            <IconAction icon="reply" label="Responder" onClick={() => setReplyTo(m)} />
                          )}
                          {canReact && (
                            <button
                              type="button"
                              className="icon-btn icon-btn--sm"
                              aria-expanded={picker === m.id}
                              onClick={() => setPicker(picker === m.id ? undefined : m.id)}
                            >
                              <Icon name="smile" />
                              <span className="sr-only">Reagir</span>
                            </button>
                          )}
                        </div>
                      )}
                      {picker === m.id && (
                        <div className="reaction-picker" role="group" aria-label="Escolhe uma reação">
                          {detail.reactionChoices.map((e) => (
                            <button key={e} type="button" className="reaction-picker__choice" onClick={() => react(m, e)}>
                              {e}
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </li>
              );
            })}
          </ol>
        )}
      </div>

      {error && (
        <p className="chat-thread__error" role="alert">
          {error}
        </p>
      )}

      {removing && (
        <ConfirmDialog
          title="Remover conversa"
          confirmLabel="Remover"
          busy={removing === 'busy'}
          error={removeError}
          onClose={() => {
            setRemoving(undefined);
            setRemoveError(undefined);
          }}
          onConfirm={async () => {
            setRemoving('busy');
            const o = await api.remove(id);
            if (o.kind === 'ok') return onRemoved();
            setRemoving('ask');
            setRemoveError(problem(o, 'remover a conversa'));
          }}
        >
          <p>
            A conversa com <strong>{c.title}</strong> sai da lista, para ti e para {c.title}. Se voltarem a escrever-se, volta a
            aparecer com as mensagens antigas.
          </p>
        </ConfirmDialog>
      )}

      {c.canSend ? (
        <Composer replyTo={replyTo} onCancelReply={() => setReplyTo(undefined)} onSend={send} />
      ) : (
        <p className="chat-thread__readonly">
          {c.kind === 'system'
            ? 'Avisos do sistema: só de leitura.'
            : c.kind === 'direct'
              ? 'Já não é possível escrever a este membro.'
              : 'Neste canal só alguns cargos escrevem.'}
        </p>
      )}
    </div>
  );
}
