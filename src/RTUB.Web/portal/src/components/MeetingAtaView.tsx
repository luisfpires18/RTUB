// The "Ver Ata" dialog of /meetings: the published ata and the attendee confirm / refuse.
import { useEffect, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { problem } from './MeetingUi';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingAtaView, MeetingCard } from '../data/meetingsTypes';
import { numericDateTime } from '../data/meetingsDates';
import { Block } from './MeetingAtaParts';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';

/** "Ver Ata": the ata as published (or a writer's own draft), and the attendee's confirm / refuse. */
export function AtaViewDialog({ card, onClose }: { card: MeetingCard; onClose: () => void }) {
  const [ata, setAta] = useState<MeetingAtaView | null>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    meetingsApi.ata(card.id).then((o) => {
      if (o.kind === 'ok') setAta(o.data);
      else {
        setAta(null);
        setError(o.kind === 'notfound' ? 'Ata não encontrada.' : problem(o, 'abrir a ata'));
      }
    });
  }, [card.id]);

  const answer = async (confirm: boolean) => {
    setBusy(true);
    setError(undefined);
    const o = await meetingsApi.confirmAta(card.id, confirm);
    setBusy(false);
    if (o.kind === 'ok') setAta(o.data);
    else setError(problem(o, confirm ? 'confirmar a ata' : 'recusar a ata'));
  };

  const name = (value: string | null) => value ?? <span className="note">(Não definido)</span>;

  return (
    <Dialog
      title="Ver Ata"
      size="lg"
      onClose={onClose}
      footer={
        <>
          {ata && (
            <a className="btn btn--primary" href={meetingsApi.ataPdfUrl(card.id)} download>
              <Icon name="filePdf" />
              Download PDF
            </a>
          )}
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Fechar
          </button>
        </>
      }
    >
      {ata === undefined ? (
        <Loading label="A carregar dados da ata..." />
      ) : ata === null ? (
        <div className="mtg-empty">
          <Icon name="file" className="mtg-empty__icon" />
          <p className="mtg-empty__title">{error}</p>
        </div>
      ) : (
        <div className="mtg-ata">
          {ata.status === 'draft' && (
            <p className="mtg-info">
              <Icon name="lock" />
              Rascunho: só quem escreve esta ata a vê até ser publicada.
            </p>
          )}
          <Block icon="calendar" title="Informação da Reunião">
            <dl className="mtg-kv">
              <div>
                <dt>Tipo:</dt>
                <dd>{ata.meetingTypeLabel}</dd>
              </div>
              <div>
                <dt>Número da Ata:</dt>
                <dd>{ata.ataNumber ?? '—'}</dd>
              </div>
              <div>
                <dt>Data:</dt>
                <dd>{numericDateTime(ata.meetingDate)}</dd>
              </div>
              <div>
                <dt>Local:</dt>
                <dd>{ata.location}</dd>
              </div>
            </dl>
          </Block>

          <Block icon="people" title={ata.kind === 'cv' ? 'Constituição e Secretariado' : ata.kind === 'ag' ? 'Mesa da Assembleia Geral' : 'Constituição e Secretariado'}>
            <dl className="mtg-kv">
              {ata.kind === 'cv' ? (
                <>
                  <div>
                    <dt>Presidente do CV:</dt>
                    <dd>{name(ata.presidentName)}</dd>
                  </div>
                  <div>
                    <dt>Secretário:</dt>
                    <dd>{name(ata.firstSecretaryName)}</dd>
                  </div>
                </>
              ) : (
                <>
                  <div>
                    <dt>{ata.kind === 'ag' ? 'Presidente da Mesa:' : 'Presidente:'}</dt>
                    <dd>{name(ata.presidentName)}</dd>
                  </div>
                  <div>
                    <dt>1.º Secretário:</dt>
                    <dd>{name(ata.firstSecretaryName)}</dd>
                  </div>
                  <div>
                    <dt>2.º Secretário:</dt>
                    <dd>{name(ata.secondSecretaryName)}</dd>
                  </div>
                </>
              )}
            </dl>
          </Block>

          <Block icon="calendarCheck" title="Presenças">
            {ata.present.length > 0 ? (
              <>
                <p>
                  <strong>Presentes ({ata.present.length}):</strong>
                </p>
                <ul className="mtg-chips">
                  {ata.present.map((n, i) => (
                    <li key={`${n}-${i}`} className="mtg-chip mtg-chip--yes">
                      {n}
                    </li>
                  ))}
                </ul>
              </>
            ) : (
              <p className="note">Nenhum participante registado.</p>
            )}

            {ata.status === 'published' && ata.confirmed.length > 0 && (
              <div className="mtg-ata__split">
                <p>
                  <strong>Confirmaram a ata ({ata.confirmed.length}):</strong>
                </p>
                <ul className="mtg-chips">
                  {ata.confirmed.map((n, i) => (
                    <li key={`${n}-${i}`} className="mtg-chip">
                      {n}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {ata.status === 'published' && ata.mine && (
              <div className="mtg-ata__split">
                <p>
                  <strong>Confirmação da Ata:</strong>
                </p>
                {ata.mine.confirmed === null ? (
                  <span className="mtg-ata__answer">
                    <button type="button" className="btn btn--primary btn--sm" onClick={() => answer(true)} disabled={busy}>
                      <Icon name="checkCircle" />
                      Confirmar
                    </button>
                    <button type="button" className="btn btn--danger btn--sm" onClick={() => answer(false)} disabled={busy}>
                      <Icon name="xCircle" />
                      Recusar
                    </button>
                  </span>
                ) : (
                  <div className={ata.mine.confirmed ? 'mtg-feedback mtg-feedback--ok' : 'mtg-feedback mtg-feedback--no'}>
                    <Icon name={ata.mine.confirmed ? 'checkCircle' : 'xCircle'} />
                    <strong>{ata.mine.confirmed ? 'Confirmaste esta ata' : 'Recusaste esta ata'}</strong>
                    {ata.mine.notes && <small className="mtg-feedback__note">{ata.mine.notes}</small>}
                  </div>
                )}
              </div>
            )}
            <Banner text={error} />
          </Block>

          {ata.agendaPoints.length > 0 && (
            <Block icon="listOl" title="Ordem de Trabalhos">
              <ol className="mtg-points">
                {ata.agendaPoints.map((p) => (
                  <li key={p.number} className="mtg-point">
                    <strong className="mtg-point__title">
                      {p.number}. {p.title}
                    </strong>
                    {p.discussion && (
                      <p className="mtg-pre">
                        <strong>Discussão:</strong> {p.discussion}
                      </p>
                    )}
                    {p.decision && (
                      <p className="mtg-pre">
                        <strong>Deliberação:</strong> {p.decision}
                      </p>
                    )}
                    {(p.votesFor !== null || p.votesAgainst !== null || p.votesAbstain !== null) && (
                      <span className="mtg-tally">
                        <span className="pill pill--yes">A favor: {p.votesFor ?? 0}</span>
                        <span className="pill pill--no">Contra: {p.votesAgainst ?? 0}</span>
                        <span className="pill">Abstenções: {p.votesAbstain ?? 0}</span>
                        {p.result && <span className={p.result === 'Aprovado' ? 'pill pill--yes' : 'pill pill--no'}>{p.result}</span>}
                      </span>
                    )}
                  </li>
                ))}
              </ol>
            </Block>
          )}

          {ata.closingText && (
            <Block icon="journal" title="Encerramento">
              <p className="mtg-pre">{ata.closingText}</p>
            </Block>
          )}
        </div>
      )}
    </Dialog>
  );
}
