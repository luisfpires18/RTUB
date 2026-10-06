import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { memberAdminApi, type MemberEdit, type MemberInput, type MemberInstrument, type Mentor, type Option } from './membersApi';

// The member admin tools (React track 018; were the Blazor /members/manage). Admin and Owner; Owner also deletes
// regular members and, with the current Magister, changes complete dates. The server decides and validates
// everything (MemberAdminService); hiding a button here is only a convenience.

const CATEGORIES = [
  { value: 'Leitao', label: 'Leitão' },
  { value: 'Caloiro', label: 'Caloiro' },
  { value: 'Tuno', label: 'Tuno' },
];
const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
const MIN_YEAR = 1990;
const LOCKED_HINT = 'Data fechada: só o Owner ou o Magister deste ano a corrigem.';

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Este membro já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** As UsernameHelper.NormalizeUsername: lower case, no accents, letters and digits only. */
export const username = (nickname: string) =>
  nickname
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]/g, '');

type Form = MemberInput & { mentorName: string | null };

const blank = (): Form => ({
  firstName: '',
  lastName: '',
  nickname: '',
  phoneNumber: '',
  email: '',
  city: '',
  degree: '',
  dateOfBirth: null,
  category: '',
  fundador: false,
  honorario: false,
  noNickname: false,
  yearLeitao: null,
  monthLeitao: null,
  yearCaloiro: null,
  monthCaloiro: null,
  yearTuno: null,
  monthTuno: null,
  mentorId: null,
  mentorName: null,
  instruments: null,
});

const fromEdit = ({ id: _, instruments: __, lockedDates: ___, ...m }: MemberEdit): Form => ({ ...m, noNickname: false, instruments: null });

/** "Adicionar membro" / "Editar membro": the old modal's fields, rules and dependent fields. */
export function MemberFormDialog({
  memberId,
  instrumentOptions,
  onClose,
  onSaved,
}: {
  memberId?: string;
  instrumentOptions: Option[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const creating = memberId === undefined;
  const [form, setForm] = useState<Form | 'failed' | undefined>(creating ? blank() : undefined);
  const [locked, setLocked] = useState({ leitao: false, caloiro: false, tuno: false });
  // Creating: kept here and sent with the form. Editing: saved one by one, as before.
  const [instruments, setInstruments] = useState<MemberInstrument[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = {
    first: useId(),
    last: useId(),
    nick: useId(),
    phone: useId(),
    email: useId(),
    birth: useId(),
    degree: useId(),
    city: useId(),
    category: useId(),
    noNick: useId(),
    fundador: useId(),
    honorario: useId(),
  };

  useEffect(() => {
    if (creating) return;
    memberAdminApi.edit(memberId).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      setForm(fromEdit(o.data));
      setLocked(o.data.lockedDates);
      setInstruments(o.data.instruments);
    });
  }, [creating, memberId]);

  if (form === undefined || form === 'failed') {
    return (
      <Dialog title="Editar membro" size="lg" onClose={onClose}>
        {form === undefined ? <Loading label="A carregar o membro…" /> : <p className="form__banner">Não foi possível abrir este membro para editar.</p>}
      </Dialog>
    );
  }

  const set = (next: Partial<Form>) => setForm({ ...form, ...next });
  const emailPrefix = (email: string | null) => (email ?? '').split('@')[0];

  const setCategory = (category: string) =>
    set({
      category,
      ...(category !== 'Tuno' && { fundador: false, honorario: false }),
      ...(category !== 'Leitao' && { noNickname: false }),
    });
  const setFundador = (on: boolean) =>
    set(
      on
        ? { fundador: true, honorario: false, yearTuno: 1991, monthTuno: 12, yearLeitao: null, monthLeitao: null, yearCaloiro: null, monthCaloiro: null, mentorId: null, mentorName: null }
        : { fundador: false, yearTuno: null, monthTuno: null },
    );
  const setHonorario = (on: boolean) =>
    set(on ? { honorario: true, fundador: false, yearLeitao: null, monthLeitao: null, yearCaloiro: null, monthCaloiro: null, mentorId: null, mentorName: null } : { honorario: false });

  const special = form.fundador || form.honorario;
  const show = {
    leitao: form.category !== '' && !special,
    caloiro: (form.category === 'Caloiro' || form.category === 'Tuno') && !special,
    tuno: form.category === 'Tuno' && !form.fundador,
    mentor: (form.category === 'Caloiro' || form.category === 'Tuno') && !special,
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const { mentorName: _, ...input } = form;
    if (creating) input.instruments = instruments.map((i) => ({ instrument: i.instrument, primary: i.primary }));
    const o = creating ? await memberAdminApi.create(input) : await memberAdminApi.update(memberId, input);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved();
      onClose();
    } else if (o.kind === 'invalid') {
      setErrors(o.errors);
    } else {
      setErrors({});
      setBanner(problem(o, 'guardar'));
    }
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  const text = (id: string, key: 'firstName' | 'lastName' | 'phoneNumber' | 'city' | 'degree', label: string, max?: number) => (
    <div className={field(key)}>
      <label htmlFor={id}>{label}</label>
      <input id={id} type="text" maxLength={max} value={form[key] ?? ''} onChange={(e) => set({ [key]: e.target.value })} aria-invalid={errors[key] ? true : undefined} />
      {error(key)}
    </div>
  );

  return (
    <Dialog
      title={creating ? 'Adicionar membro' : 'Editar membro'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${ids.first}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${ids.first}-form`} className="form member-form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        <div className="form__row">
          {text(ids.first, 'firstName', 'Primeiro nome', 80)}
          {text(ids.last, 'lastName', 'Último nome', 80)}
        </div>
        <div className="form__row">
          <div className={field('nickname')}>
            <label htmlFor={ids.nick}>Nome de tuna</label>
            <input
              id={ids.nick}
              type="text"
              maxLength={80}
              value={form.nickname ?? ''}
              disabled={form.noNickname}
              onChange={(e) => set({ nickname: e.target.value })}
              aria-invalid={errors.nickname ? true : undefined}
            />
            {form.noNickname && <p className="form__hint">Segue o email automaticamente.</p>}
            {error('nickname')}
          </div>
          {text(ids.phone, 'phoneNumber', 'Contacto', 80)}
        </div>
        <div className="form__row">
          <div className={field('email')}>
            <label htmlFor={ids.email}>Email</label>
            <input
              id={ids.email}
              type="email"
              value={form.email ?? ''}
              onChange={(e) => set({ email: e.target.value, ...(form.noNickname && { nickname: emailPrefix(e.target.value) }) })}
              aria-invalid={errors.email ? true : undefined}
            />
            {error('email')}
          </div>
          <div className={field('dateOfBirth')}>
            <label htmlFor={ids.birth}>Data de nascimento</label>
            <input id={ids.birth} type="date" value={form.dateOfBirth ?? ''} onChange={(e) => set({ dateOfBirth: e.target.value || null })} />
            {error('dateOfBirth')}
          </div>
        </div>
        <div className="form__row">
          {text(ids.degree, 'degree', 'Curso')}
          {text(ids.city, 'city', 'Cidade', 100)}
        </div>

        <div className={field('category')}>
          <label htmlFor={ids.category}>Categoria</label>
          <span className="control control--select">
            <select id={ids.category} value={form.category} onChange={(e) => setCategory(e.target.value)}>
              <option value="">Escolher categoria</option>
              {CATEGORIES.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>
          </span>
          {error('category')}
        </div>

        {creating && form.category === 'Leitao' && (
          <label className="form__check" htmlFor={ids.noNick}>
            <input
              id={ids.noNick}
              type="checkbox"
              checked={form.noNickname}
              onChange={(e) => set({ noNickname: e.target.checked, ...(e.target.checked && { nickname: emailPrefix(form.email) }) })}
            />
            Sem alcunha (recém-entrada): o username vem do email
          </label>
        )}
        {form.category === 'Tuno' && (
          <>
            <label className="form__check" htmlFor={ids.fundador}>
              <input id={ids.fundador} type="checkbox" checked={form.fundador} onChange={(e) => setFundador(e.target.checked)} />
              Fundador
            </label>
            <label className="form__check" htmlFor={ids.honorario}>
              <input id={ids.honorario} type="checkbox" checked={form.honorario} onChange={(e) => setHonorario(e.target.checked)} />
              Tuno Honorário
            </label>
          </>
        )}

        {(show.leitao || show.caloiro || show.tuno) && (
          <div className="member-form__dates">
            {show.leitao && (
              <MonthYear
                label="Entrada a Leitão"
                year={form.yearLeitao}
                month={form.monthLeitao}
                locked={!creating && locked.leitao}
                error={errors.leitao}
                onChange={(yearLeitao, monthLeitao) => set({ yearLeitao, monthLeitao })}
              />
            )}
            {show.caloiro && (
              <MonthYear
                label="Passagem a Caloiro"
                year={form.yearCaloiro}
                month={form.monthCaloiro}
                locked={!creating && locked.caloiro}
                error={errors.caloiro}
                onChange={(yearCaloiro, monthCaloiro) => set({ yearCaloiro, monthCaloiro })}
              />
            )}
            {show.tuno && (
              <MonthYear
                label="Passagem a Tuno"
                year={form.yearTuno}
                month={form.monthTuno}
                locked={!creating && locked.tuno}
                error={errors.tuno}
                onChange={(yearTuno, monthTuno) => set({ yearTuno, monthTuno })}
              />
            )}
          </div>
        )}

        <Instruments
          memberId={memberId}
          options={instrumentOptions}
          list={instruments}
          error={errors.instruments}
          onChange={setInstruments}
        />

        {show.mentor && (
          <MentorPicker
            excludeId={memberId}
            mentorId={form.mentorId}
            mentorName={form.mentorName}
            error={errors.mentorId}
            onChange={(mentorId, mentorName) => set({ mentorId, mentorName })}
          />
        )}
      </form>
    </Dialog>
  );
}

export function MonthYear({
  label,
  year,
  month,
  locked,
  lockedHint = LOCKED_HINT,
  error,
  onChange,
}: {
  label: string;
  year: number | null;
  month: number | null;
  locked: boolean;
  lockedHint?: string;
  error?: string;
  onChange: (year: number | null, month: number | null) => void;
}) {
  const id = useId();
  const years: number[] = [];
  for (let y = new Date().getFullYear(); y >= MIN_YEAR; y--) years.push(y);
  // As the old picker: choosing one half fills the other (January / this year), clearing one clears both.
  const pickMonth = (m: string) => (m ? onChange(year ?? new Date().getFullYear(), Number(m)) : onChange(null, null));
  const pickYear = (y: string) => (y ? onChange(Number(y), month ?? 1) : onChange(null, null));

  return (
    <fieldset className={error ? 'form__field form__field--error member-form__date' : 'form__field member-form__date'} disabled={locked}>
      <legend className="form__label">{label}</legend>
      <span className="member-form__pair">
        <span className="control control--select">
          <label className="sr-only" htmlFor={`${id}-m`}>
            Mês
          </label>
          <select id={`${id}-m`} value={month ?? ''} onChange={(e) => pickMonth(e.target.value)}>
            <option value="">Mês</option>
            {MONTHS.map((m, i) => (
              <option key={m} value={i + 1}>
                {m}
              </option>
            ))}
          </select>
        </span>
        <span className="control control--select">
          <label className="sr-only" htmlFor={`${id}-y`}>
            Ano
          </label>
          <select id={`${id}-y`} value={year ?? ''} onChange={(e) => pickYear(e.target.value)}>
            <option value="">Ano</option>
            {year !== null && !years.includes(year) && <option value={year}>{year}</option>}
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </span>
      </span>
      {locked && <p className="form__hint">{lockedHint}</p>}
      {error && <p className="form__error">{error}</p>}
    </fieldset>
  );
}

function Instruments({
  memberId,
  options,
  list,
  error,
  onChange,
}: {
  memberId?: string;
  options: Option[];
  list: MemberInstrument[];
  error?: string;
  onChange: (list: MemberInstrument[]) => void;
}) {
  const id = useId();
  const [picked, setPicked] = useState('');
  const [problemText, setProblemText] = useState<string>();
  const [busy, setBusy] = useState(false);
  const available = options.filter((o) => !list.some((i) => i.instrument === o.value));

  // An existing member's instruments are saved at once, as before; a new member's go with the form.
  const run = async (action: () => Promise<Outcome<MemberInstrument[]>>) => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data);
      setProblemText(undefined);
    } else setProblemText(problem(o, 'guardar o instrumento'));
  };

  const add = () => {
    if (!picked) return;
    if (memberId) run(() => memberAdminApi.addInstrument(memberId, picked));
    else onChange([...list, { id: -Date.now(), instrument: picked, label: options.find((o) => o.value === picked)?.label ?? picked, primary: list.length === 0 }]);
    setPicked('');
  };
  const remove = (i: MemberInstrument) => {
    if (memberId) return run(() => memberAdminApi.removeInstrument(memberId, i.id));
    const rest = list.filter((x) => x.id !== i.id);
    onChange(i.primary && rest.length > 0 ? rest.map((x, n) => ({ ...x, primary: n === 0 })) : rest);
  };
  const primary = (i: MemberInstrument) => {
    if (memberId) return run(() => memberAdminApi.primaryInstrument(memberId, i.id));
    onChange(list.map((x) => ({ ...x, primary: x.id === i.id })));
  };

  const shown = problemText ?? error;
  return (
    <div className={shown ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={id}>Instrumentos</label>
      <span className="member-form__pair">
        <span className="control control--select">
          <select id={id} value={picked} onChange={(e) => setPicked(e.target.value)} disabled={busy}>
            <option value="">Escolher instrumento…</option>
            {available.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </span>
        <button type="button" className="btn btn--ghost btn--sm" onClick={add} disabled={!picked || busy}>
          <Icon name="plus" />
          Adicionar
        </button>
      </span>
      {list.length > 0 ? (
        <ul className="member-rows member-instruments">
          {list.map((i) => (
            <li key={i.id} className="member-row">
              <label className="form__check">
                <input type="radio" name={`${id}-primary`} checked={i.primary} onChange={() => primary(i)} disabled={busy} />
                {i.label}
                {i.primary && <span className="member-badge member-badge--active">Principal</span>}
              </label>
              <button type="button" className="icon-btn icon-btn--danger" onClick={() => remove(i)} disabled={busy}>
                <Icon name="trash" />
                <span className="sr-only">Remover {i.label}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : (
        memberId && <p className="note">Nenhum instrumento adicionado ainda.</p>
      )}
      {shown && <p className="form__error">{shown}</p>}
    </div>
  );
}

/** A stable function, so the search effect below does not re-run on every render. */
const adminMentorSearch = (q: string, exclude?: string) => memberAdminApi.mentors(q, exclude);

export function MentorPicker({
  excludeId,
  mentorId,
  mentorName,
  error,
  onChange,
  search = adminMentorSearch,
}: {
  excludeId?: string;
  mentorId: string | null;
  mentorName: string | null;
  error?: string;
  onChange: (id: string | null, name: string | null) => void;
  /** Admin/Owner by default; the member's own profile passes its own search (task 032). */
  search?: (q: string, exclude?: string) => Promise<Outcome<Mentor[]>>;
}) {
  const id = useId();
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Mentor[]>();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => {
      search(query.trim(), excludeId).then((o) => setFound(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => clearTimeout(timer);
  }, [query, excludeId, search]);

  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={id}>Padrinho</label>
      {mentorId && (
        <p className="member-form__picked">
          <span>
            Padrinho escolhido: <strong>{mentorName ?? 'membro'}</strong>
          </span>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onChange(null, null)}>
            Tirar
          </button>
        </p>
      )}
      <span className="control">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar padrinho por nome ou alcunha" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {found && found.length === 0 && <p className="note">Nenhum padrinho encontrado.</p>}
      {found && found.length > 0 && (
        <ul className="member-rows member-form__mentors">
          {found.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                className="member-row member-row--button"
                onClick={() => {
                  onChange(m.id, m.displayName);
                  setQuery('');
                }}
              >
                <MemberFace avatarUrl={m.avatarUrl} size={36} />
                <span className="member-row__who">
                  <strong>{m.displayName}</strong>
                  {m.fullName && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}

/** "Definir alcunha" for a Leitão: the new nickname and the username it gives; the member gets an email. */
export function NicknameDialog({ member, onClose, onDone }: { member: { id: string; displayName: string }; onClose: () => void; onDone: () => void }) {
  const id = useId();
  const [nickname, setNickname] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!nickname.trim()) return setError('O nome de tuna é obrigatório');
    setBusy(true);
    const o = await memberAdminApi.nickname(member.id, nickname);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(problem(o, 'guardar a alcunha'));
  };

  return (
    <Dialog
      title="Definir alcunha"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <p>
          Leitão: <strong>{member.displayName}</strong>
        </p>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={id}>Nome de tuna</label>
          <input id={id} type="text" maxLength={80} value={nickname} onChange={(e) => setNickname(e.target.value)} aria-invalid={error ? true : undefined} />
          <p className="form__hint">O username passa a ser gerado a partir da alcunha e o membro recebe um email com o novo.</p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
        {username(nickname) && (
          <p className="note">
            Novo username: <strong>{username(nickname)}</strong>
          </p>
        )}
      </form>
    </Dialog>
  );
}

/** One confirmation for expel, reactivate, delete, "Tornar ativo" and the push reminder. */
export function ConfirmAction({
  title,
  confirmLabel,
  danger,
  run,
  onClose,
  onDone,
  children,
}: {
  title: string;
  confirmLabel: string;
  danger?: boolean;
  run: () => Promise<Outcome<unknown>>;
  onClose: () => void;
  onDone: () => void;
  children: ReactNode;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    const o = await run();
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(problem(o, title.toLowerCase()));
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
          <button type="button" className={danger ? 'btn btn--danger' : 'btn btn--primary'} onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {confirmLabel}
          </button>
        </>
      }
    >
      {children}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
