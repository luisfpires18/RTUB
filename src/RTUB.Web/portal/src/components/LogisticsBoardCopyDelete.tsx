// The duplicate board and delete board dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { logisticsApi, problem, type BoardSummary } from '../data/logisticsApi';

/** "Duplicar quadro": says what is copied (and what is not) before creating it. */
export function DuplicateBoardDialog({ board, onClose }: { board: BoardSummary; onClose: () => void }) {
  const [name, setName] = useState(`${board.name} (cópia)`.slice(0, 200));
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const create = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await logisticsApi.duplicateBoard(board.id, name.trim());
    setBusy(false);
    if (o.kind === 'ok') location.href = `/logistics/${o.data.id}`;
    else setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : problem(o, 'duplicar o quadro'));
  };

  return (
    <Dialog
      title="Duplicar quadro"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Criar cópia
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={create} noValidate>
        <div className="form__field">
          <label htmlFor={`${fid}-name`}>Nome da cópia</label>
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="lx-copy-preview">
          <p>
            <strong>Copia:</strong> as colunas, os cartões ativos (título, detalhes, etiquetas, prioridade, datas, checklist por fazer), a visibilidade
            {board.visibility === 'Private' ? ' privada e a lista de acesso' : ''} e o responsável.
          </p>
          <p>
            <strong>Não copia:</strong> responsáveis e colaboradores dos cartões, comentários, histórico, anexos, lembretes, cartões arquivados e
            ficheiros do quadro.
          </p>
        </div>
        {error && (
          <p className="form__banner" role="alert">
            {error}
          </p>
        )}
      </form>
    </Dialog>
  );
}

export function DeleteBoardDialog({ board, onClose, onDone }: { board: BoardSummary; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const remove = async () => {
    setBusy(true);
    const o = await logisticsApi.removeBoard(board.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar o quadro'));
  };
  return (
    <Dialog
      title="Eliminar quadro"
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
        Eliminar o quadro <strong>{board.name}</strong>?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Apaga também as colunas, os cartões e os seus anexos. Para guardar sem apagar, use Arquivar. Não dá para desfazer.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
