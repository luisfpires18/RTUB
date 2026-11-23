import { useEffect, useState } from 'react';
import { trintunaApi, problem, type Board, type Gathering } from '../data/trintunaApi';
import { GatheringDialog, AccessDialog, HallOfFameDialog } from '../components/TrintunaDialogs';
import { GatheringForm } from '../components/TrintunaForm';
import { PastRow, UpcomingCard, type Tools } from '../components/TrintunaRows';
import { Icon } from '../components/icons';
import { Notice, RetryNotice, SignInNotice } from '../components/Notice';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';
import { EmptyBlock, EmptyNote } from '../components/ui/EmptyNote';
import { AsyncConfirmDialog } from '../components/ui/ConfirmDialog';

// The shared link /trintuna?almoco={id} opens that gathering; the dialog keeps the query in step (the Gallery pattern).
const almocoFromUrl = () => Number(new URLSearchParams(location.search).get('almoco')) || undefined;

function setAlmocoInUrl(id?: number) {
  const url = new URL(location.href);
  if (id) url.searchParams.set('almoco', String(id));
  else url.searchParams.delete('almoco');
  history.replaceState(null, '', url.pathname + url.search);
}

/**
 * /trintuna - the Trintuna gatherings ("Nº Almoço"). The Owner and the members the Owner lists only: anyone else gets
 * the "no access" notice (the API answers 404). Próximos with their date poll and Vou / Não vou, then Passados
 * (Fui / Não fui). "Hall of Fame" ranks everyone by past almoços. ?almoco={id} opens one gathering.
 * Everything is decided by GET /api/trintuna.
 */
export default function Trintuna() {
  const [board, setBoard] = useState<Board | 'signin' | 'notfound' | null>();
  const [open, setOpen] = useState<number | undefined>(almocoFromUrl);
  const [editing, setEditing] = useState<number | 'new'>();
  const [deleting, setDeleting] = useState<Gathering>();
  const [access, setAccess] = useState(false);
  const [hall, setHall] = useState(false);

  useEffect(() => {
    document.title = 'Trintuna · RTUB';
  }, []);

  const load = (quiet = false) => {
    if (!quiet) setBoard(undefined);
    trintunaApi.board().then((o) => {
      if (o.kind === 'ok') setBoard(o.data);
      else if (o.kind === 'signin' || o.kind === 'notfound') setBoard(o.kind);
      else if (!quiet) setBoard(null);
    });
  };
  useEffect(() => load(), []);

  const data = board && typeof board === 'object' ? board : undefined;
  const all = data ? [...data.upcoming, ...data.past] : [];
  const find = (id?: number) => all.find((g) => g.id === id);
  const tools = (g: Gathering): Tools => ({
    onEdit: g.canEdit ? () => setEditing(g.id) : undefined,
    onDelete: data?.canManage ? () => setDeleting(g) : undefined,
  });
  const opened = find(open);
  const show = (id?: number) => {
    setOpen(id);
    setAlmocoInUrl(id);
  };

  // A shared link to a gathering that is not on the board (deleted, wrong id): drop the query, no dialog.
  useEffect(() => {
    if (data && open !== undefined && !opened) show(undefined);
  }, [data, open, opened]);
  const edited = editing === 'new' ? undefined : find(editing);

  const remove = async () => {
    if (!deleting) return;
    const o = await trintunaApi.remove(deleting.id);
    if (o.kind !== 'ok') return problem(o, 'apagar o almoço');
    setDeleting(undefined);
    load(true);
  };

  return (
    <section className="page wrap events-page" aria-labelledby="trintuna-title">
      <PageHead eyebrow="Tuna" titleId="trintuna-title" title="Trintuna" lead="Os almoços da Trintuna: onde, quando e quem vai.">
        {data && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setHall(true)}>
              <Icon name="trophy" />
              Hall of Fame
            </button>
            {data.canManage && (
              <>
                <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing('new')}>
                  <Icon name="plus" />
                  Novo almoço
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAccess(true)}>
                  <Icon name="people" />
                  Quem tem acesso
                </button>
              </>
            )}
          </div>
        )}
      </PageHead>

      {board === undefined ? (
        <Loading label="A carregar os almoços…" />
      ) : board === 'signin' ? (
        <SignInNotice path="/trintuna">Entre para ver os almoços da Trintuna.</SignInNotice>
      ) : board === 'notfound' ? (
        <Notice>Não tem acesso a esta página.</Notice>
      ) : board === null ? (
        <RetryNotice onRetry={() => load()}>Não conseguimos abrir os almoços neste momento.</RetryNotice>
      ) : (
        <>
          <section className="events-block" aria-labelledby="upcoming-title">
            <h2 id="upcoming-title" className="events-block__title">
              Próximos
            </h2>
            {board.upcoming.length === 0 ? (
              <EmptyBlock className="upcoming-empty" icon="calendar">
                <p>Sem almoços marcados por agora.</p>
              </EmptyBlock>
            ) : (
              <ul className="agenda">
                {board.upcoming.map((g) => (
                  <li key={g.id}>
                    <UpcomingCard g={g} onOpen={() => show(g.id)} tools={tools(g)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="events-block" aria-labelledby="past-title">
            <h2 id="past-title" className="events-block__title">
              Passados
            </h2>
            {board.past.length === 0 ? (
              <EmptyNote>Ainda não há almoços passados.</EmptyNote>
            ) : (
              <div className="archive">
                <ul className="archive__list">
                  {board.past.map((g) => (
                    <li key={g.id}>
                      <PastRow g={g} onOpen={() => show(g.id)} tools={tools(g)} />
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
        </>
      )}

      {opened && <GatheringDialog gathering={opened} onClose={() => show(undefined)} onChanged={() => load(true)} />}
      {editing !== undefined && (editing === 'new' || edited) && (
        <GatheringForm gathering={edited} canManage={Boolean(data?.canManage)} onClose={() => setEditing(undefined)} onSaved={() => load(true)} />
      )}
      {access && <AccessDialog onClose={() => setAccess(false)} onSaved={() => load(true)} />}
      {hall && data && <HallOfFameDialog ranks={data.hallOfFame} onClose={() => setHall(false)} />}
      {deleting && (
        <AsyncConfirmDialog title="Apagar almoço" confirmLabel="Apagar" confirmIcon="trash" danger onClose={() => setDeleting(undefined)} onConfirm={remove}>
          <p>
            O {deleting.title} ({deleting.place}) é apagado com as datas possíveis, os votos e as respostas. Não é possível recuperá-lo.
          </p>
        </AsyncConfirmDialog>
      )}
    </section>
  );
}
