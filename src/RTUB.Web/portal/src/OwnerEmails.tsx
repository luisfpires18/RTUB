import { useId, useState, type FormEvent } from 'react';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { ownerApi, ownerProblem, type EmailRecipients, type OwnerPerson } from './ownerApi';
import { Flash, matches, OwnerPage, OwnerPager, SearchBox, useOwnerLoad } from './OwnerUi';

const TITLE_MAX = 200;
const CONTENT_MAX = 5000;

type Message = { text: string; error?: boolean };

/**
 * /emails (task 035; was the Blazor page, an Admin page then): the Owner writes an announcement and sends it by email to
 * every account with a confirmed address and "Notificações por email" on, and chooses who those are. The server decides
 * the recipients when it sends and refuses anyone but the Owner.
 */
export default function OwnerEmails() {
  const [recipients, , setRecipients] = useOwnerLoad(() => ownerApi.emailRecipients(), []);

  return (
    <OwnerPage
      path={portal.emails}
      title="Emails"
      lead="Enviar um anúncio por email a todos os utilizadores subscritos."
      icon="envelope"
      state={recipients}
      loadingLabel="A carregar os destinatários…"
    >
      {(data) => (
        <>
          <Compose count={data.subscribed.length} />
          <Recipients data={data} onChanged={setRecipients} />
        </>
      )}
    </OwnerPage>
  );
}

function Compose({ count }: { count: number }) {
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
        <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input
            id={`${id}-title`}
            value={title}
            maxLength={TITLE_MAX}
            placeholder="Ex.: Nova atuação agendada"
            onChange={(e) => setTitle(e.target.value)}
            aria-invalid={errors.title ? true : undefined}
            aria-describedby={`${id}-title-hint`}
          />
          <p id={`${id}-title-hint`} className="form__hint">
            É o assunto do email: [RTUB] {title.trim() || '{título}'}
          </p>
          {errors.title && <p className="form__error">{errors.title}</p>}
        </div>
        <div className={errors.content ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-content`}>Conteúdo</label>
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
          <p id={`${id}-content-hint`} className="form__hint">
            Vai para {count} {count === 1 ? 'utilizador subscrito' : 'utilizadores subscritos'}. {content.length}/{CONTENT_MAX}
          </p>
          {errors.content && <p className="form__error">{errors.content}</p>}
        </div>
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

function Recipients({ data, onChanged }: { data: EmailRecipients; onChanged: (next: EmailRecipients) => void }) {
  const id = useId();
  const [message, setMessage] = useState<Message>();
  const [busy, setBusy] = useState(false);

  const change = async (people: OwnerPerson[], subscribed: boolean) => {
    if (people.length === 0) return;
    setBusy(true);
    const o = await ownerApi.setSubscription(
      people.map((p) => p.id),
      subscribed,
    );
    setBusy(false);
    if (o.kind !== 'ok') return setMessage({ text: ownerProblem(o, 'alterar as subscrições'), error: true });
    onChanged(o.data.recipients);
    const who = people.length === 1 ? people[0].nickname : `${o.data.changed} utilizador(es)`;
    const text = subscribed ? `${who} adicionado(s) às notificações.` : `${who} removido(s) das notificações.`;
    setMessage(
      o.data.errors.length > 0 ? { text: `${o.data.changed > 0 ? `${text} ` : ''}Erros: ${o.data.errors.join('; ')}`, error: true } : { text },
    );
  };

  return (
    <section className="owner-card" aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="owner-card__title">
        <Icon name="people" />
        Lista de destinatários
      </h2>
      <Flash message={message} onDismiss={() => setMessage(undefined)} />
      <div className="owner-columns">
        <PeopleList
          title={`Destinatários (${data.subscribed.length})`}
          people={data.subscribed}
          busy={busy}
          actionLabel="Remover"
          actionIcon="close"
          onAction={(p) => change(p, false)}
        />
        <PeopleList
          title={`Quem não vai receber (${data.notSubscribed.length})`}
          people={data.notSubscribed}
          busy={busy}
          actionLabel="Adicionar"
          actionIcon="plus"
          onAction={(p) => change(p, true)}
        />
      </div>
    </section>
  );
}

/** One side of the recipients: search, select (all of the search, or one by one), and act on one or the selection. */
function PeopleList({
  title,
  people,
  busy,
  actionLabel,
  actionIcon,
  onAction,
}: {
  title: string;
  people: OwnerPerson[];
  busy: boolean;
  actionLabel: string;
  actionIcon: 'close' | 'plus';
  onAction: (people: OwnerPerson[]) => void;
}) {
  const id = useId();
  const [q, setQ] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const filtered = people.filter((p) => matches(q, p.nickname, p.fullName, p.email));
  const shown = filtered.slice((page - 1) * pageSize, page * pageSize);
  const chosen = people.filter((p) => selected.has(p.id));
  const allChosen = filtered.length > 0 && filtered.every((p) => selected.has(p.id));

  const toggle = (ids: string[], on: boolean) =>
    setSelected((s) => {
      const next = new Set(s);
      ids.forEach((i) => (on ? next.add(i) : next.delete(i)));
      return next;
    });

  return (
    <div className="owner-people">
      <h3 className="owner-people__title">{title}</h3>
      <SearchBox
        label="Procurar utilizador"
        placeholder="Procurar por nome ou email"
        value={q}
        onChange={(v) => {
          setQ(v);
          setPage(1);
        }}
      />
      {people.length === 0 ? (
        <p className="note">Ninguém.</p>
      ) : (
        <>
          <div className="owner-people__bar">
            <label className="form__check" htmlFor={`${id}-all`}>
              <input id={`${id}-all`} type="checkbox" checked={allChosen} onChange={(e) => toggle(filtered.map((p) => p.id), e.target.checked)} />
              Selecionar todos
            </label>
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              disabled={busy || chosen.length === 0}
              onClick={() => {
                onAction(chosen);
                setSelected(new Set());
              }}
            >
              <Icon name={actionIcon} />
              {actionLabel} ({chosen.length})
            </button>
          </div>
          <ul className="owner-people__list">
            {shown.map((p) => (
              <li key={p.id} className="owner-person">
                <input
                  type="checkbox"
                  checked={selected.has(p.id)}
                  onChange={(e) => toggle([p.id], e.target.checked)}
                  aria-label={`Selecionar ${p.nickname}`}
                />
                <img className="owner-person__avatar" src={p.avatarUrl} alt="" width="32" height="32" loading="lazy" />
                <span className="owner-person__name">
                  <strong>{p.nickname}</strong>
                  <span className="note">{p.fullName}</span>
                </span>
                <button type="button" className="icon-btn icon-btn--sm" disabled={busy} onClick={() => onAction([p])} title={`${actionLabel} ${p.nickname}`}>
                  <Icon name={actionIcon} />
                  <span className="sr-only">
                    {actionLabel} {p.nickname}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {filtered.length === 0 && <p className="note">Ninguém corresponde à pesquisa.</p>}
          {filtered.length > 10 && (
            <OwnerPager
              total={filtered.length}
              itemLabel="utilizadores"
              page={page}
              pageSize={pageSize}
              sizes={[10, 25, 50, 100]}
              onPage={setPage}
              onPageSize={(s) => {
                setPageSize(s);
                setPage(1);
              }}
            />
          )}
        </>
      )}
    </div>
  );
}
