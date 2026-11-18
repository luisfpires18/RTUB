// Inventory add / edit instrument form dialog.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { type Option, type Input, api, MAX_IMAGE, IMAGE_TYPES, CROP_TEXT, problem } from '../data/inventoryApi';
import { Thumb } from './InventoryDialogs';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';
import { DialogFooter } from './ui/DialogFooter';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';

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

/** "Novo instrumento" / "Editar instrumento": the instrument's fields; the type is chosen once, when the instrument is added. */
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
        {form === undefined ? <Loading label="A carregar…" /> : <Banner role="none" text="Não foi possível abrir este instrumento." />}
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
  const text = (key: 'name' | 'brand' | 'serialNumber' | 'location', label: string, max: number) => (
    <FormField id={`${fid}-${key}`} label={label} error={errors[key]} alert>
      <input id={`${fid}-${key}`} type="text" maxLength={max} value={form[key]} onChange={(e) => set({ [key]: e.target.value })} />
    </FormField>
  );

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo instrumento' : 'Editar instrumento'}
        size="lg"
        onClose={onClose}
        footer={<DialogFooter form={`${fid}-form`} busy={busy} onClose={onClose} />}
      >
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          <Banner text={banner} />
          <div className="form__row">
            {text('name', 'Nome', 100)}
            <FormField id={`${fid}-category`} label="Tipo" hint={savedId !== undefined ? 'O tipo fica como foi escolhido ao adicionar.' : undefined} error={errors.category} alert>
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
            </FormField>
          </div>
          <div className="form__row">
            {text('brand', 'Marca', 100)}
            {text('serialNumber', 'Número de série', 100)}
          </div>
          <div className="form__row">
            <FormField id={`${fid}-condition`} label="Condição" error={errors.condition} alert>
              <span className="control control--select">
                <select id={`${fid}-condition`} value={form.condition} onChange={(e) => set({ condition: e.target.value })}>
                  {conditions.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </span>
            </FormField>
            {text('location', 'Localização', 200)}
          </div>
          <div className="form__row">
            <FormField id={`${fid}-date`} label="Última manutenção" error={errors.lastMaintenanceDate} alert>
              <input id={`${fid}-date`} type="date" value={form.lastMaintenanceDate ?? ''} onChange={(e) => set({ lastMaintenanceDate: e.target.value || null })} />
            </FormField>
            <FormField id={`${fid}-notes`} label="Notas de manutenção" error={errors.maintenanceNotes} alert>
              <textarea id={`${fid}-notes`} rows={3} maxLength={500} value={form.maintenanceNotes} onChange={(e) => set({ maintenanceNotes: e.target.value })} />
            </FormField>
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
            <FieldError text={errors.image} />
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
