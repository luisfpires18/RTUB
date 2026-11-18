// The create / edit meeting dialog of /meetings.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingBoard, MeetingCard, MeetingDraft, MeetingForm, MeetingOption } from '../data/meetingsTypes';
import { localDateTime } from '../data/meetingsDates';
import { MAX_TITLE, MAX_LOCATION, MAX_STATEMENT, problem, type Errors, fieldClass } from './MeetingUi';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';
import { Spinner } from './ui/Spinner';

const TYPE_LABELS: Record<string, string> = {
  AssembleiaGeralOrdinaria: 'Assembleia Geral Ordinária',
  AssembleiaGeralExtraordinaria: 'Assembleia Geral Extraordinária',
  ConselhoVeteranos: 'Conselho de Veteranos',
  ReuniaoDirecao: 'Reunião de Direção',
};

// The default type: AGO when allowed, else CV, else AGE.
const defaultType = (types: MeetingOption[]) =>
  ['AssembleiaGeralOrdinaria', 'ConselhoVeteranos', 'AssembleiaGeralExtraordinaria'].find((t) => types.some((o) => o.value === t)) ?? types[0]?.value ?? '';

/** "Nova Reunião" (also prefilled from an accepted request) and "Editar Reunião". */
export function MeetingFormDialog({
  card,
  draft,
  board,
  onClose,
  onSaved,
}: {
  card?: MeetingCard;
  draft?: MeetingDraft;
  board: MeetingBoard;
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const editing = Boolean(card);
  const [form, setForm] = useState<MeetingForm | null>();
  const [type, setType] = useState(card?.type ?? draft?.type ?? '');
  const [title, setTitle] = useState(card?.title ?? draft?.title ?? '');
  const [date, setDate] = useState(card?.date ?? (draft?.date || localDateTime(1, 20))); // default: tomorrow at 20:00, also for a ?new= draft
  const [place, setPlace] = useState(card?.location ?? draft?.location ?? '');
  const [statement, setStatement] = useState(card?.statement ?? draft?.statement ?? '');
  const [representative, setRepresentative] = useState(card?.tunoRepresentativeId ?? '');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.form().then((o) => {
      if (o.kind !== 'ok') {
        setForm(null);
        setBanner(problem(o, 'abrir o formulário'));
        return;
      }
      setForm(o.data);
      setType((t) => t || defaultType(o.data.types.length ? o.data.types : board.access.meetingTypes));
    });
  }, []);

  const types = form?.types ?? [];
  const options = type && !types.some((t) => t.value === type) ? [{ value: type, label: card?.typeLabel ?? TYPE_LABELS[type] ?? type }, ...types] : types;
  const isCouncil = type === 'ConselhoVeteranos';
  const representatives = form?.tunoRepresentatives ?? [];

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const input = {
      type,
      title,
      date,
      location: place,
      statement,
      tunoRepresentativeId: isCouncil && representative ? representative : null,
    };
    const o = card ? await meetingsApi.update(card.id, input) : await meetingsApi.create(input);
    setBusy(false);
    if (o.kind === 'ok') {
      const saved = editing ? 'Reunião atualizada.' : 'Reunião criada.';
      onSaved(o.data.card ? saved : `${saved} Não aparece na sua lista: é de um tipo de reunião que não vê.`);
    } else if (o.kind === 'invalid') {
      setErrors(o.errors);
      setBanner('Há campos por corrigir.');
    } else {
      setErrors({});
      setBanner(problem(o, 'guardar a reunião'));
    }
  };

  return (
    <Dialog
      title={editing ? 'Editar Reunião' : 'Nova Reunião'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !form}>
            {busy && <Spinner small />}
            Guardar
          </button>
        </>
      }
    >
      {form === undefined ? (
        <Loading label="A carregar o formulário…" />
      ) : (
        <form id={`${id}-form`} className="form mtg-form" onSubmit={submit} noValidate>
          <Banner text={banner} />
          {form && (
            <>
              <div className={fieldClass(errors, 'type')}>
                <label htmlFor={`${id}-type`}>Tipo de Reunião</label>
                <span className="control control--select">
                  <select id={`${id}-type`} value={type} onChange={(e) => setType(e.target.value)}>
                    <option value="" disabled>
                      -- Selecionar Tipo --
                    </option>
                    {options.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                </span>
                <FieldError text={errors.type} />
              </div>
              <div className={fieldClass(errors, 'title')}>
                <label htmlFor={`${id}-title`}>Título</label>
                <input id={`${id}-title`} type="text" maxLength={MAX_TITLE} placeholder="Inserir título..." value={title} onChange={(e) => setTitle(e.target.value)} />
                <FieldError text={errors.title} />
              </div>
              <div className={fieldClass(errors, 'date')}>
                <label htmlFor={`${id}-date`}>Data e Hora</label>
                <input id={`${id}-date`} type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
                <FieldError text={errors.date} />
              </div>
              <div className={fieldClass(errors, 'location')}>
                <label htmlFor={`${id}-location`}>Localização (Opcional)</label>
                <input
                  id={`${id}-location`}
                  type="text"
                  maxLength={MAX_LOCATION}
                  placeholder="Inserir localização..."
                  value={place}
                  onChange={(e) => setPlace(e.target.value)}
                />
                <FieldError text={errors.location} />
              </div>
              {isCouncil && (
                <div className={fieldClass(errors, 'tunoRepresentativeId')}>
                  <label htmlFor={`${id}-rep`}>Representante de Tunos (Opcional)</label>
                  <span className="control control--select">
                    <select id={`${id}-rep`} value={representative} onChange={(e) => setRepresentative(e.target.value)}>
                      <option value="">Nenhum representante selecionado</option>
                      {representative && !representatives.some((r) => r.id === representative) && (
                        <option value={representative}>{card?.tunoRepresentative ?? 'Representante atual'}</option>
                      )}
                      {representatives.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.label}
                        </option>
                      ))}
                    </select>
                  </span>
                  <p className="form__hint">Este tuno poderá ver e participar nesta reunião de CV.</p>
                  <FieldError text={errors.tunoRepresentativeId} />
                </div>
              )}
              <div className={fieldClass(errors, 'statement')}>
                <label htmlFor={`${id}-statement`}>Declaração</label>
                <textarea
                  id={`${id}-statement`}
                  rows={10}
                  maxLength={MAX_STATEMENT}
                  placeholder="Inserir declaração..."
                  value={statement}
                  onChange={(e) => setStatement(e.target.value)}
                />
                <p className="form__hint">
                  Máximo {MAX_STATEMENT} caracteres ({statement.length} / {MAX_STATEMENT})
                </p>
                <FieldError text={errors.statement} />
              </div>
            </>
          )}
        </form>
      )}
    </Dialog>
  );
}
