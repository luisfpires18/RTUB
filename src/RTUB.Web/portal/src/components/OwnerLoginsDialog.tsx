// Owner tracing logins dialog.
import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { dateTime, ownerApi, ownerProblem, type Logins } from '../data/ownerApi';
import { matches, SearchBox } from './OwnerUi';
import { Pager } from './ui/Pager';
import { Avatar } from './ui/Avatar';
import { Banner } from './ui/Banner';
import { FormField } from './ui/FormField';
import { CloseFooter } from './ui/DialogFooter';

export const today = () => new Date().toLocaleDateString('sv-SE');
/** The Portugal day (yyyy-MM-dd) of a UTC stamp, as the server buckets LoginEvents. */
const dayOf = (iso: string) => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date(iso));

export function LoginsDialog({ onClose }: { onClose: () => void }) {
  const id = useId();
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(today);
  const [logins, setLogins] = useState<Logins | string>();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(12);

  const load = async () => {
    setLogins(undefined);
    const o = await ownerApi.logins(from, to);
    setLogins(o.kind === 'ok' ? o.data : ownerProblem(o, 'carregar os logins'));
    setPage(1);
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const records = typeof logins === 'object' ? logins.records : logins;
  const filtered = Array.isArray(records) ? records.filter((r) => matches(q, r.nickname, r.fullName, r.userName)) : [];
  // Sign-in history (LoginEvents) has no rows from before it was deployed; earlier days only know each account's last access.
  const historyDay = typeof logins === 'object' && logins.historySince ? dayOf(logins.historySince) : null;
  const partial = typeof logins === 'object' && (historyDay === null || from < historyDay);

  return (
    <Dialog
      title="Registos de login"
      size="lg"
      onClose={onClose}
      footer={<CloseFooter onClose={onClose} />}
    >
      <div className="owner-filters__grid">
        <FormField id={`${id}-from`} label="Data de início">
          <input id={`${id}-from`} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </FormField>
        <FormField id={`${id}-to`} label="Data de fim">
          <input id={`${id}-to`} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </FormField>
      </div>
      <div className="owner-actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={load}>
          Carregar
        </button>
      </div>
      {partial && (
        <p className="note">
          {historyDay
            ? `O histórico de sessões começa a ${historyDay.split('-').reverse().join('/')}; antes disso só existe o último acesso de cada conta.`
            : 'Ainda não há histórico de sessões; só existe o último acesso de cada conta.'}
        </p>
      )}
      {records === undefined ? (
        <p className="note" role="status">
          A carregar…
        </p>
      ) : typeof records === 'string' ? (
        <Banner text={records} />
      ) : records.length === 0 ? (
        <p className="note">Sem logins no período escolhido.</p>
      ) : (
        <>
          <SearchBox
            label="Procurar login"
            placeholder="Procurar por nome ou username"
            value={q}
            onChange={(v) => {
              setQ(v);
              setPage(1);
            }}
          />
          <ul className="owner-people__list owner-logins">
            {filtered.slice((page - 1) * pageSize, page * pageSize).map((r) => (
              <li key={r.userId} className="owner-person">
                <Avatar className="owner-person__avatar" src={r.avatarUrl} size={32} />
                <span className="owner-person__name">
                  <strong>{r.nickname}</strong>
                  <span className="note">{r.fullName}</span>
                  {r.fromHistory ? (
                    <span className="note">
                      {r.signIns} {r.signIns === 1 ? 'início de sessão' : 'inícios de sessão'} · {r.activeDays}{' '}
                      {r.activeDays === 1 ? 'dia ativo' : 'dias ativos'}
                    </span>
                  ) : (
                    <span className="note">Só o último acesso</span>
                  )}
                  {r.signInTimes.length > 0 && (
                    <details className="owner-login__times">
                      <summary>Ver sessões</summary>
                      <ul>
                        {r.signInTimes.map((t) => (
                          <li key={t}>{dateTime(t)}</li>
                        ))}
                      </ul>
                    </details>
                  )}
                </span>
                <span className="note owner-login__when">
                  {r.fromHistory && r.firstAt !== r.lastLoginAt && (
                    <>
                      {dateTime(r.firstAt)} →<br />
                    </>
                  )}
                  {dateTime(r.lastLoginAt)}
                </span>
              </li>
            ))}
          </ul>
          {filtered.length === 0 && <p className="note">Nenhum login corresponde à pesquisa.</p>}
          {filtered.length > pageSize && (
            <Pager
              total={filtered.length}
              itemLabel="logins"
              page={page}
              pageSize={pageSize}
              sizes={[12, 24, 36, 48, 60]}
              onPage={setPage}
              onPageSize={(s) => {
                setPageSize(s);
                setPage(1);
              }}
            />
          )}
        </>
      )}
    </Dialog>
  );
}
