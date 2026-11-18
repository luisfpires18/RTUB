// Naipes add / edit and delete dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { naipesApi, type NaipeBoard, type NaipeItem } from '../data/naipesApi';
import { MAX_VIDEO, MAX_IMAGE, VIDEO_ACCEPT, IMAGE_ACCEPT, problem, type Editing } from '../data/naipesShared';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { Banner } from './ui/Banner';
import { DialogFooter } from './ui/DialogFooter';
import { FormField } from './ui/FormField';

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


  return (
    <Dialog
      title={creating ? (isVideo ? 'Adicionar vídeo' : 'Adicionar imagem') : 'Editar conteúdo'}
      onClose={onClose}
      footer={<DialogFooter form={`${id}-form`} busy={busy} label={busy && creating ? 'A enviar…' : 'Guardar'} onClose={onClose} />}
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={banner} />
        {creating && (
          <>
            <FormField id={`${id}-instrument`} label="Instrumento" error={errors.instrument} alert>
              <span className="control control--select">
                <select id={`${id}-instrument`} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
                  {board.instrumentOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </span>
            </FormField>
            <FormField
              id={`${id}-file`}
              label={isVideo ? 'Ficheiro de vídeo' : 'Ficheiro de imagem'}
              hint={isVideo ? 'MP4, MOV, WebM ou 3GP, até 100 MB.' : 'JPEG, PNG, WebP ou GIF, até 10 MB.'}
              error={errors.file}
              alert
            >
              <input id={`${id}-file`} type="file" accept={isVideo ? VIDEO_ACCEPT : IMAGE_ACCEPT} onChange={(e) => pickFile(e.target.files?.[0])} />
            </FormField>
          </>
        )}
        <FormField id={`${id}-title`} label="Título" error={errors.title} alert>
          <input
            id={`${id}-title`}
            type="text"
            maxLength={200}
            value={title}
            placeholder={creating ? 'Se ficar vazio, recebe um nome automático' : undefined}
            onChange={(e) => setTitle(e.target.value)}
          />
        </FormField>
        <FormField id={`${id}-description`} label="Descrição" error={errors.description} alert>
          <textarea id={`${id}-description`} rows={3} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
        <FormField id={`${id}-order`} label="Ordem" hint="Os números mais baixos aparecem primeiro; usa decimais (1.5) para pôr entre dois." error={errors.sortOrder} alert>
          <input id={`${id}-order`} type="number" inputMode="decimal" step="0.1" min="0.1" max="999.9" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        </FormField>
      </form>
    </Dialog>
  );
}

export function DeleteDialog({ item, onClose, onDone }: { item: NaipeItem; onClose: () => void; onDone: () => void }) {

  const confirm = async () => {
    const o = await naipesApi.remove(item.id);
    if (o.kind !== 'ok') return problem(o, 'eliminar');
    onDone();
  };

  return (
    <AsyncConfirmDialog title="Eliminar conteúdo" confirmLabel="Eliminar" danger onConfirm={confirm} onClose={onClose}>
      <p>
        Eliminar <strong>{item.title}</strong>? O ficheiro e os comentários vão com ele, sem volta atrás.
      </p>
    </AsyncConfirmDialog>
  );
}
