import { useEffect, useState } from 'react';
import { PageHead } from '../components/ui/PageHead';
import { portal } from '../data/content';
import { memberAreaApi, type FameRecord, type HallOfFame as Fame } from '../data/memberAreaApi';
import { Notice, SignInNotice } from '../components/Notice';
import { Loading } from '../components/ui/Loading';
import { Avatar } from '../components/ui/Avatar';

/**
 * /hall-of-fame. The tuna's twelve records, as the server computes them
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
      <PageHead eyebrow="Área de membros" title="Hall of Fame" titleId="fame-title" lead="Os recordes da RTUB, de quem passou a Tuno mais depressa a quem mais ensaios somou." />

      {fame === undefined ? (
        <Loading label="A calcular os recordes…" />
      ) : fame === 'signin' ? (
        <SignInNotice path={portal.hallOfFame}>O Hall of Fame é da área de membros.</SignInNotice>
      ) : fame === null ? (
        <Notice>Não foi possível carregar os recordes. Tenta outra vez daqui a pouco.</Notice>
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
                <Avatar className="who__avatar" src={w.avatarUrl} size={72} />
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
