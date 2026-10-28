import { useEffect, useId, useState, type CSSProperties, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { DEFAULT_AVATAR, membersApi, shortDate, type ActiveMember, type Birthday, type MemberDetail, type MemberState, type TimelineItem } from './membersApi';

// The old /members modals (React track 017). The server decides who sees what (MemberDirectoryService); Admin/Owner
// tools (018) come in through `actions` / `rowActions` from Members.tsx.

/**
 * The server's progress sentence (MemberStatusService): "N meses até reforma" for an active member (shown only with no
 * activity this month and 1-5 months left; MemberDirectoryService.ShowProgress) or "N/3 meses de atividade consecutiva"
 * for a retired one. Only the "1 meses" wording is fixed here; a countdown is never shown for a retired member.
 */
export const progressText = (progress: string | null, retired: boolean | null) =>
  !progress || (retired && /até reforma/.test(progress)) ? null : progress.replace(/^1 meses/, '1 mês');

export function MemberFace({ avatarUrl, size }: { avatarUrl: string | null; size: number }) {
  return (
    <img
      className="who__avatar"
      src={avatarUrl ?? DEFAULT_AVATAR}
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

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="member-field">
      <dt>{label}</dt>
      <dd>{children ?? <span className="note">Não definido</span>}</dd>
    </div>
  );
}

/** "Detalhes do Membro": the same sections and fields the old modal showed any signed-in member. */
export function MemberDialog({ memberId, onClose, actions }: { memberId: string; onClose: () => void; actions?: (m: MemberDetail) => ReactNode }) {
  const [member, setMember] = useState<MemberDetail | 'missing' | null>();

  useEffect(() => {
    membersApi.member(memberId).then((o) => setMember(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [memberId]);

  return (
    <Dialog title="Detalhes do membro" size="lg" onClose={onClose}>
      {member === undefined ? (
        <Loading label="A carregar o membro…" />
      ) : member === 'missing' ? (
        <p className="form__banner" role="alert">
          Este membro já não existe.
        </p>
      ) : member === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar o membro.
        </p>
      ) : (
        <>
          <MemberBody m={member} />
          {actions && <div className="member-detail__actions">{actions(member)}</div>}
        </>
      )}
    </Dialog>
  );
}

function MemberBody({ m }: { m: MemberDetail }) {
  return (
    <div className="member-detail">
      <div className="member-detail__head">
        <MemberFace avatarUrl={m.avatarUrl} size={96} />
        <p className="member-detail__name">{m.displayName}</p>
        {m.fullName && <p className="who__meta">{m.fullName}</p>}
        <span className="member-badges">
          {m.badges.map((b) => (
            <MemberBadge key={b.kind} kind={b.kind}>{b.label}</MemberBadge>
          ))}
          {m.positions.map((p) => (
            <MemberBadge key={p} kind="position">{p}</MemberBadge>
          ))}
        </span>
      </div>

      <section className="member-section" aria-label="Informações pessoais">
        <h3 className="member-section__title">Informações pessoais</h3>
        <dl className="member-fields">
          <Field label="Email">{m.email && <a href={`mailto:${m.email}`}>{m.email}</a>}</Field>
          <Field label="Contacto">{m.phone && <a href={`tel:${m.phone}`}>{m.phone}</a>}</Field>
          <Field label="Cidade">{m.city}</Field>
          <Field label="Data de nascimento">{m.birthDate && `${m.birthDate}${m.age !== null ? ` (${m.age} anos)` : ''}`}</Field>
          <Field label="Curso">{m.degree}</Field>
        </dl>
      </section>

      <section className="member-section" aria-label="Informação da tuna">
        <h3 className="member-section__title">Informação da tuna</h3>
        <dl className="member-fields">
          {m.showInstruments && <Field label="Instrumentos">{m.instruments.length > 0 ? m.instruments.join(', ') : null}</Field>}
          {m.showMentor && <Field label="Padrinho">{m.mentor}</Field>}
        </dl>
        <div className="member-path">
          <p className="member-path__title">Percurso na tuna</p>
          {m.timeline.length > 0 ? <TunaPath items={m.timeline} /> : <p className="note">Ainda sem percurso registado.</p>}
        </div>
      </section>

      {m.state && <StateSection state={m.state} />}
    </div>
  );
}

/**
 * The look of a milestone, from what the server already says (MemberDirectoryService.Timeline): `accent` is the
 * category (leitao, caloiro, tuno, veterano, tunossauro, fundador, honorario) and `role` / `magister` are the officer
 * positions, one look for both.
 */
const pathVariant = (t: TimelineItem) => (t.accent === 'role' || t.accent === 'magister' ? 'position' : t.accent);

/**
 * "Percurso na Tuna": the milestones the server built, oldest first, as cards on a thin rail - a row on wide screens
 * (it scrolls sideways if it must), a column on a phone. The variant colours the card and shapes the marker; `state`
 * marks the current steps. Shared by /profile and the member details (the same server timeline, the same look).
 */
export function TunaPath({ items }: { items: TimelineItem[] }) {
  return (
    <ol className="tuna-path" style={{ '--steps': items.length } as CSSProperties}>
      {items.map((t, i) => (
        <li key={i} className={`tuna-path__step tuna-path__step--${pathVariant(t)}${t.state ? ` is-${t.state}` : ''}`}>
          <span className="tuna-path__marker" aria-hidden="true" />
          <span className="tuna-path__card">
            <span className="tuna-path__label">{t.label}</span>
            <span className="tuna-path__years">{t.years}</span>
            {t.notes && <span className="tuna-path__notes">{t.notes}</span>}
          </span>
        </li>
      ))}
    </ol>
  );
}

/** "Estado na tuna": active or retired, the way back, and the last rehearsal and event. */
export function StateSection({ state }: { state: MemberState }) {
  return (
    <section className="member-section" aria-label="Estado na tuna">
      <h3 className="member-section__title">Estado na tuna</h3>
      <dl className="member-fields">
        {state.retired !== null && (
          <Field label="Estado atual">
            <MemberBadge kind={state.retired ? 'retired' : 'active'}>{state.retired ? 'REFORMADO' : 'NO ATIVO'}</MemberBadge>
            {progressText(state.progress, state.retired) && <span className="member-progress">{progressText(state.progress, state.retired)}</span>}
            {state.progress && state.progressMonths !== null && state.progressTotalMonths ? (
              <progress className="member-progress__bar" max={state.progressTotalMonths} value={state.progressMonths} />
            ) : null}
          </Field>
        )}
        {state.encourage && <p className="notice member-encourage">Falta pouco: com uma atividade ainda este mês, regressas ao ativo.</p>}
        <Field label="Último ensaio">{shortDate(state.lastRehearsal)}</Field>
        <Field label="Última atuação">{shortDate(state.lastEvent)}</Field>
      </dl>
      <ActivityList activities={state.activities} />
    </section>
  );
}

/** "Ver atuações e ensaios detalhados": what the member attended, newest first (built by the server). */
export function ActivityList({ activities }: { activities: MemberState['activities'] }) {
  return (
    <details className="who__more">
      <summary>Ver atuações e ensaios detalhados</summary>
      {activities.length === 0 ? (
        <p className="note">Nenhuma atividade registada</p>
      ) : (
        <ol className="activity-feed">
          {activities.map((a, i) => {
            const d = new Date(a.date);
            return (
              <li key={i} className={a.isRehearsal ? 'activity-feed__item' : 'activity-feed__item activity-feed__item--event'}>
                <time className="activity-feed__date" dateTime={a.date}>
                  <strong>{d.getDate()}</strong>
                  <small>
                    {d.toLocaleDateString('pt-PT', { month: 'short' }).replace('.', '')} {d.getFullYear()}
                  </small>
                </time>
                <span className="activity-feed__body">
                  <strong>{a.name}</strong>
                  <small>
                    <Icon name={a.isRehearsal ? 'music' : 'calendar'} />
                    {a.type}
                  </small>
                </span>
              </li>
            );
          })}
        </ol>
      )}
    </details>
  );
}

function useSearch(initial = '') {
  const [text, setText] = useState(initial);
  const [q, setQ] = useState(initial);
  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);
  return { text, setText, q };
}

/** The old "Gestão de Membros Ativos": who is active or retired, and since when; Admin/Owner act on a row. */
export function ActiveMembersDialog({ onClose, rowActions }: { onClose: () => void; rowActions?: (m: ActiveMember, reload: () => void) => ReactNode }) {
  const [list, setList] = useState<ActiveMember[] | null>();
  const [status, setStatus] = useState('');
  const [version, setVersion] = useState(0);
  const search = useSearch();
  const ids = { q: useId(), status: useId() };

  useEffect(() => {
    let live = true; // a slower answer for an older query never replaces the newer one
    membersApi.active(status, search.q).then((o) => live && setList(o.kind === 'ok' ? o.data : null));
    return () => {
      live = false;
    };
  }, [status, search.q, version]);

  return (
    <Dialog title="Membros ativos" size="lg" onClose={onClose}>
      <div className="events-tools" role="search">
        <label className="control" htmlFor={ids.q}>
          <span className="sr-only">Procurar</span>
          <Icon name="search" />
          <input id={ids.q} type="search" placeholder="Procurar por nome ou alcunha" value={search.text} onChange={(e) => search.setText(e.target.value)} />
        </label>
        <label className="control control--select" htmlFor={ids.status}>
          <span className="sr-only">Estado</span>
          <select id={ids.status} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Todos os estados</option>
            <option value="active">No ativo</option>
            <option value="retired">Reformado</option>
          </select>
        </label>
      </div>
      {list === undefined ? (
        <Loading label="A carregar…" />
      ) : list === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar a lista.
        </p>
      ) : list.length === 0 ? (
        <p className="note">Nenhum membro ativo encontrado.</p>
      ) : (
        <ul className="member-rows">
          {list.map((m) => (
            <li key={m.id} className="member-row">
              <MemberFace avatarUrl={m.avatarUrl} size={48} />
              <span className="member-row__who">
                <strong>{m.displayName}</strong>
                {m.fullName && <small>{m.fullName}</small>}
                <small>{m.instrument ?? '-'}</small>
                <span className="member-dates">
                  <span className="member-date">
                    <Icon name="music" />
                    <span>
                      <span className="member-date__label">Último ensaio</span>
                      <strong>{shortDate(m.lastRehearsal) ?? '-'}</strong>
                    </span>
                  </span>
                  <span className="member-date">
                    <Icon name="calendar" />
                    <span>
                      <span className="member-date__label">Última atuação</span>
                      <strong>{shortDate(m.lastEvent) ?? '-'}</strong>
                    </span>
                  </span>
                </span>
                {progressText(m.progress, m.retired) && (
                  <span className="member-progress member-progress--row">
                    <Icon name="clock" />
                    {progressText(m.progress, m.retired)}
                  </span>
                )}
                {m.encourage && <small className="member-encourage">Falta pouco: com uma atividade ainda este mês, regressas ao ativo.</small>}
              </span>
              <MemberBadge kind={m.retired ? 'retired' : 'active'}>{m.retired ? 'REFORMADO' : 'NO ATIVO'}</MemberBadge>
              {rowActions && <span className="member-row__actions">{rowActions(m, () => setVersion((v) => v + 1))}</span>}
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  );
}

/** The old "Aniversários": the birthdays still to come this year, soonest first. */
export function BirthdaysDialog({ onClose }: { onClose: () => void }) {
  const [list, setList] = useState<Birthday[] | null>();
  const search = useSearch();
  const id = useId();

  useEffect(() => {
    membersApi.birthdays(search.q).then((o) => setList(o.kind === 'ok' ? o.data : null));
  }, [search.q]);

  const now = new Date();
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

  return (
    <Dialog title="Aniversários" size="lg" onClose={onClose}>
      <div className="events-tools" role="search">
        <label className="control" htmlFor={id}>
          <span className="sr-only">Procurar</span>
          <Icon name="search" />
          <input id={id} type="search" placeholder="Procurar por nome ou alcunha" value={search.text} onChange={(e) => search.setText(e.target.value)} />
        </label>
      </div>
      {list === undefined ? (
        <Loading label="A carregar…" />
      ) : list === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar os aniversários.
        </p>
      ) : list.length === 0 ? (
        <p className="note">Nenhum aniversário encontrado.</p>
      ) : (
        <ul className="member-rows">
          {list.map((b) => (
            <li key={b.id} className="member-row">
              <MemberFace avatarUrl={b.avatarUrl} size={48} />
              <span className="member-row__who">
                <strong>{b.displayName}</strong>
                {b.fullName && <small>{b.fullName}</small>}
                <small>
                  {b.age} anos · {b.city ?? '-'}
                </small>
                <span className="member-badges">
                  {b.badges.map((x) => (
                    <MemberBadge key={x.kind} kind={x.kind}>{x.label}</MemberBadge>
                  ))}
                </span>
              </span>
              <MemberBadge kind={b.date === today ? 'active' : undefined}>{b.date === today ? 'Hoje' : b.birthday}</MemberBadge>
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  );
}
