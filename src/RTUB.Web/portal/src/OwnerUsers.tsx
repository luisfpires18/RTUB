import { useEffect, useId, useState } from 'react';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { dateTime, ownerApi, ownerProblem, type OwnerUserCard, type OwnerUserDetail } from './ownerApi';
import { Flash, OwnerPage, OwnerPager, SearchBox, useOwnerLoad } from './OwnerUi';
import { MemberBadge } from './MemberBadge';

const ROLES = [
  { value: 'Member', hint: 'Acesso básico aos recursos da aplicação.' },
  { value: 'Mod', hint: 'Acesso moderado, incluindo inventário, loja e logística.' },
  { value: 'Admin', hint: 'Acesso às funcionalidades administrativas.' },
  { value: 'Owner', hint: 'Acesso total, incluindo estas ferramentas do Owner.' },
];

type Message = { text: string; error?: boolean };

/**
 * /users (task 035; was the Blazor page): every account, online first, searchable; the Owner sees an account's details,
 * unlocks it, changes its role (after confirming) and syncs every member's status. The server refuses anyone but the
 * Owner.
 */
export default function OwnerUsers() {
  const [q, setQ] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [staff, setStaff] = useState(false);
  const [users, reload] = useOwnerLoad(() => ownerApi.users(search, page, pageSize, staff), [search, page, pageSize, staff]);
  const [message, setMessage] = useState<Message>();
  const [details, setDetails] = useState<string>();
  const [editing, setEditing] = useState<OwnerUserCard>();
  const [syncing, setSyncing] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(q.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [q]);

  const sync = async () => {
    setSyncing(true);
    const o = await ownerApi.syncStatus();
    setSyncing(false);
    setMessage(
      o.kind === 'ok'
        ? o.data.failed > 0
          ? { text: `Estado de ${o.data.updated} membros sincronizado (${o.data.changed} alterados); ${o.data.failed} falharam.`, error: true }
          : { text: `Estado de ${o.data.updated} membros sincronizado com sucesso (${o.data.changed} alterados).` }
        : { text: 'Erro ao sincronizar o estado dos membros. Tenta outra vez.', error: true },
    );
    if (o.kind === 'ok') reload();
  };

  return (
    <OwnerPage
      path={portal.users}
      title="Utilizadores"
      lead="Contas, funções e bloqueios dos utilizadores."
      icon="personGear"
      state={users}
      loadingLabel="A carregar os utilizadores…"
      actions={
        <button type="button" className="btn btn--ghost btn--sm" onClick={sync} disabled={syncing}>
          {syncing ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="restore" />}
          {syncing ? 'A sincronizar…' : 'Sincronizar estado da Tuna'}
        </button>
      }
    >
      {(data) => (
        <>
          <Flash message={message} onDismiss={() => setMessage(undefined)} />
          <div className="events-tools" role="search">
            <SearchBox label="Procurar utilizador" placeholder="Procurar por nome, username, email, alcunha ou ID" value={q} onChange={setQ} />
            <div className="trace-toggles" role="group" aria-label="Filtros rápidos">
              <button
                type="button"
                className="trace-toggle"
                aria-pressed={staff}
                onClick={() => {
                  setStaff(!staff);
                  setPage(1);
                }}
              >
                <Icon name="personGear" />
                Só Mod, Admin e Owner
              </button>
            </div>
          </div>
          {data.items.length === 0 ? (
            <div className="state" role="status">
              <Icon name="people" className="state__icon" />
              <p className="state__title">Nenhum utilizador encontrado</p>
              <p>Ajusta a pesquisa.</p>
            </div>
          ) : (
            <ul className="owner-users">
              {data.items.map((u) => (
                <li key={u.id}>
                  <UserCard user={u} onDetails={() => setDetails(u.id)} onEdit={() => setEditing(u)} />
                </li>
              ))}
            </ul>
          )}
          <OwnerPager
            total={data.total}
            itemLabel="utilizadores"
            page={data.page}
            pageSize={data.pageSize}
            sizes={[20, 40, 60, 80, 100]}
            onPage={setPage}
            onPageSize={(s) => {
              setPageSize(s);
              setPage(1);
            }}
          />
          {details && (
            <DetailsDialog
              id={details}
              onClose={() => setDetails(undefined)}
              onUnlocked={(u) => {
                setMessage({ text: `Conta de ${u.fullName || u.nickname} desbloqueada com sucesso.` });
                reload();
              }}
            />
          )}
          {editing && (
            <RoleDialog
              user={editing}
              onClose={() => setEditing(undefined)}
              onSaved={(u) => {
                setEditing(undefined);
                setMessage({ text: `Função de ${u.fullName || u.nickname} alterada para ${u.role} com sucesso.` });
                reload();
              }}
            />
          )}
        </>
      )}
    </OwnerPage>
  );
}

function UserCard({ user, onDetails, onEdit }: { user: OwnerUserCard; onDetails: () => void; onEdit: () => void }) {
  return (
    <article className="owner-user">
      <img className="owner-user__avatar" src={user.avatarUrl} alt="" width="64" height="64" loading="lazy" />
      <div className="owner-user__body">
        <p className={user.online ? 'owner-user__status is-online' : 'owner-user__status'}>
          <span aria-hidden="true" />
          {user.online ? 'Online' : 'Offline'}
        </p>
        <h2 className="owner-user__name">{user.nickname || user.userName}</h2>
        <p className="note">{user.fullName}</p>
        <p className="member-badges">
          <MemberBadge kind="position">{user.role}</MemberBadge>
          {user.locked && <MemberBadge kind="danger">Bloqueado</MemberBadge>}
        </p>
        <p className="owner-user__meta">
          <code>{user.userName}</code>
          <span>{user.email}</span>
          <span>Último login: {dateTime(user.lastLoginAt)}</span>
        </p>
      </div>
      <div className="owner-user__actions">
        <button type="button" className="btn btn--ghost btn--sm" onClick={onDetails}>
          <Icon name="eye" />
          Detalhes<span className="sr-only"> de {user.nickname}</span>
        </button>
        <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
          <Icon name="pencil" />
          Função<span className="sr-only"> de {user.nickname}</span>
        </button>
      </div>
    </article>
  );
}

function DetailsDialog({ id, onClose, onUnlocked }: { id: string; onClose: () => void; onUnlocked: (u: OwnerUserDetail) => void }) {
  const [user, setUser] = useState<OwnerUserDetail | string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    ownerApi.user(id).then((o) => setUser(o.kind === 'ok' ? o.data : ownerProblem(o, 'abrir a conta')));
  }, [id]);

  const unlock = async () => {
    setBusy(true);
    setError(undefined);
    const o = await ownerApi.unlock(id);
    setBusy(false);
    if (o.kind === 'ok') {
      setUser(o.data);
      onUnlocked(o.data);
    } else setError(ownerProblem(o, 'desbloquear a conta'));
  };

  return (
    <Dialog
      title="Detalhes do utilizador"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Fechar
        </button>
      }
    >
      {user === undefined ? (
        <p className="note" role="status">
          A carregar…
        </p>
      ) : typeof user === 'string' ? (
        <p className="form__banner" role="alert">
          {user}
        </p>
      ) : (
        <div className="owner-detail">
          <div className="owner-detail__who">
            <img className="owner-person__avatar" src={user.avatarUrl} alt="" width="48" height="48" />
            <div>
              <strong>{user.nickname}</strong>
              <p className="note">{user.fullName}</p>
              <code>{user.userName}</code>
            </div>
          </div>
          <h3 className="owner-detail__heading">Conta</h3>
          <dl className="owner-dl">
            <dt>ID</dt>
            <dd>
              <code>{user.id}</code>
            </dd>
            <dt>Email</dt>
            <dd>{user.email ?? '—'}</dd>
            <dt>Função</dt>
            <dd>{user.role}</dd>
            <dt>Último login</dt>
            <dd>{dateTime(user.lastLoginAt)}</dd>
            <dt>Subscrito a emails</dt>
            <dd>{user.subscribed ? 'Sim' : 'Não'}</dd>
          </dl>
          <h3 className="owner-detail__heading">Estado</h3>
          <dl className="owner-dl">
            <dt>Bloqueado</dt>
            <dd>{user.locked ? 'Sim' : 'Não'}</dd>
            <dt>Tem de mudar a password</dt>
            <dd>{user.requirePasswordChange ? 'Sim' : 'Não'}</dd>
          </dl>
          {user.locked && (
            <div className="owner-danger">
              <p>
                <Icon name="lock" />
                Conta bloqueada até {dateTime(user.lockoutEnd)}. Desbloquear também reinicia o contador de tentativas falhadas.
              </p>
              <button type="button" className="btn btn--danger btn--sm" onClick={unlock} disabled={busy}>
                {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="unlock" />}
                Desbloquear
              </button>
              {error && (
                <p className="form__banner" role="alert">
                  {error}
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}

function RoleDialog({ user, onClose, onSaved }: { user: OwnerUserCard; onClose: () => void; onSaved: (u: OwnerUserDetail) => void }) {
  const id = useId();
  const [role, setRole] = useState(ROLES.some((r) => r.value === user.role) ? user.role : 'Member');
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const save = async () => {
    setBusy(true);
    setError(undefined);
    const o = await ownerApi.changeRole(user.id, role);
    setBusy(false);
    if (o.kind === 'ok') onSaved(o.data);
    else setError(ownerProblem(o, 'alterar a função'));
  };

  if (confirming) {
    return (
      <ConfirmDialog title="Confirmar a alteração de função" confirmLabel="Confirmar" busy={busy} error={error} onConfirm={save} onClose={onClose}>
        <p>
          Alterar a função de {user.fullName || user.nickname} de {user.role} para <strong>{role}</strong>? A sessão desta conta passa a usar a nova
          função no próximo pedido.
        </p>
      </ConfirmDialog>
    );
  }

  return (
    <Dialog
      title="Editar a função"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={() => (role === user.role ? onClose() : setConfirming(true))}>
            Guardar
          </button>
        </>
      }
    >
      <div className="owner-detail__who">
        <img className="owner-person__avatar" src={user.avatarUrl} alt="" width="48" height="48" />
        <div>
          <strong>{user.nickname}</strong>
          <p className="note">{user.fullName}</p>
        </div>
      </div>
      <fieldset className="owner-roles">
        <legend>Função</legend>
        {ROLES.map((r) => (
          <label key={r.value} className="owner-role" htmlFor={`${id}-${r.value}`}>
            <input id={`${id}-${r.value}`} type="radio" name={`${id}-role`} value={r.value} checked={role === r.value} onChange={() => setRole(r.value)} />
            <span>
              <strong>{r.value}</strong>
              <span className="note">{r.hint}</span>
            </span>
          </label>
        ))}
      </fieldset>
    </Dialog>
  );
}
