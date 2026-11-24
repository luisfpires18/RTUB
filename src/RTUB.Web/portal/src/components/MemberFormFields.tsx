// Member form field pieces: month/year picker, instruments editor, mentor picker.
import { useEffect, useId, useState } from 'react';
import { EmptyNote } from './ui/EmptyNote';
import { FieldError } from './ui/FieldError';
import type { Outcome } from '../data/api';
import { Icon } from './icons';
import { memberAdminApi, type MemberInstrument, type Mentor } from '../data/membersApi';
import { MemberBadge } from './MemberBadge';
import { LOCKED_HINT, MIN_YEAR, MONTHS, problem } from '../data/memberFormState';
import { Avatar } from './ui/Avatar';
import type { Option } from '../data/sharedTypes';
import { IconAction } from './ui/IconAction';
import { SelectField } from './ui/SearchTools';

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
  // Choosing one half fills the other (January / this year), clearing one clears both.
  const pickMonth = (m: string) => (m ? onChange(year ?? new Date().getFullYear(), Number(m)) : onChange(null, null));
  const pickYear = (y: string) => (y ? onChange(Number(y), month ?? 1) : onChange(null, null));

  return (
    <fieldset className={error ? 'form__field form__field--error member-form__date' : 'form__field member-form__date'} disabled={locked}>
      <legend className="form__label">{label}</legend>
      <span className="member-form__pair">
        <SelectField id={`${id}-m`} aria-label="Mês" value={month ?? ''} onChange={pickMonth}>
          <option value="">Mês</option>
          {MONTHS.map((m, i) => (
            <option key={m} value={i + 1}>
              {m}
            </option>
          ))}
        </SelectField>
        <SelectField id={`${id}-y`} aria-label="Ano" value={year ?? ''} onChange={pickYear}>
          <option value="">Ano</option>
          {year !== null && !years.includes(year) && <option value={year}>{year}</option>}
          {years.map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </SelectField>
      </span>
      {locked && <p className="form__hint">{lockedHint}</p>}
      <FieldError text={error} alert={false} />
    </fieldset>
  );
}

export function Instruments({
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

  // An existing member's instruments are saved at once; a new member's go with the form.
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
        <SelectField id={id} value={picked} onChange={setPicked} disabled={busy}>
          <option value="">Escolher instrumento…</option>
          {available.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </SelectField>
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
                {i.primary && <MemberBadge kind="active">Principal</MemberBadge>}
              </label>
              <IconAction icon="trash" label={`Remover ${i.label}`} onClick={() => remove(i)} large danger disabled={busy} />
            </li>
          ))}
        </ul>
      ) : (
        memberId && <EmptyNote>Nenhum instrumento adicionado ainda.</EmptyNote>
      )}
      <FieldError text={shown} alert={false} />
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
  /** Admin/Owner by default; the member's own profile passes its own search. */
  search?: (q: string, exclude?: string) => Promise<Outcome<Mentor[]>>;
}) {
  const id = useId();
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Mentor[]>();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      search(query.trim(), excludeId).then((o) => live && setFound(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
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
      {found && found.length === 0 && <EmptyNote>Nenhum padrinho encontrado.</EmptyNote>}
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
                <Avatar className="who__avatar" src={m.avatarUrl} size={36} />
                <span className="member-row__who">
                  <strong>{m.displayName}</strong>
                  {m.fullName && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <FieldError text={error} alert={false} />
    </div>
  );
}
