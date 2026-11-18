// Renders the one open /meetings dialog for the page state (Open).
import { AtaEditorDialog } from './MeetingAta';
import { AtaViewDialog } from './MeetingAtaView';
import { CancelDialog } from './MeetingCancelDialog';
import { problem } from './MeetingUi';
import { DetailsDialog } from './MeetingDetailsDialog';
import { EmailDialog } from './MeetingEmailDialog';
import { MeetingFormDialog } from './MeetingFormDialog';
import { ParticipantsDialog } from './MeetingParticipantsDialog';
import { ParticipationDialog } from './MeetingParticipationDialog';
import { ProposalDialog } from './MeetingProposalDialogs';
import { PushDialog } from './MeetingPushDialog';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingBoard, MeetingCard, MeetingDraft } from '../data/meetingsTypes';
import { type Flash } from '../pages/Meetings';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

export type Open =
  | { kind: 'details'; card: MeetingCard }
  | { kind: 'form'; card?: MeetingCard; draft?: MeetingDraft }
  | { kind: 'delete'; card: MeetingCard }
  | { kind: 'respond'; card: MeetingCard; willAttend: boolean }
  | { kind: 'removeOwn'; card: MeetingCard }
  | { kind: 'participants'; card: MeetingCard }
  | { kind: 'email'; card: MeetingCard }
  | { kind: 'push'; card: MeetingCard }
  | { kind: 'cancel'; card: MeetingCard }
  | { kind: 'uncancel'; card: MeetingCard }
  | { kind: 'ata'; card: MeetingCard }
  | { kind: 'viewAta'; card: MeetingCard }
  | { kind: 'propose'; type: string };

export function Dialogs({
  open,
  board,
  onClose,
  onChanged,
  onFlash,
  onRequestsChanged,
}: {
  open: Open;
  board: MeetingBoard;
  onClose: () => void;
  onChanged: () => void;
  onFlash: (f: Flash) => void;
  onRequestsChanged: () => void;
}) {
  const done = (message?: string, warning?: string | null) => {
    onChanged();
    if (warning) onFlash({ tone: 'warn', text: message ? `${message} ${warning}` : warning });
    else if (message) onFlash({ tone: 'ok', text: message });
  };

  switch (open.kind) {
    case 'details':
      return <DetailsDialog card={open.card} onClose={onClose} />;
    case 'form':
      return <MeetingFormDialog card={open.card} draft={open.draft} board={board} onClose={onClose} onSaved={(m) => (done(m), onClose())} />;
    case 'delete':
      return (
        <AsyncConfirmDialog
          title="Confirmar Eliminação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={onClose}
          onConfirm={async () => {
            const o = await meetingsApi.remove(open.card.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar a reunião');
            done('Reunião eliminada.');
          }}
        >
          <div className="mtg-confirm">
            <p>
              Tem a certeza que quer eliminar a reunião <strong>{open.card.title}</strong>?
            </p>
            <p className="note">Não é possível desfazer.</p>
          </div>
        </AsyncConfirmDialog>
      );
    case 'respond':
      return <ParticipationDialog card={open.card} willAttend={open.willAttend} onClose={onClose} onSaved={() => (done(), onClose())} />;
    case 'removeOwn':
      return (
        <AsyncConfirmDialog
          title="Confirmar Remoção de Participação"
          confirmLabel="Sim, Remover"
          cancelLabel="Não"
          danger
          onClose={onClose}
          onConfirm={async () => {
            const mine = open.card.myParticipation;
            if (!mine) return 'Já não tem uma resposta nesta reunião.';
            const o = await meetingsApi.removeParticipation(open.card.id, mine.id);
            if (o.kind !== 'ok') return problem(o, 'remover a participação');
            done('Participação removida.');
          }}
        >
          <div className="mtg-confirm">
            <p>Tem certeza que deseja remover sua participação nesta reunião?</p>
          </div>
        </AsyncConfirmDialog>
      );
    case 'participants':
      return <ParticipantsDialog card={open.card} onClose={onClose} onChanged={onChanged} />;
    case 'email':
      return <EmailDialog card={open.card} onClose={onClose} onSent={(r) => (done(`Email enviado a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.`, r.warning), onClose())} />;
    case 'push':
      return (
        <PushDialog card={open.card} onClose={onClose} onSent={(r) => (done(`Notificação push enviada a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.`, r.warning), onClose())} />
      );
    case 'cancel':
      return (
        <CancelDialog
          card={open.card}
          onClose={onClose}
          onCancelled={(r, notified) => (
            done(notified ? `Reunião cancelada. Email enviado a ${r.sent} ${r.sent === 1 ? 'membro' : 'membros'}.` : 'Reunião cancelada.', r.warning), onClose()
          )}
        />
      );
    case 'uncancel':
      return (
        <AsyncConfirmDialog
          title="Reverter cancelamento"
          confirmLabel="Reverter"
          confirmIcon="restore"
          onClose={onClose}
          onConfirm={async () => {
            const o = await meetingsApi.uncancel(open.card.id);
            if (o.kind !== 'ok') return problem(o, 'reverter o cancelamento');
            done('A reunião voltou a ficar agendada.');
          }}
        >
          <div className="mtg-confirm">
            <p>
              A reunião <strong>{open.card.title}</strong> volta a ficar agendada.
            </p>
          </div>
        </AsyncConfirmDialog>
      );
    case 'ata':
      return <AtaEditorDialog card={open.card} onClose={onClose} onChanged={onChanged} />;
    case 'viewAta':
      return <AtaViewDialog card={open.card} onClose={onClose} />;
    case 'propose':
      return (
        <ProposalDialog
          type={open.type}
          onClose={onClose}
          onSent={() => {
            onRequestsChanged();
            onFlash({ tone: 'ok', text: 'Proposta enviada.' });
            onClose();
          }}
        />
      );
  }
}
