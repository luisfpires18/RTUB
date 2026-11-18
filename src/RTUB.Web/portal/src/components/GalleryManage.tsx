import { useEffect, useId, useState, type FormEvent } from 'react';
import { FieldError } from './ui/FieldError';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { today } from '../data/format';
import { MAX_IMAGE, MAX_VIDEO, type EditData, type Form, type UploadEvent, galleryApi, problem, upload } from '../data/galleryUploadApi';
import { PeoplePicker, Details } from './GalleryFormParts';
import { Loading } from './ui/Loading';

// Gallery management. Any signed-in member uploads; the
// uploader, Admin or Owner edit and delete. The server decides and validates everything (GalleryManagementService).

/** Upload one photo or video; the people tagged get a push notification. */
export function UploadDialog({ onClose, onDone, event }: { onClose: () => void; onDone: () => void; event?: UploadEvent }) {
  const [file, setFile] = useState<File>();
  const [preview, setPreview] = useState<string>();
  const [form, setForm] = useState<Form>({ title: event?.title.slice(0, 200) ?? '', date: event?.date || today(), membersOnly: true, people: [], eventId: event?.id });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [progress, setProgress] = useState<number>();
  const id = useId();
  const set = (next: Partial<Form>) => setForm((f) => ({ ...f, ...next }));

  useEffect(() => () => void (preview && URL.revokeObjectURL(preview)), [preview]);

  const pick = (f?: File) => {
    if (!f) return;
    const video = f.type.startsWith('video/');
    if (!video && !f.type.startsWith('image/')) return setErrors({ file: 'Só dá para carregar fotos e vídeos.' });
    if (f.size > (video ? MAX_VIDEO : MAX_IMAGE)) return setErrors({ file: `O tamanho do ficheiro deve ser inferior a ${video ? 100 : 10} MB.` });
    setErrors({});
    setFile(f);
    setPreview(URL.createObjectURL(f));
    if (!form.title) set({ title: f.name.replace(/\.[^.]+$/, '').slice(0, 200) });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return setErrors({ file: 'Escolhe uma foto ou um vídeo.' });
    setProgress(0);
    const o = await upload(file, form, setProgress);
    setProgress(undefined);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setErrors(o.kind === 'invalid' ? o.errors : { form: o.message });
  };

  const busy = progress !== undefined;
  return (
    <Dialog
      title="Carregar para a galeria"
      onClose={busy ? () => undefined : onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !file}>
            <Icon name="upload" />
            {busy ? `A carregar… ${Math.round((progress ?? 0) * 100)}%` : 'Carregar'}
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <div className={errors.file ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-file`}>Foto ou vídeo</label>
          <input id={`${id}-file`} type="file" accept="image/*,video/*" onChange={(e) => pick(e.target.files?.[0])} disabled={busy} />
          <p className="form__hint">Imagens até 10 MB, vídeos até 100 MB.</p>
          <FieldError text={errors.file} alert={false} />
        </div>
        {event && (
          <p className="note">
            <Icon name="calendar" /> Fica associada ao evento «{event.title}». Só uma foto resolve o pedido da galeria; um vídeo não.
          </p>
        )}
        <FieldError text={errors.eventId} alert={false} />
        {file && preview && (
          <div className="gallery-manage__preview">
            {file.type.startsWith('video/') ? <video src={preview} controls playsInline preload="metadata" /> : <img src={preview} alt="" />}
          </div>
        )}
        <Details form={form} set={set} errors={errors} idPrefix={id} />
        <PeoplePicker people={form.people} onChange={(people) => set({ people })} />
        <FieldError text={errors.personIds} alert={false} />
        {form.people.length > 0 && (
          <p className="note">
            <Icon name="bell" /> {form.people.length === 1 ? 'A pessoa marcada recebe' : `As ${form.people.length} pessoas marcadas recebem`} uma notificação.
          </p>
        )}
        {busy && <progress className="gallery-manage__progress" max={1} value={progress} aria-label="Progresso do carregamento" />}
        <FieldError text={errors.form} alert={false} />
      </form>
    </Dialog>
  );
}

/** Edit title, date, members-only and who appears (no notification); delete behind a confirm. */
export function EditDialog({ itemId, onClose, onDone }: { itemId: number; onClose: () => void; onDone: () => void }) {
  const [data, setData] = useState<EditData | null>();
  const [form, setForm] = useState<Form>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    galleryApi.forEdit(itemId).then((o) => {
      if (o.kind !== 'ok') return setData(null);
      setData(o.data);
      setForm({ title: o.data.title, date: o.data.date, membersOnly: o.data.membersOnly, people: o.data.people });
    });
  }, [itemId]);

  const set = (next: Partial<Form>) => setForm((f) => (f ? { ...f, ...next } : f));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setBusy(true);
    const o = await galleryApi.update(itemId, form);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o, 'guardar') });
    onDone();
    onClose();
  };

  const remove = async () => {
    setBusy(true);
    const o = await galleryApi.remove(itemId);
    setBusy(false);
    setConfirming(false);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'apagar') });
    onDone();
    onClose();
  };

  return (
    <Dialog
      title="Editar media"
      onClose={onClose}
      footer={
        form &&
        (confirming ? (
          <>
            <span>Apagar de vez? O ficheiro também é apagado.</span>
            <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)}>
              Não
            </button>
            <button type="button" className="btn btn--danger" disabled={busy} onClick={remove}>
              Apagar
            </button>
          </>
        ) : (
          <>
            <button type="button" className="btn btn--ghost gallery-manage__delete" onClick={() => setConfirming(true)}>
              <Icon name="trash" />
              Apagar
            </button>
            <button type="button" className="btn btn--ghost" onClick={onClose}>
              Cancelar
            </button>
            <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
              Guardar
            </button>
          </>
        ))
      }
    >
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null || !form ? (
        <p>Não foi possível abrir esta media para edição.</p>
      ) : (
        <form id={`${id}-form`} className="form" onSubmit={save} noValidate>
          {data.url && (
            <div className="gallery-manage__preview">
              {data.type === 'video' ? <video src={data.url} controls playsInline preload="metadata" /> : <img src={data.url} alt="" />}
            </div>
          )}
          <Details form={form} set={set} errors={errors} idPrefix={id} />
          <PeoplePicker people={form.people} onChange={(people) => set({ people })} />
          <FieldError text={errors.personIds} alert={false} />
          <p className="form__hint">Mudar quem aparece não envia notificações.</p>
          <FieldError text={errors.form} alert={false} />
        </form>
      )}
    </Dialog>
  );
}
