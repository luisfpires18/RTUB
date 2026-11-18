import { useEffect, useId, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { auditQuery, ENTITY_LABELS, entityLabel, ownerApi, type AuditEntry, type AuditFilter } from '../data/ownerApi';
import { Flash, OwnerPage, SearchBox, useOwnerLoad } from '../components/OwnerUi';
import { primaryName, stamp, actionTone } from '../data/ownerTracingHelpers';
import { UserField, Select, Announcement } from '../components/OwnerTracingParts';
import { DetailsDialog, DeleteDialog, TruncateDialog } from '../components/OwnerTracingDialogs';
import { LoginsDialog } from '../components/OwnerLoginsDialog';
import { Pager } from '../components/ui/Pager';

const EMPTY: AuditFilter = { user: '', excludeUser: '', entityType: '', action: '', critical: false, hideSystem: false, q: '', page: 1, pageSize: 20 };
const NO_TEXT = { q: '', user: '', excludeUser: '' };
/**
 * /owner/tracing: the audit log with its filters, search, details, delete, "DELETE ALL"
 * truncation and JSON export, the logins of a period, and the system announcement. The server refuses anyone but the
 * Owner and checks the truncation confirmation itself.
 */
export default function OwnerTracing() {
  const [filter, setFilter] = useState(EMPTY);
  // What is typed (search and the two member fields), applied 300 ms after the last key.
  const [text, setText] = useState(NO_TEXT);
  const [filters, reloadFilters] = useOwnerLoad(() => ownerApi.auditFilters(), []);
  const [logs, reloadLogs] = useOwnerLoad(() => ownerApi.audit(filter), [filter]);
  const [open, setOpen] = useState<{ kind: 'details' | 'delete'; entry: AuditEntry } | { kind: 'truncate' | 'logins' }>();
  const [message, setMessage] = useState<{ text: string; error?: boolean }>();
  const id = useId();

  const lists = filters && typeof filters === 'object' ? filters : { users: [], entityTypes: [], actions: [] };

  useEffect(() => {
    const timer = setTimeout(
      () =>
        setFilter((f) => {
          const q = text.q.trim();
          const user = text.user.trim();
          // "Excluir" is an exact username: it applies once the typed name is one of the list (or is cleared).
          const typed = text.excludeUser.trim();
          const excludeUser = typed === '' || lists.users.includes(typed) ? typed : f.excludeUser;
          return f.q === q && f.user === user && f.excludeUser === excludeUser ? f : { ...f, q, user, excludeUser, page: 1 };
        }),
      300,
    );
    return () => clearTimeout(timer);
  }, [text, lists.users]);

  const set = (patch: Partial<AuditFilter>) => setFilter((f) => ({ ...f, ...patch, page: patch.page ?? 1 }));
  const type = (patch: Partial<typeof NO_TEXT>) => setText((t) => ({ ...t, ...patch }));
  const clear = () => {
    setText(NO_TEXT);
    setFilter({ ...EMPTY, pageSize: filter.pageSize });
  };
  const state = filters && typeof filters === 'object' ? logs : filters;
  const entityText = (type: string) => (ENTITY_LABELS[type] ? `${ENTITY_LABELS[type]} (${type})` : type);
  // The filters in force, each removable on its own.
  const active: { key: string; label: string; remove: () => void }[] = [
    filter.q && { key: 'q', label: `“${filter.q}”`, remove: () => type({ q: '' }) },
    filter.user && { key: 'user', label: `Utilizador: ${filter.user}`, remove: () => (type({ user: '' }), set({ user: '' })) },
    filter.excludeUser && { key: 'ex', label: `Sem ${filter.excludeUser}`, remove: () => (type({ excludeUser: '' }), set({ excludeUser: '' })) },
    filter.entityType && { key: 'entity', label: entityText(filter.entityType), remove: () => set({ entityType: '' }) },
    filter.action && { key: 'action', label: `Ação: ${filter.action}`, remove: () => set({ action: '' }) },
    filter.hideSystem && { key: 'system', label: 'Sem ações do sistema', remove: () => set({ hideSystem: false }) },
    filter.critical && { key: 'critical', label: 'Só críticas', remove: () => set({ critical: false }) },
  ].filter((c): c is { key: string; label: string; remove: () => void } => !!c);

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

          <section className="owner-card trace-filters" aria-labelledby={`${id}-filters`}>
            <div className="trace-filters__head">
              <h2 id={`${id}-filters`} className="owner-card__title">
                <Icon name="search" />
                Filtros
              </h2>
              <div className="trace-toggles" role="group" aria-label="Filtros rápidos">
                <button type="button" className="trace-toggle" aria-pressed={filter.hideSystem} onClick={() => set({ hideSystem: !filter.hideSystem })}>
                  <Icon name="personGear" />
                  Esconder sistema
                </button>
                <button type="button" className="trace-toggle trace-toggle--critical" aria-pressed={filter.critical} onClick={() => set({ critical: !filter.critical })}>
                  <Icon name="warning" />
                  Só críticas
                </button>
              </div>
            </div>
            <SearchBox label="Procurar no nome ou nas alterações" placeholder="Procurar no nome guardado ou nas alterações" value={text.q} onChange={(q) => type({ q })} />
            <div className="trace-filters__grid">
              <UserField id={`${id}-user`} label="Utilizador" placeholder="Todos" value={text.user} users={lists.users} onChange={(user) => type({ user })} />
              <UserField
                id={`${id}-exclude`}
                label="Excluir utilizador"
                placeholder="Ninguém"
                value={text.excludeUser}
                users={lists.users}
                onChange={(excludeUser) => type({ excludeUser })}
              />
              <Select
                id={`${id}-entity`}
                label="Entidade"
                all="Todas"
                value={filter.entityType}
                items={lists.entityTypes}
                text={entityText}
                onChange={(entityType) => set({ entityType })}
              />
              <Select id={`${id}-action`} label="Ação" all="Todas" value={filter.action} items={lists.actions} onChange={(action) => set({ action })} />
            </div>
            {active.length > 0 && (
              <div className="trace-chips">
                <ul aria-label="Filtros ativos">
                  {active.map((c) => (
                    <li key={c.key}>
                      <button type="button" className="trace-chip" onClick={c.remove}>
                        {c.label}
                        <Icon name="close" />
                        <span className="sr-only">Remover filtro</span>
                      </button>
                    </li>
                  ))}
                </ul>
                <button type="button" className="btn btn--ghost btn--sm" onClick={clear}>
                  <Icon name="xCircle" />
                  Limpar filtros
                </button>
              </div>
            )}
          </section>

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
                <li key={entry.id} className={entry.isCritical ? 'owner-log owner-log--critical' : 'owner-log'}>
                  <p className="owner-log__name">
                    {primaryName(entry)}
                    {entry.currentDisplayName && (
                      <span className="owner-log__current" title="Nome resolvido agora; o registo guardou outro (ver Detalhes).">
                        nome atual
                      </span>
                    )}
                  </p>
                  <p className="owner-log__what">
                    <span className="owner-log__type">{entityLabel(entry.entityType)}</span>
                    <span className={`owner-action owner-action--${actionTone(entry.action)}`}>{entry.action}</span>
                    {entry.isCritical && (
                      <span className="owner-log__critical">
                        <Icon name="warning" />
                        Crítica
                      </span>
                    )}
                  </p>
                  <div className="owner-log__foot">
                    <p className="owner-log__meta">
                      <span>{entry.userName ?? 'Sistema'}</span>
                      <time dateTime={entry.timestamp}>{stamp(entry.timestamp)}</time>
                    </p>
                    <span className="owner-log__buttons">
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'details', entry })}>
                        <Icon name="eye" />
                        Detalhes
                        <span className="sr-only"> de {primaryName(entry)}</span>
                      </button>
                      <button
                        type="button"
                        className="icon-btn icon-btn--sm icon-btn--danger"
                        onClick={() => setOpen({ kind: 'delete', entry })}
                        title="Apagar este registo"
                      >
                        <Icon name="trash" />
                        <span className="sr-only">Apagar o registo de {primaryName(entry)}</span>
                      </button>
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
          <Pager
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
                clear();
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
