import { useCallback, useEffect, useId, useState } from 'react';
import { getGovernance, type Governance as GovernanceData, type GovernanceMember } from './api';
import { Loading, useCurrentUser } from './App';
import { legacy, portal } from './content';
import { Icon } from './icons';

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** ?fy=2024-2025 keeps a mandate shareable, as the Blazor page did. */
const yearFromUrl = () => new URLSearchParams(location.search).get('fy') ?? undefined;

/**
 * /roles - Órgãos Sociais. Who held each position in one mandate, from the positions the tuna
 * records (GET /api/public/governance). Public fields only; members get a way to the RGI and the
 * management tools, which stay on the Blazor /member/roles.
 */
export default function Governance() {
  const [data, setData] = useState<GovernanceData>();
  const [failed, setFailed] = useState(false);
  const [year, setYear] = useState(yearFromUrl);
  const { user } = useCurrentUser();
  const yearId = useId();

  const load = useCallback((fiscalYear?: string) => {
    setFailed(false);
    getGovernance(fiscalYear).then(setData, () => setFailed(true));
  }, []);

  useEffect(() => {
    document.title = 'Órgãos Sociais · RTUB';
  }, []);

  useEffect(() => load(year), [load, year]);

  const choose = (fiscalYear: string) => {
    history.replaceState(null, '', `${portal.roles}?fy=${encodeURIComponent(fiscalYear)}`);
    setData(undefined);
    setYear(fiscalYear);
  };

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
        {data && data.fiscalYears.length > 1 && (
          <label className="governance-page__year" htmlFor={yearId}>
            Ano letivo
            <span className="control control--select">
              <select id={yearId} value={data.fiscalYear ?? ''} onChange={(e) => choose(e.target.value)}>
                {data.fiscalYears.map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </select>
            </span>
          </label>
        )}
      </header>

      {failed ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar os órgãos sociais agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load(year)}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar os órgãos sociais…" />
      ) : !data.fiscalYear ? (
        <div className="notice" role="status">
          <p>Ainda não há órgãos sociais registados.</p>
        </div>
      ) : (
        <>
          <h2 className="governance-page__mandate">Mandato {data.fiscalYear}</h2>
          <div className="bodies">
            {data.bodies.map((body) => (
              <section key={body.name} className="body-card" aria-label={body.name}>
                <h3 className="body-card__name">
                  <Icon name="bank" />
                  {body.name}
                </h3>
                <ul className="body-card__positions">
                  {body.positions.map((position) => (
                    <li key={position.title} className="seat">
                      <p className="seat__title">{position.title}</p>
                      {position.holders.length === 0 ? (
                        <p className="seat__empty">Sem registo</p>
                      ) : (
                        position.holders.map((member, i) => <Holder key={i} member={member} />)
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </>
      )}

      {user?.authenticated && (
        <div className="notice governance-page__members">
          <p>Os Regulamentos Gerais Internos e a gestão dos cargos estão na área de membros.</p>
          <a className="btn btn--ghost btn--sm" href={legacy.memberGovernance}>
            <Icon name="arrow" />
            Abrir na área de membros
          </a>
        </div>
      )}
    </section>
  );
}

function Holder({ member }: { member: GovernanceMember }) {
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
    </div>
  );
}
