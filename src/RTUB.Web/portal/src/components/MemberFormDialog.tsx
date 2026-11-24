// The create/edit member dialog (Members admin).
import { DialogFooter } from './ui/DialogFooter';
import { FieldError } from './ui/FieldError';
import { CheckField } from './ui/CheckField';
import { Banner } from './ui/Banner';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { memberAdminApi, type MemberInstrument } from '../data/membersApi';
import { CATEGORIES, type Form, blank, fromEdit, problem } from '../data/memberFormState';
import { Instruments, MentorPicker, MonthYear } from './MemberFormFields';
import { Loading } from './ui/Loading';
import type { Option } from '../data/sharedTypes';
import { SelectField } from './ui/SearchTools';

/** "Adicionar membro" / "Editar membro": the member's fields, rules and dependent fields. */
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
  // Creating: kept here and sent with the form. Editing: saved one by one.
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
        {form === undefined ? <Loading label="A carregar o membro…" /> : <Banner role="none" text="Não foi possível abrir este membro para editar." />}
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
    <FieldError text={errors[key]} />;
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
      footer={<DialogFooter form={`${ids.first}-form`} busy={busy} onClose={onClose} />}
    >
      <form id={`${ids.first}-form`} className="form member-form" onSubmit={submit} noValidate>
        <Banner text={banner} />
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
          <SelectField id={ids.category} value={form.category} onChange={setCategory}>
            <option value="">Escolher categoria</option>
            {CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </SelectField>
          {error('category')}
        </div>

        {creating && form.category === 'Leitao' && (
          <CheckField id={ids.noNick} checked={form.noNickname} onChange={(c) => set({ noNickname: c, ...(c && { nickname: emailPrefix(form.email) }) })}>
            Sem alcunha (recém-entrada): o username vem do email
          </CheckField>
        )}
        {form.category === 'Tuno' && (
          <>
            <CheckField id={ids.fundador} checked={form.fundador} onChange={setFundador}>
              Fundador
            </CheckField>
            <CheckField id={ids.honorario} checked={form.honorario} onChange={setHonorario}>
              Tuno Honorário
            </CheckField>
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
