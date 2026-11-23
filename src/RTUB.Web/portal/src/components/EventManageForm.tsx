// Create / edit event dialog with its cropped image.

import { CheckField } from './ui/CheckField';
import { FieldError } from './ui/FieldError';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { eventsApi, type EventInput, type EventTypeOption } from '../data/eventsApi';
import { Icon } from './icons';
import { today } from '../data/format';
import { MAX_SOURCE_BYTES, EVENT_IMAGE, blank, fromEdit, type Form } from '../data/eventFormDraft';
import { failure } from './EventManageShared';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';
import { AnswerActions } from './ui/AnswerActions';
import { FormField } from './ui/FormField';
import { SelectField } from './ui/SearchTools';

/**
 * Create (no `eventId`) or edit one event, with its optional image (cropped 3:2 in the browser,
 * uploaded after the details are saved). `onSaved` refreshes the agenda.
 */
export function EventFormDialog({
  eventId,
  prefill,
  types,
  onClose,
  onSaved,
}: {
  eventId?: number;
  /** A new event's starting values (Requests' "criar atuação"). */
  prefill?: Partial<Pick<Form, 'name' | 'location' | 'description' | 'date'>>;
  types: EventTypeOption[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<Form | 'failed' | undefined>(eventId === undefined ? { ...blank(), ...prefill } : undefined);
  // Once a new event is created it is edited from then on, so a retry never creates it twice.
  const [savedId, setSavedId] = useState(eventId);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [image, setImage] = useState<{ blob: Blob; preview: string } | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const [picked, setPicked] = useState<File | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { name: useId(), multi: useId(), membersOnly: useId(), date: useId(), time: useId(), end: useId(), location: useId(), type: useId(), description: useId() };

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
      membersOnly: form.membersOnly,
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
  const error = (key: keyof EventInput) => <FieldError text={errors[key]} alert={false} />;

  return (
    <>
    <Dialog title={savedId === undefined ? 'Adicionar atuação' : 'Editar atuação'} onClose={onClose}>
      {form === undefined ? (
        <Loading label="A carregar a atuação…" />
      ) : form === 'failed' ? (
        <p>Não foi possível abrir esta atuação para editar.</p>
      ) : (
        <form className="form event-form" onSubmit={submit} noValidate>
          <FormField id={ids.name} label="Nome" error={errors.name}>
            <input id={ids.name} type="text" maxLength={200} required value={form.name} onChange={(e) => set({ name: e.target.value })} />
          </FormField>

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
              <FormField id={ids.end} label="Último dia" error={errors.endDate}>
                <input id={ids.end} type="date" min={form.date} value={form.endDate ?? ''} onChange={(e) => set({ endDate: e.target.value })} />
              </FormField>
            ) : (
              <FormField id={ids.time} label="Hora" error={errors.time}>
                <input id={ids.time} type="time" value={form.time ?? ''} onChange={(e) => set({ time: e.target.value })} />
              </FormField>
            )}
          </div>
          {form.date && form.date < today() && (!form.multiDay || !form.endDate || form.endDate < today()) && (
            <p className="form__hint">Esta data já passou: a atuação fica no arquivo.</p>
          )}

          <div className="form__row">
            <FormField id={ids.location} label="Local" error={errors.location}>
              <input id={ids.location} type="text" maxLength={200} value={form.location} onChange={(e) => set({ location: e.target.value })} />
            </FormField>
            <FormField id={ids.type} label="Tipo" error={errors.type}>
              <SelectField id={ids.type} value={form.type} onChange={(v) => set({ type: v })}>
                {types.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </SelectField>
            </FormField>
          </div>

          <FormField id={ids.description} label="Descrição (só membros a veem)" error={errors.description}>
            <textarea id={ids.description} rows={4} maxLength={2000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
          </FormField>

          <CheckField id={ids.membersOnly} checked={form.membersOnly} onChange={(membersOnly) => set({ membersOnly })}>
            Privado (só membros): quem não tem sessão iniciada não vê esta atuação
          </CheckField>

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
                <FieldError text={errors.image} alert={false} />
              </div>
            </div>
          </div>

          {savedId === undefined && <p className="form__hint">Ao guardar, os membros recebem uma notificação da nova atuação.</p>}

          <Banner text={banner} />

          <AnswerActions cancelLabel="Cancelar" onCancel={onClose} label={savedId === undefined ? 'Adicionar' : 'Guardar'} busy={busy} />
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
