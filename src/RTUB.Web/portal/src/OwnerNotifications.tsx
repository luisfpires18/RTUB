import { useId, useState, type FormEvent } from 'react';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { ownerApi, ownerProblem, type OwnerPerson } from './ownerApi';
import { Flash, matches, OwnerPage, OwnerPager, SearchBox, useOwnerLoad } from './OwnerUi';

const TITLE_MAX = 100;
const BODY_MAX = 500;

/**
 * /notifications (task 035; was the Blazor page, an Admin page then): the Owner writes a push notification and sends it
 * to the chosen accounts with a push subscription (all of them to start with), through the existing push sender. This
 * is not the members' own push settings. The server refuses anyone but the Owner and audits every send.
 */
export default function OwnerNotifications() {
  const [recipients] = useOwnerLoad(() => ownerApi.pushRecipients(), []);

  return (
    <OwnerPage
      path={portal.notifications}
      title="Notificações"
      lead="Enviar uma notificação push aos utilizadores escolhidos."
      icon="bell"
      state={recipients}
      loadingLabel="A carregar os destinatários…"
    >
      {(data) => <Composer people={data.recipients} />}
    </OwnerPage>
  );
}

function Composer({ people }: { people: OwnerPerson[] }) {
  const id = useId();
  const all = () => new Set(people.map((p) => p.id));
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [selected, setSelected] = useState<Set<string>>(all);
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [open, setOpen] = useState<'preview' | 'confirm'>();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; error?: boolean }>();
  const ready = title.trim() !== '' && body.trim() !== '';
  const filtered = people.filter((p) => matches(q, p.nickname, p.fullName, p.email));
  const shown = filtered.slice((page - 1) * pageSize, page * pageSize);
  const allShown = filtered.length > 0 && filtered.every((p) => selected.has(p.id));

  const toggle = (ids: string[], on: boolean) =>
    setSelected((s) => {
      const next = new Set(s);
      ids.forEach((i) => (on ? next.add(i) : next.delete(i)));
      return next;
    });

  const clear = () => {
    setTitle('');
    setBody('');
    setErrors({});
    setSelected(all());
  };

  const ask = (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    if (!title.trim()) missing.title = 'O título é obrigatório.';
    if (!body.trim()) missing.body = 'A mensagem é obrigatória.';
    setErrors(missing);
    if (Object.keys(missing).length > 0) return;
    if (selected.size === 0) return setMessage({ text: 'Seleciona pelo menos um utilizador para receber a notificação push.', error: true });
    setOpen('confirm');
  };

  const send = async () => {
    setBusy(true);
    const o = await ownerApi.sendPush(title, body, [...selected]);
    setBusy(false);
    setOpen(undefined);
    if (o.kind === 'ok') {
      const { recipients, sent, failed } = o.data;
      setMessage(
        failed > 0
          ? { text: `Notificação enviada para ${recipients} utilizadores (${sent} push enviados, ${failed} falhados).`, error: sent === 0 }
          : { text: `Notificação enviada com sucesso para ${recipients} utilizadores (${sent} push enviados).` },
      );
      clear();
    } else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.userIds) setMessage({ text: o.errors.userIds, error: true });
    } else setMessage({ text: ownerProblem(o, 'enviar a notificação'), error: true });
  };

  return (
    <>
      <section className="owner-card" aria-labelledby={`${id}-compose`}>
        <h2 id={`${id}-compose`} className="owner-card__title">
          <Icon name="pencil" />
          Compor notificação
        </h2>
        <Flash message={message} onDismiss={() => setMessage(undefined)} />
        <form id={`${id}-form`} className="form" onSubmit={ask} noValidate>
          <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${id}-title`}>Título</label>
            <input
              id={`${id}-title`}
              value={title}
              maxLength={TITLE_MAX}
              placeholder="Ex.: Nova atuação agendada"
              onChange={(e) => setTitle(e.target.value)}
              aria-invalid={errors.title ? true : undefined}
            />
            <p className="form__hint">
              {title.length}/{TITLE_MAX}
            </p>
            {errors.title && <p className="form__error">{errors.title}</p>}
          </div>
          <div className={errors.body ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${id}-body`}>Mensagem</label>
            <textarea
              id={`${id}-body`}
              rows={5}
              value={body}
              maxLength={BODY_MAX}
              placeholder="Escreve aqui a mensagem da notificação…"
              onChange={(e) => setBody(e.target.value)}
              aria-invalid={errors.body ? true : undefined}
            />
            <p className="form__hint">
              Vai para os utilizadores selecionados abaixo. {body.length}/{BODY_MAX}
            </p>
            {errors.body && <p className="form__error">{errors.body}</p>}
          </div>
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
      </section>

      <section className="owner-card" aria-labelledby={`${id}-people`}>
        <h2 id={`${id}-people`} className="owner-card__title">
          <Icon name="people" />
          Selecionar destinatários
        </h2>
        {people.length === 0 ? (
          <p className="note">Ninguém tem as notificações push ativas neste momento.</p>
        ) : (
          <div className="owner-people">
            <SearchBox
              label="Procurar utilizador"
              placeholder="Procurar por nome ou email"
              value={q}
              onChange={(v) => {
                setQ(v);
                setPage(1);
              }}
            />
            <div className="owner-people__bar">
              <span className="note">
                {selected.size}/{people.length} selecionados
              </span>
              <label className="form__check" htmlFor={`${id}-all`}>
                <input id={`${id}-all`} type="checkbox" checked={allShown} onChange={(e) => toggle(filtered.map((p) => p.id), e.target.checked)} />
                Selecionar todos
              </label>
            </div>
            <ul className="owner-people__list">
              {shown.map((p) => (
                <li key={p.id}>
                  <label className="owner-person">
                    <input type="checkbox" checked={selected.has(p.id)} onChange={(e) => toggle([p.id], e.target.checked)} />
                    <img className="owner-person__avatar" src={p.avatarUrl} alt="" width="32" height="32" loading="lazy" />
                    <span className="owner-person__name">
                      <strong>{p.nickname}</strong>
                      <span className="note">{p.fullName}</span>
                    </span>
                  </label>
                </li>
              ))}
            </ul>
            {filtered.length === 0 && <p className="note">Ninguém corresponde à pesquisa.</p>}
            {filtered.length > pageSize && (
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
          </div>
        )}
      </section>

      {open === 'preview' && (
        <Dialog title="Pré-visualização da notificação" onClose={() => setOpen(undefined)}>
          <article className="owner-push-preview">
            <img src="/icons/rtub-logo-192.png" alt="" width="48" height="48" />
            <div>
              <strong>{title}</strong>
              <p>{body}</p>
              <span className="note">Notificação push RTUB</span>
            </div>
          </article>
        </Dialog>
      )}
      {open === 'confirm' && (
        <ConfirmDialog title="Enviar a notificação?" confirmLabel="Enviar" busy={busy} onConfirm={send} onClose={() => setOpen(undefined)}>
          <p>
            «{title.trim()}» vai para {selected.size} {selected.size === 1 ? 'utilizador' : 'utilizadores'}. Uma notificação enviada não se recupera.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}
