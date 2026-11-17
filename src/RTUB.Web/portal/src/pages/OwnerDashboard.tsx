import { useState } from 'react';
import { portal } from '../data/content';
import { ownerApi, type Dashboard } from '../data/ownerApi';
import { OwnerPage, useOwnerLoad } from '../components/OwnerUi';
import { lisbonToday } from '../data/format';
import { RANGES, PAGE_LABELS, addDays, shortDay, number } from '../data/ownerDashboardView';
import { Tile, LineChart, Heatmap, Ranking } from '../components/OwnerDashboardCharts';

/**
 * /owner/dashboard (task 050): sign-ins, active members, movements, uploads, plays and enrollments per day for the
 * chosen range, each against the period before; the busiest hours; and the top members and areas (088 dropped the song,
 * naipe and action-type rankings).
 * Owner only (the server refuses everyone else). Sign-in history only exists since 050.
 */
export default function OwnerDashboard() {
  const [days, setDays] = useState<number>(30);
  const to = lisbonToday();
  const from = addDays(to, 1 - days);
  const [state] = useOwnerLoad(() => ownerApi.dashboard(from, to), [from, to]);
  const [metric, setMetric] = useState('signIns');

  return (
    <OwnerPage
      path={portal.ownerDashboard}
      title="Estatísticas"
      lead="Sessões, movimentos, uploads e reproduções ao longo do tempo."
      icon="chart"
      state={state}
      loadingLabel="A carregar as estatísticas…"
      actions={
        <div className="segmented" role="group" aria-label="Período">
          {RANGES.map((r) => (
            <button
              key={r.days}
              type="button"
              aria-pressed={days === r.days}
              onClick={() => setDays(r.days)}
            >
              {r.label}
            </button>
          ))}
        </div>
      }
    >
      {(data) => <DashboardBody data={data} metric={metric} onMetric={setMetric} />}
    </OwnerPage>
  );
}

function DashboardBody({ data, metric, onMetric }: { data: Dashboard; metric: string; onMetric: (key: string) => void }) {
  const selected = data.series.find((s) => s.key === metric) ?? data.series[0];

  return (
    <div className="owner-dash">
      <dl className="owner-dash__now">
        <div>
          <dt>Contas</dt>
          <dd>{number(data.now.accounts)}</dd>
        </div>
        <div>
          <dt>Online agora</dt>
          <dd>{number(data.now.online)}</dd>
        </div>
        <div>
          <dt>Com notificações push</dt>
          <dd>{number(data.now.pushMembers)}</dd>
        </div>
      </dl>

      <section className="owner-card" aria-label="Métricas">
        <div className="owner-dash__tiles" role="tablist" aria-label="Métrica no gráfico">
          {data.series.map((s) => (
            <Tile key={s.key} series={s} selected={s.key === selected.key} onSelect={() => onMetric(s.key)} />
          ))}
        </div>
        <LineChart days={data.days} series={selected} />
        <details className="owner-dash__table">
          <summary>Ver como tabela</summary>
          <div className="owner-dash__scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Dia</th>
                  {data.series.map((s) => (
                    <th key={s.key} scope="col">
                      {s.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.days.map((day, i) => (
                  <tr key={day}>
                    <th scope="row">{shortDay(day, true)}</th>
                    {data.series.map((s) => (
                      <td key={s.key}>{number(s.values[i])}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
        {(selected.key === 'signIns' || selected.key === 'activeMembers') && (
          <p className="owner-dash__note">Sessões registadas desde a versão com esta página; antes disso só existe o último acesso de cada conta.</p>
        )}
      </section>

      <Heatmap hours={data.hours} />

      <div className="owner-dash__ranks">
        <Ranking title="Membros mais ativos" icon="people" rows={data.topMembers} />
        <Ranking title="Áreas com mais movimentos" icon="diagram" rows={data.areas} />
        <Ranking title="Páginas mais vistas" icon="eye" rows={data.topPages} label={(p) => PAGE_LABELS[p] ?? p} />
      </div>
    </div>
  );
}
