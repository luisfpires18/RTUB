import { useEffect, useId, useState } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { portal } from './content';
import { dateLabel, eventsApi, type EventContactRow, type EventContacts as Contacts, type EventSummary } from './eventsApi';
import { Icon } from './icons';
import { when } from './EventDiscussion';
import { loginTo } from './musicApi';

const NOTES_MAX = 500;

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

const answers = [
  { value: true, label: 'Vai', icon: 'check', className: 'choice__option--yes' },
  { value: false, label: 'Não vai', icon: 'close', className: 'choice__option--no' },
  { value: null, label: 'Indeciso', icon: 'clock', className: '' },
] as const;

function Answer({ value }: { value: boolean | null }) {
  const a = answers.find((x) => x.value === value)!;
  return <span className={`contact__answer contact__answer--${value === true ? 'yes' : value === false ? 'no' : 'maybe'}`}>{a.label}</span>;
}

/**
 * /events/{id}/contacts - contact tracking (React track 013; was a Blazor page). Mod and above call members
 * about an event and note what they said; an answer also records the member's inscrição (as before), and
 * "Repor" undoes both. The list carries phone numbers, so the server only gives it to Mod and above.
 */
export default function EventContacts({ eventId }: { eventId: number }) {
  const [event, setEvent] = useState<EventSummary | 'missing' | null>();
  const [list, setList] = useState<Contacts | 'signin' | 'forbidden' | null>();
  const [recording, setRecording] = useState<EventContactRow>();
  const [resetting, setResetting] = useState<string>();
  const [error, setError] = useState<string>();
  const [q, setQ] = useState('');
  const searchId = useId();

  useEffect(() => {
    eventsApi.event(eventId).then((o) => setEvent(o.kind === 'ok' ? o.data.event : o.kind === 'notfound' ? 'missing' : null));
    eventsApi.contacts(eventId).then((o) =>
      setList(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null),
    );
  }, [eventId]);

  useEffect(() => {
    document.title = event && event !== 'missing' ? `Contactos · ${event.name} · RTUB` : 'Contactos · RTUB';
  }, [event]);

  const reset = async (row: EventContactRow) => {
    setError(undefined);
    const o = await eventsApi.resetContact(eventId, row.userId);
    setResetting(undefined);
    if (o.kind === 'ok') setList(o.data);
    else setError('Não foi possível repor o contacto. Tenta outra vez.');
  };

  const match = (r: EventContactRow) => !q.trim() || fold(`${r.name} ${r.fullName ?? ''}`).includes(fold(q.trim()));

  if (event === 'missing') {
    return (
      <section className="page wrap">
        <a className="back-link" href={portal.events}>
          <Icon name="arrow" />
          Agenda
        </a>
        <div className="notice" role="status">
          <p>Esta atuação não existe ou já foi apagada.</p>
        </div>
      </section>
    );
  }

  const card = (r: EventContactRow) => (
    <li key={r.userId} className={`contact${r.contactedAt ? ' contact--done' : ''}`}>
      <img className="contact__avatar" src={r.avatarUrl} alt="" loading="lazy" width="48" height="48" />
      <div className="contact__who">
        <p className="contact__name">{r.name}</p>
        {r.fullName && r.fullName !== r.name && <p className="contact__full">{r.fullName}</p>}
        {r.phone ? (
          <a className="contact__phone" href={`tel:${r.phone.replace(/\s+/g, '')}`}>
            <Icon name="phone" />
            {r.phone}
          </a>
        ) : (
          <p className="contact__full">Sem telefone</p>
        )}
        {r.contactedAt && (
          <p className="contact__status">
            <Answer value={r.willAttend} />
            <span className="contact__when">{when(r.contactedAt)}</span>
          </p>
        )}
        {r.notes && <p className="contact__notes">“{r.notes}”</p>}
      </div>
      <div className="contact__actions">
        {resetting === r.userId ? (
          <span className="talk__confirm">
            Repor? A inscrição também sai.
            <button type="button" className="btn btn--danger btn--sm" onClick={() => reset(r)}>
              Repor
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setResetting(undefined)}>
              Cancelar
            </button>
          </span>
        ) : r.contactedAt ? (
          <>
            <button type="button" className="link-btn" onClick={() => setRecording(r)}>
              <Icon name="pencil" />
              Editar
            </button>
            <button type="button" className="link-btn link-btn--danger" onClick={() => setResetting(r.userId)}>
              <Icon name="restore" />
              Repor
            </button>
          </>
        ) : (
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setRecording(r)}>
            <Icon name="phone" />
            Contactado
          </button>
        )}
      </div>
    </li>
  );

  return (
    <section className="page wrap contacts-page" aria-labelledby="contacts-title">
      <a className="back-link" href={portal.event(eventId)}>
        <Icon name="arrow" />
        {event ? event.name : 'Atuação'}
      </a>
      <header className="page__head">
        <p className="eyebrow">Mod e acima</p>
        <h1 id="contacts-title" className="page__title">
          Contactos
        </h1>
        {event && (
          <p className="page__lead">
            {event.name} · {dateLabel(event)} · {event.location}
          </p>
        )}
      </header>

      {list === undefined ? (
        <Loading label="A carregar os contactos…" />
      ) : list === 'signin' ? (
        <div className="notice" role="status">
          <p>Os contactos são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(`/events/${eventId}/contacts`)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : list === 'forbidden' ? (
        <div className="notice" role="status">
          <p>Os contactos de uma atuação são geridos por Mod e acima.</p>
          <a className="btn btn--ghost btn--sm" href={portal.event(eventId)}>
            Voltar à atuação
          </a>
        </div>
      ) : list === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir os contactos agora. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={searchId}>
              <span className="sr-only">Procurar membro</span>
              <Icon name="search" />
              <input id={searchId} type="search" placeholder="Procurar pelo nome" value={q} onChange={(e) => setQ(e.target.value)} />
            </label>
          </div>
          {error && <p className="form__error">{error}</p>}
          <section className="events-block" aria-labelledby="contacted-title">
            <h2 id="contacted-title" className="events-block__title">
              Contactados · {list.contacted.length}
            </h2>
            {list.contacted.length === 0 ? (
              <p className="note">Ainda ninguém foi contactado para esta atuação.</p>
            ) : (
              <ul className="contacts">{list.contacted.filter(match).map(card)}</ul>
            )}
          </section>
          <section className="events-block" aria-labelledby="pending-title">
            <h2 id="pending-title" className="events-block__title">
              Por contactar · {list.notContacted.length}
            </h2>
            {list.notContacted.length === 0 ? (
              <p className="note">Todos os membros foram contactados.</p>
            ) : (
              <ul className="contacts">{list.notContacted.filter(match).map(card)}</ul>
            )}
          </section>
        </>
      )}

      {recording && (
        <RecordDialog
          eventId={eventId}
          row={recording}
          onClose={() => setRecording(undefined)}
          onSaved={(next) => {
            setList(next);
            setRecording(undefined);
          }}
        />
      )}
    </section>
  );
}

function RecordDialog({ eventId, row, onClose, onSaved }: { eventId: number; row: EventContactRow; onClose: () => void; onSaved: (c: Contacts) => void }) {
  const [willAttend, setWillAttend] = useState<boolean | null>(row.contactedAt ? row.willAttend : null);
  const [notes, setNotes] = useState(row.notes ?? '');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const notesId = useId();

  const save = async () => {
    setBusy(true);
    const o = await eventsApi.saveContact(eventId, row.userId, willAttend, notes);
    setBusy(false);
    if (o.kind === 'ok') onSaved(o.data);
    else setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : 'Não foi possível guardar. Tenta outra vez.');
  };

  return (
    <Dialog
      title="Registar contacto"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" disabled={busy} onClick={save}>
            {busy ? 'A guardar…' : 'Guardar'}
          </button>
        </>
      }
    >
      <div className="answer">
        <p className="contact__name">
          {row.name}
          {row.phone && (
            <a className="contact__phone" href={`tel:${row.phone.replace(/\s+/g, '')}`}>
              <Icon name="phone" />
              {row.phone}
            </a>
          )}
        </p>
        <fieldset className="choice">
          <legend className="contact__legend">Vai à atuação?</legend>
          <div className="choice__options contact__choices">
            {answers.map((a) => (
              <label key={String(a.value)} className={`choice__option ${a.className}${willAttend === a.value ? ' is-on' : ''}`}>
                <input type="radio" name="willAttend" checked={willAttend === a.value} onChange={() => setWillAttend(a.value)} />
                <Icon name={a.icon} />
                {a.label}
              </label>
            ))}
          </div>
        </fieldset>
        <p className="note">“Vai” ou “Não vai” também regista a inscrição do membro, sem notificação.</p>
        <div className="form__field">
          <label htmlFor={notesId}>Notas</label>
          <textarea id={notesId} rows={3} maxLength={NOTES_MAX} placeholder="Notas sobre a conversa…" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        {error && <p className="form__error">{error}</p>}
      </div>
    </Dialog>
  );
}
