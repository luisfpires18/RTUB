import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { dateLabel, type Outcome } from './eventsApi';
import { Icon } from './icons';
import {
  presenceLabel,
  rehearsalsApi,
  type MyAttendance,
  type NoticeAudience,
  type RehearsalInput,
  type RehearsalStats,
  type RehearsalSummary,
} from './rehearsalsApi';

const NOTES_MAX = 500;

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tens permissão para isto.'
      : o.kind === 'notfound'
        ? 'Este ensaio já não existe.'
        : o.kind === 'closed'
          ? 'Este ensaio já não permite isto. Recarrega a página.'
          : o.kind === 'signin'
            ? 'A sessão terminou. Entra outra vez.'
            : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

export function RehearsalLine({ rehearsal }: { rehearsal: RehearsalSummary }) {
  return (
    <p className="answer-event">
      <strong>{rehearsal.theme ?? 'Ensaio'}</strong>
      <span>
        {dateLabel({ date: rehearsal.date, endDate: null })} · {rehearsal.start}–{rehearsal.end} · {rehearsal.location}
      </span>
    </p>
  );
}

// ---------- the member's presença ----------

/**
 * "Vais ao ensaio?" - a member's presença (Vou / Não vou, the instrument, a note) on an upcoming rehearsal.
 * A new "vou" waits for Admin/Owner to confirm it. Over or cancelled: only removing one's own presença.
 */
export function PresenceDialog({ rehearsalId, onClose, onSaved }: { rehearsalId: number; onClose: () => void; onSaved: () => void }) {
  const [data, setData] = useState<MyAttendance | null>();

  useEffect(() => {
    rehearsalsApi.myAttendance(rehearsalId).then((o) => setData(o.kind === 'ok' ? o.data : null));
  }, [rehearsalId]);

  return (
    <Dialog title={data && data.state !== 'open' ? 'A minha presença' : 'Vais ao ensaio?'} onClose={onClose}>
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null ? (
        <p>Não foi possível abrir a tua presença agora. Tenta outra vez daqui a pouco.</p>
      ) : (
        <>
          <RehearsalLine rehearsal={data.rehearsal} />
          {data.state === 'open' ? (
            <PresenceForm data={data} onClose={onClose} onSaved={onSaved} />
          ) : data.state === 'cancelled' ? (
            <p className="warning">
              <Icon name="warning" />
              Este ensaio foi cancelado; não há presenças a marcar.
            </p>
          ) : (
            <RemovePresence data={data} onClose={onClose} onSaved={onSaved} />
          )}
        </>
      )}
    </Dialog>
  );
}

function PresenceForm({ data, onClose, onSaved }: { data: MyAttendance; onClose: () => void; onSaved: () => void }) {
  const going = data.status === 'pending' || data.status === 'approved';
  const [willAttend, setWillAttend] = useState<boolean | null>(data.status ? going : null);
  // Someone going plays their primary instrument unless they pick "não vou tocar"; a Leitão starts without one.
  const [instrument, setInstrument] = useState(going ? (data.instrument ?? '') : data.isLeitao ? '' : (data.defaultInstrument ?? ''));
  const [notes, setNotes] = useState(data.notes ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const ids = { instrument: useId(), notes: useId() };

  // Changing the answer starts a fresh note: a reason for missing it is not a note for going.
  const choose = (value: boolean) => {
    if (value === willAttend) return;
    setWillAttend(value);
    setNotes(value === going && data.status ? (data.notes ?? '') : '');
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (willAttend === null) return;
    setBusy(true);
    setError(undefined);
    const o = await rehearsalsApi.saveAttendance(data.rehearsal.id, willAttend, willAttend && instrument ? instrument : null, notes.trim() || null);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar'));
    onSaved();
    onClose();
  };

  return (
    <form className="answer" onSubmit={submit}>
      <fieldset className="choice">
        <legend className="sr-only">A tua presença</legend>
        <div className="choice__options">
          <label className={`choice__option choice__option--yes${willAttend === true ? ' is-on' : ''}`}>
            <input type="radio" name="willAttend" checked={willAttend === true} onChange={() => choose(true)} />
            <Icon name="check" />
            Vou
          </label>
          <label className={`choice__option choice__option--no${willAttend === false ? ' is-on' : ''}`}>
            <input type="radio" name="willAttend" checked={willAttend === false} onChange={() => choose(false)} />
            <Icon name="close" />
            Não vou
          </label>
        </div>
      </fieldset>

      {willAttend === true && data.instruments.length > 0 && (
        <div className="form__field">
          <label htmlFor={ids.instrument}>Instrumento</label>
          <span className="control control--select">
            <select id={ids.instrument} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
              <option value="">{data.isLeitao ? 'Sem instrumento' : 'Não vou tocar'}</option>
              {data.instruments.map((i) => (
                <option key={i.value} value={i.value}>
                  {i.label}
                </option>
              ))}
            </select>
          </span>
        </div>
      )}

      {willAttend !== null && (
        <details className="answer__more" open={Boolean(notes)}>
          <summary>{willAttend ? 'Acrescentar uma nota' : 'Dizer porquê'}</summary>
          <label className="sr-only" htmlFor={ids.notes}>
            {willAttend ? 'Nota' : 'Motivo'}
          </label>
          <textarea
            id={ids.notes}
            rows={3}
            maxLength={NOTES_MAX}
            placeholder={willAttend ? 'Chego mais tarde…' : 'Opcional'}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </details>
      )}

      {willAttend === true && data.status !== 'approved' && <p className="note">A presença fica pendente até ser confirmada no fim do ensaio.</p>}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Cancelar
        </button>
        <button type="submit" className="btn btn--primary" disabled={busy || willAttend === null}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Guardar
        </button>
      </div>
    </form>
  );
}

function RemovePresence({ data, onClose, onSaved }: { data: MyAttendance; onClose: () => void; onSaved: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  if (!data.status) return <p>Este ensaio já passou e não tens presença registada.</p>;

  const remove = async () => {
    setBusy(true);
    const o = await rehearsalsApi.removeAttendance(data.rehearsal.id);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'remover'));
    onSaved();
    onClose();
  };

  return (
    <div className="answer">
      <p>
        A tua presença: <strong>{presenceLabel(data.status, true)}</strong>. O ensaio já passou; se a presença está errada, podes
        removê-la.
      </p>
      {error && <p className="form__error">{error}</p>}
      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Manter
        </button>
        <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
          Remover a minha presença
        </button>
      </div>
    </div>
  );
}

// ---------- Admin/Owner ----------

const emptyInput = (): RehearsalInput => ({ date: today(), location: 'Centro Académico', theme: '', description: '', notes: '' });

/** Create (no `rehearsal`) or edit one rehearsal. Editing never changes the date, as before. */
export function RehearsalFormDialog({ rehearsal, notes, onClose, onSaved }: { rehearsal?: RehearsalSummary; notes?: string | null; onClose: () => void; onSaved: (id: number) => void }) {
  const [form, setForm] = useState<RehearsalInput>(() =>
    rehearsal
      ? { date: rehearsal.date, location: rehearsal.location, theme: rehearsal.theme ?? '', description: rehearsal.description ?? '', notes: notes ?? '' }
      : emptyInput(),
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ids = { date: useId(), location: useId(), theme: useId(), description: useId(), notes: useId() };
  const set = (next: Partial<RehearsalInput>) => setForm((f) => ({ ...f, ...next }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = rehearsal ? await rehearsalsApi.update(rehearsal.id, form) : await rehearsalsApi.create(form);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o, 'guardar') });
    onSaved(o.data.id);
    onClose();
  };

  const field = (key: keyof RehearsalInput) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <Dialog
      title={rehearsal ? 'Editar ensaio' : 'Adicionar ensaio'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancelar
          </button>
          <button type="submit" form="rehearsal-form" className="btn btn--primary" disabled={busy}>
            {busy ? 'A guardar…' : 'Guardar'}
          </button>
        </>
      }
    >
      <form id="rehearsal-form" className="form" onSubmit={submit} noValidate>
        {!rehearsal && (
          <div className={field('date')}>
            <label htmlFor={ids.date}>Data</label>
            <input id={ids.date} type="date" value={form.date} onChange={(e) => set({ date: e.target.value })} />
            {error('date')}
          </div>
        )}
        <div className={field('location')}>
          <label htmlFor={ids.location}>Local</label>
          <input id={ids.location} maxLength={200} value={form.location} onChange={(e) => set({ location: e.target.value })} />
          {error('location')}
        </div>
        <div className={field('theme')}>
          <label htmlFor={ids.theme}>Tema (opcional)</label>
          <input id={ids.theme} maxLength={500} value={form.theme} onChange={(e) => set({ theme: e.target.value })} />
          {error('theme')}
        </div>
        <div className={field('description')}>
          <label htmlFor={ids.description}>Descrição (opcional)</label>
          <textarea id={ids.description} rows={2} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
          {error('description')}
        </div>
        <div className={field('notes')}>
          <label htmlFor={ids.notes}>Notas (opcional)</label>
          <textarea id={ids.notes} rows={3} maxLength={1000} value={form.notes} onChange={(e) => set({ notes: e.target.value })} />
          {error('notes')}
        </div>
        <p className="note">Os ensaios são das 21:30 à meia-noite.</p>
        {error('form')}
      </form>
    </Dialog>
  );
}

/** Every Tuesday and Thursday between two dates (up to 3 months); dates that already have a rehearsal are skipped. */
export function RangeDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ from: today(), to: today(), location: 'Centro Académico', theme: '', description: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [done, setDone] = useState<{ created: number; skipped: number }>();
  const [busy, setBusy] = useState(false);
  const ids = { from: useId(), to: useId(), location: useId(), theme: useId(), description: useId() };
  const set = (next: Partial<typeof form>) => setForm((f) => ({ ...f, ...next }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await rehearsalsApi.createRange(form);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o, 'criar os ensaios') });
    setErrors({});
    setDone(o.data);
    if (o.data.created > 0) onSaved();
  };

  const error = (key: string) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <Dialog
      title="Adicionar vários ensaios"
      onClose={onClose}
      footer={
        done ? (
          <button type="button" className="btn btn--primary" onClick={onClose}>
            Fechar
          </button>
        ) : (
          <>
            <button type="button" className="btn btn--ghost" onClick={onClose}>
              Cancelar
            </button>
            <button type="submit" form="range-form" className="btn btn--primary" disabled={busy}>
              {busy ? 'A criar…' : 'Criar'}
            </button>
          </>
        )
      }
    >
      {done ? (
        <p role="status">
          {done.created === 0
            ? `Nenhum ensaio criado: ${done.skipped === 0 ? 'não há terças nem quintas nessas datas.' : 'todas essas datas já tinham ensaio.'}`
            : `Criados ${done.created} ensaios.${done.skipped > 0 ? ` ${done.skipped} datas já tinham ensaio e foram ignoradas.` : ''}`}
        </p>
      ) : (
        <form id="range-form" className="form" onSubmit={submit} noValidate>
          <div className="talk__lift-fields range-fields">
            <div className={errors.from ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.from}>De</label>
              <input id={ids.from} type="date" min={today()} value={form.from} onChange={(e) => set({ from: e.target.value })} />
              {error('from')}
            </div>
            <div className={errors.to ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.to}>Até</label>
              <input id={ids.to} type="date" min={form.from} value={form.to} onChange={(e) => set({ to: e.target.value })} />
              {error('to')}
            </div>
          </div>
          <div className={errors.location ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={ids.location}>Local</label>
            <input id={ids.location} maxLength={200} value={form.location} onChange={(e) => set({ location: e.target.value })} />
            {error('location')}
          </div>
          <div className="form__field">
            <label htmlFor={ids.theme}>Tema (opcional)</label>
            <input id={ids.theme} maxLength={500} value={form.theme} onChange={(e) => set({ theme: e.target.value })} />
            {error('theme')}
          </div>
          <div className="form__field">
            <label htmlFor={ids.description}>Descrição (opcional)</label>
            <textarea id={ids.description} rows={2} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
            {error('description')}
          </div>
          <p className="note">Cria um ensaio em cada terça e quinta do intervalo (máximo 3 meses).</p>
          {error('form')}
        </form>
      )}
    </Dialog>
  );
}

/** Cancel (reason required): every presença is removed, as before; no email goes out. */
export function CancelDialog({ rehearsal, onClose, onDone }: { rehearsal: RehearsalSummary; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  const submit = async () => {
    setBusy(true);
    const o = await rehearsalsApi.cancel(rehearsal.id, reason);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'cancelar'));
    onDone();
    onClose();
  };

  return (
    <Dialog
      title="Cancelar ensaio"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Não cancelar
          </button>
          <button type="button" className="btn btn--danger" disabled={busy} onClick={submit}>
            Cancelar ensaio
          </button>
        </>
      }
    >
      <div className="answer">
        <RehearsalLine rehearsal={rehearsal} />
        <div className="form__field">
          <label htmlFor={id}>Motivo</label>
          <textarea id={id} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
        <p className="note">As presenças marcadas são removidas. Não é enviado nenhum email.</p>
        {error && <p className="form__error">{error}</p>}
      </div>
    </Dialog>
  );
}

/** A push to subscribed members about an upcoming rehearsal; counts first, then a confirm. */
export function NoticeDialog({ rehearsal, onClose }: { rehearsal: RehearsalSummary; onClose: () => void }) {
  const [audience, setAudience] = useState<NoticeAudience | null>();
  const [message, setMessage] = useState('');
  const [few, setFew] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [result, setResult] = useState<string>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { message: useId(), few: useId() };

  useEffect(() => {
    rehearsalsApi.noticeAudience(rehearsal.id).then((o) => setAudience(o.kind === 'ok' ? o.data : null));
  }, [rehearsal.id]);

  const count = audience ? (few ? audience.leitoesCaloirosSubscribed : audience.subscribed) : 0;
  const send = async () => {
    setBusy(true);
    const o = await rehearsalsApi.sendNotice(rehearsal.id, message, few);
    setBusy(false);
    setConfirming(false);
    if (o.kind !== 'ok') return setError(problem(o, 'enviar'));
    setResult(`Enviada a ${o.data.sent} ${o.data.sent === 1 ? 'membro' : 'membros'}${o.data.failed ? ` (${o.data.failed} falharam)` : ''}.`);
  };

  return (
    <Dialog title="Notificar por push" onClose={onClose}>
      <div className="answer">
        <RehearsalLine rehearsal={rehearsal} />
        {result ? (
          <p role="status">{result}</p>
        ) : audience === undefined ? (
          <Loading label="A contar quem recebe…" />
        ) : (
          <>
            <div className="form__field">
              <label htmlFor={ids.message}>Mensagem</label>
              <textarea id={ids.message} rows={3} maxLength={500} value={message} onChange={(e) => (setMessage(e.target.value), setConfirming(false))} />
            </div>
            <label className="check" htmlFor={ids.few}>
              <input id={ids.few} type="checkbox" checked={few} onChange={(e) => (setFew(e.target.checked), setConfirming(false))} />
              Só Leitões e Caloiros
            </label>
            {audience && (
              <p className="note">
                Recebem {count} de {few ? audience.leitoesCaloirosTotal : audience.total} (quem tem as notificações ativas).
              </p>
            )}
            {error && <p className="form__error">{error}</p>}
            <div className="answer__actions">
              {confirming ? (
                <>
                  <span>Enviar a {count} membros?</span>
                  <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)}>
                    Não
                  </button>
                  <button type="button" className="btn btn--primary" disabled={busy} onClick={send}>
                    Sim, enviar
                  </button>
                </>
              ) : (
                <button type="button" className="btn btn--primary" disabled={!message.trim() || count === 0} onClick={() => setConfirming(true)}>
                  <Icon name="bell" />
                  Enviar…
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
}

// ---------- statistics and the member's own record ----------

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/**
 * "Estatísticas de presenças" (was the Blazor modal): for the dates chosen (the current season by default),
 * each member's confirmed and pending presenças at past, not cancelled rehearsals, and the confirmed share.
 */
export function StatsDialog({ onClose }: { onClose: () => void }) {
  const [stats, setStats] = useState<RehearsalStats | null>();
  const [range, setRange] = useState<{ from: string; to: string }>();
  const [group, setGroup] = useState('');
  const [q, setQ] = useState('');
  const [error, setError] = useState<string>();
  const ids = { from: useId(), to: useId(), group: useId(), q: useId() };

  useEffect(() => {
    if (range && (!range.from || !range.to)) return;
    let live = true;
    rehearsalsApi.stats(range?.from, range?.to).then((o) => {
      if (!live) return;
      if (o.kind === 'ok') {
        setError(undefined);
        setStats(o.data);
      } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
      else setStats(null);
    });
    return () => {
      live = false;
    };
  }, [range]);

  const from = range?.from ?? stats?.from ?? '';
  const to = range?.to ?? stats?.to ?? '';
  const shown =
    stats?.members.filter(
      (m) => (!group || m.groups.includes(group as 'tuno')) && (!q.trim() || fold(`${m.name} ${m.fullName ?? ''}`).includes(fold(q.trim()))),
    ) ?? [];

  return (
    <Dialog title="Estatísticas de presenças" onClose={onClose} size="lg">
      <div className="events-tools stats-tools">
        <label className="control" htmlFor={ids.from}>
          <span>De</span>
          <input id={ids.from} type="date" value={from} onChange={(e) => setRange({ from: e.target.value, to })} />
        </label>
        <label className="control" htmlFor={ids.to}>
          <span>Até</span>
          <input id={ids.to} type="date" value={to} onChange={(e) => setRange({ from, to: e.target.value })} />
        </label>
        <label className="control control--select" htmlFor={ids.group}>
          <span className="sr-only">Categoria</span>
          <select id={ids.group} value={group} onChange={(e) => setGroup(e.target.value)}>
            <option value="">Todas as categorias</option>
            <option value="tuno">Tunos</option>
            <option value="caloiro">Caloiros</option>
            <option value="leitao">Leitões</option>
          </select>
        </label>
        <label className="control" htmlFor={ids.q}>
          <span className="sr-only">Procurar membro</span>
          <Icon name="search" />
          <input id={ids.q} type="search" placeholder="Procurar pelo nome" value={q} onChange={(e) => setQ(e.target.value)} />
        </label>
      </div>
      {error && <p className="form__error">{error}</p>}
      {stats === undefined ? (
        <Loading label="A carregar as estatísticas…" />
      ) : stats === null ? (
        <p>Não foi possível carregar as estatísticas agora.</p>
      ) : shown.length === 0 ? (
        <p className="note">{stats.members.length === 0 ? 'Sem presenças nestas datas.' : 'Nenhum membro encontrado.'}</p>
      ) : (
        <>
          <p className="note">{stats.pastRehearsals === 1 ? '1 ensaio já feito' : `${stats.pastRehearsals} ensaios já feitos`} nestas datas.</p>
          <ul className="who">
            {shown.map((m, i) => (
              <li key={i} className="who__person" title={m.fullName ?? undefined}>
                <img className="who__avatar" src={m.avatarUrl} alt="" loading="lazy" width="72" height="72" />
                <p className="who__name">{m.name}</p>
                {m.categories.length > 0 && <span className="who__badge">{m.categories.join(' · ')}</span>}
                <p className="who__meta">
                  {m.approved === 1 ? '1 presença' : `${m.approved} presenças`}
                  {m.pending > 0 && ` · ${m.pending} pendentes`}
                </p>
                {stats.pastRehearsals > 0 && m.approved > 0 && (
                  <p className="who__note">{(m.approved / stats.pastRehearsals).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}</p>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}

const RECENT = 10;

/** "As minhas presenças" (was the Blazor modal): the last 10 (or all) past, not cancelled rehearsals and the confirmed share. */
export function MyPresencesDialog({ past, onClose }: { past: RehearsalSummary[]; onClose: () => void }) {
  const [all, setAll] = useState(false);
  const id = useId();
  const done = past.filter((r) => !r.cancelled);
  const shown = all ? done : done.slice(0, RECENT);
  const went = shown.filter((r) => r.mine?.status === 'approved').length;

  return (
    <Dialog title="As minhas presenças" onClose={onClose} size="lg">
      {shown.length === 0 ? (
        <p className="note">Ainda não há ensaios passados.</p>
      ) : (
        <>
          <p className="presence-rate">
            Estiveste em <strong>{went}</strong> de {shown.length} {all ? 'ensaios' : `ensaios (os últimos ${RECENT})`} ·{' '}
            {(went / shown.length).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}
          </p>
          <label className="check" htmlFor={id}>
            <input id={id} type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
            Mostrar todos
          </label>
          <ul className="my-list">
            {shown.map((r) => (
              <li key={r.id}>
                <a className="presence-row" href={`/rehearsals/${r.id}`}>
                  <span>
                    <strong>{dateLabel({ date: r.date, endDate: null })}</strong>
                    {r.theme && <small>{r.theme}</small>}
                  </span>
                  <PresencePill rehearsal={r} fallback />
                </a>
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}

/** The caller's own presença as a pill; `fallback` shows "Não foste" on a past rehearsal with none. */
export function PresencePill({ rehearsal, fallback = false }: { rehearsal: RehearsalSummary; fallback?: boolean }) {
  const status = rehearsal.mine?.status ?? (fallback && rehearsal.past ? 'notGoing' : null);
  if (!status || rehearsal.cancelled) return null;
  const kind = status === 'notGoing' ? 'no' : status === 'approved' ? 'yes' : 'wait';
  return (
    <span className={`pill pill--${kind}`}>
      <Icon name={status === 'notGoing' ? 'close' : status === 'approved' ? 'check' : 'clock'} />
      {presenceLabel(status, rehearsal.past)}
    </span>
  );
}
