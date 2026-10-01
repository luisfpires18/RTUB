import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import {
  dateLabel,
  eventsApi,
  timeLabel,
  type EventEdit,
  type EventPrize,
  type EventInput,
  type EventSummary,
  type EventTypeOption,
  type NoticeAudience,
  type Outcome,
} from './eventsApi';
import { Icon } from './icons';

// Admin/Owner event management on the React agenda: create, edit, delete (011.5); the image,
// cancel / reactivate and email / push notices (012A). The server enforces who may do it; these
// modals only appear for people it would accept. Prizes, videos, repertoire and statistics stay on
// the members' Blazor /member/events.

/** The old event cropper's limit on the picked file; the cropped image the server takes is ≤5 MB. */
const MAX_SOURCE_BYTES = 10 * 1024 * 1024;

const EVENT_IMAGE = {
  title: 'Recortar a imagem',
  label: 'Pré-visualização da imagem da atuação (3:2). Arraste ou use as setas para enquadrar.',
  note: 'As imagens das atuações ficam em 3:2. Arraste a imagem para escolher o enquadramento.',
};

/** The banner for a refused or failed call; `what` names the action ("guardar", "cancelar"...). */
const failure = (o: Outcome<unknown>, what: string) =>
  o.kind === 'forbidden' || o.kind === 'signin'
    ? 'Não tem permissão para gerir atuações.'
    : o.kind === 'notfound'
      ? 'Esta atuação já não existe.'
      : o.kind === 'closed'
        ? 'Esta atuação já passou ou mudou entretanto. Recarregue a página.'
        : `Não foi possível ${what}. Tente outra vez daqui a pouco.`;

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

/**
 * Create (no `eventId`) or edit one event, with its optional image (cropped 3:2 in the browser,
 * uploaded after the details are saved). `onSaved` refreshes the agenda.
 */
export function EventFormDialog({
  eventId,
  types,
  onClose,
  onSaved,
}: {
  eventId?: number;
  types: EventTypeOption[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<Form | 'failed' | undefined>(eventId === undefined ? blank() : undefined);
  // Once a new event is created it is edited from then on, so a retry never creates it twice.
  const [savedId, setSavedId] = useState(eventId);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [image, setImage] = useState<{ blob: Blob; preview: string } | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const [picked, setPicked] = useState<File | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { name: useId(), multi: useId(), date: useId(), time: useId(), end: useId(), location: useId(), type: useId(), description: useId() };

  useEffect(() => {
    if (eventId === undefined) return;
    eventsApi.eventForEdit(eventId).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      setForm(fromEdit(o.data));
      setCurrentImage(o.data.imageUrl);
    });
  }, [eventId]);

  const pick = (file: File) => {
    if (!file.type.startsWith('image/')) return setErrors((e) => ({ ...e, image: 'Escolha um ficheiro de imagem.' }));
    if (file.size > MAX_SOURCE_BYTES) return setErrors((e) => ({ ...e, image: 'A imagem não pode exceder 10 MB.' }));
    setErrors(({ image: _, ...rest }) => rest);
    setPicked(file);
  };

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
    const outcome = savedId === undefined ? await eventsApi.createEvent(input) : await eventsApi.updateEvent(savedId, input);
    if (outcome.kind !== 'ok') {
      setBusy(false);
      if (outcome.kind === 'invalid') return setErrors(outcome.errors);
      setErrors({});
      return setBanner(failure(outcome, 'guardar'));
    }
    const id = outcome.data.id;
    setSavedId(id);
    onSaved();

    // The details are saved; now the image, if it changed.
    const imageOutcome = image
      ? await eventsApi.setImage(id, image.blob)
      : removeImage && currentImage
        ? await eventsApi.removeImage(id)
        : null;
    setBusy(false);
    if (!imageOutcome || imageOutcome.kind === 'ok') {
      if (imageOutcome) onSaved();
      onClose();
      return;
    }
    setErrors(imageOutcome.kind === 'invalid' ? { image: Object.values(imageOutcome.errors)[0] } : {});
    setBanner(imageOutcome.kind === 'invalid' ? 'A atuação foi guardada, mas a imagem não.' : `A atuação foi guardada, mas a imagem não. ${failure(imageOutcome, 'guardar a imagem')}`);
  };

  const shownImage = image?.preview ?? (removeImage ? null : currentImage);

  const field = (key: keyof EventInput) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: keyof EventInput) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <>
    <Dialog title={savedId === undefined ? 'Adicionar atuação' : 'Editar atuação'} onClose={onClose}>
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

          <div className={errors.image ? 'form__field form__field--error' : 'form__field'}>
            <span className="form__label">Imagem (opcional)</span>
            <div className="cover-pick">
              {shownImage ? (
                <img className="cover-pick__img cover-pick__img--wide" src={shownImage} alt="Imagem da atuação" width="180" height="120" />
              ) : (
                <span className="cover-pick__img cover-pick__img--wide cover-pick__img--empty">
                  <Icon name="images" />
                </span>
              )}
              <div className="cover-pick__actions">
                <label className="btn btn--ghost btn--sm">
                  <Icon name="upload" />
                  {shownImage ? 'Trocar imagem' : 'Escolher imagem'}
                  <input
                    className="sr-only"
                    type="file"
                    accept="image/*"
                    disabled={busy}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      e.target.value = '';
                      if (file) pick(file);
                    }}
                  />
                </label>
                {image ? (
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setImage(null)} disabled={busy}>
                    Desfazer
                  </button>
                ) : removeImage ? (
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoveImage(false)} disabled={busy}>
                    Manter a imagem
                  </button>
                ) : (
                  currentImage && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoveImage(true)} disabled={busy}>
                      <Icon name="trash" />
                      Remover imagem
                    </button>
                  )
                )}
                <p className="form__hint">
                  {removeImage && !image ? 'A imagem atual é apagada ao guardar.' : 'Recortada em 3:2 antes de guardar.'}
                </p>
                {errors.image && <p className="form__error">{errors.image}</p>}
              </div>
            </div>
          </div>

          {savedId === undefined && <p className="form__hint">Ao guardar, os membros recebem uma notificação da nova atuação.</p>}

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
              {savedId === undefined ? 'Adicionar' : 'Guardar'}
            </button>
          </div>
        </form>
      )}
    </Dialog>
    {picked && (
      <Cropper
        file={picked}
        aspect={3 / 2}
        outWidth={1200}
        text={EVENT_IMAGE}
        onCancel={() => setPicked(null)}
        onDone={(blob, preview) => {
          setImage({ blob, preview });
          setRemoveImage(false);
          setPicked(null);
        }}
      />
    )}
    </>
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

/** Name, date and place, at the top of every confirmation. */
function EventLine({ event }: { event: EventSummary }) {
  const time = timeLabel(event.time);
  return (
    <p className="answer-event">
      <strong>{event.name}</strong>
      <span>
        {dateLabel(event)}
        {time && ` · ${time}`} · {event.location}
      </span>
    </p>
  );
}

const members = (n: number) => (n === 1 ? '1 membro' : `${n} membros`);

/** The counts of a notice's audience, once loaded. */
function useAudience(eventId: number) {
  const [audience, setAudience] = useState<NoticeAudience | null>();
  useEffect(() => {
    eventsApi.noticeAudience(eventId).then((o) => setAudience(o.kind === 'ok' ? o.data : null));
  }, [eventId]);
  return audience;
}

/**
 * Cancels an upcoming event, as the old page did: a reason is required, every inscrição is deleted,
 * and an email to the members with email notifications on is opt-in. `onDone` refreshes the page.
 */
export function CancelEventDialog({ event, onClose, onDone }: { event: EventSummary; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('');
  const [notify, setNotify] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [warning, setWarning] = useState<string>();
  const audience = useAudience(event.id);
  const id = useId();
  const going = event.member?.goingCount ?? 0;

  const confirm = async () => {
    if (!reason.trim()) return setError('Indique o motivo do cancelamento.');
    setBusy(true);
    setError(undefined);
    const o = await eventsApi.cancelEvent(event.id, reason.trim(), notify);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      if (o.data.warning) setWarning(o.data.warning);
      else onClose();
      return;
    }
    setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : failure(o, 'cancelar'));
  };

  return (
    <Dialog title="Cancelar atuação" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        {warning ? (
          <>
            <p className="warning" role="alert">
              <Icon name="warning" />
              {warning}
            </p>
            <div className="answer__actions">
              <button type="button" className="btn btn--primary" onClick={onClose}>
                Fechar
              </button>
            </div>
          </>
        ) : (
          <>
            <p className="warning">
              <Icon name="warning" />
              Cancelar apaga todas as inscrições desta atuação{going > 0 && ` (${going === 1 ? '1 confirmado' : `${going} confirmados`})`}.
              Reativar depois não as recupera.
            </p>
            <div className={error ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={id}>Motivo do cancelamento</label>
              <textarea id={id} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} disabled={busy} />
              <p className="form__hint">Os membros veem o motivo na página da atuação.</p>
            </div>
            <label className="form__check">
              <input type="checkbox" checked={notify} onChange={(e) => setNotify(e.target.checked)} disabled={busy} />
              Avisar por email
              {audience && ` (${members(audience.emailSubscribed)} com notificações por email ativas)`}
            </label>
            {error && (
              <p className="form__error" role="alert">
                {error}
              </p>
            )}
            <div className="answer__actions">
              <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
                Voltar
              </button>
              <button type="button" className="btn btn--danger" onClick={confirm} disabled={busy}>
                {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                Cancelar atuação
              </button>
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
}

/** Reactivates a cancelled upcoming event. No one is notified and the deleted inscrições stay deleted. */
export function ReactivateEventDialog({ event, onClose, onDone }: { event: EventSummary; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    const o = await eventsApi.reactivateEvent(event.id);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(failure(o, 'reativar'));
  };

  return (
    <Dialog title="Reativar atuação" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        <p>
          A atuação volta à agenda como confirmada e aceita inscrições outra vez. Ninguém é notificado e as inscrições
          apagadas ao cancelar não voltam.
        </p>
        {error && (
          <p className="form__error" role="alert">
            {error}
          </p>
        )}
        <div className="answer__actions">
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Voltar
          </button>
          <button type="button" className="btn btn--primary" onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Reativar
          </button>
        </div>
      </div>
    </Dialog>
  );
}

/**
 * The event's prizes for Admin/Owner, inside the Prémios modal (012B): rename or delete each one in
 * place, add a new one when the event takes prizes (a past festival). Every write answers the full
 * list; `onChanged` refreshes the page behind so its Prémios button and history follow.
 */
export function PrizeManager({ eventId, canAdd, onChanged }: { eventId: number; canAdd: boolean; onChanged: () => void }) {
  const [prizes, setPrizes] = useState<EventPrize[] | null>();
  const [name, setName] = useState('');
  const [editing, setEditing] = useState<{ id: number; name: string }>();
  const [deleting, setDeleting] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const ids = { add: useId(), edit: useId() };

  useEffect(() => {
    eventsApi.prizes(eventId).then((o) => setPrizes(o.kind === 'ok' ? o.data : null));
  }, [eventId]);

  const run = async (call: Promise<Outcome<EventPrize[]>>, done: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      setPrizes(o.data);
      done();
      onChanged();
    } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
    else if (o.kind === 'closed') setError('Só se juntam prémios a festivais que já aconteceram.');
    else setError(failure(o, 'guardar o prémio'));
  };

  const add = (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return setError('Indique o nome do prémio.');
    run(eventsApi.addPrize(eventId, name.trim()), () => setName(''));
  };

  const rename = (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    if (!editing.name.trim()) return setError('Indique o nome do prémio.');
    run(eventsApi.renamePrize(eventId, editing.id, editing.name.trim()), () => setEditing(undefined));
  };

  if (prizes === undefined) return <Loading label="A carregar os prémios…" />;
  if (prizes === null) return <p className="form__error">Não foi possível carregar os prémios desta atuação.</p>;

  return (
    <section className="prize-manager" aria-label="Gerir os prémios desta atuação">
      <p className="eyebrow">Nesta atuação · gerir</p>
      {prizes.length === 0 ? (
        <p className="note">Ainda sem prémios nesta atuação.</p>
      ) : (
        <ul className="prize-manager__list">
          {prizes.map((p) => (
            <li key={p.id}>
              {editing?.id === p.id ? (
                <form className="prize-manager__row" onSubmit={rename}>
                  <label className="sr-only" htmlFor={ids.edit}>
                    Novo nome do prémio
                  </label>
                  <input
                    id={ids.edit}
                    type="text"
                    maxLength={200}
                    value={editing.name}
                    onChange={(e) => setEditing({ id: p.id, name: e.target.value })}
                    disabled={busy}
                    autoFocus
                  />
                  <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
                    Guardar
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)} disabled={busy}>
                    Cancelar
                  </button>
                </form>
              ) : deleting === p.id ? (
                <div className="prize-manager__row" role="alert">
                  <span className="prize-manager__name">Apagar «{p.name}»?</span>
                  <button
                    type="button"
                    className="btn btn--danger btn--sm"
                    onClick={() => run(eventsApi.deletePrize(eventId, p.id), () => setDeleting(undefined))}
                    disabled={busy}
                  >
                    Apagar
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDeleting(undefined)} disabled={busy}>
                    Voltar
                  </button>
                </div>
              ) : (
                <div className="prize-manager__row">
                  <Icon name="trophy" />
                  <span className="prize-manager__name">{p.name}</span>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    onClick={() => {
                      setDeleting(undefined);
                      setEditing({ id: p.id, name: p.name });
                    }}
                    disabled={busy}
                    title="Mudar o nome"
                  >
                    <Icon name="pencil" />
                    <span className="sr-only">Mudar o nome de {p.name}</span>
                  </button>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm icon-btn--danger"
                    onClick={() => {
                      setEditing(undefined);
                      setDeleting(p.id);
                    }}
                    disabled={busy}
                    title="Apagar"
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Apagar {p.name}</span>
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {canAdd && (
        <form className="prize-manager__row prize-manager__add" onSubmit={add}>
          <label className="sr-only" htmlFor={ids.add}>
            Nome do novo prémio
          </label>
          <input
            id={ids.add}
            type="text"
            maxLength={200}
            placeholder="Ex.: Melhor Pandeireta"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={busy}
          />
          <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
            <Icon name="plus" />
            Adicionar
          </button>
        </form>
      )}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}

/**
 * One notice about an upcoming event, as the old page offered: an email ("Nova atuação" or
 * "Lembrete") to the members with email notifications on, or a push message to the members with
 * push on (optionally Leitões and Caloiros only). The audience is shown before anything is sent,
 * and sending takes a second, explicit confirmation.
 */
export function NoticeDialog({ event, onClose }: { event: EventSummary; onClose: () => void }) {
  const [channel, setChannel] = useState<'email' | 'push'>('email');
  const [kind, setKind] = useState<'new' | 'reminder'>('new');
  const [message, setMessage] = useState('');
  const [young, setYoung] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [sent, setSent] = useState<{ count: number; warning: string | null }>();
  const audience = useAudience(event.id);
  const ids = { kind: useId(), message: useId() };

  const reach = !audience
    ? null
    : channel === 'email'
      ? { to: audience.emailSubscribed, of: audience.emailTotal, what: 'notificações por email' }
      : young
        ? { to: audience.pushLeitoesCaloirosSubscribed, of: audience.pushLeitoesCaloirosTotal, what: 'notificações push (Leitões e Caloiros)' }
        : { to: audience.pushSubscribed, of: audience.pushTotal, what: 'notificações push' };
  const ready = !!reach && reach.to > 0 && (channel === 'email' || message.trim().length > 0);

  // Any change goes back to the first step: what is confirmed is exactly what is sent.
  const change = (f: () => void) => {
    f();
    setConfirming(false);
    setError(undefined);
  };

  const send = async () => {
    setBusy(true);
    const o = await eventsApi.sendNotice(
      event.id,
      channel === 'email'
        ? { channel, kind, message: null, onlyLeitoesAndCaloiros: false }
        : { channel, kind: null, message: message.trim(), onlyLeitoesAndCaloiros: young },
    );
    setBusy(false);
    setConfirming(false);
    if (o.kind === 'ok') return setSent({ count: o.data.sent, warning: o.data.warning });
    setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : failure(o, 'enviar'));
  };

  return (
    <Dialog title="Enviar aviso" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        {sent ? (
          <>
            <p role="status">{channel === 'email' ? `Email enviado a ${members(sent.count)}.` : `Notificação enviada a ${members(sent.count)}.`}</p>
            {sent.warning && (
              <p className="warning">
                <Icon name="warning" />
                {sent.warning}
              </p>
            )}
            <div className="answer__actions">
              <button type="button" className="btn btn--primary" onClick={onClose}>
                Fechar
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="segmented" role="radiogroup" aria-label="Canal">
              {(['email', 'push'] as const).map((c) => (
                <label key={c} className={channel === c ? 'segmented__option is-on' : 'segmented__option'}>
                  <input type="radio" name="notice-channel" checked={channel === c} onChange={() => change(() => setChannel(c))} disabled={busy} />
                  <Icon name={c === 'email' ? 'envelope' : 'bell'} />
                  {c === 'email' ? 'Email' : 'Push'}
                </label>
              ))}
            </div>

            {channel === 'email' ? (
              <div className="form__field">
                <label htmlFor={ids.kind}>Tipo de email</label>
                <span className="control control--select">
                  <select id={ids.kind} value={kind} onChange={(e) => change(() => setKind(e.target.value as 'new' | 'reminder'))} disabled={busy}>
                    <option value="new">Nova atuação</option>
                    <option value="reminder">Lembrete de atuação</option>
                  </select>
                </span>
                <p className="form__hint">
                  {kind === 'new' ? 'Apresenta a atuação aos membros.' : 'Lembra os membros da atuação que se aproxima.'}
                </p>
              </div>
            ) : (
              <>
                <div className="form__field">
                  <label htmlFor={ids.message}>Mensagem</label>
                  <textarea
                    id={ids.message}
                    rows={3}
                    maxLength={500}
                    value={message}
                    onChange={(e) => change(() => setMessage(e.target.value))}
                    disabled={busy}
                  />
                  <p className="form__hint">O título é o nome da atuação. Máximo 500 caracteres ({message.length}/500).</p>
                </div>
                <label className="form__check">
                  <input type="checkbox" checked={young} onChange={(e) => change(() => setYoung(e.target.checked))} disabled={busy} />
                  Só Leitões e Caloiros
                </label>
              </>
            )}

            <p className="notice-audience" role="status">
              <Icon name="person" />
              {audience === undefined
                ? 'A contar os destinatários…'
                : reach
                  ? `${members(reach.to)} de ${reach.of} com ${reach.what} ativas.`
                  : 'Não foi possível contar os destinatários.'}
            </p>

            {error && (
              <p className="form__error" role="alert">
                {error}
              </p>
            )}

            {confirming ? (
              <div className="notice-confirm" role="alert">
                <p>
                  <strong>
                    Enviar {channel === 'email' ? 'este email' : 'esta notificação'} a {members(reach?.to ?? 0)}?
                  </strong>{' '}
                  Não dá para desfazer.
                </p>
                <div className="answer__actions">
                  <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)} disabled={busy}>
                    Voltar
                  </button>
                  <button type="button" className="btn btn--primary" onClick={send} disabled={busy}>
                    {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                    Sim, enviar
                  </button>
                </div>
              </div>
            ) : (
              <div className="answer__actions">
                <button type="button" className="btn btn--ghost" onClick={onClose}>
                  Cancelar
                </button>
                <button type="button" className="btn btn--primary" onClick={() => setConfirming(true)} disabled={!ready}>
                  <Icon name="send" />
                  Enviar…
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </Dialog>
  );
}
