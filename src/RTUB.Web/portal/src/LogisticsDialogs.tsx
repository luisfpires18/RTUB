import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { logisticsApi, problem, type Board, type Card, type List, type MemberOption } from './logisticsApi';
import { DEFAULT_AVATAR } from './membersApi';

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

export const size = (b: number) => (b < 1024 ? `${b} B` : b < 1024 ** 2 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1024 ** 2).toFixed(1)} MB`);

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
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => logisticsApi.members(query.trim()).then((o) => live && setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
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

export const PersonChip = ({ name, avatar, onRemove, removeLabel }: { name: string; avatar: string | null; onRemove: () => void; removeLabel: string }) => (
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
