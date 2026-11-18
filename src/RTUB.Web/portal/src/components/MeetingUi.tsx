// Shared pieces of the /meetings dialogs: field limits, error text, pills, avatars, ConfirmDialog and summary blocks.
import { useId, type ReactNode } from 'react';
import type { Outcome } from '../data/eventsApi';
import { Icon, type IconName } from './icons';
import type { MeetingCard, MeetingPerson } from '../data/meetingsTypes';
import { longDateTime } from '../data/meetingsDates';
import { Avatar } from './ui/Avatar';

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

export const typeClass = (type: string) =>
  type === 'ConselhoVeteranos' ? 'mtg-type mtg-type--cv' : type === 'ReuniaoDirecao' ? 'mtg-type mtg-type--direcao' : 'mtg-type mtg-type--ag';

const REQUEST_STATUS_LABEL: Record<string, string> = { Pending: 'Pendente', Analysing: 'Em Análise', Confirmed: 'Confirmado', Rejected: 'Rejeitado' };
const REQUEST_STATUS_PILL: Record<string, string> = { Pending: 'pill pill--wait', Analysing: 'pill pill--wait', Confirmed: 'pill pill--yes', Rejected: 'pill pill--no' };

export function RequestStatusPill({ status }: { status: string }) {
  return <span className={REQUEST_STATUS_PILL[status] ?? 'pill'}>{REQUEST_STATUS_LABEL[status] ?? status}</span>;
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
              <Avatar className="mtg-face" src={p.avatarUrl} />
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
