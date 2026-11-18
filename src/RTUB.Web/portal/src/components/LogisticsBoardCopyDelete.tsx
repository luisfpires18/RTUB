// The duplicate board and delete board dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { logisticsApi, problem, type BoardSummary } from '../data/logisticsApi';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { Banner } from './ui/Banner';
import { DialogFooter } from './ui/DialogFooter';
import { FormField } from './ui/FormField';

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
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!name.trim()} label="Criar cópia" onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={create} noValidate>
        <FormField id={`${fid}-name`} label="Nome da cópia">
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
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
        <Banner text={error} />
      </form>
    </Dialog>
  );
}

export function DeleteBoardDialog({ board, onClose, onDone }: { board: BoardSummary; onClose: () => void; onDone: () => void }) {
  const remove = async () => {
    const o = await logisticsApi.removeBoard(board.id);
    if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, 'eliminar o quadro');
    onDone();
  };
  return (
    <AsyncConfirmDialog title="Eliminar quadro" confirmLabel="Eliminar" danger onConfirm={remove} onClose={onClose}>
      <p>
        Eliminar o quadro <strong>{board.name}</strong>?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Apaga também as colunas, os cartões e os seus anexos. Para guardar sem apagar, use Arquivar. Não dá para desfazer.
      </p>
    </AsyncConfirmDialog>
  );
}
