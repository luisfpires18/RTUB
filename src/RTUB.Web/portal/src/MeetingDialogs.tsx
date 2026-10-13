import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon, type IconName } from './icons';
import {
  localDateTime,
  longDateTime,
  meetingsApi,
  numericDateTime,
  type MeetingBoard,
  type MeetingCancelDraft,
  type MeetingCandidate,
  type MeetingCard,
  type MeetingDraft,
  type MeetingEmailDraft,
  type MeetingForm,
  type MeetingNoticeResult,
  type MeetingOption,
  type MeetingParticipant,
  type MeetingParticipants,
  type MeetingPerson,
  type MeetingPushDraft,
  type MeetingRequest,
} from './meetingsApi';

// The /meetings dialogs (task 034): details, create / edit, "Vou / Não vou", participants, email, push, cancel,
// proposals and request details. Texts are the old page's; every rule is the server's.

const MAX_TITLE = 200;
const MAX_LOCATION = 200;
const MAX_STATEMENT = 5000;
const MAX_DESCRIPTION = 2000;
const MAX_NOTES = 500;
const MAX_REASON = 1000;
const MAX_PUSH = 500;
const MAX_SUBJECT = 300;
const MAX_EMAIL_BODY = 10000;
const DEFAULT_AVATAR = '/images/default-avatar.webp';

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

function Spinner({ on }: { on: boolean }) {
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

/** The meeting the notice is about, as the old dialogs showed it. */
function MeetingSummary({ card, heading = 'Detalhes da Reunião:' }: { card: MeetingCard; heading?: string }) {
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
function PeopleList({ title, people, empty }: { title: string; people: MeetingPerson[]; empty?: string }) {
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

function Detail({ icon, label, children }: { icon: IconName; label: string; children: ReactNode }) {
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

// ---------- details ----------

export function DetailsDialog({ card, onClose }: { card: MeetingCard; onClose: () => void }) {
  return (
    <Dialog
      title="Detalhes da Reunião"
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Voltar Atrás
        </button>
      }
    >
      <div className="mtg-detail">
        <span className={typeClass(card.type)}>{card.typeLabel}</span>
        <h3 className="mtg-detail__title">{card.title}</h3>
        <dl className="mtg-detail__list">
          <Detail icon="calendar" label="Data e Hora">
            {numericDateTime(card.date)}
          </Detail>
          {card.location && (
            <Detail icon="geo" label="Localização">
              {card.location}
            </Detail>
          )}
          {card.organizerName && (
            <Detail icon="person" label="Organizador">
              {card.organizerName}
              {card.organizerPosition && <span className="note"> ({card.organizerPosition})</span>}
            </Detail>
          )}
          {card.type === 'ConselhoVeteranos' && card.tunoRepresentative && (
            <Detail icon="personBadge" label="Representante de Tunos">
              {card.tunoRepresentative}
            </Detail>
          )}
          <Detail icon="file" label="Declaração">
            <span className="mtg-pre">{card.statement}</span>
          </Detail>
        </dl>
      </div>
    </Dialog>
  );
}

// ---------- create / edit ----------

const TYPE_LABELS: Record<string, string> = {
  AssembleiaGeralOrdinaria: 'Assembleia Geral Ordinária',
  AssembleiaGeralExtraordinaria: 'Assembleia Geral Extraordinária',
  ConselhoVeteranos: 'Conselho de Veteranos',
  ReuniaoDirecao: 'Reunião de Direção',
};

// The old default: AGO when allowed, else CV, else AGE.
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
  const [date, setDate] = useState(card?.date ?? draft?.date ?? localDateTime(1, 20)); // the old default: tomorrow at 20:00
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
            <Spinner on={busy} />
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
                <FieldError errors={errors} name="type" />
              </div>
              <div className={fieldClass(errors, 'title')}>
                <label htmlFor={`${id}-title`}>Título</label>
                <input id={`${id}-title`} type="text" maxLength={MAX_TITLE} placeholder="Inserir título..." value={title} onChange={(e) => setTitle(e.target.value)} />
                <FieldError errors={errors} name="title" />
              </div>
              <div className={fieldClass(errors, 'date')}>
                <label htmlFor={`${id}-date`}>Data e Hora</label>
                <input id={`${id}-date`} type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
                <FieldError errors={errors} name="date" />
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
                <FieldError errors={errors} name="location" />
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
                  <FieldError errors={errors} name="tunoRepresentativeId" />
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
                <FieldError errors={errors} name="statement" />
              </div>
            </>
          )}
        </form>
      )}
    </Dialog>
  );
}

// ---------- "Vou / Não vou" ----------

export function ParticipationDialog({
  card,
  willAttend,
  onClose,
  onSaved,
}: {
  card: MeetingCard;
  willAttend: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const existing = card.myParticipation;
  const [notes, setNotes] = useState(existing?.notes ?? '');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();
  const title = existing
    ? willAttend
      ? 'Editar Confirmação'
      : 'Editar Impossibilidade'
    : willAttend
      ? 'Confirmar Participação'
      : 'Registar Impossibilidade';

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const o = await meetingsApi.respond(card.id, willAttend, notes.trim() ? notes : null);
    setBusy(false);
    if (o.kind === 'ok') onSaved();
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(o.kind === 'closed' ? 'Já não é possível responder a esta reunião.' : problem(o, 'guardar a resposta'));
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
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            <Spinner on={busy} />
            {existing ? 'Guardar' : 'Confirmar'}
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form mtg-form" onSubmit={submit} noValidate>
        <p className="mtg-answer-for">
          <span className={willAttend ? 'pill pill--yes' : 'pill pill--no'}>
            <Icon name={willAttend ? 'checkCircle' : 'xCircle'} />
            {willAttend ? 'Vou' : 'Não vou'}
          </span>
          <strong>{card.title}</strong>
        </p>
        <Banner text={banner} />
        <div className={fieldClass(errors, 'notes')}>
          <label htmlFor={`${id}-notes`}>{willAttend ? 'Notas (opcional)' : 'Motivo (opcional)'}</label>
          <textarea
            id={`${id}-notes`}
            rows={4}
            maxLength={MAX_NOTES}
            placeholder={willAttend ? 'Informações adicionais...' : 'Motivo da impossibilidade...'}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
          <FieldError errors={errors} name="notes" />
        </div>
      </form>
    </Dialog>
  );
}

// ---------- participants ----------

const BADGE_CLASS: Record<string, string> = {
  Magister: 'member-badge member-badge--position',
  Tuno: 'member-badge member-badge--tuno',
  Caloiro: 'member-badge member-badge--caloiro',
};

/** "Participantes - {título}": who goes and who does not, with notes; Admin / Owner add members. */
export function ParticipantsDialog({ card, onClose, onChanged }: { card: MeetingCard; onClose: () => void; onChanged: () => void }) {
  const [list, setList] = useState<MeetingParticipants | null>();
  const [removing, setRemoving] = useState<MeetingParticipant>();
  const [adding, setAdding] = useState(false);
  const [query, setQuery] = useState('');
  const [candidates, setCandidates] = useState<MeetingCandidate[]>();
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; text: string }>();
  const [loadError, setLoadError] = useState<string>();
  const [version, setVersion] = useState(0);
  const searchId = useId();

  useEffect(() => {
    meetingsApi.participants(card.id).then((o) => {
      if (o.kind === 'ok') setList(o.data);
      else {
        setList(null);
        setLoadError(problem(o, 'carregar os participantes'));
      }
    });
  }, [card.id]);

  useEffect(() => {
    if (!adding || !query.trim()) return setCandidates(undefined);
    let live = true;
    const timer = setTimeout(() => {
      meetingsApi.candidates(card.id, query.trim()).then((o) => live && setCandidates(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [card.id, adding, query, version]);

  const run = async (action: Promise<Outcome<MeetingParticipants>>, success: string, what: string) => {
    setBusy(true);
    setFeedback(undefined);
    const o = await action;
    setBusy(false);
    setRemoving(undefined);
    if (o.kind === 'ok') {
      setList(o.data);
      setVersion((v) => v + 1);
      setFeedback({ ok: true, text: success });
      onChanged();
    } else setFeedback({ ok: false, text: problem(o, what) });
  };

  const group = (title: string, people: MeetingParticipant[], tone: 'yes' | 'no') =>
    people.length > 0 && (
      <section className="mtg-who" aria-label={title}>
        <h3 className={`mtg-who__title mtg-who__title--${tone}`}>
          {title} ({people.length})
        </h3>
        <ul className="who mtg-who__list">
          {people.map((p) => (
            <li key={p.id} className="who__person mtg-who__person" title={p.fullName || undefined}>
              <Face src={p.avatarUrl} size={64} />
              <p className="who__name">{p.nickname || p.fullName}</p>
              {p.fullName && p.fullName !== p.nickname && <p className="who__meta">{p.fullName}</p>}
              {p.badge && <span className={BADGE_CLASS[p.badge] ?? 'member-badge'}>{p.badge}</span>}
              {p.notes && <p className="who__note">“{p.notes}”</p>}
              {p.canRemove && (
                <button
                  type="button"
                  className="icon-btn icon-btn--sm icon-btn--danger mtg-who__remove"
                  onClick={() => setRemoving(p)}
                  disabled={busy}
                  title="Remover participação"
                >
                  <Icon name="trash" />
                  <span className="sr-only">Remover a participação de {p.nickname || p.fullName}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      </section>
    );

  return (
    <Dialog
      title={`Participantes - ${card.title}`}
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Voltar Atrás
        </button>
      }
    >
      {list === undefined ? (
        <Loading label="A carregar os participantes…" />
      ) : list === null ? (
        <Banner text={loadError} />
      ) : (
        <div className="mtg-participants">
          {removing && (
            <div className="mtg-inline-confirm" role="alertdialog" aria-label="Confirmar Eliminação">
              <p>
                Tem a certeza que quer remover a participação de <strong>{removing.nickname || 'este membro'}</strong>?
              </p>
              <p className="note">Não é possível desfazer.</p>
              <span className="mtg-inline-confirm__actions">
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoving(undefined)} disabled={busy}>
                  Cancelar
                </button>
                <button
                  type="button"
                  className="btn btn--danger btn--sm"
                  onClick={() => run(meetingsApi.removeParticipation(card.id, removing.id), 'Participação removida com sucesso.', 'remover a participação')}
                  disabled={busy}
                >
                  <Spinner on={busy} />
                  Eliminar
                </button>
              </span>
            </div>
          )}
          {feedback && <p className={feedback.ok ? 'mtg-feedback mtg-feedback--ok' : 'form__banner'} role={feedback.ok ? 'status' : 'alert'}>{feedback.text}</p>}
          {list.going.length === 0 && list.notGoing.length === 0 ? (
            <div className="mtg-empty">
              <Icon name="people" className="mtg-empty__icon" />
              <p className="mtg-empty__title">Nenhuma participação ainda para esta reunião</p>
            </div>
          ) : (
            <>
              {group('Vão participar', list.going, 'yes')}
              {group('Não vão participar', list.notGoing, 'no')}
            </>
          )}

          {list.canAdd && (
            <div className="mtg-add">
              {!adding ? (
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAdding(true)}>
                  <Icon name="plus" />
                  Adicionar Membro
                </button>
              ) : (
                <>
                  <label className="control" htmlFor={searchId}>
                    <span className="sr-only">Pesquisar membro</span>
                    <Icon name="search" />
                    <input
                      id={searchId}
                      type="search"
                      placeholder="Pesquisar membro por nome..."
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                      autoFocus
                    />
                  </label>
                  <p className="form__hint">Fica como «Vou». Não é enviada notificação.</p>
                  {candidates && candidates.length === 0 && <p className="note">Nenhum membro encontrado.</p>}
                  {candidates && candidates.length > 0 && (
                    <ul className="mtg-candidates">
                      {candidates.map((c) => (
                        <li key={c.id} className="mtg-candidate">
                          <Face src={c.avatarUrl} size={36} />
                          <span className="mtg-candidate__name">{c.name}</span>
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            onClick={() => run(meetingsApi.addParticipant(card.id, c.id), `Participação de ${c.name} adicionada com sucesso.`, 'adicionar o membro')}
                            disabled={busy}
                          >
                            <Icon name="plus" />
                            Adicionar
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm mtg-add__cancel"
                    onClick={() => {
                      setAdding(false);
                      setQuery('');
                    }}
                  >
                    <Icon name="xCircle" />
                    Cancelar
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}

// ---------- email ----------

/**
 * The preview document for the sandboxed frame (sandbox="", srcdoc: no script runs and nothing reaches this page). The
 * frame inherits the page's Content-Security-Policy, which refuses inline CSS and other origins' images, so those are
 * dropped first (a missed one is only refused, never applied) and the browser shows the email's text without a refusal
 * in the console; blocks the template pre-wraps keep their line breaks. Parsing happens in an inert DOMParser document.
 */
function previewDocument(html: string): string {
  const cleaned = html
    .replace(/<style\b[\s\S]*?<\/style\s*>/gi, '')
    .replace(/\sstyle\s*=\s*("[^"]*"|'[^']*')/gi, (_match, value: string) => (/white-space\s*:\s*pre/i.test(value) ? ' data-mtg-pre=""' : ''));
  const doc = new DOMParser().parseFromString(cleaned, 'text/html');
  doc.querySelectorAll('script, link, base, meta[http-equiv], iframe, object, embed').forEach((el) => el.remove());
  doc.querySelectorAll('img').forEach((img) => {
    let sameOrigin = false;
    try {
      sameOrigin = new URL(img.getAttribute('src') ?? '', location.href).origin === location.origin;
    } catch {
      sameOrigin = false;
    }
    if (!sameOrigin) img.replaceWith(doc.createTextNode(img.getAttribute('alt') ?? ''));
  });
  doc.querySelectorAll('[data-mtg-pre]').forEach((el) => {
    const walker = doc.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    const texts: Text[] = [];
    while (walker.nextNode()) texts.push(walker.currentNode as Text);
    for (const text of texts) {
      const lines = text.data.split('\n');
      if (lines.length < 2) continue;
      const fragment = doc.createDocumentFragment();
      lines.forEach((line, i) => {
        if (i > 0) fragment.append(doc.createElement('br'));
        fragment.append(line);
      });
      text.replaceWith(fragment);
    }
  });
  return `<!doctype html>${doc.documentElement.outerHTML}`;
}

/**
 * The meeting email: subject, the statement as the body, a preview of the rendered email and who receives it. The
 * preview is the server's HTML in a sandboxed frame (no scripts, no same-origin access): nothing of it reaches this
 * page's DOM.
 */
export function EmailDialog({ card, onClose, onSent }: { card: MeetingCard; onClose: () => void; onSent: (r: MeetingNoticeResult) => void }) {
  const [draft, setDraft] = useState<MeetingEmailDraft | null>();
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [tunoId, setTunoId] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [preview, setPreview] = useState<{ html?: string; error?: string }>();
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.emailDraft(card.id).then((o) => {
      if (o.kind !== 'ok') {
        setDraft(null);
        setBanner(problem(o, 'preparar o email'));
        return;
      }
      setDraft(o.data);
      setSubject(o.data.subject);
      setBody(o.data.body);
    });
  }, [card.id]);

  useEffect(() => {
    if (!showPreview) return;
    let live = true;
    const timer = setTimeout(() => {
      meetingsApi.emailPreview(card.id, body).then((o) => {
        if (live) setPreview(o.kind === 'ok' ? { html: previewDocument(o.data.html) } : { error: problem(o, 'gerar a pré-visualização') });
      });
    }, 400);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [card.id, body, showPreview]);

  const send = async () => {
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.sendEmail(card.id, subject, body, tunoId || null);
    setBusy(false);
    if (o.kind === 'ok') onSent(o.data);
    else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.notice) setBanner(o.errors.notice);
    } else setBanner(problem(o, 'enviar o email'));
  };

  const recipients = draft?.recipients ?? [];
  const canSend = Boolean(draft) && recipients.length > 0 && subject.trim() !== '' && body.trim() !== '';

  return (
    <Dialog
      title="Enviar notificação da reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={send} disabled={busy || !canSend}>
            <Spinner on={busy} />
            {busy ? 'A enviar emails…' : 'Enviar Notificação'}
          </button>
        </>
      }
    >
      <MeetingSummary card={card} />
      <Banner text={banner} />
      {draft === undefined ? (
        <Loading label="A carregar subscritores..." />
      ) : draft === null ? null : (
        <div className="form mtg-form">
          <div className={fieldClass(errors, 'subject')}>
            <label htmlFor={`${id}-subject`}>
              Assunto <span className="mtg-required">*</span>
            </label>
            <input id={`${id}-subject`} type="text" maxLength={MAX_SUBJECT} value={subject} onChange={(e) => setSubject(e.target.value)} />
            <FieldError errors={errors} name="subject" />
          </div>
          <div className={fieldClass(errors, 'body')}>
            <label htmlFor={`${id}-body`}>
              Corpo do Email <span className="mtg-required">*</span>
            </label>
            <textarea id={`${id}-body`} rows={12} maxLength={MAX_EMAIL_BODY} value={body} onChange={(e) => setBody(e.target.value)} />
            <p className="form__hint">Escreva a mensagem da reunião em texto simples. Será formatada automaticamente no email.</p>
            <FieldError errors={errors} name="body" />
          </div>
          <div className="mtg-preview">
            <button type="button" className="btn btn--ghost btn--sm" aria-expanded={showPreview} onClick={() => setShowPreview((s) => !s)}>
              <Icon name="eye" />
              {showPreview ? 'Ocultar' : 'Ver'} Pré-visualização
            </button>
            {showPreview && (
              <div className="mtg-preview__box">
                <p className="mtg-preview__label">Pré-visualização do Email:</p>
                {preview?.error ? (
                  <p className="form__error">{preview.error}</p>
                ) : preview?.html === undefined ? (
                  <Loading label="A gerar a pré-visualização…" />
                ) : (
                  <iframe className="mtg-preview__frame" title="Pré-visualização do email" sandbox="" referrerPolicy="no-referrer" srcDoc={preview.html} />
                )}
              </div>
            )}
          </div>
          {draft.tunoOptions.length > 0 && (
            <div className={fieldClass(errors, 'tunoId')}>
              <label htmlFor={`${id}-tuno`}>Adicionar um Representante de Tunos</label>
              <span className="control control--select">
                <select id={`${id}-tuno`} value={tunoId} onChange={(e) => setTunoId(e.target.value)}>
                  <option value="">Nenhum representante selecionado</option>
                  {draft.tunoOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </span>
              <p className="form__hint">Opcionalmente adicione um representante de Tunos à lista de destinatários para reuniões CV.</p>
              <FieldError errors={errors} name="tunoId" />
            </div>
          )}
          <p className="mtg-info">
            <Icon name="envelope" />
            {recipients.length} membros irão receber esta mensagem.
          </p>
          <PeopleList title="Destinatários:" people={recipients} empty="Nenhum destinatário encontrado." />
          <PeopleList title="Quem não irá receber:" people={draft.notReceiving} />
        </div>
      )}
    </Dialog>
  );
}

// ---------- push ----------

export function PushDialog({ card, onClose, onSent }: { card: MeetingCard; onClose: () => void; onSent: (r: MeetingNoticeResult) => void }) {
  const [draft, setDraft] = useState<MeetingPushDraft | null>();
  const [message, setMessage] = useState('');
  const [tunoId, setTunoId] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.pushDraft(card.id).then((o) => {
      if (o.kind === 'ok') setDraft(o.data);
      else {
        setDraft(null);
        setBanner(problem(o, 'carregar os destinatários'));
      }
    });
  }, [card.id]);

  const send = async () => {
    if (!message.trim()) return setErrors({ message: 'A mensagem da notificação é obrigatória.' });
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.sendPush(card.id, message, tunoId || null);
    setBusy(false);
    if (o.kind === 'ok') onSent(o.data);
    else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.notice) setBanner(o.errors.notice);
    } else setBanner(problem(o, 'enviar a notificação push'));
  };

  const recipients = draft?.recipients ?? [];
  const total = recipients.length + (draft?.notSubscribed.length ?? 0);

  return (
    <Dialog
      title="Enviar notificação push da reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={send} disabled={busy || recipients.length === 0}>
            <Spinner on={busy} />
            {!busy && <Icon name="bell" />}
            Enviar Notificação Push
          </button>
        </>
      }
    >
      <MeetingSummary card={card} />
      <Banner text={banner} />
      {draft === undefined ? (
        <Loading label="A carregar destinatários..." />
      ) : draft === null ? null : (
        <div className="form mtg-form">
          {draft.tunoOptions.length > 0 && (
            <div className={fieldClass(errors, 'tunoId')}>
              <label htmlFor={`${id}-tuno`}>Adicionar um Representante de Tunos</label>
              <span className="control control--select">
                <select id={`${id}-tuno`} value={tunoId} onChange={(e) => setTunoId(e.target.value)}>
                  <option value="">Nenhum representante selecionado</option>
                  {draft.tunoOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </span>
              <p className="form__hint">Opcionalmente adicione um representante de Tunos à lista de destinatários para reuniões CV.</p>
              <FieldError errors={errors} name="tunoId" />
            </div>
          )}
          <div className={fieldClass(errors, 'message')}>
            <label htmlFor={`${id}-message`}>
              Mensagem da Notificação <span className="mtg-required">*</span>
            </label>
            <textarea
              id={`${id}-message`}
              rows={4}
              maxLength={MAX_PUSH}
              placeholder="Ex: Reunião importante, confirma a tua presença!"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
            <p className="form__hint">
              A notificação terá um título automático com a data da reunião. Máximo {MAX_PUSH} caracteres ({message.length} / {MAX_PUSH}).
            </p>
            <FieldError errors={errors} name="message" />
          </div>
          <p className="mtg-info">
            <Icon name="bell" />
            {recipients.length} / {total} membros têm notificações push ativas e irão receber esta mensagem.
          </p>
          <PeopleList title="Destinatários com subscrições push ativas:" people={recipients} empty="Nenhum membro com subscrição push ativa encontrado." />
          <PeopleList title="Membros sem subscrição push:" people={draft.notSubscribed} />
        </div>
      )}
    </Dialog>
  );
}

// ---------- cancel ----------

export function CancelDialog({
  card,
  onClose,
  onCancelled,
}: {
  card: MeetingCard;
  onClose: () => void;
  onCancelled: (r: MeetingNoticeResult, notified: boolean) => void;
}) {
  const [draft, setDraft] = useState<MeetingCancelDraft | null>();
  const [notify, setNotify] = useState(false);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.cancelDraft(card.id).then((o) => {
      if (o.kind === 'ok') setDraft(o.data);
      else {
        setDraft(null);
        setBanner(problem(o, 'preparar o cancelamento'));
      }
    });
  }, [card.id]);

  const confirm = async () => {
    if (!reason.trim()) return setErrors({ reason: 'O motivo do cancelamento é obrigatório.' });
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.cancel(card.id, reason, notify);
    setBusy(false);
    if (o.kind === 'ok') onCancelled(o.data, notify);
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'cancelar a reunião'));
  };

  return (
    <Dialog
      title="Cancelar Reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Não Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={confirm} disabled={busy || draft === null}>
            <Spinner on={busy} />
            {busy && notify ? 'A enviar emails de cancelamento…' : 'Cancelar Reunião'}
          </button>
        </>
      }
    >
      <MeetingSummary card={card} heading="Reunião:" />
      <Banner text={banner} />
      <div className="form mtg-form">
        <label className="form__check" htmlFor={`${id}-notify`}>
          <input id={`${id}-notify`} type="checkbox" checked={notify} onChange={(e) => setNotify(e.target.checked)} />
          Notificar todos os membros por email
        </label>
        {notify &&
          (draft === undefined ? (
            <Loading label="A carregar subscritores..." />
          ) : draft ? (
            <>
              <p className="mtg-info">
                <Icon name="envelope" />
                {draft.recipients.length} / {draft.total} membros têm 'Notificações por email' ativas e irão receber esta mensagem.
              </p>
              <PeopleList title="Membros que irão receber:" people={draft.recipients} />
              <PeopleList title="Quem não irá receber:" people={draft.notReceiving} />
            </>
          ) : null)}
        <div className={fieldClass(errors, 'reason')}>
          <label htmlFor={`${id}-reason`}>
            Motivo do Cancelamento <span className="mtg-required">*</span>
          </label>
          <textarea
            id={`${id}-reason`}
            rows={4}
            maxLength={MAX_REASON}
            placeholder="Descreva o motivo do cancelamento da reunião..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <p className="form__hint">Máximo {MAX_REASON} caracteres</p>
          <FieldError errors={errors} name="reason" />
        </div>
      </div>
    </Dialog>
  );
}

// ---------- proposals and requests ----------

const PROPOSAL_TITLES: Record<string, string> = {
  ConselhoVeteranos: 'Propor Reunião de CV',
  ReuniaoDirecao: 'Propor Reunião de Direção',
  AssembleiaGeralOrdinaria: 'Propor Assembleia Geral Ordinária',
  AssembleiaGeralExtraordinaria: 'Propor Assembleia Geral',
};

export function ProposalDialog({ type, onClose, onSent }: { type: string; onClose: () => void; onSent: () => void }) {
  const [title, setTitle] = useState('');
  const [date, setDate] = useState(() => localDateTime(7)); // the old default: a week from now
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
            <Spinner on={busy} />
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
          <FieldError errors={errors} name="title" />
        </div>
        <div className={fieldClass(errors, 'proposedDate')}>
          <label htmlFor={`${id}-date`}>Data e Hora Proposta</label>
          <input id={`${id}-date`} type="datetime-local" value={date} onChange={(e) => setDate(e.target.value)} />
          <FieldError errors={errors} name="proposedDate" />
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
          <FieldError errors={errors} name="location" />
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
          <FieldError errors={errors} name="description" />
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
