// Admin/Owner dialogs to create / edit one rehearsal or add a range of them.

import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { today } from '../data/format';
import { rehearsalsApi, type RehearsalSummary, type RehearsalInput } from '../data/rehearsalsApi';
import { problem } from './RehearsalShared';

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
