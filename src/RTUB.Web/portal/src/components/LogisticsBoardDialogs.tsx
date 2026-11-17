// The dialogs a board page opens: card workspace, column, create / delete card, move card, files.
import { CardWorkspace } from './LogisticsCard';
import { logisticsApi, type Board } from '../data/logisticsApi';
import type { Open } from '../data/logisticsBoardTypes';
import { DeleteListDialog, MoveCardDialog } from './LogisticsMoveDialogs';
import { FilesDialog } from './LogisticsFilesDialog';
import { ConfirmDialog, CreateCardDialog, ListDialog } from './LogisticsDialogs';

/** The dialog of whatever the board page has open. */
export function LogisticsBoardDialogs({ open, setOpen, data, load, loadFiles, moveCard }: { open?: Open; setOpen: (o: Open | undefined) => void; data: Board; load: () => void; loadFiles: () => void; moveCard: (cardId: number, listId: number, position: number) => void }) {
  return (
    <>
      {open?.kind === 'card' && (
        <CardWorkspace
          board={data}
          cardId={open.id}
          onClose={() => setOpen(undefined)}
          onChanged={load}
          onDelete={(card) => setOpen({ kind: 'delete-card', card })}
        />
      )}
      {open?.kind === 'create-card' && (
        <CreateCardDialog
          boardId={data.id}
          list={open.list}
          onClose={() => setOpen(undefined)}
          onCreated={(id) => {
            load();
            setOpen({ kind: 'card', id });
          }}
        />
      )}
      {open?.kind === 'list' && <ListDialog boardId={data.id} list={open.list} onClose={() => setOpen(undefined)} onSaved={load} />}
      {open?.kind === 'delete-list' && <DeleteListDialog board={data} list={open.list} onClose={() => setOpen(undefined)} onDone={load} />}
      {open?.kind === 'delete-card' && (
        <ConfirmDialog
          title="Eliminar cartão"
          what={<>o cartão <strong>{open.card.title}</strong></>}
          warning="Não dá para desfazer."
          action={() => logisticsApi.removeCard(open.card.id)}
          onClose={() => setOpen(undefined)}
          onDone={load}
        />
      )}
      {open?.kind === 'move' && (
        <MoveCardDialog
          board={data}
          card={open.card}
          listId={open.listId}
          onClose={() => setOpen(undefined)}
          onMove={(listId, position) => {
            setOpen(undefined);
            moveCard(open.card.id, listId, position);
          }}
        />
      )}
      {open?.kind === 'files' && <FilesDialog board={data} onClose={() => setOpen(undefined)} onChanged={loadFiles} />}
    </>
  );
}
