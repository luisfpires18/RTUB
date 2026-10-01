import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { dateLabel, eventsApi, timeLabel, type EventEdit, type EventInput, type EventSummary, type EventTypeOption } from './eventsApi';
import { Icon } from './icons';

// Admin/Owner event management on the React agenda (React track 011.5): create, edit, delete. The
// server enforces who may do it; these modals only appear for people it would accept. Images,
// cancelling, prizes, videos, repertoire and notices stay on the members' Blazor /member/events.

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

type Form = EventInput & { multiDay: boolean };

const blank = (): Form => ({
  name: '',
  date: today(),
  time: '19:00',
  endDate: null,
  location: '',
  type: 'Atuacao',
  description: '',
  multiDay: false,
});

const fromEdit = (e: EventEdit): Form => ({ ...e, multiDay: e.endDate !== null });

/** Create (no `eventId`) or edit one event. `onSaved` gets the event as the agenda shows it. */
export function EventFormDialog({
  eventId,
  types,
  onClose,
  onSaved,
}: {
  eventId?: number;
  types: EventTypeOption[];
  onClose: () => void;
  onSaved: (event: EventSummary) => void;
}) {
  const [form, setForm] = useState<Form | 'failed' | undefined>(eventId === undefined ? blank() : undefined);
  const [hasImage, setHasImage] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { name: useId(), multi: useId(), date: useId(), time: useId(), end: useId(), location: useId(), type: useId(), description: useId() };

  useEffect(() => {
    if (eventId === undefined) return;
    eventsApi.eventForEdit(eventId).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      setForm(fromEdit(o.data));
      setHasImage(o.data.hasImage);
    });
  }, [eventId]);

  const set = (next: Partial<Form>) => setForm((f) => (typeof f === 'object' ? { ...f, ...next } : f));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (typeof form !== 'object') return;
    setBusy(true);
    setBanner(undefined);
    const input: EventInput = {
      name: form.name,
      date: form.date,
      time: form.multiDay ? null : form.time || null,
      endDate: form.multiDay ? form.endDate || null : null,
      location: form.location,
      type: form.type,
      description: form.description,
    };
    const outcome = eventId === undefined ? await eventsApi.createEvent(input) : await eventsApi.updateEvent(eventId, input);
    setBusy(false);
    if (outcome.kind === 'ok') {
      onSaved(outcome.data);
      onClose();
      return;
    }
    if (outcome.kind === 'invalid') {
      setErrors(outcome.errors);
      return;
    }
    setErrors({});
    setBanner(
      outcome.kind === 'forbidden' || outcome.kind === 'signin'
        ? 'Não tem permissão para gerir atuações.'
        : outcome.kind === 'notfound'
          ? 'Esta atuação já não existe.'
          : 'Não foi possível guardar. Tente outra vez daqui a pouco.',
    );
  };

  const field = (key: keyof EventInput) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: keyof EventInput) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <Dialog title={eventId === undefined ? 'Adicionar atuação' : 'Editar atuação'} onClose={onClose}>
      {form === undefined ? (
        <Loading label="A carregar a atuação…" />
      ) : form === 'failed' ? (
        <p>Não foi possível abrir esta atuação para editar.</p>
      ) : (
        <form className="form event-form" onSubmit={submit} noValidate>
          <div className={field('name')}>
            <label htmlFor={ids.name}>Nome</label>
            <input id={ids.name} type="text" maxLength={200} required value={form.name} onChange={(e) => set({ name: e.target.value })} />
            {error('name')}
          </div>

          <label className="form__check" htmlFor={ids.multi}>
            <input
              id={ids.multi}
              type="checkbox"
              checked={form.multiDay}
              onChange={(e) => set({ multiDay: e.target.checked, endDate: e.target.checked ? (form.endDate ?? form.date) : null })}
            />
            Vários dias
          </label>

          <div className="form__row">
            <div className={field('date')}>
              <label htmlFor={ids.date}>{form.multiDay ? 'Primeiro dia' : 'Data'}</label>
              <input id={ids.date} type="date" required value={form.date} onChange={(e) => set({ date: e.target.value })} />
              {error('date')}
            </div>
            {form.multiDay ? (
              <div className={field('endDate')}>
                <label htmlFor={ids.end}>Último dia</label>
                <input id={ids.end} type="date" min={form.date} value={form.endDate ?? ''} onChange={(e) => set({ endDate: e.target.value })} />
                {error('endDate')}
              </div>
            ) : (
              <div className={field('time')}>
                <label htmlFor={ids.time}>Hora</label>
                <input id={ids.time} type="time" value={form.time ?? ''} onChange={(e) => set({ time: e.target.value })} />
                {error('time')}
              </div>
            )}
          </div>
          {form.date && form.date < today() && (!form.multiDay || !form.endDate || form.endDate < today()) && (
            <p className="form__hint">Esta data já passou: a atuação fica no arquivo.</p>
          )}

          <div className="form__row">
            <div className={field('location')}>
              <label htmlFor={ids.location}>Local</label>
              <input id={ids.location} type="text" maxLength={200} value={form.location} onChange={(e) => set({ location: e.target.value })} />
              {error('location')}
            </div>
            <div className={field('type')}>
              <label htmlFor={ids.type}>Tipo</label>
              <span className="control control--select">
                <select id={ids.type} value={form.type} onChange={(e) => set({ type: e.target.value })}>
                  {types.map((t) => (
                    <option key={t.value} value={t.value}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </span>
              {error('type')}
            </div>
          </div>

          <div className={field('description')}>
            <label htmlFor={ids.description}>Descrição (só membros a veem)</label>
            <textarea id={ids.description} rows={4} maxLength={2000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
            {error('description')}
          </div>

          {eventId === undefined ? (
            <p className="form__hint">Ao guardar, os membros recebem uma notificação da nova atuação.</p>
          ) : (
            hasImage && <p className="form__hint">A imagem mantém-se. Mudá-la ainda se faz na área de membros.</p>
          )}

          {banner && (
            <p className="form__banner" role="alert">
              {banner}
            </p>
          )}

          <div className="answer__actions">
            <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
              Cancelar
            </button>
            <button type="submit" className="btn btn--primary" disabled={busy}>
              {busy && <span className="spinner spinner--small" aria-hidden="true" />}
              {eventId === undefined ? 'Adicionar' : 'Guardar'}
            </button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

/** Confirms a delete, naming the event; the database also drops its answers, prizes, videos and repertoire. */
export function DeleteEventDialog({ event, onClose, onDeleted }: { event: EventSummary; onClose: () => void; onDeleted: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const time = timeLabel(event.time);

  const remove = async () => {
    setBusy(true);
    const outcome = await eventsApi.deleteEvent(event.id);
    setBusy(false);
    if (outcome.kind === 'ok' || outcome.kind === 'notfound') {
      onDeleted();
      onClose();
    } else if (outcome.kind === 'inuse') setError('Esta atuação tem encomendas NERBA associadas e não pode ser apagada.');
    else if (outcome.kind === 'forbidden' || outcome.kind === 'signin') setError('Não tem permissão para apagar atuações.');
    else setError('Não foi possível apagar. Tente outra vez daqui a pouco.');
  };

  return (
    <Dialog title="Apagar atuação" onClose={onClose}>
      <div className="answer">
        <p className="answer-event">
          <strong>{event.name}</strong>
          <span>
            {dateLabel(event)}
            {time && ` · ${time}`} · {event.location}
          </span>
        </p>
        <p className="warning">
          <Icon name="warning" />
          Apaga também a imagem, as inscrições, os prémios, os vídeos, o repertório e a discussão desta atuação. Não dá para desfazer.
        </p>
        {error && (
          <p className="form__error" role="alert">
            {error}
          </p>
        )}
        <div className="answer__actions">
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Apagar
          </button>
        </div>
      </div>
    </Dialog>
  );
}
