// The paged question list of /questions.
import { PersonLine } from './ui/PersonLine';
import { SimplePager } from './ui/SimplePager';
import { useEffect, useId, useState } from 'react';
import { Icon } from './icons';
import { type Question, type QuestionPage, api, stamp, statusLabel, statusPill } from '../data/questionsApi';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';

export function QuestionList({
  closed,
  q,
  recipient,
  version,
  onOpen,
  onAction,
}: {
  closed: boolean;
  q: string;
  recipient: string;
  version: number;
  onOpen: (id: number) => void;
  onAction: (a: { kind: 'close' | 'remind' | 'delete'; question: Question }) => void;
}) {
  const id = useId();
  const [page, setPage] = useState(1);
  const [data, setData] = useState<QuestionPage | null>();

  useEffect(() => setPage(1), [q, recipient]);
  useEffect(() => {
    let live = true; // a slower answer for an older query never replaces the newer one
    api.page(closed, q, recipient, page).then((o) => live && setData(o.kind === 'ok' ? o.data : null));
    return () => {
      live = false;
    };
  }, [closed, q, recipient, page, version]);

  // The closed list only appears once there is something in it.
  if (closed && data && data.total === 0 && page === 1) return null;

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <section className="rq-group" aria-labelledby={id}>
      <h2 id={id} className="rq-group__title">
        {closed ? 'Perguntas fechadas' : 'Perguntas abertas'} {data && <span className="note">({data.total})</span>}
      </h2>
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null ? (
        <Banner text="Não foi possível carregar esta lista." />
      ) : data.items.length === 0 ? (
        <p className="note">{q || recipient ? 'Nenhuma pergunta corresponde à pesquisa.' : 'Ainda não há perguntas abertas.'}</p>
      ) : (
        <>
          <ul className="rq-grid">
            {data.items.map((x) => (
              <li key={x.id} className="rq-card qs-card">
                <div className="qs-card__people">
                  <PersonLine name={x.author.displayName} avatar={x.author.avatarUrl} />
                  <Icon name="arrow" className="qs-card__to" />
                  <PersonLine name={x.recipient.displayName} avatar={x.recipient.avatarUrl} note={x.recipient.role} />
                </div>
                <div className="rq-card__head">
                  <strong className="rq-card__title">{x.title}</strong>
                  <span className={statusPill[x.status]}>{statusLabel[x.status]}</span>
                </div>
                <p className="qs-card__text">{x.content}</p>
                <p className="note">
                  {stamp(x.createdAt)} · {x.replyCount === 1 ? '1 resposta' : `${x.replyCount} respostas`}
                </p>
                <div className="rq-card__actions">
                  <button type="button" className={x.canReply ? 'btn btn--primary btn--sm' : 'btn btn--ghost btn--sm'} onClick={() => onOpen(x.id)}>
                    <Icon name={x.canReply ? 'chat' : 'search'} />
                    {x.canReply ? 'Responder' : 'Ver'}
                  </button>
                  {x.canRemind && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => onAction({ kind: 'remind', question: x })}>
                      <Icon name="bell" />
                      Lembrar
                    </button>
                  )}
                  {x.isMine && x.status !== 'closed' && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => onAction({ kind: 'close', question: x })}>
                      <Icon name="check" />
                      Fechar
                    </button>
                  )}
                  {x.isMine && (
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onAction({ kind: 'delete', question: x })}>
                      <Icon name="trash" />
                      <span className="sr-only">Eliminar a pergunta «{x.title}»</span>
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          <SimplePager page={page} pages={pages} onPage={setPage} />
        </>
      )}
    </section>
  );
}
