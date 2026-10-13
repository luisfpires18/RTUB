import { useEffect, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { day, euros, problem, treasuryApi, type Nerba, type NerbaSummary } from './treasuryApi';
import { Confirm, Refusal, TreasuryTabs } from './TreasuryUi';

/**
 * /treasury/nerba - Encomendas Nerba (React track 024; was the Blazor /nerba, which visitors could open). Treasury members:
 * the Nerba events, upcoming (nearest first) then past (latest first), with item count and total. Mod, Admin and Owner
 * delete every order of an event.
 */
export default function TreasuryNerba() {
  const [data, setData] = useState<Nerba | 'signin' | 'forbidden' | 'failed'>();
  const [deleting, setDeleting] = useState<NerbaSummary>();

  useEffect(() => {
    document.title = 'Nerba · Tesouraria · RTUB';
  }, []);

  const load = () =>
    treasuryApi.nerba().then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' || o.kind === 'forbidden' ? o.kind : 'failed'));

  useEffect(() => {
    load();
  }, []);

  const n = data && typeof data === 'object' ? data : null;

  const grid = (list: NerbaSummary[]) => (
    <ul className="lx-boards">
      {list.map((s) => (
        <li key={s.event.id} className="lx-board">
          <a className="lx-board__open" href={portal.treasuryNerbaEvent(s.event.id)}>
            <span className="lx-board__icon" aria-hidden="true">
              <Icon name="calendar" />
            </span>
            <strong>{s.event.name}</strong>
            <span className="tr-muted">
              {day(s.event.date)}
              {s.event.endDate && ` — ${day(s.event.endDate)}`}
            </span>
            <span className="tr-badges">
              <span className="badge">
                {s.count} {s.count === 1 ? 'item' : 'itens'}
              </span>
              <span className="pill pill--yes">{euros(s.total)}</span>
            </span>
          </a>
          {n?.canManage && s.count > 0 && (
            <span className="lx-board__tools">
              <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setDeleting(s)}>
                <Icon name="trash" />
                <span className="sr-only">Eliminar as encomendas de {s.event.name}</span>
              </button>
            </span>
          )}
        </li>
      ))}
    </ul>
  );

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <a className="back-link" href={portal.treasury}>
        <Icon name="arrow" />
        Tesouraria
      </a>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="tr-title" className="page__title">
            Encomendas Nerba
          </h1>
          <p className="page__lead">O material encomendado para cada evento Nerba.</p>
        </div>
      </header>

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasuryNerba} onRetry={load} />
      ) : !n ? (
        <Loading label="A carregar as encomendas…" />
      ) : (
        <>
          <TreasuryTabs current="nerba" />
          {n.upcoming.length === 0 && n.past.length === 0 ? (
            <p className="note">Não há eventos Nerba.</p>
          ) : (
            <>
              {n.upcoming.length > 0 && (
                <section className="lx-section" aria-labelledby="tr-upcoming">
                  <h2 id="tr-upcoming" className="lx-section__title">
                    Próximos eventos <small>{n.upcoming.length}</small>
                  </h2>
                  {grid(n.upcoming)}
                </section>
              )}
              {n.past.length > 0 && (
                <section className="lx-section" aria-labelledby="tr-past">
                  <h2 id="tr-past" className="lx-section__title">
                    Eventos passados <small>{n.past.length}</small>
                  </h2>
                  {grid(n.past)}
                </section>
              )}
            </>
          )}
          {deleting && (
            <Confirm
              title="Eliminar encomendas"
              action="Eliminar"
              onClose={() => setDeleting(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.removeNerbaOrders(deleting.event.id);
                if (o.kind !== 'ok') return problem(o, 'eliminar as encomendas');
                setData(o.data);
              }}
            >
              <p>
                Eliminar todas as encomendas de <strong>{deleting.event.name}</strong> ({deleting.count} {deleting.count === 1 ? 'item' : 'itens'})?
              </p>
              <p className="warning">
                <Icon name="warning" />
                Não dá para desfazer.
              </p>
            </Confirm>
          )}
        </>
      )}
    </section>
  );
}
