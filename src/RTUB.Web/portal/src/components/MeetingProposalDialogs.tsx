// The meeting proposal and request-details dialogs of /meetings.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingRequest } from '../data/meetingsTypes';
import { localDateTime, numericDateTime } from '../data/meetingsDates';
import { MAX_TITLE, MAX_LOCATION, MAX_DESCRIPTION, problem, type Errors, fieldClass, typeClass, RequestStatusPill, Detail } from './MeetingUi';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';
import { Spinner } from './ui/Spinner';

const PROPOSAL_TITLES: Record<string, string> = {
  ConselhoVeteranos: 'Propor Reunião de CV',
  ReuniaoDirecao: 'Propor Reunião de Direção',
  AssembleiaGeralOrdinaria: 'Propor Assembleia Geral Ordinária',
  AssembleiaGeralExtraordinaria: 'Propor Assembleia Geral',
};

export function ProposalDialog({ type, onClose, onSent }: { type: string; onClose: () => void; onSent: () => void }) {
  const [title, setTitle] = useState('');
  const [date, setDate] = useState(() => localDateTime(7)); // default: a week from now
  const [place, setPlace] = useState('');
  const [description, setDescription] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const o = await meetingsApi.propose({ type, title, proposedDate: date, location: place, description });
    setBusy(false);
    if (o.kind === 'ok') onSent();
    else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.type) setBanner(o.errors.type);
    } else {
      setErrors({});
      setBanner(problem(o, 'enviar a proposta'));
    }
  };

  return (
    <Dialog
      title={PROPOSAL_TITLES[type] ?? 'Propor Reunião'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <Spinner small />}
            {!busy && <Icon name="send" />}
            Enviar Proposta
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form mtg-form" onSubmit={submit} noValidate>
        <Banner text={banner} />
        <div className={fieldClass(errors, 'title')}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input id={`${id}-title`} type="text" maxLength={MAX_TITLE} placeholder="Inserir título..." value={title} onChange={(e) => setTitle(e.target.value)} />
          <FieldError text={errors.title} />
        </div>
        <div className={fieldClass(errors, 'proposedDate')}>
          <label htmlFor={`${id}-date`}>Data e Hora Proposta</label>
          <input id={`${id}-date`} type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
          <FieldError text={errors.proposedDate} />
        </div>
        <div className={fieldClass(errors, 'location')}>
          <label htmlFor={`${id}-location`}>Localização (Opcional)</label>
          <input
            id={`${id}-location`}
            type="text"
            maxLength={MAX_LOCATION}
            placeholder="Ex: Discord, Centro Académico..."
            value={place}
            onChange={(e) => setPlace(e.target.value)}
          />
          <FieldError text={errors.location} />
        </div>
        <div className={fieldClass(errors, 'description')}>
          <label htmlFor={`${id}-description`}>Descrição/Motivo</label>
          <textarea
            id={`${id}-description`}
            rows={10}
            maxLength={MAX_DESCRIPTION}
            placeholder="Descrever o motivo e assuntos a discutir..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <p className="form__hint">
            Máximo {MAX_DESCRIPTION} caracteres ({description.length} / {MAX_DESCRIPTION})
          </p>
          <FieldError text={errors.description} />
        </div>
      </form>
    </Dialog>
  );
}

export function RequestDetailsDialog({ request, onClose }: { request: MeetingRequest; onClose: () => void }) {
  return (
    <Dialog
      title="Detalhes do Pedido de Reunião"
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Voltar Atrás
        </button>
      }
    >
      <div className="mtg-detail">
        <span className={typeClass(request.type)}>{request.typeLabel}</span>
        <h3 className="mtg-detail__title">{request.title}</h3>
        <dl className="mtg-detail__list">
          <Detail icon="person" label="Autor">
            {request.authorName}
          </Detail>
          <Detail icon="calendar" label="Data e Hora Proposta">
            {numericDateTime(request.proposedDate)}
          </Detail>
          {request.location && (
            <Detail icon="geo" label="Localização">
              {request.location}
            </Detail>
          )}
          <Detail icon="file" label="Descrição/Motivo">
            <span className="mtg-pre">{request.description}</span>
          </Detail>
          <Detail icon="journal" label="Estado">
            <RequestStatusPill status={request.status} />
          </Detail>
        </dl>
      </div>
    </Dialog>
  );
}
