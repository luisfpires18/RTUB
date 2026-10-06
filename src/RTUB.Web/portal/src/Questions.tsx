import { useEffect, useId, useMemo, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { loginTo } from './musicApi';

// /api/questions (task 031, Endpoints/QuestionEndpoints.cs). Dates are UTC.
type Status = 'unanswered' | 'answered' | 'inDiscussion' | 'closed';
type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };
type Question = {
  id: number;
  title: string;
  content: string;
  author: Person;
  recipient: { displayName: string; avatarUrl: string | null; role: string };
  status: Status;
  createdAt: string;
  lastActivityAt: string;
  replyCount: number;
  isMine: boolean;
  canReply: boolean;
  canRemind: boolean;
};
type QuestionPage = { items: Question[]; total: number; page: number; pageSize: number };
type Reply = { id: number; content: string; author: Person; fromRecipient: boolean; createdAt: string };
type Detail = { question: Question; replies: Reply[] };
type Recipient = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null; position: string; role: string };

const api = {
  page: (closed: boolean, q: string, recipient: string, page: number) => {
    const p = new URLSearchParams({ closed: String(closed), page: String(page), pageSize: String(PAGE_SIZE) });
    if (q) p.set('q', q);
    if (recipient) p.set('recipient', recipient);
    return call<QuestionPage>('GET', `/api/questions?${p}`);
  },
  recipients: () => call<Recipient[]>('GET', '/api/questions/recipients'),
  detail: (id: number) => call<Detail>('GET', `/api/questions/${id}`),
  ask: (input: { title: string; content: string; recipientId: string; position: string }) => call<Detail>('POST', '/api/questions', input),
  reply: (id: number, content: string) => call<Detail>('POST', `/api/questions/${id}/replies`, { content }),
  close: (id: number) => call<Detail>('POST', `/api/questions/${id}/close`),
  remind: (id: number) => call<void>('POST', `/api/questions/${id}/remind`),
  remove: (id: number) => call<void>('DELETE', `/api/questions/${id}`),
};

const PAGE_SIZE = 10;
const MAX_TITLE = 100;
const MIN_CONTENT = 10;
const MAX_CONTENT = 5000;

const statusLabel: Record<Status, string> = {
  unanswered: 'Aguarda resposta',
  answered: 'Respondida',
  inDiscussion: 'Em discussão',
  closed: 'Fechada',
};
const statusPill: Record<Status, string> = {
  unanswered: 'pill pill--wait',
  answered: 'pill pill--yes',
  inDiscussion: 'pill pill--wait',
  closed: 'pill',
};

const stamp = (iso: string) =>
  new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`).toLocaleString('pt-PT', { dateStyle: 'medium', timeStyle: 'short' });

const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Esta pergunta já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/**
 * /questions - Perguntas aos Órgãos Sociais (task 031; was the Blazor page). Any signed-in member asks a question to
 * whoever holds a position in the Órgãos Sociais; the two then take turns replying. The author closes, deletes, or
 * reminds the recipient while the answer is pending.
 */
export default function Questions() {
  const [session, setSession] = useState<'signin' | 'failed' | 'ok'>();
  const [recipients, setRecipients] = useState<Recipient[]>([]);
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [recipient, setRecipient] = useState('');
  const [version, setVersion] = useState(0);
  const [open, setOpen] = useState<number>();
  const [asking, setAsking] = useState(false);
  const [action, setAction] = useState<{ kind: 'close' | 'remind' | 'delete'; question: Question }>();
  const ids = { q: useId(), who: useId() };

  useEffect(() => {
    document.title = 'Perguntas · RTUB';
    api.recipients().then((o) => {
      if (o.kind === 'ok') {
        setRecipients(o.data);
        setSession('ok');
      } else setSession(o.kind === 'signin' ? 'signin' : 'failed');
    });
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  // One filter entry per member (the same member can hold more than one position).
  const filterOptions = useMemo(() => {
    const seen = new Map<string, string>();
    for (const r of recipients) seen.set(r.id, seen.has(r.id) ? `${seen.get(r.id)}, ${r.role}` : `${r.displayName} - ${r.role}`);
    return [...seen.entries()].map(([id, label]) => ({ id, label }));
  }, [recipients]);

  const refresh = () => setVersion((v) => v + 1);

  return (
    <section className="page wrap questions-page" aria-labelledby="questions-title">
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="questions-title" className="page__title">
          Perguntas aos Órgãos Sociais
        </h1>
        <p className="page__lead">Pergunta diretamente a quem tem o cargo; a conversa fica aqui até a fechares.</p>
      </header>

      {session === 'signin' ? (
        <div className="notice" role="status">
          <p>As perguntas são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo('/questions')}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : session === 'failed' ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir as perguntas agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => location.reload()}>
            Tentar novamente
          </button>
        </div>
      ) : !session ? (
        <Loading label="A carregar as perguntas…" />
      ) : (
        <>
          <div className="events-tools qs-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar perguntas</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar perguntas…" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.who}>
              <span className="sr-only">Para quem</span>
              <select id={ids.who} value={recipient} onChange={(e) => setRecipient(e.target.value)}>
                <option value="">Todos os membros</option>
                {filterOptions.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.label}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setAsking(true)}>
              <Icon name="plus" />
              Nova pergunta
            </button>
          </div>

          <QuestionList closed={false} q={q} recipient={recipient} version={version} onOpen={setOpen} onAction={setAction} />
          <QuestionList closed q={q} recipient={recipient} version={version} onOpen={setOpen} onAction={setAction} />

          {open !== undefined && (
            <QuestionDialog
              id={open}
              onClose={() => setOpen(undefined)}
              onChanged={refresh}
              onAction={(a) => {
                setOpen(undefined);
                setAction(a);
              }}
            />
          )}
          {asking && (
            <AskDialog
              recipients={recipients}
              onClose={() => setAsking(false)}
              onAsked={(id) => {
                setAsking(false);
                refresh();
                setOpen(id);
              }}
            />
          )}
          {action && (
            <ConfirmAction
              action={action.kind}
              question={action.question}
              onClose={() => setAction(undefined)}
              onDone={() => {
                setAction(undefined);
                refresh();
              }}
            />
          )}
        </>
      )}
    </section>
  );
}

function QuestionList({
  closed,
  q,
  recipient,
  version,
  onOpen,
  onAction,
}: {
  closed: boolean;
  q: string;
  recipient: string;
  version: number;
  onOpen: (id: number) => void;
  onAction: (a: { kind: 'close' | 'remind' | 'delete'; question: Question }) => void;
}) {
  const id = useId();
  const [page, setPage] = useState(1);
  const [data, setData] = useState<QuestionPage | null>();

  useEffect(() => setPage(1), [q, recipient]);
  useEffect(() => {
    api.page(closed, q, recipient, page).then((o) => setData(o.kind === 'ok' ? o.data : null));
  }, [closed, q, recipient, page, version]);

  // The closed list only appears once there is something in it, as before.
  if (closed && data && data.total === 0 && page === 1) return null;

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <section className="rq-group" aria-labelledby={id}>
      <h2 id={id} className="rq-group__title">
        {closed ? 'Perguntas fechadas' : 'Perguntas abertas'} {data && <span className="note">({data.total})</span>}
      </h2>
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar esta lista.
        </p>
      ) : data.items.length === 0 ? (
        <p className="note">{q || recipient ? 'Nenhuma pergunta corresponde à pesquisa.' : 'Ainda não há perguntas abertas.'}</p>
      ) : (
        <>
          <ul className="rq-grid">
            {data.items.map((x) => (
              <li key={x.id} className="rq-card qs-card">
                <div className="qs-card__people">
                  <span className="qs-person">
                    <MemberFace avatarUrl={x.author.avatarUrl} size={32} />
                    <strong>{x.author.displayName}</strong>
                  </span>
                  <Icon name="arrow" className="qs-card__to" />
                  <span className="qs-person">
                    <MemberFace avatarUrl={x.recipient.avatarUrl} size={32} />
                    <span>
                      <strong>{x.recipient.displayName}</strong>
                      <small className="note">{x.recipient.role}</small>
                    </span>
                  </span>
                </div>
                <div className="rq-card__head">
                  <strong className="rq-card__title">{x.title}</strong>
                  <span className={statusPill[x.status]}>{statusLabel[x.status]}</span>
                </div>
                <p className="qs-card__text">{x.content}</p>
                <p className="note">
                  {stamp(x.createdAt)} · {x.replyCount === 1 ? '1 resposta' : `${x.replyCount} respostas`}
                </p>
                <div className="rq-card__actions">
                  <button type="button" className={x.canReply ? 'btn btn--primary btn--sm' : 'btn btn--ghost btn--sm'} onClick={() => onOpen(x.id)}>
                    <Icon name={x.canReply ? 'chat' : 'search'} />
                    {x.canReply ? 'Responder' : 'Ver'}
                  </button>
                  {x.canRemind && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => onAction({ kind: 'remind', question: x })}>
                      <Icon name="bell" />
                      Lembrar
                    </button>
                  )}
                  {x.isMine && x.status !== 'closed' && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => onAction({ kind: 'close', question: x })}>
                      <Icon name="check" />
                      Fechar
                    </button>
                  )}
                  {x.isMine && (
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onAction({ kind: 'delete', question: x })}>
                      <Icon name="trash" />
                      <span className="sr-only">Eliminar a pergunta «{x.title}»</span>
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <div className="pager">
              <button type="button" className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Anterior
              </button>
              <span className="pager__status">
                {page} / {pages}
              </span>
              <button type="button" className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>
                Seguinte
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function QuestionDialog({
  id,
  onClose,
  onChanged,
  onAction,
}: {
  id: number;
  onClose: () => void;
  onChanged: () => void;
  onAction: (a: { kind: 'close' | 'remind' | 'delete'; question: Question }) => void;
}) {
  const replyId = useId();
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  const [text, setText] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.detail(id).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return setError('Escreva uma resposta.');
    setBusy(true);
    const o = await api.reply(id, text);
    setBusy(false);
    if (o.kind === 'ok') {
      setDetail(o.data);
      setText('');
      setError(undefined);
      onChanged();
    } else setError(problem(o, 'enviar a resposta'));
  };

  const d = detail && typeof detail === 'object' ? detail : null;
  const x = d?.question;

  return (
    <Dialog
      title="Pergunta"
      size="lg"
      onClose={onClose}
      footer={
        <>
          {x?.isMine && x.status !== 'closed' && (
            <button type="button" className="btn btn--ghost" onClick={() => onAction({ kind: 'close', question: x })}>
              Fechar a pergunta
            </button>
          )}
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Voltar
          </button>
        </>
      }
    >
      {detail === undefined ? (
        <Loading label="A carregar a pergunta…" />
      ) : detail === 'missing' ? (
        <p className="note">Esta pergunta já não existe.</p>
      ) : !d || !x ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar a pergunta.
        </p>
      ) : (
        <div className="qs-detail">
          <div className="qs-card__people">
            <span className="qs-person">
              <MemberFace avatarUrl={x.author.avatarUrl} size={36} />
              <span>
                <strong>{x.author.displayName}</strong>
                <small className="note">{stamp(x.createdAt)}</small>
              </span>
            </span>
            <span className={statusPill[x.status]}>{statusLabel[x.status]}</span>
          </div>
          <p className="note">
            Para <strong>{x.recipient.displayName}</strong> ({x.recipient.role})
          </p>
          <h3 className="qs-detail__title">{x.title}</h3>
          <p className="rq-message">{x.content}</p>

          <h3 className="member-section__title">
            Respostas <span className="note">({d.replies.length})</span>
          </h3>
          {d.replies.length === 0 ? (
            <p className="note">Ainda sem respostas.</p>
          ) : (
            <ul className="member-rows qs-replies">
              {d.replies.map((r) => (
                <li key={r.id} className={r.fromRecipient ? 'qs-reply qs-reply--recipient' : 'qs-reply'}>
                  <span className="qs-person">
                    <MemberFace avatarUrl={r.author.avatarUrl} size={32} />
                    <span>
                      <strong>{r.author.displayName}</strong>
                      {r.fromRecipient && <span className="member-badge member-badge--position">{x.recipient.role}</span>}
                      <small className="note">{stamp(r.createdAt)}</small>
                    </span>
                  </span>
                  <p className="rq-message">{r.content}</p>
                </li>
              ))}
            </ul>
          )}

          {x.canReply ? (
            <form className="form" onSubmit={submit} noValidate>
              <div className={error ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={replyId}>A tua resposta</label>
                <textarea
                  id={replyId}
                  rows={4}
                  maxLength={MAX_CONTENT}
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  aria-invalid={error ? true : undefined}
                />
                {error && (
                  <p className="form__error" role="alert">
                    {error}
                  </p>
                )}
              </div>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !text.trim()}>
                {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                <Icon name="send" />
                Responder
              </button>
            </form>
          ) : x.status === 'closed' ? (
            <p className="note">Esta pergunta foi fechada pelo autor; já não aceita respostas.</p>
          ) : x.isMine ? (
            <p className="note">Aguarda a resposta de {x.recipient.displayName}.</p>
          ) : (
            <p className="note">Só {x.recipient.displayName} e o autor da pergunta escrevem aqui, à vez.</p>
          )}
        </div>
      )}
    </Dialog>
  );
}

function AskDialog({ recipients, onClose, onAsked }: { recipients: Recipient[]; onClose: () => void; onAsked: (id: number) => void }) {
  const id = useId();
  const [search, setSearch] = useState('');
  const [chosen, setChosen] = useState<Recipient>();
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);

  const found = useMemo(() => {
    const term = fold(search.trim());
    if (!term) return recipients;
    return recipients.filter((r) => fold(`${r.displayName} ${r.fullName ?? ''} ${r.role}`).includes(term));
  }, [recipients, search]);

  const valid = chosen && title.trim().length > 0 && title.trim().length <= MAX_TITLE && content.trim().length >= MIN_CONTENT;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!chosen) return setErrors({ recipientId: 'Escolha um membro dos Órgãos Sociais.' });
    setBusy(true);
    const o = await api.ask({ title, content, recipientId: chosen.id, position: chosen.position });
    setBusy(false);
    if (o.kind === 'ok') onAsked(o.data.question.id);
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'enviar a pergunta'));
  };

  return (
    <Dialog
      title="Nova pergunta"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !valid}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            <Icon name="send" />
            Enviar pergunta
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        <fieldset className={errors.recipientId ? 'form__field form__field--error qs-recipient' : 'form__field qs-recipient'}>
          <legend>Para quem</legend>
          {recipients.length === 0 ? (
            <p className="note">Não há membros nos Órgãos Sociais neste momento.</p>
          ) : chosen ? (
            <div className="qs-chosen">
              <span className="qs-person">
                <MemberFace avatarUrl={chosen.avatarUrl} size={36} />
                <span>
                  <strong>{chosen.displayName}</strong>
                  <small className="note">{chosen.role}</small>
                </span>
              </span>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setChosen(undefined)}>
                Mudar
              </button>
            </div>
          ) : (
            <>
              <label className="control" htmlFor={`${id}-who`}>
                <span className="sr-only">Procurar por nome, alcunha ou cargo</span>
                <Icon name="search" />
                <input
                  id={`${id}-who`}
                  type="search"
                  placeholder="Nome, alcunha ou cargo…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>
              <ul className="qs-pick" aria-label="Membros dos Órgãos Sociais">
                {found.map((r) => (
                  <li key={`${r.id}-${r.position}`}>
                    <button type="button" className="qs-pick__option" onClick={() => setChosen(r)}>
                      <MemberFace avatarUrl={r.avatarUrl} size={32} />
                      <span>
                        <strong>{r.displayName}</strong>
                        <small className="note">{r.role}</small>
                      </span>
                    </button>
                  </li>
                ))}
                {found.length === 0 && <li className="note">Ninguém corresponde à pesquisa.</li>}
              </ul>
            </>
          )}
          {errors.recipientId && (
            <p className="form__error" role="alert">
              {errors.recipientId}
            </p>
          )}
        </fieldset>

        <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input
            id={`${id}-title`}
            type="text"
            maxLength={MAX_TITLE}
            placeholder="Resumo da pergunta"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            aria-invalid={errors.title ? true : undefined}
          />
          <p className="form__hint">
            {title.length} / {MAX_TITLE}
          </p>
          {errors.title && <p className="form__error">{errors.title}</p>}
        </div>
        <div className={errors.content ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-content`}>A pergunta</label>
          <textarea
            id={`${id}-content`}
            rows={6}
            maxLength={MAX_CONTENT}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            aria-invalid={errors.content ? true : undefined}
          />
          <p className="form__hint">Pelo menos {MIN_CONTENT} caracteres.</p>
          {errors.content && <p className="form__error">{errors.content}</p>}
        </div>
      </form>
    </Dialog>
  );
}

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

function ConfirmAction({
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
    <Dialog
      title={copy.title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className={action === 'delete' ? 'btn btn--danger' : 'btn btn--primary'} onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {copy.button}
          </button>
        </>
      }
    >
      <p>
        <strong>{question.title}</strong>
      </p>
      <p>{copy.text}</p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
