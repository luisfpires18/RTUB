import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { loginTo } from './musicApi';
import { publishUnread, useUnreadProvider } from './unread';

// /api/conversations + /api/messages (task 041, Endpoints/MessagingEndpoints.cs): Messaging v2 over the preserved v1
// tables. The server decides every rule (MessagingService): a conversation the member is not in is a 404, and
// `canSend` only spares a composer that would be refused. Text only. Dates are UTC.
type Person = { id: string; displayName: string; fullName: string | null; avatarUrl: string };
type Kind = 'direct' | 'group' | 'system';
type ConversationItem = {
  id: number;
  kind: Kind;
  title: string;
  avatarUrl: string;
  lastMessagePreview: string | null;
  lastMessageIsMine: boolean;
  lastMessageAt: string;
  unreadCount: number;
  muted: boolean;
  pinned: boolean;
  canSend: boolean;
};
type Reaction = { emoji: string; count: number; mine: boolean };
type MessageItem = {
  id: number;
  conversationId: number;
  sender: Person | null;
  isMine: boolean;
  isSystem: boolean;
  body: string;
  link: string | null;
  createdAt: string;
  replyTo: { messageId: number | null; senderName: string | null; body: string } | null;
  reactions: Reaction[];
  seen: boolean;
};
type Detail = {
  conversation: ConversationItem;
  participants: Person[];
  messages: MessageItem[];
  hasMore: boolean;
  reactionChoices: string[];
};

const api = {
  list: () => call<ConversationItem[]>('GET', '/api/conversations'),
  recipients: () => call<Person[]>('GET', '/api/conversations/recipients'),
  detail: (id: number, before?: number) => call<Detail>('GET', `/api/conversations/${id}${before ? `?before=${before}` : ''}`),
  start: (userId: string) => call<Detail>('POST', '/api/conversations', { userId }),
  send: (id: number, body: string, replyToMessageId: number | null) =>
    call<MessageItem>('POST', `/api/conversations/${id}/messages`, { body, replyToMessageId }),
  read: (id: number) => call<void>('POST', `/api/conversations/${id}/read`),
  mute: (id: number, muted: boolean) => call<{ muted: boolean }>('POST', `/api/conversations/${id}/${muted ? 'mute' : 'unmute'}`),
  react: (messageId: number, emoji: string) => call<MessageItem>('POST', `/api/messages/${messageId}/reactions`, { emoji }),
  unreact: (messageId: number) => call<MessageItem>('DELETE', `/api/messages/${messageId}/reactions`),
};

const MAX_BODY = 2000;
const THREAD_POLL_MS = 10_000;
const LIST_POLL_MS = 30_000;

const toDate = (iso: string) => new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();
const clock = (iso: string) => toDate(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
const listStamp = (iso: string) => {
  const d = toDate(iso);
  return sameDay(d, new Date()) ? clock(iso) : d.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' });
};
const dayLabel = (iso: string) => {
  const d = toDate(iso);
  const today = new Date();
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  if (sameDay(d, today)) return 'Hoje';
  if (sameDay(d, yesterday)) return 'Ontem';
  return d.toLocaleDateString('pt-PT', { day: 'numeric', month: 'long', year: d.getFullYear() === today.getFullYear() ? undefined : 'numeric' });
};

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não podes escrever nesta conversa.'
      : o.kind === 'notfound'
        ? 'Esta conversa ou mensagem já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** "Sistema RTUB" and the groups wear the logo the portal header uses (the server sends the same file since #226 F09). */
const RTUB_LOGO = '/icons/rtub-logo-352.webp';
const chatAvatar = (c: { kind: string; avatarUrl: string }) => (c.kind === 'direct' ? c.avatarUrl : RTUB_LOGO);

/** Sistema RTUB first, then the groups, then the people; inside each the server's order (pinned, then latest activity). */
const TIER: Record<string, number> = { system: 0, group: 1, direct: 2 };
const byTier = (list: ConversationItem[]) => [...list].sort((a, b) => TIER[a.kind] - TIER[b.kind]);

const conversationFromUrl = () => Number(new URLSearchParams(location.search).get('c')) || undefined;

/**
 * /messages - Mensagens (Messaging v2, task 041). The conversation list and one chat; on phones one at a time (the
 * chat is ?c=<id>, so the back button returns to the list), side by side on wide screens. Replies, emoji reactions,
 * mute, unread / read. Text only: no attachments.
 */
export default function Messages() {
  useUnreadProvider(); // the list below publishes the badge's number: no separate 10 s badge request here
  const [list, setList] = useState<ConversationItem[] | 'signin' | 'failed'>();
  const [selected, setSelected] = useState<number | undefined>(conversationFromUrl);
  const [starting, setStarting] = useState(false);

  const loadList = useCallback(
    () =>
      api.list().then((o) => {
        if (o.kind === 'ok') publishUnread(o.data); // the header badge follows the list
        setList((cur) => (o.kind === 'ok' ? byTier(o.data) : o.kind === 'signin' ? 'signin' : Array.isArray(cur) ? cur : 'failed'));
      }),
    [],
  );

  useEffect(() => {
    document.documentElement.classList.add('app-screen'); // phones: no page scroll, no footer (styles.css)
    return () => document.documentElement.classList.remove('app-screen');
  }, []);

  useEffect(() => {
    document.title = 'Mensagens · RTUB';
    loadList();
    const timer = setInterval(() => document.visibilityState === 'visible' && loadList(), LIST_POLL_MS);
    const onPop = () => setSelected(conversationFromUrl());
    addEventListener('popstate', onPop);
    return () => {
      clearInterval(timer);
      removeEventListener('popstate', onPop);
    };
  }, [loadList]);

  const open = (id?: number) => {
    setSelected(id);
    history.pushState(null, '', id ? `${portal.messages}?c=${id}` : portal.messages);
  };

  return (
    <section className={`page wrap chat-page${selected ? ' chat-page--open' : ''}`} aria-labelledby="chat-title">
      <header className="page__head chat-page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="chat-title" className="page__title">
          Mensagens
        </h1>
      </header>

      {list === undefined ? (
        <Loading label="A carregar as conversas…" />
      ) : list === 'signin' ? (
        <div className="notice" role="status">
          <p>As mensagens são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.messages)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : list === 'failed' ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar as conversas. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <div className="chat">
          <aside className="chat-list" aria-label="Conversas">
            <div className="chat-list__head">
              <h2 className="chat-list__title">Conversas</h2>
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setStarting(true)}>
                <Icon name="compose" />
                Nova
              </button>
            </div>
            {list.length === 0 ? (
              <p className="note chat-list__empty">Ainda não tens conversas. Começa uma com “Nova”.</p>
            ) : (
              <ul className="chat-list__items">
                {list.map((c) => (
                  <li key={c.id}>
                    <button
                      type="button"
                      className={`chat-row${c.id === selected ? ' chat-row--active' : ''}${c.unreadCount > 0 ? ' chat-row--unread' : ''}`}
                      aria-current={c.id === selected ? 'true' : undefined}
                      onClick={() => open(c.id)}
                    >
                      <MemberFace avatarUrl={chatAvatar(c)} size={44} />
                      <span className="chat-row__main">
                        <span className="chat-row__top">
                          <span className="chat-row__title">{c.title}</span>
                          <time className="chat-row__time" dateTime={c.lastMessageAt}>
                            {listStamp(c.lastMessageAt)}
                          </time>
                        </span>
                        <span className="chat-row__bottom">
                          <span className="chat-row__preview">
                            {c.lastMessagePreview === null ? 'Sem mensagens' : `${c.lastMessageIsMine ? 'Tu: ' : ''}${c.lastMessagePreview}`}
                          </span>
                          {c.muted && (
                            <span className="chat-row__muted" title="Silenciada">
                              <Icon name="bellSlash" />
                              <span className="sr-only">Silenciada</span>
                            </span>
                          )}
                          {c.unreadCount > 0 && (
                            <span className="chat-row__badge">
                              {c.unreadCount > 99 ? '99+' : c.unreadCount}
                              <span className="sr-only"> por ler</span>
                            </span>
                          )}
                        </span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </aside>

          {selected ? (
            <Thread key={selected} id={selected} onBack={() => open(undefined)} onChanged={loadList} />
          ) : (
            <div className="chat-thread chat-thread--empty">
              <Icon name="chat" />
              <p>Escolhe uma conversa.</p>
            </div>
          )}
        </div>
      )}

      {starting && (
        <StartDialog
          onClose={() => setStarting(false)}
          onStarted={(id) => {
            setStarting(false);
            loadList();
            open(id);
          }}
        />
      )}
    </section>
  );
}

function Thread({ id, onBack, onChanged }: { id: number; onBack: () => void; onChanged: () => void }) {
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
        <MemberFace avatarUrl={chatAvatar(c)} size={40} />
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
                            <button type="button" className="icon-btn icon-btn--sm" onClick={() => setReplyTo(m)}>
                              <Icon name="reply" />
                              <span className="sr-only">Responder</span>
                            </button>
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

function Composer({
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
          <button type="button" className="icon-btn icon-btn--sm" onClick={onCancelReply}>
            <Icon name="close" />
            <span className="sr-only">Cancelar a resposta</span>
          </button>
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

function StartDialog({ onClose, onStarted }: { onClose: () => void; onStarted: (id: number) => void }) {
  const [people, setPeople] = useState<Person[] | 'failed'>();
  const [q, setQ] = useState('');
  const [error, setError] = useState<string>();
  const searchId = useId();

  useEffect(() => {
    api.recipients().then((o) => setPeople(o.kind === 'ok' ? o.data : 'failed'));
  }, []);

  const shown = useMemo(() => {
    if (!Array.isArray(people)) return [];
    const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    const term = fold(q.trim());
    return people.filter((p) => !term || fold(`${p.displayName} ${p.fullName ?? ''}`).includes(term));
  }, [people, q]);

  const start = async (p: Person) => {
    const o = await api.start(p.id);
    if (o.kind === 'ok') onStarted(o.data.conversation.id);
    else setError(problem(o, 'abrir a conversa'));
  };

  return (
    <Dialog title="Nova conversa" onClose={onClose}>
      {people === undefined ? (
        <Loading label="A carregar os membros…" />
      ) : people === 'failed' ? (
        <p className="note">Não foi possível carregar os membros. Tenta outra vez daqui a pouco.</p>
      ) : (
        <div className="chat-start">
          <label className="control" htmlFor={searchId}>
            <span className="sr-only">Procurar membros</span>
            <Icon name="search" />
            <input id={searchId} type="search" placeholder="Procurar membros…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
          </label>
          {error && (
            <p className="chat-thread__error" role="alert">
              {error}
            </p>
          )}
          <ul className="chat-start__list">
            {shown.map((p) => (
              <li key={p.id}>
                <button type="button" className="chat-start__person" onClick={() => start(p)}>
                  <MemberFace avatarUrl={p.avatarUrl} size={36} />
                  <span>
                    <strong>{p.displayName}</strong>
                    {p.fullName && p.fullName !== p.displayName && <small>{p.fullName}</small>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {shown.length === 0 && <p className="note">Ninguém com esse nome.</p>}
        </div>
      )}
    </Dialog>
  );
}
