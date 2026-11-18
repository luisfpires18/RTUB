// Member list dialogs: active members (with search) and upcoming birthdays.
import { useEffect, useId, useState, type ReactNode } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { membersApi, shortDate, type ActiveMember, type Birthday } from '../data/membersApi';
import { MemberFace, progressText } from './MemberDialogs';

function useSearch(initial = '') {
  const [text, setText] = useState(initial);
  const [q, setQ] = useState(initial);
  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);
  return { text, setText, q };
}

/** "Gestão de Membros Ativos": who is active or retired, and since when; Admin/Owner act on a row. */
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

/** "Aniversários": the birthdays still to come this year, soonest first. */
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
