// Naipes add / edit and delete dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { naipesApi, type NaipeBoard, type NaipeItem } from '../data/naipesApi';
import { MAX_VIDEO, MAX_IMAGE, VIDEO_ACCEPT, IMAGE_ACCEPT, problem, type Editing } from '../data/naipesShared';

/** "Adicionar vídeo" / "Adicionar imagem" (instrument, file, title, description, order) and "Editar". */
export function EditDialog({ board, editing, onClose, onSaved }: { board: NaipeBoard; editing: Editing; onClose: () => void; onSaved: (item: NaipeItem) => void }) {
  const creating = editing.kind === 'new';
  const isVideo = editing.kind === 'new' ? editing.isVideo : editing.item.isVideo;
  const item = editing.kind === 'edit' ? editing.item : undefined;
  const [instrument, setInstrument] = useState(board.instrument ?? board.instrumentOptions[0]?.value ?? '');
  const [file, setFile] = useState<File>();
  const [title, setTitle] = useState(item?.title ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [sortOrder, setSortOrder] = useState(String(item?.sortOrder ?? board.nextSortOrder));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();
  const max = isVideo ? MAX_VIDEO : MAX_IMAGE;

  const pickFile = (picked?: File) => {
    if (!picked) return setFile(undefined);
    if (picked.size > max) {
      setFile(undefined);
      return setErrors({ ...errors, file: isVideo ? 'O vídeo não pode exceder 100 MB.' : 'A imagem não pode exceder 10 MB.' });
    }
    setErrors({ ...errors, file: '' });
    setFile(picked);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (creating && !file) return setErrors({ ...errors, file: 'O ficheiro é obrigatório.' });
    setBusy(true);
    setBanner(undefined);
    const o = creating
      ? await naipesApi.create({ file: file!, instrument, isVideo, title, description, sortOrder })
      : await naipesApi.update(item!.id, title, description, sortOrder.trim() ? Number(sortOrder.replace(',', '.')) : null);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved(o.data);
      onClose();
    } else if (o.kind === 'invalid') setErrors(o.errors);
    else {
      setErrors({});
      setBanner(problem(o, 'guardar'));
    }
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={creating ? (isVideo ? 'Adicionar vídeo' : 'Adicionar imagem') : 'Editar conteúdo'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {busy && creating ? 'A enviar…' : 'Guardar'}
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        {creating && (
          <>
            <div className={field('instrument')}>
              <label htmlFor={`${id}-instrument`}>Instrumento</label>
              <span className="control control--select">
                <select id={`${id}-instrument`} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
                  {board.instrumentOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </span>
              {error('instrument')}
            </div>
            <div className={field('file')}>
              <label htmlFor={`${id}-file`}>{isVideo ? 'Ficheiro de vídeo' : 'Ficheiro de imagem'}</label>
              <input id={`${id}-file`} type="file" accept={isVideo ? VIDEO_ACCEPT : IMAGE_ACCEPT} onChange={(e) => pickFile(e.target.files?.[0])} />
              <p className="form__hint">{isVideo ? 'MP4, MOV, WebM ou 3GP, até 100 MB.' : 'JPEG, PNG, WebP ou GIF, até 10 MB.'}</p>
              {error('file')}
            </div>
          </>
        )}
        <div className={field('title')}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input
            id={`${id}-title`}
            type="text"
            maxLength={200}
            value={title}
            placeholder={creating ? 'Se ficar vazio, recebe um nome automático' : undefined}
            onChange={(e) => setTitle(e.target.value)}
          />
          {error('title')}
        </div>
        <div className={field('description')}>
          <label htmlFor={`${id}-description`}>Descrição</label>
          <textarea id={`${id}-description`} rows={3} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} />
          {error('description')}
        </div>
        <div className={field('sortOrder')}>
          <label htmlFor={`${id}-order`}>Ordem</label>
          <input id={`${id}-order`} type="number" inputMode="decimal" step="0.1" min="0.1" max="999.9" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
          <p className="form__hint">Os números mais baixos aparecem primeiro; usa decimais (1.5) para pôr entre dois.</p>
          {error('sortOrder')}
        </div>
      </form>
    </Dialog>
  );
}

export function DeleteDialog({ item, onClose, onDone }: { item: NaipeItem; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    const o = await naipesApi.remove(item.id);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title="Eliminar conteúdo"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar <strong>{item.title}</strong>? O ficheiro e os comentários vão com ele, sem volta atrás.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
