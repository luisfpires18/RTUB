import { useCallback, useEffect, useId, useState } from 'react';
import { getGovernance, type Governance as GovernanceData, type GovernanceMember } from './api';
import { Loading, useCurrentUser } from './App';
import { portal } from './content';
import {
  AssignDialog,
  CreateYearDialog,
  governanceApi,
  RemoveDialog,
  RgiDialog,
  type Holder as ManagedHolder,
  type Manage,
  type ManagePosition,
} from './GovernanceManage';
import { Icon } from './icons';

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** ?fy=2024-2025 keeps a mandate shareable, as the Blazor page did. */
const yearFromUrl = () => new URLSearchParams(location.search).get('fy') ?? undefined;

type Seat = { title: string; position?: string; holders: (GovernanceMember & { assignmentId?: number })[] };
type Open = { kind: 'rgi' } | { kind: 'year' } | { kind: 'assign'; seat: ManagePosition & { body: string } } | { kind: 'remove'; holder: ManagedHolder; title: string };

/**
 * /roles - Órgãos Sociais. Who held each position in one mandate (GET /api/public/governance), public fields only.
 * Signed-in members also open the RGI; Mod, Admin and Owner add fiscal years and assign or remove positions here
 * (React track 016, was the Blazor /member/roles), over GET /api/governance/manage, which also lists empty years.
 */
export default function Governance() {
  const [data, setData] = useState<GovernanceData>();
  const [manage, setManage] = useState<Manage | null>();
  const [failed, setFailed] = useState(false);
  const [year, setYear] = useState(yearFromUrl);
  const [open, setOpen] = useState<Open | null>(null);
  const { user } = useCurrentUser();
  const signedIn = user?.authenticated === true;
  const yearId = useId();

  const load = useCallback((fiscalYear?: string) => {
    setFailed(false);
    getGovernance(fiscalYear).then(setData, () => setFailed(true));
  }, []);

  useEffect(() => {
    document.title = 'Órgãos Sociais · RTUB';
  }, []);

  useEffect(() => load(year), [load, year]);

  // Management state only for Mod/Admin/Owner; anyone else gets 401/403 and keeps the public view.
  useEffect(() => {
    if (!signedIn) return setManage(null);
    setManage(undefined);
    governanceApi.manage(year).then((o) => setManage(o.kind === 'ok' ? o.data : null));
  }, [signedIn, year]);

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

  return (
    <section className="page wrap" aria-labelledby="governance-page-title">
      <header className="page__head governance-page__head">
        <div>
          <p className="eyebrow">Quem conduz a tuna</p>
          <h1 id="governance-page-title" className="page__title">
            Órgãos Sociais
          </h1>
          <p className="page__lead">
            Direção, Mesa da Assembleia, Conselho Fiscal e Conselho de Veteranos: quem ocupou cada cargo em cada ano
            letivo.
          </p>
        </div>
        <div className="governance-page__tools">
          {shown && years.length > (canManage ? 0 : 1) && (
            <label className="governance-page__year" htmlFor={yearId}>
              Ano letivo
              <span className="control control--select">
                <select id={yearId} value={shown.fiscalYear ?? ''} onChange={(e) => choose(e.target.value)}>
                  {years.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </span>
            </label>
          )}
          {canManage && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'year' })}>
              <Icon name="plus" />
              Adicionar ano letivo
            </button>
          )}
        </div>
      </header>

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
        <div className="notice" role="status">
          <p>Não foi possível carregar os órgãos sociais agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load(year)}>
            Tentar novamente
          </button>
        </div>
      ) : !shown || (signedIn && manage === undefined) ? (
        <Loading label="A carregar os órgãos sociais…" />
      ) : !shown.fiscalYear ? (
        <div className="notice" role="status">
          <p>{canManage ? 'Ainda não há anos letivos. Adiciona o primeiro.' : 'Ainda não há órgãos sociais registados.'}</p>
        </div>
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
      <img
        className="seat__avatar"
        src={member.avatarUrl ?? DEFAULT_AVATAR}
        alt=""
        width="56"
        height="56"
        loading="lazy"
        onError={(e) => {
          if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
        }}
      />
      <span className="seat__who">
        <span className="seat__name">{member.displayName}</span>
        {member.fullName && <span className="seat__full">{member.fullName}</span>}
      </span>
      {onRemove && (
        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger seat__action" onClick={onRemove}>
          <Icon name="trash" />
          <span className="sr-only">Remover {member.displayName}</span>
        </button>
      )}
    </div>
  );
}
