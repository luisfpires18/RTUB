// Shop product form and delete confirmation.
import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { type ProductInput, api, MAX_SOURCE, CROP_TEXT, problem } from '../data/shopApi';
import { Loading } from './ui/Loading';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { DialogFooter } from './ui/DialogFooter';
import { FormField } from './ui/FormField';
import { FieldError } from './ui/FieldError';
import { Banner } from './ui/Banner';
import { CheckField } from './ui/CheckField';

export function Confirm({
  title,
  label,
  run,
  onClose,
  onDone,
  children,
}: {
  title: string;
  label: string;
  run: () => Promise<Outcome<unknown>>;
  onClose: () => void;
  onDone: () => void;
  children: ReactNode;
}) {
  const go = async () => {
    const o = await run();
    if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, title.toLowerCase());
    onDone();
  };
  return (
    <AsyncConfirmDialog title={title} confirmLabel={label} danger onConfirm={go} onClose={onClose}>
      {children}
    </AsyncConfirmDialog>
  );
}

const blank = (): ProductInput => ({ name: '', type: '', price: 0, stock: 0, isPublic: true, description: '', availableAllYears: false });

/** "Novo produto" (Mod, Admin, Owner) / "Editar produto" (Admin, Owner), with the square image. */
export function ProductForm({ id, canManage, onClose, onSaved }: { id?: number; canManage: boolean; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<ProductInput | 'failed' | undefined>(id === undefined ? blank() : undefined);
  const [priceText, setPriceText] = useState('');
  const [savedId, setSavedId] = useState(id);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  // The fiscal year of the product's creation (edit only): where a year-specific product is shown.
  const [createdYear, setCreatedYear] = useState<string>();
  const [picked, setPicked] = useState<File | null>(null);
  const [image, setImage] = useState<{ blob: Blob; preview: string } | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    if (id === undefined) return;
    api.product(id).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      const d = o.data;
      setForm({
        name: d.name,
        type: d.type,
        price: d.price,
        stock: d.stock,
        isPublic: d.isPublic,
        description: d.description ?? '',
        availableAllYears: d.availableAllYears,
      });
      setPriceText(d.price.toFixed(2));
      setCurrentImage(d.imageUrl);
      setCreatedYear(d.fiscalYear);
    });
  }, [id]);

  if (form === undefined || form === 'failed') {
    return (
      <Dialog title="Editar produto" onClose={onClose}>
        {form === undefined ? <Loading label="A carregar…" /> : <Banner role="none" text="Não foi possível abrir este produto." />}
      </Dialog>
    );
  }

  const set = (next: Partial<ProductInput>) => setForm({ ...form, ...next });
  // Only Admin and Owner change an image; a Mod adding a product gives it its first one.
  const canPickImage = canManage || (savedId === undefined && !currentImage);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const input = { ...form, price: Number(priceText.replace(',', '.')) || 0 };
    const saved = savedId === undefined ? await api.create(input) : await api.update(savedId, input);
    if (saved.kind !== 'ok') {
      setBusy(false);
      if (saved.kind === 'invalid') return setErrors(saved.errors);
      return setBanner(problem(saved, 'guardar'));
    }
    const newId = savedId ?? (saved.data as { id: number }).id;
    setSavedId(newId);
    onSaved();
    if (image) {
      const uploaded = await api.image(newId, image.blob);
      setBusy(false);
      if (uploaded.kind !== 'ok') {
        setErrors(uploaded.kind === 'invalid' ? { image: Object.values(uploaded.errors)[0] } : {});
        return setBanner('O produto foi guardado, mas a imagem não.');
      }
      onSaved();
    }
    setBusy(false);
    onClose();
  };

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo produto' : 'Editar produto'}
        size="lg"
        onClose={onClose}
        footer={<DialogFooter form={`${fid}-form`} busy={busy} onClose={onClose} />}
      >
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          <Banner text={banner} />
          <div className="form__row">
            <FormField alert id={`${fid}-name`} label="Nome" error={errors.name}>
              <input id={`${fid}-name`} type="text" maxLength={200} value={form.name} onChange={(e) => set({ name: e.target.value })} />
            </FormField>
            <FormField alert id={`${fid}-type`} label="Tipo" error={errors.type}>
              <input id={`${fid}-type`} type="text" maxLength={50} placeholder="Roupa, Álbum, Pin, Outro…" value={form.type} onChange={(e) => set({ type: e.target.value })} />
            </FormField>
          </div>
          <div className="form__row">
            <FormField alert id={`${fid}-price`} label="Preço (€)" error={errors.price}>
              <input id={`${fid}-price`} type="number" min="0.01" step="0.01" inputMode="decimal" value={priceText} onChange={(e) => setPriceText(e.target.value)} />
            </FormField>
            <FormField alert id={`${fid}-stock`} label="Stock" error={errors.stock} hint="Com stock 0 o produto fica esgotado e indisponível.">
              <input id={`${fid}-stock`} type="number" min="0" step="1" value={form.stock} onChange={(e) => set({ stock: Math.trunc(Number(e.target.value) || 0) })} />
            </FormField>
          </div>
          <CheckField id={`${fid}-public`} checked={form.isPublic} onChange={(on) => set({ isPublic: on })}>
            Público (não membros). Sem esta opção, o produto é só para membros e pode ser reservado.
          </CheckField>
          <div className="form__field">
            <label className="form__check" htmlFor={`${fid}-all-years`}>
              <input
                id={`${fid}-all-years`}
                type="checkbox"
                aria-describedby={`${fid}-all-years-hint`}
                checked={form.availableAllYears}
                onChange={(e) => set({ availableAllYears: e.target.checked })}
              />
              Disponível em todos os anos letivos
            </label>
            <p id={`${fid}-all-years-hint`} className="form__hint">
              {form.availableAllYears
                ? 'Aparece na loja em qualquer ano letivo (por exemplo, CDs). O stock continua a contar.'
                : createdYear
                  ? `Aparece só no ano letivo ${createdYear}, em que foi criado.`
                  : 'Aparece só no ano letivo em que é criado.'}
            </p>
          </div>
          <FormField alert id={`${fid}-description`} label="Descrição" error={errors.description}>
            <textarea id={`${fid}-description`} rows={3} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
          </FormField>
          {canPickImage && (
            <div className={errors.image ? 'form__field form__field--error' : 'form__field'}>
              <span className="form__label">Imagem</span>
              <span className="inventory-image-pick">
                {image ? (
                  <img className="shop-thumb" src={image.preview} alt="" width={72} height={72} />
                ) : (
                  currentImage && <img className="shop-thumb" src={currentImage} alt="" width={72} height={72} />
                )}
                <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
                  <Icon name="upload" />
                  {image || currentImage ? 'Trocar imagem' : 'Escolher imagem'}
                </label>
                <input
                  id={`${fid}-file`}
                  className="sr-only"
                  type="file"
                  accept="image/*"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = '';
                    if (!file) return;
                    if (!file.type.startsWith('image/')) return setErrors({ ...errors, image: 'Escolha um ficheiro de imagem.' });
                    if (file.size > MAX_SOURCE) return setErrors({ ...errors, image: 'A imagem não pode exceder 10 MB.' });
                    setPicked(file);
                  }}
                />
              </span>
              <p className="form__hint">A imagem é recortada em quadrado.</p>
              <FieldError text={errors.image} />
            </div>
          )}
        </form>
      </Dialog>
      {picked && (
        <Cropper
          file={picked}
          text={CROP_TEXT}
          outWidth={800}
          onCancel={() => setPicked(null)}
          onDone={(blob, preview) => {
            setImage({ blob, preview });
            setPicked(null);
          }}
        />
      )}
    </>
  );
}
