import { useEffect, useState, type CSSProperties, type ReactNode } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { DEFAULT_AVATAR, membersApi, shortDate, type MemberDetail, type MemberState, type TimelineItem } from '../data/membersApi';

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
