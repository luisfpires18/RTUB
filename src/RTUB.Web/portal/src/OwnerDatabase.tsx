import { useId, useState } from 'react';
import { portal } from './content';
import { Icon } from './icons';
import { ConfirmDialog } from './MusicUi';
import { ownerApi, ownerProblem, type DbGrid } from './ownerApi';
import { Flash, OwnerPage, OwnerPager, useOwnerLoad } from './OwnerUi';
import { MemberBadge } from './MemberBadge';

type Message = { text: string; error?: boolean };
type View = { kind: 'table'; table: string; grid: DbGrid } | { kind: 'query'; grid: DbGrid; page: number; pageSize: number };

/**
 * /owner/db (task 035; was the Blazor page): browse any table page by page, run a read-only SELECT, or run an INSERT /
 * UPDATE / DELETE after a warning and a confirmation. The server applies the same SQL rules as before (only SELECT on the
 * first tab; only INSERT / UPDATE / DELETE, no DDL, on the second), refuses a change sent without the confirmation, and
 * refuses anyone but the Owner.
 */
export default function OwnerDatabase() {
  const id = useId();
  const [tables] = useOwnerLoad(() => ownerApi.tables(), []);
  const [view, setView] = useState<View>();
  const [tab, setTab] = useState<'select' | 'modify'>('select');
  const [select, setSelect] = useState('');
  const [modify, setModify] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [message, setMessage] = useState<Message>();

  const openTable = async (table: string, page = 1, pageSize = view?.kind === 'table' ? view.grid.pageSize : 50) => {
    if (!table) return;
    setBusy(true);
    const o = await ownerApi.table(table, page, pageSize);
    setBusy(false);
    if (o.kind === 'ok') setView({ kind: 'table', table, grid: o.data });
    else setMessage({ text: ownerProblem(o, 'abrir a tabela'), error: true });
  };

  const runSelect = async () => {
    setBusy(true);
    setMessage(undefined);
    const o = await ownerApi.select(select);
    setBusy(false);
    if (o.kind === 'ok') {
      setView({ kind: 'query', grid: o.data, page: 1, pageSize: 50 });
      setMessage({ text: `Consulta executada. ${o.data.total} registo(s).` });
    } else setMessage({ text: ownerProblem(o, 'executar a consulta'), error: true });
  };

  const runModify = async () => {
    setBusy(true);
    const o = await ownerApi.modify(modify);
    setBusy(false);
    setConfirming(false);
    if (o.kind === 'ok') {
      setModify('');
      setMessage({ text: `Alteração executada. ${o.data.rowsAffected} linha(s) afetada(s).` });
    } else setMessage({ text: ownerProblem(o, 'executar a alteração'), error: true });
  };

  const refresh = () => {
    if (view?.kind === 'table') openTable(view.table, view.grid.page, view.grid.pageSize);
    else if (view?.kind === 'query' && select.trim()) runSelect();
  };

  const selected = view?.kind === 'table' ? view.table : '';

  return (
    <OwnerPage
      path={portal.ownerDb}
      title="Base de dados"
      lead="Ver e gerir as tabelas e os registos da base de dados."
      icon="database"
      state={tables}
      loadingLabel="A carregar as tabelas…"
      actions={
        <button type="button" className="btn btn--ghost btn--sm" onClick={refresh} disabled={busy || !view}>
          <Icon name="restore" />
          Recarregar
        </button>
      }
    >
      {(names) => (
        <div className="owner-db">
          <aside className="owner-db__tables" aria-label="Tabelas">
            <div className="form__field owner-db__picker">
              <label htmlFor={`${id}-table`}>Tabela</label>
              <span className="control control--select">
                <select id={`${id}-table`} value={selected} onChange={(e) => openTable(e.target.value, 1)}>
                  <option value="">Escolher uma tabela</option>
                  {names.map((t) => (
                    <option key={t} value={t}>
                      {t}
                    </option>
                  ))}
                </select>
              </span>
            </div>
            <div className="owner-db__list">
              <h2 className="owner-card__title">Tabelas ({names.length})</h2>
              <ul>
                {names.map((t) => (
                  <li key={t}>
                    <button type="button" className="owner-db__table" aria-current={t === selected ? 'true' : undefined} onClick={() => openTable(t, 1)}>
                      {t}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          </aside>

          <div className="owner-db__main">
            <Flash message={message} onDismiss={() => setMessage(undefined)} />
            <section className="owner-card" aria-label="Consultas">
              <div className="owner-tabs" role="tablist" aria-label="Tipo de consulta">
                <button type="button" role="tab" aria-selected={tab === 'select'} className="owner-tab" onClick={() => setTab('select')}>
                  <Icon name="search" />
                  SELECT
                </button>
                <button type="button" role="tab" aria-selected={tab === 'modify'} className="owner-tab" onClick={() => setTab('modify')}>
                  <Icon name="pencil" />
                  INSERT / UPDATE / DELETE
                </button>
              </div>
              {tab === 'select' ? (
                <div className="form" role="tabpanel">
                  <label htmlFor={`${id}-select`} className="sr-only">
                    Consulta SELECT
                  </label>
                  <textarea
                    id={`${id}-select`}
                    className="owner-sql"
                    rows={3}
                    spellCheck={false}
                    value={select}
                    placeholder="SELECT * FROM Events WHERE Id > 10"
                    onChange={(e) => setSelect(e.target.value)}
                  />
                  <div className="owner-actions">
                    <button type="button" className="btn btn--primary btn--sm" onClick={runSelect} disabled={busy || !select.trim()}>
                      {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="play" />}
                      Executar
                    </button>
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      onClick={() => {
                        setSelect('');
                        if (view?.kind === 'query') setView(undefined);
                        setMessage(undefined);
                      }}
                      disabled={busy}
                    >
                      <Icon name="xCircle" />
                      Limpar
                    </button>
                  </div>
                  <p className="form__hint">Só consultas SELECT. O resultado aparece na grelha abaixo.</p>
                </div>
              ) : (
                <div className="form" role="tabpanel">
                  <label htmlFor={`${id}-modify`} className="sr-only">
                    Consulta INSERT, UPDATE ou DELETE
                  </label>
                  <textarea
                    id={`${id}-modify`}
                    className="owner-sql"
                    rows={8}
                    spellCheck={false}
                    value={modify}
                    placeholder="INSERT, UPDATE ou DELETE"
                    onChange={(e) => setModify(e.target.value)}
                  />
                  <div className="owner-actions">
                    <button type="button" className="btn btn--danger btn--sm" onClick={() => setConfirming(true)} disabled={busy || !modify.trim()}>
                      <Icon name="play" />
                      Executar
                    </button>
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      onClick={() => {
                        setModify('');
                        setMessage(undefined);
                      }}
                      disabled={busy}
                    >
                      <Icon name="xCircle" />
                      Limpar
                    </button>
                  </div>
                  <div className="owner-danger">
                    <p>
                      <Icon name="warning" />
                      <strong>Cuidado:</strong> não há forma de desfazer.
                    </p>
                    <ul>
                      <li>Só comandos que alteram dados: INSERT, UPDATE e DELETE.</li>
                      <li>SELECT não é aceite neste separador.</li>
                      <li>Testa primeiro num ambiente de desenvolvimento.</li>
                      <li>Faz uma cópia da base de dados antes de alterações destrutivas.</li>
                    </ul>
                  </div>
                </div>
              )}
            </section>

            <Grid
              view={view}
              busy={busy}
              onTablePage={(p, s) => view?.kind === 'table' && openTable(view.table, p, s)}
              onQueryPage={(page, pageSize) => view?.kind === 'query' && setView({ ...view, page, pageSize })}
            />
          </div>

          {confirming && (
            <ConfirmDialog title="Executar esta alteração?" confirmLabel="Executar" busy={busy} onConfirm={runModify} onClose={() => setConfirming(false)}>
              <pre className="owner-code">{modify}</pre>
              <p className="owner-danger">
                <Icon name="warning" />
                Altera a base de dados de imediato e não pode ser desfeito.
              </p>
            </ConfirmDialog>
          )}
        </div>
      )}
    </OwnerPage>
  );
}

function Grid({
  view,
  busy,
  onTablePage,
  onQueryPage,
}: {
  view?: View;
  busy: boolean;
  onTablePage: (page: number, pageSize: number) => void;
  onQueryPage: (page: number, pageSize: number) => void;
}) {
  const title = view?.kind === 'table' ? view.table : view ? 'Resultado da consulta' : 'Nenhuma tabela escolhida';
  const rows = !view ? [] : view.kind === 'table' ? view.grid.rows : view.grid.rows.slice((view.page - 1) * view.pageSize, view.page * view.pageSize);

  return (
    <section className="owner-card" aria-label={title} aria-busy={busy}>
      <h2 className="owner-card__title owner-db__grid-title">
        <Icon name={view?.kind === 'query' ? 'listOl' : 'database'} />
        <span>{title}</span>
        {view && <MemberBadge>{view.grid.total} registos</MemberBadge>}
      </h2>
      {!view ? (
        <p className="note">Escolhe uma tabela para ver os registos, ou executa uma consulta.</p>
      ) : view.grid.columns.length === 0 ? (
        <p className="note">A tabela ou a consulta não devolveu resultados.</p>
      ) : (
        <>
          <div className="owner-table" tabIndex={0} role="region" aria-label={`Registos de ${title}`}>
            <table>
              <thead>
                <tr>
                  {view.grid.columns.map((c) => (
                    <th key={c} scope="col">
                      {c}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row, i) => (
                  <tr key={i}>
                    {row.map((cell, j) => (
                      <td key={j}>{cell}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <OwnerPager
            total={view.grid.total}
            itemLabel="registos"
            page={view.kind === 'table' ? view.grid.page : view.page}
            pageSize={view.kind === 'table' ? view.grid.pageSize : view.pageSize}
            sizes={[10, 25, 50, 100]}
            onPage={(p) => (view.kind === 'table' ? onTablePage(p, view.grid.pageSize) : onQueryPage(p, view.pageSize))}
            onPageSize={(s) => (view.kind === 'table' ? onTablePage(1, s) : onQueryPage(1, s))}
          />
        </>
      )}
    </section>
  );
}
