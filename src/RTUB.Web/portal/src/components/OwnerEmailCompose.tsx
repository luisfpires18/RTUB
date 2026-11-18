// Owner emails compose form.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ownerApi, ownerProblem } from '../data/ownerApi';
import { Flash } from './OwnerUi';
import { TITLE_MAX, CONTENT_MAX, type Message } from '../data/ownerEmailsView';
import { ConfirmDialog } from './ui/ConfirmDialog';
import { FormField } from './ui/FormField';

export function Compose({ count }: { count: number }) {
  const id = useId();
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [open, setOpen] = useState<'preview' | 'confirm'>();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>();
  const ready = title.trim() !== '' && content.trim() !== '';

  const clear = () => {
    setTitle('');
    setContent('');
    setErrors({});
  };

  const ask = (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    if (!title.trim()) missing.title = 'O título é obrigatório.';
    if (!content.trim()) missing.content = 'O conteúdo é obrigatório.';
    setErrors(missing);
    if (Object.keys(missing).length === 0) setOpen('confirm');
  };

  const send = async () => {
    setBusy(true);
    const o = await ownerApi.sendEmail(title, content);
    setBusy(false);
    setOpen(undefined);
    if (o.kind === 'ok') {
      setMessage(
        o.data.success
          ? { text: `Foram enviados ${o.data.sent} de ${o.data.total} emails com sucesso.` }
          : { text: o.data.message ?? 'Erro ao enviar emails.', error: true },
      );
      if (o.data.success) clear();
    } else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.recipients) setMessage({ text: o.errors.recipients, error: true });
    } else setMessage({ text: ownerProblem(o, 'enviar os emails'), error: true });
  };

  return (
    <section className="owner-card" aria-labelledby={`${id}-compose`}>
      <h2 id={`${id}-compose`} className="owner-card__title">
        <Icon name="pencil" />
        Compor mensagem
      </h2>
      <Flash message={message} onDismiss={() => setMessage(undefined)} />
      <form className="form" onSubmit={ask} noValidate>
        <FormField id={`${id}-title`} label="Título" error={errors.title} hint={`É o assunto do email: [RTUB] ${title.trim() || '{título}'}`}>
          <input
            id={`${id}-title`}
            value={title}
            maxLength={TITLE_MAX}
            placeholder="Ex.: Nova atuação agendada"
            onChange={(e) => setTitle(e.target.value)}
            aria-invalid={errors.title ? true : undefined}
            aria-describedby={`${id}-title-hint`}
          />
        </FormField>
        <FormField
          id={`${id}-content`}
          label="Conteúdo"
          error={errors.content}
          hint={`Vai para ${count} ${count === 1 ? 'utilizador subscrito' : 'utilizadores subscritos'}. ${content.length}/${CONTENT_MAX}`}
        >
          <textarea
            id={`${id}-content`}
            rows={8}
            value={content}
            maxLength={CONTENT_MAX}
            placeholder="Escreve aqui a mensagem do anúncio…"
            onChange={(e) => setContent(e.target.value)}
            aria-invalid={errors.content ? true : undefined}
            aria-describedby={`${id}-content-hint`}
          />
        </FormField>
        <p className="note owner-note">
          <Icon name="bell" />O email vai para todos os utilizadores com as notificações por email ativas.
        </p>
        <div className="owner-actions">
          <button type="button" className="btn btn--ghost" onClick={() => setOpen('preview')} disabled={busy || !ready}>
            <Icon name="eye" />
            Pré-visualizar
          </button>
          <button type="submit" className="btn btn--primary" disabled={busy || !ready}>
            <Icon name="send" />
            Enviar
          </button>
          <button type="button" className="btn btn--ghost" onClick={clear} disabled={busy}>
            <Icon name="xCircle" />
            Limpar
          </button>
        </div>
      </form>

      {open === 'preview' && (
        <Dialog title="Pré-visualização do email" size="lg" onClose={() => setOpen(undefined)}>
          <article className="owner-preview">
            <h3 className="owner-preview__title">{title}</h3>
            <p>Olá [Nome do utilizador]</p>
            <p className="owner-preview__body">{content}</p>
            <p>Saudações RTUBianas.</p>
          </article>
        </Dialog>
      )}
      {open === 'confirm' && (
        <ConfirmDialog title="Enviar o email?" confirmLabel="Enviar" busy={busy} onConfirm={send} onClose={() => setOpen(undefined)}>
          <p>
            «[RTUB] {title.trim()}» vai para {count} {count === 1 ? 'utilizador' : 'utilizadores'}. Um email enviado não se recupera.
          </p>
        </ConfirmDialog>
      )}
    </section>
  );
}
