import { useCallback, useEffect, useId, useState } from 'react';
import { Avatar } from '../components/ui/Avatar';
import { PageHead } from '../components/ui/PageHead';
import { getGovernance, type Governance as GovernanceData, type GovernanceMember } from '../data/api';
import { useCurrentUser, useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import {
  AssignDialog,
  CreateYearDialog,
  governanceApi,
  RemoveDialog,
  RgiDialog,
  type Holder as ManagedHolder,
  type Manage,
  type ManagePosition,
} from '../components/GovernanceManage';
import { Icon } from '../components/icons';
import { Notice, RetryNotice } from '../components/Notice';
import { Loading } from '../components/ui/Loading';
import { IconAction } from '../components/ui/IconAction';

/** ?fy=2024-2025 keeps a mandate shareable. */
const yearFromUrl = () => new URLSearchParams(location.search).get('fy') ?? undefined;

type Seat = { title: string; position?: string; holders: (GovernanceMember & { assignmentId?: number })[] };
type Open = { kind: 'rgi' } | { kind: 'year' } | { kind: 'assign'; seat: ManagePosition & { body: string } } | { kind: 'remove'; holder: ManagedHolder; title: string };

/**
 * /roles - Órgãos Sociais. Who held each position in one mandate (GET /api/public/governance), public fields only.
 * Signed-in members also open the RGI; Mod, Admin and Owner add fiscal years and assign or remove positions here
 * over GET /api/governance/manage, which also lists empty years.
 */
export default function Governance() {
  const [data, setData] = useState<GovernanceData>();
  const [manage, setManage] = useState<Manage | null>();
  const [failed, setFailed] = useState(false);
  const [year, setYear] = useState(yearFromUrl);
  const [open, setOpen] = useState<Open | null>(null);
  const { user } = useCurrentUser();
  const signedIn = user?.authenticated === true;
  // menu.governance = Mod/Admin/Owner (GovernanceAuthorization.CanManage): the others never ask, so no 403 in the console.
  const mayManage = user?.authenticated === true && user.menu.governance;
  const yearId = useId();

  const latest = useLatest();
  const load = useCallback(
    (fiscalYear?: string) => {
      setFailed(false);
      // a failure is applied the same way, so an old year's error cannot hide the new year's data
      latest(
        getGovernance(fiscalYear).then((d) => () => setData(d), () => () => setFailed(true)),
        (apply) => apply(),
      );
    },
    [latest],
  );

  useEffect(() => {
    document.title = 'Órgãos Sociais · RTUB';
  }, []);

  useEffect(() => load(year), [load, year]);

  // Management state only for Mod/Admin/Owner (the session says so); everyone else keeps the public view.
  useEffect(() => {
    if (!mayManage) return setManage(null);
    let live = true;
    setManage(undefined);
    governanceApi.manage(year).then((o) => live && setManage(o.kind === 'ok' ? o.data : null));
    return () => {
      live = false;
    };
  }, [mayManage, year]);

  const choose = (fiscalYear: string) => {
    history.replaceState(null, '', `${portal.roles}?fy=${encodeURIComponent(fiscalYear)}`);
    setData(undefined);
    setYear(fiscalYear);
  };

  /** After a write: show the mandate the server answered with, and refresh the public view behind it. */
  const saved = (m: Manage) => {
    setOpen(null);
    setManage(m);
    if (m.fiscalYear && m.fiscalYear !== year) choose(m.fiscalYear);
    else load(year);
  };

  const shown = manage ?? data;
  const years = shown?.fiscalYears ?? [];
  const canManage = !!manage;
  const showYears = !!shown && years.length > (canManage ? 0 : 1);
  // The server's own list of start years still missing (the rule POST /api/governance/years enforces): nothing left
  // to create, no button. The dialog keeps its "all registered" message and the server its validation.
  const canAddYear = !!manage && manage.availableStartYears.length > 0;

  return (
    <section className="page wrap" aria-labelledby="governance-page-title">
      <PageHead eyebrow="Quem conduz a tuna" title="Órgãos Sociais" titleId="governance-page-title" lead="Direção, Mesa da Assembleia, Conselho Fiscal e Conselho de Veteranos: quem ocupou cada cargo em cada ano letivo.">
        {(showYears || canAddYear) && (
          <div className="governance-page__tools">
            {showYears && (
              <label className="governance-page__year" htmlFor={yearId}>
                Ano letivo
                <span className="control control--select">
                  <select id={yearId} value={shown?.fiscalYear ?? ''} onChange={(e) => choose(e.target.value)}>
                    {years.map((y) => (
                      <option key={y} value={y}>
                        {y}
                      </option>
                    ))}
                  </select>
                </span>
              </label>
            )}
            {canAddYear && (
              <button type="button" className="btn btn--primary btn--sm governance-page__add" onClick={() => setOpen({ kind: 'year' })}>
                <Icon name="plus" />
                Adicionar ano letivo
              </button>
            )}
          </div>
        )}
      </PageHead>

      {signedIn && (
        <div className="notice governance-page__rgi">
          <p>
            <strong>Regulamentos Gerais Internos</strong>
            <br />
            Os regulamentos e normas internas da RTUB.
          </p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'rgi' })}>
            <Icon name="lyrics" />
            Abrir RGI
          </button>
        </div>
      )}

      {failed && !manage ? (
        <RetryNotice onRetry={() => load(year)}>Não foi possível carregar os órgãos sociais agora.</RetryNotice>
      ) : !shown || (mayManage && manage === undefined) ? (
        <Loading label="A carregar os órgãos sociais…" />
      ) : !shown.fiscalYear ? (
        <Notice>{canManage ? 'Ainda não há anos letivos. Adiciona o primeiro.' : 'Ainda não há órgãos sociais registados.'}</Notice>
      ) : (
        <>
          <h2 className="governance-page__mandate">Mandato {shown.fiscalYear}</h2>
          <div className="bodies">
            {shown.bodies.map((body) => (
              <section key={body.name} className="body-card" aria-label={body.name}>
                <h3 className="body-card__name">
                  <Icon name="bank" />
                  {body.name}
                </h3>
                <ul className="body-card__positions">
                  {(body.positions as Seat[]).map((seat) => (
                    <li key={seat.title} className="seat">
                      <p className="seat__title">{seat.title}</p>
                      {seat.holders.length === 0 ? (
                        <div className="seat__holder">
                          <p className="seat__empty">Sem registo</p>
                          {canManage && seat.position && (
                            <button
                              type="button"
                              className="btn btn--ghost btn--sm seat__action"
                              onClick={() => setOpen({ kind: 'assign', seat: { ...(seat as ManagePosition), body: body.name } })}
                            >
                              <Icon name="plus" />
                              Atribuir
                            </button>
                          )}
                        </div>
                      ) : (
                        seat.holders.map((member, i) => (
                          <Holder
                            key={member.assignmentId ?? i}
                            member={member}
                            onRemove={
                              canManage && member.assignmentId
                                ? () => setOpen({ kind: 'remove', holder: member as ManagedHolder, title: `${seat.title} (${body.name})` })
                                : undefined
                            }
                          />
                        ))
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </>
      )}

      {open?.kind === 'rgi' && <RgiDialog onClose={() => setOpen(null)} />}
      {open?.kind === 'year' && manage && (
        <CreateYearDialog years={manage.availableStartYears} onDone={saved} onClose={() => setOpen(null)} />
      )}
      {open?.kind === 'assign' && manage?.fiscalYear && (
        <AssignDialog fiscalYear={manage.fiscalYear} seat={open.seat} onDone={saved} onClose={() => setOpen(null)} />
      )}
      {open?.kind === 'remove' && <RemoveDialog holder={open.holder} title={open.title} onDone={saved} onClose={() => setOpen(null)} />}
    </section>
  );
}

function Holder({ member, onRemove }: { member: GovernanceMember; onRemove?: () => void }) {
  return (
    <div className="seat__holder">
      <Avatar className="seat__avatar" src={member.avatarUrl} size={56} />
      <span className="seat__who">
        <span className="seat__name">{member.displayName}</span>
        {member.fullName && <span className="seat__full">{member.fullName}</span>}
      </span>
      {onRemove && (
        <IconAction icon="trash" label={`Remover ${member.displayName}`} onClick={onRemove} danger className="seat__action" />
      )}
    </div>
  );
}
