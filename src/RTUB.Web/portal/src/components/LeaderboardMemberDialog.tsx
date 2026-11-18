// Leaderboard member details dialog.
import { useEffect, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { type MemberDetail, api } from '../data/leaderboardApi';
import { Comments } from './LeaderboardComments';
import { Loading } from './ui/Loading';
import { Avatar } from './ui/Avatar';

/** "Detalhes da Classificação": level and progress for the year shown, XP origin and activities of all time, comments. */
export function MemberDialog({ id, fiscalYear, onClose }: { id: string; fiscalYear: string; onClose: () => void }) {
  const [detail, setDetail] = useState<MemberDetail | 'missing' | null>();

  useEffect(() => {
    api.member(id, fiscalYear).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id, fiscalYear]);

  return (
    <Dialog title="Detalhes da classificação" size="lg" onClose={onClose}>
      {detail === undefined ? (
        <Loading label="A carregar…" />
      ) : detail === 'missing' ? (
        <p className="form__banner" role="alert">
          Este membro já não existe.
        </p>
      ) : detail === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar os detalhes.
        </p>
      ) : (
        <div className="member-detail">
          <div className="member-detail__head">
            <Avatar className="who__avatar" src={detail.avatarUrl} size={96} />
            <p className="member-detail__name">{detail.displayName}</p>
            {detail.fullName && <p className="who__meta">{detail.fullName}</p>}
            <span className="member-badges">
              <MemberBadge kind="position">Nível {detail.progress.level}</MemberBadge>
              <MemberBadge>{detail.progress.rankName}</MemberBadge>
            </span>
          </div>

          <section className="member-section" aria-label="Estatísticas">
            <h3 className="member-section__title">Estatísticas</h3>
            <dl className="member-fields">
              <div className="member-field">
                <dt>XP total</dt>
                <dd>{detail.breakdown.totalXp}</dd>
              </div>
            </dl>
            {detail.breakdown.totalXp > 0 && (
              <ul className="member-rows leaderboard-xp">
                <li className="member-row">
                  <Icon name="music" />
                  <span className="member-row__who">
                    <strong>Ensaios</strong>
                    <small>
                      {detail.breakdown.rehearsalCount} × {detail.breakdown.rehearsalXpPerUnit} XP
                    </small>
                  </span>
                  <MemberBadge>{detail.breakdown.rehearsalXpTotal} XP</MemberBadge>
                </li>
                {detail.breakdown.eventsByType.length === 0 ? (
                  <li className="note">Nenhuma atuação registada</li>
                ) : (
                  detail.breakdown.eventsByType.map((t) => (
                    <li key={t.type} className="member-row">
                      <Icon name="calendar" />
                      <span className="member-row__who">
                        <strong>{t.type}</strong>
                        <small>
                          {t.count} × {t.xpPerUnit} XP
                        </small>
                      </span>
                      <MemberBadge>{t.totalXp} XP</MemberBadge>
                    </li>
                  ))
                )}
              </ul>
            )}
          </section>

          <details className="who__more" open>
            <summary>Ver atuações e ensaios detalhados</summary>
            {detail.activities.length === 0 ? (
              <p className="note">Nenhuma atividade registada</p>
            ) : (
              <ul className="member-activities">
                {detail.activities.map((a, i) => (
                  <li key={i}>
                    <Icon name={a.isRehearsal ? 'music' : 'calendar'} />
                    <span>
                      <strong>{a.name}</strong>
                      <small>
                        {a.date.split('-').reverse().join('/')} · {a.type}
                      </small>
                    </span>
                    <MemberBadge kind="active">+{a.xpEarned} XP</MemberBadge>
                  </li>
                ))}
              </ul>
            )}
          </details>

          <details className="who__more">
            <summary>Nível de alcoolismo</summary>
            <dl className="member-fields">
              <div className="member-field">
                <dt>Nível atual</dt>
                <dd>
                  {detail.progress.level} – {detail.progress.rankName}
                  {detail.progress.isMaxLevel && ' (nível máximo)'}
                </dd>
              </div>
              <div className="member-field">
                <dt>XP</dt>
                <dd>{detail.progress.xp}</dd>
              </div>
              {!detail.progress.isMaxLevel && (
                <div className="member-field">
                  <dt>Próximo nível</dt>
                  <dd>
                    {detail.progress.level + 1}
                    {detail.progress.nextRankName && ` – ${detail.progress.nextRankName}`}, faltam {detail.progress.xpToNextLevel} XP
                  </dd>
                </div>
              )}
            </dl>
            {!detail.progress.isMaxLevel && (
              <progress className="member-progress__bar" max={100} value={detail.progress.progressPercentage} aria-label="Progresso para o próximo nível" />
            )}
          </details>

          <Comments memberId={detail.id} />
        </div>
      )}
    </Dialog>
  );
}
