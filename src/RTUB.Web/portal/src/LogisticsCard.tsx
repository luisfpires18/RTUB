import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberPicker, PersonChip, size } from './LogisticsDialogs';
import {
  day,
  logisticsApi,
  PRIORITY,
  problem,
  STATUS,
  uploadAttachment,
  type Board,
  type Card,
  type CardActivity,
  type CardAttachment,
  type CardComment,
  type CardDetail,
  type Priority,
  type Status,
} from './logisticsApi';
import { DEFAULT_AVATAR } from './membersApi';

const STATUSES: Status[] = ['Todo', 'InProgress', 'Done'];
const PRIORITIES: Priority[] = ['Low', 'Normal', 'High', 'Urgent'];
const MAX_FILE = 10 * 1024 * 1024;
const FILE_TYPES = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'text/plain',
  'image/png',
  'image/jpeg',
];
const HEX = /^#[0-9a-f]{6}$/i;

type Draft = { title: string; description: string; priority: Priority; startDate: string; dueDate: string; owner: { id: string; name: string; avatar: string | null } | null };
type Upload = { name: string; share: number; file: File; error?: string };

const draftOf = (d: CardDetail): Draft => ({
  title: d.card.title,
  description: d.card.description,
  priority: d.card.priority,
  startDate: d.card.startDate?.slice(0, 10) ?? '',
  dueDate: d.card.dueDate?.slice(0, 10) ?? '',
  owner: d.assignedTo ? { id: d.assignedTo.userId ?? '', name: d.assignedTo.displayName, avatar: d.assignedTo.avatarUrl } : null,
});

const same = (a: Draft, b: Draft) =>
  a.title === b.title && a.description === b.description && a.priority === b.priority && a.startDate === b.startDate && a.dueDate === b.dueDate &&
  (a.owner?.id ?? '') === (b.owner?.id ?? '');

/** Today in Portugal, "yyyy-mm-dd". */
const lisbonToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date());

/** "Faltam 3 dias", "Falta 1 dia", "Termina hoje", "Atrasado 2 dias" against the Portuguese calendar day; nothing once done. */
export function deadline(due: string | null, status: Status): { text: string; tone: 'late' | 'soon' | 'ok' } | null {
  if (!due || status === 'Done') return null;
  const days = Math.round((Date.parse(due.slice(0, 10)) - Date.parse(lisbonToday())) / 86_400_000);
  if (days < 0) return { text: `Atrasado ${-days} ${days === -1 ? 'dia' : 'dias'}`, tone: 'late' };
  if (days === 0) return { text: 'Termina hoje', tone: 'soon' };
  return { text: days === 1 ? 'Falta 1 dia' : `Faltam ${days} dias`, tone: days <= 3 ? 'soon' : 'ok' };
}

/** "12/11/2026, 21:10" in Portugal time (the server sends UTC). */
const at = (iso: string) =>
  new Intl.DateTimeFormat('pt-PT', { timeZone: 'Europe/Lisbon', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(
    new Date(iso),
  );

const Err = ({ text }: { text?: string }) =>
  text ? (
    <p className="form__error" role="alert">
      {text}
    </p>
  ) : null;

const Section = ({ id, title, extra, children }: { id: string; title: string; extra?: ReactNode; children: ReactNode }) => (
  <section className="lx-detail__section" aria-labelledby={id}>
    <h3 id={id} className="lx-detail__title">
      {title} {extra}
    </h3>
    {children}
  </section>
);

/**
 * The card workspace (Logística 2/5, task 066). One layout for everyone; the controls follow the server's flags:
 * `canWork` (owner, collaborators, managers) edits the card's fields, checklist, status, comments and attachments;
 * `canManage` also changes the owner, collaborators, labels, linked cards and deletes. The card's own fields save
 * together ("Guardar alterações" in the footer, version-checked: a 409 shows a conflict and keeps the draft); the
 * rest saves as you go and shows only what the server answered. Full screen on phones.
 */
export function CardWorkspace({
  board,
  cardId,
  onClose,
  onChanged,
  onDelete,
}: {
  board: Board;
  cardId: number;
  onClose: () => void;
  onChanged: () => void;
  onDelete: (card: Card) => void;
}) {
  const [detail, setDetail] = useState<CardDetail | 'missing' | null>();
  const [draft, setDraft] = useState<Draft>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [busy, setBusy] = useState<string>();
  const fid = useId();

  const load = (keepDraft = false) =>
    logisticsApi.card(cardId).then((o) => {
      if (o.kind !== 'ok') return setDetail(o.kind === 'notfound' ? 'missing' : null);
      setDetail(o.data);
      if (!keepDraft) setDraft(draftOf(o.data));
    });

  useEffect(() => {
    load();
  }, [cardId]);

  if (!detail || detail === 'missing' || !draft) {
    return (
      <Dialog title="Cartão" onClose={onClose} className="lx-card-modal">
        {detail === undefined ? (
          <Loading label="A carregar o cartão…" />
        ) : (
          <div className="notice" role="status">
            <p>{detail === 'missing' ? 'Este cartão já não existe.' : 'Não foi possível abrir o cartão.'}</p>
            {detail === null && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setDetail(undefined), load())}>
                Tentar novamente
              </button>
            )}
          </div>
        )}
      </Dialog>
    );
  }

  const card = detail.card;
  const dirty = detail.canWork && !same(draft, draftOf(detail));
  const due = deadline(detail.card.dueDate, card.status);

  // Every immediate change answers with the fresh card; the draft of the card's own fields is kept.
  const apply = async (o: Promise<Outcome<CardDetail>>, key: string, what: string) => {
    setErrors({});
    setBusy(key);
    const r = await o;
    setBusy(undefined);
    if (r.kind === 'ok') {
      setDetail(r.data);
      onChanged();
      return true;
    }
    setErrors({ [key]: problem(r, what) });
    return false;
  };

  const save = async (e?: FormEvent, version = detail.version) => {
    e?.preventDefault();
    setSaving(true);
    setSaved(false);
    setErrors({});
    const o = await logisticsApi.updateCard(card.id, {
      title: draft.title,
      description: draft.description,
      priority: draft.priority,
      startDate: draft.startDate || null,
      dueDate: draft.dueDate || null,
      // Non-managers have no member ids: they send none and the server keeps the owner.
      assignedToUserId: detail.canManage ? draft.owner?.id || null : null,
      version,
    });
    setSaving(false);
    if (o.kind === 'conflict') return setConflict(true);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o cartão') });
    setConflict(false);
    setDetail(o.data);
    setDraft(draftOf(o.data));
    setSaved(true);
    onChanged();
  };

  // Their version wins: reload, the draft is dropped. Mine wins: save the draft over the newest version.
  const reloadTheirs = () => (setConflict(false), load());
  const keepMine = async () => {
    const o = await logisticsApi.card(card.id);
    if (o.kind === 'ok') save(undefined, o.data.version);
    else setErrors({ form: problem(o, 'guardar o cartão') });
  };

  const close = () => {
    if (dirty && !window.confirm('Tem alterações por guardar neste cartão. Fechar sem guardar?')) return;
    onClose();
  };

  const set = (patch: Partial<Draft>) => (setSaved(false), setDraft({ ...draft, ...patch }));
  const listName = board.lists.find((l) => l.id === detail.listId)?.name;

  return (
    <Dialog
      title={card.title}
      size="xl"
      className="lx-card-modal"
      onClose={close}
      footer={
        <>
          {detail.canManage && (
            <button type="button" className="btn btn--danger lx-ws__delete" onClick={() => onDelete(card)}>
              <Icon name="trash" />
              Eliminar
            </button>
          )}
          <span className="lx-ws__state" role="status">
            {saving ? 'A guardar…' : dirty ? 'Alterações por guardar' : saved ? 'Guardado.' : ''}
          </span>
          <button type="button" className="btn btn--ghost" onClick={close}>
            Fechar
          </button>
          {detail.canWork && (
            <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={!dirty || saving || !draft.title.trim()}>
              {saving && <span className="spinner spinner--small" aria-hidden="true" />}
              Guardar alterações
            </button>
          )}
        </>
      }
    >
      <div className="lx-ws">
        <div className="lx-ws__strip">
          <span className={`lx-status lx-view__status--${card.status.toLowerCase()}`}>{STATUS[card.status].label}</span>
          {card.priority !== 'Normal' && <span className={`lx-prio lx-prio--${card.priority.toLowerCase()}`}>{PRIORITY[card.priority]}</span>}
          {due && <span className={`lx-due lx-due--${due.tone}`}>{due.text}</span>}
          {listName && <span className="lx-chip">{listName}</span>}
          {card.mine && <span className="lx-chip lx-chip--mine">Atribuído a si</span>}
          {!detail.assignedTo && <span className="lx-chip lx-chip--warn">Sem responsável</span>}
        </div>
        {!detail.canWork && (
          <p className="lx-view__role">
            <Icon name="eye" />
            Só leitura: este cartão é trabalhado pelo responsável, pelos colaboradores e por Mod, Admin ou Owner.
          </p>
        )}
        {conflict && (
          <div className="form__banner lx-ws__conflict" role="alert">
            <p>Outra pessoa guardou este cartão enquanto o editava. As suas alterações ainda não foram guardadas.</p>
            <div className="lx-inline">
              <button type="button" className="btn btn--ghost btn--sm" onClick={reloadTheirs}>
                Ver a versão atual (descartar as minhas)
              </button>
              <button type="button" className="btn btn--primary btn--sm" onClick={keepMine}>
                Guardar as minhas por cima
              </button>
            </div>
          </div>
        )}
        <Err text={errors.form} />

        <form id={`${fid}-form`} className="lx-ws__grid" onSubmit={save} noValidate>
          <div className="lx-ws__main">
            {detail.canWork ? (
              <>
                <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
                  <label htmlFor={`${fid}-title`}>Título</label>
                  <input id={`${fid}-title`} type="text" maxLength={200} value={draft.title} onChange={(e) => set({ title: e.target.value })} />
                  <Err text={errors.title} />
                </div>
                <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
                  <label htmlFor={`${fid}-desc`}>Detalhes</label>
                  <textarea id={`${fid}-desc`} rows={5} maxLength={2000} value={draft.description} onChange={(e) => set({ description: e.target.value })} />
                  <small className="note">{draft.description.length}/2000</small>
                  <Err text={errors.description} />
                </div>
              </>
            ) : (
              card.description && (
                <Section id={`${fid}-desc`} title="Detalhes">
                  <p className="lx-view__text">{card.description}</p>
                </Section>
              )
            )}

            <Checklist detail={detail} busy={busy} errors={errors} apply={apply} />
            <Attachments detail={detail} board={board} apply={apply} errors={errors} onUploaded={(d) => (setDetail(d), onChanged())} />
            <Discussion detail={detail} apply={apply} busy={busy} errors={errors} />
          </div>

          <aside className="lx-ws__side" aria-label="Estado e pessoas">
            <Section id={`${fid}-status`} title="Estado">
              {detail.canWork ? (
                <div className="lx-segment" role="group" aria-labelledby={`${fid}-status`}>
                  {STATUSES.map((s) => (
                    <button
                      key={s}
                      type="button"
                      className={`lx-segment--${s.toLowerCase()}`}
                      aria-pressed={card.status === s}
                      disabled={!!busy}
                      onClick={() => card.status !== s && apply(logisticsApi.setStatus(card.id, s), 'status', 'mudar o estado')}
                    >
                      {STATUS[s].label}
                      <small>{STATUS[s].name}</small>
                    </button>
                  ))}
                </div>
              ) : (
                <p className="lx-view__text">
                  {STATUS[card.status].label} · {STATUS[card.status].name}
                </p>
              )}
              <Err text={errors.status} />
            </Section>

            <div className="lx-ws__facts">
              <div className={errors.priority ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={`${fid}-prio`}>Prioridade</label>
                {detail.canWork ? (
                  <span className="control control--select control--sm">
                    <select id={`${fid}-prio`} value={draft.priority} onChange={(e) => set({ priority: e.target.value as Priority })}>
                      {PRIORITIES.map((p) => (
                        <option key={p} value={p}>
                          {PRIORITY[p]}
                        </option>
                      ))}
                    </select>
                  </span>
                ) : (
                  <p id={`${fid}-prio`} className="lx-view__text">
                    {PRIORITY[card.priority]}
                  </p>
                )}
                <Err text={errors.priority} />
              </div>
              <div className={errors.startDate ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={`${fid}-start`}>Início</label>
                {detail.canWork ? (
                  <input id={`${fid}-start`} type="date" value={draft.startDate} onChange={(e) => set({ startDate: e.target.value })} />
                ) : (
                  <p id={`${fid}-start`} className="lx-view__text">
                    {day(card.startDate) || '—'}
                  </p>
                )}
                <Err text={errors.startDate} />
              </div>
              <div className={errors.dueDate ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={`${fid}-due`}>Prazo</label>
                {detail.canWork ? (
                  <input id={`${fid}-due`} type="date" value={draft.dueDate} onChange={(e) => set({ dueDate: e.target.value })} />
                ) : (
                  <p id={`${fid}-due`} className="lx-view__text">
                    {day(card.dueDate) || '—'}
                  </p>
                )}
                <Err text={errors.dueDate} />
              </div>
            </div>

            <People detail={detail} draft={draft} set={set} apply={apply} errors={errors} />
            <Labels detail={detail} apply={apply} errors={errors} />
            <LinkedCards detail={detail} board={board} apply={apply} errors={errors} />
            {card.eventName && <p className="note">Atuação: {card.eventName}</p>}
            <History activity={detail.activity} />
          </aside>
        </form>
      </div>
    </Dialog>
  );
}

type Apply = (o: Promise<Outcome<CardDetail>>, key: string, what: string) => Promise<boolean>;

/** One item at a time: tick, rename (Enter saves, Esc cancels), remove, add. The item's text identifies it on the server. */
function Checklist({ detail, busy, errors, apply }: { detail: CardDetail; busy?: string; errors: Record<string, string>; apply: Apply }) {
  const [task, setTask] = useState('');
  const [editing, setEditing] = useState<{ index: number; text: string }>();
  const id = useId();
  const items = detail.checklist;
  const done = items.filter((i) => i.done).length;
  const cardId = detail.card.id;

  const add = async () => {
    if (task.trim() && (await apply(logisticsApi.addItem(cardId, task.trim()), 'task', 'adicionar a tarefa'))) setTask('');
  };
  const rename = async () => {
    if (!editing) return;
    const item = items[editing.index];
    if (!editing.text.trim() || editing.text.trim() === item.task) return setEditing(undefined);
    if (await apply(logisticsApi.renameItem(cardId, editing.index, item.task, editing.text.trim()), 'items', 'editar a tarefa')) setEditing(undefined);
  };

  return (
    <Section id={`${id}-check`} title="Checklist" extra={items.length > 0 && <small>{done}/{items.length}</small>}>
      {items.length === 0 ? (
        <p className="note">{detail.canWork ? 'Sem tarefas. Divida o trabalho em passos abaixo.' : 'Sem tarefas.'}</p>
      ) : (
        <>
          <span className="lx-progress__bar lx-progress__bar--wide">
            <span style={{ width: `${Math.round((done / items.length) * 100)}%` }} />
          </span>
          <ul className="lx-checklist">
            {items.map((item, i) =>
              !detail.canWork ? (
                <li key={i} className="lx-checklist__read">
                  {item.done ? <Icon name="checkCircle" className="lx-checklist__tick" /> : <span className="lx-checklist__open" aria-hidden="true" />}
                  <span className={item.done ? 'lx-checklist__done' : undefined}>
                    {item.task}
                    <span className="sr-only">{item.done ? ' (feita)' : ' (por fazer)'}</span>
                  </span>
                </li>
              ) : editing?.index === i ? (
                <li key={i}>
                  <input
                    className="lx-checklist__edit"
                    type="text"
                    aria-label={`Editar a tarefa ${item.task}`}
                    maxLength={200}
                    autoFocus
                    value={editing.text}
                    onChange={(e) => setEditing({ index: i, text: e.target.value })}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') (e.preventDefault(), rename());
                      if (e.key === 'Escape') (e.preventDefault(), e.stopPropagation(), setEditing(undefined));
                    }}
                  />
                  <button type="button" className="icon-btn icon-btn--sm" onClick={rename} disabled={!!busy}>
                    <Icon name="check" />
                    <span className="sr-only">Guardar a tarefa</span>
                  </button>
                </li>
              ) : (
                <li key={i}>
                  <label>
                    <input
                      type="checkbox"
                      checked={item.done}
                      disabled={!!busy}
                      onChange={() => apply(logisticsApi.toggleItem(cardId, i, item.task, !item.done), 'items', 'guardar a tarefa')}
                    />
                    <span className={item.done ? 'lx-checklist__done' : undefined}>{item.task}</span>
                  </label>
                  <button type="button" className="icon-btn icon-btn--sm" onClick={() => setEditing({ index: i, text: item.task })}>
                    <Icon name="pencil" />
                    <span className="sr-only">Editar {item.task}</span>
                  </button>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    disabled={!!busy}
                    onClick={() => apply(logisticsApi.removeItem(cardId, i, item.task), 'items', 'remover a tarefa')}
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Remover {item.task}</span>
                  </button>
                </li>
              ),
            )}
          </ul>
        </>
      )}
      <Err text={errors.items} />
      {detail.canWork && (
        <div className="lx-inline">
          <input
            type="text"
            aria-label="Nova tarefa"
            placeholder="Nova tarefa"
            maxLength={200}
            value={task}
            onChange={(e) => setTask(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), add())}
          />
          <button type="button" className="btn btn--ghost btn--sm" disabled={!task.trim() || !!busy} onClick={add}>
            <Icon name="plus" />
            Adicionar
          </button>
        </div>
      )}
      <Err text={errors.task} />
    </Section>
  );
}

/** The card's own files and links (not the board's files, not links to other cards). */
function Attachments({
  detail,
  board,
  apply,
  errors,
  onUploaded,
}: {
  detail: CardDetail;
  board: Board;
  apply: Apply;
  errors: Record<string, string>;
  onUploaded: (d: CardDetail) => void;
}) {
  const [uploads, setUploads] = useState<Upload[]>([]);
  const [error, setError] = useState<string>();
  const [link, setLink] = useState<{ url: string; name: string }>();
  const [confirm, setConfirm] = useState<number>();
  const fileInput = useRef<HTMLInputElement>(null);
  const id = useId();
  const cardId = detail.card.id;

  const send = async (file: File) => {
    const name = file.name;
    if (file.size > MAX_FILE) return setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0, error: 'Ficheiro grande demais: o limite são 10 MB.' }]);
    if (!FILE_TYPES.includes(file.type))
      return setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0, error: 'Só PDF, Word, Excel, TXT, PNG ou JPG.' }]);
    setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0 }]);
    const o = await uploadAttachment(cardId, file, (share) => setUploads((u) => u.map((x) => (x.name === name ? { ...x, share } : x))));
    if (o.kind === 'ok') {
      setUploads((u) => u.filter((x) => x.name !== name));
      onUploaded(o.data);
    } else {
      setUploads((u) => u.map((x) => (x.name === name ? { ...x, error: problem(o, 'carregar o ficheiro') } : x)));
    }
  };

  const open = async (a: CardAttachment) => {
    setError(undefined);
    const o = await logisticsApi.attachment(cardId, a.id);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener,noreferrer');
    else setError(problem(o, 'abrir o anexo'));
  };

  const boardFile = async (name: string) => {
    setError(undefined);
    const o = await logisticsApi.file(board.id, name);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener');
    else setError(problem(o, 'abrir o ficheiro'));
  };

  const addLink = async (e: FormEvent | KeyboardEvent) => {
    e.preventDefault();
    if (!link) return;
    setError(undefined);
    if (await apply(logisticsApi.addLink(cardId, link.url.trim(), link.name.trim()), 'url', 'adicionar a ligação')) setLink(undefined);
  };

  return (
    <Section id={`${id}-att`} title="Anexos" extra={detail.attachments.length > 0 && <small>{detail.attachments.length}</small>}>
      {detail.attachments.length === 0 && uploads.length === 0 && <p className="note">Sem anexos neste cartão.</p>}
      {(detail.attachments.length > 0 || uploads.length > 0) && (
        <ul className="lx-files">
          {detail.attachments.map((a) => (
            <li key={a.id}>
              <Icon name={a.kind === 'Link' ? 'external' : 'file'} />
              <button type="button" className="lx-files__name lx-files__open" onClick={() => open(a)}>
                {a.name}
                <small>
                  {a.kind === 'Link' ? a.host : `${(a.extension ?? '').replace('.', '').toUpperCase()} · ${size(a.sizeBytes)}`}
                  {a.addedBy && ` · ${a.addedBy.displayName}`}
                </small>
              </button>
              <button type="button" className="icon-btn icon-btn--sm" onClick={() => open(a)}>
                <Icon name={a.kind === 'Link' ? 'external' : 'download'} />
                <span className="sr-only">{a.kind === 'Link' ? `Abrir ${a.name}` : `Descarregar ${a.name}`}</span>
              </button>
              {a.canDelete &&
                (confirm === a.id ? (
                  <button
                    type="button"
                    className="btn btn--danger btn--sm"
                    onClick={async () => (setConfirm(undefined), await apply(logisticsApi.removeAttachment(cardId, a.id), 'attachments', 'remover o anexo'))}
                  >
                    Remover?
                  </button>
                ) : (
                  <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setConfirm(a.id)}>
                    <Icon name="trash" />
                    <span className="sr-only">Remover {a.name}</span>
                  </button>
                ))}
            </li>
          ))}
          {uploads.map((u) => (
            <li key={`up-${u.name}`} className={u.error ? 'lx-upload-row lx-upload-row--error' : 'lx-upload-row'}>
              <Icon name={u.error ? 'warning' : 'upload'} />
              <span className="lx-files__name">
                {u.name}
                {u.error ? (
                  <small role="alert">{u.error}</small>
                ) : (
                  <span className="lx-progress__bar" role="progressbar" aria-label={`A carregar ${u.name}`} aria-valuenow={Math.round(u.share * 100)}>
                    <span style={{ width: `${Math.round(u.share * 100)}%` }} />
                  </span>
                )}
              </span>
              {u.error && FILE_TYPES.includes(u.file.type) && u.file.size <= MAX_FILE && (
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => send(u.file)}>
                  Tentar novamente
                </button>
              )}
              {u.error && (
                <button type="button" className="icon-btn icon-btn--sm" onClick={() => setUploads((x) => x.filter((y) => y.name !== u.name))}>
                  <Icon name="close" />
                  <span className="sr-only">Dispensar</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {detail.canWork && (
        <div className="lx-inline">
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => fileInput.current?.click()}>
            <Icon name="upload" />
            Carregar ficheiro
          </button>
          <input
            ref={fileInput}
            className="sr-only"
            type="file"
            tabIndex={-1}
            aria-hidden="true"
            accept=".pdf,.docx,.doc,.xlsx,.xls,.txt,.png,.jpg,.jpeg"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) send(file);
              e.target.value = '';
            }}
          />
          {!link && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setLink({ url: '', name: '' })}>
              <Icon name="link" />
              Adicionar ligação
            </button>
          )}
          <small className="note">PDF, Word, Excel, TXT, PNG ou JPG, até 10 MB.</small>
        </div>
      )}
      {link && (
        <div className="lx-link-form">
          <input
            type="url"
            inputMode="url"
            aria-label="Endereço (https://…)"
            placeholder="https://…"
            autoFocus
            value={link.url}
            onChange={(e) => setLink({ ...link, url: e.target.value })}
            onKeyDown={(e) => e.key === 'Enter' && addLink(e)}
          />
          <input
            type="text"
            aria-label="Nome (opcional)"
            placeholder="Nome (opcional)"
            maxLength={200}
            value={link.name}
            onChange={(e) => setLink({ ...link, name: e.target.value })}
            onKeyDown={(e) => e.key === 'Enter' && addLink(e)}
          />
          <div className="lx-inline">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setLink(undefined)}>
              Cancelar
            </button>
            <button type="button" className="btn btn--primary btn--sm" disabled={!link.url.trim()} onClick={addLink}>
              Adicionar
            </button>
          </div>
        </div>
      )}
      <Err text={error ?? errors.attachments ?? errors.url ?? errors.name} />
      {board.files.length > 0 && (
        <details className="lx-ws__board-files">
          <summary>Ficheiros do quadro ({board.files.length}): partilhados por todos os cartões</summary>
          <ul className="lx-files">
            {board.files.map((f) => (
              <li key={f.name}>
                <Icon name="folder" />
                <span className="lx-files__name">
                  {f.name}
                  <small>
                    {f.extension.replace('.', '').toUpperCase()} · {size(f.sizeBytes)}
                  </small>
                </span>
                <button type="button" className="icon-btn icon-btn--sm" onClick={() => boardFile(f.name)}>
                  <Icon name="download" />
                  <span className="sr-only">Descarregar {f.name}</span>
                </button>
              </li>
            ))}
          </ul>
        </details>
      )}
    </Section>
  );
}

/** The discussion: plain text (never HTML), @mentions picked from members, Ctrl/⌘+Enter sends. */
function Discussion({ detail, apply, busy, errors }: { detail: CardDetail; apply: Apply; busy?: string; errors: Record<string, string> }) {
  const [body, setBody] = useState('');
  const [mentions, setMentions] = useState<{ id: string; name: string }[]>([]);
  const [picking, setPicking] = useState(false);
  const [editing, setEditing] = useState<{ id: number; body: string }>();
  const [confirm, setConfirm] = useState<number>();
  const id = useId();
  const cardId = detail.card.id;

  const send = async () => {
    if (!body.trim()) return;
    if (await apply(logisticsApi.comment(cardId, body.trim(), mentions.map((m) => m.id)), 'body', 'publicar o comentário')) {
      setBody('');
      setMentions([]);
    }
  };

  return (
    <Section id={`${id}-talk`} title="Discussão" extra={detail.comments.length > 0 && <small>{detail.comments.length}</small>}>
      {detail.comments.length === 0 ? (
        <p className="note">{detail.canWork ? 'Ainda sem comentários. Combine aqui os próximos passos.' : 'Ainda sem comentários.'}</p>
      ) : (
        <ol className="lx-talk">
          {detail.comments.map((c: CardComment) => (
            <li key={c.id} className="lx-talk__item">
              <img className="lx-talk__avatar" src={c.author?.avatarUrl ?? DEFAULT_AVATAR} alt="" width="32" height="32" loading="lazy" />
              <div className="lx-talk__body">
                <p className="lx-talk__who">
                  <strong title={c.author?.fullName ?? undefined}>{c.author?.displayName ?? 'Membro removido'}</strong>
                  <time dateTime={c.createdAt}>{at(c.createdAt)}</time>
                  {c.editedAt && <span className="note"> · editado</span>}
                </p>
                {editing?.id === c.id ? (
                  <div className="lx-talk__edit">
                    <textarea
                      aria-label="Editar o comentário"
                      rows={3}
                      maxLength={2000}
                      value={editing.body}
                      onChange={(e) => setEditing({ id: c.id, body: e.target.value })}
                    />
                    <div className="lx-inline">
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)}>
                        Cancelar
                      </button>
                      <button
                        type="button"
                        className="btn btn--primary btn--sm"
                        disabled={!editing.body.trim() || !!busy}
                        onClick={async () => (await apply(logisticsApi.editComment(c.id, editing.body.trim()), `comment-${c.id}`, 'editar o comentário')) && setEditing(undefined)}
                      >
                        Guardar
                      </button>
                    </div>
                  </div>
                ) : (
                  <p className="lx-talk__text">{c.body}</p>
                )}
                {c.mentions.length > 0 && <p className="lx-talk__mentions">Menciona: {c.mentions.join(', ')}</p>}
                {(c.canEdit || c.canDelete) && editing?.id !== c.id && (
                  <p className="lx-talk__actions">
                    {c.canEdit && (
                      <button type="button" className="link-btn" onClick={() => setEditing({ id: c.id, body: c.body })}>
                        Editar
                      </button>
                    )}
                    {c.canDelete &&
                      (confirm === c.id ? (
                        <button
                          type="button"
                          className="link-btn link-btn--danger"
                          onClick={async () => (setConfirm(undefined), await apply(logisticsApi.removeComment(c.id), `comment-${c.id}`, 'apagar o comentário'))}
                        >
                          Confirmar: apagar
                        </button>
                      ) : (
                        <button type="button" className="link-btn" onClick={() => setConfirm(c.id)}>
                          Apagar
                        </button>
                      ))}
                  </p>
                )}
                <Err text={errors[`comment-${c.id}`]} />
              </div>
            </li>
          ))}
        </ol>
      )}
      {detail.canWork && (
        <div className="lx-talk__compose">
          <label className="sr-only" htmlFor={`${id}-new`}>
            Novo comentário
          </label>
          <textarea
            id={`${id}-new`}
            rows={3}
            maxLength={2000}
            placeholder="Escreva um comentário"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && (e.ctrlKey || e.metaKey) && (e.preventDefault(), send())}
          />
          {mentions.length > 0 && (
            <div className="chips">
              {mentions.map((m) => (
                <PersonChip key={m.id} name={`@${m.name}`} avatar={null} onRemove={() => setMentions(mentions.filter((x) => x.id !== m.id))} removeLabel={`Não mencionar ${m.name}`} />
              ))}
            </div>
          )}
          {picking && (
            <MemberPicker
              label="Mencionar membro"
              exclude={mentions.map((m) => m.id)}
              onPick={(m) => (setMentions([...mentions, { id: m.id, name: m.displayName }]), setPicking(false))}
            />
          )}
          <div className="lx-inline lx-talk__send">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setPicking(!picking)} aria-expanded={picking} disabled={mentions.length >= 20}>
              @ Mencionar
            </button>
            <small className="note">{body.length}/2000</small>
            <button type="button" className="btn btn--primary btn--sm" disabled={!body.trim() || busy === 'body'} onClick={send}>
              {busy === 'body' && <span className="spinner spinner--small" aria-hidden="true" />}
              Comentar
            </button>
          </div>
          <Err text={errors.body ?? errors.mentionUserIds} />
        </div>
      )}
    </Section>
  );
}

/** Owner (manager picks; saved with the card's fields) and collaborators (managers add / remove, saved at once). */
function People({
  detail,
  draft,
  set,
  apply,
  errors,
}: {
  detail: CardDetail;
  draft: Draft;
  set: (patch: Partial<Draft>) => void;
  apply: Apply;
  errors: Record<string, string>;
}) {
  const id = useId();
  const cardId = detail.card.id;
  const person = (name: string, avatar: string | null, note?: string) => (
    <span className="chip lx-person">
      <img src={avatar ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
      {name}
      {note && <small className="lx-view__lead">{note}</small>}
    </span>
  );

  return (
    <Section id={`${id}-people`} title="Pessoas">
      <div className="form__field">
        <span className="form__label">Responsável</span>
        {detail.canManage ? (
          draft.owner ? (
            <PersonChip name={draft.owner.name} avatar={draft.owner.avatar} onRemove={() => set({ owner: null })} removeLabel="Remover o responsável" />
          ) : (
            <MemberPicker label="Responsável" onPick={(m) => set({ owner: { id: m.id, name: m.displayName, avatar: m.avatarUrl } })} />
          )
        ) : detail.assignedTo ? (
          person(detail.assignedTo.displayName, detail.assignedTo.avatarUrl)
        ) : (
          <p className="note">Sem responsável.</p>
        )}
        <Err text={errors.assignedToUserId} />
      </div>
      <div className="form__field">
        <span className="form__label">Colaboradores</span>
        {detail.assignments.length > 0 ? (
          <div className="chips">
            {detail.assignments.map((a, i) =>
              detail.canManage && a.userId ? (
                <PersonChip
                  key={a.userId}
                  name={a.displayName}
                  avatar={a.avatarUrl}
                  onRemove={() => apply(logisticsApi.unassign(cardId, a.userId!), 'userId', 'remover o colaborador')}
                  removeLabel={`Remover ${a.displayName}`}
                />
              ) : (
                <span key={i}>{person(a.displayName, a.avatarUrl)}</span>
              ),
            )}
          </div>
        ) : (
          <p className="note">Sem colaboradores.</p>
        )}
        {detail.canManage && (
          <MemberPicker
            label="Adicionar colaborador"
            exclude={[...detail.assignments.flatMap((a) => (a.userId ? [a.userId] : [])), ...(draft.owner ? [draft.owner.id] : [])]}
            onPick={(m) => apply(logisticsApi.assign(cardId, m.id), 'userId', 'adicionar o colaborador')}
          />
        )}
        <Err text={errors.userId} />
      </div>
    </Section>
  );
}

function Labels({ detail, apply, errors }: { detail: CardDetail; apply: Apply; errors: Record<string, string> }) {
  const [text, setText] = useState('');
  const [colour, setColour] = useState('#6f42c1');
  const id = useId();
  const labels = detail.card.labels;
  const cardId = detail.card.id;
  if (!detail.canManage && labels.length === 0) return null;

  const add = async () => {
    if (text.trim() && (await apply(logisticsApi.setLabels(cardId, [...labels, { text: text.trim(), color: colour }]), 'labels', 'guardar as etiquetas'))) setText('');
  };

  return (
    <Section id={`${id}-labels`} title="Etiquetas">
      {labels.length > 0 ? (
        <ul className="lx-labels">
          {labels.map((l, i) => (
            <li key={`${l.text}-${i}`} className={detail.canManage ? 'lx-label lx-label--edit' : 'lx-label'}>
              <span className="lx-label__dot" style={{ background: HEX.test(l.color) ? l.color : '#6f42c1' }} aria-hidden="true" />
              {l.text}
              {detail.canManage && (
                <button type="button" className="chip__remove" onClick={() => apply(logisticsApi.setLabels(cardId, labels.filter((_, j) => j !== i)), 'labels', 'guardar as etiquetas')}>
                  <Icon name="close" />
                  <span className="sr-only">Remover {l.text}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="note">Sem etiquetas.</p>
      )}
      {detail.canManage && (
        <div className="lx-inline">
          <input type="text" aria-label="Nome da etiqueta" placeholder="Nova etiqueta" maxLength={50} value={text} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), add())} />
          <input type="color" aria-label="Cor da etiqueta" value={colour} onChange={(e) => setColour(e.target.value)} />
          <button type="button" className="btn btn--ghost btn--sm" onClick={add} disabled={!text.trim()}>
            <Icon name="plus" />
            Adicionar
          </button>
        </div>
      )}
      <Err text={errors.labels} />
    </Section>
  );
}

/** Links to other cards of this board ("Cartões ligados"), distinct from the attachments. */
function LinkedCards({ detail, board, apply, errors }: { detail: CardDetail; board: Board; apply: Apply; errors: Record<string, string> }) {
  const [pick, setPick] = useState('');
  const id = useId();
  const cardId = detail.card.id;
  const linked = detail.links.map((l) => l.cardId).filter((x): x is number => x !== null);
  const others = board.lists.flatMap((l) => l.cards.map((c) => ({ ...c, list: l.name }))).filter((c) => c.id !== cardId && !linked.includes(c.id));
  if (!detail.canManage && detail.links.length === 0) return null;

  return (
    <Section id={`${id}-links`} title="Cartões ligados">
      {detail.links.length > 0 ? (
        <ul className="lx-files">
          {detail.links.map((l, i) => (
            <li key={`${l.cardId}-${i}`}>
              <Icon name="kanban" />
              <span className="lx-files__name">{l.name}</span>
              {detail.canManage && (
                <button type="button" className="icon-btn icon-btn--sm" onClick={() => apply(logisticsApi.setLinks(cardId, linked.filter((x) => x !== l.cardId)), 'cardIds', 'remover a ligação')}>
                  <Icon name="trash" />
                  <span className="sr-only">Desligar {l.name}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="note">Sem cartões ligados.</p>
      )}
      {detail.canManage && others.length > 0 && (
        <div className="lx-inline">
          <span className="control control--select control--sm">
            <select aria-label="Cartão a ligar" value={pick} onChange={(e) => setPick(e.target.value)}>
              <option value="">Ligar a outro cartão…</option>
              {others.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.list} · {c.title}
                </option>
              ))}
            </select>
          </span>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={!pick}
            onClick={async () => (await apply(logisticsApi.setLinks(cardId, [...linked, Number(pick)]), 'cardIds', 'ligar o cartão')) && setPick('')}
          >
            Ligar
          </button>
        </div>
      )}
      <Err text={errors.cardIds} />
    </Section>
  );
}

/** Who did what and when, newest first (Portugal time); folded by default. */
function History({ activity }: { activity: CardActivity[] }) {
  return (
    <details className="lx-history">
      <summary>Histórico ({activity.length})</summary>
      {activity.length === 0 ? (
        <p className="note">Sem alterações registadas.</p>
      ) : (
        <ol className="lx-history__list">
          {activity.map((a, i) => (
            <li key={i}>
              <span className="lx-history__what">{a.detail}</span>
              <span className="lx-history__meta">
                {a.actor?.displayName ?? 'Membro removido'} · <time dateTime={a.at}>{at(a.at)}</time>
              </span>
            </li>
          ))}
        </ol>
      )}
    </details>
  );
}
