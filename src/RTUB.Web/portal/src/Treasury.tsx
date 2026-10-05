import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { euros, matches, problem, treasuryApi, type ReportSummary, type Reports } from './treasuryApi';
import { Confirm, Refusal, SaveFooter, TreasuryTabs } from './TreasuryUi';

/**
 * /treasury - Tesouraria (React track 024; was the Blazor /finance). Treasury members: the annual reports, newest first,
 * with income, expenses and balance, search and PDF. Admin, Owner and treasury-team Mods create a report; Owner and the
 * treasury team publish and delete drafts.
 */
export default function Treasury() {
  const [data, setData] = useState<Reports | 'signin' | 'forbidden' | 'failed'>();
  const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false);
  const [publishing, setPublishing] = useState<ReportSummary>();
  const [deleting, setDeleting] = useState<ReportSummary>();
  const id = useId();

  useEffect(() => {
    document.title = 'Tesouraria · RTUB';
  }, []);

  const load = () =>
    treasuryApi.reports().then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' || o.kind === 'forbidden' ? o.kind : 'failed'));

  useEffect(() => {
    load();
  }, []);

  const reports = data && typeof data === 'object' ? data : null;
  const shown = reports?.reports.filter((r) => matches(search, r.title, String(r.year), `${r.year}-${r.year + 1}`)) ?? [];

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="tr-title" className="page__title">
            Tesouraria
          </h1>
          <p className="page__lead">As contas da tuna, ano letivo a ano letivo: atividades, receitas, despesas e saldo.</p>
        </div>
        {reports?.canCreate && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setCreating(true)}>
              <Icon name="plus" />
              Novo relatório
            </button>
          </div>
        )}
      </header>

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasury} onRetry={load} />
      ) : !reports ? (
        <Loading label="A carregar os relatórios…" />
      ) : (
        <>
          <TreasuryTabs current="reports" />
          <div className="events-tools" role="search">
            <label className="control" htmlFor={id}>
              <span className="sr-only">Procurar relatório</span>
              <Icon name="search" />
              <input id={id} type="search" placeholder="Procurar por título ou ano" value={search} onChange={(e) => setSearch(e.target.value)} />
            </label>
          </div>

          {shown.length === 0 ? (
            <p className="note">{search ? 'Nenhum relatório corresponde à pesquisa.' : 'Ainda não há relatórios de contas.'}</p>
          ) : (
            <ul className="tr-reports">
              {shown.map((r) => (
                <li key={r.id} className="tr-report">
                  <a className="tr-report__open" href={portal.treasuryReport(r.id)}>
                    <strong>{r.title}</strong>
                    <span className="tr-muted">
                      Ano letivo {r.year}-{r.year + 1}
                    </span>
                    <span className="tr-badges">
                      {r.isCurrentYear && <span className="badge">Ano atual</span>}
                      <span className={r.isPublished ? 'pill pill--yes' : 'pill pill--wait'}>{r.isPublished ? 'Publicado' : 'Rascunho'}</span>
                    </span>
                    <dl className="tr-stats tr-stats--compact">
                      <div>
                        <dt>Receitas</dt>
                        <dd className="tr-in">{euros(r.totalIncome)}</dd>
                      </div>
                      <div>
                        <dt>Despesas</dt>
                        <dd className="tr-out">{euros(r.totalExpenses)}</dd>
                      </div>
                      <div>
                        <dt>Saldo</dt>
                        <dd className={r.balance >= 0 ? 'tr-in' : 'tr-out'}>{euros(r.balance)}</dd>
                      </div>
                    </dl>
                  </a>
                  <span className="tr-report__tools">
                    <a className="icon-btn icon-btn--sm" href={`/api/treasury/reports/${r.id}/pdf`} download>
                      <Icon name="download" />
                      <span className="sr-only">Descarregar o PDF de {r.title}</span>
                    </a>
                    {reports.canPublish && !r.isPublished && (
                      <>
                        <button type="button" className="icon-btn icon-btn--sm" onClick={() => setPublishing(r)}>
                          <Icon name="check" />
                          <span className="sr-only">Publicar {r.title}</span>
                        </button>
                        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setDeleting(r)}>
                          <Icon name="trash" />
                          <span className="sr-only">Eliminar {r.title}</span>
                        </button>
                      </>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}

          {creating && <CreateReportDialog years={reports.availableYears} onClose={() => setCreating(false)} onSaved={load} />}
          {publishing && (
            <Confirm
              title="Publicar relatório"
              action="Publicar"
              danger={false}
              onClose={() => setPublishing(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.publish(publishing.id);
                if (o.kind !== 'ok') return problem(o, 'publicar o relatório');
                load();
              }}
            >
              <p>
                Publicar <strong>{publishing.title}</strong> ({publishing.year}-{publishing.year + 1})?
              </p>
              <p className="warning">
                <Icon name="warning" />
                Depois de publicado, o relatório fica só de leitura.
              </p>
            </Confirm>
          )}
          {deleting && (
            <Confirm
              title="Eliminar relatório"
              action="Eliminar"
              onClose={() => setDeleting(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.removeReport(deleting.id);
                if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, 'eliminar o relatório');
                load();
              }}
            >
              <p>
                Eliminar <strong>{deleting.title}</strong>?
              </p>
              <p className="warning">
                <Icon name="warning" />
                Apaga também as atividades, as transações e os recibos. Não dá para desfazer.
              </p>
            </Confirm>
          )}
        </>
      )}
    </section>
  );
}

/** "Novo relatório": only the fiscal year; the title is "Relatório de Contas Y - Y+1", as before. */
function CreateReportDialog({ years, onClose, onSaved }: { years: number[]; onClose: () => void; onSaved: () => void }) {
  const [year, setYear] = useState(years[0] ?? 0);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await treasuryApi.createReport(year);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'criar o relatório'));
    onSaved();
    onClose();
  };

  return (
    <Dialog title="Novo relatório" onClose={onClose} footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={years.length === 0} onClose={onClose} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {years.length === 0 ? (
          <p className="note">Todos os anos letivos até ao atual já têm relatório.</p>
        ) : (
          <div className={error ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-year`}>Ano letivo</label>
            <span className="control control--select">
              <select id={`${fid}-year`} value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {years.map((y) => (
                  <option key={y} value={y}>
                    {y}-{y + 1}
                  </option>
                ))}
              </select>
            </span>
            {error && (
              <p className="form__error" role="alert">
                {error}
              </p>
            )}
          </div>
        )}
      </form>
    </Dialog>
  );
}
