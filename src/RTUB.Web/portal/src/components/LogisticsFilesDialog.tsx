// The board's files dialog.
import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { logisticsApi, problem, type Board } from '../data/logisticsApi';
import { FILE_TYPES, MAX_FILE, size } from '../data/logisticsFiles';
import { Banner } from './ui/Banner';
import { Spinner } from './ui/Spinner';
import { EmptyNote } from './ui/EmptyNote';
import { IconAction } from './ui/IconAction';
import { CloseFooter } from './ui/DialogFooter';

/** The board's files, shared by all its cards. Every member downloads; managers upload and delete. */
export function FilesDialog({ board, onClose, onChanged }: { board: Board; onClose: () => void; onChanged: () => void }) {
  const [files, setFiles] = useState(board.files);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<string>();
  const fid = useId();

  useEffect(() => setFiles(board.files), [board.files]);

  const download = async (name: string) => {
    setError(undefined);
    const o = await logisticsApi.file(board.id, name);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener');
    else setError(problem(o, 'abrir o ficheiro'));
  };

  const upload = async (file: File | undefined) => {
    setError(undefined);
    if (!file) return;
    if (file.size > MAX_FILE) return setError('Ficheiro grande demais: o limite são 10 MB.');
    if (!FILE_TYPES.includes(file.type)) return setError('Só PDF, Word, Excel, TXT, PNG ou JPG.');
    setBusy(true);
    const o = await logisticsApi.upload(board.id, file);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'carregar o ficheiro'));
    onChanged();
  };

  const remove = async (name: string) => {
    setConfirm(undefined);
    setBusy(true);
    const o = await logisticsApi.removeFile(board.id, name);
    setBusy(false);
    if (o.kind !== 'ok' && o.kind !== 'notfound') return setError(problem(o, 'eliminar o ficheiro'));
    onChanged();
  };

  return (
    <Dialog
      title="Ficheiros do quadro"
      onClose={onClose}
      footer={<CloseFooter onClose={onClose} />}
    >
      <p className="note">
        São os mesmos para todos os cartões e ficam com o quadro (mudar o nome ou o ano letivo não os perde). Os ficheiros de anos anteriores aparecem com o
        ano.
      </p>
      {files.length === 0 ? (
        <EmptyNote>Ainda sem ficheiros.</EmptyNote>
      ) : (
        <ul className="lx-files">
          {files.map((f) => (
            <li key={f.id}>
              <Icon name="file" />
              <span className="lx-files__name">
                {f.name}
                <small>
                  {f.extension.replace('.', '').toUpperCase()} · {size(f.sizeBytes)}
                  {f.year && ` · ${f.year}`}
                </small>
              </span>
              <IconAction icon="download" label={`Descarregar ${f.name}`} onClick={() => download(f.id)} />
              {board.canManage &&
                (confirm === f.id ? (
                  <button type="button" className="btn btn--danger btn--sm" onClick={() => remove(f.id)} disabled={busy}>
                    Eliminar?
                  </button>
                ) : (
                  <IconAction icon="trash" label={`Eliminar ${f.name}`} onClick={() => setConfirm(f.id)} danger />
                ))}
            </li>
          ))}
        </ul>
      )}
      {board.canManage && (
        <div className="lx-upload">
          <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
            {busy ? <Spinner small /> : <Icon name="upload" />}
            Carregar ficheiro
          </label>
          <input
            id={`${fid}-file`}
            className="sr-only"
            type="file"
            accept=".pdf,.docx,.doc,.xlsx,.xls,.txt,.png,.jpg,.jpeg"
            disabled={busy}
            onChange={(e) => {
              upload(e.target.files?.[0]);
              e.target.value = '';
            }}
          />
          <small className="note">PDF, Word, Excel, TXT, PNG ou JPG, até 10 MB.</small>
        </div>
      )}
      <Banner text={error} />
    </Dialog>
  );
}
