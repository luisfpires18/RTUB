import { useCallback, useEffect, useLayoutEffect, useRef, useState, type UIEvent } from 'react';
import { type Detail, type MessageItem, THREAD_POLL_MS, api, problem } from './messagesApi';

/** Within this many pixels of the bottom the reader counts as "at the bottom", so new messages scroll into view. */
const STICK_TO_BOTTOM_PX = 80;

/** State, polling, scrolling and actions of one open conversation. */
export function useThread(id: number, onChanged: () => void) {
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

  const onScroll = (e: UIEvent<HTMLDivElement>) => {
    const el = e.currentTarget;
    stick.current = el.scrollHeight - el.scrollTop - el.clientHeight < STICK_TO_BOTTOM_PX;
  };

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

  /** `loaded` is the detail the header was drawn from; the mute flag is written back into it. */
  const toggleMute = async (loaded: Detail) => {
    const c = loaded.conversation;
    const o = await api.mute(id, !c.muted);
    if (o.kind !== 'ok') return setError(problem(o, 'mudar o som da conversa'));
    setDetail({ ...loaded, conversation: { ...c, muted: o.data.muted } });
    onChanged();
  };

  return { detail, messages, hasMore, replyTo, setReplyTo, picker, setPicker, error, scroller, onScroll, loadOlder, send, react, toggleMute };
}
