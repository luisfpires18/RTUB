// The create / edit gathering dialog of /trintuna. There is no free date field: "Marcar data" picks one of the
// gathering's own saved poll options (POST /settle), so the date always matches an option.
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { trintunaApi, problem, MAX_PLACE, MAX_DESCRIPTION, MAX_POLL_DATES, type Gathering, type Member } from '../data/trintunaApi';
import { Banner } from './ui/Banner';
import { DialogFooter } from './ui/DialogFooter';
import { FormField, fieldProps } from './ui/FormField';
import { PersonChip } from './ui/PersonChip';
import { MemberPicker } from './LogisticsFields';
import { SelectField } from './ui/SearchTools';

const ORGANIZER_HINT = 'Escolhido entre quem tem acesso à página.';

/**
 * "Novo almoço" / "Editar almoço". The server numbers a new gathering (highest + 1); nobody types it. The Owner sets
 * the organizer (an Owner or a member on the access list); the organizer edits place, description and dates. Once the
 * date is set, the dates no longer change.
 */
export function GatheringForm({
  gathering: g,
  canManage,
  onClose,
  onSaved,
}: {
  gathering?: Gathering;
  canManage: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [place, setPlace] = useState(g?.place ?? '');
  const [description, setDescription] = useState(g?.description ?? '');
  const [organizer, setOrganizer] = useState(g?.organizerUserId ?? '');
  const [dates, setDates] = useState<string[]>(g ? g.poll.options.map((o) => o.date) : []);
  const [settle, setSettle] = useState('');
  const [members, setMembers] = useState<Member[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const id = (k: string) => `${fid}-${k}`;
  const closed = Boolean(g?.poll.closed);
  // "Marcar data" only offers saved options still in the date list: removing or changing the chosen date clears it.
  const settleOptions = g ? g.poll.options.filter((o) => dates.includes(o.date)) : [];
  const settleDate = settleOptions.some((o) => o.date === settle) ? settle : '';

  // The organizer list: the Owner(s) and the access list (Owner only, GET /organizers).
  useEffect(() => {
    if (!canManage) return;
    trintunaApi.organizers().then((o) => o.kind === 'ok' && setMembers(o.data));
  }, [canManage]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErrors({});
    const input = { place, description, organizerUserId: organizer || null, pollDates: dates.filter(Boolean) };
    const saved = g ? await trintunaApi.update(g.id, input) : await trintunaApi.create(input);
    if (saved.kind !== 'ok') {
      setBusy(false);
      return setErrors(saved.kind === 'invalid' ? saved.errors : { form: problem(saved, 'guardar o almoço') });
    }

    // "Marcar data": the saved gathering's option for the chosen date. If it is gone, say so and keep the form open.
    const option = settleDate ? saved.data.poll.options.find((o) => o.date === settleDate) : undefined;
    const settled = option ? await trintunaApi.settle(saved.data.id, option.id) : undefined;
    setBusy(false);
    onSaved();
    if (settleDate && !option) return setErrors({ settle: 'A data escolhida já não está nas datas possíveis. Escolha outra.' });
    if (settled && settled.kind !== 'ok') return setErrors({ settle: problem(settled, 'marcar a data') });
    onClose();
  };

  const stored = g?.organizer;
  const organizers = useMemo(
    () =>
      members.some((m) => m.id === organizer) || !stored || !organizer
        ? members
        : [{ id: organizer, displayName: stored.displayName, avatarUrl: stored.avatarUrl }, ...members],
    [members, organizer, stored],
  );
  const chosen = organizers.find((m) => m.id === organizer);
  // Stable between renders, so the picker's search effect does not run again on every render.
  const pickable = useMemo(() => organizers.map((m) => ({ id: m.id, displayName: m.displayName, fullName: null, avatarUrl: m.avatarUrl })), [organizers]);

  return (
    <Dialog
      title={g ? `Editar ${g.title}` : 'Novo almoço'}
      size="lg"
      onClose={onClose}
      footer={<DialogFooter form={id('form')} busy={busy} disabled={!place.trim()} onClose={onClose} />}
    >
      <form id={id('form')} className="form" onSubmit={submit} noValidate>
        <Banner text={errors.form} />
        <FormField id={id('place')} label="Local" error={errors.place}>
          <input {...fieldProps(id('place'), errors.place)} type="text" maxLength={MAX_PLACE} value={place} onChange={(e) => setPlace(e.target.value)} />
        </FormField>
        <FormField id={id('desc')} label="Descrição" error={errors.description}>
          <textarea {...fieldProps(id('desc'), errors.description)} rows={3} maxLength={MAX_DESCRIPTION} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
        {canManage ? (
          <FormField id={id('organizer')} label="Organizador" error={errors.organizerUserId} hint={ORGANIZER_HINT}>
            {chosen ? (
              <div className="chips">
                <PersonChip name={chosen.displayName} avatar={chosen.avatarUrl} onRemove={() => setOrganizer('')} removeLabel="Escolher outro organizador" />
              </div>
            ) : (
              <MemberPicker
                label="Organizador"
                options={pickable}
                onPick={(m) => setOrganizer(m.id)}
              />
            )}
          </FormField>
        ) : (
          g?.organizer && (
            <FormField id={id('organizer')} label="Organizador" heading>
              <PersonChip readOnly name={g.organizer.displayName} avatar={g.organizer.avatarUrl} />
            </FormField>
          )
        )}
        <fieldset className="mtg-poll__create-options" disabled={closed}>
          <legend>Datas possíveis (votação)</legend>
          {closed && <p className="note">A data já está marcada: as datas possíveis já não mudam.</p>}
          {dates.map((date, index) => (
            <div className="mtg-poll__option-input" key={index}>
              <label className="form__field">
                <span className="sr-only">Data {index + 1}</span>
                <input type="date" value={date} onChange={(e) => setDates((d) => d.map((x, i) => (i === index ? e.target.value : x)))} aria-label={`Data ${index + 1}`} />
              </label>
              <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDates((d) => d.filter((_, i) => i !== index))} aria-label={`Remover data ${index + 1}`}>
                <Icon name="trash" />
              </button>
            </div>
          ))}
          {!closed && dates.length < MAX_POLL_DATES && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDates((d) => [...d, ''])}>
              <Icon name="plus" />
              Adicionar data
            </button>
          )}
          {errors.pollDates && <p className="form__error">{errors.pollDates}</p>}
        </fieldset>
        {g && !closed && settleOptions.length > 0 && (
          <FormField id={id('settle')} label="Marcar data" error={errors.settle ?? errors.optionId} hint="Uma das datas possíveis já guardadas. Depois de marcada, a votação fecha.">
            <SelectField {...fieldProps(id('settle'), errors.settle ?? errors.optionId)} value={settleDate} onChange={setSettle}>
              <option value="">Ainda não</option>
              {settleOptions.map((o) => (
                <option key={o.id} value={o.date}>
                  {o.text} ({o.votes} {o.votes === 1 ? 'voto' : 'votos'})
                </option>
              ))}
            </SelectField>
          </FormField>
        )}
      </form>
    </Dialog>
  );
}
