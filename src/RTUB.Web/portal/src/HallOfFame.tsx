import { useEffect, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { memberAreaApi, type FameRecord, type HallOfFame as Fame } from './memberAreaApi';
import { loginTo } from './musicApi';

/**
 * /hall-of-fame (task 032; was the Blazor page). The tuna's twelve records, as the server computes them
 * (HallOfFameService): a tie lists every member who shares the record, and a record nobody holds yet says so.
 * Signed-in members only.
 */
export default function HallOfFame() {
  const [fame, setFame] = useState<Fame | 'signin' | null>();

  useEffect(() => {
    document.title = 'Hall of Fame · RTUB';
    memberAreaApi.hallOfFame().then((o) => setFame(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  }, []);

  return (
    <section className="page wrap fame-page" aria-labelledby="fame-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="fame-title" className="page__title">
            Hall of Fame
          </h1>
          <p className="page__lead">Os recordes da RTUB, de quem passou a Tuno mais depressa a quem mais ensaios somou.</p>
        </div>
        <div className="events-page__actions">
          <a className="btn btn--ghost btn--sm" href={portal.leaderboard}>
            <Icon name="trophy" />
            Classificação
          </a>
        </div>
      </header>

      {fame === undefined ? (
        <Loading label="A calcular os recordes…" />
      ) : fame === 'signin' ? (
        <div className="notice" role="status">
          <p>O Hall of Fame é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.hallOfFame)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : fame === null ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar os recordes. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <ol className="fame-grid">
          {fame.categories.map((c) => (
            <FamePanel key={c.key} record={c} />
          ))}
        </ol>
      )}
    </section>
  );
}

function FamePanel({ record }: { record: FameRecord }) {
  const titleId = `fame-${record.key}`;
  return (
    <li className="fame-panel" aria-labelledby={titleId}>
      <h2 id={titleId} className="fame-panel__title">
        {record.title}
      </h2>
      {record.winners.length === 0 ? (
        <p className="note fame-panel__empty">Sem dados para este recorde, por enquanto.</p>
      ) : (
        <>
          <ul className="fame-winners">
            {record.winners.map((w, i) => (
              <li key={i} className="fame-winner">
                <MemberFace avatarUrl={w.avatarUrl} size={72} />
                <strong>{w.displayName}</strong>
                {w.fullName && <small>{w.fullName}</small>}
              </li>
            ))}
          </ul>
          {record.value && <p className="fame-panel__value">{record.value}</p>}
        </>
      )}
    </li>
  );
}
