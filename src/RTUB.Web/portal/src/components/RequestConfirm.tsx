// Confirm dialog for request actions on /requests.
import { useState } from 'react';
import { Dialog } from './Dialog';
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
    <Dialog
      title={copy.title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className={action === 'approve' ? 'btn btn--primary' : 'btn btn--danger'} onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {copy.button}
          </button>
        </>
      }
    >
      <p>{copy.text(request.name)}</p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
