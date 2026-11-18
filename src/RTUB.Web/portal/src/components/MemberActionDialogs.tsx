// Member admin dialogs: change nickname and confirm an action.
import { DialogFooter } from './ui/DialogFooter';
import { FieldError } from './ui/FieldError';
import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import type { Outcome } from '../data/eventsApi';
import { memberAdminApi } from '../data/membersApi';
import { problem, username } from '../data/memberFormState';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

/** "Definir alcunha" for a Leitão: the new nickname and the username it gives; the member gets an email. */
export function NicknameDialog({ member, onClose, onDone }: { member: { id: string; displayName: string }; onClose: () => void; onDone: () => void }) {
  const id = useId();
  const [nickname, setNickname] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!nickname.trim()) return setError('O nome de tuna é obrigatório');
    setBusy(true);
    const o = await memberAdminApi.nickname(member.id, nickname);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(problem(o, 'guardar a alcunha'));
  };

  return (
    <Dialog
      title="Definir alcunha"
      onClose={onClose}
      footer={<DialogFooter form={`${id}-form`} busy={busy} onClose={onClose} />}
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <p>
          Leitão: <strong>{member.displayName}</strong>
        </p>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={id}>Nome de tuna</label>
          <input id={id} type="text" maxLength={80} value={nickname} onChange={(e) => setNickname(e.target.value)} aria-invalid={error ? true : undefined} />
          <p className="form__hint">O username passa a ser gerado a partir da alcunha e o membro recebe um email com o novo.</p>
          <FieldError text={error} />
        </div>
        {username(nickname) && (
          <p className="note">
            Novo username: <strong>{username(nickname)}</strong>
          </p>
        )}
      </form>
    </Dialog>
  );
}

/** One confirmation for expel, reactivate, delete and the push reminder. */
export function ConfirmAction({
  title,
  confirmLabel,
  danger,
  run,
  onClose,
  onDone,
  children,
}: {
  title: string;
  confirmLabel: string;
  danger?: boolean;
  run: () => Promise<Outcome<unknown>>;
  onClose: () => void;
  onDone: () => void;
  children: ReactNode;
}) {
  const confirm = async () => {
    const o = await run();
    if (o.kind !== 'ok') return problem(o, title.toLowerCase());
    onDone();
  };

  return (
    <AsyncConfirmDialog title={title} confirmLabel={confirmLabel} danger={danger} onConfirm={confirm} onClose={onClose}>
      {children}
    </AsyncConfirmDialog>
  );
}
