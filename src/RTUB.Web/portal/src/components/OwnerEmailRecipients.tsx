// Owner emails recipients picker.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { ownerApi, ownerProblem, type EmailRecipients, type OwnerPerson } from '../data/ownerApi';
import { Flash, matches, SearchBox } from './OwnerUi';
import { type Message } from '../data/ownerEmailsView';
import { Pager } from './ui/Pager';
import { Avatar } from './ui/Avatar';
import { IconAction } from './ui/IconAction';
import { CheckField } from './ui/CheckField';
import { EmptyNote } from './ui/EmptyNote';

export function Recipients({ data, onChanged }: { data: EmailRecipients; onChanged: (next: EmailRecipients) => void }) {
  const id = useId();
  const [message, setMessage] = useState<Message>();
  const [busy, setBusy] = useState(false);

  const change = async (people: OwnerPerson[], subscribed: boolean) => {
    if (people.length === 0) return;
    setBusy(true);
    const o = await ownerApi.setSubscription(
      people.map((p) => p.id),
      subscribed,
    );
    setBusy(false);
    if (o.kind !== 'ok') return setMessage({ text: ownerProblem(o, 'alterar as subscrições'), error: true });
    onChanged(o.data.recipients);
    const who = people.length === 1 ? people[0].nickname : `${o.data.changed} utilizador(es)`;
    const text = subscribed ? `${who} adicionado(s) às notificações.` : `${who} removido(s) das notificações.`;
    setMessage(
      o.data.errors.length > 0 ? { text: `${o.data.changed > 0 ? `${text} ` : ''}Erros: ${o.data.errors.join('; ')}`, error: true } : { text },
    );
  };

  return (
    <section className="owner-card" aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="owner-card__title">
        <Icon name="people" />
        Lista de destinatários
      </h2>
      <Flash message={message} onDismiss={() => setMessage(undefined)} />
      <div className="owner-columns">
        <PeopleList
          title={`Destinatários (${data.subscribed.length})`}
          people={data.subscribed}
          busy={busy}
          actionLabel="Remover"
          actionIcon="close"
          onAction={(p) => change(p, false)}
        />
        <PeopleList
          title={`Quem não vai receber (${data.notSubscribed.length})`}
          people={data.notSubscribed}
          busy={busy}
          actionLabel="Adicionar"
          actionIcon="plus"
          onAction={(p) => change(p, true)}
        />
      </div>
    </section>
  );
}

/** One side of the recipients: search, select (all of the search, or one by one), and act on one or the selection. */
function PeopleList({
  title,
  people,
  busy,
  actionLabel,
  actionIcon,
  onAction,
}: {
  title: string;
  people: OwnerPerson[];
  busy: boolean;
  actionLabel: string;
  actionIcon: 'close' | 'plus';
  onAction: (people: OwnerPerson[]) => void;
}) {
  const id = useId();
  const [q, setQ] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const filtered = people.filter((p) => matches(q, p.nickname, p.fullName, p.email));
  const shown = filtered.slice((page - 1) * pageSize, page * pageSize);
  const chosen = people.filter((p) => selected.has(p.id));
  const allChosen = filtered.length > 0 && filtered.every((p) => selected.has(p.id));

  const toggle = (ids: string[], on: boolean) =>
    setSelected((s) => {
      const next = new Set(s);
      ids.forEach((i) => (on ? next.add(i) : next.delete(i)));
      return next;
    });

  return (
    <div className="owner-people">
      <h3 className="owner-people__title">{title}</h3>
      <SearchBox
        label="Procurar utilizador"
        placeholder="Procurar por nome ou email"
        value={q}
        onChange={(v) => {
          setQ(v);
          setPage(1);
        }}
      />
      {people.length === 0 ? (
        <EmptyNote>Ninguém.</EmptyNote>
      ) : (
        <>
          <div className="owner-people__bar">
            <CheckField id={`${id}-all`} checked={allChosen} onChange={(on) => toggle(filtered.map((p) => p.id), on)}>
              Selecionar todos
            </CheckField>
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              disabled={busy || chosen.length === 0}
              onClick={() => {
                onAction(chosen);
                setSelected(new Set());
              }}
            >
              <Icon name={actionIcon} />
              {actionLabel} ({chosen.length})
            </button>
          </div>
          <ul className="owner-people__list">
            {shown.map((p) => (
              <li key={p.id} className="owner-person">
                <input
                  type="checkbox"
                  checked={selected.has(p.id)}
                  onChange={(e) => toggle([p.id], e.target.checked)}
                  aria-label={`Selecionar ${p.nickname}`}
                />
                <Avatar className="owner-person__avatar" src={p.avatarUrl} size={32} />
                <span className="owner-person__name">
                  <strong>{p.nickname}</strong>
                  <span className="note">{p.fullName}</span>
                </span>
                <IconAction icon={actionIcon} label={`${actionLabel} ${p.nickname}`} title={`${actionLabel} ${p.nickname}`} onClick={() => onAction([p])} disabled={busy} />
              </li>
            ))}
          </ul>
          {filtered.length === 0 && <EmptyNote>Ninguém corresponde à pesquisa.</EmptyNote>}
          {filtered.length > 10 && (
            <Pager
              total={filtered.length}
              itemLabel="utilizadores"
              page={page}
              pageSize={pageSize}
              sizes={[10, 25, 50, 100]}
              onPage={setPage}
              onPageSize={(s) => {
                setPageSize(s);
                setPage(1);
              }}
            />
          )}
        </>
      )}
    </div>
  );
}
