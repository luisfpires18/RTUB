import { rehearsalActivity, CalendarOfferDialog } from '../components/CalendarOffer';
import { useEffect, useState } from 'react';
import { Notice, SignInNotice } from '../components/Notice';
import { weekday, dateLabel } from '../data/eventsApi';
import type { Outcome } from '../data/api';
import { Icon } from '../components/icons';
import { rehearsalsApi, type RehearsalDetail as Detail } from '../data/rehearsalsApi';
import { CancelDialog, NoticeDialog } from '../components/RehearsalCancelNotice';
import { PresenceDialog } from '../components/RehearsalPresence';
import { PresencePill } from '../components/RehearsalShared';
import { RehearsalFormDialog } from '../components/RehearsalFormDialogs';
import { Counts, Group, AddAttendee } from '../components/RehearsalAttendees';
import { Loading } from '../components/ui/Loading';
import { AnswerActions } from '../components/ui/AnswerActions';
import { BackLink } from '../components/ui/BackLink';
import { FieldError } from '../components/ui/FieldError';
import { EmptyNote } from '../components/ui/EmptyNote';

/**
 * /rehearsals/{id} - one rehearsal: its details, the member's own presença and everyone's
 * presenças (members going, Leitões, not going) with what they play. Admin/Owner edit, notify, cancel or delete it,
 * and once it can be approved confirm, remove or add presenças. Every rule is the server's.
 */
export default function RehearsalDetail({ rehearsalId }: { rehearsalId: number }) {
  const [detail, setDetail] = useState<Detail | 'missing' | 'signin' | null>();
  const [dialog, setDialog] = useState<'presence' | 'edit' | 'notice' | 'cancel' | 'calendar'>();
  const [confirm, setConfirm] = useState<'delete' | 'reactivate'>();
  const [error, setError] = useState<string>();

  const load = (quiet = false) => {
    if (!quiet) setDetail(undefined);
    rehearsalsApi.rehearsal(rehearsalId).then((o) =>
      setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : o.kind === 'signin' ? 'signin' : null),
    );
  };
  useEffect(() => load(), [rehearsalId]);

  const loaded = detail && typeof detail === 'object' ? detail : null;
  useEffect(() => {
    document.title = loaded ? `Ensaio de ${dateLabel({ date: loaded.rehearsal.date, endDate: null })} · RTUB` : 'Ensaio · RTUB';
  }, [loaded]);

  const act = async (call: Promise<Outcome<void>>, after: () => void) => {
    setError(undefined);
    const o = await call;
    setConfirm(undefined);
    if (o.kind === 'ok') after();
    else setError(o.kind === 'closed' ? 'Este ensaio já não permite isto.' : 'Não foi possível concluir. Tenta outra vez.');
  };

  const back = (
    <BackLink href="/rehearsals">Ensaios</BackLink>
  );

  if (detail === undefined) return <section className="page wrap">{back}<Loading label="A carregar o ensaio…" /></section>;
  if (detail === 'signin' || detail === 'missing' || detail === null) {
    return (
      <section className="page wrap">
        {back}
        {detail === 'signin' ? (
          <SignInNotice path={`/rehearsals/${rehearsalId}`}>Os ensaios são da área de membros.</SignInNotice>
        ) : (
          <Notice>{detail === 'missing' ? 'Este ensaio não existe ou já foi apagado.' : 'Não conseguimos abrir este ensaio agora.'}</Notice>
        )}
      </section>
    );
  }

  const r = detail.rehearsal;
  const day = dateLabel({ date: r.date, endDate: null });
  const open = !r.past && !r.cancelled;
  const a = detail.attendance;

  return (
    <section className="page wrap event-page rehearsal-page" aria-labelledby="rehearsal-title">
      <div className="event-topbar">
        {back}
        {detail.canEdit && (
          <span className="event-topbar__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('edit')}>
              <Icon name="pencil" />
              Editar
            </button>
            {detail.canManage && open && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('notice')}>
                <Icon name="bell" />
                Notificar
              </button>
            )}
            {detail.canManage && !r.cancelled && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('cancel')}>
                <Icon name="ban" />
                Cancelar ensaio
              </button>
            )}
            {detail.canManage && r.cancelled && !r.past && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirm('reactivate')}>
                <Icon name="restore" />
                Reativar
              </button>
            )}
            {detail.canManage && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirm('delete')}>
                <Icon name="trash" />
                Apagar
              </button>
            )}
          </span>
        )}
      </div>

      {confirm && (
        <div className="notice" role="alert">
          <p>
            {confirm === 'delete'
              ? `Apagar o ensaio de ${day}? As presenças vão com ele. Não dá para desfazer.`
              : 'Reativar este ensaio? As presenças removidas no cancelamento não voltam.'}
          </p>
          <AnswerActions
            cancelLabel="Não"
            onCancel={() => setConfirm(undefined)}
            label={confirm === 'delete' ? 'Apagar' : 'Reativar'}
            onConfirm={() =>
              confirm === 'delete'
                ? act(rehearsalsApi.remove(r.id), () => location.assign('/rehearsals'))
                : act(rehearsalsApi.reactivate(r.id), () => load(true))
            }
            danger={confirm === 'delete'}
            small
          />
        </div>
      )}
      <FieldError text={error} alert={false} />

      <header className="rehearsal-hero">
        <p className="eyebrow">Ensaio{r.cancelled && ' · cancelado'}</p>
        <h1 id="rehearsal-title" className="page__title">
          {r.theme ?? `Ensaio de ${weekday(r.date)}`}
        </h1>
        <ul className="rehearsal-hero__facts">
          <li>
            <Icon name="calendar" />
            {day}
          </li>
          <li>
            <Icon name="clock" />
            {r.start}–{r.end}
          </li>
          <li>
            <Icon name="geo" />
            {r.location}
          </li>
        </ul>
      </header>

      {r.cancelled && (
        <p className="warning">
          <Icon name="warning" />
          Ensaio cancelado{r.cancellationReason && `: ${r.cancellationReason}`}
        </p>
      )}

      <div className="event-layout">

        <div className="event-main">
          {(r.description || detail.notes) && (
            <section className="event-section" aria-labelledby="about-title">
              <h2 id="about-title" className="event-section__title">
                Sobre o ensaio
              </h2>
              {r.description && <p className="talk__text">{r.description}</p>}
              {detail.notes && (
                <p className="talk__text note">
                  <strong>Notas: </strong>
                  {detail.notes}
                </p>
              )}
            </section>
          )}

          <section className="event-section" aria-labelledby="presences-title">
            <h2 id="presences-title" className="event-section__title">
              Presenças
            </h2>
            {detail.canManage && r.approvable && !r.cancelled && (
              <p className="note">Confirma as presenças de quem esteve no ensaio; podes também acrescentar quem não marcou.</p>
            )}
            {a.going.length + a.leitoes.length + a.notGoing.length === 0 ? (
              <EmptyNote>{r.cancelled ? 'As presenças foram removidas com o cancelamento.' : 'Ainda ninguém marcou presença.'}</EmptyNote>
            ) : (
              <>
                <Group title={r.past ? 'Foram' : 'Vão'} people={a.going} rehearsalId={r.id} onChanged={() => load(true)} />
                <Group title="Leitões" people={a.leitoes} rehearsalId={r.id} onChanged={() => load(true)} />
                {a.notGoing.length > 0 && (
                  <details className="who__more">
                    <summary>
                      {r.past ? 'Não foram' : 'Não vão'} · {a.notGoing.length}
                    </summary>
                    <Group title="" people={a.notGoing} rehearsalId={r.id} onChanged={() => load(true)} />
                  </details>
                )}
              </>
            )}
            {detail.canManage && r.approvable && !r.cancelled && <AddAttendee rehearsalId={r.id} onAdded={() => load(true)} />}
          </section>

          {(detail.instruments.length > 0 || detail.otherInstruments.length > 0) && (
            <section className="event-section" aria-labelledby="instruments-title">
              <h2 id="instruments-title" className="event-section__title">
                Instrumentos
              </h2>
              <Counts counts={detail.instruments} />
              {detail.otherInstruments.length > 0 && (
                <details className="who__more">
                  <summary>Outros instrumentos que tocam</summary>
                  <Counts counts={detail.otherInstruments} />
                </details>
              )}
            </section>
          )}
        </div>
        <aside className="member-panel" aria-labelledby="mine-title">
          <h2 id="mine-title" className="member-panel__title">
            A minha presença
          </h2>
          <div className="member-panel__answer">
            {r.mine ? <PresencePill rehearsal={r} /> : <span className="note">{open ? 'Ainda não marcaste.' : 'Sem presença.'}</span>}
            {r.mine?.instrument && <span className="note">{r.mine.instrument}</span>}
          </div>
          {(open || r.mine) && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setDialog('presence')}>
              {open ? (r.mine ? 'Alterar presença' : 'Marcar presença') : 'Ver a minha presença'}
            </button>
          )}
          {open && r.mine && r.mine.status !== 'notGoing' && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('calendar')}>
              <Icon name="calendar" />
              Adicionar ao calendário
            </button>
          )}
          <dl className="member-panel__counts">
            <div>
              <dt>{r.past ? 'Foram' : 'Vão'}</dt>
              <dd>{r.goingCount}</dd>
            </div>
            <div>
              <dt>{r.past ? 'Não foram' : 'Não vão'}</dt>
              <dd>{a.notGoing.length}</dd>
            </div>
            {detail.canManage && r.pendingCount > 0 && (
              <div>
                <dt>Por confirmar</dt>
                <dd>{r.pendingCount}</dd>
              </div>
            )}
          </dl>
        </aside>
      </div>

      {dialog === 'calendar' && <CalendarOfferDialog activity={rehearsalActivity(r)} headline={null} onClose={() => setDialog(undefined)} />}
      {dialog === 'presence' && <PresenceDialog rehearsalId={r.id} onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'edit' && <RehearsalFormDialog rehearsal={r} notes={detail.notes} onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'notice' && <NoticeDialog rehearsal={r} onClose={() => setDialog(undefined)} />}
      {dialog === 'cancel' && <CancelDialog rehearsal={r} onClose={() => setDialog(undefined)} onDone={() => load(true)} />}
    </section>
  );
}
