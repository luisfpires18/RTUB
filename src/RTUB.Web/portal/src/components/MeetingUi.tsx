// Shared pieces of the /meetings dialogs: field limits, error text, pills, avatars, ConfirmDialog and summary blocks.
import { useId, useState, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import type { Outcome } from '../data/eventsApi';
import { Icon, type IconName } from './icons';
import type { MeetingCard, MeetingPerson } from '../data/meetingsTypes';
import { longDateTime } from '../data/meetingsDates';
import { DEFAULT_AVATAR } from '../data/membersApi';

export const MAX_TITLE = 200;
export const MAX_LOCATION = 200;
export const MAX_STATEMENT = 5000;
export const MAX_DESCRIPTION = 2000;
export const MAX_NOTES = 500;
export const MAX_REASON = 1000;
export const MAX_PUSH = 500;
export const MAX_SUBJECT = 300;
export const MAX_EMAIL_BODY = 10000;

/** One sentence for a refused or failed call. */
export const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? (Object.values(o.errors)[0] ?? `Não foi possível ${what}.`)
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Esta reunião ou pedido já não existe, ou deixou de estar disponível.'
        : o.kind === 'closed'
          ? 'Esta reunião já não permite esta ação.'
          : o.kind === 'signin'
            ? 'A sessão terminou. Entre outra vez.'
            : `Não foi possível ${what}. Tente outra vez daqui a pouco.`;

export type Errors = Record<string, string>;

export const fieldClass = (errors: Errors, key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');

export function FieldError({ errors, name }: { errors: Errors; name: string }) {
  return errors[name] ? (
    <p className="form__error" role="alert">
      {errors[name]}
    </p>
  ) : null;
}

export function Banner({ text }: { text?: string }) {
  return text ? (
    <p className="form__banner" role="alert">
      {text}
    </p>
  ) : null;
}

export const typeClass = (type: string) =>
  type === 'ConselhoVeteranos' ? 'mtg-type mtg-type--cv' : type === 'ReuniaoDirecao' ? 'mtg-type mtg-type--direcao' : 'mtg-type mtg-type--ag';

const REQUEST_STATUS_LABEL: Record<string, string> = { Pending: 'Pendente', Analysing: 'Em Análise', Confirmed: 'Confirmado', Rejected: 'Rejeitado' };
const REQUEST_STATUS_PILL: Record<string, string> = { Pending: 'pill pill--wait', Analysing: 'pill pill--wait', Confirmed: 'pill pill--yes', Rejected: 'pill pill--no' };

export function RequestStatusPill({ status }: { status: string }) {
  return <span className={REQUEST_STATUS_PILL[status] ?? 'pill'}>{REQUEST_STATUS_LABEL[status] ?? status}</span>;
}

export function Face({ src, size = 32 }: { src: string | null; size?: number }) {
  return (
    <img
      className="mtg-face"
      src={src || DEFAULT_AVATAR}
      alt=""
      width={size}
      height={size}
      loading="lazy"
      onError={(e) => {
        if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
      }}
    />
  );
}

export function Spinner({ on }: { on: boolean }) {
  return on ? <span className="spinner spinner--small" aria-hidden="true" /> : null;
}

/** A yes / no question with its own busy state; `onConfirm` answers an error sentence, or nothing when done. */
export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  cancelLabel = 'Cancelar',
  confirmIcon,
  danger = false,
  onConfirm,
  onClose,
}: {
  title: string;
  children: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  confirmIcon?: IconName;
  danger?: boolean;
  onConfirm: () => Promise<string | void>;
  onClose: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    setError(undefined);
    const failure = await onConfirm();
    setBusy(false);
    if (failure) setError(failure);
    else onClose();
  };

  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            {cancelLabel}
          </button>
          <button type="button" className={danger ? 'btn btn--danger' : 'btn btn--primary'} onClick={confirm} disabled={busy}>
            <Spinner on={busy} />
            {!busy && confirmIcon && <Icon name={confirmIcon} />}
            {confirmLabel}
          </button>
        </>
      }
    >
      <div className="mtg-confirm">{children}</div>
      <Banner text={error} />
    </Dialog>
  );
}

/** The meeting the notice is about. */
export function MeetingSummary({ card, heading = 'Detalhes da Reunião:' }: { card: MeetingCard; heading?: string }) {
  return (
    <section className="mtg-summary" aria-label={heading}>
      <p className="mtg-summary__heading">{heading}</p>
      <dl>
        <div>
          <dt>Tipo:</dt>
          <dd>{card.typeLabel}</dd>
        </div>
        <div>
          <dt>Título:</dt>
          <dd>{card.title}</dd>
        </div>
        <div>
          <dt>Data:</dt>
          <dd>{longDateTime(card.date)}</dd>
        </div>
        {card.location && (
          <div>
            <dt>Local:</dt>
            <dd>{card.location}</dd>
          </div>
        )}
      </dl>
    </section>
  );
}

/** "Destinatários" / "Quem não irá receber": names and pictures (and, for a CV, years as Tuno). */
export function PeopleList({ title, people, empty }: { title: string; people: MeetingPerson[]; empty?: string }) {
  const id = useId();
  return (
    <section className="mtg-people" aria-labelledby={id}>
      <h3 id={id} className="mtg-people__title">
        {title} <span className="mtg-people__count">{people.length}</span>
      </h3>
      {people.length === 0 ? (
        empty ? <p className="note">{empty}</p> : null
      ) : (
        <ul className="mtg-people__list">
          {people.map((p, i) => (
            <li key={`${p.name}-${i}`} className="mtg-person">
              <Face src={p.avatarUrl} />
              <span className="mtg-person__name">{p.name}</span>
              {p.detail && <small className="mtg-person__detail">{p.detail}</small>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function Detail({ icon, label, children }: { icon: IconName; label: string; children: ReactNode }) {
  return (
    <div className="mtg-detail__row">
      <dt>
        <Icon name={icon} />
        {label}
      </dt>
      <dd>{children}</dd>
    </div>
  );
}
