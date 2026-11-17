// Owner tracing detail, delete and truncate dialogs.
import { useId, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { entityLabel, ownerApi, ownerProblem, type AuditEntry, type AuditFilters } from '../data/ownerApi';
import { CONFIRMATION, primaryName, stamp, actionTone, pretty } from '../data/ownerTracingHelpers';
import { Select } from './OwnerTracingParts';

export function DetailsDialog({ entry, onClose }: { entry: AuditEntry; onClose: () => void }) {
  return (
    <Dialog
      title={`Detalhes · ${entityLabel(entry.entityType)}`}
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Fechar
        </button>
      }
    >
      {/* #220: everything the log stored, raw; the card's prettier name never replaces it here. */}
      <dl className="owner-dl">
        <dt>Data</dt>
        <dd>
          <time dateTime={entry.timestamp}>{stamp(entry.timestamp)}</time>
        </dd>
        <dt>Utilizador</dt>
        <dd>{entry.userName ?? 'Sistema'}</dd>
        <dt>Tipo</dt>
        <dd>{entityLabel(entry.entityType)}</dd>
        <dt>Tipo técnico</dt>
        <dd>
          <code>{entry.entityType}</code>
        </dd>
        <dt>ID da entidade</dt>
        <dd>{entry.entityId ?? '—'}</dd>
        <dt>Nome guardado</dt>
        <dd>{entry.entityDisplayName || '— (sem nome no registo)'}</dd>
        {entry.currentDisplayName && entry.currentDisplayName !== entry.entityDisplayName && (
          <>
            <dt>Nome atual</dt>
            <dd>{entry.currentDisplayName} (resolvido agora, não guardado)</dd>
          </>
        )}
        <dt>Ação</dt>
        <dd>
          <span className={`owner-action owner-action--${actionTone(entry.action)}`}>{entry.action}</span>
        </dd>
        <dt>Crítica</dt>
        <dd>{entry.isCritical ? 'Sim' : 'Não'}</dd>
      </dl>
      <h3 className="owner-detail__heading">Alterações</h3>
      <pre className="owner-code">{pretty(entry.changes)}</pre>
    </Dialog>
  );
}

export function DeleteDialog({ entry, onClose, onDone }: { entry: AuditEntry; onClose: () => void; onDone: () => void }) {
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
        <dt>Registo</dt>
        <dd>
          {primaryName(entry)} · {entityLabel(entry.entityType)}
        </dd>
        <dt>Ação</dt>
        <dd>{entry.action}</dd>
        <dt>Utilizador</dt>
        <dd>{entry.userName ?? 'Sistema'}</dd>
        <dt>Data</dt>
        <dd>{stamp(entry.timestamp)}</dd>
      </dl>
      <p className="owner-danger">
        <Icon name="warning" />
        Não pode ser desfeito.
      </p>
    </ConfirmDialog>
  );
}

/** Every log, or one user's: only after typing DELETE ALL, which the server checks again. */
export function TruncateDialog({
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
