import { useEffect, useId, useMemo, useState } from 'react';
import { Icon } from '../components/icons';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { AskDialog } from '../components/AskDialog';
import { ConfirmAction } from '../components/QuestionConfirm';
import { type Question, type Recipient, api } from '../data/questionsApi';
import { QuestionDialog } from '../components/QuestionDialog';
import { QuestionList } from '../components/QuestionList';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';
import { SearchField, SelectField } from '../components/ui/SearchTools';

/**
 * /questions - Perguntas aos Órgãos Sociais. Any signed-in member asks a question to
 * whoever holds a position in the Órgãos Sociais; the two then take turns replying. The author closes, deletes, or
 * reminds the recipient while the answer is pending.
 */
export default function Questions() {
  const [session, setSession] = useState<'signin' | 'failed' | 'ok'>();
  const [recipients, setRecipients] = useState<Recipient[]>([]);
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [recipient, setRecipient] = useState('');
  const [version, setVersion] = useState(0);
  // ?question={id} (from "As minhas ações pendentes") opens that conversation once; the API decides access.
  const [open, setOpen] = useState<number | undefined>(() => Number(new URLSearchParams(location.search).get('question')) || undefined);
  const [asking, setAsking] = useState(false);
  const [action, setAction] = useState<{ kind: 'close' | 'remind' | 'delete'; question: Question }>();
  const ids = { q: useId(), who: useId() };

  useEffect(() => {
    document.title = 'Perguntas · RTUB';
    api.recipients().then((o) => {
      if (o.kind === 'ok') {
        setRecipients(o.data);
        setSession('ok');
      } else setSession(o.kind === 'signin' ? 'signin' : 'failed');
    });
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  // One filter entry per member (the same member can hold more than one position).
  const filterOptions = useMemo(() => {
    const seen = new Map<string, string>();
    for (const r of recipients) seen.set(r.id, seen.has(r.id) ? `${seen.get(r.id)}, ${r.role}` : `${r.displayName} - ${r.role}`);
    return [...seen.entries()].map(([id, label]) => ({ id, label }));
  }, [recipients]);

  const refresh = () => setVersion((v) => v + 1);

  return (
    <section className="page wrap questions-page" aria-labelledby="questions-title">
      <PageHead plain eyebrow="Área de membros" title="Perguntas aos Órgãos Sociais" titleId="questions-title" lead="Pergunta diretamente a quem tem o cargo; a conversa fica aqui até a fechares." />

      {session === 'signin' ? (
        <SignInNotice path={'/questions'}>As perguntas são da área de membros.</SignInNotice>
      ) : session === 'failed' ? (
        <RetryNotice onRetry={() => location.reload()}>Não conseguimos abrir as perguntas agora.</RetryNotice>
      ) : !session ? (
        <Loading label="A carregar as perguntas…" />
      ) : (
        <>
          <div className="events-tools qs-tools" role="search">
            <SearchField id={ids.q} label="Procurar perguntas" placeholder="Procurar perguntas…" value={text} onChange={setText} />
            <SelectField id={ids.who} label="Para quem" value={recipient} onChange={setRecipient}>
              <option value="">Todos os membros</option>
              {filterOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.label}
                </option>
              ))}
            </SelectField>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setAsking(true)}>
              <Icon name="plus" />
              Nova pergunta
            </button>
          </div>

          <QuestionList closed={false} q={q} recipient={recipient} version={version} onOpen={setOpen} onAction={setAction} />
          <QuestionList closed q={q} recipient={recipient} version={version} onOpen={setOpen} onAction={setAction} />

          {open !== undefined && (
            <QuestionDialog
              id={open}
              onClose={() => setOpen(undefined)}
              onChanged={refresh}
              onAction={(a) => {
                setOpen(undefined);
                setAction(a);
              }}
            />
          )}
          {asking && (
            <AskDialog
              recipients={recipients}
              onClose={() => setAsking(false)}
              onAsked={(id) => {
                setAsking(false);
                refresh();
                setOpen(id);
              }}
            />
          )}
          {action && (
            <ConfirmAction
              action={action.kind}
              question={action.question}
              onClose={() => setAction(undefined)}
              onDone={() => {
                setAction(undefined);
                refresh();
              }}
            />
          )}
        </>
      )}
    </section>
  );
}
