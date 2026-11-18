// One question with its replies and answer form.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { type Detail, MAX_CONTENT, type Question, api, problem, stamp, statusLabel, statusPill } from '../data/questionsApi';
import { Loading } from './ui/Loading';
import { Avatar } from './ui/Avatar';

export function QuestionDialog({
  id,
  onClose,
  onChanged,
  onAction,
}: {
  id: number;
  onClose: () => void;
  onChanged: () => void;
  onAction: (a: { kind: 'close' | 'remind' | 'delete'; question: Question }) => void;
}) {
  const replyId = useId();
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  const [text, setText] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.detail(id).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return setError('Escreva uma resposta.');
    setBusy(true);
    const o = await api.reply(id, text);
    setBusy(false);
    if (o.kind === 'ok') {
      setDetail(o.data);
      setText('');
      setError(undefined);
      onChanged();
    } else setError(problem(o, 'enviar a resposta'));
  };

  const d = detail && typeof detail === 'object' ? detail : null;
  const x = d?.question;

  return (
    <Dialog
      title="Pergunta"
      size="lg"
      onClose={onClose}
      footer={
        <>
          {x?.isMine && x.status !== 'closed' && (
            <button type="button" className="btn btn--ghost" onClick={() => onAction({ kind: 'close', question: x })}>
              Fechar a pergunta
            </button>
          )}
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Voltar
          </button>
        </>
      }
    >
      {detail === undefined ? (
        <Loading label="A carregar a pergunta…" />
      ) : detail === 'missing' ? (
        <p className="note">Esta pergunta já não existe.</p>
      ) : !d || !x ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar a pergunta.
        </p>
      ) : (
        <div className="qs-detail">
          <div className="qs-card__people">
            <span className="qs-person">
              <Avatar className="who__avatar" src={x.author.avatarUrl} size={36} />
              <span>
                <strong>{x.author.displayName}</strong>
                <small className="note">{stamp(x.createdAt)}</small>
              </span>
            </span>
            <span className={statusPill[x.status]}>{statusLabel[x.status]}</span>
          </div>
          <p className="note">
            Para <strong>{x.recipient.displayName}</strong> ({x.recipient.role})
          </p>
          <h3 className="qs-detail__title">{x.title}</h3>
          <p className="rq-message">{x.content}</p>

          <h3 className="member-section__title">
            Respostas <span className="note">({d.replies.length})</span>
          </h3>
          {d.replies.length === 0 ? (
            <p className="note">Ainda sem respostas.</p>
          ) : (
            <ul className="member-rows qs-replies">
              {d.replies.map((r) => (
                <li key={r.id} className={r.fromRecipient ? 'qs-reply qs-reply--recipient' : 'qs-reply'}>
                  <span className="qs-person">
                    <Avatar className="who__avatar" src={r.author.avatarUrl} size={32} />
                    <span>
                      <strong>{r.author.displayName}</strong>
                      {r.fromRecipient && <MemberBadge kind="position">{x.recipient.role}</MemberBadge>}
                      <small className="note">{stamp(r.createdAt)}</small>
                    </span>
                  </span>
                  <p className="rq-message">{r.content}</p>
                </li>
              ))}
            </ul>
          )}

          {x.canReply ? (
            <form className="form" onSubmit={submit} noValidate>
              <div className={error ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={replyId}>A tua resposta</label>
                <textarea
                  id={replyId}
                  rows={4}
                  maxLength={MAX_CONTENT}
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  aria-invalid={error ? true : undefined}
                />
                {error && (
                  <p className="form__error" role="alert">
                    {error}
                  </p>
                )}
              </div>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !text.trim()}>
                {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                <Icon name="send" />
                Responder
              </button>
            </form>
          ) : x.status === 'closed' ? (
            <p className="note">Esta pergunta foi fechada pelo autor; já não aceita respostas.</p>
          ) : x.isMine ? (
            <p className="note">Aguarda a resposta de {x.recipient.displayName}.</p>
          ) : (
            <p className="note">Só {x.recipient.displayName} e o autor da pergunta escrevem aqui, à vez.</p>
          )}
        </div>
      )}
    </Dialog>
  );
}
