import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import {
  logisticsApi,
  problem,
  STATUS,
  type Board,
  type Card,
  type CardDetail,
  type ChecklistItem,
  type Label,
  type List,
  type MemberOption,
  type Status,
} from './logisticsApi';
import { DEFAULT_AVATAR } from './membersApi';

const STATUSES: Status[] = ['Todo', 'InProgress', 'Done'];
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

const size = (b: number) => (b < 1024 ? `${b} B` : b < 1024 ** 2 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1024 ** 2).toFixed(1)} MB`);

const Footer = ({ onClose, busy, form, label, disabled }: { onClose: () => void; busy: boolean; form: string; label: string; disabled?: boolean }) => (
  <>
    <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
      Cancelar
    </button>
    <button type="submit" form={form} className="btn btn--primary" disabled={busy || disabled}>
      {busy && <span className="spinner spinner--small" aria-hidden="true" />}
      {label}
    </button>
  </>
);

const Error = ({ text }: { text?: string }) =>
  text ? (
    <p className="form__error" role="alert">
      {text}
    </p>
  ) : null;

/** Search members by nickname or name (the server returns at most 20) and pick one. */
export function MemberPicker({ label, exclude = [], onPick }: { label: string; exclude?: string[]; onPick: (m: MemberOption) => void }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<MemberOption[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => logisticsApi.members(query.trim()).then((o) => setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => clearTimeout(timer);
  }, [query]);

  const shown = found?.filter((m) => !exclude.includes(m.id));
  return (
    <div className="lx-picker">
      <label className="sr-only" htmlFor={id}>
        {label}
      </label>
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha ou nome" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {shown && shown.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {shown && shown.length > 0 && (
        <ul className="talk__picks">
          {shown.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  onPick(m);
                  setQuery('');
                }}
              >
                <img src={m.avatarUrl ?? DEFAULT_AVATAR} alt="" width="28" height="28" loading="lazy" />
                <span>
                  {m.displayName}
                  {m.fullName && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

const PersonChip = ({ name, avatar, onRemove, removeLabel }: { name: string; avatar: string | null; onRemove: () => void; removeLabel: string }) => (
  <span className="chip chip--removable">
    <img src={avatar ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
    {name}
    <button type="button" className="chip__remove" onClick={onRemove}>
      <Icon name="close" />
      <span className="sr-only">{removeLabel}</span>
    </button>
  </span>
);

export function ConfirmDialog({
  title,
  what,
  warning,
  action,
  onClose,
  onDone,
}: {
  title: string;
  what: ReactNode;
  warning: string;
  action: () => Promise<Outcome<void>>;
  onClose: () => void;
  onDone: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const run = async () => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={run} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>Eliminar {what}?</p>
      <p className="warning">
        <Icon name="warning" />
        {warning}
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

/** "Criar lista" / "Renomear lista". New lists go last, as before. */
export function ListDialog({ boardId, list, onClose, onSaved }: { boardId: number; list?: List; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(list?.name ?? '');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = list ? await logisticsApi.renameList(list.id, name) : await logisticsApi.createList(boardId, name);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar a lista'));
    onSaved();
    onClose();
  };
  return (
    <Dialog title={list ? 'Renomear lista' : 'Criar lista'} onClose={onClose} footer={<Footer onClose={onClose} busy={busy} form={`${fid}-form`} label="Guardar" disabled={!name.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-name`}>Nome da lista</label>
          <input id={`${fid}-name`} type="text" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          <Error text={error} />
        </div>
      </form>
    </Dialog>
  );
}

/** "Criar cartão": title, description and the optional "Atribuir a" member, as before. Opens the details after. */
export function CreateCardDialog({ list, onClose, onCreated }: { list: List; onClose: () => void; onCreated: (id: number) => void }) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assignee, setAssignee] = useState<MemberOption | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await logisticsApi.createCard(list.id, { title, description, assignedToUserId: assignee?.id ?? null });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'criar o cartão') });
    onCreated(o.data.card.id);
  };
  return (
    <Dialog title={`Novo cartão · ${list.name}`} onClose={onClose} footer={<Footer onClose={onClose} busy={busy} form={`${fid}-form`} label="Criar" disabled={!title.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Error text={errors.form} />
        <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-title`}>Título</label>
          <input id={`${fid}-title`} type="text" maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} autoFocus />
          <Error text={errors.title} />
        </div>
        <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-desc`}>Descrição (opcional)</label>
          <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
          <Error text={errors.description} />
        </div>
        <div className="form__field">
          <span className="form__label">Atribuir a (opcional)</span>
          {assignee ? (
            <PersonChip name={assignee.displayName} avatar={assignee.avatarUrl} onRemove={() => setAssignee(null)} removeLabel="Remover" />
          ) : (
            <MemberPicker label="Atribuir a" onPick={setAssignee} />
          )}
          <Error text={errors.assignedToUserId} />
        </div>
      </form>
    </Dialog>
  );
}

/** "Mover cartão": any list of the board, at any position; the touch-friendly twin of drag and drop. */
export function MoveCardDialog({
  board,
  card,
  listId,
  onClose,
  onMove,
}: {
  board: Board;
  card: Card;
  listId: number;
  onClose: () => void;
  onMove: (listId: number, position: number) => void;
}) {
  const [target, setTarget] = useState(listId);
  const others = board.lists.find((l) => l.id === target)!.cards.filter((c) => c.id !== card.id);
  const current = board.lists.find((l) => l.id === listId)!.cards.findIndex((c) => c.id === card.id);
  const [position, setPosition] = useState(target === listId ? current : others.length);
  const fid = useId();
  return (
    <Dialog
      title="Mover cartão"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={() => onMove(target, position)}>
            Mover
          </button>
        </>
      }
    >
      <p className="note">{card.title}</p>
      <div className="form">
        <div className="form__row">
          <div className="form__field">
            <label htmlFor={`${fid}-list`}>Lista</label>
            <span className="control control--select">
              <select
                id={`${fid}-list`}
                value={target}
                onChange={(e) => {
                  const next = Number(e.target.value);
                  setTarget(next);
                  setPosition(next === listId ? current : board.lists.find((l) => l.id === next)!.cards.length);
                }}
              >
                {board.lists.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </select>
            </span>
          </div>
          <div className="form__field">
            <label htmlFor={`${fid}-pos`}>Posição</label>
            <span className="control control--select">
              <select id={`${fid}-pos`} value={position} onChange={(e) => setPosition(Number(e.target.value))}>
                {Array.from({ length: others.length + 1 }, (_, i) => (
                  <option key={i} value={i}>
                    {i === 0 ? '1 (no topo)' : i === others.length ? `${i + 1} (no fim)` : i + 1}
                  </option>
                ))}
              </select>
            </span>
          </div>
        </div>
      </div>
    </Dialog>
  );
}

/** "Criar Lembrete": any member who sees the board, as before. Stored for the chosen members. */
export function ReminderDialog({ board, onClose }: { board: Board; onClose: () => void }) {
  const tomorrow = new Date(Date.now() + 864e5);
  const local = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 6e4).toISOString().slice(0, 16);
  const [cardId, setCardId] = useState('');
  const [frequency, setFrequency] = useState('OneTime');
  const [when, setWhen] = useState(local(tomorrow));
  const [people, setPeople] = useState<MemberOption[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await logisticsApi.createReminder(board.id, { cardId: Number(cardId), frequency, nextReminderDate: `${when}:00`, userIds: people.map((p) => p.id) });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'criar o lembrete') });
    setDone(true);
  };

  if (done) {
    return (
      <Dialog
        title="Lembrete criado"
        onClose={onClose}
        footer={
          <button type="button" className="btn btn--primary" onClick={onClose}>
            Fechar
          </button>
        }
      >
        <p>O lembrete ficou guardado.</p>
      </Dialog>
    );
  }

  return (
    <Dialog
      title="Criar lembrete"
      onClose={onClose}
      footer={<Footer onClose={onClose} busy={busy} form={`${fid}-form`} label="Criar lembrete" disabled={!cardId || people.length === 0 || !when} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Error text={errors.form} />
        <div className={errors.cardId ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-card`}>Cartão</label>
          <span className="control control--select">
            <select id={`${fid}-card`} value={cardId} onChange={(e) => setCardId(e.target.value)}>
              <option value="">Escolher um cartão…</option>
              {board.lists.map((l) => (
                <optgroup key={l.id} label={l.name}>
                  {l.cards.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.title}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </span>
          <Error text={errors.cardId} />
        </div>
        <div className="form__row">
          <div className={errors.frequency ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-freq`}>Frequência</label>
            <span className="control control--select">
              <select id={`${fid}-freq`} value={frequency} onChange={(e) => setFrequency(e.target.value)}>
                <option value="OneTime">Uma vez</option>
                <option value="Daily">Diariamente</option>
                <option value="Weekly">Semanalmente</option>
              </select>
            </span>
            <Error text={errors.frequency} />
          </div>
          <div className={errors.nextReminderDate ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-when`}>Primeira data</label>
            <input id={`${fid}-when`} type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
            <Error text={errors.nextReminderDate} />
          </div>
        </div>
        <div className={errors.userIds ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Membros</span>
          {people.length > 0 && (
            <div className="chips">
              {people.map((p) => (
                <PersonChip key={p.id} name={p.displayName} avatar={p.avatarUrl} onRemove={() => setPeople(people.filter((x) => x.id !== p.id))} removeLabel={`Remover ${p.displayName}`} />
              ))}
            </div>
          )}
          <MemberPicker label="Adicionar membro" exclude={people.map((p) => p.id)} onPick={(m) => setPeople([...people, m])} />
          <Error text={errors.userIds} />
        </div>
      </form>
    </Dialog>
  );
}

/** The board's files, shared by all its cards (as before). Every member downloads; managers upload and delete. */
export function FilesDialog({ board, onClose, onChanged }: { board: Board; onClose: () => void; onChanged: () => void }) {
  const [files, setFiles] = useState(board.files);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<string>();
  const fid = useId();

  useEffect(() => setFiles(board.files), [board.files]);

  const download = async (name: string) => {
    setError(undefined);
    const o = await logisticsApi.file(board.id, name);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener');
    else setError(problem(o, 'abrir o ficheiro'));
  };

  const upload = async (file: File | undefined) => {
    setError(undefined);
    if (!file) return;
    if (file.size > MAX_FILE) return setError('Ficheiro grande demais: o limite são 10 MB.');
    if (!FILE_TYPES.includes(file.type)) return setError('Só PDF, Word, Excel, TXT, PNG ou JPG.');
    setBusy(true);
    const o = await logisticsApi.upload(board.id, file);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'carregar o ficheiro'));
    onChanged();
  };

  const remove = async (name: string) => {
    setConfirm(undefined);
    setBusy(true);
    const o = await logisticsApi.removeFile(board.id, name);
    setBusy(false);
    if (o.kind !== 'ok' && o.kind !== 'notfound') return setError(problem(o, 'eliminar o ficheiro'));
    onChanged();
  };

  return (
    <Dialog
      title="Ficheiros do quadro"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Fechar
        </button>
      }
    >
      <p className="note">Ficam na pasta do quadro na Documentação, no ano letivo atual, e são os mesmos para todos os cartões.</p>
      {files.length === 0 ? (
        <p className="note">Ainda sem ficheiros.</p>
      ) : (
        <ul className="lx-files">
          {files.map((f) => (
            <li key={f.name}>
              <Icon name="file" />
              <span className="lx-files__name">
                {f.name}
                <small>
                  {f.extension.replace('.', '').toUpperCase()} · {size(f.sizeBytes)}
                </small>
              </span>
              <button type="button" className="icon-btn icon-btn--sm" onClick={() => download(f.name)}>
                <Icon name="download" />
                <span className="sr-only">Descarregar {f.name}</span>
              </button>
              {board.canManage &&
                (confirm === f.name ? (
                  <button type="button" className="btn btn--danger btn--sm" onClick={() => remove(f.name)} disabled={busy}>
                    Eliminar?
                  </button>
                ) : (
                  <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setConfirm(f.name)}>
                    <Icon name="trash" />
                    <span className="sr-only">Eliminar {f.name}</span>
                  </button>
                ))}
            </li>
          ))}
        </ul>
      )}
      {board.canManage && (
        <div className="lx-upload">
          <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
            {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="upload" />}
            Carregar ficheiro
          </label>
          <input
            id={`${fid}-file`}
            className="sr-only"
            type="file"
            accept=".pdf,.docx,.doc,.xlsx,.xls,.txt,.png,.jpg,.jpeg"
            disabled={busy}
            onChange={(e) => {
              upload(e.target.files?.[0]);
              e.target.value = '';
            }}
          />
          <small className="note">PDF, Word, Excel, TXT, PNG ou JPG, até 10 MB.</small>
        </div>
      )}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

/** "Detalhes do cartão" (Mod, Admin, Owner): every field the old modal edited; each section saves on its own. */
export function CardDialog({
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
  const [form, setForm] = useState({ title: '', description: '', startDate: '', dueDate: '' });
  const [assignee, setAssignee] = useState<{ id: string; name: string; avatar: string | null } | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [labelText, setLabelText] = useState('');
  const [labelColour, setLabelColour] = useState('#6f42c1');
  const [task, setTask] = useState('');
  const [link, setLink] = useState('');
  const fid = useId();

  const take = (d: CardDetail) => {
    setDetail(d);
    setForm({ title: d.card.title, description: d.card.description, startDate: d.card.startDate?.slice(0, 10) ?? '', dueDate: d.card.dueDate?.slice(0, 10) ?? '' });
    setAssignee(d.assignedTo ? { id: d.assignedTo.userId, name: d.assignedTo.displayName, avatar: d.assignedTo.avatarUrl } : null);
  };

  useEffect(() => {
    logisticsApi.card(cardId).then((o) => (o.kind === 'ok' ? take(o.data) : setDetail(o.kind === 'notfound' ? 'missing' : null)));
  }, [cardId]);

  if (!detail || detail === 'missing') {
    return (
      <Dialog title="Cartão" onClose={onClose}>
        {detail === undefined ? (
          <Loading label="A carregar…" />
        ) : (
          <p className="form__banner">{detail === 'missing' ? 'Este cartão já não existe.' : 'Não foi possível abrir o cartão.'}</p>
        )}
      </Dialog>
    );
  }

  const card = detail.card;

  // Every immediate change (status, labels, checklist, links, members) answers with the fresh card.
  const apply = async (o: Promise<Outcome<CardDetail>>, key: string, what: string) => {
    setErrors({});
    const r = await o;
    if (r.kind === 'ok') {
      setDetail(r.data);
      onChanged();
      return true;
    }
    setErrors({ [key]: problem(r, what) });
    return false;
  };

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setSaved(false);
    setErrors({});
    const o = await logisticsApi.updateCard(card.id, {
      title: form.title,
      description: form.description,
      startDate: form.startDate || null,
      dueDate: form.dueDate || null,
      assignedToUserId: assignee?.id ?? null,
    });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o cartão') });
    take(o.data);
    setSaved(true);
    onChanged();
  };

  const labels = card.labels;
  const addLabel = async () => {
    if (!labelText.trim()) return;
    if (await apply(logisticsApi.setLabels(card.id, [...labels, { text: labelText.trim(), color: labelColour }]), 'labels', 'guardar as etiquetas')) setLabelText('');
  };
  const setChecklist = (items: ChecklistItem[]) => apply(logisticsApi.setChecklist(card.id, items), 'items', 'guardar a checklist');
  const done = detail.checklist.filter((i) => i.done).length;
  const linked = detail.links.map((l) => l.cardId).filter((id): id is number => id !== null);
  const otherCards = board.lists.flatMap((l) => l.cards.map((c) => ({ ...c, list: l.name }))).filter((c) => c.id !== card.id && !linked.includes(c.id));
  const listName = board.lists.find((l) => l.id === detail.listId)?.name;

  return (
    <Dialog
      title="Detalhes do cartão"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--danger" onClick={() => onDelete(card)}>
            <Icon name="trash" />
            Eliminar
          </button>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Fechar
          </button>
        </>
      }
    >
      <div className="lx-detail">
        <p className="note">Lista: {listName}</p>

        <section className="lx-detail__section" aria-labelledby={`${fid}-status`}>
          <h3 id={`${fid}-status`} className="lx-detail__title">
            Estado
          </h3>
          <div className="lx-segment" role="group" aria-labelledby={`${fid}-status`}>
            {STATUSES.map((s) => (
              <button
                key={s}
                type="button"
                className={`lx-segment--${s.toLowerCase()}`}
                aria-pressed={card.status === s}
                onClick={() => card.status !== s && apply(logisticsApi.setStatus(card.id, s), 'status', 'mudar o estado')}
              >
                {STATUS[s].label}
                <small>{STATUS[s].name}</small>
              </button>
            ))}
          </div>
          <Error text={errors.status} />
        </section>

        <form className="form lx-detail__section" onSubmit={save} noValidate>
          <Error text={errors.form} />
          <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-title`}>Título</label>
            <input id={`${fid}-title`} type="text" maxLength={200} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
            <Error text={errors.title} />
          </div>
          <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-desc`}>Detalhes</label>
            <textarea id={`${fid}-desc`} rows={4} maxLength={2000} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            <Error text={errors.description} />
          </div>
          <div className="form__row">
            <div className={errors.startDate ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={`${fid}-start`}>Início</label>
              <input id={`${fid}-start`} type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
              <Error text={errors.startDate} />
            </div>
            <div className={errors.dueDate ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={`${fid}-due`}>Prazo</label>
              <input id={`${fid}-due`} type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} />
              <Error text={errors.dueDate} />
            </div>
          </div>
          <div className="form__field">
            <span className="form__label">Responsável</span>
            {assignee ? (
              <PersonChip name={assignee.name} avatar={assignee.avatar} onRemove={() => setAssignee(null)} removeLabel="Remover responsável" />
            ) : (
              <MemberPicker label="Responsável" onPick={(m) => setAssignee({ id: m.id, name: m.displayName, avatar: m.avatarUrl })} />
            )}
            <Error text={errors.assignedToUserId} />
          </div>
          <div className="lx-detail__save">
            {saved && <span className="note">Guardado.</span>}
            <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !form.title.trim()}>
              {busy && <span className="spinner spinner--small" aria-hidden="true" />}
              Guardar
            </button>
          </div>
        </form>

        <section className="lx-detail__section" aria-labelledby={`${fid}-labels`}>
          <h3 id={`${fid}-labels`} className="lx-detail__title">
            Etiquetas
          </h3>
          {labels.length > 0 ? (
            <ul className="lx-labels">
              {labels.map((l: Label, i) => (
                <li key={`${l.text}-${i}`} className="lx-label lx-label--edit">
                  <span className="lx-label__dot" style={{ background: /^#[0-9a-f]{6}$/i.test(l.color) ? l.color : '#6f42c1' }} aria-hidden="true" />
                  {l.text}
                  <button type="button" className="chip__remove" onClick={() => apply(logisticsApi.setLabels(card.id, labels.filter((_, j) => j !== i)), 'labels', 'guardar as etiquetas')}>
                    <Icon name="close" />
                    <span className="sr-only">Remover {l.text}</span>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="note">Sem etiquetas.</p>
          )}
          <div className="lx-inline">
            <input type="text" aria-label="Nome da etiqueta" placeholder="Nova etiqueta" maxLength={50} value={labelText} onChange={(e) => setLabelText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), addLabel())} />
            <input type="color" aria-label="Cor da etiqueta" value={labelColour} onChange={(e) => setLabelColour(e.target.value)} />
            <button type="button" className="btn btn--ghost btn--sm" onClick={addLabel} disabled={!labelText.trim()}>
              <Icon name="plus" />
              Adicionar
            </button>
          </div>
          <Error text={errors.labels} />
        </section>

        <section className="lx-detail__section" aria-labelledby={`${fid}-check`}>
          <h3 id={`${fid}-check`} className="lx-detail__title">
            Checklist {detail.checklist.length > 0 && <small>{done}/{detail.checklist.length}</small>}
          </h3>
          {detail.checklist.length > 0 && (
            <>
              <span className="lx-progress__bar lx-progress__bar--wide">
                <span style={{ width: `${Math.round((done / detail.checklist.length) * 100)}%` }} />
              </span>
              <ul className="lx-checklist">
                {detail.checklist.map((item, i) => (
                  <li key={i}>
                    <label>
                      <input
                        type="checkbox"
                        checked={item.done}
                        onChange={() => setChecklist(detail.checklist.map((x, j) => (j === i ? { ...x, done: !x.done } : x)))}
                      />
                      <span className={item.done ? 'lx-checklist__done' : undefined}>{item.task}</span>
                    </label>
                    <button type="button" className="icon-btn icon-btn--sm" onClick={() => setChecklist(detail.checklist.filter((_, j) => j !== i))}>
                      <Icon name="trash" />
                      <span className="sr-only">Remover {item.task}</span>
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
          <div className="lx-inline">
            <input
              type="text"
              aria-label="Nova tarefa"
              placeholder="Nova tarefa"
              maxLength={200}
              value={task}
              onChange={(e) => setTask(e.target.value)}
              onKeyDown={async (e) => {
                if (e.key !== 'Enter') return;
                e.preventDefault();
                if (task.trim() && (await setChecklist([...detail.checklist, { task: task.trim(), done: false }]))) setTask('');
              }}
            />
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              disabled={!task.trim()}
              onClick={async () => (await setChecklist([...detail.checklist, { task: task.trim(), done: false }])) && setTask('')}
            >
              <Icon name="plus" />
              Adicionar
            </button>
          </div>
          <Error text={errors.items} />
        </section>

        <section className="lx-detail__section" aria-labelledby={`${fid}-people`}>
          <h3 id={`${fid}-people`} className="lx-detail__title">
            Membros atribuídos
          </h3>
          {detail.assignments.length > 0 ? (
            <div className="chips">
              {detail.assignments.map((a) => (
                <PersonChip
                  key={a.userId}
                  name={a.displayName}
                  avatar={a.avatarUrl}
                  onRemove={() => apply(logisticsApi.unassign(card.id, a.userId), 'userId', 'remover o membro')}
                  removeLabel={`Remover ${a.displayName}`}
                />
              ))}
            </div>
          ) : (
            <p className="note">Ninguém atribuído.</p>
          )}
          <MemberPicker label="Atribuir membro" exclude={detail.assignments.map((a) => a.userId)} onPick={(m) => apply(logisticsApi.assign(card.id, m.id), 'userId', 'atribuir o membro')} />
          <Error text={errors.userId} />
        </section>

        <section className="lx-detail__section" aria-labelledby={`${fid}-links`}>
          <h3 id={`${fid}-links`} className="lx-detail__title">
            Ligações
          </h3>
          {detail.links.length > 0 ? (
            <ul className="lx-files">
              {detail.links.map((l, i) => (
                <li key={`${l.cardId}-${i}`}>
                  <Icon name="link" />
                  <span className="lx-files__name">{l.name}</span>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    onClick={() => apply(logisticsApi.setLinks(card.id, linked.filter((id) => id !== l.cardId)), 'cardIds', 'remover a ligação')}
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Remover a ligação a {l.name}</span>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="note">Sem ligações.</p>
          )}
          {otherCards.length > 0 && (
            <div className="lx-inline">
              <span className="control control--select control--sm">
                <select aria-label="Cartão a ligar" value={link} onChange={(e) => setLink(e.target.value)}>
                  <option value="">Ligar a outro cartão…</option>
                  {otherCards.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.list} · {c.title}
                    </option>
                  ))}
                </select>
              </span>
              <button
                type="button"
                className="btn btn--ghost btn--sm"
                disabled={!link}
                onClick={async () => (await apply(logisticsApi.setLinks(card.id, [...linked, Number(link)]), 'cardIds', 'ligar o cartão')) && setLink('')}
              >
                <Icon name="plus" />
                Ligar
              </button>
            </div>
          )}
          <Error text={errors.cardIds} />
        </section>

        {card.eventName && <p className="note">Atuação: {card.eventName}</p>}
      </div>
    </Dialog>
  );
}
