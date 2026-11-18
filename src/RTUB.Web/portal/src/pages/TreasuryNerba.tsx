import { useEffect, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { problem, treasuryApi, type Nerba, type NerbaSummary } from '../data/treasuryApi';
import { Refusal, TreasuryTabs } from '../components/TreasuryUi';

import { day, euros } from '../data/format';
import { Loading } from '../components/ui/Loading';
import { AsyncConfirmDialog } from '../components/ui/ConfirmDialog';
import { PageHead } from '../components/ui/PageHead';
import { BackLink } from '../components/ui/BackLink';
import { IconAction } from '../components/ui/IconAction';
import { EmptyNote } from '../components/ui/EmptyNote';
/**
 * /treasury/nerba - Encomendas Nerba. Treasury members:
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
              <IconAction icon="trash" label={`Eliminar as encomendas de ${s.event.name}`} onClick={() => setDeleting(s)} danger />
            </span>
          )}
        </li>
      ))}
    </ul>
  );

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <BackLink href={portal.treasury}>Tesouraria</BackLink>
      <PageHead eyebrow="Área de membros" title="Encomendas Nerba" titleId="tr-title" lead="O material encomendado para cada evento Nerba." />

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasuryNerba} onRetry={load} />
      ) : !n ? (
        <Loading label="A carregar as encomendas…" />
      ) : (
        <>
          <TreasuryTabs current="nerba" />
          {n.upcoming.length === 0 && n.past.length === 0 ? (
            <EmptyNote>Não há eventos Nerba.</EmptyNote>
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
            <AsyncConfirmDialog danger
              title="Eliminar encomendas"
              confirmLabel="Eliminar"
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
            </AsyncConfirmDialog>
          )}
        </>
      )}
    </section>
  );
}
