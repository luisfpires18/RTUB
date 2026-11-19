// One open conversation: messages, reactions, replies, mute and remove.
import { useState } from 'react';
import { Composer, ThreadHeader, ThreadMessage, ThreadNotice } from './MessageParts';
import { api, problem, sameDay, toDate } from '../data/messagesApi';
import { useThread } from '../data/useThread';
import { Loading } from './ui/Loading';
import { ConfirmDialog } from './ui/ConfirmDialog';

export function Thread({ id, onBack, onChanged, onRemoved }: { id: number; onBack: () => void; onChanged: () => void; onRemoved: () => void }) {
  const [removing, setRemoving] = useState<'ask' | 'busy'>();
  const [removeError, setRemoveError] = useState<string>();
  const { detail, messages, hasMore, replyTo, setReplyTo, picker, setPicker, error, scroller, onScroll, loadOlder, send, react, toggleMute } = useThread(id, onChanged);

  if (detail === undefined) {
    return (
      <div className="chat-thread">
        <Loading label="A abrir a conversa…" />
      </div>
    );
  }

  if (typeof detail === 'string') return <ThreadNotice reason={detail} onBack={onBack} />;

  const c = detail.conversation;
  const canReact = c.kind !== 'system';
  const showNames = c.kind !== 'direct';

  return (
    <div className="chat-thread">
      <ThreadHeader detail={detail} onBack={onBack} onToggleMute={() => toggleMute(detail)} onRemove={() => setRemoving('ask')} />

      <div className="chat-thread__scroll" ref={scroller} onScroll={onScroll}>
        {hasMore && (
          <button type="button" className="btn btn--ghost btn--sm chat-thread__older" onClick={loadOlder}>
            Mensagens anteriores
          </button>
        )}
        {messages.length === 0 ? (
          <p className="note chat-thread__none">Ainda não há mensagens. Diz olá!</p>
        ) : (
          <ol className="chat-messages" aria-label={`Mensagens com ${c.title}`}>
            {messages.map((m, i) => (
              <ThreadMessage
                key={m.id}
                message={m}
                newDay={i === 0 || !sameDay(toDate(messages[i - 1].createdAt), toDate(m.createdAt))}
                kind={c.kind}
                canSend={c.canSend}
                canReact={canReact}
                showNames={showNames}
                reactionChoices={detail.reactionChoices}
                pickerOpen={picker === m.id}
                onReply={() => setReplyTo(m)}
                onTogglePicker={() => setPicker(picker === m.id ? undefined : m.id)}
                onReact={(emoji) => react(m, emoji)}
              />
            ))}
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
