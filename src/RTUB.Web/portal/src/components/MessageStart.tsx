// Starting a conversation: the draft thread and the people picker dialog.
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ChatFace, Composer } from './MessageParts';
import { type Draft, type Person, api, problem } from '../data/messagesApi';
import { Loading } from './ui/Loading';
import { Avatar } from './ui/Avatar';
import { FormField } from './ui/FormField';
import { fold } from '../data/format';

export function DraftThread({ draft, onBack, onStarted }: { draft: Draft; onBack: () => void; onStarted: (id: number) => void }) {
  const [error, setError] = useState<string>();
  const group = draft.people.length > 1;
  const title = group ? draft.title : draft.people[0].displayName;

  const send = async (body: string) => {
    const o = await api.start(
      draft.people.map((p) => p.id),
      draft.title,
      body,
    );
    if (o.kind !== 'ok') {
      setError(problem(o, 'enviar a mensagem'));
      return false;
    }
    onStarted(o.data.conversation.id);
    return true;
  };

  return (
    <div className="chat-thread">
      <header className="chat-thread__head">
        <button type="button" className="icon-btn chat-thread__back" onClick={onBack}>
          <Icon name="arrowLeft" />
          <span className="sr-only">Voltar às conversas</span>
        </button>
        <ChatFace kind={group ? 'group' : 'direct'} avatarUrl={draft.people[0].avatarUrl} size={40} />
        <div className="chat-thread__who">
          <h2 className="chat-thread__title">{title}</h2>
          <p className="chat-thread__subtitle">
            {group ? draft.people.map((p) => p.displayName).join(', ') : (draft.people[0].fullName ?? 'Nova conversa')}
          </p>
        </div>
      </header>
      <div className="chat-thread__scroll">
        <p className="note chat-thread__none">
          {group ? 'O grupo é criado quando enviares a primeira mensagem.' : 'A conversa começa quando enviares a primeira mensagem.'}
        </p>
      </div>
      {error && (
        <p className="chat-thread__error" role="alert">
          {error}
        </p>
      )}
      <Composer onCancelReply={() => undefined} onSend={send} />
    </div>
  );
}

/** Pick one member (their chat) or several plus a name (a new group). Nothing is stored here. */
export function StartDialog({ onClose, onPicked }: { onClose: () => void; onPicked: (people: Person[], title: string) => Promise<void> }) {
  const [people, setPeople] = useState<Person[] | 'failed'>();
  const [q, setQ] = useState('');
  const [chosen, setChosen] = useState<Person[]>([]);
  const [title, setTitle] = useState('');
  const [busy, setBusy] = useState(false);
  const searchId = useId();
  const titleId = useId();

  useEffect(() => {
    api.recipients().then((o) => setPeople(o.kind === 'ok' ? o.data : 'failed'));
  }, []);

  const shown = useMemo(() => {
    if (!Array.isArray(people)) return [];
    const term = fold(q.trim());
    return people.filter((p) => !term || fold(`${p.displayName} ${p.fullName ?? ''}`).includes(term));
  }, [people, q]);

  const toggle = (p: Person) =>
    setChosen((cur) => (cur.some((c) => c.id === p.id) ? cur.filter((c) => c.id !== p.id) : [...cur, p]));
  const group = chosen.length > 1;
  const ready = chosen.length > 0 && (!group || title.trim().length > 0);

  const go = async (e: FormEvent) => {
    e.preventDefault();
    if (!ready || busy) return;
    setBusy(true);
    await onPicked(chosen, title.trim());
    setBusy(false);
  };

  return (
    <Dialog title="Nova conversa" onClose={onClose}>
      {people === undefined ? (
        <Loading label="A carregar os membros…" />
      ) : people === 'failed' ? (
        <p className="note">Não foi possível carregar os membros. Tenta outra vez daqui a pouco.</p>
      ) : (
        <form className="chat-start" onSubmit={go}>
          <label className="control" htmlFor={searchId}>
            <span className="sr-only">Procurar membros</span>
            <Icon name="search" />
            <input id={searchId} type="search" placeholder="Procurar membros…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
          </label>
          <ul className="chat-start__list">
            {shown.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  className="chat-start__person"
                  aria-pressed={chosen.some((c) => c.id === p.id)}
                  onClick={() => toggle(p)}
                >
                  <Avatar className="who__avatar" src={p.avatarUrl} size={36} />
                  <span>
                    <strong>{p.displayName}</strong>
                    {p.fullName && p.fullName !== p.displayName && <small>{p.fullName}</small>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {shown.length === 0 && <p className="note">Ninguém com esse nome.</p>}
          {group && (
            <FormField id={titleId} label="Nome do grupo">
              <input id={titleId} type="text" maxLength={200} required value={title} onChange={(e) => setTitle(e.target.value)} />
            </FormField>
          )}
          <button type="submit" className="btn btn--primary btn--sm" disabled={!ready || busy}>
            {chosen.length === 0 ? 'Escolhe membros' : group ? `Criar grupo (${chosen.length})` : `Conversar com ${chosen[0].displayName}`}
          </button>
        </form>
      )}
    </Dialog>
  );
}
