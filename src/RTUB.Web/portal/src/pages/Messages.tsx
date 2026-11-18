import { useCallback, useEffect, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { publishUnread, useUnreadProvider } from '../data/unread';
import { Notice, SignInNotice } from '../components/Notice';
import { ChatFace } from '../components/MessageParts';
import { type ConversationItem, type Draft, LIST_POLL_MS, api, byTier, conversationFromUrl, listStamp } from '../data/messagesApi';
import { DraftThread, StartDialog } from '../components/MessageStart';
import { Thread } from '../components/MessageThread';
import { Loading } from '../components/ui/Loading';

/**
 * /messages - Mensagens (Messaging v2). The conversation list and one chat; on phones one at a time (the
 * chat is ?c=<id>, so the back button returns to the list), side by side on wide screens. Replies, emoji reactions,
 * mute, unread / read. Text only: no attachments.
 */
export default function Messages() {
  useUnreadProvider(); // the list below publishes the badge's number: no separate 10 s badge request here
  const [list, setList] = useState<ConversationItem[] | 'signin' | 'failed'>();
  const [selected, setSelected] = useState<number | undefined>(conversationFromUrl);
  const [starting, setStarting] = useState(false);
  const [draft, setDraft] = useState<Draft>();

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
    const onPop = () => {
      setDraft(undefined);
      setSelected(conversationFromUrl());
    };
    addEventListener('popstate', onPop);
    return () => {
      clearInterval(timer);
      removeEventListener('popstate', onPop);
    };
  }, [loadList]);

  const open = (id?: number) => {
    setDraft(undefined);
    setSelected(id);
    history.pushState(null, '', id ? `${portal.messages}?c=${id}` : portal.messages);
  };

  return (
    <section className={`page wrap chat-page${selected || draft ? ' chat-page--open' : ''}`} aria-labelledby="chat-title">
      <header className="page__head chat-page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="chat-title" className="page__title">
          Mensagens
        </h1>
      </header>

      {list === undefined ? (
        <Loading label="A carregar as conversas…" />
      ) : list === 'signin' ? (
        <SignInNotice path={portal.messages}>As mensagens são da área de membros.</SignInNotice>
      ) : list === 'failed' ? (
        <Notice>Não foi possível carregar as conversas. Tenta outra vez daqui a pouco.</Notice>
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
                      <ChatFace kind={c.kind} avatarUrl={c.avatarUrl} size={44} />
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

          {draft ? (
            <DraftThread
              draft={draft}
              onBack={() => setDraft(undefined)}
              onStarted={(id) => {
                loadList();
                open(id);
              }}
            />
          ) : selected ? (
            <Thread
              key={selected}
              id={selected}
              onBack={() => open(undefined)}
              onChanged={loadList}
              onRemoved={() => {
                loadList();
                open(undefined);
              }}
            />
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
          onPicked={async (people, title) => {
            // One member: their existing chat opens; otherwise nothing is created until the first message.
            const found = people.length === 1 ? await api.direct(people[0].id) : undefined;
            setStarting(false);
            if (found?.kind === 'ok' && found.data.id) open(found.data.id);
            else setDraft({ people, title });
          }}
        />
      )}
    </section>
  );
}
