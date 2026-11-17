// Treasury report activity panel (list, filters and paging).
import { useId, useState } from 'react';
import { Icon } from './icons';
import { matches, type Activity, type Report, type Transaction } from '../data/treasuryApi';
import { day, euros } from '../data/format';
import { PAGE, when } from '../data/treasuryReportView';

export function ActivityPanel({
  activity: a,
  report,
  onLock,
  onEdit,
  onDelete,
  onAdd,
  onEditTransaction,
  onDeleteTransaction,
}: {
  activity: Activity;
  report: Report;
  onLock: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onAdd: () => void;
  onEditTransaction: (t: Transaction) => void;
  onDeleteTransaction: (t: Transaction) => void;
}) {
  const [search, setSearch] = useState('');
  const [limit, setLimit] = useState(PAGE);
  const id = useId();
  const editable = report.canManage && !report.isPublished;
  const shown = a.transactions.filter((t) => matches(search, t.description, t.category));

  return (
    <details className="tr-activity">
      <summary className="tr-activity__head">
        <span className="tr-activity__name">
          {a.isLocked && <Icon name="lock" />}
          <strong>{a.name}</strong>
          {a.eventName && <span className="tag">{a.eventName}</span>}
        </span>
        <span className="tr-activity__meta">
          <span className="tag">{when(a)}</span>
          <span className={a.balance >= 0 ? 'pill pill--yes' : 'pill pill--no'}>Saldo {euros(a.balance)}</span>
        </span>
        <Icon name="chevronDown" className="lx-completed__chevron" />
      </summary>
      <div className="tr-activity__body">
        {report.canManage && (
          <div className="tr-activity__tools">
            <button type="button" className="btn btn--ghost btn--sm" onClick={onLock}>
              <Icon name={a.isLocked ? 'unlock' : 'lock'} />
              {a.isLocked ? 'Desbloquear' : 'Bloquear'}
            </button>
            {editable && (
              <>
                <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
                  <Icon name="pencil" />
                  Editar
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={onDelete}>
                  <Icon name="trash" />
                  Eliminar
                </button>
              </>
            )}
          </div>
        )}
        {a.description && <p className="tr-muted tr-pre">{a.description}</p>}
        <dl className="tr-stats tr-stats--compact">
          <div>
            <dt>Receitas</dt>
            <dd className="tr-in">{euros(a.income)}</dd>
          </div>
          <div>
            <dt>Despesas</dt>
            <dd className="tr-out">{euros(a.expenses)}</dd>
          </div>
        </dl>

        <div className="tr-section-head">
          <h3 className="tr-subtitle">Transações</h3>
          {editable && !a.isLocked && (
            <button type="button" className="btn btn--primary btn--sm" onClick={onAdd}>
              <Icon name="plus" />
              Nova transação
            </button>
          )}
        </div>
        {a.isLocked && (
          <p className="note">Atividade bloqueada: não é possível adicionar transações.</p>
        )}

        {a.transactions.length === 0 ? (
          <p className="note">Sem transações.</p>
        ) : (
          <>
            <label className="control control--sm" htmlFor={id}>
              <span className="sr-only">Procurar transação em {a.name}</span>
              <Icon name="search" />
              <input
                id={id}
                type="search"
                placeholder="Procurar por descrição ou categoria"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setLimit(PAGE);
                }}
              />
            </label>
            {search && (
              <p className="note">
                A mostrar {shown.length} de {a.transactions.length} transações.
              </p>
            )}
            <ul className="tr-transactions">
              {shown.slice(0, limit).map((t) => (
                <li key={t.id} className="tr-transaction">
                  <span className="tr-transaction__main">
                    <strong>{t.description}</strong>
                    <span className="tr-muted">
                      {day(t.date)} · {t.category}
                    </span>
                  </span>
                  <span className={t.type === 'Income' ? 'tr-in tr-transaction__amount' : 'tr-out tr-transaction__amount'}>
                    {t.type === 'Income' ? '+' : '−'}
                    {euros(t.amount)}
                  </span>
                  <span className="tr-transaction__tools">
                    {t.receiptUrl && (
                      <a className="icon-btn icon-btn--sm" href={t.receiptUrl} target="_blank" rel="noopener noreferrer">
                        <Icon name="file" />
                        <span className="sr-only">Ver o recibo de {t.description}</span>
                      </a>
                    )}
                    {editable && (
                      <>
                        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onEditTransaction(t)}>
                          <Icon name="pencil" />
                          <span className="sr-only">Editar {t.description}</span>
                        </button>
                        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onDeleteTransaction(t)}>
                          <Icon name="trash" />
                          <span className="sr-only">Eliminar {t.description}</span>
                        </button>
                      </>
                    )}
                  </span>
                </li>
              ))}
            </ul>
            {shown.length === 0 && <p className="note">Nenhuma transação encontrada.</p>}
            {shown.length > limit && (
              <button type="button" className="btn btn--ghost btn--sm tr-more" onClick={() => setLimit(limit + PAGE)}>
                Mostrar mais ({shown.length - limit})
              </button>
            )}
          </>
        )}
      </div>
    </details>
  );
}
