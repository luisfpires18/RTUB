// Presença statistics dialog and the member's own presenças record.

import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { dateLabel } from '../data/eventsApi';
import { Icon } from './icons';
import { MemberBadge, badgeKind } from './MemberBadge';
import { fold } from '../data/format';
import { rehearsalsApi, type RehearsalSummary, type RehearsalStats } from '../data/rehearsalsApi';
import { PresencePill } from './RehearsalShared';

// ---------- statistics and the member's own record ----------

/**
 * "Estatísticas de presenças": for the dates chosen (the current season by default),
 * each member's confirmed and pending presenças at past, not cancelled rehearsals, and the confirmed share.
 */
export function StatsDialog({ onClose }: { onClose: () => void }) {
  const [stats, setStats] = useState<RehearsalStats | null>();
  const [range, setRange] = useState<{ from: string; to: string }>();
  const [group, setGroup] = useState('');
  const [q, setQ] = useState('');
  const [error, setError] = useState<string>();
  const ids = { from: useId(), to: useId(), group: useId(), q: useId() };

  useEffect(() => {
    if (range && (!range.from || !range.to)) return;
    let live = true;
    rehearsalsApi.stats(range?.from, range?.to).then((o) => {
      if (!live) return;
      if (o.kind === 'ok') {
        setError(undefined);
        setStats(o.data);
      } else if (o.kind === 'invalid') setError(Object.values(o.errors)[0]);
      else setStats(null);
    });
    return () => {
      live = false;
    };
  }, [range]);

  const from = range?.from ?? stats?.from ?? '';
  const to = range?.to ?? stats?.to ?? '';
  const shown =
    stats?.members.filter(
      (m) => (!group || m.groups.includes(group as 'tuno')) && (!q.trim() || fold(`${m.name} ${m.fullName ?? ''}`).includes(fold(q.trim()))),
    ) ?? [];

  return (
    <Dialog title="Estatísticas de presenças" onClose={onClose} size="lg">
      <div className="events-tools stats-tools">
        <label className="control" htmlFor={ids.from}>
          <span>De</span>
          <input id={ids.from} type="date" value={from} onChange={(e) => setRange({ from: e.target.value, to })} />
        </label>
        <label className="control" htmlFor={ids.to}>
          <span>Até</span>
          <input id={ids.to} type="date" value={to} onChange={(e) => setRange({ from, to: e.target.value })} />
        </label>
        <label className="control control--select" htmlFor={ids.group}>
          <span className="sr-only">Categoria</span>
          <select id={ids.group} value={group} onChange={(e) => setGroup(e.target.value)}>
            <option value="">Todas as categorias</option>
            <option value="tuno">Tunos</option>
            <option value="caloiro">Caloiros</option>
            <option value="leitao">Leitões</option>
          </select>
        </label>
        <label className="control" htmlFor={ids.q}>
          <span className="sr-only">Procurar membro</span>
          <Icon name="search" />
          <input id={ids.q} type="search" placeholder="Procurar pelo nome" value={q} onChange={(e) => setQ(e.target.value)} />
        </label>
      </div>
      {error && <p className="form__error">{error}</p>}
      {stats === undefined ? (
        <Loading label="A carregar as estatísticas…" />
      ) : stats === null ? (
        <p>Não foi possível carregar as estatísticas agora.</p>
      ) : shown.length === 0 ? (
        <p className="note">{stats.members.length === 0 ? 'Sem presenças nestas datas.' : 'Nenhum membro encontrado.'}</p>
      ) : (
        <>
          <p className="note">{stats.pastRehearsals === 1 ? '1 ensaio já feito' : `${stats.pastRehearsals} ensaios já feitos`} nestas datas.</p>
          <ul className="who">
            {shown.map((m, i) => (
              <li key={i} className="who__person" title={m.fullName ?? undefined}>
                <img className="who__avatar" src={m.avatarUrl} alt="" loading="lazy" width="72" height="72" />
                <p className="who__name">{m.name}</p>
                {m.categories.length > 0 && (
                  <span className="member-badges">
                    {m.categories.map((c) => (
                      <MemberBadge key={c} kind={badgeKind(c)}>
                        {c}
                      </MemberBadge>
                    ))}
                  </span>
                )}
                <p className="who__meta">
                  {m.approved === 1 ? '1 presença' : `${m.approved} presenças`}
                  {m.pending > 0 && ` · ${m.pending} pendentes`}
                </p>
                {stats.pastRehearsals > 0 && m.approved > 0 && (
                  <p className="who__note">{(m.approved / stats.pastRehearsals).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}</p>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}

const RECENT = 10;

/** "As minhas presenças": the last 10 (or all) past, not cancelled rehearsals and the confirmed share. */
export function MyPresencesDialog({ past, onClose }: { past: RehearsalSummary[]; onClose: () => void }) {
  const [all, setAll] = useState(false);
  const id = useId();
  const done = past.filter((r) => !r.cancelled);
  const shown = all ? done : done.slice(0, RECENT);
  const went = shown.filter((r) => r.mine?.status === 'approved').length;

  return (
    <Dialog title="As minhas presenças" onClose={onClose} size="lg">
      {shown.length === 0 ? (
        <p className="note">Ainda não há ensaios passados.</p>
      ) : (
        <>
          <p className="presence-rate">
            Estiveste em <strong>{went}</strong> de {shown.length} {all ? 'ensaios' : `ensaios (os últimos ${RECENT})`} ·{' '}
            {(went / shown.length).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}
          </p>
          <label className="check" htmlFor={id}>
            <input id={id} type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
            Mostrar todos
          </label>
          <ul className="my-list">
            {shown.map((r) => (
              <li key={r.id}>
                <a className="presence-row" href={`/rehearsals/${r.id}`}>
                  <span>
                    <strong>{dateLabel({ date: r.date, endDate: null })}</strong>
                    {r.theme && <small>{r.theme}</small>}
                  </span>
                  <PresencePill rehearsal={r} fallback />
                </a>
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}
