import { useEffect, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { ownerApi, type OwnerUserCard } from '../data/ownerApi';
import { Flash, OwnerPage, SearchBox, useOwnerLoad } from '../components/OwnerUi';
import { type Message } from '../data/ownerUsersView';
import { UserCard } from '../components/OwnerUserCard';
import { DetailsDialog, RoleDialog } from '../components/OwnerUserDialogs';
import { Pager } from '../components/ui/Pager';

/**
 * /users: every account, online first, searchable; the Owner sees an account's details,
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
          <Pager
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
