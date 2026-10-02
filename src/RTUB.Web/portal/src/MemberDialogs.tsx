import { useEffect, useId, useState, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { DEFAULT_AVATAR, membersApi, shortDate, type ActiveMember, type Birthday, type MemberDetail } from './membersApi';

// The old /members modals (React track 017), read-only. The server decides who sees what (MemberDirectoryService).

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

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="member-field">
      <dt>{label}</dt>
      <dd>{children ?? <span className="note">Não definido</span>}</dd>
    </div>
  );
}

/** "Detalhes do Membro": the same sections and fields the old modal showed any signed-in member. */
export function MemberDialog({ memberId, onClose }: { memberId: string; onClose: () => void }) {
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
        <MemberBody m={member} />
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
          <Field label="Percurso na tuna">
            {m.timeline.length > 0 ? (
              <ol className="member-timeline">
                {m.timeline.map((t, i) => (
                  <li key={i} className={`member-timeline__item member-timeline__item--${t.accent}${t.state ? ` is-${t.state}` : ''}`}>
                    <span className="member-timeline__label">{t.label}</span>
                    <span className="member-timeline__years">{t.years}</span>
                    {t.notes && <span className="note">{t.notes}</span>}
                  </li>
                ))}
              </ol>
            ) : null}
          </Field>
        </dl>
      </section>

      {m.state && (
        <section className="member-section" aria-label="Estado na tuna">
          <h3 className="member-section__title">Estado na tuna</h3>
          <dl className="member-fields">
            {m.state.retired !== null && (
              <Field label="Estado atual">
                <span className={m.state.retired ? 'member-badge member-badge--retired' : 'member-badge member-badge--active'}>
                  {m.state.retired ? 'REFORMADO' : 'NO ATIVO'}
                </span>
                {m.state.progress && <span className="member-progress">{m.state.progress}</span>}
                {m.state.progress && m.state.progressMonths !== null && m.state.progressTotalMonths ? (
                  <progress className="member-progress__bar" max={m.state.progressTotalMonths} value={m.state.progressMonths} />
                ) : null}
              </Field>
            )}
            {m.state.encourage && <p className="notice member-encourage">Falta pouco: com uma atividade ainda este mês, regressas ao ativo.</p>}
            <Field label="Último ensaio">{shortDate(m.state.lastRehearsal)}</Field>
            <Field label="Última atuação">{shortDate(m.state.lastEvent)}</Field>
          </dl>
          <details className="who__more">
            <summary>Ver atuações e ensaios detalhados</summary>
            {m.state.activities.length === 0 ? (
              <p className="note">Nenhuma atividade registada</p>
            ) : (
              <ul className="member-activities">
                {m.state.activities.map((a, i) => (
                  <li key={i}>
                    <Icon name={a.isRehearsal ? 'music' : 'calendar'} />
                    <span>
                      <strong>{a.name}</strong>
                      <small>
                        {new Date(a.date).toLocaleDateString('pt-PT')} · {a.type}
                      </small>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </details>
        </section>
      )}
    </div>
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

/** The old "Gestão de Membros Ativos", read-only: who is active or retired, and since when. */
export function ActiveMembersDialog({ onClose }: { onClose: () => void }) {
  const [list, setList] = useState<ActiveMember[] | null>();
  const [status, setStatus] = useState('');
  const search = useSearch();
  const ids = { q: useId(), status: useId() };

  useEffect(() => {
    membersApi.active(status, search.q).then((o) => setList(o.kind === 'ok' ? o.data : null));
  }, [status, search.q]);

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
                <small>
                  {m.instrument ?? '-'} · Último ensaio {shortDate(m.lastRehearsal) ?? '-'} · Última atuação {shortDate(m.lastEvent) ?? '-'}
                </small>
                {m.progress && <small className="member-progress">{m.progress}</small>}
                {m.encourage && <small className="member-encourage">Falta pouco: com uma atividade ainda este mês, regressas ao ativo.</small>}
              </span>
              <span className={m.retired ? 'member-badge member-badge--retired' : 'member-badge member-badge--active'}>
                {m.retired ? 'REFORMADO' : 'NO ATIVO'}
              </span>
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
                    <span key={x.kind} className={`member-badge member-badge--${x.kind}`}>
                      {x.label}
                    </span>
                  ))}
                </span>
              </span>
              <span className={b.date === today ? 'member-badge member-badge--active' : 'member-badge'}>
                {b.date === today ? 'Hoje' : b.birthday}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  );
}
