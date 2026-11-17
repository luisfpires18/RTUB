// The /profile tuna-data section: info, state badge and edit form.
import { useState, type FormEvent } from 'react';
import { Icon } from './icons';
import { ActivityList, TunaPath, progressText } from './MemberDialogs';
import { MemberBadge } from './MemberBadge';
import { MentorPicker, MonthYear } from './MemberFormFields';
import { memberAreaApi, type MyProfile, type TunaInput } from '../data/memberAreaApi';
import { DEFAULT_AVATAR, shortDate, type MemberState } from '../data/membersApi';
import { FormButtons, SavedNote, Section, problem, useFormState } from './ProfileShared';

const tunaOf = (p: MyProfile): TunaInput => ({
  mentorId: p.tuna.mentorId,
  yearLeitao: p.tuna.yearLeitao,
  monthLeitao: p.tuna.monthLeitao,
  yearCaloiro: p.tuna.yearCaloiro,
  monthCaloiro: p.tuna.monthCaloiro,
  yearTuno: p.tuna.yearTuno,
  monthTuno: p.tuna.monthTuno,
});

/** Informação da Tuna, read-only: instrumentos (with their Naipes pictures), padrinho → afilhado, percurso, estado. */
function TunaInfo({ profile }: { profile: MyProfile }) {
  const { tuna: t, member: m, instruments } = profile;
  return (
    <div className="tuna-info">
      <div className="tuna-info__top">
        <section className="tuna-info__cell" aria-labelledby="tuna-instruments-title">
          <h3 id="tuna-instruments-title" className="tuna-info__title">
            Instrumentos
          </h3>
          {instruments.length > 0 ? (
            <ul className="instrument-chips">
              {[...instruments]
                .sort((a, b) => Number(b.primary) - Number(a.primary))
                .map((i) => (
                  <li key={i.id} className={i.primary ? 'instrument-chip is-primary' : 'instrument-chip'}>
                    <InstrumentPic url={profile.instrumentPictures[i.instrument]} />
                    <span>
                      {i.label}
                      {i.primary && <small>Principal</small>}
                    </span>
                  </li>
                ))}
            </ul>
          ) : (
            <p className="note">Não definido</p>
          )}
        </section>
        {t.showMentor && (
          <section className="tuna-info__cell" aria-labelledby="tuna-mentor-title">
            <h3 id="tuna-mentor-title" className="tuna-info__title">
              Padrinho
            </h3>
            {t.mentorName ? (
              <ol className="lineage" aria-label="Padrinho e afilhado">
                <li className="lineage__node">
                  <img src={t.mentorAvatarUrl ?? DEFAULT_AVATAR} alt="" width="64" height="64" loading="lazy" />
                  <span>{t.mentorName}</span>
                </li>
                <li className="lineage__node lineage__node--me">
                  <img src={m.avatarUrl ?? DEFAULT_AVATAR} alt="" width="64" height="64" loading="lazy" />
                  <span>{m.displayName}</span>
                </li>
              </ol>
            ) : (
              <p className="note">Não definido</p>
            )}
          </section>
        )}
      </div>
      <section className="tuna-info__block" aria-labelledby="tuna-path-title">
        <h3 id="tuna-path-title" className="tuna-info__title">
          Percurso na Tuna
        </h3>
        {m.timeline.length > 0 ? <TunaPath items={m.timeline} /> : <p className="note">Ainda sem percurso registado.</p>}
      </section>
      {m.state && <TunaState state={m.state} />}
    </div>
  );
}

/** An instrument's Naipes picture in a circle; the naipes icon when the instrument has none. */
export function InstrumentPic({ url, large }: { url?: string; large?: boolean }) {
  return (
    <span className={large ? 'instrument-pic instrument-pic--lg' : 'instrument-pic'} aria-hidden="true">
      {url ? <img src={url} alt="" loading="lazy" /> : <Icon name="naipes" />}
    </span>
  );
}

/** "Estado na Tuna": the server's status, progress and the last rehearsal / event, as in the old profile. */
function TunaState({ state }: { state: MemberState }) {
  return (
    <section className="tuna-info__block" aria-labelledby="tuna-state-title">
      <h3 id="tuna-state-title" className="tuna-info__title">
        Estado na Tuna
      </h3>
      {state.retired !== null && (
        <div className="tuna-state">
          <MemberBadge kind={state.retired ? 'retired' : 'active'}>{state.retired ? 'REFORMADO' : 'NO ATIVO'}</MemberBadge>
          {progressText(state.progress, state.retired) && <span className="tuna-state__progress">{progressText(state.progress, state.retired)}</span>}
          {state.progress && state.progressMonths !== null && state.progressTotalMonths ? (
            <progress className="member-progress__bar" max={state.progressTotalMonths} value={state.progressMonths} aria-label="Progresso de atividade" />
          ) : null}
          {state.encourage && <p className="note">Falta pouco: com uma atividade ainda este mês, regressas ao ativo.</p>}
        </div>
      )}
      <dl className="tuna-stats">
        <div className="tuna-stat">
          <Icon name="music" />
          <dt>Último ensaio</dt>
          <dd>{shortDate(state.lastRehearsal) ?? '—'}</dd>
        </div>
        <div className="tuna-stat tuna-stat--event">
          <Icon name="calendar" />
          <dt>Última atuação</dt>
          <dd>{shortDate(state.lastEvent) ?? '—'}</dd>
        </div>
      </dl>
      <ActivityList activities={state.activities} />
    </section>
  );
}

const LOCKED_DATE = 'Esta data já não muda aqui. Se estiver errada, fala com o Magister.';

/** Stable, so the padrinho search does not restart on every render. */
const myMentorSearch = (q: string) => memberAreaApi.mentors(q);

export function TunaSection({ profile, onSaved }: { profile: MyProfile; onSaved: (p: MyProfile) => void }) {
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const t = profile.tuna;
  const editable = t.showMentor || t.showLeitao || t.showCaloiro || t.showTuno;

  return (
    <Section
      id="tuna"
      title="Informação da Tuna"
      icon="music"
      action={
        editable &&
        !editing && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setEditing(true), setSaved(false))}>
            <Icon name="pencil" />
            Editar
          </button>
        )
      }
    >
      {editing ? (
        <TunaForm
          profile={profile}
          onCancel={() => setEditing(false)}
          onSaved={(next) => {
            onSaved(next);
            setEditing(false);
            setSaved(true);
          }}
        />
      ) : (
        <>
          <TunaInfo profile={profile} />
          <SavedNote show={saved} />
        </>
      )}
    </Section>
  );
}

function TunaForm({ profile, onCancel, onSaved }: { profile: MyProfile; onCancel: () => void; onSaved: (p: MyProfile) => void }) {
  const [form, setForm] = useState<TunaInput>(() => tunaOf(profile));
  const [mentorName, setMentorName] = useState(profile.tuna.mentorName);
  const f = useFormState();
  const t = profile.tuna;
  const set = (next: Partial<TunaInput>) => setForm({ ...form, ...next });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    f.setBusy(true);
    f.setBanner(undefined);
    const o = await memberAreaApi.saveTuna(form);
    f.setBusy(false);
    if (o.kind === 'ok') onSaved(o.data);
    else if (o.kind === 'invalid') f.setErrors(o.errors);
    else {
      f.setErrors({});
      f.setBanner(problem(o, 'guardar'));
    }
  };

  return (
    <form className="form profile-form" onSubmit={submit} noValidate>
      {f.banner && (
        <p className="form__banner" role="alert">
          {f.banner}
        </p>
      )}
      {t.showMentor && (
        <MentorPicker
          mentorId={form.mentorId}
          mentorName={mentorName}
          error={f.errors.mentorId}
          search={myMentorSearch}
          onChange={(mentorId, name) => {
            set({ mentorId });
            setMentorName(name);
          }}
        />
      )}
      {(t.showLeitao || t.showCaloiro || t.showTuno) && (
        <div className="member-form__dates">
          {t.showLeitao && (
            <MonthYear
              label="Entrada a Leitão"
              year={form.yearLeitao}
              month={form.monthLeitao}
              locked={t.lockedDates.leitao}
              lockedHint={LOCKED_DATE}
              error={f.errors.leitao}
              onChange={(yearLeitao, monthLeitao) => set({ yearLeitao, monthLeitao })}
            />
          )}
          {t.showCaloiro && (
            <MonthYear
              label="Passagem a Caloiro"
              year={form.yearCaloiro}
              month={form.monthCaloiro}
              locked={t.lockedDates.caloiro}
              lockedHint={LOCKED_DATE}
              error={f.errors.caloiro}
              onChange={(yearCaloiro, monthCaloiro) => set({ yearCaloiro, monthCaloiro })}
            />
          )}
          {t.showTuno && (
            <MonthYear
              label="Passagem a Tuno"
              year={form.yearTuno}
              month={form.monthTuno}
              locked={t.lockedDates.tuno}
              lockedHint={LOCKED_DATE}
              error={f.errors.tuno}
              onChange={(yearTuno, monthTuno) => set({ yearTuno, monthTuno })}
            />
          )}
        </div>
      )}
      <p className="form__hint">Uma data completa (mês e ano) fica guardada de vez; confirma-a antes de guardar.</p>
      <FormButtons busy={f.busy} onCancel={onCancel} />
    </form>
  );
}

// ---------- Instrumentos ----------
