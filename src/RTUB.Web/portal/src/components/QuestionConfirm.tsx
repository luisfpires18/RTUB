// Confirm dialog for question actions on /questions.
import { useState } from 'react';
import { ConfirmDialog } from './ui/ConfirmDialog';
import { type Question, api, problem } from '../data/questionsApi';

const confirmCopy = {
  close: {
    title: 'Fechar pergunta',
    button: 'Fechar',
    what: 'fechar a pergunta',
    text: 'Fechar esta pergunta? Depois de fechada, deixa de aceitar respostas.',
  },
  remind: {
    title: 'Enviar lembrete',
    button: 'Enviar',
    what: 'enviar o lembrete',
    text: 'Enviar uma notificação a lembrar quem tem de responder?',
  },
  delete: {
    title: 'Eliminar pergunta',
    button: 'Eliminar',
    what: 'eliminar a pergunta',
    text: 'Eliminar esta pergunta e as respostas? Deixa de aparecer para todos.',
  },
};

export function ConfirmAction({
  action,
  question,
  onClose,
  onDone,
}: {
  action: 'close' | 'remind' | 'delete';
  question: Question;
  onClose: () => void;
  onDone: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const copy = confirmCopy[action];

  const confirm = async () => {
    setBusy(true);
    const o = action === 'close' ? await api.close(question.id) : action === 'remind' ? await api.remind(question.id) : await api.remove(question.id);
    setBusy(false);
    if (o.kind === 'ok' || (action === 'delete' && o.kind === 'notfound')) onDone();
    else setError(problem(o, copy.what));
  };

  return (
    <ConfirmDialog title={copy.title} confirmLabel={copy.button} danger={action === 'delete'} busy={busy} error={error} onConfirm={confirm} onClose={onClose}>
      <p>
        <strong>{question.title}</strong>
      </p>
      <p>{copy.text}</p>
    </ConfirmDialog>
  );
}
