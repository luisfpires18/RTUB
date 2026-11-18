// The member info dialog (Members admin).
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { memberAdminApi, type MemberInfo, type MemberInfoInput, type MemberInstrument, type Option } from '../data/membersApi';
import { Instruments, MentorPicker, MonthYear } from './MemberFormFields';
import { problem } from '../data/memberFormState';
import { Loading } from './ui/Loading';

/**
 * "Completar informação" - the limited edit Mod, Admin and Owner share: Cidade, Contacto, instruments, the
 * missing Leitão / Caloiro / Tuno dates and the Padrinho, only the fields that apply to this member (the server's rule),
 * the empty ones marked. A complete date pair stays locked unless the caller is Owner or the Magister. Saving over a
 * newer version is refused and asks to open again; errors keep what was typed.
 */
export function MemberInfoDialog({
  memberId,
  instrumentOptions,
  onClose,
  onSaved,
}: {
  memberId: string;
  instrumentOptions: Option[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [info, setInfo] = useState<MemberInfo | 'missing' | null>();
  const [form, setForm] = useState<MemberInfoInput & { mentorName: string | null }>();
  const [instruments, setInstruments] = useState<MemberInstrument[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { city: useId(), phone: useId(), form: useId() };

  useEffect(() => {
    memberAdminApi.info(memberId).then((o) => {
      if (o.kind !== 'ok') return setInfo(o.kind === 'notfound' || o.kind === 'forbidden' ? 'missing' : null);
      setInfo(o.data);
      const { id: _id, displayName: _d, instruments: list, fields: _f, missing: _m, lockedDates: _l, ...values } = o.data;
      setForm(values);
      setInstruments(list);
    });
  }, [memberId]);

  if (info === undefined || (info && info !== 'missing' && !form)) {
    return (
      <Dialog title="Completar informação" onClose={onClose}>
        <Loading label="A carregar…" />
      </Dialog>
    );
  }

  if (info === null || info === 'missing' || !form) {
    return (
      <Dialog title="Completar informação" onClose={onClose}>
        <p className="form__banner" role="alert">
          {info === 'missing' ? 'Este membro não está disponível para esta edição.' : 'Não foi possível abrir. Tenta de novo.'}
        </p>
      </Dialog>
    );
  }

  const has = (f: string) => info.fields.includes(f);
  const gap = (f: string) => info.missing.includes(f);
  const set = (patch: Partial<typeof form>) => setForm({ ...form, ...patch });
  const label = (text: string, f: string) => (gap(f) ? `${text} · em falta` : text);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const { mentorName: _, ...input } = form;
    const o = await memberAdminApi.saveInfo(memberId, input);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved();
      onClose();
    } else if (o.kind === 'invalid') {
      setErrors(o.errors);
    } else if (o.kind === 'conflict' || o.kind === 'closed') {
      setBanner('Os dados deste membro mudaram entretanto. Fecha e abre de novo para ver a versão atual.');
    } else {
      setErrors({});
      setBanner(problem(o, 'guardar'));
    }
  };

  const textField = (id: string, key: 'city' | 'phoneNumber', f: string, text: string, max: number) => (
    <div className={errors[f] ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={id}>{label(text, f)}</label>
      <input id={id} type="text" maxLength={max} value={form[key] ?? ''} onChange={(e) => set({ [key]: e.target.value })} aria-invalid={errors[f] ? true : undefined} />
      {errors[f] && (
        <p className="form__error" role="alert">
          {errors[f]}
        </p>
      )}
    </div>
  );

  return (
    <Dialog
      title={`Completar informação · ${info.displayName}`}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${ids.form}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${ids.form}-form`} className="form member-form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        {info.missing.length > 0 && (
          <p className="note">Em falta: {info.missing.map((f) => INFO_LABELS[f] ?? f).join(', ')}.</p>
        )}
        <div className="form__row">
          {textField(ids.city, 'city', 'city', 'Cidade', 100)}
          {textField(ids.phone, 'phoneNumber', 'phone', 'Contacto', 80)}
        </div>
        {(has('leitao') || has('caloiro') || has('tuno')) && (
          <div className="member-form__dates">
            {has('leitao') && (
              <MonthYear
                label={label('Entrada na Tuna', 'leitao')}
                year={form.yearLeitao}
                month={form.monthLeitao}
                locked={info.lockedDates.leitao}
                lockedHint={INFO_LOCKED_HINT}
                error={errors.leitao}
                onChange={(yearLeitao, monthLeitao) => set({ yearLeitao, monthLeitao })}
              />
            )}
            {has('caloiro') && (
              <MonthYear
                label={label('Passagem a Caloiro', 'caloiro')}
                year={form.yearCaloiro}
                month={form.monthCaloiro}
                locked={info.lockedDates.caloiro}
                lockedHint={INFO_LOCKED_HINT}
                error={errors.caloiro}
                onChange={(yearCaloiro, monthCaloiro) => set({ yearCaloiro, monthCaloiro })}
              />
            )}
            {has('tuno') && (
              <MonthYear
                label={label('Passagem a Tuno', 'tuno')}
                year={form.yearTuno}
                month={form.monthTuno}
                locked={info.lockedDates.tuno}
                lockedHint={INFO_LOCKED_HINT}
                error={errors.tuno}
                onChange={(yearTuno, monthTuno) => set({ yearTuno, monthTuno })}
              />
            )}
          </div>
        )}
        {has('instrument') && (
          <Instruments memberId={memberId} options={instrumentOptions} list={instruments} error={errors.instrument} onChange={setInstruments} />
        )}
        {has('mentor') && (
          <MentorPicker
            excludeId={memberId}
            mentorId={form.mentorId}
            mentorName={form.mentorName}
            error={errors.mentor}
            onChange={(mentorId, mentorName) => set({ mentorId, mentorName })}
          />
        )}
      </form>
    </Dialog>
  );
}

/** The lock explained without naming who may change it (the Magister needs more than the position). */
const INFO_LOCKED_HINT = 'Data fechada: não a podes alterar aqui.';

const INFO_LABELS: Record<string, string> = {
  city: 'Cidade',
  phone: 'Contacto',
  instrument: 'Instrumento',
  leitao: 'Entrada na Tuna',
  caloiro: 'Passagem a Caloiro',
  tuno: 'Passagem a Tuno',
  mentor: 'Padrinho',
};
