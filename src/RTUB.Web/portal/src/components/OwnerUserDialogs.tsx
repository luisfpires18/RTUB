// Owner users details and role dialogs.
import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { dateTime, ownerApi, ownerProblem, type OwnerUserCard, type OwnerUserDetail } from '../data/ownerApi';
import { ROLES } from '../data/ownerUsersView';
import { ConfirmDialog } from './ui/ConfirmDialog';
import { Spinner } from './ui/Spinner';
import { Banner } from './ui/Banner';
import { CloseFooter, DialogFooter } from './ui/DialogFooter';
import { Avatar } from './ui/Avatar';

export function DetailsDialog({ id, onClose, onUnlocked }: { id: string; onClose: () => void; onUnlocked: (u: OwnerUserDetail) => void }) {
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
      footer={<CloseFooter onClose={onClose} />}
    >
      {user === undefined ? (
        <p className="note" role="status">
          A carregar…
        </p>
      ) : typeof user === 'string' ? (
        <Banner text={user} />
      ) : (
        <div className="owner-detail">
          <div className="owner-detail__who">
            <Avatar className="owner-person__avatar" src={user.avatarUrl} size={48} eager />
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
                {busy ? <Spinner small /> : <Icon name="unlock" />}
                Desbloquear
              </button>
              <Banner text={error} />
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}

export function RoleDialog({ user, onClose, onSaved }: { user: OwnerUserCard; onClose: () => void; onSaved: (u: OwnerUserDetail) => void }) {
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
      footer={<DialogFooter busy={false} onClose={onClose} onSubmit={() => (role === user.role ? onClose() : setConfirming(true))} label="Guardar" />}
    >
      <div className="owner-detail__who">
        <Avatar className="owner-person__avatar" src={user.avatarUrl} size={48} eager />
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
