// Inventory add / edit instrument form dialog.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './AppShared';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { type Option, type Input, api, MAX_IMAGE, IMAGE_TYPES, CROP_TEXT, problem } from '../data/inventoryApi';
import { Thumb } from './InventoryDialogs';

const blank = (): Input => ({
  name: '',
  category: '',
  condition: 'Good',
  brand: '',
  serialNumber: '',
  location: '',
  lastMaintenanceDate: null,
  maintenanceNotes: '',
});

/** "Novo instrumento" / "Editar instrumento": the old fields; the type is chosen once, when the instrument is added. */
export function FormDialog({
  id,
  categories,
  conditions,
  onClose,
  onSaved,
}: {
  id?: number;
  categories: Option[];
  conditions: Option[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<Input | 'failed' | undefined>(id === undefined ? blank() : undefined);
  const [savedId, setSavedId] = useState(id);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [picked, setPicked] = useState<File | null>(null);
  const [image, setImage] = useState<{ file: File; thumbnail: Blob; preview: string } | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    if (id === undefined) return;
    api.get(id).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      const d = o.data;
      setForm({
        name: d.name,
        category: d.category,
        condition: d.condition,
        brand: d.brand ?? '',
        serialNumber: d.serialNumber ?? '',
        location: d.location ?? '',
        lastMaintenanceDate: d.lastMaintenanceDate,
        maintenanceNotes: d.maintenanceNotes ?? '',
      });
      setCurrentImage(d.thumbnailUrl ?? d.imageUrl);
    });
  }, [id]);

  if (form === undefined || form === 'failed') {
    return (
      <Dialog title="Editar instrumento" onClose={onClose}>
        {form === undefined ? <Loading label="A carregar…" /> : <p className="form__banner">Não foi possível abrir este instrumento.</p>}
      </Dialog>
    );
  }

  const set = (next: Partial<Input>) => setForm({ ...form, ...next });

  const pick = (file: File | undefined) => {
    if (!file) return;
    if (!IMAGE_TYPES.includes(file.type)) return setErrors({ ...errors, image: 'A imagem tem de ser WebP, JPEG ou PNG.' });
    if (file.size > MAX_IMAGE) return setErrors({ ...errors, image: 'A imagem não pode exceder 10 MB.' });
    const { image: _, ...rest } = errors;
    setErrors(rest);
    setPicked(file);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const saved = savedId === undefined ? await api.create(form) : await api.update(savedId, form);
    if (saved.kind !== 'ok') {
      setBusy(false);
      if (saved.kind === 'invalid') return setErrors(saved.errors);
      return setBanner(problem(saved, 'guardar'));
    }
    const newId = savedId ?? (saved.data as { id: number }).id;
    setSavedId(newId);
    onSaved();
    if (image) {
      const uploaded = await api.image(newId, image.file, image.thumbnail);
      setBusy(false);
      if (uploaded.kind !== 'ok') {
        setErrors(uploaded.kind === 'invalid' ? { image: Object.values(uploaded.errors)[0] } : {});
        return setBanner(`O instrumento foi guardado, mas a imagem não. ${uploaded.kind === 'invalid' ? '' : problem(uploaded, 'guardar a imagem')}`.trim());
      }
      onSaved();
    }
    setBusy(false);
    onClose();
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  const text = (key: 'name' | 'brand' | 'serialNumber' | 'location', label: string, max: number) => (
    <div className={field(key)}>
      <label htmlFor={`${fid}-${key}`}>{label}</label>
      <input id={`${fid}-${key}`} type="text" maxLength={max} value={form[key]} onChange={(e) => set({ [key]: e.target.value })} />
      {error(key)}
    </div>
  );

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo instrumento' : 'Editar instrumento'}
        size="lg"
        onClose={onClose}
        footer={
          <>
            <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
              Cancelar
            </button>
            <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy}>
              {busy && <span className="spinner spinner--small" aria-hidden="true" />}
              Guardar
            </button>
          </>
        }
      >
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          {banner && (
            <p className="form__banner" role="alert">
              {banner}
            </p>
          )}
          <div className="form__row">
            {text('name', 'Nome', 100)}
            <div className={field('category')}>
              <label htmlFor={`${fid}-category`}>Tipo</label>
              <span className="control control--select">
                <select id={`${fid}-category`} value={form.category} disabled={savedId !== undefined} onChange={(e) => set({ category: e.target.value })}>
                  <option value="">Escolher…</option>
                  {categories.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                  {form.category && !categories.some((c) => c.value === form.category) && <option value={form.category}>{form.category}</option>}
                </select>
              </span>
              {savedId !== undefined && <p className="form__hint">O tipo fica como foi escolhido ao adicionar.</p>}
              {error('category')}
            </div>
          </div>
          <div className="form__row">
            {text('brand', 'Marca', 100)}
            {text('serialNumber', 'Número de série', 100)}
          </div>
          <div className="form__row">
            <div className={field('condition')}>
              <label htmlFor={`${fid}-condition`}>Condição</label>
              <span className="control control--select">
                <select id={`${fid}-condition`} value={form.condition} onChange={(e) => set({ condition: e.target.value })}>
                  {conditions.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </span>
              {error('condition')}
            </div>
            {text('location', 'Localização', 200)}
          </div>
          <div className="form__row">
            <div className={field('lastMaintenanceDate')}>
              <label htmlFor={`${fid}-date`}>Última manutenção</label>
              <input id={`${fid}-date`} type="date" value={form.lastMaintenanceDate ?? ''} onChange={(e) => set({ lastMaintenanceDate: e.target.value || null })} />
              {error('lastMaintenanceDate')}
            </div>
            <div className={field('maintenanceNotes')}>
              <label htmlFor={`${fid}-notes`}>Notas de manutenção</label>
              <textarea id={`${fid}-notes`} rows={3} maxLength={500} value={form.maintenanceNotes} onChange={(e) => set({ maintenanceNotes: e.target.value })} />
              {error('maintenanceNotes')}
            </div>
          </div>
          <div className={field('image')}>
            <span className="form__label">Imagem</span>
            <span className="inventory-image-pick">
              {image ? (
                <img className="inventory-thumb" src={image.preview} alt="" width={72} height={72} />
              ) : (
                <Thumb url={currentImage} size={72} />
              )}
              <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
                <Icon name="upload" />
                {image || currentImage ? 'Trocar imagem' : 'Escolher imagem'}
              </label>
              <input
                id={`${fid}-file`}
                className="sr-only"
                type="file"
                accept={IMAGE_TYPES.join(',')}
                onChange={(e) => {
                  pick(e.target.files?.[0]);
                  e.target.value = '';
                }}
              />
            </span>
            <p className="form__hint">WebP, JPEG ou PNG até 10 MB. Depois recortas a miniatura quadrada.</p>
            {error('image')}
          </div>
        </form>
      </Dialog>
      {picked && (
        <Cropper
          file={picked}
          text={CROP_TEXT}
          outWidth={600}
          onCancel={() => setPicked(null)}
          onDone={(thumbnail, preview) => {
            setImage({ file: picked, thumbnail, preview });
            setPicked(null);
          }}
        />
      )}
    </>
  );
}
