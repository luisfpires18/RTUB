import { useEffect, useId, useRef, useState, type CSSProperties, type KeyboardEvent, type PointerEvent } from 'react';
import { portal } from './content';
import { Icon, type IconName } from './icons';
import { entityLabel, ownerApi, type Dashboard, type DashboardRank, type DashboardSeries } from './ownerApi';
import { OwnerPage, useOwnerLoad } from './OwnerUi';

const RANGES = [
  { days: 7, label: '7 dias' },
  { days: 30, label: '30 dias' },
  { days: 90, label: '90 dias' },
  { days: 365, label: '12 meses' },
] as const;

const ICONS: Record<string, IconName> = {
  signIns: 'login',
  activeMembers: 'people',
  movements: 'shield',
  uploads: 'upload',
  songPlays: 'music',
  naipePlays: 'naipes',
  enrollments: 'calendarCheck',
};

const ACTION_LABELS: Record<string, string> = {
  Created: 'Criado',
  Modified: 'Alterado',
  Deleted: 'Apagado',
  Reformed: 'Reformado',
  PushNotificationSent: 'Notificação enviada',
};
const WEEKDAYS = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];

/** Today on Portugal's calendar, yyyy-MM-dd (the server buckets by Portugal day too). */
const portugalToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date());

const addDays = (day: string, n: number) => {
  const d = new Date(`${day}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};

/** dd/MM, or "seg., 12/10" with the weekday. */
const shortDay = (day: string, weekday = false) => {
  const d = new Date(`${day}T00:00:00Z`);
  return new Intl.DateTimeFormat('pt-PT', { timeZone: 'UTC', day: '2-digit', month: '2-digit', ...(weekday ? { weekday: 'short' } : {}) }).format(d);
};

const number = (n: number) => new Intl.NumberFormat('pt-PT').format(n);

/**
 * /owner/dashboard (task 050): sign-ins, active members, movements, uploads, plays and enrollments per day for the
 * chosen range, each against the period before; the busiest hours; and the top songs, naipes, members and areas.
 * Owner only (the server refuses everyone else). Sign-in history only exists since 050.
 */
export default function OwnerDashboard() {
  const [days, setDays] = useState<number>(30);
  const to = portugalToday();
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
        <Ranking title="Músicas mais tocadas" icon="music" rows={data.topSongs} />
        <Ranking title="Naipes mais vistos" icon="naipes" rows={data.topNaipes} />
        <Ranking title="Membros mais ativos" icon="people" rows={data.topMembers} />
        <Ranking title="Áreas com mais movimentos" icon="diagram" rows={data.areas} label={entityLabel} />
        <Ranking title="Tipos de movimento" icon="listOl" rows={data.actions} label={(a) => ACTION_LABELS[a] ?? a} />
      </div>
    </div>
  );
}

function Tile({ series, selected, onSelect }: { series: DashboardSeries; selected: boolean; onSelect: () => void }) {
  const { total, previousTotal: prev } = series;
  const change = prev > 0 ? Math.round(((total - prev) / prev) * 100) : null;
  const trend =
    change === null
      ? total > 0
        ? 'Sem dados no período anterior'
        : 'Sem dados'
      : `${change > 0 ? '+' : ''}${change}% vs. período anterior`;

  return (
    <button type="button" role="tab" aria-selected={selected} className="owner-dash__tile" onClick={onSelect}>
      <span className="owner-dash__tile-label">
        <Icon name={ICONS[series.key] ?? 'chart'} />
        {series.label}
      </span>
      <span className="owner-dash__tile-value">{number(total)}</span>
      <span className="owner-dash__tile-trend">
        {change !== null && change !== 0 && <Icon name={change > 0 ? 'up' : 'down'} />}
        {trend}
      </span>
    </button>
  );
}

/** Rounds the top of the scale up to 1, 2 or 5 × 10ⁿ so the gridlines land on whole numbers. */
function niceMax(max: number) {
  if (max <= 4) return 4;
  const step = 10 ** Math.floor(Math.log10(max / 4));
  const nice = [1, 2, 5, 10].map((m) => m * step).find((s) => s * 4 >= max)!;
  return nice * 4;
}

function LineChart({ days, series }: { days: string[]; series: DashboardSeries }) {
  const box = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(640);
  const [hover, setHover] = useState<number>();

  useEffect(() => {
    const el = box.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setWidth(Math.max(240, entry.contentRect.width)));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const values = series.values;
  const n = values.length;
  const height = 240;
  const pad = { l: 40, r: 12, t: 12, b: 28 };
  const max = niceMax(Math.max(0, ...values));
  const stepX = n > 1 ? (width - pad.l - pad.r) / (n - 1) : 0;
  const x = (i: number) => (n > 1 ? pad.l + i * stepX : (pad.l + width - pad.r) / 2);
  const y = (v: number) => pad.t + (height - pad.t - pad.b) * (1 - v / max);
  const line = values.map((v, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(v).toFixed(1)}`).join('');
  const area = `${line}L${x(n - 1).toFixed(1)},${y(0)}L${x(0).toFixed(1)},${y(0)}Z`;
  const ticks = [0, max / 4, max / 2, (3 * max) / 4, max];
  const labelEvery = Math.max(1, Math.ceil(n / Math.max(2, Math.floor(width / 72))));

  const pick = (e: PointerEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const px = e.clientX - rect.left;
    setHover(n > 1 ? Math.min(n - 1, Math.max(0, Math.round((px - pad.l) / stepX))) : 0);
  };

  const keys = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
    e.preventDefault();
    const at = hover ?? (e.key === 'ArrowLeft' ? n : -1);
    setHover(Math.min(n - 1, Math.max(0, at + (e.key === 'ArrowRight' ? 1 : -1))));
  };

  const tipLeft = hover === undefined ? 0 : Math.min(Math.max(x(hover), 70), width - 70);

  return (
    <div
      ref={box}
      className="owner-dash__chart"
      tabIndex={0}
      role="img"
      aria-label={`${series.label}: ${number(series.total)} no período. Setas para percorrer os dias.`}
      onKeyDown={keys}
      onBlur={() => setHover(undefined)}
    >
      <svg width={width} height={height} onPointerMove={pick} onPointerLeave={() => setHover(undefined)} aria-hidden="true">
        {ticks.map((t) => (
          <g key={t}>
            <line className="owner-dash__grid" x1={pad.l} x2={width - pad.r} y1={y(t)} y2={y(t)} />
            <text className="owner-dash__axis" x={pad.l - 8} y={y(t)} dy="0.32em" textAnchor="end">
              {number(t)}
            </text>
          </g>
        ))}
        {days.map((d, i) =>
          i % labelEvery === 0 ? (
            <text key={d} className="owner-dash__axis" x={x(i)} y={height - 8} textAnchor="middle">
              {shortDay(d)}
            </text>
          ) : null,
        )}
        <path className="owner-dash__area" d={area} />
        <path className="owner-dash__line" d={line} />
        {hover !== undefined && (
          <g>
            <line className="owner-dash__cross" x1={x(hover)} x2={x(hover)} y1={pad.t} y2={y(0)} />
            <circle className="owner-dash__dot" cx={x(hover)} cy={y(values[hover])} r={5} />
          </g>
        )}
      </svg>
      {hover !== undefined && (
        <div className="owner-dash__tip" style={{ left: tipLeft }} role="status">
          <span>{shortDay(days[hover], true)}</span>
          <strong>
            {number(values[hover])} <span>{series.label.toLowerCase()}</span>
          </strong>
        </div>
      )}
    </div>
  );
}

function Heatmap({ hours }: { hours: number[][] }) {
  const titleId = useId();
  const max = Math.max(1, ...hours.flat());

  return (
    <section className="owner-card" aria-labelledby={titleId}>
      <h2 id={titleId} className="owner-card__title">
        <Icon name="clock" />
        Horas de maior atividade
      </h2>
      <div className="owner-dash__scroll">
        <div className="owner-dash__heat" role="table" aria-label="Sessões e horas com membros ativos, por dia da semana e hora">
          <div role="row" className="owner-dash__heat-row">
            <span role="columnheader" />
            {Array.from({ length: 24 }, (_, h) => (
              <span key={h} role="columnheader" className="owner-dash__heat-hour">
                {h % 3 === 0 ? h : ''}
              </span>
            ))}
          </div>
          {hours.map((row, d) => (
            <div key={WEEKDAYS[d]} role="row" className="owner-dash__heat-row">
              <span role="rowheader" className="owner-dash__heat-day">
                {WEEKDAYS[d]}
              </span>
              {row.map((v, h) => (
                <span
                  key={h}
                  role="cell"
                  className="owner-dash__heat-cell"
                  style={{ '--heat': v ? 0.15 + 0.85 * (v / max) : 0 } as CSSProperties}
                  title={`${WEEKDAYS[d]} ${h}h: ${number(v)}`}
                  aria-label={`${WEEKDAYS[d]} ${h}h: ${number(v)}`}
                />
              ))}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function Ranking({ title, icon, rows, label = (s) => s }: { title: string; icon: IconName; rows: DashboardRank[]; label?: (s: string) => string }) {
  const titleId = useId();
  const max = Math.max(1, ...rows.map((r) => r.count));

  return (
    <section className="owner-card" aria-labelledby={titleId}>
      <h2 id={titleId} className="owner-card__title">
        <Icon name={icon} />
        {title}
      </h2>
      {rows.length === 0 ? (
        <p className="owner-dash__note">Nada neste período.</p>
      ) : (
        <ol className="owner-dash__rank">
          {rows.map((r) => (
            <li key={r.label}>
              <span className="owner-dash__rank-label">{label(r.label)}</span>
              <span className="owner-dash__rank-count">{number(r.count)}</span>
              <span className="owner-dash__rank-bar" style={{ width: `${(r.count / max) * 100}%` }} />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
