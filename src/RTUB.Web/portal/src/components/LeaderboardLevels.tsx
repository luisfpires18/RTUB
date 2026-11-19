// Leaderboard levels explanation.
import { useId } from 'react';
import { Icon } from './icons';
import { type Entry, type Level, type Story } from '../data/leaderboardApi';

/**
 * "Níveis de Alcoolismo": the explanation and the 12 levels as one section, open by default (it still folds
 * away, the table is below it). Each level shows its number, name and XP threshold; the reader's own level for the
 * year shown (`me`, from the server) is marked and the ones below it read as reached. Names and thresholds are the
 * server's configuration, untouched here.
 */
export function Levels({ levels, story, me }: { levels: Level[]; story: Story | null; me: Entry | null }) {
  const titleId = useId();
  return (
    <details className="leaderboard-levels" open>
      <summary className="leaderboard-levels__head">
        <span className="leaderboard-levels__title" id={titleId}>
          Níveis de Alcoolismo
        </span>
        {me && (
          <span className="leaderboard-levels__me">
            O teu nível: <strong>{me.level} – {me.rankName}</strong>
          </span>
        )}
        <Icon name="chevronDown" />
      </summary>
      {story?.isActive && (
        <div className="leaderboard-story">
          <p className="leaderboard-story__title">
            <strong>{story.title}</strong>
          </p>
          <p className="leaderboard-story__text">{story.content}</p>
        </div>
      )}
      <ol className="leaderboard-level-list" aria-labelledby={titleId}>
        {levels.map((l) => {
          const state = !me ? '' : l.level === me.level ? ' is-current' : l.level < me.level ? ' is-reached' : '';
          return (
            <li key={l.level} className={`leaderboard-level${state}`} aria-current={state === ' is-current' ? 'true' : undefined}>
              <span className="leaderboard-level__n" aria-hidden="true">
                {l.level}
              </span>
              <span className="leaderboard-level__text">
                <span className="sr-only">Nível {l.level}: </span>
                <strong className="leaderboard-level__name">{l.name}</strong>
                <span className="leaderboard-level__xp">{l.xpThreshold === 0 ? 'Desde 0 XP' : `${l.xpThreshold} XP`}</span>
                {state === ' is-current' && <span className="leaderboard-level__you">O teu nível</span>}
              </span>
            </li>
          );
        })}
      </ol>
    </details>
  );
}
