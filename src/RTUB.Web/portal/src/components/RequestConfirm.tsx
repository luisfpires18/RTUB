// Confirm dialog for request actions on /requests.
import { useState } from 'react';
import { ConfirmDialog } from './ui/ConfirmDialog';
import { type RequestItem, api, problem } from '../data/requestsApi';

const confirmCopy = {
  approve: { title: 'Aprovar pedido', button: 'Aprovar', what: 'aprovar o pedido', text: (n: string) => `Aprovar o pedido de ${n}?` },
  reject: {
    title: 'Rejeitar pedido',
    button: 'Rejeitar',
    what: 'rejeitar o pedido',
    text: (n: string) => `Rejeitar o pedido de ${n}? Depois de respondido, o pedido não volta a ficar por responder.`,
  },
  delete: {
    title: 'Eliminar pedido',
    button: 'Eliminar',
    what: 'eliminar o pedido',
    text: (n: string) => `Eliminar o pedido de ${n}? Não é possível recuperá-lo.`,
  },
};

export function ConfirmAction({
  action,
  request,
  onClose,
  onDone,
}: {
  action: 'approve' | 'reject' | 'delete';
  request: RequestItem;
  onClose: () => void;
  onDone: (approved?: { request: RequestItem; createEventUrl: string }) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const copy = confirmCopy[action];

  const confirm = async () => {
    setBusy(true);
    if (action === 'approve') {
      const o = await api.approve(request.id);
      setBusy(false);
      return o.kind === 'ok' ? onDone(o.data) : setError(problem(o, copy.what));
    }
    const o = action === 'reject' ? await api.reject(request.id) : await api.remove(request.id);
    setBusy(false);
    if (o.kind === 'ok' || (action === 'delete' && o.kind === 'notfound')) onDone();
    else setError(problem(o, copy.what));
  };

  return (
    <ConfirmDialog title={copy.title} confirmLabel={copy.button} danger={action !== 'approve'} busy={busy} error={error} onConfirm={confirm} onClose={onClose}>
      <p>{copy.text(request.name)}</p>
    </ConfirmDialog>
  );
}
