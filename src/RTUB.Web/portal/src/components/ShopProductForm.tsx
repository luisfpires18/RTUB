// Shop product form and delete confirmation.
import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { type ProductInput, api, MAX_SOURCE, CROP_TEXT, problem } from '../data/shopApi';
import { Loading } from './ui/Loading';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

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
        {form === undefined ? <Loading label="A carregar…" /> : <p className="form__banner">Não foi possível abrir este produto.</p>}
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

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo produto' : 'Editar produto'}
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
            <div className={field('name')}>
              <label htmlFor={`${fid}-name`}>Nome</label>
              <input id={`${fid}-name`} type="text" maxLength={200} value={form.name} onChange={(e) => set({ name: e.target.value })} />
              {error('name')}
            </div>
            <div className={field('type')}>
              <label htmlFor={`${fid}-type`}>Tipo</label>
              <input id={`${fid}-type`} type="text" maxLength={50} placeholder="Roupa, Álbum, Pin, Outro…" value={form.type} onChange={(e) => set({ type: e.target.value })} />
              {error('type')}
            </div>
          </div>
          <div className="form__row">
            <div className={field('price')}>
              <label htmlFor={`${fid}-price`}>Preço (€)</label>
              <input id={`${fid}-price`} type="number" min="0.01" step="0.01" inputMode="decimal" value={priceText} onChange={(e) => setPriceText(e.target.value)} />
              {error('price')}
            </div>
            <div className={field('stock')}>
              <label htmlFor={`${fid}-stock`}>Stock</label>
              <input id={`${fid}-stock`} type="number" min="0" step="1" value={form.stock} onChange={(e) => set({ stock: Math.trunc(Number(e.target.value) || 0) })} />
              <p className="form__hint">Com stock 0 o produto fica esgotado e indisponível.</p>
              {error('stock')}
            </div>
          </div>
          <label className="form__check" htmlFor={`${fid}-public`}>
            <input id={`${fid}-public`} type="checkbox" checked={form.isPublic} onChange={(e) => set({ isPublic: e.target.checked })} />
            Público (não membros). Sem esta opção, o produto é só para membros e pode ser reservado.
          </label>
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
          <div className={field('description')}>
            <label htmlFor={`${fid}-description`}>Descrição</label>
            <textarea id={`${fid}-description`} rows={3} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
            {error('description')}
          </div>
          {canPickImage && (
            <div className={field('image')}>
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
              {error('image')}
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
