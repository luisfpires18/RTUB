// The meeting proposal and request-details dialogs of /meetings.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingRequest } from '../data/meetingsTypes';
import { localDateTime, numericDateTime } from '../data/meetingsDates';
import { MAX_TITLE, MAX_LOCATION, MAX_DESCRIPTION, problem, type Errors, typeClass, RequestStatusPill, Detail } from './MeetingUi';
import { Banner } from './ui/Banner';
import { FormField } from './ui/FormField';
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
        <FormField id={`${id}-title`} label="Título" error={errors.title} alert>
          <input id={`${id}-title`} type="text" maxLength={MAX_TITLE} placeholder="Inserir título..." value={title} onChange={(e) => setTitle(e.target.value)} />
        </FormField>
        <FormField id={`${id}-date`} label="Data e Hora Proposta" error={errors.proposedDate} alert>
          <input id={`${id}-date`} type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
        </FormField>
        <FormField id={`${id}-location`} label="Localização (Opcional)" error={errors.location} alert>
          <input
            id={`${id}-location`}
            type="text"
            maxLength={MAX_LOCATION}
            placeholder="Ex: Discord, Centro Académico..."
            value={place}
            onChange={(e) => setPlace(e.target.value)}
          />
        </FormField>
        <FormField id={`${id}-description`} label="Descrição/Motivo" error={errors.description} hint={`Máximo ${MAX_DESCRIPTION} caracteres (${description.length} / ${MAX_DESCRIPTION})`} alert>
          <textarea
            id={`${id}-description`}
            rows={10}
            maxLength={MAX_DESCRIPTION}
            placeholder="Descrever o motivo e assuntos a discutir..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </FormField>
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
