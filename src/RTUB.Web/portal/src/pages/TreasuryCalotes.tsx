import { useEffect, useId, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { matches, problem, treasuryApi, type Calotes, type Person } from '../data/treasuryApi';
import { Refusal, TreasuryTabs } from '../components/TreasuryUi';
import { day, euros } from '../data/format';
import { DEFAULT_AVATAR } from '../data/membersApi';
import { type Debt, key } from '../data/treasuryCalotesView';
import { DebtsDialog, DebtDialog } from '../components/TreasuryCalotesDialogs';
import { Loading } from '../components/ui/Loading';
import { AsyncConfirmDialog } from '../components/ui/ConfirmDialog';
import { PageHead } from '../components/ui/PageHead';
import { BackLink } from '../components/ui/BackLink';

/**
 * /treasury/calotes - Calotes. Treasury members see
 * every member's debts of a fiscal year; Caloiros and Leitões only their own. Mod, Admin and Owner add, edit, remove and
 * set the commitment date ("Compromisso de pagamento") that pauses the daily reminder.
 */
export default function TreasuryCalotes() {
  const [fy, setFy] = useState(() => new URLSearchParams(location.search).get('fy') ?? '');
  const [data, setData] = useState<Calotes | 'signin' | 'failed'>();
  const [search, setSearch] = useState('');
  const [viewing, setViewing] = useState<string>(); // a group's key
  const [form, setForm] = useState<{ member?: Person; debt?: Debt }>();
  const [deleting, setDeleting] = useState<{ debt: Debt; who: string }>();
  const ids = { search: useId(), year: useId() };

  const load = (year = fy) =>
    treasuryApi.calotes(year).then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : 'failed'));

  useEffect(() => {
    load();
  }, []);

  const c = data && typeof data === 'object' ? data : null;
  const own = c?.scope === 'own';

  useEffect(() => {
    document.title = `${own ? 'Os meus calotes' : 'Calotes'} · Tesouraria · RTUB`;
  }, [own]);

  const changeYear = (year: string) => {
    setFy(year);
    setSearch('');
    history.replaceState(null, '', `${portal.treasuryCalotes}?fy=${encodeURIComponent(year)}`);
    load(year);
  };

  const saved = (next: Calotes) => setData(next);
  const groups = c?.groups.filter((g) => matches(search, g.member.displayName, g.member.fullName)) ?? [];
  const group = c?.groups.find((g, i) => key(g, i) === viewing);

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      {!own && <BackLink href={portal.treasury}>Tesouraria</BackLink>}
      <PageHead
        eyebrow="Área de membros"
        title={own ? 'Os meus calotes' : 'Calotes'}
        titleId="tr-title"
        lead={own ? 'O que deves à tuna, por ano letivo.' : 'Registo das dívidas dos membros à tuna.'}
      >
        {c?.canManage && c.fiscalYear && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar calote
            </button>
          </div>
        )}
      </PageHead>

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasuryCalotes} onRetry={() => load()} />
      ) : !c ? (
        <Loading label="A carregar os calotes…" />
      ) : (
        <>
          {!own && <TreasuryTabs current="calotes" fiscalYear={c.fiscalYear ?? undefined} />}
          {c.fiscalYears.length === 0 ? (
            <p className="note">Ainda não há anos letivos.</p>
          ) : (
            <>
              <div className="events-tools" role="search">
                {!own && (
                  <label className="control" htmlFor={ids.search}>
                    <span className="sr-only">Procurar membro</span>
                    <Icon name="search" />
                    <input id={ids.search} type="search" placeholder="Procurar membro" value={search} onChange={(e) => setSearch(e.target.value)} />
                  </label>
                )}
                <label className="control control--select" htmlFor={ids.year}>
                  <span className="sr-only">Ano letivo</span>
                  <Icon name="calendar" />
                  <select id={ids.year} value={c.fiscalYear ?? ''} onChange={(e) => changeYear(e.target.value)}>
                    {c.fiscalYears.map((y) => (
                      <option key={y} value={y}>
                        {y}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <dl className="tr-tiles tr-tiles--one">
                <div className="tr-tile">
                  <dt>{own ? 'Total em dívida' : 'Total de calotes'}</dt>
                  <dd className="tr-out">{euros(c.total)}</dd>
                </div>
              </dl>

              {c.groups.length === 0 ? (
                <p className="note">{own ? 'Não tens calotes neste ano letivo.' : 'Não há dívidas registadas neste ano letivo.'}</p>
              ) : groups.length === 0 ? (
                <p className="note">Nenhum membro corresponde à pesquisa.</p>
              ) : (
                <ul className="tr-debtors">
                  {groups.map((g) => (
                    <li key={key(g, c.groups.indexOf(g))} className="tr-debtor">
                      <img src={g.member.avatarUrl ?? DEFAULT_AVATAR} alt="" width="64" height="64" loading="lazy" />
                      <strong>{g.member.displayName}</strong>
                      {g.member.fullName && g.member.fullName !== g.member.displayName && <span className="tr-muted">{g.member.fullName}</span>}
                      <span className="pill pill--no">{euros(g.total)}</span>
                      {g.compromisedUntil && (
                        <span className="badge">
                          <Icon name="calendar" />
                          Compromisso até {day(g.compromisedUntil)}
                        </span>
                      )}
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setViewing(key(g, c.groups.indexOf(g)))}>
                        Ver detalhes
                        <span className="sr-only"> de {g.member.displayName}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}

          {group && (
            <DebtsDialog
              group={group}
              calotes={c}
              onClose={() => setViewing(undefined)}
              onSaved={saved}
              onEdit={(debt) => setForm({ member: group.member, debt })}
              onDelete={(debt) => setDeleting({ debt, who: group.member.displayName })}
              onAdd={() => setForm({ member: group.member })}
            />
          )}
          {form && c.fiscalYear && (
            <DebtDialog
              fiscalYear={c.fiscalYear}
              member={form.member}
              debt={form.debt}
              onClose={() => setForm(undefined)}
              onSaved={saved}
            />
          )}
          {deleting && (
            <AsyncConfirmDialog danger
              title="Remover calote"
              confirmLabel="Remover"
              onClose={() => setDeleting(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.removeDebt(deleting.debt.id);
                if (o.kind !== 'ok') return problem(o, 'remover o calote');
                saved(o.data);
              }}
            >
              <p>
                Remover o calote de <strong>{deleting.who}</strong> ({euros(deleting.debt.amount)})?
              </p>
            </AsyncConfirmDialog>
          )}
        </>
      )}
    </section>
  );
}
