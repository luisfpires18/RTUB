// Event answer statistics dialogs: the member's own record and everyone's "vou" counts.

import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { portal } from '../data/content';
import { eventsApi, dateLabel, type EventSummary, type EventStats } from '../data/eventsApi';
import { Icon } from './icons';
import { MemberBadge, badgeKind } from './MemberBadge';
import { fold } from '../data/format';

// ---------- statistics (members) ----------

/**
 * "Estatísticas de inscrições": for the dates chosen (the current season
 * by default), how many events each member said "vou" to - already past, and still to come - and the
 * share of that range's past, not cancelled events. Read-only; the server refuses visitors.
 */
const RECENT = 10;

/** "Onde estiveste": the last 10 (or all) past, not cancelled events and the share the member went to. */
export function WhereYouWereDialog({ past, onClose }: { past: EventSummary[]; onClose: () => void }) {
  const [all, setAll] = useState(false);
  const id = useId();
  const done = past.filter((e) => !e.cancelled);
  const shown = all ? done : done.slice(0, RECENT);
  const went = shown.filter((e) => e.member?.myStatus === 'going').length;

  return (
    <Dialog title="Onde estiveste" onClose={onClose} size="lg">
      {shown.length === 0 ? (
        <p className="note">Ainda sem atuações no arquivo.</p>
      ) : (
        <>
          <p className="enrollment-rate">
            Foste a <strong>{went}</strong> de {shown.length} {all ? 'atuações' : `atuações (as últimas ${RECENT})`} ·{' '}
            {(went / shown.length).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}
          </p>
          <label className="check" htmlFor={id}>
            <input id={id} type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
            Mostrar todas
          </label>
          <ul className="my-list">
            {shown.map((e) => (
              <li key={e.id}>
                <a className="enrollment-row" href={portal.event(e.id)}>
                  <span>
                    <strong>{e.name}</strong>
                    <small>
                      {dateLabel(e)} · {e.location}
                    </small>
                  </span>
                  {e.member?.myStatus === 'going' ? (
                    <span className="pill pill--yes">
                      <Icon name="check" />
                      Foste
                    </span>
                  ) : (
                    <span className="pill pill--no">
                      <Icon name="close" />
                      Não foste
                    </span>
                  )}
                </a>
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}

export function StatsDialog({ onClose }: { onClose: () => void }) {
  const [stats, setStats] = useState<EventStats | null>();
  const [range, setRange] = useState<{ from: string; to: string }>();
  const [group, setGroup] = useState('');
  const [q, setQ] = useState('');
  const [error, setError] = useState<string>();
  const ids = { from: useId(), to: useId(), group: useId(), q: useId() };

  useEffect(() => {
    // Both dates complete (or none: the server's default season), never a half-typed one.
    if (range && (!range.from || !range.to)) return;
    let live = true;
    eventsApi.stats(range?.from, range?.to).then((o) => {
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
      (m) =>
        (!group || m.groups.includes(group as 'tuno')) &&
        (!q.trim() || fold(`${m.name} ${m.fullName ?? ''}`).includes(fold(q.trim()))),
    ) ?? [];

  return (
    <Dialog title="Estatísticas de inscrições" onClose={onClose} size="lg">
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
        <p className="note">{stats.members.length === 0 ? 'Sem inscrições nestas datas.' : 'Nenhum membro encontrado.'}</p>
      ) : (
        <>
          <p className="note">
            {stats.pastEvents === 1 ? '1 atuação já feita' : `${stats.pastEvents} atuações já feitas`} nestas datas.
          </p>
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
                  {m.went === 1 ? '1 participação' : `${m.went} participações`}
                  {m.going > 0 && ` · ${m.going} por fazer`}
                </p>
                {stats.pastEvents > 0 && m.went > 0 && (
                  <p className="who__note">
                    {(m.went / stats.pastEvents).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}
                  </p>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </Dialog>
  );
}
