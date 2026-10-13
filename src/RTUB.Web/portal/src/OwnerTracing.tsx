import { useEffect, useId, useState } from 'react';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { auditQuery, dateTime, ownerApi, ownerProblem, type AuditEntry, type AuditFilter, type AuditFilters, type LoginRecord } from './ownerApi';
import { Flash, matches, OwnerPage, OwnerPager, SearchBox, useOwnerLoad } from './OwnerUi';
import { MemberBadge } from './MemberBadge';

const EMPTY: AuditFilter = { user: '', excludeUser: '', entityType: '', action: '', critical: false, q: '', page: 1, pageSize: 20 };
const CONFIRMATION = 'DELETE ALL';

const actionTone = (action: string) =>
  action === 'Created' ? 'ok' : action === 'Modified' ? 'warn' : action === 'Deleted' || action === 'Role Removed' ? 'danger' : 'info';

/** Indented JSON when the changes are JSON, the raw text otherwise. */
const pretty = (changes: string | null) => {
  if (!changes) return 'Sem alterações registadas.';
  try {
    return JSON.stringify(JSON.parse(changes), null, 2);
  } catch {
    return changes;
  }
};

/**
 * /owner/tracing (task 035; was the Blazor page): the audit log with its filters, search, details, delete, "DELETE ALL"
 * truncation and JSON export, the logins of a period, and the system announcement. The server refuses anyone but the
 * Owner and checks the truncation confirmation itself.
 */
export default function OwnerTracing() {
  const [filter, setFilter] = useState(EMPTY);
  const [q, setQ] = useState('');
  const [filters, reloadFilters] = useOwnerLoad(() => ownerApi.auditFilters(), []);
  const [logs, reloadLogs] = useOwnerLoad(() => ownerApi.audit(filter), [filter]);
  const [open, setOpen] = useState<{ kind: 'details' | 'delete'; entry: AuditEntry } | { kind: 'truncate' | 'logins' }>();
  const [message, setMessage] = useState<{ text: string; error?: boolean }>();
  const id = useId();

  useEffect(() => {
    const timer = setTimeout(() => setFilter((f) => (f.q === q.trim() ? f : { ...f, q: q.trim(), page: 1 })), 300);
    return () => clearTimeout(timer);
  }, [q]);

  const set = (patch: Partial<AuditFilter>) => setFilter((f) => ({ ...f, ...patch, page: patch.page ?? 1 }));
  const state = filters && typeof filters === 'object' ? logs : filters;
  const lists = filters && typeof filters === 'object' ? filters : { users: [], entityTypes: [], actions: [] };

  return (
    <OwnerPage
      path={portal.ownerTracing}
      title="Auditoria"
      lead="O registo das atividades em todo o sistema, para responsabilização e resolução de problemas."
      icon="shield"
      state={state}
      loadingLabel="A carregar o registo…"
      actions={
        <>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'logins' })}>
            <Icon name="login" />
            Logins
          </button>
          <a className="btn btn--ghost btn--sm" href={`/api/owner/tracing/export${auditQuery({ ...filter, q: '', page: 0, pageSize: 0 })}`} download>
            <Icon name="download" />
            Exportar JSON
          </a>
          <button type="button" className="btn btn--danger btn--sm" onClick={() => setOpen({ kind: 'truncate' })}>
            <Icon name="trash" />
            Apagar registos
          </button>
        </>
      }
    >
      {(page) => (
        <>
          <Flash message={message} onDismiss={() => setMessage(undefined)} />
          <Announcement />

          <details className="owner-card owner-filters" open>
            <summary className="owner-card__title">
              <Icon name="search" />
              Filtros
            </summary>
            <div className="owner-filters__grid">
              <Select id={`${id}-user`} label="Utilizador" all="Todos os utilizadores" value={filter.user} items={lists.users} onChange={(user) => set({ user })} />
              <Select
                id={`${id}-exclude`}
                label="Excluir utilizador"
                all="Ninguém excluído"
                value={filter.excludeUser}
                items={lists.users}
                onChange={(excludeUser) => set({ excludeUser })}
              />
              <Select
                id={`${id}-entity`}
                label="Entidade"
                all="Todas as entidades"
                value={filter.entityType}
                items={lists.entityTypes}
                onChange={(entityType) => set({ entityType })}
              />
              <Select id={`${id}-action`} label="Ação" all="Todas as ações" value={filter.action} items={lists.actions} onChange={(action) => set({ action })} />
            </div>
            <SearchBox label="Procurar nas alterações" placeholder="Procurar nas alterações (ignora os outros filtros)" value={q} onChange={setQ} />
            <div className="owner-people__bar">
              <label className="form__check" htmlFor={`${id}-critical`}>
                <input id={`${id}-critical`} type="checkbox" checked={filter.critical} onChange={(e) => set({ critical: e.target.checked })} />
                Só ações críticas
              </label>
              <button
                type="button"
                className="btn btn--ghost btn--sm"
                onClick={() => {
                  setQ('');
                  setFilter({ ...EMPTY, pageSize: filter.pageSize });
                }}
              >
                <Icon name="xCircle" />
                Limpar filtros
              </button>
            </div>
          </details>

          <dl className="owner-stats">
            <div>
              <dt>Registos</dt>
              <dd>{page.total}</dd>
            </div>
            <div>
              <dt>Críticas (nesta página)</dt>
              <dd>{page.criticalOnPage}</dd>
            </div>
            <div>
              <dt>Entidades</dt>
              <dd>{lists.entityTypes.length}</dd>
            </div>
            <div>
              <dt>Utilizadores</dt>
              <dd>{lists.users.length}</dd>
            </div>
          </dl>

          {page.items.length === 0 ? (
            <div className="state" role="status">
              <Icon name="inbox" className="state__icon" />
              <p className="state__title">Nenhum registo corresponde aos filtros.</p>
            </div>
          ) : (
            <ul className="owner-logs">
              {page.items.map((entry) => (
                <li key={entry.id} className="owner-log">
                  <div className="owner-log__head">
                    <time dateTime={entry.timestamp}>{dateTime(entry.timestamp)}</time>
                    <MemberBadge>{entry.userName ?? 'Sistema'}</MemberBadge>
                    {entry.isCritical && (
                      <span className="owner-log__critical" title="Ação crítica">
                        <Icon name="warning" />
                        <span className="sr-only">Ação crítica</span>
                      </span>
                    )}
                  </div>
                  <p className="owner-log__entity">
                    <strong>{entry.entityType}</strong>
                    {(entry.entityDisplayName || entry.entityId !== null) && (
                      <span className="note">{entry.entityDisplayName || `ID: ${entry.entityId}`}</span>
                    )}
                  </p>
                  <div className="owner-log__foot">
                    <span className={`owner-action owner-action--${actionTone(entry.action)}`}>{entry.action}</span>
                    <span className="owner-log__buttons">
                      {entry.changes ? (
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'details', entry })}>
                          <Icon name="eye" />
                          Detalhes
                        </button>
                      ) : (
                        <span className="note">Sem detalhes</span>
                      )}
                      <button
                        type="button"
                        className="icon-btn icon-btn--sm icon-btn--danger"
                        onClick={() => setOpen({ kind: 'delete', entry })}
                        title="Apagar este registo"
                      >
                        <Icon name="trash" />
                        <span className="sr-only">Apagar este registo</span>
                      </button>
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
          <OwnerPager
            total={page.total}
            itemLabel="registos"
            page={page.page}
            pageSize={page.pageSize}
            sizes={[20, 40, 60, 80, 100]}
            onPage={(p) => set({ page: p })}
            onPageSize={(pageSize) => set({ pageSize })}
          />

          {open?.kind === 'details' && <DetailsDialog entry={open.entry} onClose={() => setOpen(undefined)} />}
          {open?.kind === 'delete' && (
            <DeleteDialog
              entry={open.entry}
              onClose={() => setOpen(undefined)}
              onDone={() => {
                setOpen(undefined);
                reloadLogs();
              }}
            />
          )}
          {open?.kind === 'truncate' && (
            <TruncateDialog
              filters={lists}
              total={page.total}
              onClose={() => setOpen(undefined)}
              onDone={(user) => {
                setOpen(undefined);
                setMessage({ text: user ? `Registos de ${user} apagados.` : 'Todos os registos foram apagados.' });
                setFilter({ ...EMPTY, pageSize: filter.pageSize });
                setQ('');
                reloadFilters();
              }}
            />
          )}
          {open?.kind === 'logins' && <LoginsDialog onClose={() => setOpen(undefined)} />}
        </>
      )}
    </OwnerPage>
  );
}

function Select({ id, label, all, value, items, onChange }: { id: string; label: string; all: string; value: string; items: string[]; onChange: (v: string) => void }) {
  return (
    <div className="form__field">
      <label htmlFor={id}>{label}</label>
      <span className="control control--select">
        <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">{all}</option>
          {items.map((i) => (
            <option key={i} value={i}>
              {i}
            </option>
          ))}
        </select>
      </span>
    </div>
  );
}

/** A message shown at once, as a pop-up, to the users connected right now; nothing is stored. */
function Announcement() {
  const id = useId();
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; error?: boolean }>();

  const send = async () => {
    setBusy(true);
    const o = await ownerApi.announce(text);
    setBusy(false);
    if (o.kind === 'ok') {
      setText('');
      setMessage({ text: 'Anúncio transmitido a todos os utilizadores ligados.' });
    } else setMessage({ text: ownerProblem(o, 'transmitir o anúncio'), error: true });
  };

  return (
    <section className="owner-card" aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="owner-card__title">
        <Icon name="megaphone" />
        Anúncio de sistema
      </h2>
      <Flash message={message} onDismiss={() => setMessage(undefined)} />
      <div className="form__field">
        <label htmlFor={`${id}-text`} className="sr-only">
          Anúncio
        </label>
        <textarea
          id={`${id}-text`}
          rows={2}
          maxLength={500}
          value={text}
          placeholder="Ex.: O servidor vai ser reiniciado para atualização. Por favor, aguarda."
          onChange={(e) => setText(e.target.value)}
        />
      </div>
      <div className="owner-actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={send} disabled={busy || !text.trim()}>
          {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="megaphone" />}
          {busy ? 'A enviar…' : 'Transmitir a todos'}
        </button>
      </div>
    </section>
  );
}

function DetailsDialog({ entry, onClose }: { entry: AuditEntry; onClose: () => void }) {
  return (
    <Dialog
      title={`Detalhes · ${entry.entityType}`}
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Fechar
        </button>
      }
    >
      <dl className="owner-dl">
        <dt>Data</dt>
        <dd>{dateTime(entry.timestamp)}</dd>
        <dt>Utilizador</dt>
        <dd>{entry.userName ?? 'Sistema'}</dd>
        <dt>Entidade</dt>
        <dd>{entry.entityType}</dd>
        {entry.entityDisplayName && (
          <>
            <dt>Nome</dt>
            <dd>{entry.entityDisplayName}</dd>
          </>
        )}
        <dt>ID da entidade</dt>
        <dd>{entry.entityId ?? '—'}</dd>
        <dt>Ação</dt>
        <dd>
          <span className={`owner-action owner-action--${actionTone(entry.action)}`}>{entry.action}</span>
        </dd>
        {entry.isCritical && (
          <>
            <dt>Crítica</dt>
            <dd>Sim</dd>
          </>
        )}
      </dl>
      <h3 className="owner-detail__heading">Alterações</h3>
      <pre className="owner-code">{pretty(entry.changes)}</pre>
    </Dialog>
  );
}

function DeleteDialog({ entry, onClose, onDone }: { entry: AuditEntry; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const remove = async () => {
    setBusy(true);
    const o = await ownerApi.deleteAudit(entry.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') onDone();
    else setError(ownerProblem(o, 'apagar o registo'));
  };

  return (
    <ConfirmDialog title="Apagar este registo?" confirmLabel="Apagar" busy={busy} error={error} onConfirm={remove} onClose={onClose}>
      <dl className="owner-dl">
        <dt>Entidade</dt>
        <dd>
          {entry.entityType}
          {entry.entityDisplayName ? ` · ${entry.entityDisplayName}` : entry.entityId !== null ? ` (ID: ${entry.entityId})` : ''}
        </dd>
        <dt>Ação</dt>
        <dd>{entry.action}</dd>
        <dt>Utilizador</dt>
        <dd>{entry.userName ?? 'Sistema'}</dd>
        <dt>Data</dt>
        <dd>{dateTime(entry.timestamp)}</dd>
      </dl>
      <p className="owner-danger">
        <Icon name="warning" />
        Não pode ser desfeito.
      </p>
    </ConfirmDialog>
  );
}

/** Every log, or one user's: only after typing DELETE ALL, which the server checks again. */
function TruncateDialog({
  filters,
  total,
  onClose,
  onDone,
}: {
  filters: AuditFilters;
  total: number;
  onClose: () => void;
  onDone: (user: string) => void;
}) {
  const id = useId();
  const [user, setUser] = useState('');
  const [typed, setTyped] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const truncate = async () => {
    setBusy(true);
    setError(undefined);
    const o = await ownerApi.truncateAudit(user, typed);
    setBusy(false);
    if (o.kind === 'ok') onDone(user);
    else setError(ownerProblem(o, 'apagar os registos'));
  };

  return (
    <Dialog
      title={user ? 'Apagar os registos de um utilizador' : 'Apagar todos os registos'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={truncate} disabled={busy || typed !== CONFIRMATION}>
            {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="trash" />}
            {user ? 'Apagar os registos dele' : 'Apagar tudo'}
          </button>
        </>
      }
    >
      <div className="form">
        <Select id={`${id}-user`} label="Utilizador (opcional)" all="Todos os utilizadores" value={user} items={filters.users} onChange={setUser} />
        <p className="owner-danger">
          <Icon name="warning" />
          {user
            ? `Apaga para sempre todos os registos de ${user}.`
            : `Apaga para sempre todos os registos (${total}) e o histórico de atividades do sistema.`}{' '}
          Não pode ser desfeito.
        </p>
        <div className="form__field">
          <label htmlFor={`${id}-confirm`}>
            Escreve <code>{CONFIRMATION}</code> para confirmar
          </label>
          <input id={`${id}-confirm`} value={typed} autoComplete="off" placeholder={CONFIRMATION} onChange={(e) => setTyped(e.target.value)} />
        </div>
        {error && (
          <p className="form__banner" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}

const today = () => new Date().toLocaleDateString('sv-SE');

function LoginsDialog({ onClose }: { onClose: () => void }) {
  const id = useId();
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(today);
  const [records, setRecords] = useState<LoginRecord[] | string>();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(12);

  const load = async () => {
    setRecords(undefined);
    const o = await ownerApi.logins(from, to);
    setRecords(o.kind === 'ok' ? o.data : ownerProblem(o, 'carregar os logins'));
    setPage(1);
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const filtered = Array.isArray(records) ? records.filter((r) => matches(q, r.nickname, r.fullName, r.userName)) : [];

  return (
    <Dialog
      title="Registos de login"
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Fechar
        </button>
      }
    >
      <div className="owner-filters__grid">
        <div className="form__field">
          <label htmlFor={`${id}-from`}>Data de início</label>
          <input id={`${id}-from`} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div className="form__field">
          <label htmlFor={`${id}-to`}>Data de fim</label>
          <input id={`${id}-to`} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </div>
      </div>
      <div className="owner-actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={load}>
          Carregar
        </button>
      </div>
      {records === undefined ? (
        <p className="note" role="status">
          A carregar…
        </p>
      ) : typeof records === 'string' ? (
        <p className="form__banner" role="alert">
          {records}
        </p>
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
          <ul className="owner-people__list">
            {filtered.slice((page - 1) * pageSize, page * pageSize).map((r) => (
              <li key={r.userId} className="owner-person">
                <img className="owner-person__avatar" src={r.avatarUrl} alt="" width="32" height="32" loading="lazy" />
                <span className="owner-person__name">
                  <strong>{r.nickname}</strong>
                  <span className="note">{r.fullName}</span>
                </span>
                <span className="note">{dateTime(r.lastLoginAt)}</span>
              </li>
            ))}
          </ul>
          {filtered.length === 0 && <p className="note">Nenhum login corresponde à pesquisa.</p>}
          {filtered.length > pageSize && (
            <OwnerPager
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
