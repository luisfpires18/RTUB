import { useEffect, useId, useRef, useState, type FormEvent, type ReactNode, type Ref } from 'react';
import { getCurrentUser, type CurrentUser } from './api';
import { Loading, useCurrentUser } from './App';
import { loginToProfile, portal } from './content';
import { Cropper } from './Cropper';
import type { Outcome } from './eventsApi';
import { Icon, type IconName } from './icons';
import { Field, MemberFace, StateSection, Timeline } from './MemberDialogs';
import { MentorPicker, MonthYear } from './MemberManage';
import { memberAreaApi, type MyProfile, type PersonalInput, type TunaInput } from './memberAreaApi';
import { DEFAULT_AVATAR, shortDate, type MemberInstrument } from './membersApi';
import { SignOutButton } from './MemberShell';

/**
 * /profile - the members-only corner of a public portal. RTUB has no public accounts: the tuna creates its members'
 * logins, so a visitor is told that first and keeps the public portal one tap away. Signing in is the React /login.
 *
 * Signed in, it is the member's own profile (task 032; was the Blazor /member/profile): who they are, their rank, and
 * the sections they edit - Pessoal, Tuna, Instrumentos, Foto, Notificações por email and Segurança. The server decides
 * every rule (MyProfileService); a disabled field here is only a convenience. The members' menu is the member shell.
 */
export default function Profile() {
  const { user, failed, retry } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  useEffect(() => {
    document.title = signedIn ? 'O meu perfil · RTUB' : 'Área de membros · RTUB';
  }, [signedIn]);

  return (
    <section className="page wrap profile-page" aria-labelledby="profile-title">
      <header className="page__head">
        <p className="eyebrow">{user ? 'Área de membros' : 'RTUB'}</p>
        <h1 id="profile-title" className="page__title">
          {!user ? 'Área de membros' : signedIn ? 'O meu perfil' : 'Área reservada a membros'}
        </h1>
      </header>

      {failed ? (
        <div className="notice" role="status">
          <p>Não conseguimos confirmar a sessão agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={retry}>
            Tentar novamente
          </button>
        </div>
      ) : !user ? (
        <SessionSkeleton />
      ) : user.authenticated ? (
        <MyProfilePage user={user} />
      ) : (
        <div className="members">
          <div className="members__main">
            <SignedOut />
          </div>
          <PublicShortcuts />
        </div>
      )}
    </section>
  );
}

/** Same footprint as the account card, so nothing jumps when the session arrives. */
function SessionSkeleton() {
  return (
    <div className="account__card account__card--skeleton" role="status">
      <span className="skeleton skeleton--avatar" aria-hidden="true" />
      <span className="skeleton__lines" aria-hidden="true">
        <span className="skeleton skeleton--line" />
        <span className="skeleton skeleton--line skeleton--short" />
      </span>
      <span className="sr-only">A verificar a sessão…</span>
    </div>
  );
}

// ---------- signed in: the profile editor ----------

type Member = Extract<CurrentUser, { authenticated: true }>;

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'signin'
      ? 'A sessão terminou. Entra outra vez.'
      : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

function MyProfilePage({ user }: { user: Member }) {
  const [profile, setProfile] = useState<MyProfile | 'failed'>();
  const security = useRef<HTMLElement>(null);

  useEffect(() => {
    memberAreaApi.profile().then((o) => setProfile(o.kind === 'ok' ? o.data : 'failed'));
  }, []);

  if (profile === undefined) return <Loading label="A carregar o perfil…" />;
  if (profile === 'failed') {
    return (
      <div className="notice" role="status">
        <p>Não foi possível carregar o perfil. Tenta outra vez daqui a pouco.</p>
      </div>
    );
  }

  const m = profile.member;
  const goToSecurity = () => {
    security.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    security.current?.querySelector('input')?.focus({ preventScroll: true });
  };

  return (
    <div className="profile">
      {profile.requirePasswordChange && (
        <div className="notice notice--warning profile__warning" role="alert">
          <p>
            <strong>Palavra-passe temporária.</strong> Escolhe já uma palavra-passe tua.
          </p>
          <button type="button" className="btn btn--primary btn--sm" onClick={goToSecurity}>
            <Icon name="lock" />
            Mudar a palavra-passe
          </button>
        </div>
      )}

      <div className="profile__top">
        <div className="account__card profile__identity">
          <MemberFace avatarUrl={m.avatarUrl} size={96} />
          <div className="account__who">
            <p className="account__name">{m.displayName}</p>
            {m.fullName && <p className="account__full">{m.fullName}</p>}
            {m.badges.length + m.positions.length > 0 ? (
              <span className="member-badges">
                {m.badges.map((b) => (
                  <span key={b.kind} className={`member-badge member-badge--${b.kind}`}>
                    {b.label}
                  </span>
                ))}
                {m.positions.map((p) => (
                  <span key={p} className="member-badge member-badge--position">
                    {p}
                  </span>
                ))}
              </span>
            ) : (
              <p className="note">Ainda sem categoria atribuída.</p>
            )}
            <p className="note profile__login">Entraste como {user.displayName}.</p>
          </div>
        </div>
        <RankCard profile={profile} />
      </div>

      <div className="account__actions profile__actions">
        <a className="btn btn--ghost btn--sm" href={portal.myEnrollments}>
          <Icon name="calendar" />
          As minhas inscrições
        </a>
        <SignOutButton className="btn btn--ghost btn--sm" />
      </div>

      <PersonalSection profile={profile} onSaved={setProfile} />
      <TunaSection profile={profile} onSaved={setProfile} />
      <InstrumentsSection profile={profile} onChange={(instruments) => setProfile({ ...profile, instruments })} />
      <PhotoSection profile={profile} onChange={(avatarUrl) => setProfile({ ...profile, member: { ...m, avatarUrl } })} />
      <SubscriptionSection profile={profile} onChange={(subscribed) => setProfile({ ...profile, subscribed })} />
      <SecuritySection ref={security} onChanged={() => setProfile({ ...profile, requirePasswordChange: false })} />
    </div>
  );
}

function Section({
  id,
  title,
  icon,
  action,
  sectionRef,
  children,
}: {
  id: string;
  title: string;
  icon: IconName;
  action?: ReactNode;
  sectionRef?: Ref<HTMLElement>;
  children: ReactNode;
}) {
  return (
    <section ref={sectionRef} className="profile-section" id={id} aria-labelledby={`${id}-title`}>
      <header className="profile-section__head">
        <h2 id={`${id}-title`} className="profile-section__title">
          <Icon name={icon} />
          {title}
        </h2>
        {action}
      </header>
      {children}
    </section>
  );
}

/** "Nível de alcoolismo": the leaderboard's level, XP and the way to the next level. */
function RankCard({ profile }: { profile: MyProfile }) {
  const r = profile.rank;
  return (
    <div className="profile-rank" aria-label="Nível">
      <p className="profile-rank__level">
        <span>Nível</span>
        <strong>{r.level}</strong>
      </p>
      <div className="profile-rank__body">
        <p className="profile-rank__name">{r.name}</p>
        <p className="note">{r.xp} XP no total</p>
        {r.maxLevel ? (
          <p className="profile-rank__next">Chegaste ao nível máximo.</p>
        ) : (
          <>
            <progress className="profile-rank__bar" max={100} value={r.percent} aria-label="Progresso para o próximo nível" />
            <p className="note">
              {r.xpInLevel} / {r.xpForLevel} XP · faltam {r.xpToNext} XP para o nível {r.level + 1}
              {r.nextName && ` (${r.nextName})`}
            </p>
          </>
        )}
        <a className="profile-rank__link" href={portal.leaderboard}>
          Ver a classificação
        </a>
      </div>
    </div>
  );
}

function useFormState() {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  return { errors, setErrors, banner, setBanner, busy, setBusy, field, error };
}

function SavedNote({ show }: { show: boolean }) {
  return show ? (
    <p className="profile-saved" role="status">
      <Icon name="check" />
      Guardado.
    </p>
  ) : null;
}

// ---------- Pessoal ----------

const personalOf = (p: MyProfile): PersonalInput => {
  const { nicknameLocked: _, ...rest } = p.personal;
  return rest;
};

function PersonalSection({ profile, onSaved }: { profile: MyProfile; onSaved: (p: MyProfile) => void }) {
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const p = profile.personal;
  const m = profile.member;

  return (
    <Section
      id="personal"
      title="Pessoal"
      icon="person"
      action={
        !editing && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setEditing(true), setSaved(false))}>
            <Icon name="pencil" />
            Editar
          </button>
        )
      }
    >
      {editing ? (
        <PersonalForm
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
          <dl className="member-fields">
            <Field label="Nome">{[p.firstName, p.lastName].filter(Boolean).join(' ') || null}</Field>
            <Field label="Nome de tuna">{p.nickname}</Field>
            <Field label="Email">{p.email}</Field>
            <Field label="Contacto">{p.phoneNumber}</Field>
            <Field label="Data de nascimento">{m.birthDate && `${m.birthDate}${m.age !== null ? ` (${m.age} anos)` : ''}`}</Field>
            <Field label="Cidade">{p.city}</Field>
            <Field label="Curso">{p.degree}</Field>
          </dl>
          <SavedNote show={saved} />
        </>
      )}
    </Section>
  );
}

function PersonalForm({ profile, onCancel, onSaved }: { profile: MyProfile; onCancel: () => void; onSaved: (p: MyProfile) => void }) {
  const [form, setForm] = useState<PersonalInput>(() => personalOf(profile));
  const f = useFormState();
  const id = useId();
  const locked = profile.personal.nicknameLocked;
  const set = (next: Partial<PersonalInput>) => setForm({ ...form, ...next });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    f.setBusy(true);
    f.setBanner(undefined);
    const o = await memberAreaApi.savePersonal(form);
    f.setBusy(false);
    if (o.kind === 'ok') onSaved(o.data);
    else if (o.kind === 'invalid') f.setErrors(o.errors);
    else {
      f.setErrors({});
      f.setBanner(problem(o, 'guardar'));
    }
  };

  const text = (key: 'firstName' | 'lastName' | 'phoneNumber' | 'city' | 'degree', label: string, max?: number, type = 'text', autoComplete?: string) => (
    <div className={f.field(key)}>
      <label htmlFor={`${id}-${key}`}>{label}</label>
      <input
        id={`${id}-${key}`}
        type={type}
        maxLength={max}
        autoComplete={autoComplete}
        value={form[key] ?? ''}
        onChange={(e) => set({ [key]: e.target.value })}
        aria-invalid={f.errors[key] ? true : undefined}
      />
      {f.error(key)}
    </div>
  );

  return (
    <form className="form profile-form" onSubmit={submit} noValidate>
      {f.banner && (
        <p className="form__banner" role="alert">
          {f.banner}
        </p>
      )}
      <div className="form__row">
        {text('firstName', 'Primeiro nome', 80, 'text', 'given-name')}
        {text('lastName', 'Último nome', 80, 'text', 'family-name')}
      </div>
      <div className="form__row">
        <div className={f.field('nickname')}>
          <label htmlFor={`${id}-nickname`}>Nome de tuna</label>
          <input
            id={`${id}-nickname`}
            type="text"
            maxLength={80}
            value={form.nickname ?? ''}
            disabled={locked}
            onChange={(e) => set({ nickname: e.target.value })}
            aria-invalid={f.errors.nickname ? true : undefined}
          />
          {locked && <p className="form__hint">O nome de tuna chega com a passagem a Caloiro.</p>}
          {f.error('nickname')}
        </div>
        <div className={f.field('email')}>
          <label htmlFor={`${id}-email`}>Email</label>
          <input
            id={`${id}-email`}
            type="email"
            autoComplete="email"
            value={form.email ?? ''}
            onChange={(e) => set({ email: e.target.value })}
            aria-invalid={f.errors.email ? true : undefined}
          />
          {f.error('email')}
        </div>
      </div>
      <div className="form__row">
        {text('phoneNumber', 'Contacto', 80, 'tel', 'tel')}
        <div className={f.field('dateOfBirth')}>
          <label htmlFor={`${id}-birth`}>Data de nascimento</label>
          <input id={`${id}-birth`} type="date" value={form.dateOfBirth ?? ''} onChange={(e) => set({ dateOfBirth: e.target.value || null })} />
          {f.error('dateOfBirth')}
        </div>
      </div>
      <div className="form__row">
        {text('city', 'Cidade', 100, 'text', 'address-level2')}
        {text('degree', 'Curso')}
      </div>
      <p className="form__hint">A cidade é o que te põe no mapa de membros (só a cidade, nunca a morada).</p>
      <FormButtons busy={f.busy} onCancel={onCancel} />
    </form>
  );
}

function FormButtons({ busy, onCancel, label = 'Guardar' }: { busy: boolean; onCancel?: () => void; label?: string }) {
  return (
    <div className="profile-form__buttons">
      {onCancel && (
        <button type="button" className="btn btn--ghost" onClick={onCancel} disabled={busy}>
          Cancelar
        </button>
      )}
      <button type="submit" className="btn btn--primary" disabled={busy}>
        {busy && <span className="spinner spinner--small" aria-hidden="true" />}
        {label}
      </button>
    </div>
  );
}

// ---------- Tuna ----------

const tunaOf = (p: MyProfile): TunaInput => ({
  mentorId: p.tuna.mentorId,
  yearLeitao: p.tuna.yearLeitao,
  monthLeitao: p.tuna.monthLeitao,
  yearCaloiro: p.tuna.yearCaloiro,
  monthCaloiro: p.tuna.monthCaloiro,
  yearTuno: p.tuna.yearTuno,
  monthTuno: p.tuna.monthTuno,
});

const LOCKED_DATE = 'Esta data já não muda aqui. Se estiver errada, fala com o Magister.';
const MONTHS = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];
const monthYear = (year: number | null, month: number | null) => (year ? `${month ? `${MONTHS[month - 1]} de ` : ''}${year}` : null);

/** Stable, so the padrinho search does not restart on every render. */
const myMentorSearch = (q: string) => memberAreaApi.mentors(q);

function TunaSection({ profile, onSaved }: { profile: MyProfile; onSaved: (p: MyProfile) => void }) {
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const t = profile.tuna;
  const m = profile.member;
  const editable = t.showMentor || t.showLeitao || t.showCaloiro || t.showTuno;

  return (
    <Section
      id="tuna"
      title="Tuna"
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
          <dl className="member-fields">
            {t.showMentor && <Field label="Padrinho">{t.mentorName}</Field>}
            {t.showLeitao && <Field label="Entrada a Leitão">{monthYear(t.yearLeitao, t.monthLeitao)}</Field>}
            {t.showCaloiro && <Field label="Passagem a Caloiro">{monthYear(t.yearCaloiro, t.monthCaloiro)}</Field>}
            {t.showTuno && <Field label="Passagem a Tuno">{monthYear(t.yearTuno, t.monthTuno)}</Field>}
            <Field label="Percurso na tuna">{m.timeline.length > 0 ? <Timeline items={m.timeline} /> : null}</Field>
          </dl>
          <SavedNote show={saved} />
        </>
      )}
      {m.state && <StateSection state={m.state} />}
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

function InstrumentsSection({ profile, onChange }: { profile: MyProfile; onChange: (list: MemberInstrument[]) => void }) {
  const id = useId();
  const [picked, setPicked] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const list = profile.instruments;
  const available = profile.instrumentOptions.filter((o) => !list.some((i) => i.instrument === o.value));

  const run = async (action: () => Promise<Outcome<MemberInstrument[]>>) => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data);
      setError(undefined);
    } else setError(problem(o, 'guardar o instrumento'));
  };

  return (
    <Section id="instruments" title="Instrumentos" icon="music">
      <div className={error ? 'form__field form__field--error' : 'form__field'}>
        <label htmlFor={id}>Juntar um instrumento</label>
        <span className="member-form__pair">
          <span className="control control--select">
            <select id={id} value={picked} onChange={(e) => setPicked(e.target.value)} disabled={busy}>
              <option value="">Escolher instrumento…</option>
              {available.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </span>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            onClick={() => {
              if (picked) run(() => memberAreaApi.addInstrument(picked));
              setPicked('');
            }}
            disabled={!picked || busy}
          >
            <Icon name="plus" />
            Adicionar
          </button>
        </span>
        {list.length > 0 ? (
          <ul className="member-rows member-instruments" aria-label="Os teus instrumentos">
            {list.map((i) => (
              <li key={i.id} className="member-row">
                <label className="form__check">
                  <input
                    type="radio"
                    name={`${id}-primary`}
                    checked={i.primary}
                    onChange={() => run(() => memberAreaApi.primaryInstrument(i.id))}
                    disabled={busy}
                  />
                  {i.label}
                  {i.primary && <span className="member-badge member-badge--active">Principal</span>}
                </label>
                <button type="button" className="icon-btn icon-btn--danger" onClick={() => run(() => memberAreaApi.removeInstrument(i.id))} disabled={busy}>
                  <Icon name="trash" />
                  <span className="sr-only">Remover {i.label}</span>
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <p className="note">Ainda não juntaste nenhum instrumento. O primeiro fica como principal.</p>
        )}
        {error && <p className="form__error">{error}</p>}
      </div>
    </Section>
  );
}

// ---------- Foto ----------

const MAX_PHOTO = 10 * 1024 * 1024;
const PHOTO_TYPES = ['image/webp', 'image/jpeg', 'image/png'];
const ROUND = {
  title: 'Recortar a foto',
  label: 'Pré-visualização da foto de perfil. Arraste ou use as setas para enquadrar.',
  note: 'A foto aparece num círculo: deixa a cara ao centro do quadrado.',
};

function PhotoSection({ profile, onChange }: { profile: MyProfile; onChange: (avatarUrl: string) => void }) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  const pick = (picked: File | undefined) => {
    setSaved(false);
    if (input.current) input.current.value = '';
    if (!picked) return;
    if (!PHOTO_TYPES.includes(picked.type)) return setError('Escolhe uma imagem WebP, JPEG ou PNG.');
    if (picked.size > MAX_PHOTO) return setError('A imagem não pode passar de 10 MB.');
    setError(undefined);
    setFile(picked);
  };

  const upload = async (photo: Blob) => {
    setFile(undefined);
    setBusy(true);
    const o = await memberAreaApi.photo(photo);
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data.avatarUrl);
      setSaved(true);
      getCurrentUser(true).catch(() => undefined); // the menu's avatar, on the next page
    } else setError(problem(o, 'guardar a foto'));
  };

  return (
    <Section id="photo" title="Foto" icon="images">
      <div className="profile-photo">
        <img
          className="profile-photo__img"
          src={profile.member.avatarUrl ?? DEFAULT_AVATAR}
          alt="A tua foto de perfil"
          width="120"
          height="120"
          onError={(e) => {
            if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
          }}
        />
        <div className="profile-photo__pick">
          <input
            ref={input}
            id={id}
            className="sr-only"
            type="file"
            accept={PHOTO_TYPES.join(',')}
            onChange={(e) => pick(e.target.files?.[0])}
            disabled={busy}
          />
          <label htmlFor={id} className={busy ? 'btn btn--ghost btn--sm is-disabled' : 'btn btn--ghost btn--sm'}>
            {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="upload" />}
            {busy ? 'A guardar…' : 'Escolher foto'}
          </label>
          <p className="note">WebP, JPEG ou PNG até 10 MB. Recortas em quadrado antes de guardar; a foto anterior é substituída.</p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
          <SavedNote show={saved} />
        </div>
      </div>
      {file && <Cropper file={file} aspect={1} outWidth={600} text={ROUND} onCancel={() => setFile(undefined)} onDone={(blob) => upload(blob)} />}
    </Section>
  );
}

// ---------- Notificações por email ----------

function SubscriptionSection({ profile, onChange }: { profile: MyProfile; onChange: (subscribed: boolean) => void }) {
  const id = useId();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string }>();

  const toggle = async (subscribed: boolean) => {
    setBusy(true);
    setMessage(undefined);
    const o = await memberAreaApi.subscription(subscribed);
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data.subscribed);
      setMessage({ ok: true, text: o.data.subscribed ? 'Vais receber os emails da RTUB.' : 'Deixas de receber os emails da RTUB.' });
    } else setMessage({ ok: false, text: problem(o, 'mudar esta preferência') });
  };

  return (
    <Section id="email" title="Notificações por email" icon="envelope">
      <label className="form__check profile-switch" htmlFor={id}>
        <input id={id} type="checkbox" role="switch" checked={profile.subscribed} onChange={(e) => toggle(e.target.checked)} disabled={busy} />
        Receber por email os avisos da tuna (atuações, cancelamentos e novidades)
      </label>
      {message && (
        <p className={message.ok ? 'profile-saved' : 'form__error'} role="status">
          {message.text}
        </p>
      )}
    </Section>
  );
}

// ---------- Segurança ----------

function SecuritySection({ ref, onChanged }: { ref: Ref<HTMLElement>; onChanged: () => void }) {
  const id = useId();
  const blank = { currentPassword: '', newPassword: '', confirmPassword: '' };
  const [form, setForm] = useState(blank);
  const [done, setDone] = useState(false);
  const f = useFormState();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    f.setBusy(true);
    f.setBanner(undefined);
    setDone(false);
    const o = await memberAreaApi.password(form.currentPassword, form.newPassword, form.confirmPassword);
    f.setBusy(false);
    if (o.kind === 'ok') {
      setForm(blank);
      f.setErrors({});
      setDone(true);
      onChanged();
    } else if (o.kind === 'invalid') f.setErrors(o.errors);
    else {
      f.setErrors({});
      f.setBanner(problem(o, 'mudar a palavra-passe'));
    }
  };

  const secret = (key: keyof typeof blank, label: string, autoComplete: string) => (
    <div className={f.field(key)}>
      <label htmlFor={`${id}-${key}`}>{label}</label>
      <input
        id={`${id}-${key}`}
        type="password"
        autoComplete={autoComplete}
        value={form[key]}
        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
        aria-invalid={f.errors[key] ? true : undefined}
      />
      {f.error(key)}
    </div>
  );

  return (
    <Section id="security" title="Segurança" icon="lock" sectionRef={ref}>
      <form className="form profile-form" onSubmit={submit} noValidate>
        {f.banner && (
          <p className="form__banner" role="alert">
            {f.banner}
          </p>
        )}
        {secret('currentPassword', 'Palavra-passe atual', 'current-password')}
        <div className="form__row">
          {secret('newPassword', 'Nova palavra-passe', 'new-password')}
          {secret('confirmPassword', 'Repetir a nova', 'new-password')}
        </div>
        <p className="form__hint">Pelo menos 8 caracteres. Continuas com a sessão aberta depois de mudar.</p>
        {done && (
          <p className="profile-saved" role="status">
            <Icon name="check" />
            Palavra-passe mudada.
          </p>
        )}
        <FormButtons busy={f.busy} label="Mudar a palavra-passe" />
      </form>
    </Section>
  );
}

// ---------- signed out ----------

function SignedOut() {
  return (
    <div className="account">
      <div className="account__card account__card--info">
        <Icon name="lock" className="account__lock" />
        <div className="account__who">
          <p>
            Esta área serve apenas os membros da RTUB. O acesso é criado pela própria tuna; não há registo público.
          </p>
          <p className="account__muted">Se és membro, entra para consultar ensaios, atuações e o resto da área de membros.</p>
        </div>
      </div>
      <div className="account__actions">
        <a className="btn btn--ghost" href={loginToProfile}>
          <Icon name="login" />
          Entrar como membro
        </a>
      </div>
    </div>
  );
}

const shortcuts: { href: string; icon: IconName; label: string }[] = [
  { href: portal.request, icon: 'send', label: 'Pedir uma atuação' },
  { href: '/#events', icon: 'calendar', label: 'Próximas atuações' },
  { href: portal.music, icon: 'music', label: 'Discografia' },
  { href: portal.roles, icon: 'bank', label: 'Órgãos Sociais' },
  { href: '/#gallery', icon: 'images', label: 'Galeria' },
];

/** The public portal, always within reach: the main paths for everyone who is not a member. */
function PublicShortcuts() {
  return (
    <nav className="shortcuts" aria-labelledby="shortcuts-title">
      <h2 id="shortcuts-title" className="request__title">
        No portal
      </h2>
      <ul>
        {shortcuts.map((s, i) => (
          <li key={s.href}>
            <a className={i === 0 ? 'shortcut shortcut--primary' : 'shortcut'} href={s.href}>
              <Icon name={s.icon} />
              {s.label}
              <Icon name="arrow" className="shortcut__go" />
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}
