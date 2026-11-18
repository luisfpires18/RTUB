// Documentation folder, upload and delete dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { type Folder, type Docs, type Target, api, problem, fileSize } from '../data/documentationApi';

/** "Criar pasta": always in the current fiscal year. */
export function FolderDialog({ onClose, onDone }: { onClose: () => void; onDone: (name: string) => void }) {
  const [name, setName] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await api.createFolder(name);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'criar a pasta'));
    onDone(o.data.name);
    onClose();
  };

  return (
    <Dialog
      title="Criar pasta"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || name.trim() === ''}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Criar
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-name`}>Nome da pasta</label>
          <input id={`${fid}-name`} type="text" maxLength={50} placeholder="Ex.: Regulamentos" value={name} onChange={(e) => setName(e.target.value)} />
          <p className="form__hint">Letras, números, espaços e hífens. A pasta fica no ano letivo atual.</p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
      </form>
    </Dialog>
  );
}

export function UploadDialog({ docs, folder, onClose, onDone }: { docs: Docs; folder: Folder; onClose: () => void; onDone: () => void }) {
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const types = docs.extensions.map((e) => e.replace('.', '').toUpperCase()).join(', ');

  const pick = (picked: File | undefined) => {
    setError(undefined);
    setFile(null);
    if (!picked) return;
    const extension = picked.name.includes('.') ? picked.name.slice(picked.name.lastIndexOf('.')).toLowerCase() : '';
    if (!docs.extensions.includes(extension)) return setError(`Tipo de ficheiro não permitido. Tipos permitidos: ${types}.`);
    if (picked.size > docs.maxFileBytes) return setError(`O ficheiro é muito grande (${fileSize(picked.size)}). Tamanho máximo: ${fileSize(docs.maxFileBytes)}.`);
    setFile(picked);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setBusy(true);
    const o = await api.upload(docs.fiscalYear, folder.name, file);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'carregar o ficheiro'));
    onDone();
    onClose();
  };

  return (
    <Dialog
      title={`Carregar documento · ${folder.label}`}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !file}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {busy ? 'A carregar…' : 'Carregar'}
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Ficheiro</span>
          <span className="doc-pick">
            <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
              <Icon name="upload" />
              Escolher ficheiro
            </label>
            <span className="doc-pick__name">{file ? `${file.name} (${fileSize(file.size)})` : 'Sem ficheiro escolhido'}</span>
            <input
              id={`${fid}-file`}
              className="sr-only"
              type="file"
              accept={docs.extensions.join(',')}
              onChange={(e) => {
                pick(e.target.files?.[0]);
                e.target.value = '';
              }}
            />
          </span>
          <p className="form__hint">
            Tipos permitidos: {types} (máx. {fileSize(docs.maxFileBytes)}).
            {docs.canManage && ' Um ficheiro com o mesmo nome substitui o que já está na pasta.'}
          </p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
      </form>
    </Dialog>
  );
}

export function DeleteDialog({ fiscalYear, target, onClose, onDone }: { fiscalYear: string; target: Target; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const { folder, document } = target;

  const remove = async () => {
    setBusy(true);
    const o = document ? await api.removeDocument(fiscalYear, folder.name, document.name) : await api.removeFolder(fiscalYear, folder.name);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title={document ? 'Eliminar documento' : 'Eliminar pasta'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar {document ? 'o documento' : 'a pasta'} <strong>{document?.name ?? folder.label}</strong>?
      </p>
      <p className="warning">
        <Icon name="warning" />
        {document ? 'Não dá para desfazer.' : 'Elimina também todos os documentos da pasta. Não dá para desfazer.'}
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
