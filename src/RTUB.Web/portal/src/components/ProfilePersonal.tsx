// The /profile personal-data section and its edit form.
import { useId, useState, type FormEvent } from 'react';
import { Icon } from './icons';
import { memberAreaApi, type MyProfile, type PersonalInput } from '../data/memberAreaApi';
import { Fact, FormButtons, SavedNote, Section, breakableEmail, problem, useFormState } from './ProfileShared';

const personalOf = (p: MyProfile): PersonalInput => {
  const { nicknameLocked: _, ...rest } = p.personal;
  return rest;
};

export function PersonalSection({ profile, onSaved }: { profile: MyProfile; onSaved: (p: MyProfile) => void }) {
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
          <dl className="profile-facts">
            <Fact icon="person" label="Nome">{[p.firstName, p.lastName].filter(Boolean).join(' ') || null}</Fact>
            <Fact icon="star" label="Nome de tuna" accent>
              {p.nickname}
            </Fact>
            <Fact icon="calendar" label="Data de nascimento">
              {m.birthDate && (
                <>
                  {m.birthDate}
                  {m.age !== null && <span className="profile-fact__tag">{m.age} anos</span>}
                </>
              )}
            </Fact>
            <Fact icon="geo" label="Cidade">{p.city}</Fact>
            <Fact icon="envelope" label="Email" wide>
              {p.email && <span className="profile-fact__email">{breakableEmail(p.email)}</span>}
            </Fact>
            <Fact icon="phone" label="Contacto">{p.phoneNumber}</Fact>
            <Fact icon="journal" label="Curso">{p.degree}</Fact>
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
